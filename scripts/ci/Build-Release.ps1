<#
.SYNOPSIS
    Builds the LearnSphere release package: a sanitised backend publish and a
    frontend bundle, each as a folder and a zip.

.DESCRIPTION
    This is the single source of truth for "what goes to the server". GitHub Actions
    calls it, and you can run it locally to get byte-for-byte the same output:

        pwsh ./scripts/ci/Build-Release.ps1 -ApiUrl 'http://103.233.3.146:1000/api'

    Steps:
      1. dotnet publish (Release, framework-dependent, win-x64 target so the
         LearnSphere.API.exe apphost is produced for IIS).
      2. Sanitise appsettings.json in the publish folder. The connection string, JWT key
         and allowed origin are replaced with SET_VIA_ENVIRONMENT placeholders so the
         package never carries a secret; the API refuses to start until the real values
         are supplied on the server (see docs/deployment-checklist.md).
      3. Copy frontend/ as-is, then append the API URL to config.js when -ApiUrl is given.
         Without -ApiUrl the frontend calls <same origin>/api, the reverse-proxy layout.
      4. Write a build-info.json into each package (commit, branch, time) so a server
         can always say which build it is running.
      5. Zip both packages.

    Works on Windows PowerShell 5.1 and PowerShell 7 (ubuntu runners use pwsh).

.PARAMETER OutputRoot
    Where to put artifacts. Default: ./artifacts (git-ignored).
.PARAMETER ApiUrl
    Absolute API URL baked into frontend/config.js. Leave empty for same-origin /api.
.PARAMETER SkipTests
    Skip `dotnet test`. CI runs tests in a separate job, so the CD workflow passes this.
.PARAMETER Configuration
    Build configuration. Default: Release.
#>
[CmdletBinding()]
param(
    [string] $OutputRoot = (Join-Path $PSScriptRoot '..\..\artifacts'),
    [string] $ApiUrl = '',
    [switch] $SkipTests,
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot    = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$apiProject  = Join-Path $repoRoot 'backend\LearnSphere.API\LearnSphere.API.csproj'
$testProject = Join-Path $repoRoot 'backend\LearnSphere.Tests\LearnSphere.Tests.csproj'
$frontendSrc = Join-Path $repoRoot 'frontend'

$OutputRoot  = [System.IO.Path]::GetFullPath($OutputRoot)
$backendOut  = Join-Path $OutputRoot 'backend'
$frontendOut = Join-Path $OutputRoot 'frontend'

function Write-Step([string] $Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Checked {
    # Native commands do not throw on a non-zero exit code; make them.
    param([scriptblock] $Command)
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code ${LASTEXITCODE}: $Command" }
}

# Deliberately no array splatting and no $LASTEXITCODE check: in Windows PowerShell 5.1,
# `& git @args 2>$null` reports exit code -1 even when git succeeded. An empty result is
# the failure signal instead.
function Get-GitCommit {
    try { $v = git rev-parse HEAD 2>$null; if ($v) { return "$v".Trim() } } catch { }
    return 'unknown'
}
function Get-GitBranch {
    try { $v = git rev-parse --abbrev-ref HEAD 2>$null; if ($v) { return "$v".Trim() } } catch { }
    return 'unknown'
}

# ---------------------------------------------------------------------------
Write-Step "Clean output: $OutputRoot"
if (Test-Path $OutputRoot) { Remove-Item $OutputRoot -Recurse -Force }
New-Item -ItemType Directory -Path $backendOut, $frontendOut -Force | Out-Null

# Build metadata. In Actions these env vars are set; locally fall back to git.
$commit  = if ($env:GITHUB_SHA)      { $env:GITHUB_SHA }      else { Get-GitCommit }
$branch  = if ($env:GITHUB_REF_NAME) { $env:GITHUB_REF_NAME } else { Get-GitBranch }
$runId   = if ($env:GITHUB_RUN_ID)   { $env:GITHUB_RUN_ID }   else { 'local' }
$buildInfo = [ordered]@{
    commit    = $commit
    branch    = $branch
    run       = $runId
    builtAt   = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    apiUrl    = if ($ApiUrl) { $ApiUrl } else { '<same-origin>/api' }
}
Write-Host ("Build: {0} @ {1} (run {2})" -f $branch, $commit.Substring(0, [Math]::Min(12, $commit.Length)), $runId)

# ---------------------------------------------------------------------------
Write-Step "dotnet restore"
Invoke-Checked { dotnet restore $apiProject --nologo }
Invoke-Checked { dotnet restore $testProject --nologo }

if (-not $SkipTests) {
    Write-Step "dotnet test"
    Invoke-Checked { dotnet test $testProject -c $Configuration --nologo --no-restore }
}

# ---------------------------------------------------------------------------
Write-Step "dotnet publish -> $backendOut"
# Framework-dependent: the server has the ASP.NET Core 9 hosting bundle, so the package
# stays small (~10 MB) and runtime patches are applied server-side by Windows Update /
# hosting-bundle upgrades. -r win-x64 with --self-contained false still produces the
# .exe apphost that IIS's web.config points at.
# No --no-restore here: a RID-specific publish needs a RID-specific restore, which the
# plain restore above did not do. Letting publish restore itself costs a few seconds.
Invoke-Checked {
    dotnet publish $apiProject `
        -c $Configuration `
        -r win-x64 `
        --self-contained false `
        -o $backendOut `
        --nologo `
        "-p:DebugType=None" `
        "-p:DebugSymbols=false"
}

# ---------------------------------------------------------------------------
Write-Step "Sanitise appsettings.json"
# Never ship the dev connection string or the sample JWT key. The placeholders are the
# same strings the previously hand-made publish/ folder used, so Program.cs's startup
# guard (Jwt:Key must not be the sample value, must be 32+ chars) still trips loudly
# if the server forgets to set the environment variables.
$settingsPath = Join-Path $backendOut 'appsettings.json'
$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
$settings.ConnectionStrings.DefaultConnection = 'SET_VIA_ENVIRONMENT ConnectionStrings__DefaultConnection'
$settings.Jwt.Key = 'SET_VIA_ENVIRONMENT Jwt__Key'
$settings.AllowedOrigins = @('SET_VIA_ENVIRONMENT AllowedOrigins__0')
if ($settings.PSObject.Properties['Smtp'] -and $settings.Smtp.PSObject.Properties['Password']) {
    $settings.Smtp.Password = ''
}
# Depth 10 keeps nested Logging/Smtp sections intact (default depth of 2 truncates).
$settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath -Encoding UTF8

# Belt and braces: fail the build if anything that looks like a real secret survived.
$leakPatterns = @('Password=LearnSphere', 'SuperSecret', 'Server=127.0.0.1')
$published = Get-Content $settingsPath -Raw
foreach ($p in $leakPatterns) {
    if ($published -like "*$p*") { throw "Sanitisation failed: '$p' still present in published appsettings.json" }
}
# Never ship a Development settings file either (it is git-ignored but may exist locally).
Remove-Item (Join-Path $backendOut 'appsettings.Development.json') -ErrorAction SilentlyContinue

# dotnet publish copies the whole wwwroot, including any uploads present on the build
# machine (tutors' ID documents on a dev box). Those are server-side data, never package
# content; Deploy-IIS.ps1 preserves the server's own wwwroot\uploads.
$uploadsOut = Join-Path $backendOut 'wwwroot\uploads'
if (Test-Path $uploadsOut) {
    $n = (Get-ChildItem $uploadsOut -Recurse -File | Measure-Object).Count
    Remove-Item $uploadsOut -Recurse -Force
    Write-Host "Removed wwwroot\uploads from the package ($n local file(s); server data stays on the server)"
}

# ---------------------------------------------------------------------------
Write-Step "Frontend -> $frontendOut"
# The frontend is plain AngularJS 1.x served as static files: no bundler, so "build" is a copy.
Copy-Item -Path (Join-Path $frontendSrc '*') -Destination $frontendOut -Recurse -Force

if ($ApiUrl) {
    $configPath = Join-Path $frontendOut 'config.js'
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
    Add-Content -Path $configPath -Encoding UTF8 -Value @"

// Set by scripts/ci/Build-Release.ps1 on $stamp (commit $($commit.Substring(0, [Math]::Min(12, $commit.Length)))).
window.LEARNSPHERE_API_URL = '$ApiUrl';
"@
    Write-Host "config.js -> window.LEARNSPHERE_API_URL = '$ApiUrl'"
} else {
    Write-Host "config.js left as-is (frontend will call <same origin>/api)"
}

# ---------------------------------------------------------------------------
Write-Step "Write build-info.json"
$buildInfoJson = $buildInfo | ConvertTo-Json
Set-Content (Join-Path $backendOut  'build-info.json') $buildInfoJson -Encoding UTF8
Set-Content (Join-Path $frontendOut 'build-info.json') $buildInfoJson -Encoding UTF8

# ---------------------------------------------------------------------------
Write-Step "Zip"
$shortSha = $commit.Substring(0, [Math]::Min(7, $commit.Length))
$backendZip  = Join-Path $OutputRoot "learnsphere-backend-$shortSha.zip"
$frontendZip = Join-Path $OutputRoot "learnsphere-frontend-$shortSha.zip"
Compress-Archive -Path (Join-Path $backendOut  '*') -DestinationPath $backendZip  -Force
Compress-Archive -Path (Join-Path $frontendOut '*') -DestinationPath $frontendZip -Force

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Get-ChildItem $OutputRoot -File | ForEach-Object {
    "{0,-45} {1,8:N0} KB" -f $_.Name, ($_.Length / 1KB)
}
