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
    VerificationStatus VARCHAR(20)   NOT NULL DEFAULT 'not_submitted', -- not_submitted | pending | approved | rejected
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
-- credentials, intro video, specialist certs. Every DocumentType upserts a single
-- row except specialist_cert, which allows multiple, ordered by SortOrder.
-- See TutorsController.SaveDocument/ReviewDocument.)
-- ============================================================
CREATE TABLE IF NOT EXISTS TutorDocuments (
    Id             INT AUTO_INCREMENT PRIMARY KEY,
    TutorId        INT             NOT NULL,
    DocumentType   VARCHAR(30)     NOT NULL DEFAULT '', -- identity_photo | o_level | a_level | degree | postgrad | nie_cert | intro_video | specialist_cert
    FileUrl        LONGTEXT        NULL,
    ExternalUrl    LONGTEXT        NULL,                -- intro_video may be a pasted link instead of an upload
    FileName       LONGTEXT        NULL,
    FileSizeBytes  BIGINT          NULL,
    IdType         VARCHAR(20)     NULL,                -- identity_photo only: NRIC | MyKad | Passport
    IdNumber       LONGTEXT        NULL,                -- identity_photo only
    SortOrder      INT             NOT NULL DEFAULT 0,
    Status         VARCHAR(20)     NOT NULL DEFAULT 'pending', -- pending | approved | rejected
    AdminNote      LONGTEXT        NULL,
    UploadedAt     DATETIME(6)     NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    ReviewedAt     DATETIME(6)     NULL,
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
-- StudentPreferredModes  (ranked teaching-mode preference per child)
-- ============================================================
CREATE TABLE IF NOT EXISTS StudentPreferredModes (
    Id         INT AUTO_INCREMENT PRIMARY KEY,
    StudentId  INT          NOT NULL,
    Mode       VARCHAR(50)  NOT NULL, -- Online | Home Visit | Tutor Place | Tuition Center
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
-- LessonReports  (1-to-1 with Booking)
-- ============================================================
CREATE TABLE IF NOT EXISTS LessonReports (
    Id          INT AUTO_INCREMENT PRIMARY KEY,
    BookingId   INT         NOT NULL UNIQUE,
    Covered     LONGTEXT    NOT NULL,
    Performance LONGTEXT    NOT NULL,
    Homework    LONGTEXT    NOT NULL,
    SubmitDate  LONGTEXT    NOT NULL,
    CONSTRAINT FK_LessonReports_Bookings FOREIGN KEY (BookingId) REFERENCES Bookings(Id) ON DELETE CASCADE
);

-- ============================================================
-- LessonReportEdits  (edit history for a lesson report)
-- ============================================================
CREATE TABLE IF NOT EXISTS LessonReportEdits (
    Id               INT AUTO_INCREMENT PRIMARY KEY,
    LessonReportId   INT         NOT NULL,
    Date             LONGTEXT    NOT NULL,
    Changes          LONGTEXT    NOT NULL,
    CONSTRAINT FK_LessonReportEdits_LessonReports
        FOREIGN KEY (LessonReportId) REFERENCES LessonReports(Id) ON DELETE CASCADE
);

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
