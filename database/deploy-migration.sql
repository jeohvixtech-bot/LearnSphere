-- ============================================================
-- LearnSphere — Incremental Deployment Migration SQL
-- Use this to upgrade an EXISTING production database.
-- Safe to run multiple times — all changes use IF NOT EXISTS /
-- IF EXISTS guards or ADD COLUMN ... checks.
--
-- Covers all changes from branch_28062026 (original deployment)
-- up to current branch (merge/hitpay-into-12092026).
-- ============================================================

USE LearnSphere;

-- ============================================================
-- 1. USERS — new columns
-- ============================================================
ALTER TABLE Users
    ADD COLUMN IF NOT EXISTS CreatedAt          DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    ADD COLUMN IF NOT EXISTS MustChangePassword TINYINT(1)  NOT NULL DEFAULT 0;

-- ============================================================
-- 2. TUTORS — new columns
-- ============================================================
ALTER TABLE Tutors
    ADD COLUMN IF NOT EXISTS IsOnline            TINYINT(1)   NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS VerificationStatus  VARCHAR(20)  NOT NULL DEFAULT 'not_submitted',
    ADD COLUMN IF NOT EXISTS OfferingsUnlocked   TINYINT(1)   NOT NULL DEFAULT 0;

-- Index for parent catalog filter
CREATE INDEX IF NOT EXISTS IX_Tutors_Verified_Online ON Tutors (IsVerified, IsOnline);

-- ============================================================
-- 3. TUTOR OFFERINGS — new column
-- ============================================================
ALTER TABLE TutorOfferings
    ADD COLUMN IF NOT EXISTS Country VARCHAR(50) NOT NULL DEFAULT 'Singapore';

-- ============================================================
-- 4. NEW TABLE: TutorDocuments
-- ============================================================
CREATE TABLE IF NOT EXISTS TutorDocuments (
    Id                  INT AUTO_INCREMENT PRIMARY KEY,
    TutorId             INT             NOT NULL,
    DocumentType        VARCHAR(30)     NOT NULL DEFAULT '',
    FileUrl             LONGTEXT        NULL,
    ExternalUrl         LONGTEXT        NULL,
    FileName            LONGTEXT        NULL,
    FileSizeBytes       BIGINT          NULL,
    IdType              VARCHAR(20)     NULL,
    IdNumber            LONGTEXT        NULL,
    SortOrder           INT             NOT NULL DEFAULT 0,
    Status              VARCHAR(20)     NOT NULL DEFAULT 'pending',
    AdminNote           LONGTEXT        NULL,
    UploadedAt          DATETIME(6)     NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    ReviewedAt          DATETIME(6)     NULL,
    ReplacesDocumentId  INT             NULL,
    IsArchived          TINYINT(1)      NOT NULL DEFAULT 0,
    KEY IX_TutorDocuments_TutorId (TutorId),
    CONSTRAINT FK_TutorDocuments_Tutors FOREIGN KEY (TutorId) REFERENCES Tutors(Id) ON DELETE CASCADE
);

-- ============================================================
-- 5. TUTOR TIME SLOTS — new columns (preset class support)
-- ============================================================
ALTER TABLE TutorTimeSlots
    ADD COLUMN IF NOT EXISTS EndTime             LONGTEXT        NULL,
    ADD COLUMN IF NOT EXISTS DurationMinutes     INT             NOT NULL DEFAULT 60,
    ADD COLUMN IF NOT EXISTS Mode                LONGTEXT        NULL,
    ADD COLUMN IF NOT EXISTS Subject             LONGTEXT        NULL,
    ADD COLUMN IF NOT EXISTS Level               LONGTEXT        NULL,
    ADD COLUMN IF NOT EXISTS Country             LONGTEXT        NULL,
    ADD COLUMN IF NOT EXISTS ClassSize           VARCHAR(20)     NOT NULL DEFAULT 'one-to-one',
    ADD COLUMN IF NOT EXISTS MaxStudents         INT             NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS ConfirmedCount      INT             NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS IsFull              TINYINT(1)      NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS PricePerLesson      DECIMAL(10,2)   NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS PresetGroupId       VARCHAR(20)     NULL,
    ADD COLUMN IF NOT EXISTS VideoConferenceLink LONGTEXT        NULL;

-- ============================================================
-- 6. NEW TABLE: TutorBlockedDates
-- ============================================================
CREATE TABLE IF NOT EXISTS TutorBlockedDates (
    Id          INT          NOT NULL AUTO_INCREMENT,
    TutorId     INT          NOT NULL,
    StartDate   VARCHAR(10)  NOT NULL,
    EndDate     VARCHAR(10)  NOT NULL,
    CreatedAt   DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (Id),
    KEY IX_TutorBlockedDates_TutorId (TutorId),
    CONSTRAINT FK_TutorBlockedDates_Tutors FOREIGN KEY (TutorId) REFERENCES Tutors(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 7. NEW TABLE: SyllabusTopics
-- ============================================================
CREATE TABLE IF NOT EXISTS SyllabusTopics (
    Id          INT          NOT NULL AUTO_INCREMENT,
    Country     VARCHAR(10)  NOT NULL DEFAULT '',
    Subject     VARCHAR(100) NOT NULL DEFAULT '',
    Level       VARCHAR(100) NOT NULL DEFAULT '',
    Topic       VARCHAR(200) NOT NULL DEFAULT '',
    SortOrder   INT          NOT NULL DEFAULT 0,
    PRIMARY KEY (Id),
    UNIQUE KEY UQ_SyllabusTopic (Country, Subject, Level, Topic),
    KEY IX_SyllabusTopics_Country_Subject_Level (Country, Subject, Level)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 8. NEW TABLE: PresetGroupSyllabuses
-- ============================================================
CREATE TABLE IF NOT EXISTS PresetGroupSyllabuses (
    Id              INT          NOT NULL AUTO_INCREMENT,
    PresetGroupId   VARCHAR(20)  NOT NULL DEFAULT '',
    SyllabusTopicId INT          NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE KEY UQ_PresetGroupSyllabus (PresetGroupId, SyllabusTopicId),
    KEY IX_PresetGroupSyllabuses_PresetGroupId (PresetGroupId),
    CONSTRAINT FK_PresetGroupSyllabuses_SyllabusTopics
        FOREIGN KEY (SyllabusTopicId) REFERENCES SyllabusTopics(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- 9. STUDENTS — new column
-- ============================================================
ALTER TABLE Students
    ADD COLUMN IF NOT EXISTS IsArchived TINYINT(1) NOT NULL DEFAULT 0;

-- ============================================================
-- 10. NEW TABLE: StudentTutorFirstClasses
-- ============================================================
CREATE TABLE IF NOT EXISTS StudentTutorFirstClasses (
    Id        INT          NOT NULL AUTO_INCREMENT,
    Country   VARCHAR(50)  NOT NULL DEFAULT '',
    Subject   VARCHAR(100) NOT NULL DEFAULT '',
    Level     VARCHAR(100) NOT NULL DEFAULT '',
    TutorId   INT          NOT NULL,
    StudentId INT          NOT NULL,
    BookingId INT          NULL,
    CreatedAt DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (Id),
    UNIQUE KEY UQ_StudentTutorFirstClass (Country, Subject, Level, TutorId, StudentId),
    KEY IX_STFC_TutorId   (TutorId),
    KEY IX_STFC_StudentId (StudentId),
    CONSTRAINT FK_STFC_Tutors    FOREIGN KEY (TutorId)   REFERENCES Tutors(Id)    ON DELETE CASCADE,
    CONSTRAINT FK_STFC_Students  FOREIGN KEY (StudentId) REFERENCES Students(Id)  ON DELETE CASCADE,
    CONSTRAINT FK_STFC_Bookings  FOREIGN KEY (BookingId) REFERENCES Bookings(Id)  ON DELETE SET NULL
);

-- ============================================================
-- 11. NEW TABLE: StudentPreferredModes
-- ============================================================
CREATE TABLE IF NOT EXISTS StudentPreferredModes (
    Id        INT          AUTO_INCREMENT PRIMARY KEY,
    StudentId INT          NOT NULL,
    Mode      VARCHAR(50)  NOT NULL,
    Sequence  INT          NOT NULL DEFAULT 0,
    KEY IX_StudentPreferredModes_StudentId (StudentId),
    CONSTRAINT FK_StudentPreferredModes_Students FOREIGN KEY (StudentId) REFERENCES Students(Id) ON DELETE CASCADE
);

-- ============================================================
-- 12. BOOKINGS — new columns
-- ============================================================
ALTER TABLE Bookings
    ADD COLUMN IF NOT EXISTS BookingNumber          LONGTEXT        NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS BookingType            VARCHAR(20)     NOT NULL DEFAULT 'parent-offer',
    ADD COLUMN IF NOT EXISTS PresetSlotId           INT             NULL,
    ADD COLUMN IF NOT EXISTS VideoConferenceLink    LONGTEXT        NULL,
    ADD COLUMN IF NOT EXISTS VideoLinkReminderStatus VARCHAR(20)    NOT NULL DEFAULT 'none',
    ADD COLUMN IF NOT EXISTS Date                   LONGTEXT        NULL,
    ADD COLUMN IF NOT EXISTS Time                   LONGTEXT        NULL,
    ADD COLUMN IF NOT EXISTS SlotId                 INT             NULL;

ALTER TABLE Bookings MODIFY COLUMN DurationHours DOUBLE NOT NULL DEFAULT 1;

-- ============================================================
-- 13. BOOKING CLASSES — new columns
-- ============================================================
ALTER TABLE BookingClasses
    ADD COLUMN IF NOT EXISTS Status         VARCHAR(20)  NOT NULL DEFAULT 'scheduled',
    ADD COLUMN IF NOT EXISTS DeliveryStatus VARCHAR(20)  NOT NULL DEFAULT 'Scheduled',
    ADD COLUMN IF NOT EXISTS DeliveredAt    DATETIME(6)  NULL;

-- ============================================================
-- 14. NEW TABLE: BookingPresetSlots
-- ============================================================
CREATE TABLE IF NOT EXISTS BookingPresetSlots (
    Id               INT AUTO_INCREMENT PRIMARY KEY,
    BookingId        INT NOT NULL,
    TutorTimeSlotId  INT NOT NULL,
    KEY IX_BookingPresetSlots_BookingId (BookingId),
    CONSTRAINT FK_BookingPresetSlots_Bookings       FOREIGN KEY (BookingId)       REFERENCES Bookings(Id)       ON DELETE CASCADE,
    CONSTRAINT FK_BookingPresetSlots_TutorTimeSlots FOREIGN KEY (TutorTimeSlotId) REFERENCES TutorTimeSlots(Id) ON DELETE CASCADE
);

-- ============================================================
-- 15. COUNTER PROPOSALS — new columns (legacy date/time made nullable)
-- ============================================================
ALTER TABLE CounterProposals
    ADD COLUMN IF NOT EXISTS ProposedBy VARCHAR(20) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS CreatedAt  DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6);

ALTER TABLE CounterProposals MODIFY COLUMN Date LONGTEXT NULL;
ALTER TABLE CounterProposals MODIFY COLUMN Time LONGTEXT NULL;

-- ============================================================
-- 16. NEW TABLE: CounterProposalClasses
-- ============================================================
CREATE TABLE IF NOT EXISTS CounterProposalClasses (
    Id                  INT AUTO_INCREMENT PRIMARY KEY,
    CounterProposalId   INT      NOT NULL,
    OriginalDate        LONGTEXT NOT NULL,
    OriginalTime        LONGTEXT NOT NULL,
    ProposedDate        LONGTEXT NOT NULL,
    ProposedTime        LONGTEXT NOT NULL,
    CONSTRAINT FK_CounterProposalClasses_CounterProposals
        FOREIGN KEY (CounterProposalId) REFERENCES CounterProposals(Id) ON DELETE CASCADE
);

-- ============================================================
-- 17. NEW TABLE: ClassRemarks (replaces TutorReviews)
-- ============================================================
CREATE TABLE IF NOT EXISTS ClassRemarks (
    Id                INT          AUTO_INCREMENT PRIMARY KEY,
    BookingClassId    INT          NOT NULL,
    TutorId           INT          NOT NULL,
    ParentUserId      INT          NOT NULL,
    ParentDisplayName VARCHAR(200) NOT NULL DEFAULT '',
    Rating            INT          NOT NULL DEFAULT 5,
    Text              LONGTEXT     NOT NULL,
    Status            VARCHAR(20)  NOT NULL DEFAULT 'published',
    DisputeReason     LONGTEXT     NULL,
    CreatedAt         DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    EditedAt          DATETIME(6)  NULL,
    ResolvedAt        DATETIME(6)  NULL,
    UNIQUE KEY UQ_ClassRemark_BookingClass (BookingClassId),
    KEY IX_ClassRemarks_TutorId (TutorId),
    CONSTRAINT FK_ClassRemarks_BookingClasses FOREIGN KEY (BookingClassId) REFERENCES BookingClasses(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ClassRemarks_Tutors         FOREIGN KEY (TutorId)        REFERENCES Tutors(Id)        ON DELETE CASCADE
);

-- ============================================================
-- 18. NEW TABLE: ClassRemarkLikes
-- ============================================================
CREATE TABLE IF NOT EXISTS ClassRemarkLikes (
    Id             INT         AUTO_INCREMENT PRIMARY KEY,
    ClassRemarkId  INT         NOT NULL,
    ParentUserId   INT         NOT NULL,
    CreatedAt      DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    UNIQUE KEY UQ_ClassRemarkLike_RemarkParent (ClassRemarkId, ParentUserId),
    CONSTRAINT FK_ClassRemarkLikes_ClassRemarks FOREIGN KEY (ClassRemarkId) REFERENCES ClassRemarks(Id) ON DELETE CASCADE
);

-- ============================================================
-- 19. ISSUE REPORTS — new column
-- ============================================================
ALTER TABLE IssueReports
    ADD COLUMN IF NOT EXISTS CreatedAt  DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    ADD COLUMN IF NOT EXISTS ResolvedAt DATETIME(6) NULL,
    ADD COLUMN IF NOT EXISTS Resolved   TINYINT(1)  NOT NULL DEFAULT 0;

-- ============================================================
-- 20. INVOICES — new columns (markup + first-match + wallet)
-- ============================================================
ALTER TABLE Invoices
    ADD COLUMN IF NOT EXISTS InvoiceNumber       LONGTEXT        NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS BaseAmount          DECIMAL(10,2)   NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS MarkupAmount        DECIMAL(10,2)   NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS MarkupPercent       DECIMAL(5,2)    NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS IsFirstMatch        TINYINT(1)      NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS WalletCreditApplied DECIMAL(10,2)   NOT NULL DEFAULT 0;

-- ============================================================
-- 21. NEW TABLE: PresetCancellationDecisions
-- ============================================================
CREATE TABLE IF NOT EXISTS PresetCancellationDecisions (
    Id               INT AUTO_INCREMENT PRIMARY KEY,
    BookingId        INT             NOT NULL,
    OriginalDate     LONGTEXT        NOT NULL,
    OriginalTime     LONGTEXT        NOT NULL,
    OriginalEndTime  LONGTEXT        NOT NULL,
    PricePerLesson   DECIMAL(10,2)   NOT NULL DEFAULT 0,
    ProposedDate     LONGTEXT        NULL,
    ProposedTime     LONGTEXT        NULL,
    ProposedEndTime  LONGTEXT        NULL,
    Status           VARCHAR(20)     NOT NULL DEFAULT 'pending',
    AcknowledgedAt   DATETIME(6)     NULL,
    CreatedAt        DATETIME(6)     NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    DecidedAt        DATETIME(6)     NULL,
    ResolvedAt       DATETIME(6)     NULL,
    AdminNote        LONGTEXT        NULL,
    KEY IX_PresetCancellationDecisions_BookingId (BookingId),
    KEY IX_PresetCancellationDecisions_Status    (Status),
    CONSTRAINT FK_PresetCancellationDecisions_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(Id) ON DELETE CASCADE
);

-- ============================================================
-- 22. NEW TABLE: TutorPenalties
-- ============================================================
CREATE TABLE IF NOT EXISTS TutorPenalties (
    Id         INT AUTO_INCREMENT PRIMARY KEY,
    TutorId    INT             NOT NULL,
    BookingId  INT             NULL,
    Amount     DECIMAL(10,2)   NOT NULL DEFAULT 0,
    Reason     LONGTEXT        NOT NULL,
    CreatedAt  DATETIME(6)     NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    KEY IX_TutorPenalties_TutorId (TutorId),
    CONSTRAINT FK_TutorPenalties_Tutors FOREIGN KEY (TutorId) REFERENCES Tutors(Id) ON DELETE CASCADE
);

-- ============================================================
-- 23. NEW TABLE: FavoriteTutors
-- ============================================================
CREATE TABLE IF NOT EXISTS FavoriteTutors (
    Id            INT AUTO_INCREMENT PRIMARY KEY,
    ParentUserId  INT          NOT NULL,
    TutorId       INT          NOT NULL,
    CreatedAt     DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    UNIQUE KEY UQ_FavoriteTutors_Parent_Tutor (ParentUserId, TutorId),
    CONSTRAINT FK_FavoriteTutors_Users  FOREIGN KEY (ParentUserId) REFERENCES Users(Id)  ON DELETE CASCADE,
    CONSTRAINT FK_FavoriteTutors_Tutors FOREIGN KEY (TutorId)      REFERENCES Tutors(Id) ON DELETE CASCADE
);

-- ============================================================
-- 24. CHAT MESSAGES — new columns
-- ============================================================
ALTER TABLE ChatMessages
    ADD COLUMN IF NOT EXISTS ParentUserId INT       NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS IsRead       TINYINT(1) NOT NULL DEFAULT 0;

-- ============================================================
-- 25. NEW TABLE: ScoringWeightages
-- ============================================================
CREATE TABLE IF NOT EXISTS ScoringWeightages (
    Id         INT AUTO_INCREMENT PRIMARY KEY,
    `Key`      VARCHAR(20)  NOT NULL,
    Label      VARCHAR(100) NOT NULL,
    Percent    INT          NOT NULL DEFAULT 0,
    SortOrder  INT          NOT NULL DEFAULT 0,
    UNIQUE KEY UQ_ScoringWeightages_Key (`Key`)
);

-- Seed default scoring weightages (safe to run — ignores duplicates)
INSERT IGNORE INTO ScoringWeightages (`Key`, Label, Percent, SortOrder) VALUES
    ('rating',      'Tutor Rating',           25, 1),
    ('activeness',  'Tutor Activeness',        25, 2),
    ('disputes',    'Tutor Dispute (Refresh Monthly)', 25, 3),
    ('experience',  'Tutor Experience',        25, 4),
    ('na1',         'N/A',                      0, 5),
    ('na2',         'N/A',                      0, 6);

-- ============================================================
-- 26. NEW TABLE: PaymentGatewaySettings
-- ============================================================
CREATE TABLE IF NOT EXISTS PaymentGatewaySettings (
    Id           INT AUTO_INCREMENT PRIMARY KEY,
    Provider     VARCHAR(40)  NOT NULL DEFAULT 'hitpay',
    IsEnabled    TINYINT(1)   NOT NULL DEFAULT 0,
    Mode         VARCHAR(20)  NOT NULL DEFAULT 'sandbox',
    ApiKey       VARCHAR(500) NOT NULL DEFAULT '',
    Salt         VARCHAR(500) NOT NULL DEFAULT '',
    Currency     VARCHAR(10)  NOT NULL DEFAULT 'SGD',
    ReturnUrl    VARCHAR(500) NOT NULL DEFAULT '',
    ApiBaseUrl   VARCHAR(500) NULL,
    ReturnOrigin VARCHAR(500) NULL,
    UpdatedAt    DATETIME(6)  NULL
);

-- ============================================================
-- 27. NEW TABLE: PaymentTransactions
-- ============================================================
CREATE TABLE IF NOT EXISTS PaymentTransactions (
    Id               INT AUTO_INCREMENT PRIMARY KEY,
    InvoiceId        INT           NOT NULL,
    PaymentRequestId VARCHAR(100)  NOT NULL DEFAULT '',
    Amount           DECIMAL(10,2) NOT NULL DEFAULT 0,
    Status           VARCHAR(40)   NOT NULL DEFAULT '',
    ReturnOrigin     VARCHAR(500)  NULL,
    CreatedAt        DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    KEY IX_PaymentTransactions_PaymentRequestId (PaymentRequestId),
    CONSTRAINT FK_PaymentTransactions_Invoices FOREIGN KEY (InvoiceId) REFERENCES Invoices(Id) ON DELETE CASCADE
);

-- ============================================================
-- 28. NEW TABLE: CommissionSettings
-- ============================================================
CREATE TABLE IF NOT EXISTS CommissionSettings (
    Id                          INT AUTO_INCREMENT PRIMARY KEY,
    RatePercent                 DECIMAL(5,2) NOT NULL DEFAULT 0,
    EffectiveFrom               DATETIME(6)  NULL,
    MarkupPercent               DECIMAL(5,2) NOT NULL DEFAULT 15,
    FirstMatchCommissionPercent DECIMAL(5,2) NOT NULL DEFAULT 100,
    FirstMatchEffectiveFrom     DATETIME(6)  NULL,
    UpdatedAt                   DATETIME(6)  NULL,
    UpdatedByUserId             INT          NULL
);

-- ============================================================
-- 29. NEW TABLE: TutorLedgerEntries
-- ============================================================
CREATE TABLE IF NOT EXISTS TutorLedgerEntries (
    Id              INT AUTO_INCREMENT PRIMARY KEY,
    TutorId         INT           NOT NULL,
    Fund            VARCHAR(20)   NOT NULL DEFAULT 'withdrawable',
    Type            VARCHAR(40)   NOT NULL DEFAULT '',
    Amount          DECIMAL(10,2) NOT NULL DEFAULT 0,
    InvoiceId       INT           NULL,
    PayoutId        INT           NULL,
    PenaltyId       INT           NULL,
    BookingId       INT           NULL,
    SourceEntryId   INT           NULL,
    Reason          VARCHAR(500)  NOT NULL DEFAULT '',
    ExpiresAt       DATETIME(6)   NULL,
    RatePercent     DECIMAL(5,2)  NULL,
    CreatedByUserId INT           NULL,
    CreatedAt       DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    KEY IX_TutorLedgerEntries_TutorId   (TutorId),
    KEY IX_TutorLedgerEntries_InvoiceId (InvoiceId),
    KEY IX_TutorLedgerEntries_PayoutId  (PayoutId),
    KEY IX_TutorLedgerEntries_PenaltyId (PenaltyId),
    CONSTRAINT FK_TutorLedgerEntries_Tutors FOREIGN KEY (TutorId) REFERENCES Tutors(Id) ON DELETE CASCADE
);

-- ============================================================
-- 30. NEW TABLE: ParentWalletEntries
-- ============================================================
CREATE TABLE IF NOT EXISTS ParentWalletEntries (
    Id              INT AUTO_INCREMENT PRIMARY KEY,
    ParentUserId    INT           NOT NULL,
    Type            VARCHAR(40)   NOT NULL DEFAULT '',
    Amount          DECIMAL(10,2) NOT NULL DEFAULT 0,
    InvoiceId       INT           NULL,
    SourceEntryId   INT           NULL,
    Reason          VARCHAR(500)  NOT NULL DEFAULT '',
    ExpiresAt       DATETIME(6)   NULL,
    CreatedByUserId INT           NULL,
    CreatedAt       DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    KEY IX_ParentWalletEntries_ParentUserId  (ParentUserId),
    KEY IX_ParentWalletEntries_InvoiceId     (InvoiceId),
    KEY IX_ParentWalletEntries_SourceEntryId (SourceEntryId),
    CONSTRAINT FK_ParentWalletEntries_Users FOREIGN KEY (ParentUserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- ============================================================
-- 31. NEW TABLE: PayoutBatches
-- ============================================================
CREATE TABLE IF NOT EXISTS PayoutBatches (
    Id                  INT AUTO_INCREMENT PRIMARY KEY,
    BatchNumber         VARCHAR(40)   NOT NULL DEFAULT '',
    Period              VARCHAR(7)    NOT NULL DEFAULT '',
    TotalAmount         DECIMAL(12,2) NOT NULL DEFAULT 0,
    TutorCount          INT           NOT NULL DEFAULT 0,
    Status              VARCHAR(20)   NOT NULL DEFAULT 'Pending',
    CreatedAt           DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    ApprovedAt          DATETIME(6)   NULL,
    ApprovedByUserId    INT           NULL,
    TransferredAt       DATETIME(6)   NULL,
    TransferredByUserId INT           NULL,
    Notes               VARCHAR(1000) NULL,
    UNIQUE KEY UQ_PayoutBatch_Period (Period)
);

-- ============================================================
-- 32. NEW TABLE: TutorPayables
-- ============================================================
CREATE TABLE IF NOT EXISTS TutorPayables (
    Id            INT AUTO_INCREMENT PRIMARY KEY,
    TutorId       INT           NOT NULL,
    Period        VARCHAR(7)    NOT NULL DEFAULT '',
    PeriodStart   VARCHAR(10)   NOT NULL DEFAULT '',
    PeriodEnd     VARCHAR(10)   NOT NULL DEFAULT '',
    Amount        DECIMAL(10,2) NOT NULL DEFAULT 0,
    SessionCount  INT           NOT NULL DEFAULT 0,
    Status        VARCHAR(20)   NOT NULL DEFAULT 'Pending',
    PayoutBatchId INT           NULL,
    PayoutId      INT           NULL,
    CreatedAt     DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    UNIQUE KEY UQ_TutorPayable_Tutor_Period (TutorId, Period),
    KEY IX_TutorPayables_PayoutBatchId (PayoutBatchId),
    CONSTRAINT FK_TutorPayables_Tutors        FOREIGN KEY (TutorId)       REFERENCES Tutors(Id)        ON DELETE CASCADE,
    CONSTRAINT FK_TutorPayables_PayoutBatches FOREIGN KEY (PayoutBatchId) REFERENCES PayoutBatches(Id) ON DELETE SET NULL
);

-- ============================================================
-- 33. TUTOR REVIEWS — BookingId column + unique index
--     (EF migration: 20260630150745_AddReviewBookingId)
-- ============================================================
ALTER TABLE TutorReviews
    ADD COLUMN IF NOT EXISTS BookingId    INT NULL,
    ADD COLUMN IF NOT EXISTS BookingIdUniq INT NULL;

-- Only add the unique index if it doesn't already exist
SET @idx_exists = (
    SELECT COUNT(1) FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name   = 'TutorReviews'
      AND index_name   = 'UQ_TutorReviews_Booking'
);
SET @sql = IF(@idx_exists = 0,
    'CREATE UNIQUE INDEX UQ_TutorReviews_Booking ON TutorReviews (TutorId, BookingIdUniq)',
    'SELECT ''UQ_TutorReviews_Booking already exists, skipping'''
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ============================================================
-- 34. LessonReports — drop old LessonReportEdits table if present
-- ============================================================
DROP TABLE IF EXISTS LessonReportEdits;

-- ============================================================
-- 35. EF Migrations history — register the migration so EF doesn't
--     try to re-apply it on startup
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
