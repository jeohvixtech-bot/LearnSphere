# Dev dataset

`learnsphere-dev-data.sql` is a snapshot of a working local database — schema plus data —
so a second machine renders the same app instead of an empty shell.

The application code carries none of this. Accounts, a tutor's published classes, their
verified status, bookings, invoices, the money ledger and the platform fee settings all
live in MySQL, which is why a fresh checkout looks like a different product.

## Use it

```bash
docker compose up -d      # from the repo root
```

MySQL runs everything in this folder on first start, so the database comes up already
populated. See `docker-compose.yml`.

To load it into a database that already exists:

```bash
docker exec -i learnsphere-mysql \
  mysql -ulearnsphere -p'LearnSphere2026!' LearnSphere < learnsphere-dev-data.sql
```

## What was deliberately left out

Two tables ship as **structure only, no rows**:

| Table | Why |
|---|---|
| `PaymentGatewaySettings` | Holds the HitPay API key and webhook salt. Secrets do not belong in git. |
| `TutorDocuments` | Holds NRIC/passport numbers against verification uploads. The repo already excludes uploaded ID documents for the same reason — see `.gitignore`. |

Consequences on a restored machine, both expected:

- **No payment gateway.** Payments fall back to the local "mark as paid" path until a key
  is entered under Admin → Payment Gateway. That path is refused whenever a real gateway
  is armed, so it cannot be used to bypass one.
- **No verification document rows.** Tutors stay verified regardless — that state lives on
  `Tutors.IsVerified`, not on the documents.

Password hashes *are* included, so the existing accounts can be logged into. They are
bcrypt and were always meant to be stored, but it is still credential material: treat this
file as internal and do not publish the repo with it in place.

## Refreshing the snapshot

```bash
docker exec learnsphere-mysql sh -c "
  mysqldump -ulearnsphere -pLearnSphere2026! --skip-comments --no-data \
    LearnSphere PaymentGatewaySettings TutorDocuments > /tmp/a.sql
  mysqldump -ulearnsphere -pLearnSphere2026! --skip-comments \
    --ignore-table=LearnSphere.PaymentGatewaySettings \
    --ignore-table=LearnSphere.TutorDocuments LearnSphere > /tmp/b.sql
  cat /tmp/a.sql /tmp/b.sql > /tmp/seed.sql"
docker cp learnsphere-mysql:/tmp/seed.sql database/seed/learnsphere-dev-data.sql
```

The structure-only dump must come first: it creates the two excluded tables while
foreign-key checks are still disabled.
