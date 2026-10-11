-- A fingerprint of what is actually IN the database.
--
-- check-schema.sql answers "does this database have the right shape". This answers
-- "does it hold the same things", which is the question behind an app that looks
-- different on another machine. The API upgrades schema on startup but never invents
-- data, so accounts, a tutor's published classes and their verified status, bookings and
-- balances only exist where someone created them.
--
--   docker exec -i learnsphere-mysql \
--     mysql -ulearnsphere -p'LearnSphere2026!' LearnSphere < database/check-data.sql
--
-- Run it on both machines and compare the two outputs line for line. Nothing here
-- prints a secret or a password hash.

SELECT '=== accounts ===' AS section;
SELECT Role, COUNT(*) AS n FROM Users GROUP BY Role ORDER BY Role;

SELECT '=== tutors ===' AS section;
SELECT
  COUNT(*)                                          AS tutors,
  SUM(IsVerified = 1)                               AS verified,
  SUM(IsOnline = 1)                                 AS online,
  (SELECT COUNT(*) FROM TutorOfferings)             AS offerings,
  (SELECT COUNT(*) FROM TutorTimeSlots)             AS slots,
  (SELECT COUNT(*) FROM TutorTimeSlots
     WHERE Status = 'Available' AND IsFull = 0)     AS bookable_slots
FROM Tutors;

-- A verified tutor with bookable slots is what makes the parent catalogue non-empty.
-- Zero here is the usual reason a fresh machine looks like a different product.
SELECT '=== bookable tutors (catalogue visibility) ===' AS section;
SELECT t.Id AS tutor_id, u.Name,
       COUNT(s.Id) AS bookable_slots
FROM Tutors t
JOIN Users u ON u.Id = t.UserId
LEFT JOIN TutorTimeSlots s
  ON s.TutorId = t.Id AND s.Status = 'Available' AND s.IsFull = 0
WHERE t.IsVerified = 1
GROUP BY t.Id, u.Name
ORDER BY t.Id;

SELECT '=== students & bookings ===' AS section;
SELECT
  (SELECT COUNT(*) FROM Students)                         AS students,
  (SELECT COUNT(*) FROM Bookings)                         AS bookings,
  (SELECT COUNT(*) FROM Bookings WHERE Status='confirmed') AS confirmed,
  (SELECT COUNT(*) FROM BookingClasses)                   AS sessions,
  (SELECT COUNT(*) FROM BookingClasses
     WHERE DeliveryStatus='Delivered')                    AS delivered,
  (SELECT COUNT(*) FROM StudentTutorFirstClasses)         AS first_match_markers;

SELECT '=== invoices ===' AS section;
SELECT Status, COUNT(*) AS n,
       COALESCE(SUM(BaseAmount),0)   AS base,
       COALESCE(SUM(MarkupAmount),0) AS markup,
       COALESCE(SUM(Amount),0)       AS billed
FROM Invoices GROUP BY Status ORDER BY Status;

SELECT '=== money ===' AS section;
SELECT
  (SELECT COALESCE(SUM(Amount),0) FROM TutorLedgerEntries WHERE Fund='withdrawable') AS tutor_ready,
  (SELECT COALESCE(SUM(Amount),0) FROM TutorLedgerEntries WHERE Fund='credit')       AS tutor_credit,
  (SELECT COALESCE(SUM(Amount),0) FROM ParentWalletEntries)                          AS parent_wallet,
  (SELECT COUNT(*) FROM PayoutBatches)                                               AS payout_batches,
  (SELECT COUNT(*) FROM TutorPayables)                                               AS payables,
  (SELECT COUNT(*) FROM Payouts)                                                     AS payouts;

-- These drive what the money flow actually does, and they are pure configuration: an
-- upgraded database keeps whatever it had, so two machines can differ here silently.
SELECT '=== platform fees (config, not data) ===' AS section;
SELECT MarkupPercent,
       FirstMatchCommissionPercent,
       IF(FirstMatchEffectiveFrom IS NULL, 'DISARMED', 'ARMED') AS first_match,
       RatePercent AS recurring_commission
FROM CommissionSettings;

SELECT '=== payment gateway (no secrets shown) ===' AS section;
SELECT IsEnabled, Mode, Currency,
       IF(ApiKey = '' OR ApiKey IS NULL, 'NOT SET', 'SET') AS api_key
FROM PaymentGatewaySettings;
