-- LearnSphere MySQL Schema
-- Generated from EF Core models (AppDbContext + AppDbContextModelSnapshot)
-- Last updated: reflects all migrations including FavoriteTutors, StudentPreferredModes,
-- ChatMessages.ParentUserId, and tutor-preset class slots (Flow B)
-- Run this against a fresh MySQL instance to create the full schema manually.
-- (The .NET backend uses EF Core migrations at runtime — this file is for manual/reference use.)

CREATE DATABASE IF NOT EXISTS LearnSphere CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE LearnSphere;

-- ============================================================
-- Users
-- ============================================================
CREATE TABLE IF NOT EXISTS Users (
    Id           INT AUTO_INCREMENT PRIMARY KEY,
    Email        LONGTEXT        NOT NULL,
    PasswordHash LONGTEXT        NOT NULL,
    Role         LONGTEXT        NOT NULL DEFAULT 'parent',  -- parent | tutor | admin
    Name         LONGTEXT        NOT NULL,
    CreatedAt    DATETIME(6)     NOT NULL,
    MustChangePassword TINYINT(1) NOT NULL DEFAULT 0
);

-- ============================================================
-- Tutors
-- ============================================================
CREATE TABLE IF NOT EXISTS Tutors (
    Id               INT AUTO_INCREMENT PRIMARY KEY,
    UserId           INT             NOT NULL UNIQUE,
    ImageUrl         LONGTEXT        NOT NULL,
    Rating           DOUBLE          NOT NULL DEFAULT 0,
    ReviewCount      INT             NOT NULL DEFAULT 0,
    PricePerSession  DECIMAL(10,2)   NOT NULL DEFAULT 0,
    ExperienceYears  INT             NOT NULL DEFAULT 0,
    Bio              LONGTEXT        NOT NULL,
    IsVerified       TINYINT(1)      NOT NULL DEFAULT 0,
    IsOnline         TINYINT(1)      NOT NULL DEFAULT 1,  -- offline hides the profile from parent search/booking entirely
    VerificationStatus VARCHAR(20)   NOT NULL DEFAULT 'not_submitted', -- not_submitted | pending | approved (no separate terminal "rejected" — see TutorDocuments)
    OfferingsUnlocked  TINYINT(1)    NOT NULL DEFAULT 0,  -- gates the offering builder until mandatory documents are approved
    CONSTRAINT FK_Tutors_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- ============================================================
-- Tutor — detail tables
-- ============================================================
CREATE TABLE IF NOT EXISTS TutorSubjects (
    Id        INT AUTO_INCREMENT PRIMARY KEY,
    TutorId   INT             NOT NULL,
    Subject   LONGTEXT        NOT NULL,
    Price     DECIMAL(10,2)   NULL,
    CONSTRAINT FK_TutorSubjects_Tutors FOREIGN KEY (TutorId) REFERENCES Tutors(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS TutorLevels (
    Id        INT AUTO_INCREMENT PRIMARY KEY,
    TutorId   INT         NOT NULL,
    Level     LONGTEXT    NOT NULL,
    CONSTRAINT FK_TutorLevels_Tutors FOREIGN KEY (TutorId) REFERENCES Tutors(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS TutorModes (
    Id        INT AUTO_INCREMENT PRIMARY KEY,
    TutorId   INT         NOT NULL,
    Mode      LONGTEXT    NOT NULL,
    CONSTRAINT FK_TutorModes_Tutors FOREIGN KEY (TutorId) REFERENCES Tutors(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS TutorQualifications (
    Id             INT AUTO_INCREMENT PRIMARY KEY,
    TutorId        INT         NOT NULL,
    Qualification  LONGTEXT    NOT NULL,
    CONSTRAINT FK_TutorQualifications_Tutors FOREIGN KEY (TutorId) REFERENCES Tutors(Id) ON DELETE CASCADE
);

-- Composite offerings (subject + level + mode + qualification + price in one row)
CREATE TABLE IF NOT EXISTS TutorOfferings (
    Id             INT AUTO_INCREMENT PRIMARY KEY,
    TutorId        INT             NOT NULL,
    Country        VARCHAR(50)     NOT NULL DEFAULT 'Singapore', -- Singapore | Malaysia
    Subject        LONGTEXT        NOT NULL,
    Level          LONGTEXT        NOT NULL,
    Mode           LONGTEXT        NOT NULL,
    Qualification  LONGTEXT        NOT NULL,
    Price          DECIMAL(10,2)   NOT NULL DEFAULT 0,
    CONSTRAINT FK_TutorOfferings_Tutors FOREIGN KEY (TutorId) REFERENCES Tutors(Id) ON DELETE CASCADE
);

-- ============================================================
-- TutorDocuments  (tutor verification uploads — identity, academic, teaching
-- credentials, intro video link, specialist certs. identity_photo and intro_video
-- are single-slot; o_level/a_level/degree/postgrad/nie_cert/specialist_cert allow
-- up to 3 rows each, ordered by SortOrder. Re-upload after rejection creates a new
-- row (ReplacesDocumentId points at the old rejected row) instead of overwriting —
-- see TutorsController.SaveDocument/ApplyVerificationDecisions.)
-- ============================================================
CREATE TABLE IF NOT EXISTS TutorDocuments (
    Id                  INT AUTO_INCREMENT PRIMARY KEY,
    TutorId             INT             NOT NULL,
    DocumentType        VARCHAR(30)     NOT NULL DEFAULT '', -- identity_photo | o_level | a_level | degree | postgrad | nie_cert | intro_video | specialist_cert
    FileUrl             LONGTEXT        NULL,
    ExternalUrl         LONGTEXT        NULL,                -- intro_video is link-only (no file upload)
    FileName            LONGTEXT        NULL,
    FileSizeBytes       BIGINT          NULL,
    IdType              VARCHAR(20)     NULL,                -- identity_photo only: NRIC | WorkPassSG | MyKad | Passport
    IdNumber            LONGTEXT        NULL,                -- identity_photo only
    SortOrder           INT             NOT NULL DEFAULT 0,
    Status              VARCHAR(20)     NOT NULL DEFAULT 'pending', -- pending | approved | rejected
    AdminNote           LONGTEXT        NULL,
    UploadedAt          DATETIME(6)     NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    ReviewedAt          DATETIME(6)     NULL,
    ReplacesDocumentId  INT             NULL,                -- self-referencing: the rejected row this re-upload supersedes
    IsArchived          TINYINT(1)      NOT NULL DEFAULT 0,   -- superseded-and-approved rows are archived, not deleted (audit trail)
    KEY IX_TutorDocuments_TutorId (TutorId),
    CONSTRAINT FK_TutorDocuments_Tutors FOREIGN KEY (TutorId) REFERENCES Tutors(Id) ON DELETE CASCADE
);

-- ============================================================
-- TutorReviews
-- Added: BookingId (nullable) + filtered unique index (task 2 / AddReviewBookingId migration)
-- ============================================================
CREATE TABLE IF NOT EXISTS TutorReviews (
    Id         INT AUTO_INCREMENT PRIMARY KEY,
    TutorId    INT         NOT NULL,
    Author     LONGTEXT    NOT NULL,
    Text       LONGTEXT    NOT NULL,
    Rating     INT         NOT NULL DEFAULT 5,
    BookingId  INT         NULL,                        -- nullable; links review to a specific completed booking
    CONSTRAINT FK_TutorReviews_Tutors FOREIGN KEY (TutorId) REFERENCES Tutors(Id) ON DELETE CASCADE
);

-- Filtered unique index: one review per (tutor, booking) — only enforced when BookingId IS NOT NULL
-- MySQL equivalent of the EF Core HasFilter("[BookingId] IS NOT NULL")
CREATE UNIQUE INDEX UQ_TutorReviews_TutorBooking
    ON TutorReviews (TutorId, BookingId);               -- MySQL enforces uniqueness only on non-NULL pairs naturally

-- ============================================================
-- TutorTimeSlots
-- ============================================================
-- Day/Time hold a preset slot's date ("YYYY-MM-DD") and start time when the slot
-- represents a tutor-preset class (BookingType = 'tutor-preset' on Bookings); the
-- extra columns below are only populated for those rows.
CREATE TABLE IF NOT EXISTS TutorTimeSlots (
    Id               INT AUTO_INCREMENT PRIMARY KEY,
    TutorId          INT             NOT NULL,
    Day              LONGTEXT        NOT NULL,
    Time             LONGTEXT        NOT NULL,
    Status           LONGTEXT        NOT NULL DEFAULT 'Available', -- Available | Booked
    BookingId        INT             NULL,
    EndTime          LONGTEXT        NULL,
    DurationMinutes  INT             NOT NULL DEFAULT 60,
    Mode             LONGTEXT        NULL,
    Subject          LONGTEXT        NULL,
    Level            LONGTEXT        NULL,
    Country          LONGTEXT        NULL,
    ClassSize        VARCHAR(20)     NOT NULL DEFAULT 'one-to-one', -- one-to-one | one-to-many
    MaxStudents      INT             NOT NULL DEFAULT 1,
    ConfirmedCount   INT             NOT NULL DEFAULT 0,
    IsFull           TINYINT(1)      NOT NULL DEFAULT 0,
    PricePerLesson   DECIMAL(10,2)   NOT NULL DEFAULT 0,
    PresetGroupId    VARCHAR(20)     NULL, -- shared across every slot from one Setup Class submission (e.g. all occurrences of a weekly recurring class), "PRESET" + zero-padded id of the batch's first slot
    CONSTRAINT FK_TutorTimeSlots_Tutors FOREIGN KEY (TutorId) REFERENCES Tutors(Id) ON DELETE CASCADE
);

-- ============================================================
-- SyllabusTopics — platform-defined topics per country+subject+level.
-- Seeded once by admin. Max 6 topics per subject+level combination.
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
-- PresetGroupSyllabuses — topics a tutor selected for a preset group (max 6).
-- ============================================================
CREATE TABLE IF NOT EXISTS PresetGroupSyllabuses (
    Id              INT          NOT NULL AUTO_INCREMENT,
    PresetGroupId   VARCHAR(20)  NOT NULL DEFAULT '',
    SyllabusTopicId INT          NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE KEY UQ_PresetGroupSyllabus (PresetGroupId, SyllabusTopicId),
    KEY IX_PresetGroupSyllabuses_PresetGroupId (PresetGroupId),
    CONSTRAINT FK_PresetGroupSyllabuses_SyllabusTopics
        FOREIGN KEY (SyllabusTopicId) REFERENCES SyllabusTopics(Id)
        ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ============================================================
-- Students
-- ============================================================
CREATE TABLE IF NOT EXISTS Students (
    Id              INT AUTO_INCREMENT PRIMARY KEY,
    ParentUserId    INT         NOT NULL,
    Name            LONGTEXT    NOT NULL,
    BirthDate       LONGTEXT    NOT NULL,
    School          LONGTEXT    NOT NULL,
    EducationLevel  LONGTEXT    NOT NULL,
    SubjectSelect   LONGTEXT    NOT NULL,
    LearningGoal    LONGTEXT    NULL,
    PhotoUrl        LONGTEXT    NULL,
    IsArchived      TINYINT(1)  NOT NULL DEFAULT 0,   -- archived profiles are hidden from active lists/booking, not deleted
    CONSTRAINT FK_Students_Users FOREIGN KEY (ParentUserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- ============================================================
-- StudentTutorFirstClasses  (tracks the first confirmed lesson between a
-- student and a tutor for a given country+subject+level combination —
-- inserted on booking confirmed (Flow A) and auto-confirmed (Flow B).
-- Fee logic is a TODO, wired in once pricing rules are finalised.)
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
    CONSTRAINT FK_STFC_Tutors
        FOREIGN KEY (TutorId)   REFERENCES Tutors(Id)    ON DELETE CASCADE,
    CONSTRAINT FK_STFC_Students
        FOREIGN KEY (StudentId) REFERENCES Students(Id)  ON DELETE CASCADE,
    CONSTRAINT FK_STFC_Bookings
        FOREIGN KEY (BookingId) REFERENCES Bookings(Id)  ON DELETE SET NULL
);

-- ============================================================
-- StudentPreferredModes  (ranked teaching-mode preference per child)
-- ============================================================
CREATE TABLE IF NOT EXISTS StudentPreferredModes (
    Id         INT AUTO_INCREMENT PRIMARY KEY,
    StudentId  INT          NOT NULL,
    Mode       VARCHAR(50)  NOT NULL, -- Online | Tutor Place | Tuition Center
    Sequence   INT          NOT NULL DEFAULT 0, -- preference order, ascending
    KEY IX_StudentPreferredModes_StudentId (StudentId),
    CONSTRAINT FK_StudentPreferredModes_Students FOREIGN KEY (StudentId) REFERENCES Students(Id) ON DELETE CASCADE
);

-- ============================================================
-- Bookings
-- Added: BookingNumber (was missing from old schema.sql)
-- Schedule now lives in BookingClasses — Date/Time/SlotId are legacy
-- columns left NULLable by the self-migration in Program.cs rather than
-- dropped, so they're kept here to mirror the live database exactly.
-- ============================================================
CREATE TABLE IF NOT EXISTS Bookings (
    Id             INT AUTO_INCREMENT PRIMARY KEY,
    TutorId        INT             NOT NULL,
    StudentId      INT             NOT NULL,
    Subject        LONGTEXT        NOT NULL,
    Mode           LONGTEXT        NOT NULL,
    Date           LONGTEXT        NULL,                     -- legacy; superseded by BookingClasses.Date
    Time           LONGTEXT        NULL,                     -- legacy; superseded by BookingClasses.Time
    DurationHours  DOUBLE          NOT NULL DEFAULT 1, -- widened from INT: 15-min-interval preset classes (e.g. 90 min) aren't whole hours
    Message        LONGTEXT        NULL,
    TotalPrice     DECIMAL(10,2)   NOT NULL,
    Status         LONGTEXT        NOT NULL DEFAULT 'pending', -- pending | countered | confirmed | completed | cancelled
    SlotId         INT             NULL,                     -- legacy; unused
    BookingNumber  LONGTEXT        NOT NULL,
    BookingType    VARCHAR(20)     NOT NULL DEFAULT 'parent-offer', -- parent-offer | tutor-preset
    PresetSlotId   INT             NULL,                     -- FK to TutorTimeSlots.Id when BookingType = 'tutor-preset'
    CONSTRAINT FK_Bookings_Tutors   FOREIGN KEY (TutorId)   REFERENCES Tutors(Id)   ON DELETE RESTRICT,
    CONSTRAINT FK_Bookings_Students FOREIGN KEY (StudentId) REFERENCES Students(Id) ON DELETE RESTRICT,
    CONSTRAINT FK_Bookings_PresetSlot FOREIGN KEY (PresetSlotId) REFERENCES TutorTimeSlots(Id) ON DELETE RESTRICT
);

-- ============================================================
-- BookingClasses  (each row = one session date/time within a booking)
-- ============================================================
CREATE TABLE IF NOT EXISTS BookingClasses (
    Id         INT AUTO_INCREMENT PRIMARY KEY,
    BookingId  INT         NOT NULL,
    Date       LONGTEXT    NOT NULL,
    Time       LONGTEXT    NOT NULL,
    -- Whether this individual session actually happened. A tutor is only paid for a session
    -- that is both Delivered and covered by a paid invoice, so this gates real money leaving
    -- the platform. A booking-level status is too coarse: a month's sessions are paid for
    -- together but delivered one at a time.
    DeliveryStatus VARCHAR(20)  NOT NULL DEFAULT 'Scheduled', -- Scheduled | Delivered | Cancelled
    DeliveredAt    DATETIME(6)  NULL,
    CONSTRAINT FK_BookingClasses_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(Id) ON DELETE CASCADE
);

-- ============================================================
-- BookingPresetSlots  (one row per TutorTimeSlot a preset-class booking
-- covers — lets a single Booking span an entire recurring series, e.g. all
-- 5 occurrences of a weekly class, while still tracking exactly which slots
-- need their seat freed if the booking is cancelled. Bookings.PresetSlotId
-- is kept for backward compatibility with bookings created before this
-- table existed — always just the first slot of the group when there's
-- more than one.)
-- ============================================================
CREATE TABLE IF NOT EXISTS BookingPresetSlots (
    Id               INT AUTO_INCREMENT PRIMARY KEY,
    BookingId        INT         NOT NULL,
    TutorTimeSlotId  INT         NOT NULL,
    KEY IX_BookingPresetSlots_BookingId (BookingId),
    CONSTRAINT FK_BookingPresetSlots_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(Id) ON DELETE CASCADE,
    CONSTRAINT FK_BookingPresetSlots_TutorTimeSlots FOREIGN KEY (TutorTimeSlotId) REFERENCES TutorTimeSlots(Id) ON DELETE CASCADE
);

-- ============================================================
-- CounterProposals  (one-to-many log of every reschedule proposal on a booking)
-- Date/Time moved to per-class CounterProposalClasses — the columns below
-- are legacy, left NULLable rather than dropped, kept here to mirror the
-- live database exactly.
--
-- BookingId is intentionally NOT unique: this table keeps a full history of
-- every proposal made by either party, not just the current one. A new
-- proposal never overwrites an existing row — the previous "pending" row (if
-- any) is marked "superseded" and a new row is inserted. Status distinguishes
-- pending | accepted | superseded | cancelled; only ever at most one row per
-- booking is "pending" at a time.
-- ============================================================
CREATE TABLE IF NOT EXISTS CounterProposals (
    Id          INT AUTO_INCREMENT PRIMARY KEY,
    BookingId   INT          NOT NULL,
    Date        LONGTEXT     NULL,                     -- legacy; superseded by CounterProposalClasses.ProposedDate
    Time        LONGTEXT     NULL,                     -- legacy; superseded by CounterProposalClasses.ProposedTime
    Message     LONGTEXT     NOT NULL,
    ProposedBy  VARCHAR(20)  NOT NULL DEFAULT '',       -- 'parent' or 'tutor' — derived server-side from the JWT, never trusted from the client
    Status      VARCHAR(20)  NOT NULL DEFAULT 'pending', -- pending | accepted | superseded | cancelled
    CreatedAt   DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    KEY IX_CounterProposals_BookingId (BookingId),
    CONSTRAINT FK_CounterProposals_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(Id) ON DELETE CASCADE
);

-- ============================================================
-- CounterProposalClasses  (proposed reschedule rows for a counter-proposal)
-- ============================================================
CREATE TABLE IF NOT EXISTS CounterProposalClasses (
    Id                  INT AUTO_INCREMENT PRIMARY KEY,
    CounterProposalId   INT         NOT NULL,
    OriginalDate        LONGTEXT    NOT NULL,
    OriginalTime        LONGTEXT    NOT NULL,
    ProposedDate        LONGTEXT    NOT NULL,
    ProposedTime        LONGTEXT    NOT NULL,
    CONSTRAINT FK_CounterProposalClasses_CounterProposals
        FOREIGN KEY (CounterProposalId) REFERENCES CounterProposals(Id) ON DELETE CASCADE
);

-- ============================================================
-- LessonReports — one report per student per session date.
-- For group classes each student gets their own personalised report.
-- Reports cannot be edited after submission.
-- ============================================================
CREATE TABLE IF NOT EXISTS LessonReports (
    Id                  INT          NOT NULL AUTO_INCREMENT,
    BookingId           INT          NOT NULL,
    StudentId           INT          NOT NULL,
    SessionDate         VARCHAR(20)  NOT NULL DEFAULT '',
    Attendance          VARCHAR(20)  NOT NULL DEFAULT '',
    Engagement          INT          NULL,
    Understanding       VARCHAR(30)  NULL,
    HomeworkCompletion  VARCHAR(30)  NULL,
    Remarks             LONGTEXT     NULL,
    SubmittedAt         DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (Id),
    UNIQUE KEY UQ_LessonReport_BookingStudentDate
        (BookingId, StudentId, SessionDate),
    KEY IX_LessonReports_BookingId (BookingId),
    KEY IX_LessonReports_StudentId (StudentId),
    CONSTRAINT FK_LessonReports_Bookings
        FOREIGN KEY (BookingId)  REFERENCES Bookings(Id) ON DELETE CASCADE,
    CONSTRAINT FK_LessonReports_Students
        FOREIGN KEY (StudentId) REFERENCES Students(Id) ON DELETE RESTRICT
);
-- LessonReportEdits removed — reports are immutable after submission

-- ============================================================
-- IssueReports  (1-to-1 with Booking)
-- ============================================================
CREATE TABLE IF NOT EXISTS IssueReports (
    Id          INT AUTO_INCREMENT PRIMARY KEY,
    BookingId   INT         NOT NULL UNIQUE,
    IssueType   LONGTEXT    NOT NULL,
    Details     LONGTEXT    NOT NULL,
    Timestamp   LONGTEXT    NOT NULL,        -- display-only, time-of-day (no date) — see CreatedAt
    CreatedAt   DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6), -- real date/time, used by the AI Speed Match "Tutor Dispute (Refresh Monthly)" scoring criterion
    CONSTRAINT FK_IssueReports_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(Id) ON DELETE CASCADE
);

-- ============================================================
-- Invoices  (1-to-1 with Booking)
-- Added: InvoiceNumber (was missing from old schema.sql)
-- ============================================================
CREATE TABLE IF NOT EXISTS Invoices (
    Id              INT AUTO_INCREMENT PRIMARY KEY,
    BookingId       INT             NOT NULL UNIQUE,
    Date            LONGTEXT        NOT NULL,
    Amount          DECIMAL(10,2)   NOT NULL,
    Status          LONGTEXT        NOT NULL DEFAULT 'Unpaid', -- Paid | Unpaid | Refunded | Cancelled
    Subject         LONGTEXT        NULL,
    InvoiceNumber   LONGTEXT        NOT NULL,
    -- Fee breakdown. Amount is what the parent is billed = BaseAmount + MarkupAmount.
    -- BaseAmount is the tutor's price, and the only figure a tutor is ever paid from.
    -- MarkupPercent is stored per invoice so a later rate change never restates an old bill.
    BaseAmount          DECIMAL(10,2) NOT NULL DEFAULT 0,
    MarkupAmount        DECIMAL(10,2) NOT NULL DEFAULT 0,
    MarkupPercent       DECIMAL(5,2)  NOT NULL DEFAULT 0,
    -- True when this invoice covers a match's FIRST tuition period. Decided once, at
    -- creation, and never recomputed.
    IsFirstMatch        TINYINT(1)    NOT NULL DEFAULT 0,
    -- How much of Amount was settled from the parent's wallet rather than in cash.
    WalletCreditApplied DECIMAL(10,2) NOT NULL DEFAULT 0,
    CONSTRAINT FK_Invoices_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(Id) ON DELETE CASCADE
);

-- ============================================================
-- Payouts
-- ============================================================
CREATE TABLE IF NOT EXISTS Payouts (
    Id        INT AUTO_INCREMENT PRIMARY KEY,
    TutorId   INT             NOT NULL,
    Date      LONGTEXT        NOT NULL,
    Amount    DECIMAL(10,2)   NOT NULL,
    Status    LONGTEXT        NOT NULL DEFAULT 'Processing', -- Processing | Completed
    CONSTRAINT FK_Payouts_Tutors FOREIGN KEY (TutorId) REFERENCES Tutors(Id) ON DELETE CASCADE
);

-- ============================================================
-- PresetCancellationDecisions  (per-booking outcome when a tutor cancels a
-- published preset-class slot — see TutorsController.DeleteSlot,
-- PresetCancellationsController, AdminController). One row per affected
-- BOOKING, not per cancellation event — a group class can have several
-- independent families on the same slot, each deciding separately.
-- Original*/PricePerLesson are snapshotted from the TutorTimeSlot at cancel
-- time since that row is deleted immediately after.
-- ============================================================
CREATE TABLE IF NOT EXISTS PresetCancellationDecisions (
    Id               INT AUTO_INCREMENT PRIMARY KEY,
    BookingId        INT             NOT NULL,
    OriginalDate     LONGTEXT        NOT NULL,
    OriginalTime     LONGTEXT        NOT NULL,
    OriginalEndTime  LONGTEXT        NOT NULL,
    PricePerLesson   DECIMAL(10,2)   NOT NULL DEFAULT 0,
    ProposedDate     LONGTEXT        NULL, -- NULL = straight cancel, no reschedule offered
    ProposedTime     LONGTEXT        NULL,
    ProposedEndTime  LONGTEXT        NULL,
    Status           VARCHAR(20)     NOT NULL DEFAULT 'pending', -- pending | accepted | auto-accepted | pending-admin | resolved
    AcknowledgedAt   DATETIME(6)     NULL, -- straight-cancel popup dismissal only; Path A decisions don't use this
    CreatedAt        DATETIME(6)     NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    DecidedAt        DATETIME(6)     NULL, -- when the parent (or the auto-accept sweep) made the call
    ResolvedAt       DATETIME(6)     NULL, -- when the refund/penalty/dispute-score side effects actually ran
    AdminNote        LONGTEXT        NULL,
    KEY IX_PresetCancellationDecisions_BookingId (BookingId),
    KEY IX_PresetCancellationDecisions_Status (Status),
    CONSTRAINT FK_PresetCancellationDecisions_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(Id) ON DELETE CASCADE
);

-- ============================================================
-- TutorPenalties  (deduction ledger against a tutor's future payout — e.g.
-- the 20% penalty charged when a preset-class cancellation resolves toward a
-- parent credit. Kept append-only rather than editing Payouts rows directly;
-- PayoutsController's available-balance calc subtracts SUM(Amount) here.
-- BookingId is a soft reference, not a hard FK — a penalty is a permanent
-- ledger entry that should outlive the booking record it originated from.)
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
-- FavoriteTutors  (parent's liked/bookmarked tutors)
-- ============================================================
CREATE TABLE IF NOT EXISTS FavoriteTutors (
    Id            INT AUTO_INCREMENT PRIMARY KEY,
    ParentUserId  INT          NOT NULL,
    TutorId       INT          NOT NULL,
    CreatedAt     DATETIME(6)  NOT NULL,
    UNIQUE KEY UQ_FavoriteTutors_Parent_Tutor (ParentUserId, TutorId),
    CONSTRAINT FK_FavoriteTutors_Users  FOREIGN KEY (ParentUserId) REFERENCES Users(Id)  ON DELETE CASCADE,
    CONSTRAINT FK_FavoriteTutors_Tutors FOREIGN KEY (TutorId)      REFERENCES Tutors(Id) ON DELETE CASCADE
);

-- ============================================================
-- ChatMessages
-- Added: ParentUserId — threads were keyed by TutorId alone, mixing every
-- parent who messaged a given tutor into one conversation. The key is now
-- (TutorId, ParentUserId).
-- Added: IsRead — a thread is strictly 1:1, so one flag per message is enough
-- to answer "has the recipient seen this" (no per-participant read table).
-- Marked true as a side effect of the recipient calling GET /api/chat/{tutorId}/{parentUserId}.
-- ============================================================
CREATE TABLE IF NOT EXISTS ChatMessages (
    Id            INT AUTO_INCREMENT PRIMARY KEY,
    TutorId       INT         NOT NULL,
    ParentUserId  INT         NOT NULL DEFAULT 0,
    Sender        LONGTEXT    NOT NULL, -- parent | tutor | system
    Text          LONGTEXT    NOT NULL,
    Timestamp     LONGTEXT    NOT NULL,
    IsRead        TINYINT(1)  NOT NULL DEFAULT 0
);

-- ============================================================
-- Notifications
-- ============================================================
CREATE TABLE IF NOT EXISTS Notifications (
    Id         INT AUTO_INCREMENT PRIMARY KEY,
    UserId     INT         NOT NULL,
    Title      LONGTEXT    NOT NULL,
    Message    LONGTEXT    NOT NULL,
    Timestamp  LONGTEXT    NOT NULL,
    Type       LONGTEXT    NOT NULL DEFAULT 'system', -- booking | message | payment | system
    IsRead     TINYINT(1)  NOT NULL DEFAULT 0,
    CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- ============================================================
-- Institutions
-- ============================================================
CREATE TABLE IF NOT EXISTS Institutions (
    Id       INT AUTO_INCREMENT PRIMARY KEY,
    Name     LONGTEXT    NOT NULL,
    Country  LONGTEXT    NOT NULL, -- Singapore | Malaysia
    Type     LONGTEXT    NOT NULL  -- Primary | Secondary | Junior College | Polytechnic/Vocational | University/Tertiary
);

-- ============================================================
-- ScoringWeightages  (AI Speed Match scoring config, admin Scoring Config
-- page — Key is the stable identifier the match-score calculator switches
-- on; Label is display text only. Seeded once with 6 fixed rows
-- (rating/activeness/disputes/experience + 2 reserved "na" slots); only
-- Percent is admin-editable thereafter.)
-- ============================================================
CREATE TABLE IF NOT EXISTS ScoringWeightages (
    Id         INT AUTO_INCREMENT PRIMARY KEY,
    `Key`      VARCHAR(20)   NOT NULL, -- rating | activeness | disputes | experience | na1 | na2
    Label      VARCHAR(100)  NOT NULL,
    Percent    INT           NOT NULL DEFAULT 0,
    SortOrder  INT           NOT NULL DEFAULT 0,
    UNIQUE KEY UQ_ScoringWeightages_Key (`Key`)
);

-- ============================================================
-- PaymentGatewaySettings  (singleton row, Id = 1 — HitPay credentials managed
-- from Admin -> Payment Gateway. Held in the database rather than appsettings
-- so a key can be rotated without a redeploy; ApiKey/Salt are never returned
-- to a client, only a masked hint.)
-- ============================================================
CREATE TABLE IF NOT EXISTS PaymentGatewaySettings (
    Id           INT AUTO_INCREMENT PRIMARY KEY,
    Provider     VARCHAR(40)   NOT NULL DEFAULT 'hitpay',
    IsEnabled    TINYINT(1)    NOT NULL DEFAULT 0,
    Mode         VARCHAR(20)   NOT NULL DEFAULT 'sandbox', -- sandbox | live
    ApiKey       VARCHAR(500)  NOT NULL DEFAULT '',
    Salt         VARCHAR(500)  NOT NULL DEFAULT '',
    Currency     VARCHAR(10)   NOT NULL DEFAULT 'SGD',
    ReturnUrl    VARCHAR(500)  NOT NULL DEFAULT '',
    ApiBaseUrl   VARCHAR(500)  NULL,
    ReturnOrigin VARCHAR(500)  NULL,
    UpdatedAt    DATETIME(6)   NULL
);

-- ============================================================
-- PaymentTransactions  (one row per checkout attempt against an invoice)
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
-- CommissionSettings  (singleton row, Id = 1 — every platform fee rate,
-- managed from Admin -> Platform Fees. Three rates that do NOT work alike:
--   MarkupPercent               added ON TOP of the base price; the PARENT pays it
--   FirstMatchCommissionPercent taken OUT of the base price once, on a match's first
--                               tuition period; the TUTOR pays it, and promotional
--                               credit exists to offset exactly this charge
--   RatePercent                 recurring per-invoice cut of everything else; 0 in the
--                               launch model, where markup is the recurring revenue
-- The two EffectiveFrom columns are what stop a rate change billing history retroactively.)
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
-- TutorLedgerEntries  (append-only money ledger for a tutor — nothing here is
-- ever updated or deleted, and a reversal is a NEW opposing entry. Two funds
-- that must never be summed together for the purpose of paying someone:
-- 'withdrawable' is real money, 'credit' is promotional credit that offsets
-- first-match commission but can never be cashed out. SourceEntryId links a
-- consumption or expiry back to the grant it draws down, which is what makes
-- oldest-first spending and per-grant expiry work while staying append-only.)
-- ============================================================
CREATE TABLE IF NOT EXISTS TutorLedgerEntries (
    Id              INT AUTO_INCREMENT PRIMARY KEY,
    TutorId         INT           NOT NULL,
    Fund            VARCHAR(20)   NOT NULL DEFAULT 'withdrawable', -- withdrawable | credit
    Type            VARCHAR(40)   NOT NULL DEFAULT '',
    Amount          DECIMAL(10,2) NOT NULL DEFAULT 0,  -- signed: + credits the tutor
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
-- ParentWalletEntries  (append-only wallet ledger for a parent, on the same
-- rules as TutorLedgerEntries. Wallet credit can pay any LearnSphere invoice
-- but can never be withdrawn to a bank; a refund the parent wants in cash is a
-- Direct Bank Refund and never touches this table. Credit expires 6 months
-- from the date it was granted.)
-- ============================================================
CREATE TABLE IF NOT EXISTS ParentWalletEntries (
    Id              INT AUTO_INCREMENT PRIMARY KEY,
    ParentUserId    INT           NOT NULL,
    Type            VARCHAR(40)   NOT NULL DEFAULT '', -- refund_credit | adjustment | payment_usage | expiry
    Amount          DECIMAL(10,2) NOT NULL DEFAULT 0,  -- signed: + adds credit
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
-- PayoutBatches  (every tutor's payable for one period, grouped into a single
-- unit an admin approves once and finance transfers once, so "has September
-- been paid?" has exactly one answer. One batch per period.)
-- ============================================================
CREATE TABLE IF NOT EXISTS PayoutBatches (
    Id                  INT AUTO_INCREMENT PRIMARY KEY,
    BatchNumber         VARCHAR(40)   NOT NULL DEFAULT '',
    Period              VARCHAR(7)    NOT NULL DEFAULT '',   -- yyyy-MM
    TotalAmount         DECIMAL(12,2) NOT NULL DEFAULT 0,
    TutorCount          INT           NOT NULL DEFAULT 0,
    Status              VARCHAR(20)   NOT NULL DEFAULT 'Pending', -- Pending | Approved | Paid | Cancelled
    CreatedAt           DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    ApprovedAt          DATETIME(6)   NULL,
    ApprovedByUserId    INT           NULL,
    TransferredAt       DATETIME(6)   NULL,
    TransferredByUserId INT           NULL,
    Notes               VARCHAR(1000) NULL,
    UNIQUE KEY UQ_PayoutBatch_Period (Period)
);

-- ============================================================
-- TutorPayables  (what one tutor is owed for one billing period, worked out at
-- the month-end cutoff. Derived, never hand-entered: it counts only sessions
-- that were BOTH delivered and paid for, and never exceeds the tutor's ledger
-- balance — so a first tuition period pays nothing unless promotional credit
-- offset its commission. One row per tutor per period.)
-- ============================================================
CREATE TABLE IF NOT EXISTS TutorPayables (
    Id            INT AUTO_INCREMENT PRIMARY KEY,
    TutorId       INT           NOT NULL,
    Period        VARCHAR(7)    NOT NULL DEFAULT '',  -- yyyy-MM
    PeriodStart   VARCHAR(10)   NOT NULL DEFAULT '',
    PeriodEnd     VARCHAR(10)   NOT NULL DEFAULT '',
    Amount        DECIMAL(10,2) NOT NULL DEFAULT 0,
    SessionCount  INT           NOT NULL DEFAULT 0,
    Status        VARCHAR(20)   NOT NULL DEFAULT 'Pending', -- Pending | Batched | Paid | Cancelled
    PayoutBatchId INT           NULL,
    PayoutId      INT           NULL,
    CreatedAt     DATETIME(6)   NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    UNIQUE KEY UQ_TutorPayable_Tutor_Period (TutorId, Period),
    KEY IX_TutorPayables_PayoutBatchId (PayoutBatchId),
    CONSTRAINT FK_TutorPayables_Tutors        FOREIGN KEY (TutorId)       REFERENCES Tutors(Id)        ON DELETE CASCADE,
    CONSTRAINT FK_TutorPayables_PayoutBatches FOREIGN KEY (PayoutBatchId) REFERENCES PayoutBatches(Id) ON DELETE SET NULL
);
