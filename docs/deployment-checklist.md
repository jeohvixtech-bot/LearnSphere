# Deployment checklist (IIS)

The branch carries ready-to-copy publish output:

| Folder | Deploy to | Notes |
|---|---|---|
| `backend/publish/backend/` | the API site (e.g. `C:\inetpub\learnsphere-api`) | .NET 9 Release build, `web.config` runs it in-process as **Production** |
| `publish/frontend/` | the frontend site (e.g. `C:\inetpub\learnsphere`) | static files + `web.config` (SPA fallback, MIME types) |

## 1. Backend — environment variables (required)

The `appsettings.json` in the publish folder is **sanitised** — the connection string, JWT key
and allowed origin are placeholders, so the package carries no secrets. The API **refuses to
start** until they are supplied. Set these on the app pool / server (System → Environment
Variables, or `<environmentVariables>` in `web.config` on the server only — never commit
secrets):

| Variable | Value |
|---|---|
| `Jwt__Key` | random secret, 32+ characters |
| `ConnectionStrings__DefaultConnection` | `Server=…;Database=LearnSphere;User=…;Password=…` |
| `AllowedOrigins__0` | the frontend origin, e.g. `https://learnsphere.example` |
| `BehindReverseProxy` | `true` when IIS/nginx sits in front (it does on IIS) |
| `EnableSwagger` | leave unset (Swagger is off in Production) |

Then `iisreset` or recycle the app pool.

## 2. Backend — file permissions

The API writes uploads under its own folder. Give the app pool identity **Modify** on it:

```
icacls "C:\inetpub\learnsphere-api\wwwroot" /grant "IIS AppPool\<pool>:(OI)(CI)M"
```

Without this, document upload returns HTTP 500. Keep `wwwroot\uploads` out of the folder
you overwrite on the next deployment, or back it up first — it holds tutors' ID documents.

IIS also caps request bodies at ~28 MB by default; intro videos go up to 100 MB:

```xml
<security><requestFiltering><requestLimits maxAllowedContentLength="115343360" /></requestFiltering></security>
```

## 3. Frontend — API address

`config.js` is read before the app starts. If the API is on the **same host under `/api`**
(a reverse-proxy rule), leave it as is. If the API has its own host, set:

```js
window.LEARNSPHERE_API_URL = 'https://api.learnsphere.example/api';
```

## 4. Database

- Run the schema check in `database/check-schema.sql`; the API's own startup ladder adds
  any missing tables/columns.
- Production must **not** load `database/seed/` (demo accounts and password hashes).
- Before go-live: `UPDATE Tutors SET TestModeEnabled = 0;` — otherwise every tutor sees the
  "Bypass date rule" test toggle.

## 5. Payment gateway

Enter the live HitPay key and salt in **Admin → Payment Gateway** (stored in the database,
not in config files). Remove the committed `HitPay_Dev` file from the repository and rotate
that sandbox key.

## 6. Smoke test after deploy

1. Open the site → welcome page loads, favicon shows.
2. Sign in as admin → Payment Gateway shows the live configuration.
3. Sign in as a tutor → Edit Profile → upload an identity photo → no error.
4. As a parent, book a class → lands on HitPay's hosted page → complete a small payment →
   invoice shows **Paid** and the tutor's wallet reflects it.
