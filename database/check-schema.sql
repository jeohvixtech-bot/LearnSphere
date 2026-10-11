-- Is this database up to date with the operational financial flow work?
--
-- The API upgrades its own schema on startup: Program.cs runs a hand-rolled ladder of
-- idempotent raw-SQL statements (CREATE TABLE IF NOT EXISTS, ADD COLUMN in try/catch)
-- before serving. Simply running the new build against an existing database is enough.
-- This script only confirms that it worked.
--
--   docker exec -i learnsphere-mysql \
--     mysql -ulearnsphere -p'LearnSphere2026!' LearnSphere < database/check-schema.sql
--
-- Empty result on both checks = up to date.

SELECT '--- missing tables ---' AS check_1;

SELECT e.tbl AS missing_table
FROM (
  SELECT 'ParentWalletEntries' AS tbl UNION ALL
  SELECT 'PayoutBatches'              UNION ALL
  SELECT 'TutorPayables'              UNION ALL
  SELECT 'TutorLedgerEntries'         UNION ALL
  SELECT 'CommissionSettings'         UNION ALL
  SELECT 'PaymentTransactions'        UNION ALL
  SELECT 'PaymentGatewaySettings'
) e
LEFT JOIN information_schema.TABLES t
  ON t.TABLE_SCHEMA = DATABASE() AND t.TABLE_NAME = e.tbl
WHERE t.TABLE_NAME IS NULL;

SELECT '--- missing columns ---' AS check_2;

SELECT CONCAT(e.tbl, '.', e.col) AS missing_column
FROM (
  SELECT 'Invoices'           AS tbl, 'BaseAmount'                  AS col UNION ALL
  SELECT 'Invoices',                  'MarkupAmount'                       UNION ALL
  SELECT 'Invoices',                  'MarkupPercent'                      UNION ALL
  SELECT 'Invoices',                  'IsFirstMatch'                       UNION ALL
  SELECT 'Invoices',                  'WalletCreditApplied'                UNION ALL
  SELECT 'BookingClasses',            'DeliveryStatus'                     UNION ALL
  SELECT 'BookingClasses',            'DeliveredAt'                        UNION ALL
  SELECT 'CommissionSettings',        'MarkupPercent'                      UNION ALL
  SELECT 'CommissionSettings',        'FirstMatchCommissionPercent'        UNION ALL
  SELECT 'CommissionSettings',        'FirstMatchEffectiveFrom'            UNION ALL
  SELECT 'TutorLedgerEntries',        'SourceEntryId'                      UNION ALL
  SELECT 'TutorLedgerEntries',        'Fund'                               UNION ALL
  SELECT 'TutorLedgerEntries',        'RatePercent'                        UNION ALL
  SELECT 'PaymentTransactions',       'ReturnOrigin'
) e
LEFT JOIN information_schema.COLUMNS c
  ON c.TABLE_SCHEMA = DATABASE() AND c.TABLE_NAME = e.tbl AND c.COLUMN_NAME = e.col
WHERE c.COLUMN_NAME IS NULL;

SELECT '--- backfills (0 pending = done) ---' AS check_3;

SELECT
  (SELECT COUNT(*) FROM Invoices WHERE BaseAmount = 0 AND Amount > 0) AS invoices_without_base,
  (SELECT COUNT(*) FROM CommissionSettings)                           AS fee_settings_rows;
