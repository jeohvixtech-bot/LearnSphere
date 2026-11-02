<#
.SYNOPSIS
    Deploys a LearnSphere release package (from Build-Release.ps1) onto the IIS server,
    with backup, upload preservation, smoke test and automatic rollback.

.DESCRIPTION
    Runs ON the IIS server (via the self-hosted GitHub Actions runner, or by hand as an
    administrator). It is idempotent and safe to re-run.

    For each site (backend, frontend):
      1. Stop the app pool and drop app_offline.htm so IIS releases file locks
         (the API is hosted in-process, so the worker process owns the DLLs).
      2. Back up the current site folder to <BackupRoot>\<timestamp>\<site>\.
         wwwroot\uploads is excluded: it is never overwritten (step 3), and it holds
         tutors' ID documents, which should not be duplicated around the disk.
      3. robocopy /MIR the package over the site folder, EXCEPT:
           - wwwroot\uploads\            user-uploaded files live on the server only
           - appsettings.Production.json server-side secrets, never in the package
           - logs\                       stdout logs, if enabled
      4. Remove app_offline.htm and start the app pool.
    Then:
      5. Smoke test: GET -SmokeUrl must return HTTP 200 within -SmokeTimeoutSeconds.
      6. If the smoke test fails and -NoRollback is not set, restore both sites from the
         backup taken in step 2, restart, and exit non-zero so the workflow goes red.
      7. Prune backups older than the newest -KeepBackups.

    Secrets: the package's appsettings.json carries placeholders. Supply the real values as
    app-pool / machine environment variables, OR as an appsettings.Production.json placed
    once in the API folder on the server (it is preserved across deployments). See
    docs/deployment-checklist.md.

.EXAMPLE
    # Typical manual run on the server after extracting the two zips:
    .\Deploy-IIS.ps1 -BackendPackage C:\deploy\backend -FrontendPackage C:\deploy\frontend `
        -SmokeUrl 'http://localhost:1000/api/admin/scoring-weightages'

.EXAMPLE
    # Dry run on any machine without IIS, to exercise the copy/backup/rollback logic:
    .\Deploy-IIS.ps1 -BackendPackage .\artifacts\backend -FrontendPackage .\artifacts\frontend `
        -ApiSitePath C:\tmp\api -FrontendSitePath C:\tmp\web -BackupRoot C:\tmp\bak `
        -SkipAppPool -SmokeUrl ''
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $BackendPackage,
    [Parameter(Mandatory)] [string] $FrontendPackage,

    [string] $ApiSitePath      = 'C:\inetpub\learnsphere-api',
    [string] $FrontendSitePath = 'C:\inetpub\learnsphere',
    [string] $ApiAppPool       = 'learnsphere-api',
    [string] $FrontendAppPool  = 'learnsphere',
    [string] $BackupRoot       = 'C:\inetpub\backups\learnsphere',
    [int]    $KeepBackups      = 5,

    # Anonymous endpoint that also touches the database. Empty string disables the check.
    [string] $SmokeUrl            = 'http://localhost:1000/api/admin/scoring-weightages',
    [int]    $SmokeTimeoutSeconds = 90,

    [switch] $NoRollback,
    # For local dry runs on a machine without IIS: skip all WebAdministration calls.
    [switch] $SkipAppPool
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step([string] $Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# ---------------------------------------------------------------------------
# IIS helpers. WebAdministration ships with IIS (Windows PowerShell 5.1); on PowerShell 7
# it loads through the Windows compatibility shim, which is why -UseWindowsPowerShell is
# tried as a fallback.
function Import-IisModule {
    if ($SkipAppPool) { return }
    if (Get-Module WebAdministration) { return }
    try   { Import-Module WebAdministration -ErrorAction Stop }
    catch { Import-Module WebAdministration -UseWindowsPowerShell -ErrorAction Stop }
}

function Set-AppPoolState([string] $Name, [ValidateSet('Started', 'Stopped')] [string] $Desired) {
    if ($SkipAppPool) { Write-Host "  (SkipAppPool) would set app pool '$Name' -> $Desired"; return }
    if (-not (Test-Path "IIS:\AppPools\$Name")) { throw "App pool '$Name' does not exist on this server" }

    $state = (Get-WebAppPoolState -Name $Name).Value
    if ($state -eq $Desired) { Write-Host "  app pool '$Name' already $Desired"; return }

    if ($Desired -eq 'Stopped') { Stop-WebAppPool  -Name $Name } else { Start-WebAppPool -Name $Name }

    # Stop/Start return immediately; the worker process shuts down asynchronously.
    $deadline = (Get-Date).AddSeconds(60)
    do {
        Start-Sleep -Milliseconds 500
        $state = (Get-WebAppPoolState -Name $Name).Value
    } while ($state -ne $Desired -and (Get-Date) -lt $deadline)

    if ($state -ne $Desired) { throw "App pool '$Name' did not reach state $Desired within 60 s (is $state)" }
    Write-Host "  app pool '$Name' -> $state"
}

# ---------------------------------------------------------------------------
# File helpers
# robocopy exit codes are bit flags: 1 = files copied, 2 = extra files in destination,
# 4 = mismatches; anything >= 8 means a copy failed. 0 means nothing needed doing.
function Describe-RobocopyExit([int] $Code) {
    if ($Code -ge 8) { return 'FAILED' }
    if ($Code -eq 0) { return 'nothing to copy' }
    $parts = @()
    if ($Code -band 1) { $parts += 'files copied' }
    if ($Code -band 2) { $parts += 'extras removed/present' }
    if ($Code -band 4) { $parts += 'mismatches' }
    return ($parts -join ', ')
}

# Quiet flags (/NFL /NDL /NJH /NJS /NP) keep the deploy log to one line per copy; add
# -Verbose to this script to see robocopy's own summary.
$script:RobocopyQuiet = @('/R:3', '/W:2', '/NFL', '/NDL', '/NJH', '/NP')
if (-not $PSBoundParameters.ContainsKey('Verbose')) { $script:RobocopyQuiet += '/NJS' }

function Invoke-Robocopy([string] $Source, [string] $Destination, [string[]] $ExcludeDirs, [string[]] $ExcludeFiles) {
    $robocopyArgs = @($Source, $Destination, '/MIR') + $script:RobocopyQuiet
    if ($ExcludeDirs)  { $robocopyArgs += '/XD'; $robocopyArgs += ($ExcludeDirs  | ForEach-Object { Join-Path $Destination $_ }) }
    if ($ExcludeFiles) { $robocopyArgs += '/XF'; $robocopyArgs += $ExcludeFiles }

    & robocopy @robocopyArgs | Out-Host
    $code = $LASTEXITCODE
    if ($code -ge 8) { throw "robocopy $Source -> $Destination failed with exit code $code" }
    Write-Host "  mirrored -> $Destination (robocopy ${code}: $(Describe-RobocopyExit $code))"
    $global:LASTEXITCODE = 0
}

function Backup-Site([string] $SitePath, [string] $BackupDir) {
    if (-not (Test-Path $SitePath)) { Write-Host "  nothing to back up at $SitePath (first deployment)"; return }
    New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
    # Not /MIR here: a plain /E copy into an empty folder, excluding uploads and logs.
    $robocopyArgs = @($SitePath, $BackupDir, '/E') + $script:RobocopyQuiet +
                    @('/XD', (Join-Path $SitePath 'wwwroot\uploads'), (Join-Path $SitePath 'logs'))
    & robocopy @robocopyArgs | Out-Host
    $code = $LASTEXITCODE
    if ($code -ge 8) { throw "backup of $SitePath failed with robocopy exit code $code" }
    $global:LASTEXITCODE = 0
    Write-Host "  backed up $SitePath -> $BackupDir (robocopy ${code}: $(Describe-RobocopyExit $code))"
}

function Test-Smoke([string] $Url, [int] $TimeoutSeconds) {
    if (-not $Url) { Write-Host "  smoke test disabled"; return $true }
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $attempt  = 0
    do {
        $attempt++
        try {
            $resp = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 15
            if ($resp.StatusCode -eq 200) {
                Write-Host "  OK  HTTP 200 from $Url (attempt $attempt)"
                return $true
            }
            Write-Host "  HTTP $($resp.StatusCode) from $Url (attempt $attempt)"
        } catch {
            # The first requests after an app-pool start usually fail while the in-process
            # host is still booting (and running its schema ladder). Keep trying.
            Write-Host "  waiting... ($($_.Exception.Message.Split([Environment]::NewLine)[0]))"
        }
        Start-Sleep -Seconds 3
    } while ((Get-Date) -lt $deadline)
    return $false
}

# ---------------------------------------------------------------------------
# Main
$BackendPackage  = (Resolve-Path $BackendPackage).Path
$FrontendPackage = (Resolve-Path $FrontendPackage).Path
foreach ($required in @((Join-Path $BackendPackage 'LearnSphere.API.dll'), (Join-Path $BackendPackage 'web.config'),
                        (Join-Path $FrontendPackage 'index.html'), (Join-Path $FrontendPackage 'web.config'))) {
    if (-not (Test-Path $required)) { throw "Package incomplete: missing $required" }
}

$stamp     = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupDir = Join-Path $BackupRoot $stamp
$sites = @(
    @{ Name = 'backend';  Package = $BackendPackage;  Path = $ApiSitePath;      Pool = $ApiAppPool;
       ExcludeDirs = @('wwwroot\uploads', 'logs'); ExcludeFiles = @('appsettings.Production.json', 'app_offline.htm') },
    @{ Name = 'frontend'; Package = $FrontendPackage; Path = $FrontendSitePath; Pool = $FrontendAppPool;
       ExcludeDirs = @();                           ExcludeFiles = @('app_offline.htm') }
)

Import-IisModule

$buildInfoPath = Join-Path $BackendPackage 'build-info.json'
if (Test-Path $buildInfoPath) { Write-Host "Deploying build:"; Get-Content $buildInfoPath | Out-Host }

try {
    foreach ($site in $sites) {
        Write-Step "[$($site.Name)] stop"
        New-Item -ItemType Directory -Path $site.Path -Force | Out-Null
        Set-Content -Path (Join-Path $site.Path 'app_offline.htm') -Value '<html><body>Deploying, back in a minute.</body></html>'
        Set-AppPoolState -Name $site.Pool -Desired Stopped

        Write-Step "[$($site.Name)] backup"
        Backup-Site -SitePath $site.Path -BackupDir (Join-Path $backupDir $site.Name)

        Write-Step "[$($site.Name)] copy package -> $($site.Path)"
        Invoke-Robocopy -Source $site.Package -Destination $site.Path -ExcludeDirs $site.ExcludeDirs -ExcludeFiles $site.ExcludeFiles

        Write-Step "[$($site.Name)] start"
        Remove-Item (Join-Path $site.Path 'app_offline.htm') -ErrorAction SilentlyContinue
        Set-AppPoolState -Name $site.Pool -Desired Started
    }

    Write-Step "Smoke test"
    if (-not (Test-Smoke -Url $SmokeUrl -TimeoutSeconds $SmokeTimeoutSeconds)) {
        throw "Smoke test failed: no HTTP 200 from $SmokeUrl within $SmokeTimeoutSeconds s"
    }
}
catch {
    Write-Host ""
    Write-Host "DEPLOYMENT FAILED: $($_.Exception.Message)" -ForegroundColor Red

    if ($NoRollback) {
        Write-Host "-NoRollback set; leaving the server as it is." -ForegroundColor Yellow
        throw
    }

    Write-Step "Rolling back from $backupDir"
    foreach ($site in $sites) {
        $siteBackup = Join-Path $backupDir $site.Name
        if (-not (Test-Path $siteBackup)) { Write-Host "  no backup for $($site.Name) (first deployment), skipping"; continue }
        try {
            Set-AppPoolState -Name $site.Pool -Desired Stopped
            Invoke-Robocopy -Source $siteBackup -Destination $site.Path -ExcludeDirs $site.ExcludeDirs -ExcludeFiles $site.ExcludeFiles
            Remove-Item (Join-Path $site.Path 'app_offline.htm') -ErrorAction SilentlyContinue
            Set-AppPoolState -Name $site.Pool -Desired Started
            Write-Host "  restored $($site.Name)"
        } catch {
            Write-Host "  ROLLBACK OF $($site.Name) FAILED: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    throw "Deployment failed and was rolled back. Original error: $($_.Exception.Message)"
}

# ---------------------------------------------------------------------------
Write-Step "Prune backups (keep newest $KeepBackups)"
if (Test-Path $BackupRoot) {
    Get-ChildItem $BackupRoot -Directory |
        Sort-Object Name -Descending |
        Select-Object -Skip $KeepBackups |
        ForEach-Object {
            Write-Host "  removing $($_.FullName)"
            Remove-Item $_.FullName -Recurse -Force
        }
}

Write-Host ""
Write-Host "Deployment succeeded." -ForegroundColor Green
