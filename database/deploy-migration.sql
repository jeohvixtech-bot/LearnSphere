-- ============================================================
-- LearnSphere — Incremental Deployment Migration SQL
-- Upgrades an existing production database to the current schema.
-- Safe to run multiple times — guarded with IF NOT EXISTS /
-- ADD COLUMN IF NOT EXISTS / INSERT IGNORE throughout.
--
-- All table names use backtick-quoting and match the exact casing
-- used in Program.cs (the authoritative source for this project).
-- ============================================================

USE `LearnSphere`;

-- ============================================================
-- 1. `Users` — new columns
-- ============================================================
ALTER TABLE `Users`
    ADD COLUMN IF NOT EXISTS `CreatedAt`          DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    ADD COLUMN IF NOT EXISTS `MustChangePassword` TINYINT(1)  NOT NULL DEFAULT 0;

-- ============================================================
-- 2. `Tutors` — new columns
-- ============================================================
ALTER TABLE `Tutors`
    ADD COLUMN IF NOT EXISTS `IsOnline`           TINYINT(1)  NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS `TestModeEnabled`    TINYINT(1)  NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS `VerificationStatus` VARCHAR(20) NOT NULL DEFAULT 'not_submitted',
    ADD COLUMN IF NOT EXISTS `OfferingsUnlocked`  TINYINT(1)  NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS `LastSubmittedAt`    DATETIME(6) NULL;

-- Ensure TestModeEnabled default is 1 even if column was added earlier with DEFAULT 0
ALTER TABLE `Tutors` ALTER COLUMN `TestModeEnabled` SET DEFAULT 1;

-- ============================================================
-- 3. `TutorSubjects` — new column
-- ============================================================
ALTER TABLE `TutorSubjects`
    ADD COLUMN IF NOT EXISTS `Price` DECIMAL(10,2) NULL;

-- ============================================================
-- 4. NEW TABLE: `TutorOfferings`
-- ============================================================
CREATE TABLE IF NOT EXISTS `TutorOfferings` (
    `Id`            INT             NOT NULL AUTO_INCREMENT,
    `TutorId`       INT             NOT NULL,
    `Subject`       VARCHAR(200)    NOT NULL DEFAULT '',
    `Level`         VARCHAR(200)    NOT NULL DEFAULT '',
    `Mode`          VARCHAR(200)    NOT NULL DEFAULT '',
    `Qualification` VARCHAR(200)    NOT NULL DEFAULT '',
    `Price`         DECIMAL(10,2)   NOT NULL DEFAULT 0,
    `Country`       VARCHAR(50)     NOT NULL DEFAULT 'Singapore',
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_TutorOfferings_Tutors` FOREIGN KEY (`TutorId`) REFERENCES `Tutors`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- In case TutorOfferings existed without Country column
ALTER TABLE `TutorOfferings`
    ADD COLUMN IF NOT EXISTS `Country` VARCHAR(50) NOT NULL DEFAULT 'Singapore';

-- ============================================================
-- 5. NEW TABLE: `TutorDocuments`
-- ============================================================
CREATE TABLE IF NOT EXISTS `TutorDocuments` (
    `Id`                INT             NOT NULL AUTO_INCREMENT,
    `TutorId`           INT             NOT NULL,
    `DocumentType`      VARCHAR(30)     NOT NULL DEFAULT '',
    `FileUrl`           LONGTEXT        NULL,
    `ExternalUrl`       LONGTEXT        NULL,
    `FileName`          LONGTEXT        NULL,
    `FileSizeBytes`     BIGINT          NULL,
    `IdType`            VARCHAR(20)     NULL,
    `IdNumber`          LONGTEXT        NULL,
    `SortOrder`         INT             NOT NULL DEFAULT 0,
    `Status`            VARCHAR(20)     NOT NULL DEFAULT 'pending',
    `AdminNote`         LONGTEXT        NULL,
    `UploadedAt`        DATETIME(6)     NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `ReviewedAt`        DATETIME(6)     NULL,
    `ReplacesDocumentId` INT            NULL,
    `IsArchived`        TINYINT(1)      NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`),
    KEY `IX_TutorDocuments_TutorId` (`TutorId`),
    CONSTRAINT `FK_TutorDocuments_Tutors` FOREIGN KEY (`TutorId`) REFERENCES `Tutors`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- In case TutorDocuments existed without these columns
ALTER TABLE `TutorDocuments`
    ADD COLUMN IF NOT EXISTS `ReplacesDocumentId` INT       NULL,
    ADD COLUMN IF NOT EXISTS `IsArchived`          TINYINT(1) NOT NULL DEFAULT 0;

-- ============================================================
-- 6. `TutorTimeSlots` — new columns (preset class support)
-- ============================================================
ALTER TABLE `TutorTimeSlots`
    ADD COLUMN IF NOT EXISTS `EndTime`              LONGTEXT        NULL,
    ADD COLUMN IF NOT EXISTS `DurationMinutes`      INT             NOT NULL DEFAULT 60,
    ADD COLUMN IF NOT EXISTS `Mode`                 LONGTEXT        NULL,
    ADD COLUMN IF NOT EXISTS `Subject`              LONGTEXT        NULL,
    ADD COLUMN IF NOT EXISTS `Level`                LONGTEXT        NULL,
    ADD COLUMN IF NOT EXISTS `Country`              LONGTEXT        NULL,
    ADD COLUMN IF NOT EXISTS `ClassSize`            VARCHAR(20)     NOT NULL DEFAULT 'one-to-one',
    ADD COLUMN IF NOT EXISTS `MaxStudents`          INT             NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS `ConfirmedCount`       INT             NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS `IsFull`               TINYINT(1)      NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS `PricePerLesson`       DECIMAL(10,2)   NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS `PresetGroupId`        VARCHAR(20)     NULL,
    ADD COLUMN IF NOT EXISTS `VideoConferenceLink`  LONGTEXT        NULL;

-- ============================================================
-- 7. NEW TABLE: `TutorBlockedDates`
-- ============================================================
CREATE TABLE IF NOT EXISTS `TutorBlockedDates` (
    `Id`        INT          NOT NULL AUTO_INCREMENT,
    `TutorId`   INT          NOT NULL,
    `StartDate` VARCHAR(10)  NOT NULL,
    `EndDate`   VARCHAR(10)  NOT NULL,
    `CreatedAt` DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_TutorBlockedDates_TutorId` (`TutorId`),
    CONSTRAINT `FK_TutorBlockedDates_Tutors` FOREIGN KEY (`TutorId`) REFERENCES `Tutors`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 8. `Students` — new column
-- ============================================================
ALTER TABLE `Students`
    ADD COLUMN IF NOT EXISTS `IsArchived` TINYINT(1) NOT NULL DEFAULT 0;

-- ============================================================
-- 9. NEW TABLE: `StudentPreferredModes`
-- ============================================================
CREATE TABLE IF NOT EXISTS `StudentPreferredModes` (
    `Id`        INT         NOT NULL AUTO_INCREMENT,
    `StudentId` INT         NOT NULL,
    `Mode`      VARCHAR(50) NOT NULL,
    `Sequence`  INT         NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`),
    KEY `IX_StudentPreferredModes_StudentId` (`StudentId`),
    CONSTRAINT `FK_StudentPreferredModes_Students` FOREIGN KEY (`StudentId`) REFERENCES `Students`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 10. `Bookings` — new columns
-- ============================================================
ALTER TABLE `Bookings`
    ADD COLUMN IF NOT EXISTS `BookingNumber`           VARCHAR(20)  NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS `BookingType`             VARCHAR(20)  NOT NULL DEFAULT 'parent-offer',
    ADD COLUMN IF NOT EXISTS `PresetSlotId`            INT          NULL,
    ADD COLUMN IF NOT EXISTS `VideoConferenceLink`     LONGTEXT     NULL,
    ADD COLUMN IF NOT EXISTS `VideoLinkReminderStatus` VARCHAR(20)  NOT NULL DEFAULT 'none';

-- Legacy columns — make nullable (data moved to BookingClasses)
ALTER TABLE `Bookings` MODIFY COLUMN `Date`   LONGTEXT NULL;
ALTER TABLE `Bookings` MODIFY COLUMN `Time`   LONGTEXT NULL;
ALTER TABLE `Bookings` MODIFY COLUMN `SlotId` INT      NULL;

-- Widen DurationHours from INT to DOUBLE (15-min preset classes need e.g. 1.5)
ALTER TABLE `Bookings` MODIFY COLUMN `DurationHours` DOUBLE NOT NULL DEFAULT 1;

-- Backfill BookingNumber for existing rows
UPDATE `Bookings` SET `BookingNumber` = CONCAT('BOK', LPAD(`Id`, 5, '0')) WHERE `BookingNumber` = '';

-- ============================================================
-- 11. NEW TABLE: `BookingClasses`
-- ============================================================
CREATE TABLE IF NOT EXISTS `BookingClasses` (
    `Id`             INT         NOT NULL AUTO_INCREMENT,
    `BookingId`      INT         NOT NULL,
    `Date`           VARCHAR(50) NOT NULL DEFAULT '',
    `Time`           VARCHAR(50) NOT NULL DEFAULT '',
    `Status`         VARCHAR(20) NOT NULL DEFAULT 'scheduled',
    `DeliveryStatus` VARCHAR(20) NOT NULL DEFAULT 'Scheduled',
    `DeliveredAt`    DATETIME(6) NULL,
    PRIMARY KEY (`Id`),
    KEY `IX_BookingClasses_BookingId` (`BookingId`),
    CONSTRAINT `FK_BookingClasses_Bookings_BookingId` FOREIGN KEY (`BookingId`) REFERENCES `Bookings`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- In case BookingClasses existed without delivery columns
ALTER TABLE `BookingClasses`
    ADD COLUMN IF NOT EXISTS `Status`         VARCHAR(20) NOT NULL DEFAULT 'scheduled',
    ADD COLUMN IF NOT EXISTS `DeliveryStatus` VARCHAR(20) NOT NULL DEFAULT 'Scheduled',
    ADD COLUMN IF NOT EXISTS `DeliveredAt`    DATETIME(6) NULL;

-- ============================================================
-- 12. NEW TABLE: `BookingPresetSlots`
-- ============================================================
CREATE TABLE IF NOT EXISTS `BookingPresetSlots` (
    `Id`              INT NOT NULL AUTO_INCREMENT,
    `BookingId`       INT NOT NULL,
    `TutorTimeSlotId` INT NOT NULL,
    PRIMARY KEY (`Id`),
    KEY `IX_BookingPresetSlots_BookingId` (`BookingId`),
    CONSTRAINT `FK_BookingPresetSlots_Bookings`       FOREIGN KEY (`BookingId`)       REFERENCES `Bookings`(`Id`)       ON DELETE CASCADE,
    CONSTRAINT `FK_BookingPresetSlots_TutorTimeSlots` FOREIGN KEY (`TutorTimeSlotId`) REFERENCES `TutorTimeSlots`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 13. `CounterProposals` — new columns + index fix
-- ============================================================
-- Make legacy Date/Time nullable (data moved to CounterProposalClasses)
ALTER TABLE `CounterProposals` MODIFY COLUMN `Date` LONGTEXT NULL;
ALTER TABLE `CounterProposals` MODIFY COLUMN `Time` LONGTEXT NULL;

ALTER TABLE `CounterProposals`
    ADD COLUMN IF NOT EXISTS `ProposedBy` VARCHAR(20) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS `Status`     VARCHAR(20) NOT NULL DEFAULT 'pending',
    ADD COLUMN IF NOT EXISTS `CreatedAt`  DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6);

-- Convert old unique index on BookingId to non-unique (a booking can have many proposals)
ALTER TABLE `CounterProposals` ADD INDEX `IX_CounterProposals_BookingId_nonunique` (`BookingId`);
ALTER TABLE `CounterProposals` DROP INDEX IF EXISTS `IX_CounterProposals_BookingId`;
ALTER TABLE `CounterProposals` RENAME INDEX `IX_CounterProposals_BookingId_nonunique` TO `IX_CounterProposals_BookingId`;

-- Backfill Status for existing resolved proposals
UPDATE `CounterProposals` cp
JOIN `Bookings` b ON b.`Id` = cp.`BookingId`
SET cp.`Status` = 'accepted'
WHERE cp.`Status` = 'pending' AND b.`Status` IN ('confirmed','completed');

UPDATE `CounterProposals` cp
JOIN `Bookings` b ON b.`Id` = cp.`BookingId`
SET cp.`Status` = 'cancelled'
WHERE cp.`Status` = 'pending' AND b.`Status` = 'cancelled';

-- ============================================================
-- 14. NEW TABLE: `CounterProposalClasses`
-- ============================================================
CREATE TABLE IF NOT EXISTS `CounterProposalClasses` (
    `Id`                INT         NOT NULL AUTO_INCREMENT,
    `CounterProposalId` INT         NOT NULL,
    `OriginalDate`      VARCHAR(50) NOT NULL DEFAULT '',
    `OriginalTime`      VARCHAR(50) NOT NULL DEFAULT '',
    `ProposedDate`      VARCHAR(50) NOT NULL DEFAULT '',
    `ProposedTime`      VARCHAR(50) NOT NULL DEFAULT '',
    PRIMARY KEY (`Id`),
    KEY `IX_CounterProposalClasses_CounterProposalId` (`CounterProposalId`),
    CONSTRAINT `FK_CounterProposalClasses_CounterProposals_CounterProposalId`
        FOREIGN KEY (`CounterProposalId`) REFERENCES `CounterProposals`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 15. NEW TABLE: `ClassRemarks` (replaces TutorReviews per-booking reviews)
-- ============================================================
CREATE TABLE IF NOT EXISTS `ClassRemarks` (
    `Id`                INT          NOT NULL AUTO_INCREMENT,
    `BookingClassId`    INT          NOT NULL,
    `TutorId`           INT          NOT NULL,
    `ParentUserId`      INT          NOT NULL,
    `ParentDisplayName` VARCHAR(200) NOT NULL DEFAULT '',
    `Rating`            INT          NOT NULL DEFAULT 5,
    `Text`              LONGTEXT     NOT NULL,
    `Status`            VARCHAR(20)  NOT NULL DEFAULT 'published',
    `DisputeReason`     LONGTEXT     NULL,
    `CreatedAt`         DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `EditedAt`          DATETIME(6)  NULL,
    `ResolvedAt`        DATETIME(6)  NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_ClassRemark_BookingClass` (`BookingClassId`),
    KEY `IX_ClassRemarks_TutorId` (`TutorId`),
    CONSTRAINT `FK_ClassRemarks_BookingClasses` FOREIGN KEY (`BookingClassId`) REFERENCES `BookingClasses`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_ClassRemarks_Tutors`         FOREIGN KEY (`TutorId`)        REFERENCES `Tutors`(`Id`)        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 16. NEW TABLE: `ClassRemarkLikes`
-- ============================================================
CREATE TABLE IF NOT EXISTS `ClassRemarkLikes` (
    `Id`            INT         NOT NULL AUTO_INCREMENT,
    `ClassRemarkId` INT         NOT NULL,
    `ParentUserId`  INT         NOT NULL,
    `CreatedAt`     DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_ClassRemarkLike_RemarkParent` (`ClassRemarkId`, `ParentUserId`),
    CONSTRAINT `FK_ClassRemarkLikes_ClassRemarks` FOREIGN KEY (`ClassRemarkId`) REFERENCES `ClassRemarks`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 17. `TutorReviews` — BookingId column + unique index
-- ============================================================
ALTER TABLE `TutorReviews`
    ADD COLUMN IF NOT EXISTS `BookingId` INT NULL;

-- Add unique index if it doesn't already exist
SET @idx := (SELECT COUNT(1) FROM information_schema.statistics
    WHERE table_schema = DATABASE() AND table_name = 'TutorReviews'
    AND index_name = 'UQ_TutorReviews_TutorBooking');
SET @sql := IF(@idx = 0,
    'CREATE UNIQUE INDEX `UQ_TutorReviews_TutorBooking` ON `TutorReviews` (`TutorId`, `BookingId`)',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ============================================================
-- 18. `IssueReports` — new columns
-- ============================================================
ALTER TABLE `IssueReports`
    ADD COLUMN IF NOT EXISTS `CreatedAt`  DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    ADD COLUMN IF NOT EXISTS `Resolved`   TINYINT(1)  NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS `ResolvedAt` DATETIME(6) NULL;

-- ============================================================
-- 19. `Invoices` — new columns
-- ============================================================
ALTER TABLE `Invoices`
    ADD COLUMN IF NOT EXISTS `InvoiceNumber`       VARCHAR(20)   NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS `BaseAmount`          DECIMAL(10,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS `MarkupAmount`        DECIMAL(10,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS `MarkupPercent`       DECIMAL(5,2)  NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS `IsFirstMatch`        TINYINT(1)    NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS `WalletCreditApplied` DECIMAL(10,2) NOT NULL DEFAULT 0;

-- Backfill InvoiceNumber for existing rows
UPDATE `Invoices` SET `InvoiceNumber` = CONCAT('INV', LPAD(`Id`, 5, '0')) WHERE `InvoiceNumber` = '';

-- ============================================================
-- 20. `ChatMessages` — new columns
-- ============================================================
ALTER TABLE `ChatMessages`
    ADD COLUMN IF NOT EXISTS `ParentUserId` INT       NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS `IsRead`       TINYINT(1) NOT NULL DEFAULT 0;

-- ============================================================
-- 21. NEW TABLE: `FavoriteTutors`
-- ============================================================
CREATE TABLE IF NOT EXISTS `FavoriteTutors` (
    `Id`           INT         NOT NULL AUTO_INCREMENT,
    `ParentUserId` INT         NOT NULL,
    `TutorId`      INT         NOT NULL,
    `CreatedAt`    DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_FavoriteTutors_Parent_Tutor` (`ParentUserId`, `TutorId`),
    CONSTRAINT `FK_FavoriteTutors_Users`  FOREIGN KEY (`ParentUserId`) REFERENCES `Users`(`Id`)  ON DELETE CASCADE,
    CONSTRAINT `FK_FavoriteTutors_Tutors` FOREIGN KEY (`TutorId`)      REFERENCES `Tutors`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 22. NEW TABLE: `StudentTutorFirstClasses`
-- ============================================================
CREATE TABLE IF NOT EXISTS `StudentTutorFirstClasses` (
    `Id`        INT          NOT NULL AUTO_INCREMENT,
    `Country`   VARCHAR(50)  NOT NULL DEFAULT '',
    `Subject`   VARCHAR(100) NOT NULL DEFAULT '',
    `Level`     VARCHAR(100) NOT NULL DEFAULT '',
    `TutorId`   INT          NOT NULL,
    `StudentId` INT          NOT NULL,
    `BookingId` INT          NULL,
    `CreatedAt` DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_StudentTutorFirstClass` (`Country`,`Subject`,`Level`,`TutorId`,`StudentId`),
    KEY `IX_STFC_TutorId`   (`TutorId`),
    KEY `IX_STFC_StudentId` (`StudentId`),
    CONSTRAINT `FK_STFC_Tutors`    FOREIGN KEY (`TutorId`)   REFERENCES `Tutors`(`Id`)    ON DELETE CASCADE,
    CONSTRAINT `FK_STFC_Students`  FOREIGN KEY (`StudentId`) REFERENCES `Students`(`Id`)  ON DELETE CASCADE,
    CONSTRAINT `FK_STFC_Bookings`  FOREIGN KEY (`BookingId`) REFERENCES `Bookings`(`Id`)  ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 23. `LessonReports` — recreate with new schema if old shape exists
--     (old shape had a `Covered` column; new shape is per-student per-session)
-- ============================================================
DROP TABLE IF EXISTS `LessonReportEdits`;

-- Check if old shape (Covered column) still exists and drop if so
SET @old := (SELECT COUNT(1) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'LessonReports' AND column_name = 'Covered');
SET @sql := IF(@old > 0, 'DROP TABLE IF EXISTS `LessonReports`', 'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

CREATE TABLE IF NOT EXISTS `LessonReports` (
    `Id`                 INT          NOT NULL AUTO_INCREMENT,
    `BookingId`          INT          NOT NULL,
    `StudentId`          INT          NOT NULL,
    `SessionDate`        VARCHAR(20)  NOT NULL DEFAULT '',
    `Attendance`         VARCHAR(20)  NOT NULL DEFAULT '',
    `Engagement`         INT          NULL,
    `Understanding`      VARCHAR(30)  NULL,
    `HomeworkCompletion` VARCHAR(30)  NULL,
    `Remarks`            LONGTEXT     NULL,
    `SubmittedAt`        DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_LessonReport_BookingStudentDate` (`BookingId`,`StudentId`,`SessionDate`),
    KEY `IX_LessonReports_BookingId` (`BookingId`),
    KEY `IX_LessonReports_StudentId` (`StudentId`),
    CONSTRAINT `FK_LessonReports_Bookings`  FOREIGN KEY (`BookingId`)  REFERENCES `Bookings`(`Id`)  ON DELETE CASCADE,
    CONSTRAINT `FK_LessonReports_Students`  FOREIGN KEY (`StudentId`)  REFERENCES `Students`(`Id`)  ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 24. NEW TABLE: `SyllabusTopics`
-- ============================================================
CREATE TABLE IF NOT EXISTS `SyllabusTopics` (
    `Id`        INT          NOT NULL AUTO_INCREMENT,
    `Country`   VARCHAR(10)  NOT NULL DEFAULT '',
    `Subject`   VARCHAR(100) NOT NULL DEFAULT '',
    `Level`     VARCHAR(100) NOT NULL DEFAULT '',
    `Topic`     VARCHAR(200) NOT NULL DEFAULT '',
    `SortOrder` INT          NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_SyllabusTopic` (`Country`,`Subject`,`Level`,`Topic`),
    KEY `IX_SyllabusTopics_CSL` (`Country`,`Subject`,`Level`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 25. NEW TABLE: `PresetGroupSyllabuses`
-- ============================================================
CREATE TABLE IF NOT EXISTS `PresetGroupSyllabuses` (
    `Id`              INT         NOT NULL AUTO_INCREMENT,
    `PresetGroupId`   VARCHAR(20) NOT NULL DEFAULT '',
    `SyllabusTopicId` INT         NOT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_PresetGroupSyllabus` (`PresetGroupId`,`SyllabusTopicId`),
    KEY `IX_PresetGroupSyllabuses_PGId` (`PresetGroupId`),
    CONSTRAINT `FK_PGS_SyllabusTopics`
        FOREIGN KEY (`SyllabusTopicId`) REFERENCES `SyllabusTopics`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 26. NEW TABLE: `PresetCancellationDecisions`
-- ============================================================
CREATE TABLE IF NOT EXISTS `PresetCancellationDecisions` (
    `Id`             INT             NOT NULL AUTO_INCREMENT,
    `BookingId`      INT             NOT NULL,
    `OriginalDate`   LONGTEXT        NOT NULL,
    `OriginalTime`   LONGTEXT        NOT NULL,
    `OriginalEndTime` LONGTEXT       NOT NULL,
    `PricePerLesson` DECIMAL(10,2)   NOT NULL DEFAULT 0,
    `ProposedDate`   LONGTEXT        NULL,
    `ProposedTime`   LONGTEXT        NULL,
    `ProposedEndTime` LONGTEXT       NULL,
    `Status`         VARCHAR(20)     NOT NULL DEFAULT 'pending',
    `AcknowledgedAt` DATETIME(6)     NULL,
    `CreatedAt`      DATETIME(6)     NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `DecidedAt`      DATETIME(6)     NULL,
    `ResolvedAt`     DATETIME(6)     NULL,
    `AdminNote`      LONGTEXT        NULL,
    PRIMARY KEY (`Id`),
    KEY `IX_PresetCancellationDecisions_BookingId` (`BookingId`),
    KEY `IX_PresetCancellationDecisions_Status`    (`Status`),
    CONSTRAINT `FK_PresetCancellationDecisions_Bookings` FOREIGN KEY (`BookingId`) REFERENCES `Bookings`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 27. NEW TABLE: `TutorPenalties`
-- ============================================================
CREATE TABLE IF NOT EXISTS `TutorPenalties` (
    `Id`        INT             NOT NULL AUTO_INCREMENT,
    `TutorId`   INT             NOT NULL,
    `BookingId` INT             NULL,
    `Amount`    DECIMAL(10,2)   NOT NULL DEFAULT 0,
    `Reason`    LONGTEXT        NOT NULL,
    `CreatedAt` DATETIME(6)     NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_TutorPenalties_TutorId` (`TutorId`),
    CONSTRAINT `FK_TutorPenalties_Tutors` FOREIGN KEY (`TutorId`) REFERENCES `Tutors`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 28. NEW TABLE: `ScoringWeightages`
-- ============================================================
CREATE TABLE IF NOT EXISTS `ScoringWeightages` (
    `Id`        INT          NOT NULL AUTO_INCREMENT,
    `Key`       VARCHAR(20)  NOT NULL,
    `Label`     VARCHAR(100) NOT NULL,
    `Percent`   INT          NOT NULL DEFAULT 0,
    `SortOrder` INT          NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_ScoringWeightages_Key` (`Key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Deduplicate any rows from before unique index was added
DELETE w FROM `ScoringWeightages` w
JOIN `ScoringWeightages` k ON k.`Key` = w.`Key` AND k.`Id` < w.`Id`;

ALTER TABLE `ScoringWeightages` MODIFY `Key` VARCHAR(20) NOT NULL;

-- Add unique key if missing
SET @idx := (SELECT COUNT(1) FROM information_schema.statistics
    WHERE table_schema = DATABASE() AND table_name = 'ScoringWeightages'
    AND index_name = 'UQ_ScoringWeightages_Key');
SET @sql := IF(@idx = 0,
    'ALTER TABLE `ScoringWeightages` ADD UNIQUE KEY `UQ_ScoringWeightages_Key` (`Key`)',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Seed default rows
INSERT IGNORE INTO `ScoringWeightages` (`Key`, `Label`, `Percent`, `SortOrder`) VALUES
    ('rating',      'Tutor Rating',                        0, 0),
    ('activeness',  'Tutor Activeness (Refresh Monthly)',  0, 1),
    ('disputes',    'Tutor Dispute (Refresh Monthly)',     0, 2),
    ('experience',  'Tutor Experience',                    0, 3),
    ('na1',         'NA',                                  0, 4),
    ('na2',         'NA',                                  0, 5);

-- ============================================================
-- 29. NEW TABLE: `PaymentGatewaySettings`
-- ============================================================
CREATE TABLE IF NOT EXISTS `PaymentGatewaySettings` (
    `Id`           INT          NOT NULL AUTO_INCREMENT,
    `Provider`     VARCHAR(40)  NOT NULL DEFAULT 'hitpay',
    `IsEnabled`    TINYINT(1)   NOT NULL DEFAULT 0,
    `Mode`         VARCHAR(20)  NOT NULL DEFAULT 'sandbox',
    `ApiKey`       VARCHAR(500) NOT NULL DEFAULT '',
    `Salt`         VARCHAR(500) NOT NULL DEFAULT '',
    `Currency`     VARCHAR(10)  NOT NULL DEFAULT 'SGD',
    `ReturnUrl`    VARCHAR(500) NOT NULL DEFAULT '',
    `ApiBaseUrl`   VARCHAR(500) NULL,
    `ReturnOrigin` VARCHAR(500) NULL,
    `UpdatedAt`    DATETIME(6)  NULL,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 30. NEW TABLE: `PaymentTransactions`
-- ============================================================
CREATE TABLE IF NOT EXISTS `PaymentTransactions` (
    `Id`               INT           NOT NULL AUTO_INCREMENT,
    `InvoiceId`        INT           NOT NULL,
    `PaymentRequestId` VARCHAR(100)  NOT NULL DEFAULT '',
    `Amount`           DECIMAL(10,2) NOT NULL DEFAULT 0,
    `Status`           VARCHAR(40)   NOT NULL DEFAULT '',
    `ReturnOrigin`     VARCHAR(500)  NULL,
    `CreatedAt`        DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_PaymentTransactions_PaymentRequestId` (`PaymentRequestId`),
    CONSTRAINT `FK_PaymentTransactions_Invoices` FOREIGN KEY (`InvoiceId`) REFERENCES `Invoices`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 31. NEW TABLE: `CommissionSettings`
-- ============================================================
CREATE TABLE IF NOT EXISTS `CommissionSettings` (
    `Id`                          INT          NOT NULL AUTO_INCREMENT,
    `RatePercent`                 DECIMAL(5,2) NOT NULL DEFAULT 0,
    `EffectiveFrom`               DATETIME(6)  NULL,
    `MarkupPercent`               DECIMAL(5,2) NOT NULL DEFAULT 15,
    `FirstMatchCommissionPercent` DECIMAL(5,2) NOT NULL DEFAULT 100,
    `FirstMatchEffectiveFrom`     DATETIME(6)  NULL,
    `UpdatedAt`                   DATETIME(6)  NULL,
    `UpdatedByUserId`             INT          NULL,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 32. NEW TABLE: `TutorLedgerEntries`
-- ============================================================
CREATE TABLE IF NOT EXISTS `TutorLedgerEntries` (
    `Id`              INT           NOT NULL AUTO_INCREMENT,
    `TutorId`         INT           NOT NULL,
    `Fund`            VARCHAR(20)   NOT NULL DEFAULT 'withdrawable',
    `Type`            VARCHAR(40)   NOT NULL DEFAULT '',
    `Amount`          DECIMAL(10,2) NOT NULL DEFAULT 0,
    `InvoiceId`       INT           NULL,
    `PayoutId`        INT           NULL,
    `PenaltyId`       INT           NULL,
    `BookingId`       INT           NULL,
    `SourceEntryId`   INT           NULL,
    `Reason`          VARCHAR(500)  NOT NULL DEFAULT '',
    `ExpiresAt`       DATETIME(6)   NULL,
    `RatePercent`     DECIMAL(5,2)  NULL,
    `CreatedByUserId` INT           NULL,
    `CreatedAt`       DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_TutorLedgerEntries_TutorId`   (`TutorId`),
    KEY `IX_TutorLedgerEntries_InvoiceId` (`InvoiceId`),
    KEY `IX_TutorLedgerEntries_PayoutId`  (`PayoutId`),
    KEY `IX_TutorLedgerEntries_PenaltyId` (`PenaltyId`),
    CONSTRAINT `FK_TutorLedgerEntries_Tutors` FOREIGN KEY (`TutorId`) REFERENCES `Tutors`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 33. NEW TABLE: `ParentWalletEntries`
-- ============================================================
CREATE TABLE IF NOT EXISTS `ParentWalletEntries` (
    `Id`              INT           NOT NULL AUTO_INCREMENT,
    `ParentUserId`    INT           NOT NULL,
    `Type`            VARCHAR(40)   NOT NULL DEFAULT '',
    `Amount`          DECIMAL(10,2) NOT NULL DEFAULT 0,
    `InvoiceId`       INT           NULL,
    `SourceEntryId`   INT           NULL,
    `Reason`          VARCHAR(500)  NOT NULL DEFAULT '',
    `ExpiresAt`       DATETIME(6)   NULL,
    `CreatedByUserId` INT           NULL,
    `CreatedAt`       DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    KEY `IX_ParentWalletEntries_ParentUserId`  (`ParentUserId`),
    KEY `IX_ParentWalletEntries_InvoiceId`     (`InvoiceId`),
    KEY `IX_ParentWalletEntries_SourceEntryId` (`SourceEntryId`),
    CONSTRAINT `FK_ParentWalletEntries_Users` FOREIGN KEY (`ParentUserId`) REFERENCES `Users`(`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 34. NEW TABLE: `PayoutBatches`
-- ============================================================
CREATE TABLE IF NOT EXISTS `PayoutBatches` (
    `Id`                  INT           NOT NULL AUTO_INCREMENT,
    `BatchNumber`         VARCHAR(40)   NOT NULL DEFAULT '',
    `Period`              VARCHAR(7)    NOT NULL DEFAULT '',
    `TotalAmount`         DECIMAL(12,2) NOT NULL DEFAULT 0,
    `TutorCount`          INT           NOT NULL DEFAULT 0,
    `Status`              VARCHAR(20)   NOT NULL DEFAULT 'Pending',
    `CreatedAt`           DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `ApprovedAt`          DATETIME(6)   NULL,
    `ApprovedByUserId`    INT           NULL,
    `TransferredAt`       DATETIME(6)   NULL,
    `TransferredByUserId` INT           NULL,
    `Notes`               VARCHAR(1000) NULL,
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_PayoutBatch_Period` (`Period`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 35. NEW TABLE: `TutorPayables`
-- ============================================================
CREATE TABLE IF NOT EXISTS `TutorPayables` (
    `Id`           INT           NOT NULL AUTO_INCREMENT,
    `TutorId`      INT           NOT NULL,
    `Period`       VARCHAR(7)    NOT NULL DEFAULT '',
    `PeriodStart`  VARCHAR(10)   NOT NULL DEFAULT '',
    `PeriodEnd`    VARCHAR(10)   NOT NULL DEFAULT '',
    `Amount`       DECIMAL(10,2) NOT NULL DEFAULT 0,
    `SessionCount` INT           NOT NULL DEFAULT 0,
    `Status`       VARCHAR(20)   NOT NULL DEFAULT 'Pending',
    `PayoutBatchId` INT          NULL,
    `PayoutId`     INT           NULL,
    `CreatedAt`    DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `UQ_TutorPayable_Tutor_Period` (`TutorId`,`Period`),
    KEY `IX_TutorPayables_PayoutBatchId` (`PayoutBatchId`),
    CONSTRAINT `FK_TutorPayables_Tutors`        FOREIGN KEY (`TutorId`)       REFERENCES `Tutors`(`Id`)        ON DELETE CASCADE,
    CONSTRAINT `FK_TutorPayables_PayoutBatches` FOREIGN KEY (`PayoutBatchId`) REFERENCES `PayoutBatches`(`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 36. EF Migrations history table + registration
-- ============================================================
CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId`    VARCHAR(150) NOT NULL,
    `ProductVersion` VARCHAR(32)  NOT NULL,
    PRIMARY KEY (`MigrationId`)
);

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260630150745_AddReviewBookingId', '9.0.0');

-- ============================================================
-- END OF MIGRATION
-- ============================================================
