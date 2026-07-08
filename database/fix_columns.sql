-- Fix column mismatches between MySQL DB and EF Core model

-- IssueReports: backfill Timestamp (column already added)
UPDATE IssueReports SET `Timestamp` = DATE_FORMAT(CreatedAt, '%H:%i:%s %p') WHERE `Timestamp` = '';

-- Invoices: already renamed (InvoiceDate -> Date applied previously)

-- Notifications: add Timestamp column
ALTER TABLE Notifications ADD COLUMN `Timestamp` longtext NOT NULL;
UPDATE Notifications SET `Timestamp` = DATE_FORMAT(CreatedAt, '%Y-%m-%d %h:%i %p');

-- ChatMessages: drop the index on SentAt before renaming
DROP INDEX IX_ChatMessages_TutorId_SentAt ON ChatMessages;
ALTER TABLE ChatMessages CHANGE MessageText `Text` longtext NOT NULL;
ALTER TABLE ChatMessages CHANGE SentAt `Timestamp` longtext NOT NULL;

-- Payouts: rename PayoutDate -> Date
ALTER TABLE Payouts CHANGE PayoutDate `Date` longtext NOT NULL;

-- TutorReviews: rename ReviewText -> Text
ALTER TABLE TutorReviews CHANGE ReviewText `Text` longtext NOT NULL;
