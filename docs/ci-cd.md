# CI/CD for LearnSphere (GitHub Actions)

Two workflows and two PowerShell scripts. The scripts hold all the logic; the workflows
only decide *when* to run them and *where*. That split is deliberate: you can run both
scripts by hand on a laptop or on the server and get the same result CI gets.

```
push / PR ──► .github/workflows/ci.yml ──► build + 70 xunit tests, frontend checks, hygiene
                                        └─► (main, Pre-Main) release zips as an artifact

merge to main ──► .github/workflows/cd.yml
                    build  (GitHub-hosted ubuntu)  scripts/ci/Build-Release.ps1 ──► release artifact
                    deploy (self-hosted, on IIS)   scripts/ci/Deploy-IIS.ps1   ──► backup, copy, smoke test, rollback
```

| File | Purpose |
|---|---|
| `global.json` | Pins the .NET SDK to 9.0.x so `setup-dotnet` and every developer resolve the same major |
| `scripts/ci/Build-Release.ps1` | Publish the API, **sanitise appsettings.json**, copy the frontend, bake the API URL into `config.js`, write `build-info.json`, zip |
| `scripts/ci/Deploy-IIS.ps1` | On the server: stop app pools, back up, robocopy the package in (preserving uploads + server secrets), start, smoke test, roll back on failure, prune old backups |
| `.github/workflows/ci.yml` | Gate for every commit |
| `.github/workflows/cd.yml` | Production deployment |
| `.github/dependabot.yml` | Weekly NuGet / Actions update PRs (each one runs CI) |

---

## 1. CI (`ci.yml`)

**Triggers.** Every push on any branch (doc-only changes skipped), and pull requests into
`main` or `Pre-Main`. A newer push to the same branch cancels the older run
(`concurrency` with `cancel-in-progress: true`), so you never wait on a stale commit.

**Jobs** (run in parallel):

1. **backend** on `ubuntu-latest`. Restores NuGet packages (cached by the hash of the
   `.csproj` files, so the cache invalidates exactly when a package reference changes),
   builds `Release`, then runs `dotnet test` with a TRX logger and coverlet's
   `XPlat Code Coverage` collector. `TestResults/` is uploaded as an artifact even when
   tests fail (`if: always()`), so a red run still gives you something to open in
   Visual Studio. Tests use EF Core InMemory, so no MySQL is needed on the runner.
2. **frontend**. The frontend is plain AngularJS 1.x with no build step, so there is
   nothing to compile. The job does three cheap, useful things:
   `node --check` on every hand-written `.js` file (a syntax error in a controller would
   otherwise be found only by a browser), a guard that `frontend/config.js` does **not**
   hard-code an API URL (the URL is baked in at package time, see below), and a check
   that every local `src=`/`href=` in `index.html` points at a file that exists.
3. **hygiene**. Fails if `appsettings.Production.json`, `appsettings.Development.json`
   or a `.env` is ever tracked. Warns (does not fail) that `publish/` is still committed:
   the package job below makes that folder redundant.
4. **package** (only on pushes to `main` / `Pre-Main`, after the three above pass). Runs
   the *same* `Build-Release.ps1` the CD workflow uses, with `-SkipTests` because the
   backend job already ran them, and uploads the zips for 14 days. This means there is
   always a downloadable, sanitised package for a manual deployment, even before the
   self-hosted runner exists.

**Why ubuntu for a Windows deployment?** The API is published framework-dependent for
`win-x64`; that is a cross-compile the SDK does on any OS, and Linux runners start
faster and are cheaper. PowerShell 7 (`pwsh`) is preinstalled on them, which is why the
build script is PowerShell and is written to work on both 5.1 and 7.

---

## 2. Build-Release.ps1

```
pwsh ./scripts/ci/Build-Release.ps1 [-ApiUrl <url>] [-SkipTests] [-OutputRoot <dir>]
```

1. `dotnet restore` both projects, `dotnet test` unless `-SkipTests`.
2. `dotnet publish -c Release -r win-x64 --self-contained false` with no PDBs. The
   server has the ASP.NET Core 9 hosting bundle, so the package stays ~10 MB and runtime
   patches come from the server, not from a rebuild.
3. **Sanitise `appsettings.json`.** The dev connection string, the sample JWT key and the
   `"*"` CORS origin are replaced with `SET_VIA_ENVIRONMENT …` placeholders and the SMTP
   password is blanked. These are the same placeholders the hand-made `publish/` folder
   used, so `Program.cs`'s startup guard still refuses to boot if the server forgot the
   environment variables. The script then greps the result for known secret fragments
   and fails the build if any survived, so a future change to `appsettings.json` cannot
   silently leak.
4. Copy `frontend/` verbatim. If `-ApiUrl` is given, append
   `window.LEARNSPHERE_API_URL = '<url>'` to `config.js`. Without it the frontend calls
   `<same origin>/api`, the reverse-proxy layout.
5. Write `build-info.json` (commit, branch, run id, UTC time, API URL) into both
   packages. The deploy script prints it, and on the server you can always answer
   "which build is this?" by opening that file.
6. Zip to `artifacts/learnsphere-backend-<sha>.zip` and `…-frontend-<sha>.zip`.

---

## 3. CD (`cd.yml`)

**Triggers.** A push to `main` (normally a merge from `Pre-Main`), or **Run workflow** in
the Actions tab, which also lets you override the API URL or skip the smoke test.
`concurrency: production-deploy` with `cancel-in-progress: false` guarantees two
deployments never overlap and a deployment in flight is never killed half-way.

**Job `build`** (GitHub-hosted). Runs `Build-Release.ps1` *with* tests. CD does not trust
that CI happened to run on this exact commit; a deployment re-proves it. The artifact
contains the two zips, `build-info.json`, and `Deploy-IIS.ps1` itself, so the server-side
job needs no git checkout.

**Job `deploy`** (self-hosted runner on the IIS server, label `learnsphere-prod`). Uses a
GitHub **environment** called `production`, which gives a deployment history tab, the
site URL on the run page, and the option to require a reviewer's approval before the
job starts (Settings → Environments → production → Required reviewers). Steps: download
the artifact, `Expand-Archive` both zips, run `Deploy-IIS.ps1`, write a summary table to
the run page.

**Why a self-hosted runner instead of SSH/WinRM/Web Deploy?**

- No credential for the server ever exists in GitHub. The runner is a Windows service
  on the server that polls `api.github.com` over outbound HTTPS and executes the job
  locally. Nothing inbound needs to be opened on the firewall.
- `WebAdministration` (the IIS PowerShell module) is available natively, so stopping and
  starting app pools is a one-liner rather than a remote-execution dance.
- If GitHub is unreachable, the same script runs by hand with the same parameters.

---

## 4. Deploy-IIS.ps1

```
.\Deploy-IIS.ps1 -BackendPackage <dir> -FrontendPackage <dir> [-SmokeUrl <url>] [...]
```

For each site (backend first, then frontend):

1. **Stop.** Write `app_offline.htm` into the site folder and stop the app pool, waiting
   until IIS reports `Stopped`. The API is hosted in-process, so the worker process owns
   the DLL file handles; without this step the copy fails with "file in use".
2. **Back up** the current folder to `C:\inetpub\backups\learnsphere\<timestamp>\<site>\`,
   excluding `wwwroot\uploads` (never overwritten, and it holds tutors' ID documents) and
   `logs`.
3. **Copy** with `robocopy /MIR` (mirror: adds, updates and deletes so stale files from an
   old release do not linger), excluding:
   - `wwwroot\uploads\` — user data lives on the server only;
   - `appsettings.Production.json` — server-side secrets. ASP.NET Core loads this file
     automatically in Production, on top of the sanitised `appsettings.json`. Put the
     connection string, JWT key and allowed origin in it once and every deployment keeps
     it. App-pool environment variables work equally well; pick one.
   - `logs\`, `app_offline.htm`.
   robocopy's exit codes 0–7 are all success variants; only ≥ 8 is treated as failure.
4. **Start.** Remove `app_offline.htm`, start the pool, wait for `Started`.

Then:

5. **Smoke test.** Poll `GET <SmokeUrl>` (default
   `http://localhost:1000/api/admin/scoring-weightages`, an anonymous endpoint that
   reads the database) until HTTP 200 or 90 s. The first requests after a cold start
   fail while the in-process host boots and runs its schema ladder; the loop absorbs that.
   A 200 proves the API started, its configuration passed the startup guards, and MySQL
   is reachable with the configured credentials.
6. **Rollback** if anything above threw: stop pools, robocopy the backup back (same
   exclusions, so uploads and secrets are untouched), start, and exit non-zero so the
   workflow goes red. `-NoRollback` disables this for debugging.
7. **Prune** backups beyond the newest 5 (`-KeepBackups`).

`-SkipAppPool` skips every IIS call so the copy/backup/rollback logic can be exercised on a
machine without IIS; that is how it was tested.

---

## 5. One-time setup

### GitHub

1. **Variables** (Settings → Secrets and variables → Actions → *Variables* tab):

   | Variable | Value for the current server |
   |---|---|
   | `API_URL` | `http://103.233.3.146:1000/api` |
   | `FRONTEND_URL` | `http://103.233.3.146:1002` |
   | `SMOKE_URL` | `http://localhost:1000/api/admin/scoring-weightages` |
   | `IIS_API_PATH`, `IIS_FRONTEND_PATH`, `IIS_API_POOL`, `IIS_FRONTEND_POOL`, `IIS_BACKUP_ROOT` | only if the server layout differs from the defaults in `cd.yml` |

   These are plain variables, not secrets: none of them is sensitive.
2. **Environment** `production` (Settings → Environments → New). Optionally add yourself
   as a required reviewer; then every merge to `main` pauses at the deploy job until
   someone clicks *Approve*.
3. **Branch protection** on `main`: require the `Backend build + tests`, `Frontend checks`
   and `Repository hygiene` status checks to pass before merging. This is what turns CI
   from "informational" into a real gate.

### Server

1. Install the ASP.NET Core 9 **Hosting Bundle** (already done if the site runs today).
2. Settings → Actions → Runners → **New self-hosted runner** → Windows x64. Follow the
   shown commands on the server, and when asked for labels add `learnsphere-prod`.
   Install it as a service (`.\svc.cmd install`, `.\svc.cmd start`).
3. The runner service account must be able to administer IIS. Simplest: run the service
   as a local administrator account dedicated to deployments. Give that account
   **Modify** on `C:\inetpub\learnsphere*` and `C:\inetpub\backups`.
4. Secrets on the server, once, either as app-pool environment variables
   (`Jwt__Key`, `ConnectionStrings__DefaultConnection`, `AllowedOrigins__0`,
   `BehindReverseProxy=true`) or as `C:\inetpub\learnsphere-api\appsettings.Production.json`:

   ```json
   {
     "ConnectionStrings": { "DefaultConnection": "Server=…;Database=LearnSphere;User=…;Password=…;CharSet=utf8mb4;" },
     "Jwt": { "Key": "<random 32+ chars>" },
     "AllowedOrigins": [ "http://103.233.3.146:1002" ],
     "BehindReverseProxy": true
   }
   ```

   The deploy script never overwrites this file.
5. First deployment: trigger **CD (production)** manually from the Actions tab and watch
   the log. The backup folder will say "nothing to back up (first deployment)".

### Repository clean-up (recommended, not required)

`publish/` is git-ignored but a copy was committed before the rule existed. CI now builds
that output on every push to `main`/`Pre-Main`, so:

```
git rm -r --cached publish
git commit -m "Stop tracking publish/; CI builds the release package"
```

---

## 6. Day-to-day

| You want to… | Do |
|---|---|
| Ship | Merge `Pre-Main` → `main`. CD runs. Check the run's summary table. |
| Deploy a hotfix branch without merging | Actions → CD (production) → Run workflow → pick the branch. |
| Know what is on the server | Open `C:\inetpub\learnsphere-api\build-info.json`, or the environment's deployment history on GitHub. |
| Roll back manually | On the server: `.\Deploy-IIS.ps1 -BackendPackage C:\inetpub\backups\learnsphere\<ts>\backend -FrontendPackage …\frontend`. A backup is a valid package. |
| Reproduce CI's package locally | `pwsh ./scripts/ci/Build-Release.ps1 -ApiUrl http://103.233.3.146:1000/api` → `artifacts/`. |
| Change the API port / host | Change the `API_URL` variable; the next deployment bakes it into `config.js`. No code change. |

## 7. Known limitations and next steps

- **Downtime** of a few seconds per deployment (stop → copy → start). For zero downtime
  you would need two IIS sites and a port swap or a reverse proxy; not worth it at the
  current scale.
- **No database migration step.** The API upgrades its own schema at startup, which the
  smoke test exercises. If that ever moves to EF migrations, add a `dotnet ef database
  update` step to `Deploy-IIS.ps1` between backup and start.
- **Coverage is collected but not enforced.** Add a threshold (`coverlet` `Threshold`
  property) once the suite grows beyond the current 70 tests.
- **No staging environment.** Adding one is a copy of the `deploy` job with
  `environment: staging`, a differently labelled runner, and its own variables.
