# QA fixes record — everything fixed since testing began (Sep 2026)

Four QA rounds were run against the platform before the production release: an end-to-end
pass over both personas with a simulated HitPay payment, a focused promotional-credit pass,
a full regression, and a final browser-driven pre-production run. **Twelve findings were
fixed** across those rounds. This is the single record of all of them; the security and
payment defects from rounds 1–2 are also written up in depth in
[security-fixes-2026-09-27.md](security-fixes-2026-09-27.md).

## At a glance

| # | Severity | Where | What was wrong | Round |
|---|---|---|---|---|
| BUG-01 | Critical | `PUT/DELETE /api/tutors/{id}`, `POST {id}/slots` | Any signed-in user could edit, reprice, delete or add slots to any tutor | 1 |
| BUG-02 | Critical | `/uploads/documents/*` | Tutor identity documents were public — no token needed | 1 |
| BUG-03 | High | Register / Change password | Any password accepted, including `1` | 1 |
| BUG-04 | Medium | Parent → Pay Invoice | Every retry created a new, still-payable HitPay link | 1 |
| BUG-05 | High | Admin → Refund | Refunding a credit-paid first match left the tutor paid and destroyed their credit | 2 |
| BUG-06 | High | Ledger reconciliation | Two settlements at once double-charged commission | 2 |
| BLK-01 | Critical | `frontend/app/app.js` | API address hard-coded to `127.0.0.1:5000` — a deployed copy calls the visitor's own PC | 4 |
| BLK-02 | High | `Program.cs`, `appsettings.json` | Sample JWT key and open CORS would go live silently; Swagger exposed in production | 4 |
| BUG-07 | Medium | Tutor → Edit Profile | Angular infinite-digest error on open and on every interaction; document checklist never rendered | 4 |
| BUG-08 | Medium | Admin → Payouts → Period | Month box blank; picking a month made the cutoff fail with HTTP 400 | 4 |
| OBS-09 | Low | Route guards | A mistyped or wrong-role URL signed the user out | 4 |
| OBS-10 | Low | `index.html` | No favicon — a 404 console error on every page load | 4 |

Rounds 1–3 also found and closed several test-environment problems (untranslatable EF
expressions surfacing only on MySQL, a broken test project) that are not user-facing and are
not listed here.

---

## Rounds 1–2 — security, payment and credit (BUG-01 … BUG-06)

Summarised here; full write-ups with evidence are in
[security-fixes-2026-09-27.md](security-fixes-2026-09-27.md).

**BUG-01 — ownership.** `TutorsController` trusted the id in the URL. One `CallerMayManage(tutor)`
helper (owner or admin) now guards `PUT {id}`, `DELETE {id}` and `POST {id}/slots`.
Proven fixed: another tutor → **403**; own profile → **200**.

**BUG-02 — public documents.** Uploads were served by `UseStaticFiles` before authentication.
New `DocumentsController` serves `/uploads/documents/{file}` behind `[Authorize]` for the owning
tutor or an admin; static files are limited to `/uploads/profiles`. Existing URLs keep working.

**BUG-03 — password policy.** New `PasswordValidator` (min 8, max 128, two of four character
classes, common-password blocklist) applied to registration and change-password.

**BUG-04 — duplicate checkout links.** `PaymentsController.Checkout` reuses the pending HitPay
request when HitPay confirms it is still open and for the amount now owed.
Three retries → **one** payment request.

**BUG-05 — refund left the offset in place.** `TutorLedgerService.UnwindOffsetsForReversedCommissionsAsync`
writes the mirrored `commission_offset_reversal` / `credit_restored` pair and returns the credit
to the grant it came from. A refunded first match now nets **0.00 / 0.00**.

**BUG-06 — concurrent settlement.** Reconciliation runs under a MySQL named lock
(`learnsphere:ledger:{tutorId}`). Two simultaneous settlements → exactly four entries each.

---

## Round 4 — final pre-production run (BLK-01 … OBS-10)

Driven through the real browser (Edge via Playwright) as parent, tutor and admin, with the
HitPay sandbox behind it.

### BLK-01 — frontend hard-wired to the developer's machine

**Severity:** Critical · **Where:** `frontend/app/app.js:5`

`.constant('API_URL', 'http://127.0.0.1:5000/api')` meant every deployed copy of the site
sent its API calls to the *visitor's* computer. This was also the root cause of the earlier
"UI looks different on my other machine" report.

**Fix.** `API_URL` is now resolved at load time, in this order:

1. `window.LEARNSPHERE_API_URL` — set per environment in the new `frontend/config.js`
   (loaded before the app; edit it on the server, never in application code);
2. `http://127.0.0.1:5000/api` when the page is served from `localhost:3000` (local dev split);
3. otherwise **same origin + `/api`** — the production shape, with the reverse proxy routing
   `/api` to the backend.

The "Cannot reach server … 127.0.0.1:5000" message now names the address actually in use.

### BLK-02 — development settings would go live silently

**Severity:** High · **Where:** `backend/LearnSphere.API/Program.cs`, `appsettings.json`

The committed `appsettings.json` carries the sample JWT key (`…_CHANGE_IN_PRODUCTION`), the
dev database password and `AllowedOrigins: ["*"]`; Swagger UI was mounted unconditionally.
None of that is wrong for development — the risk was that nothing stopped it reaching production.

**Fix.**
- Outside the `Development` environment the API **refuses to start** if `Jwt:Key` is the sample
  value or shorter than 32 characters, with a message naming the `Jwt__Key` variable to set.
  Verified: running the built DLL with `ASPNETCORE_ENVIRONMENT=Production` throws
  `InvalidOperationException: Jwt:Key is the sample value…`.
- Outside Development a startup **warning** is printed when `AllowedOrigins` is `*`.
- Swagger is mounted only in Development, or when `EnableSwagger=true` is set.

Still required on the server (configuration, not code): `Jwt__Key`,
`ConnectionStrings__DefaultConnection`, `AllowedOrigins__0=https://<frontend-origin>`,
`BehindReverseProxy=true`; a proxy body limit of at least 110 MB for document uploads; and
`wwwroot/uploads` on a persistent volume.

### BUG-07 — infinite digest on Tutor → Edit Profile

**Severity:** Medium · **Where:** `views/tutor/overview.html:1471`, `tutor.controller.js` `mandatoryDocsChecklist`

Opening the Edit Profile tab logged `[$rootScope:infdig] 10 iterations reached`, and again
after every interaction (staging a file, each keystroke in the ID number). Angular abandons
the digest when that happens: the "N of 3 documents uploaded" hover checklist rendered **zero**
items, and any other binding updated in the same cycle could be left stale.

**Root cause.** `ng-repeat="item in vm.mandatoryDocsChecklist()"`, where the function built
three brand-new objects on every call. ng-repeat watches the collection every digest, so it
saw "3 items changed" forever.

**Fix.** The controller now returns the *same* array while nothing has changed (keyed on
`label:done`), and the repeat carries `track by item.label`. Verified on a verified and an
unverified tutor: **0** page errors on open, on staging a file, and while typing; the checklist
renders its 3 items.

### BUG-08 — Payouts month picker blank, cutoff fails when a month is chosen

**Severity:** Medium · **Where:** `admin.controller.js` (`cutoffPeriod`, `runCutoff`), `views/admin/payouts.html`

The Period box is `<input type="month">`, which Angular will only bind to a `Date`, but the
controller set the string `'2026-09'` → `[ngModel:datefmt]` and an empty box. Picking August
then posted the Date serialised in UTC — `{"period":"2026-07-31T16:00:00.000Z"}` from Singapore
time — and the API answered **400 "Period must be in yyyy-MM format"**. The only cutoff an admin
could run from the UI was the current month, and only if the picker was never touched.

**Fix.** The model is a `Date` (first of the current month) and `runCutoff` formats it to
`yyyy-MM` before sending. Verified: the box shows `2026-09` by default; picking August sends
`{"period":"2026-08"}`.

### OBS-09 — wrong URL signed the user out

**Severity:** Low · **Where:** `frontend/app/app.js` route guards, `.otherwise`, `$routeChangeError`

Every guard rejection and every unknown address redirected to `/welcome`, and the welcome
page deliberately ends the session. A typo or stale bookmark therefore logged people out.

**Fix.** A signed-in user bounced off a route they cannot open — wrong role, unknown address,
template error — now lands on **their own home** (`/parent/dashboard`, `/tutor/overview`,
`/admin/overview`, or `/change-password` when a reset is pending). Signed-out users still go
to `/welcome`, which keeps its behaviour. Verified: a parent opening `#!/admin/overview` ends
on `/parent/dashboard`, still signed in.

### OBS-10 — no favicon

**Severity:** Low · **Where:** `frontend/index.html`

Added `frontend/favicon.svg` and `<link rel="icon" type="image/svg+xml" href="favicon.svg">`.

---

## Verification of the round-4 fixes

| Suite | Result |
|---|---|
| Browser E2E — parent registration → child → book → HitPay hand-off → retry reuse → wallet settlement → tutor wallet 100/400 → admin roster; 22-page sweep with zero console/page errors; guards; logout; 7 DB assertions | **65/65** (was 58/65 before the fixes) |
| API regression packs A–D and E–I (all six earlier fixes) | 35/35 · 39/39 |
| Backend unit tests | 40/40 |
| Production start-up guard (`ASPNETCORE_ENVIRONMENT=Production`, sample key) | refuses to start, as intended |

All test accounts (`e2e.*`, `rg1.*`) were removed afterwards; the database matches its pre-run
baseline (7 users, 1 booking, 1 invoice, CSYongTutor's 27 offerings intact).

## Still open

- **Extension-only upload validation** (Low) — content is not sniffed; files are served with
  an image content type, so not exploitable today.
- **Human HitPay run** — completing a card payment on HitPay's hosted page and the webhook
  settlement need one manual run in the sandbox before go-live; the post-settlement chain
  was verified through the wallet path, which shares the settlement code.
- **Business confirmations** — first-match commission armed at 100 % and markup at 15 % in
  `CommissionSettings`; the seed dump in `database/seed/` must never be loaded in production.
