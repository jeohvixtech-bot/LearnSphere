-- LearnSphere MySQL Schema
-- Generated from EF Core models (AppDbContext + AppDbContextModelSnapshot)
-- Last updated: reflects all migrations including AddReviewBookingId
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
    CreatedAt    DATETIME(6)     NOT NULL
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
CREATE TABLE IF NOT EXISTS TutorTimeSlots (
    Id         INT AUTO_INCREMENT PRIMARY KEY,
    TutorId    INT         NOT NULL,
    Day        LONGTEXT    NOT NULL,
    Time       LONGTEXT    NOT NULL,
    Status     LONGTEXT    NOT NULL DEFAULT 'Available', -- Available | Booked
    BookingId  INT         NULL,
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
    CONSTRAINT FK_Students_Users FOREIGN KEY (ParentUserId) REFERENCES Users(Id) ON DELETE CASCADE
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
    DurationHours  INT             NOT NULL DEFAULT 1,
    Message        LONGTEXT        NULL,
    TotalPrice     DECIMAL(10,2)   NOT NULL,
    Status         LONGTEXT        NOT NULL DEFAULT 'pending', -- pending | countered | confirmed | completed | cancelled
    SlotId         INT             NULL,                     -- legacy; unused
    BookingNumber  LONGTEXT        NOT NULL,
    CONSTRAINT FK_Bookings_Tutors   FOREIGN KEY (TutorId)   REFERENCES Tutors(Id)   ON DELETE RESTRICT,
    CONSTRAINT FK_Bookings_Students FOREIGN KEY (StudentId) REFERENCES Students(Id) ON DELETE RESTRICT
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
-- CounterProposals  (1-to-1 with Booking)
-- Date/Time moved to per-class CounterProposalClasses — the columns below
-- are legacy, left NULLable rather than dropped, kept here to mirror the
-- live database exactly.
-- ============================================================
CREATE TABLE IF NOT EXISTS CounterProposals (
    Id         INT AUTO_INCREMENT PRIMARY KEY,
    BookingId  INT         NOT NULL UNIQUE,
    Date       LONGTEXT    NULL,                     -- legacy; superseded by CounterProposalClasses.ProposedDate
    Time       LONGTEXT    NULL,                     -- legacy; superseded by CounterProposalClasses.ProposedTime
    Message    LONGTEXT    NOT NULL,
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
    Timestamp   LONGTEXT    NOT NULL,
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
    Status          LONGTEXT        NOT NULL DEFAULT 'Unpaid', -- Paid | Unpaid
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
-- ChatMessages
-- ============================================================
CREATE TABLE IF NOT EXISTS ChatMessages (
    Id         INT AUTO_INCREMENT PRIMARY KEY,
    TutorId    INT         NOT NULL,
    Sender     LONGTEXT    NOT NULL, -- parent | tutor | system
    Text       LONGTEXT    NOT NULL,
    Timestamp  LONGTEXT    NOT NULL
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
