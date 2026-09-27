# Security, payment & credit fixes — Sep 2026

Six defects found across two QA passes — an end-to-end run over both personas with a
simulated HitPay sandbox payment, then a focused run on promotional credit — plus the
auditing each one triggered. All six are fixed and re-tested.

Two of them were **critical** and neither was reachable by clicking through the app — the
UI always sends well-formed requests, so they only appear when something sends what the UI
would not. That is why they survived normal testing.

---

## BUG-01 — Any tutor could edit, delete or reprice any other tutor

**Severity:** Critical · **Screen:** Tutor → Teaching Overview → Edit Profile → Save

### What was wrong

`PUT /api/tutors/{id}` trusted the id in the URL and never compared it to the caller. Any
signed-in tutor could address another tutor's id and overwrite their profile.

The payload carries the **offerings list**, and offerings hold *price per lesson* — so this
was not only vandalism but unauthorised repricing, and sending `"offerings": []` wiped the
victim's catalogue entirely. A tutor with no offerings disappears from parent search.

Proven during testing: one call from an unrelated account overwrote CSYongTutor's bio and
experience and deleted all 27 of their offerings. Restored from `database/seed/`.

### What the audit then found

Checking every `{id}`-addressed mutating endpoint in `TutorsController` turned up two more
with the same omission:

| Endpoint | Exposure |
|---|---|
| `DELETE /api/tutors/{id}` | **Any signed-in user — including a parent — could delete any tutor account.** Worse than the reported bug. |
| `POST /api/tutors/{id}/slots` | Any signed-in user could inject class slots into another tutor's timetable. |

`{id}/favorite` looks similar but is correct by design: the id there is the tutor *being
favourited* by a parent, so the caller is deliberately not the owner.

### The fix

`backend/LearnSphere.API/Controllers/TutorsController.cs` — one shared helper, applied to
all three endpoints:

```csharp
private bool CallerMayManage(Tutor tutor)
{
    if (User.IsInRole("admin")) return true;
    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    return tutor.UserId == userId;
}
```

Centralised rather than repeated so a new endpoint has one obvious thing to call. The
14 endpoints that already checked ownership were left as they were.

---

## BUG-02 — Tutor identity documents were readable by anyone

**Severity:** Critical · **Screens:** Tutor → Edit Profile → Verification · Admin → Tutor Vetting Base

### What was wrong

Uploads were written into `wwwroot/` and served by `UseStaticFiles`, which runs **before
authentication**. Anything under it is public by definition.

So an NRIC or passport scan at
`https://host/uploads/documents/b19cfc21-….png` returned **HTTP 200 with no token**, from
any machine, logged out. The filename is a GUID and cannot be brute-forced, but the URL is
not a secret: it is returned by the API, stored in `TutorDocuments`, and sits in the
reviewing admin's browser history. `TutorDocuments` also stores the **ID number** beside it.

The repository already treated this as sensitive — `.gitignore` excludes the folder as
*"may contain PII — ID photos, certificates"*. The runtime simply did not apply the same
judgement.

### The fix

New `backend/LearnSphere.API/Controllers/DocumentsController.cs` serves documents behind
`[Authorize]`, allowing only **the owning tutor or an admin**, and rejecting path traversal
before touching disk.

`Program.cs` no longer serves the whole of `wwwroot`. Only `/uploads/profiles` stays
public — profile photos are meant to be seen by parents browsing the catalogue:

```csharp
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(profilesPath),
    RequestPath = "/uploads/profiles"
});
```

The new route deliberately matches the old path, `/uploads/documents/{fileName}`, so URLs
already recorded against `TutorDocuments` keep resolving — they now pass an authorisation
check on the way through. **No data migration needed.**

---

## BUG-03 — Any password was accepted, including `1`

**Severity:** High · **Screen:** Sign-up / Register (tutors and parents share the endpoint)

### What was wrong

No length, complexity or common-password check anywhere. Registering with the password `1`
returned a valid JWT. The telling detail: the *name* field on the same form **is**
validated (digits rejected with a clear message), so this looks like an oversight rather
than a decision.

These accounts hold children's names, birth dates, schools and billing history.

### The fix

New `backend/LearnSphere.API/Services/PasswordValidator.cs`, applied to **registration and
change-password** so a weak password cannot be set after the fact.

- minimum 8 characters, maximum 128
- must combine at least **two of**: lower case, upper case, digits, symbols
- rejects a list of obvious passwords (`password`, `12345678`, `qwerty`, `learnsphere`, …)

Deliberately a length floor plus modest variety rather than the classic
"one-of-each-class" rule: long passphrases are what actually resist guessing, and stacking
character-class requirements mostly produces `Password1!` and a sticky note. It mirrors how
`NameValidator` is already structured, so the two policies sit side by side.

---

## BUG-04 — Retrying checkout created a second payable link

**Severity:** Medium · **Screen:** Parent → Sessions & Activity (or Billing) → Pay Invoice

### What was wrong

Every click on **Pay Invoice** created a brand-new HitPay payment request while the
previous one stayed live and payable. Two links, same invoice, both chargeable.

The invoice could not be corrupted — the completion path only moves `Unpaid → Paid` once.
That is precisely the problem: **the second capture is real money that lands nowhere.** The
parent is out of pocket with nothing in the system recording an overpayment.

Confirmed before the fix: two `PaymentTransactions` rows for invoice 15, both $563.50, both
`pending`, after a single back-and-retry.

### The fix

`PaymentsController.Checkout` now looks for an existing pending request for that invoice
and reuses it, but only after confirming with HitPay that it is **still open** and **still
for the amount now owed** — wallet credit applied in between changes that figure.

If HitPay cannot be reached to confirm, a fresh request is created rather than reusing a
request of unknown state; the stale one expires at HitPay on its own.

---


---

## BUG-05 — A refunded booking left the tutor paid, and destroyed their credit

**Severity:** High · **Screen:** Admin → Resolution Disputes (refund path)

### What was wrong

When promotional credit paid a first-match commission, two entries were written as a pair:
the credit fund was debited, and the withdrawable fund credited by the same amount.

Refunding that invoice reversed the earning and the commission — but **not the offset**,
and it never returned the consumed credit. Two losses pulling in opposite directions:

- the tutor kept withdrawable money for a booking that no longer existed, so the platform
  would pay out for a lesson the parent was refunded for
- the tutor permanently lost the promotional credit they had spent on it

Measured before the fix: a refunded invoice netted **ready +100.00, credit −100.00**.

### The fix

`TutorLedgerService.UnwindOffsetsForReversedCommissionsAsync` runs before credit is
consumed each pass. For any invoice whose first-match commission has been reversed and
whose offset has not yet been unwound, it writes a matched pair mirroring the original:

- `commission_offset_reversal` — takes the withdrawable money back
- `credit_restored` — returns the credit to the specific grant it came from, via
  `SourceEntryId`, so a grant that is still live becomes spendable again

Idempotent: an invoice already carrying an unwind is never revisited.

After the fix the same invoice nets **0.00 on both funds**.

---

## BUG-06 — Concurrent settlement double-charged commission and ate the credit

**Severity:** High · **Module:** `TutorLedgerService.ReconcileTutorAsync`

### What was wrong

Reconciliation reads the ledger, works out a delta, then inserts — with no lock. Two
settlements landing together both saw "no earning yet" and both wrote an `earning` **and**
a `first_match_commission`.

The delta logic later noticed the duplicate earning and reversed it. It never reversed the
duplicate **commission**. So −200 of commission stood against a single +100 offset.

Caught in testing with two payments **46 microseconds apart**: the affected invoice netted
**ready 0.00, credit −100.00** where it should have been **+100 / −100**. The tutor was a
full commission short and their credit had bought nothing. An invoice settled in the same
burst that happened not to collide was perfectly correct — that contrast is the proof.

This was the concurrency gap flagged when the ledger was first built. It is no longer
theoretical.

### The fix

A MySQL named lock, `learnsphere:ledger:{tutorId}`, held across the whole read-compute-
insert cycle, on an explicitly opened connection so it cannot be released early by
connection pooling.

A named lock rather than a transaction: the work spans several `SaveChanges` calls, and an
isolation level alone would not stop two passes each deciding to insert. It waits up to ten
seconds; on timeout the pass is skipped and logged rather than writing, because a stale
balance is recoverable and a double-charged commission is not. Skipped entirely for
non-relational providers so the in-memory test database still works.

After the fix, two simultaneous settlements produce **exactly four entries each**, no
duplicates, netting +100 / −100.

## Verification

Round 2, run after the fixes:

| Suite | Result |
|---|---|
| Unit tests | 40/40 |
| Ownership audit — all 17 `{id}` endpoints | guarded |
| Regression: BUG-01, 02, 03 | 20/20 |
| Regression: BUG-04 + parent payment flow | 11/11 |
| Credit deduction — 3 parents, 500 grant | 19/19 |
| Credit edge cases | 6/7 (the 1 failure was BUG-05) |
| Fix verification: BUG-05 + BUG-06 | 8/8 |

Key evidence:

- `PUT`/`DELETE`/`POST slots` against another tutor → **403**; own profile → **200**
- Document: anonymous → **401**, unrelated parent → **403**, owning tutor → **200**, admin → **200**
- Profile photo, anonymous → **200** (intentionally still public)
- Weak passwords rejected across five variants; `Correct-Horse-9` accepted
- **Three** checkout attempts → **1** payment request (was 2 after 2 attempts)
- Forged unsigned webhook → **401**, invoice still `Unpaid`
- 500 credit drained **500 → 200 → 0** across three parents; full, partial, then nil deduction
- Refunded first match nets **0.00 on both funds** (was +100 / −100)
- Two simultaneous settlements → **4 entries each, no duplicates** (was 7 with duplicates)

All QA accounts, bookings and uploads were removed afterwards; the environment is back to
1 booking, 27 offerings for CSYongTutor, 24 free slots.

---

## Still open

**Extension-only file validation.** An executable renamed `payload.png` uploads
successfully — content is never sniffed. Low impact today because files are served with an
image content type rather than executed. Worth revisiting if document serving ever starts
trusting the stored filename.

**Untested in this pass:** browser UI rendering and client-side validation (everything ran
against the API), and completing a payment on HitPay's hosted page, which needs a human.
The post-settlement chain — receipt, ledger entries, payout eligibility — was verified
earlier via the wallet settlement path.
