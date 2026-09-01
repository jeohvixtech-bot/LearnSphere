using System.Text;
using LearnSphere.API.Data;
using LearnSphere.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPresetCancellationService, PresetCancellationService>();
// Real SMTP delivery once BOTH "Smtp:Host" and "Smtp:Password" are set (Password
// is meant to come from user-secrets/environment, never committed to
// appsettings.json) — falls back to logging emails to the console otherwise, so
// a half-configured Smtp section (e.g. Host set while still generating the app
// password) degrades to the safe no-op instead of every email-sending endpoint
// throwing.
if (!string.IsNullOrWhiteSpace(builder.Configuration["Smtp:Host"]) &&
    !string.IsNullOrWhiteSpace(builder.Configuration["Smtp:Password"]))
    builder.Services.AddScoped<IEmailService, SmtpEmailService>();
else
    builder.Services.AddScoped<IEmailService, ConsoleEmailService>();

// CORS — origins configurable via appsettings.json "AllowedOrigins"
// Set to ["*"] to allow all, or list specific origins e.g. ["http://localhost:4200","http://myserver:1002"]
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (allowedOrigins == null || allowedOrigins.Length == 0 || allowedOrigins.Contains("*"))
            policy.SetIsOriginAllowed(_ => true);
        else
            policy.WithOrigins(allowedOrigins);

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "LearnSphere API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try {
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE `TutorSubjects` ADD COLUMN `Price` DECIMAL(10,2) NULL;");
    } catch { }
    try {
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS `TutorOfferings` (
                `Id` INT NOT NULL AUTO_INCREMENT,
                `TutorId` INT NOT NULL,
                `Subject` VARCHAR(200) NOT NULL DEFAULT '',
                `Level` VARCHAR(200) NOT NULL DEFAULT '',
                `Mode` VARCHAR(200) NOT NULL DEFAULT '',
                `Qualification` VARCHAR(200) NOT NULL DEFAULT '',
                `Price` DECIMAL(10,2) NOT NULL DEFAULT 0,
                PRIMARY KEY (`Id`),
                CONSTRAINT `FK_TutorOfferings_Tutors` FOREIGN KEY (`TutorId`) REFERENCES `Tutors` (`Id`) ON DELETE CASCADE
            );");
    } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `TutorOfferings` ADD COLUMN `Country` VARCHAR(50) NOT NULL DEFAULT 'Singapore'"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Bookings` ADD COLUMN `BookingNumber` VARCHAR(20) NOT NULL DEFAULT ''"); } catch { }
    // Date/Time moved to BookingClasses — make legacy columns nullable so existing schema doesn't break INSERTs
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Bookings` MODIFY COLUMN `Date` longtext NULL"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Bookings` MODIFY COLUMN `Time` longtext NULL"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Bookings` MODIFY COLUMN `SlotId` int NULL"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Invoices` ADD COLUMN `InvoiceNumber` VARCHAR(20) NOT NULL DEFAULT ''"); } catch { }
    // TutorReview.BookingId links a review to a specific completed booking
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `TutorReviews` ADD COLUMN `BookingId` INT NULL"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "CREATE UNIQUE INDEX `UQ_TutorReviews_TutorBooking` ON `TutorReviews` (`TutorId`, `BookingId`)"); } catch { }
    // CounterProposal.Date/Time moved to per-class CounterProposalClasses
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `CounterProposals` MODIFY COLUMN `Date` longtext NULL"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `CounterProposals` MODIFY COLUMN `Time` longtext NULL"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS `CounterProposalClasses` (
            `Id` INT NOT NULL AUTO_INCREMENT,
            `CounterProposalId` INT NOT NULL,
            `OriginalDate` VARCHAR(50) NOT NULL DEFAULT '',
            `OriginalTime` VARCHAR(50) NOT NULL DEFAULT '',
            `ProposedDate` VARCHAR(50) NOT NULL DEFAULT '',
            `ProposedTime` VARCHAR(50) NOT NULL DEFAULT '',
            PRIMARY KEY (`Id`),
            KEY `IX_CounterProposalClasses_CounterProposalId` (`CounterProposalId`),
            CONSTRAINT `FK_CounterProposalClasses_CounterProposals_CounterProposalId`
                FOREIGN KEY (`CounterProposalId`) REFERENCES `CounterProposals` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    "); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS `BookingClasses` (
            `Id` INT NOT NULL AUTO_INCREMENT,
            `BookingId` INT NOT NULL,
            `Date` VARCHAR(50) NOT NULL DEFAULT '',
            `Time` VARCHAR(50) NOT NULL DEFAULT '',
            PRIMARY KEY (`Id`),
            KEY `IX_BookingClasses_BookingId` (`BookingId`),
            CONSTRAINT `FK_BookingClasses_Bookings_BookingId`
                FOREIGN KEY (`BookingId`) REFERENCES `Bookings` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    "); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Users` ADD COLUMN `MustChangePassword` TINYINT(1) NOT NULL DEFAULT 0"); } catch { }
    // Archiving a student profile is the alternative to deleting one that still has booking history
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Students` ADD COLUMN `IsArchived` TINYINT(1) NOT NULL DEFAULT 0"); } catch { }
    // Tutor-controlled online/offline switch — offline hides the profile from parent search/booking entirely
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Tutors` ADD COLUMN `IsOnline` TINYINT(1) NOT NULL DEFAULT 1"); } catch { }
    // CounterProposals becomes a per-booking log (was 1-to-1) so every reschedule proposal —
    // by either party — is kept instead of being overwritten by the next one.
    // The old unique index backs a FK, so a replacement non-unique index must exist
    // before it can be dropped, or MySQL refuses ("needed in a foreign key constraint").
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `CounterProposals` ADD INDEX `IX_CounterProposals_BookingId_nonunique` (`BookingId`)"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `CounterProposals` DROP INDEX `IX_CounterProposals_BookingId`"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `CounterProposals` RENAME INDEX `IX_CounterProposals_BookingId_nonunique` TO `IX_CounterProposals_BookingId`"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `CounterProposals` ADD COLUMN `ProposedBy` VARCHAR(20) NOT NULL DEFAULT ''"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `CounterProposals` ADD COLUMN `Status` VARCHAR(20) NOT NULL DEFAULT 'pending'"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `CounterProposals` ADD COLUMN `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)"); } catch { }
    // Pre-existing rows all default-backfilled to 'pending' above — but a row whose booking has
    // already moved on (confirmed/completed/cancelled) was actually resolved, not left hanging.
    try { await context.Database.ExecuteSqlRawAsync(@"
        UPDATE `CounterProposals` cp
        JOIN `Bookings` b ON b.`Id` = cp.`BookingId`
        SET cp.`Status` = 'accepted'
        WHERE cp.`Status` = 'pending' AND b.`Status` IN ('confirmed', 'completed')"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(@"
        UPDATE `CounterProposals` cp
        JOIN `Bookings` b ON b.`Id` = cp.`BookingId`
        SET cp.`Status` = 'cancelled'
        WHERE cp.`Status` = 'pending' AND b.`Status` = 'cancelled'"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS `FavoriteTutors` (
            `Id` INT NOT NULL AUTO_INCREMENT,
            `ParentUserId` INT NOT NULL,
            `TutorId` INT NOT NULL,
            `CreatedAt` DATETIME(6) NOT NULL,
            PRIMARY KEY (`Id`),
            UNIQUE KEY `UQ_FavoriteTutors_Parent_Tutor` (`ParentUserId`, `TutorId`),
            CONSTRAINT `FK_FavoriteTutors_Users` FOREIGN KEY (`ParentUserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE,
            CONSTRAINT `FK_FavoriteTutors_Tutors` FOREIGN KEY (`TutorId`) REFERENCES `Tutors` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    "); } catch { }
    // Chat conversations were keyed by TutorId alone, mixing every parent who messaged a
    // given tutor into one thread. ParentUserId makes the key (TutorId, ParentUserId).
    // Existing messages predate this and have no reliable parent attribution, so — this
    // only runs the first time (the ALTER only succeeds once; afterwards it throws and
    // the wipe below is skipped along with it) — they're cleared rather than guessed at.
    try {
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE `ChatMessages` ADD COLUMN `ParentUserId` INT NOT NULL DEFAULT 0");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM `ChatMessages`");
    } catch { }
    try { await context.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS `StudentPreferredModes` (
            `Id` INT NOT NULL AUTO_INCREMENT,
            `StudentId` INT NOT NULL,
            `Mode` VARCHAR(50) NOT NULL,
            `Sequence` INT NOT NULL DEFAULT 0,
            PRIMARY KEY (`Id`),
            KEY `IX_StudentPreferredModes_StudentId` (`StudentId`),
            CONSTRAINT `FK_StudentPreferredModes_Students` FOREIGN KEY (`StudentId`) REFERENCES `Students` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    "); } catch { }
    // Tutor-preset class slots (Flow B) — a slot the tutor publishes ahead of time
    // that a parent can book directly without a per-request confirmation step.
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `TutorTimeSlots` ADD COLUMN `EndTime` LONGTEXT NULL"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `TutorTimeSlots` ADD COLUMN `DurationMinutes` INT NOT NULL DEFAULT 60"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `TutorTimeSlots` ADD COLUMN `Mode` LONGTEXT NULL"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `TutorTimeSlots` ADD COLUMN `Subject` LONGTEXT NULL"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `TutorTimeSlots` ADD COLUMN `Level` LONGTEXT NULL"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `TutorTimeSlots` ADD COLUMN `Country` LONGTEXT NULL"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `TutorTimeSlots` ADD COLUMN `ClassSize` VARCHAR(20) NOT NULL DEFAULT 'one-to-one'"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `TutorTimeSlots` ADD COLUMN `MaxStudents` INT NOT NULL DEFAULT 1"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `TutorTimeSlots` ADD COLUMN `ConfirmedCount` INT NOT NULL DEFAULT 0"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `TutorTimeSlots` ADD COLUMN `IsFull` TINYINT(1) NOT NULL DEFAULT 0"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `TutorTimeSlots` ADD COLUMN `PricePerLesson` DECIMAL(10,2) NOT NULL DEFAULT 0"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `TutorTimeSlots` ADD COLUMN `PresetGroupId` VARCHAR(20) NULL"); } catch { }
    // Backfill preset slots published before PresetGroupId existed. We can't recover
    // which original Setup Class submission each row came from, so approximate the
    // catalog's old merge-by-subject behavior — group legacy rows sharing the same
    // tutor/subject/level/mode/country under one id — rather than fragmenting each
    // into its own singleton chip. Only ever-so-slightly-approximate: brand new
    // slots (created after this migration) get real per-submission grouping instead.
    await context.Database.ExecuteSqlRawAsync(@"
        UPDATE `TutorTimeSlots` t
        JOIN (
            SELECT `TutorId`, `Subject`, `Level`, `Mode`, `Country`, MIN(`Id`) AS MinId
            FROM `TutorTimeSlots`
            WHERE `Mode` IS NOT NULL AND (`PresetGroupId` IS NULL OR `PresetGroupId` = '')
            GROUP BY `TutorId`, `Subject`, `Level`, `Mode`, `Country`
        ) g ON t.`TutorId` = g.`TutorId` AND t.`Subject` <=> g.`Subject` AND t.`Level` <=> g.`Level`
            AND t.`Mode` <=> g.`Mode` AND t.`Country` <=> g.`Country`
        SET t.`PresetGroupId` = CONCAT('PRESET', LPAD(g.MinId, 6, '0'))
        WHERE t.`Mode` IS NOT NULL AND (t.`PresetGroupId` IS NULL OR t.`PresetGroupId` = '')
    ");
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Bookings` ADD COLUMN `BookingType` VARCHAR(20) NOT NULL DEFAULT 'parent-offer'"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Bookings` ADD COLUMN `PresetSlotId` INT NULL"); } catch { }
    // Widened from INT — 15-min-interval preset classes (e.g. 90 min) aren't whole hours.
    // Safe/lossless widen; existing whole-hour values are unaffected.
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Bookings` MODIFY COLUMN `DurationHours` DOUBLE NOT NULL DEFAULT 1"); } catch { }
    await context.Database.ExecuteSqlRawAsync(
        "UPDATE `Bookings` SET `BookingNumber` = CONCAT('BOK', LPAD(`Id`, 5, '0')) WHERE `BookingNumber` = ''");
    await context.Database.ExecuteSqlRawAsync(
        "UPDATE `Invoices` SET `InvoiceNumber` = CONCAT('INV', LPAD(`Id`, 5, '0')) WHERE `InvoiceNumber` = ''");
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `IssueReports` ADD COLUMN `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)"); } catch { }
    // One booking can now cover an entire recurring preset-class series (multiple
    // TutorTimeSlot occurrences) instead of one booking per occurrence — see
    // BookingsController.BookPreset. Existing single-slot preset bookings keep
    // working via the legacy Bookings.PresetSlotId column; this table is only
    // populated going forward.
    try { await context.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS `BookingPresetSlots` (
            `Id` INT NOT NULL AUTO_INCREMENT,
            `BookingId` INT NOT NULL,
            `TutorTimeSlotId` INT NOT NULL,
            PRIMARY KEY (`Id`),
            KEY `IX_BookingPresetSlots_BookingId` (`BookingId`),
            CONSTRAINT `FK_BookingPresetSlots_Bookings` FOREIGN KEY (`BookingId`) REFERENCES `Bookings` (`Id`) ON DELETE CASCADE,
            CONSTRAINT `FK_BookingPresetSlots_TutorTimeSlots` FOREIGN KEY (`TutorTimeSlotId`) REFERENCES `TutorTimeSlots` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    "); } catch { }
    // AI Speed Match scoring config (admin Scoring Config page) — Key is the stable
    // identifier the match-score calculator switches on; INSERT IGNORE seeds the six
    // rows once and never overwrites percentages an admin has already saved.
    try { await context.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS `ScoringWeightages` (
            `Id` INT NOT NULL AUTO_INCREMENT,
            `Key` VARCHAR(20) NOT NULL,
            `Label` VARCHAR(100) NOT NULL,
            `Percent` INT NOT NULL DEFAULT 0,
            `SortOrder` INT NOT NULL DEFAULT 0,
            PRIMARY KEY (`Id`),
            UNIQUE KEY `UQ_ScoringWeightages_Key` (`Key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    "); } catch { }
    await context.Database.ExecuteSqlRawAsync(@"
        INSERT IGNORE INTO `ScoringWeightages` (`Key`, `Label`, `Percent`, `SortOrder`) VALUES
            ('rating', 'Tutor Rating', 0, 0),
            ('activeness', 'Tutor Activeness (Refresh Monthly)', 0, 1),
            ('disputes', 'Tutor Dispute (Refresh Monthly)', 0, 2),
            ('experience', 'Tutor Experience', 0, 3),
            ('na1', 'NA', 0, 4),
            ('na2', 'NA', 0, 5)
    ");
    // Per-booking outcome of a tutor cancelling a published preset-class slot —
    // see TutorsController.DeleteSlot and PresetCancellationsController.
    try { await context.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS `PresetCancellationDecisions` (
            `Id` INT NOT NULL AUTO_INCREMENT,
            `BookingId` INT NOT NULL,
            `OriginalDate` LONGTEXT NOT NULL,
            `OriginalTime` LONGTEXT NOT NULL,
            `OriginalEndTime` LONGTEXT NOT NULL,
            `PricePerLesson` DECIMAL(10,2) NOT NULL DEFAULT 0,
            `ProposedDate` LONGTEXT NULL,
            `ProposedTime` LONGTEXT NULL,
            `ProposedEndTime` LONGTEXT NULL,
            `Status` VARCHAR(20) NOT NULL DEFAULT 'pending',
            `AcknowledgedAt` DATETIME(6) NULL,
            `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
            `DecidedAt` DATETIME(6) NULL,
            `ResolvedAt` DATETIME(6) NULL,
            `AdminNote` LONGTEXT NULL,
            PRIMARY KEY (`Id`),
            KEY `IX_PresetCancellationDecisions_BookingId` (`BookingId`),
            KEY `IX_PresetCancellationDecisions_Status` (`Status`),
            CONSTRAINT `FK_PresetCancellationDecisions_Bookings` FOREIGN KEY (`BookingId`) REFERENCES `Bookings` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    "); } catch { }
    // Deduction ledger against a tutor's future payout (e.g. the 20% penalty on a
    // preset-class cancellation resolved toward a parent credit) — see
    // TutorPenalty.cs and PayoutsController's available-balance calculation.
    try { await context.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS `TutorPenalties` (
            `Id` INT NOT NULL AUTO_INCREMENT,
            `TutorId` INT NOT NULL,
            `BookingId` INT NULL,
            `Amount` DECIMAL(10,2) NOT NULL DEFAULT 0,
            `Reason` LONGTEXT NOT NULL,
            `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
            PRIMARY KEY (`Id`),
            KEY `IX_TutorPenalties_TutorId` (`TutorId`),
            CONSTRAINT `FK_TutorPenalties_Tutors` FOREIGN KEY (`TutorId`) REFERENCES `Tutors` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    "); } catch { }
    // Tutor document verification — VerificationStatus tracks the submission workflow
    // (not_submitted | pending | approved). There's no separate terminal "rejected"
    // status: a mixed-result review round leaves the tutor at "pending" with the
    // individual rejected TutorDocument rows driving the re-upload loop, until every
    // mandatory document is approved — see TutorsController.ApplyVerificationDecisions.
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Tutors` ADD COLUMN `VerificationStatus` VARCHAR(20) NOT NULL DEFAULT 'not_submitted'"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Tutors` ADD COLUMN `OfferingsUnlocked` TINYINT(1) NOT NULL DEFAULT 0"); } catch { }
    // See Tutor.LastSubmittedAt — distinguishes an unsent re-upload from one
    // already submitted and waiting on admin.
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Tutors` ADD COLUMN `LastSubmittedAt` DATETIME(6) NULL"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS `TutorDocuments` (
            `Id` INT NOT NULL AUTO_INCREMENT,
            `TutorId` INT NOT NULL,
            `DocumentType` VARCHAR(30) NOT NULL DEFAULT '',
            `FileUrl` LONGTEXT NULL,
            `ExternalUrl` LONGTEXT NULL,
            `FileName` LONGTEXT NULL,
            `FileSizeBytes` BIGINT NULL,
            `IdType` VARCHAR(20) NULL,
            `IdNumber` LONGTEXT NULL,
            `SortOrder` INT NOT NULL DEFAULT 0,
            `Status` VARCHAR(20) NOT NULL DEFAULT 'pending',
            `AdminNote` LONGTEXT NULL,
            `UploadedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
            `ReviewedAt` DATETIME(6) NULL,
            PRIMARY KEY (`Id`),
            KEY `IX_TutorDocuments_TutorId` (`TutorId`),
            CONSTRAINT `FK_TutorDocuments_Tutors` FOREIGN KEY (`TutorId`) REFERENCES `Tutors` (`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    "); } catch { }
    // Dual-row re-upload-after-rejection — see TutorDocument.ReplacesDocumentId/IsArchived.
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `TutorDocuments` ADD COLUMN `ReplacesDocumentId` INT NULL"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `TutorDocuments` ADD COLUMN `IsArchived` TINYINT(1) NOT NULL DEFAULT 0"); } catch { }

    // Chat read/unread state — see ChatMessage.IsRead / ChatController.GetMessages.
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `ChatMessages` ADD COLUMN `IsRead` TINYINT(1) NOT NULL DEFAULT 0"); } catch { }

    // Tracks the first confirmed lesson between a student and a tutor for a given
    // country + subject + level combination — see StudentTutorFirstClass.cs and
    // BookingsController.RecordFirstClassAsync.
    try { await context.Database.ExecuteSqlRawAsync(@"
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
            UNIQUE KEY `UQ_StudentTutorFirstClass`
                (`Country`,`Subject`,`Level`,`TutorId`,`StudentId`),
            KEY `IX_STFC_TutorId`   (`TutorId`),
            KEY `IX_STFC_StudentId` (`StudentId`),
            CONSTRAINT `FK_STFC_Tutors`
                FOREIGN KEY (`TutorId`)   REFERENCES `Tutors`(`Id`)   ON DELETE CASCADE,
            CONSTRAINT `FK_STFC_Students`
                FOREIGN KEY (`StudentId`) REFERENCES `Students`(`Id`) ON DELETE CASCADE,
            CONSTRAINT `FK_STFC_Bookings`
                FOREIGN KEY (`BookingId`) REFERENCES `Bookings`(`Id`) ON DELETE SET NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    "); } catch { }

    // Lesson reports redesigned to one row per student per session date (was one
    // row per booking with a separate edit-history table) — see LessonReport.cs.
    // Old LessonReportEdits table is dropped: reports are now immutable after
    // submission. Existing LessonReports data is intentionally dropped along with
    // it — the old schema (Covered/Performance/Homework per booking) is
    // incompatible with the new per-student-per-session shape.
    // Guarded on the old 'Covered' column still being present, so this only ever
    // fires once — an unconditional DROP here would wipe every lesson report a
    // tutor has since submitted on every subsequent app restart.
    try
    {
        var oldLessonReportsShape = await context.Database
            .SqlQueryRaw<int>(@"SELECT COUNT(*) AS Value FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'LessonReports' AND COLUMN_NAME = 'Covered'")
            .FirstOrDefaultAsync() > 0;
        if (oldLessonReportsShape)
        {
            await context.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS `LessonReportEdits`");
            await context.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS `LessonReports`");
        }
    }
    catch { }
    try { await context.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS `LessonReports` (
            `Id`                INT          NOT NULL AUTO_INCREMENT,
            `BookingId`         INT          NOT NULL,
            `StudentId`         INT          NOT NULL,
            `SessionDate`       VARCHAR(20)  NOT NULL DEFAULT '',
            `Attendance`        VARCHAR(20)  NOT NULL DEFAULT '',
            `Engagement`        INT          NULL,
            `Understanding`     VARCHAR(30)  NULL,
            `HomeworkCompletion` VARCHAR(30) NULL,
            `Remarks`           LONGTEXT     NULL,
            `SubmittedAt`       DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
            PRIMARY KEY (`Id`),
            UNIQUE KEY `UQ_LessonReport_BookingStudentDate`
                (`BookingId`, `StudentId`, `SessionDate`),
            KEY `IX_LessonReports_BookingId`  (`BookingId`),
            KEY `IX_LessonReports_StudentId`  (`StudentId`),
            CONSTRAINT `FK_LessonReports_Bookings`
                FOREIGN KEY (`BookingId`)  REFERENCES `Bookings`(`Id`) ON DELETE CASCADE,
            CONSTRAINT `FK_LessonReports_Students`
                FOREIGN KEY (`StudentId`) REFERENCES `Students`(`Id`) ON DELETE RESTRICT
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    "); } catch { }

    // SyllabusTopics table — platform-defined topics per country+subject+level.
    try { await context.Database.ExecuteSqlRawAsync(@"
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
    "); } catch { }

    // PresetGroupSyllabuses table
    try { await context.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS `PresetGroupSyllabuses` (
            `Id`              INT         NOT NULL AUTO_INCREMENT,
            `PresetGroupId`   VARCHAR(20) NOT NULL DEFAULT '',
            `SyllabusTopicId` INT         NOT NULL,
            PRIMARY KEY (`Id`),
            UNIQUE KEY `UQ_PresetGroupSyllabus` (`PresetGroupId`,`SyllabusTopicId`),
            KEY `IX_PresetGroupSyllabuses_PGId` (`PresetGroupId`),
            CONSTRAINT `FK_PGS_SyllabusTopics`
                FOREIGN KEY (`SyllabusTopicId`)
                REFERENCES `SyllabusTopics`(`Id`) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
    "); } catch { }

    // Seed SyllabusTopics — remapped onto this app's actual SubjectCatalog
    // vocabulary (frontend/app/services/subject-catalog.service.js) so every
    // seeded row is reachable through Setup Class: Malaysia UPSR uses "Primary N"
    // (catalog), not "Year N"; Singapore JC electives use "H2 <Subject>"
    // (catalog), not "<Subject> (H2)"; Country is the full "Singapore"/"Malaysia"
    // string used everywhere else in this app, not an ISO code; Malaysia STPM
    // uses "Lower Six"/"Upper Six" (catalog), not "Lower 6"/"Upper 6"; the
    // Malaysia SPM history/geography/accounting/economics subjects use the
    // catalog's English names, not their Malay equivalents. The original seed
    // draft's separate "Matriculation Year 1/2" track was dropped entirely — no
    // such level exists anywhere in SubjectCatalog, so it could never have been
    // reached through Setup Class. INSERT IGNORE means re-running on subsequent
    // startups is safe — no duplicate rows, no errors.
    try { await context.Database.ExecuteSqlRawAsync(@"
INSERT IGNORE INTO SyllabusTopics (Country,Subject,Level,Topic,SortOrder) VALUES

-- ============================================================
-- SINGAPORE — PRIMARY
-- ============================================================
-- Mathematics
('Singapore','Mathematics','Primary 1','Numbers to 100',1),
('Singapore','Mathematics','Primary 1','Addition and Subtraction',2),
('Singapore','Mathematics','Primary 1','Shapes and Patterns',3),
('Singapore','Mathematics','Primary 1','Ordinal Numbers',4),
('Singapore','Mathematics','Primary 1','Length and Mass',5),
('Singapore','Mathematics','Primary 1','Picture Graphs',6),

('Singapore','Mathematics','Primary 2','Numbers to 1000',1),
('Singapore','Mathematics','Primary 2','Multiplication and Division',2),
('Singapore','Mathematics','Primary 2','Money',3),
('Singapore','Mathematics','Primary 2','Fractions',4),
('Singapore','Mathematics','Primary 2','Time',5),
('Singapore','Mathematics','Primary 2','2D and 3D Shapes',6),

('Singapore','Mathematics','Primary 3','Whole Numbers to 10000',1),
('Singapore','Mathematics','Primary 3','Fractions',2),
('Singapore','Mathematics','Primary 3','Money and Measurement',3),
('Singapore','Mathematics','Primary 3','Area and Perimeter',4),
('Singapore','Mathematics','Primary 3','Bar Graphs',5),
('Singapore','Mathematics','Primary 3','Angles and Lines',6),

('Singapore','Mathematics','Primary 4','Whole Numbers to 100000',1),
('Singapore','Mathematics','Primary 4','Fractions and Decimals',2),
('Singapore','Mathematics','Primary 4','Area and Perimeter',3),
('Singapore','Mathematics','Primary 4','Symmetry and Tessellation',4),
('Singapore','Mathematics','Primary 4','Tables and Line Graphs',5),
('Singapore','Mathematics','Primary 4','Angles',6),

('Singapore','Mathematics','Primary 5','Whole Numbers and Fractions',1),
('Singapore','Mathematics','Primary 5','Ratio',2),
('Singapore','Mathematics','Primary 5','Percentage',3),
('Singapore','Mathematics','Primary 5','Area and Volume',4),
('Singapore','Mathematics','Primary 5','Speed',5),
('Singapore','Mathematics','Primary 5','Triangles and 4-sided Figures',6),

('Singapore','Mathematics','Primary 6','Fractions and Ratio',1),
('Singapore','Mathematics','Primary 6','Percentage',2),
('Singapore','Mathematics','Primary 6','Speed',3),
('Singapore','Mathematics','Primary 6','Algebra',4),
('Singapore','Mathematics','Primary 6','Area and Perimeter of Composite Figures',5),
('Singapore','Mathematics','Primary 6','Volume and Circles',6),

-- English Language Primary SG
('Singapore','English Language','Primary 1','Phonics and Word Recognition',1),
('Singapore','English Language','Primary 1','Listening and Speaking',2),
('Singapore','English Language','Primary 1','Reading Comprehension',3),
('Singapore','English Language','Primary 1','Sentence Writing',4),
('Singapore','English Language','Primary 1','Grammar Fundamentals',5),
('Singapore','English Language','Primary 1','Vocabulary Building',6),

('Singapore','English Language','Primary 2','Phonics and Spelling',1),
('Singapore','English Language','Primary 2','Grammar: Nouns and Verbs',2),
('Singapore','English Language','Primary 2','Reading and Comprehension',3),
('Singapore','English Language','Primary 2','Creative Writing',4),
('Singapore','English Language','Primary 2','Punctuation',5),
('Singapore','English Language','Primary 2','Vocabulary in Context',6),

('Singapore','English Language','Primary 3','Grammar: Tenses',1),
('Singapore','English Language','Primary 3','Comprehension: Open-ended',2),
('Singapore','English Language','Primary 3','Composition Writing',3),
('Singapore','English Language','Primary 3','Vocabulary Cloze',4),
('Singapore','English Language','Primary 3','Oral Communication',5),
('Singapore','English Language','Primary 3','Editing',6),

('Singapore','English Language','Primary 4','Grammar and Editing',1),
('Singapore','English Language','Primary 4','Comprehension: MCQ and Open-ended',2),
('Singapore','English Language','Primary 4','Situational Writing',3),
('Singapore','English Language','Primary 4','Continuous Writing',4),
('Singapore','English Language','Primary 4','Oral: Reading Aloud and Stimulus',5),
('Singapore','English Language','Primary 4','Vocabulary and Cloze',6),

('Singapore','English Language','Primary 5','Grammar: Complex Structures',1),
('Singapore','English Language','Primary 5','Comprehension Cloze',2),
('Singapore','English Language','Primary 5','Situational and Continuous Writing',3),
('Singapore','English Language','Primary 5','Synthesis and Transformation',4),
('Singapore','English Language','Primary 5','Oral: Stimulus-based Conversation',5),
('Singapore','English Language','Primary 5','Editing for Spelling and Grammar',6),

('Singapore','English Language','Primary 6','PSLE Comprehension',1),
('Singapore','English Language','Primary 6','PSLE Composition',2),
('Singapore','English Language','Primary 6','Grammar and Editing',3),
('Singapore','English Language','Primary 6','Synthesis and Transformation',4),
('Singapore','English Language','Primary 6','Oral: Reading and Conversation',5),
('Singapore','English Language','Primary 6','Vocabulary and Cloze',6),

-- Science Primary SG (P3-P6)
('Singapore','Science','Primary 3','Diversity of Living and Non-living Things',1),
('Singapore','Science','Primary 3','Fungi and Bacteria',2),
('Singapore','Science','Primary 3','Plant Parts and Functions',3),
('Singapore','Science','Primary 3','Animal Life Processes',4),
('Singapore','Science','Primary 3','Materials and their Properties',5),
('Singapore','Science','Primary 3','Measurement',6),

('Singapore','Science','Primary 4','Life Cycles',1),
('Singapore','Science','Primary 4','Food Chains',2),
('Singapore','Science','Primary 4','Human Digestive System',3),
('Singapore','Science','Primary 4','Properties of Matter',4),
('Singapore','Science','Primary 4','Water Cycle',5),
('Singapore','Science','Primary 4','Magnets',6),

('Singapore','Science','Primary 5','Reproduction in Plants and Animals',1),
('Singapore','Science','Primary 5','Cells and Systems',2),
('Singapore','Science','Primary 5','Electrical Systems',3),
('Singapore','Science','Primary 5','Forces and Energy',4),
('Singapore','Science','Primary 5','Photosynthesis',5),
('Singapore','Science','Primary 5','Heat and Temperature',6),

('Singapore','Science','Primary 6','Inheritance',1),
('Singapore','Science','Primary 6','Adaptation',2),
('Singapore','Science','Primary 6','Ecosystems',3),
('Singapore','Science','Primary 6','Man and Environment',4),
('Singapore','Science','Primary 6','Energy in Food',5),
('Singapore','Science','Primary 6','PSLE Revision: Systems and Interactions',6),

-- ============================================================
-- SINGAPORE — SECONDARY
-- ============================================================
('Singapore','Mathematics','Secondary 1','Factors and Multiples',1),
('Singapore','Mathematics','Secondary 1','Real Numbers and Integers',2),
('Singapore','Mathematics','Secondary 1','Ratio Rate and Proportion',3),
('Singapore','Mathematics','Secondary 1','Algebraic Expressions',4),
('Singapore','Mathematics','Secondary 1','Geometry: Lines and Angles',5),
('Singapore','Mathematics','Secondary 1','Statistics: Tables and Diagrams',6),

('Singapore','Mathematics','Secondary 2','Simultaneous Equations',1),
('Singapore','Mathematics','Secondary 2','Indices',2),
('Singapore','Mathematics','Secondary 2','Quadratic Expressions',3),
('Singapore','Mathematics','Secondary 2','Geometry: Triangles and Polygons',4),
('Singapore','Mathematics','Secondary 2','Mensuration: Area and Volume',5),
('Singapore','Mathematics','Secondary 2','Pythagoras Theorem',6),

('Singapore','Mathematics','Secondary 3','Quadratic Equations and Graphs',1),
('Singapore','Mathematics','Secondary 3','Trigonometry',2),
('Singapore','Mathematics','Secondary 3','Coordinate Geometry',3),
('Singapore','Mathematics','Secondary 3','Vectors',4),
('Singapore','Mathematics','Secondary 3','Probability',5),
('Singapore','Mathematics','Secondary 3','Statistics: Mean Median Mode',6),

('Singapore','Mathematics','Secondary 4','Quadratic Functions and Graphs',1),
('Singapore','Mathematics','Secondary 4','Matrices',2),
('Singapore','Mathematics','Secondary 4','Trigonometric Functions and Identities',3),
('Singapore','Mathematics','Secondary 4','Differentiation and Integration',4),
('Singapore','Mathematics','Secondary 4','Statistics: Normal Distribution',5),
('Singapore','Mathematics','Secondary 4','O-Level Examination Preparation',6),

('Singapore','Additional Mathematics','Secondary 3','Polynomials and Equations',1),
('Singapore','Additional Mathematics','Secondary 3','Indices and Surds',2),
('Singapore','Additional Mathematics','Secondary 3','Logarithms',3),
('Singapore','Additional Mathematics','Secondary 3','Trigonometry',4),
('Singapore','Additional Mathematics','Secondary 3','Differentiation',5),
('Singapore','Additional Mathematics','Secondary 3','Integration',6),

('Singapore','Additional Mathematics','Secondary 4','Further Differentiation',1),
('Singapore','Additional Mathematics','Secondary 4','Further Integration',2),
('Singapore','Additional Mathematics','Secondary 4','Kinematics',3),
('Singapore','Additional Mathematics','Secondary 4','Trigonometric Proofs and Equations',4),
('Singapore','Additional Mathematics','Secondary 4','Binomial Theorem',5),
('Singapore','Additional Mathematics','Secondary 4','O-Level A-Math Examination Preparation',6),

('Singapore','English Language','Secondary 1','Comprehension: Inference and Evaluation',1),
('Singapore','English Language','Secondary 1','Expository Writing',2),
('Singapore','English Language','Secondary 1','Grammar: Complex Sentences',3),
('Singapore','English Language','Secondary 1','Vocabulary and Idioms',4),
('Singapore','English Language','Secondary 1','Oral Communication',5),
('Singapore','English Language','Secondary 1','Summary Writing',6),

('Singapore','English Language','Secondary 2','Comprehension: Synthesis',1),
('Singapore','English Language','Secondary 2','Argumentative Writing',2),
('Singapore','English Language','Secondary 2','Grammar and Editing',3),
('Singapore','English Language','Secondary 2','Vocabulary in Context',4),
('Singapore','English Language','Secondary 2','Oral: Stimulus Discussion',5),
('Singapore','English Language','Secondary 2','Summary Writing',6),

('Singapore','English Language','Secondary 3','Comprehension: Extended Response',1),
('Singapore','English Language','Secondary 3','Argumentative and Expository Writing',2),
('Singapore','English Language','Secondary 3','Summary Writing',3),
('Singapore','English Language','Secondary 3','Grammar and Editing',4),
('Singapore','English Language','Secondary 3','Oral: Reading and Discussion',5),
('Singapore','English Language','Secondary 3','Situational Writing',6),

('Singapore','English Language','Secondary 4','O-Level Comprehension',1),
('Singapore','English Language','Secondary 4','O-Level Composition',2),
('Singapore','English Language','Secondary 4','Summary Writing',3),
('Singapore','English Language','Secondary 4','Grammar and Editing',4),
('Singapore','English Language','Secondary 4','Oral: Reading Aloud and Conversation',5),
('Singapore','English Language','Secondary 4','Situational Writing',6),

('Singapore','Physics','Secondary 3','Measurement',1),
('Singapore','Physics','Secondary 3','Kinematics',2),
('Singapore','Physics','Secondary 3','Dynamics',3),
('Singapore','Physics','Secondary 3','Mass Weight and Density',4),
('Singapore','Physics','Secondary 3','Turning Effect of Forces',5),
('Singapore','Physics','Secondary 3','Pressure',6),

('Singapore','Physics','Secondary 4','Thermal Physics',1),
('Singapore','Physics','Secondary 4','Light and Optics',2),
('Singapore','Physics','Secondary 4','Waves and Sound',3),
('Singapore','Physics','Secondary 4','Electricity and Magnetism',4),
('Singapore','Physics','Secondary 4','Electromagnetism',5),
('Singapore','Physics','Secondary 4','Radioactivity',6),

('Singapore','Chemistry','Secondary 3','Experimental Chemistry',1),
('Singapore','Chemistry','Secondary 3','The Particulate Nature of Matter',2),
('Singapore','Chemistry','Secondary 3','Formulae Stoichiometry and Moles',3),
('Singapore','Chemistry','Secondary 3','Acids Bases and Salts',4),
('Singapore','Chemistry','Secondary 3','The Periodic Table',5),
('Singapore','Chemistry','Secondary 3','Metals',6),

('Singapore','Chemistry','Secondary 4','Electrolysis',1),
('Singapore','Chemistry','Secondary 4','Energy from Chemicals',2),
('Singapore','Chemistry','Secondary 4','Speed of Reaction',3),
('Singapore','Chemistry','Secondary 4','Redox',4),
('Singapore','Chemistry','Secondary 4','Organic Chemistry',5),
('Singapore','Chemistry','Secondary 4','Atmosphere and Environment',6),

('Singapore','Biology','Secondary 3','Cell Structure and Organisation',1),
('Singapore','Biology','Secondary 3','Biological Molecules',2),
('Singapore','Biology','Secondary 3','Enzymes',3),
('Singapore','Biology','Secondary 3','Nutrition in Humans',4),
('Singapore','Biology','Secondary 3','Nutrition in Plants',5),
('Singapore','Biology','Secondary 3','Transport in Humans',6),

('Singapore','Biology','Secondary 4','Transport in Plants',1),
('Singapore','Biology','Secondary 4','Respiration',2),
('Singapore','Biology','Secondary 4','Excretion',3),
('Singapore','Biology','Secondary 4','Nervous System and Hormones',4),
('Singapore','Biology','Secondary 4','Genetics and Evolution',5),
('Singapore','Biology','Secondary 4','Reproduction',6),

('Singapore','History','Secondary 1','Ancient Civilisations',1),
('Singapore','History','Secondary 1','Medieval World',2),
('Singapore','History','Secondary 1','The Age of Exploration',3),
('Singapore','History','Secondary 1','The Industrial Revolution',4),
('Singapore','History','Secondary 1','Colonialism in Southeast Asia',5),
('Singapore','History','Secondary 1','Source-based Skills',6),

('Singapore','History','Secondary 2','World War I',1),
('Singapore','History','Secondary 2','The Great Depression',2),
('Singapore','History','Secondary 2','Rise of Totalitarianism',3),
('Singapore','History','Secondary 2','World War II',4),
('Singapore','History','Secondary 2','Cold War',5),
('Singapore','History','Secondary 2','Source-based Skills',6),

('Singapore','History','Secondary 3','Singapore 1819–1942',1),
('Singapore','History','Secondary 3','Japanese Occupation',2),
('Singapore','History','Secondary 3','Merger and Separation',3),
('Singapore','History','Secondary 3','Nation Building',4),
('Singapore','History','Secondary 3','Source-based Case Study',5),
('Singapore','History','Secondary 3','Essay Writing Skills',6),

('Singapore','History','Secondary 4','Decolonisation in Asia',1),
('Singapore','History','Secondary 4','Cold War in Asia',2),
('Singapore','History','Secondary 4','Independent Singapore',3),
('Singapore','History','Secondary 4','O-Level SBQ Practice',4),
('Singapore','History','Secondary 4','O-Level Essay Practice',5),
('Singapore','History','Secondary 4','Examination Preparation',6),

('Singapore','Geography','Secondary 1','Our Changing World',1),
('Singapore','Geography','Secondary 1','Plate Tectonics',2),
('Singapore','Geography','Secondary 1','Weather and Climate',3),
('Singapore','Geography','Secondary 1','Map Reading Skills',4),
('Singapore','Geography','Secondary 1','Ecosystems',5),
('Singapore','Geography','Secondary 1','Population and Settlement',6),

('Singapore','Geography','Secondary 2','Coasts',1),
('Singapore','Geography','Secondary 2','Rivers',2),
('Singapore','Geography','Secondary 2','Tropical Rainforests',3),
('Singapore','Geography','Secondary 2','Urban Issues',4),
('Singapore','Geography','Secondary 2','Food Resources',5),
('Singapore','Geography','Secondary 2','Geographical Skills',6),

('Singapore','Geography','Secondary 3','Plate Tectonics and Hazards',1),
('Singapore','Geography','Secondary 3','Coasts and Management',2),
('Singapore','Geography','Secondary 3','Urban Environments',3),
('Singapore','Geography','Secondary 3','Food and Hunger',4),
('Singapore','Geography','Secondary 3','Skills: Data Interpretation',5),
('Singapore','Geography','Secondary 3','Case Studies: Singapore and the World',6),

('Singapore','Geography','Secondary 4','Geographical Investigation',1),
('Singapore','Geography','Secondary 4','O-Level Case Study',2),
('Singapore','Geography','Secondary 4','Essay and Skills Paper',3),
('Singapore','Geography','Secondary 4','Climate Change',4),
('Singapore','Geography','Secondary 4','Resource Management',5),
('Singapore','Geography','Secondary 4','Examination Preparation',6),

('Singapore','Principles of Accounts','Secondary 3','Introduction to Accounting',1),
('Singapore','Principles of Accounts','Secondary 3','Double Entry Bookkeeping',2),
('Singapore','Principles of Accounts','Secondary 3','Trial Balance',3),
('Singapore','Principles of Accounts','Secondary 3','Financial Statements',4),
('Singapore','Principles of Accounts','Secondary 3','Adjustments to Accounts',5),
('Singapore','Principles of Accounts','Secondary 3','Bank Reconciliation',6),

('Singapore','Principles of Accounts','Secondary 4','Partnership Accounts',1),
('Singapore','Principles of Accounts','Secondary 4','Company Accounts',2),
('Singapore','Principles of Accounts','Secondary 4','Ratio Analysis',3),
('Singapore','Principles of Accounts','Secondary 4','Cash Flow Statement',4),
('Singapore','Principles of Accounts','Secondary 4','Accounting for Inventory',5),
('Singapore','Principles of Accounts','Secondary 4','O-Level Examination Preparation',6),

-- ============================================================
-- SINGAPORE — JC
-- ============================================================
('Singapore','General Paper','JC1','Current Affairs and Critical Thinking',1),
('Singapore','General Paper','JC1','Science and Technology',2),
('Singapore','General Paper','JC1','Society and Culture',3),
('Singapore','General Paper','JC1','Politics and Governance',4),
('Singapore','General Paper','JC1','Essay Writing Techniques',5),
('Singapore','General Paper','JC1','Comprehension Skills',6),

('Singapore','General Paper','JC2','Global Issues and Perspectives',1),
('Singapore','General Paper','JC2','Environmental Issues',2),
('Singapore','General Paper','JC2','Media and Communication',3),
('Singapore','General Paper','JC2','A-Level Essay Practice',4),
('Singapore','General Paper','JC2','A-Level Comprehension Practice',5),
('Singapore','General Paper','JC2','Examination Strategies',6),

('Singapore','H2 Mathematics','JC1','Functions and Graphs',1),
('Singapore','H2 Mathematics','JC1','Sequences and Series',2),
('Singapore','H2 Mathematics','JC1','Vectors',3),
('Singapore','H2 Mathematics','JC1','Introduction to Complex Numbers',4),
('Singapore','H2 Mathematics','JC1','Differentiation Techniques',5),
('Singapore','H2 Mathematics','JC1','Integration Techniques',6),

('Singapore','H2 Mathematics','JC2','Differential Equations',1),
('Singapore','H2 Mathematics','JC2','Maclaurin Series',2),
('Singapore','H2 Mathematics','JC2','Permutations and Combinations',3),
('Singapore','H2 Mathematics','JC2','Probability',4),
('Singapore','H2 Mathematics','JC2','Sampling and Hypothesis Testing',5),
('Singapore','H2 Mathematics','JC2','A-Level Examination Preparation',6),

('Singapore','H2 Physics','JC1','Measurement',1),
('Singapore','H2 Physics','JC1','Kinematics',2),
('Singapore','H2 Physics','JC1','Dynamics',3),
('Singapore','H2 Physics','JC1','Forces',4),
('Singapore','H2 Physics','JC1','Work Energy Power',5),
('Singapore','H2 Physics','JC1','Motion in a Circle',6),

('Singapore','H2 Physics','JC2','Gravitational Field',1),
('Singapore','H2 Physics','JC2','Electric Field',2),
('Singapore','H2 Physics','JC2','Electromagnetism',3),
('Singapore','H2 Physics','JC2','Quantum Physics',4),
('Singapore','H2 Physics','JC2','Nuclear Physics',5),
('Singapore','H2 Physics','JC2','A-Level Examination Preparation',6),

('Singapore','H2 Chemistry','JC1','Atomic Structure',1),
('Singapore','H2 Chemistry','JC1','Chemical Bonding',2),
('Singapore','H2 Chemistry','JC1','Energetics',3),
('Singapore','H2 Chemistry','JC1','Reaction Kinetics',4),
('Singapore','H2 Chemistry','JC1','Equilibria',5),
('Singapore','H2 Chemistry','JC1','Electrochemistry',6),

('Singapore','H2 Chemistry','JC2','Organic Chemistry: Mechanisms',1),
('Singapore','H2 Chemistry','JC2','Organic Chemistry: Functional Groups',2),
('Singapore','H2 Chemistry','JC2','Transition Elements',3),
('Singapore','H2 Chemistry','JC2','Acid-Base Equilibria',4),
('Singapore','H2 Chemistry','JC2','Solubility Equilibria',5),
('Singapore','H2 Chemistry','JC2','A-Level Examination Preparation',6),

('Singapore','H2 Biology','JC1','Cell and Membrane Structure',1),
('Singapore','H2 Biology','JC1','Biological Molecules',2),
('Singapore','H2 Biology','JC1','Enzymes',3),
('Singapore','H2 Biology','JC1','Cell Division',4),
('Singapore','H2 Biology','JC1','Molecular Genetics',5),
('Singapore','H2 Biology','JC1','Gene Expression',6),

('Singapore','H2 Biology','JC2','Inheritance and Variation',1),
('Singapore','H2 Biology','JC2','Regulation and Control',2),
('Singapore','H2 Biology','JC2','Plant Physiology',3),
('Singapore','H2 Biology','JC2','Evolution',4),
('Singapore','H2 Biology','JC2','Ecology',5),
('Singapore','H2 Biology','JC2','A-Level Examination Preparation',6),

('Singapore','H2 Economics','JC1','Introduction to Economics',1),
('Singapore','H2 Economics','JC1','Price Mechanism',2),
('Singapore','H2 Economics','JC1','Elasticity',3),
('Singapore','H2 Economics','JC1','Market Failure',4),
('Singapore','H2 Economics','JC1','Firm and Industry',5),
('Singapore','H2 Economics','JC1','Government Intervention',6),

('Singapore','H2 Economics','JC2','National Income',1),
('Singapore','H2 Economics','JC2','Macroeconomic Objectives',2),
('Singapore','H2 Economics','JC2','Inflation and Unemployment',3),
('Singapore','H2 Economics','JC2','International Trade',4),
('Singapore','H2 Economics','JC2','Balance of Payments',5),
('Singapore','H2 Economics','JC2','A-Level Examination Preparation',6),

-- ============================================================
-- MALAYSIA — PRIMARY
-- ============================================================
('Malaysia','Mathematics','Primary 1','Numbers 1 to 20',1),
('Malaysia','Mathematics','Primary 1','Addition and Subtraction',2),
('Malaysia','Mathematics','Primary 1','Shapes',3),
('Malaysia','Mathematics','Primary 1','Measurement: Length and Mass',4),
('Malaysia','Mathematics','Primary 1','Money',5),
('Malaysia','Mathematics','Primary 1','Time',6),

('Malaysia','Mathematics','Primary 2','Numbers to 1000',1),
('Malaysia','Mathematics','Primary 2','Multiplication and Division',2),
('Malaysia','Mathematics','Primary 2','Fractions',3),
('Malaysia','Mathematics','Primary 2','Measurement: Volume and Length',4),
('Malaysia','Mathematics','Primary 2','Shapes and Space',5),
('Malaysia','Mathematics','Primary 2','Data Handling',6),

('Malaysia','Mathematics','Primary 3','Whole Numbers to 10000',1),
('Malaysia','Mathematics','Primary 3','Fractions and Decimals',2),
('Malaysia','Mathematics','Primary 3','Measurement',3),
('Malaysia','Mathematics','Primary 3','Perimeter and Area',4),
('Malaysia','Mathematics','Primary 3','Time',5),
('Malaysia','Mathematics','Primary 3','Data Handling',6),

('Malaysia','Mathematics','Primary 4','Whole Numbers to 100000',1),
('Malaysia','Mathematics','Primary 4','Fractions and Mixed Numbers',2),
('Malaysia','Mathematics','Primary 4','Decimals',3),
('Malaysia','Mathematics','Primary 4','Percentages',4),
('Malaysia','Mathematics','Primary 4','Measurement: Area and Perimeter',5),
('Malaysia','Mathematics','Primary 4','Angles',6),

('Malaysia','Mathematics','Primary 5','Whole Numbers',1),
('Malaysia','Mathematics','Primary 5','Fractions and Decimals',2),
('Malaysia','Mathematics','Primary 5','Percentages and Ratio',3),
('Malaysia','Mathematics','Primary 5','Integers and Coordinates',4),
('Malaysia','Mathematics','Primary 5','Shape and Space',5),
('Malaysia','Mathematics','Primary 5','Statistics',6),

('Malaysia','Mathematics','Primary 6','Whole Numbers and Operations',1),
('Malaysia','Mathematics','Primary 6','Fractions Decimals and Percentages',2),
('Malaysia','Mathematics','Primary 6','Ratio and Proportion',3),
('Malaysia','Mathematics','Primary 6','Integers and Algebra',4),
('Malaysia','Mathematics','Primary 6','Geometry and Measurement',5),
('Malaysia','Mathematics','Primary 6','Statistics and UPSR Preparation',6),

('Malaysia','English','Primary 1','Phonics and Reading',1),
('Malaysia','English','Primary 1','Listening and Speaking',2),
('Malaysia','English','Primary 1','Vocabulary Building',3),
('Malaysia','English','Primary 1','Sentence Construction',4),
('Malaysia','English','Primary 1','Simple Grammar',5),
('Malaysia','English','Primary 1','Writing: Simple Sentences',6),

('Malaysia','English','Primary 2','Grammar: Nouns and Verbs',1),
('Malaysia','English','Primary 2','Reading Comprehension',2),
('Malaysia','English','Primary 2','Writing: Guided Composition',3),
('Malaysia','English','Primary 2','Vocabulary in Context',4),
('Malaysia','English','Primary 2','Oral Skills',5),
('Malaysia','English','Primary 2','Punctuation and Spelling',6),

('Malaysia','English','Primary 3','Grammar: Tenses',1),
('Malaysia','English','Primary 3','Reading: Comprehension Passages',2),
('Malaysia','English','Primary 3','Writing: Narrative Composition',3),
('Malaysia','English','Primary 3','Vocabulary: Synonyms and Antonyms',4),
('Malaysia','English','Primary 3','Oral: Reading and Conversation',5),
('Malaysia','English','Primary 3','Editing and Proofreading',6),

('Malaysia','English','Primary 4','Grammar: Complex Structures',1),
('Malaysia','English','Primary 4','Comprehension: Inference',2),
('Malaysia','English','Primary 4','Writing: Descriptive Composition',3),
('Malaysia','English','Primary 4','Vocabulary: Idioms and Phrases',4),
('Malaysia','English','Primary 4','Oral: Stimulus and Discussion',5),
('Malaysia','English','Primary 4','Summary Writing',6),

('Malaysia','English','Primary 5','Grammar: Advanced Tenses',1),
('Malaysia','English','Primary 5','Comprehension: Evaluation',2),
('Malaysia','English','Primary 5','Writing: Argumentative Essays',3),
('Malaysia','English','Primary 5','Vocabulary: Register and Style',4),
('Malaysia','English','Primary 5','Oral: Debate and Presentation',5),
('Malaysia','English','Primary 5','UPSR Preparation',6),

('Malaysia','English','Primary 6','UPSR Comprehension',1),
('Malaysia','English','Primary 6','UPSR Composition',2),
('Malaysia','English','Primary 6','Grammar and Editing',3),
('Malaysia','English','Primary 6','Vocabulary',4),
('Malaysia','English','Primary 6','Oral Skills',5),
('Malaysia','English','Primary 6','Examination Strategies',6),

('Malaysia','Science','Primary 3','Living and Non-living Things',1),
('Malaysia','Science','Primary 3','Plants',2),
('Malaysia','Science','Primary 3','Animals',3),
('Malaysia','Science','Primary 3','Senses',4),
('Malaysia','Science','Primary 3','Materials',5),
('Malaysia','Science','Primary 3','Forces',6),

('Malaysia','Science','Primary 4','Living Things and their Needs',1),
('Malaysia','Science','Primary 4','Food and Nutrition',2),
('Malaysia','Science','Primary 4','Water and Air',3),
('Malaysia','Science','Primary 4','Earth and Space',4),
('Malaysia','Science','Primary 4','Energy',5),
('Malaysia','Science','Primary 4','Materials',6),

('Malaysia','Science','Primary 5','Life Processes',1),
('Malaysia','Science','Primary 5','Biodiversity',2),
('Malaysia','Science','Primary 5','Ecosystems',3),
('Malaysia','Science','Primary 5','Matter',4),
('Malaysia','Science','Primary 5','Electricity',5),
('Malaysia','Science','Primary 5','Forces and Motion',6),

('Malaysia','Science','Primary 6','Human Body Systems',1),
('Malaysia','Science','Primary 6','Reproduction',2),
('Malaysia','Science','Primary 6','Biodiversity and Conservation',3),
('Malaysia','Science','Primary 6','Matter and Changes',4),
('Malaysia','Science','Primary 6','Energy Transformations',5),
('Malaysia','Science','Primary 6','UPSR Science Preparation',6),

('Malaysia','Bahasa Malaysia','Primary 1','Abjad dan Suku Kata',1),
('Malaysia','Bahasa Malaysia','Primary 1','Membaca dan Menulis',2),
('Malaysia','Bahasa Malaysia','Primary 1','Kosa Kata Asas',3),
('Malaysia','Bahasa Malaysia','Primary 1','Ayat Mudah',4),
('Malaysia','Bahasa Malaysia','Primary 1','Lisan: Mendengar dan Bertutur',5),
('Malaysia','Bahasa Malaysia','Primary 1','Tanda Baca',6),

('Malaysia','Bahasa Malaysia','Primary 2','Kata Nama dan Kata Kerja',1),
('Malaysia','Bahasa Malaysia','Primary 2','Pemahaman',2),
('Malaysia','Bahasa Malaysia','Primary 2','Penulisan Ayat',3),
('Malaysia','Bahasa Malaysia','Primary 2','Kosa Kata',4),
('Malaysia','Bahasa Malaysia','Primary 2','Lisan',5),
('Malaysia','Bahasa Malaysia','Primary 2','Ejaan',6),

('Malaysia','Bahasa Malaysia','Primary 3','Tatabahasa: Kata Adjektif dan Kata Sendi',1),
('Malaysia','Bahasa Malaysia','Primary 3','Pemahaman Petikan',2),
('Malaysia','Bahasa Malaysia','Primary 3','Karangan Berpandu',3),
('Malaysia','Bahasa Malaysia','Primary 3','Kosa Kata dan Peribahasa',4),
('Malaysia','Bahasa Malaysia','Primary 3','Lisan: Bercerita',5),
('Malaysia','Bahasa Malaysia','Primary 3','Tanda Baca dan Ejaan',6),

('Malaysia','Bahasa Malaysia','Primary 4','Tatabahasa: Kata Hubung dan Frasa',1),
('Malaysia','Bahasa Malaysia','Primary 4','Pemahaman: Soalan Inferens',2),
('Malaysia','Bahasa Malaysia','Primary 4','Karangan Naratif',3),
('Malaysia','Bahasa Malaysia','Primary 4','Peribahasa dan Simpulan Bahasa',4),
('Malaysia','Bahasa Malaysia','Primary 4','Lisan: Perbincangan',5),
('Malaysia','Bahasa Malaysia','Primary 4','Mekanis: Ejaan dan Tanda Baca',6),

('Malaysia','Bahasa Malaysia','Primary 5','Tatabahasa Lanjutan',1),
('Malaysia','Bahasa Malaysia','Primary 5','Pemahaman: Penilaian',2),
('Malaysia','Bahasa Malaysia','Primary 5','Penulisan: Karangan Pelbagai Genre',3),
('Malaysia','Bahasa Malaysia','Primary 5','Kosa Kata Tinggi',4),
('Malaysia','Bahasa Malaysia','Primary 5','Lisan: Pidato dan Syarahan',5),
('Malaysia','Bahasa Malaysia','Primary 5','Persediaan UPSR',6),

('Malaysia','Bahasa Malaysia','Primary 6','Pemahaman UPSR',1),
('Malaysia','Bahasa Malaysia','Primary 6','Penulisan UPSR',2),
('Malaysia','Bahasa Malaysia','Primary 6','Tatabahasa',3),
('Malaysia','Bahasa Malaysia','Primary 6','Kosa Kata dan Peribahasa',4),
('Malaysia','Bahasa Malaysia','Primary 6','Lisan',5),
('Malaysia','Bahasa Malaysia','Primary 6','Strategi Peperiksaan',6),

-- ============================================================
-- MALAYSIA — SECONDARY
-- ============================================================
('Malaysia','Mathematics','Form 1','Integers and Rational Numbers',1),
('Malaysia','Mathematics','Form 1','Algebraic Expressions',2),
('Malaysia','Mathematics','Form 1','Linear Equations',3),
('Malaysia','Mathematics','Form 1','Ratio Rate and Proportion',4),
('Malaysia','Mathematics','Form 1','Geometry: Lines and Angles',5),
('Malaysia','Mathematics','Form 1','Statistics: Frequency',6),

('Malaysia','Mathematics','Form 2','Directed Numbers',1),
('Malaysia','Mathematics','Form 2','Squares Cubes and Roots',2),
('Malaysia','Mathematics','Form 2','Algebraic Formulae',3),
('Malaysia','Mathematics','Form 2','Geometric Constructions',4),
('Malaysia','Mathematics','Form 2','Pythagoras Theorem',5),
('Malaysia','Mathematics','Form 2','Graphs of Functions',6),

('Malaysia','Mathematics','Form 3','Indices',1),
('Malaysia','Mathematics','Form 3','Standard Form',2),
('Malaysia','Mathematics','Form 3','Consumer Mathematics',3),
('Malaysia','Mathematics','Form 3','Linear Inequalities',4),
('Malaysia','Mathematics','Form 3','Graphs of Motion',5),
('Malaysia','Mathematics','Form 3','Statistics: Measures of Central Tendency',6),

('Malaysia','Mathematics','Form 4','Quadratic Functions',1),
('Malaysia','Mathematics','Form 4','Number Bases',2),
('Malaysia','Mathematics','Form 4','Logical Reasoning',3),
('Malaysia','Mathematics','Form 4','Operations on Sets',4),
('Malaysia','Mathematics','Form 4','Network in Graph Theory',5),
('Malaysia','Mathematics','Form 4','Linear Programming',6),

('Malaysia','Mathematics','Form 5','Variation',1),
('Malaysia','Mathematics','Form 5','Matrices',2),
('Malaysia','Mathematics','Form 5','Consumer Mathematics: Insurance and Investment',3),
('Malaysia','Mathematics','Form 5','Probability',4),
('Malaysia','Mathematics','Form 5','Trigonometry',5),
('Malaysia','Mathematics','Form 5','SPM Examination Preparation',6),

('Malaysia','Additional Mathematics','Form 4','Functions',1),
('Malaysia','Additional Mathematics','Form 4','Quadratic Functions and Equations',2),
('Malaysia','Additional Mathematics','Form 4','Systems of Equations',3),
('Malaysia','Additional Mathematics','Form 4','Indices Surds and Logarithms',4),
('Malaysia','Additional Mathematics','Form 4','Progressions',5),
('Malaysia','Additional Mathematics','Form 4','Linear Law',6),

('Malaysia','Additional Mathematics','Form 5','Integration',1),
('Malaysia','Additional Mathematics','Form 5','Differentiation',2),
('Malaysia','Additional Mathematics','Form 5','Permutations and Combinations',3),
('Malaysia','Additional Mathematics','Form 5','Probability Distribution',4),
('Malaysia','Additional Mathematics','Form 5','Trigonometric Functions',5),
('Malaysia','Additional Mathematics','Form 5','SPM Add Maths Examination Preparation',6),

('Malaysia','English','Form 1','Grammar: Tenses and Sentence Structure',1),
('Malaysia','English','Form 1','Reading Comprehension',2),
('Malaysia','English','Form 1','Writing: Guided Essays',3),
('Malaysia','English','Form 1','Vocabulary in Context',4),
('Malaysia','English','Form 1','Oral and Listening',5),
('Malaysia','English','Form 1','Literature Component',6),

('Malaysia','English','Form 2','Grammar: Reported Speech and Passive Voice',1),
('Malaysia','English','Form 2','Comprehension: Inference and Evaluation',2),
('Malaysia','English','Form 2','Writing: Narrative and Descriptive',3),
('Malaysia','English','Form 2','Vocabulary: Idioms and Phrases',4),
('Malaysia','English','Form 2','Oral: Conversation and Discussion',5),
('Malaysia','English','Form 2','Literature: Novel and Poetry',6),

('Malaysia','English','Form 3','Grammar: Complex Structures',1),
('Malaysia','English','Form 3','Comprehension: Extended Response',2),
('Malaysia','English','Form 3','Writing: Argumentative and Expository',3),
('Malaysia','English','Form 3','Summary Writing',4),
('Malaysia','English','Form 3','Oral: Presentation',5),
('Malaysia','English','Form 3','PT3 Preparation',6),

('Malaysia','English','Form 4','Grammar: Advanced Structures',1),
('Malaysia','English','Form 4','Comprehension: Critical Reading',2),
('Malaysia','English','Form 4','Writing: Formal and Informal',3),
('Malaysia','English','Form 4','Literature: Novel Analysis',4),
('Malaysia','English','Form 4','Oral: Stimulus Discussion',5),
('Malaysia','English','Form 4','SPM Preparation: Paper 1',6),

('Malaysia','English','Form 5','SPM Comprehension',1),
('Malaysia','English','Form 5','SPM Essay Writing',2),
('Malaysia','English','Form 5','Summary Writing',3),
('Malaysia','English','Form 5','Literature: In-depth Analysis',4),
('Malaysia','English','Form 5','Oral Examination Strategies',5),
('Malaysia','English','Form 5','Examination Preparation',6),

('Malaysia','Bahasa Malaysia','Form 1','Tatabahasa: Kata dan Frasa',1),
('Malaysia','Bahasa Malaysia','Form 1','Pemahaman Teks',2),
('Malaysia','Bahasa Malaysia','Form 1','Karangan Naratif',3),
('Malaysia','Bahasa Malaysia','Form 1','Kosa Kata dan Peribahasa',4),
('Malaysia','Bahasa Malaysia','Form 1','Lisan: Bertutur dan Membaca',5),
('Malaysia','Bahasa Malaysia','Form 1','Komponen Sastera',6),

('Malaysia','Bahasa Malaysia','Form 2','Tatabahasa: Ayat Majmuk',1),
('Malaysia','Bahasa Malaysia','Form 2','Pemahaman: Soalan Terbuka',2),
('Malaysia','Bahasa Malaysia','Form 2','Karangan Deskriptif',3),
('Malaysia','Bahasa Malaysia','Form 2','Peribahasa dan Simpulan Bahasa',4),
('Malaysia','Bahasa Malaysia','Form 2','Lisan: Perbincangan',5),
('Malaysia','Bahasa Malaysia','Form 2','Sastera: Puisi dan Prosa',6),

('Malaysia','Bahasa Malaysia','Form 3','Tatabahasa Lanjutan',1),
('Malaysia','Bahasa Malaysia','Form 3','Pemahaman: Inferens dan Penilaian',2),
('Malaysia','Bahasa Malaysia','Form 3','Karangan Pelbagai Genre',3),
('Malaysia','Bahasa Malaysia','Form 3','Kosa Kata Tinggi',4),
('Malaysia','Bahasa Malaysia','Form 3','Lisan: Ucapan dan Syarahan',5),
('Malaysia','Bahasa Malaysia','Form 3','Persediaan PT3',6),

('Malaysia','Bahasa Malaysia','Form 4','Tatabahasa: Kata Majmuk dan Imbuhan',1),
('Malaysia','Bahasa Malaysia','Form 4','Pemahaman Kritis',2),
('Malaysia','Bahasa Malaysia','Form 4','Penulisan Akademik',3),
('Malaysia','Bahasa Malaysia','Form 4','Sastera: Novel SPM',4),
('Malaysia','Bahasa Malaysia','Form 4','Lisan: Pidato dan Debat',5),
('Malaysia','Bahasa Malaysia','Form 4','Persediaan SPM: Kertas 1',6),

('Malaysia','Bahasa Malaysia','Form 5','Pemahaman SPM',1),
('Malaysia','Bahasa Malaysia','Form 5','Penulisan SPM',2),
('Malaysia','Bahasa Malaysia','Form 5','Tatabahasa',3),
('Malaysia','Bahasa Malaysia','Form 5','Sastera: Analisis Mendalam',4),
('Malaysia','Bahasa Malaysia','Form 5','Lisan SPM',5),
('Malaysia','Bahasa Malaysia','Form 5','Strategi Peperiksaan',6),

('Malaysia','Science','Form 1','Introduction to Science',1),
('Malaysia','Science','Form 1','Cell Biology',2),
('Malaysia','Science','Form 1','Matter',3),
('Malaysia','Science','Form 1','Forces and Motion',4),
('Malaysia','Science','Form 1','Heat',5),
('Malaysia','Science','Form 1','Light and Optics',6),

('Malaysia','Science','Form 2','World Through Our Senses',1),
('Malaysia','Science','Form 2','Nutrition',2),
('Malaysia','Science','Form 2','Biodiversity',3),
('Malaysia','Science','Form 2','Interdependence Among Living Organisms',4),
('Malaysia','Science','Form 2','Water and Solution',5),
('Malaysia','Science','Form 2','Air Pressure',6),

('Malaysia','Science','Form 3','Coordination and Response',1),
('Malaysia','Science','Form 3','Reproduction',2),
('Malaysia','Science','Form 3','Heredity',3),
('Malaysia','Science','Form 3','Evolution',4),
('Malaysia','Science','Form 3','Chemicals in Industry',5),
('Malaysia','Science','Form 3','PT3 Preparation',6),

('Malaysia','Physics','Form 4','Introduction to Physics',1),
('Malaysia','Physics','Form 4','Force and Motion',2),
('Malaysia','Physics','Form 4','Gravitation',3),
('Malaysia','Physics','Form 4','Heat',4),
('Malaysia','Physics','Form 4','Waves',5),
('Malaysia','Physics','Form 4','Light and Optics',6),

('Malaysia','Physics','Form 5','Electricity',1),
('Malaysia','Physics','Form 5','Electromagnetism',2),
('Malaysia','Physics','Form 5','Electronics',3),
('Malaysia','Physics','Form 5','Nuclear Physics',4),
('Malaysia','Physics','Form 5','Quantum Physics',5),
('Malaysia','Physics','Form 5','SPM Physics Examination Preparation',6),

('Malaysia','Chemistry','Form 4','Introduction to Chemistry',1),
('Malaysia','Chemistry','Form 4','Chemical Formulae and Equations',2),
('Malaysia','Chemistry','Form 4','Periodic Table',3),
('Malaysia','Chemistry','Form 4','Chemical Bonds',4),
('Malaysia','Chemistry','Form 4','Electrochemistry',5),
('Malaysia','Chemistry','Form 4','Acids Bases and Salts',6),

('Malaysia','Chemistry','Form 5','Rate of Reaction',1),
('Malaysia','Chemistry','Form 5','Carbon Compounds',2),
('Malaysia','Chemistry','Form 5','Polymers',3),
('Malaysia','Chemistry','Form 5','Manufactured Substances',4),
('Malaysia','Chemistry','Form 5','Alloys and Composite Materials',5),
('Malaysia','Chemistry','Form 5','SPM Chemistry Examination Preparation',6),

('Malaysia','Biology','Form 4','Introduction to Biology',1),
('Malaysia','Biology','Form 4','Cell Biology',2),
('Malaysia','Biology','Form 4','Movement of Substances',3),
('Malaysia','Biology','Form 4','Chemical Composition of the Cell',4),
('Malaysia','Biology','Form 4','Cell Division',5),
('Malaysia','Biology','Form 4','Nutrition',6),

('Malaysia','Biology','Form 5','Transport',1),
('Malaysia','Biology','Form 5','Locomotion and Support',2),
('Malaysia','Biology','Form 5','Reproduction and Growth',3),
('Malaysia','Biology','Form 5','Inheritance',4),
('Malaysia','Biology','Form 5','Variation',5),
('Malaysia','Biology','Form 5','SPM Biology Examination Preparation',6),

('Malaysia','History','Form 1','Sejarah dan Sumber Sejarah',1),
('Malaysia','History','Form 1','Tamadun Awal Dunia',2),
('Malaysia','History','Form 1','Tamadun Islam',3),
('Malaysia','History','Form 1','Kerajaan Melayu Awal',4),
('Malaysia','History','Form 1','Kerajaan Agraria dan Maritim',5),
('Malaysia','History','Form 1','Kemahiran Sejarah',6),

('Malaysia','History','Form 2','Kedatangan Barat',1),
('Malaysia','History','Form 2','Penjajahan di Asia Tenggara',2),
('Malaysia','History','Form 2','Nasionalisme di Asia',3),
('Malaysia','History','Form 2','Gerakan Nasionalisme di Malaysia',4),
('Malaysia','History','Form 2','Perang Dunia Kedua',5),
('Malaysia','History','Form 2','Kemahiran SBQ',6),

('Malaysia','History','Form 3','Kemerdekaan Malaysia',1),
('Malaysia','History','Form 3','Pembinaan Negara Bangsa',2),
('Malaysia','History','Form 3','Perlembagaan Malaysia',3),
('Malaysia','History','Form 3','Kemajuan Ekonomi dan Sosial',4),
('Malaysia','History','Form 3','PT3 Sejarah Preparation',5),
('Malaysia','History','Form 3','Penulisan Esei Sejarah',6),

('Malaysia','History','Form 4','Kemunculan Tamadun Awal di Asia',1),
('Malaysia','History','Form 4','Perkembangan di Eropah',2),
('Malaysia','History','Form 4','Era Penjajahan dan Nasionalisme',3),
('Malaysia','History','Form 4','Pembinaan Negara Malaysia',4),
('Malaysia','History','Form 4','Hubungan Antarabangsa',5),
('Malaysia','History','Form 4','Kemahiran Menjawab SPM',6),

('Malaysia','History','Form 5','Demokrasi di Malaysia',1),
('Malaysia','History','Form 5','Dasar Luar Malaysia',2),
('Malaysia','History','Form 5','Malaysia dalam Kerjasama Serantau',3),
('Malaysia','History','Form 5','Malaysia Negara Maju',4),
('Malaysia','History','Form 5','SPM Sejarah Examination Preparation',5),
('Malaysia','History','Form 5','Penulisan Esei dan SBQ',6),

('Malaysia','Geography','Form 1','Kedudukan dan Koordinat',1),
('Malaysia','Geography','Form 1','Lakaran Peta dan Skala',2),
('Malaysia','Geography','Form 1','Tanda Aras dan Kontur',3),
('Malaysia','Geography','Form 1','Cuaca dan Iklim',4),
('Malaysia','Geography','Form 1','Bentuk Muka Bumi',5),
('Malaysia','Geography','Form 1','Kemahiran Geografi',6),

('Malaysia','Geography','Form 2','Sumber Asli',1),
('Malaysia','Geography','Form 2','Perindustrian',2),
('Malaysia','Geography','Form 2','Pertanian',3),
('Malaysia','Geography','Form 2','Penempatan dan Bandar',4),
('Malaysia','Geography','Form 2','Pengangkutan dan Komunikasi',5),
('Malaysia','Geography','Form 2','Alam Sekitar',6),

('Malaysia','Geography','Form 3','Geografi Fizikal Malaysia',1),
('Malaysia','Geography','Form 3','Pembangunan Ekonomi Malaysia',2),
('Malaysia','Geography','Form 3','Isu Alam Sekitar',3),
('Malaysia','Geography','Form 3','PT3 Persediaan',4),
('Malaysia','Geography','Form 3','Kemahiran Menjawab Soalan',5),
('Malaysia','Geography','Form 3','Kajian Lapangan',6),

('Malaysia','Geography','Form 4','Geografi Fizikal Lanjutan',1),
('Malaysia','Geography','Form 4','Iklim dan Perubahan Cuaca',2),
('Malaysia','Geography','Form 4','Hidrologi',3),
('Malaysia','Geography','Form 4','Geomorfologi',4),
('Malaysia','Geography','Form 4','Pengurusan Alam Sekitar',5),
('Malaysia','Geography','Form 4','Kemahiran SPM',6),

('Malaysia','Geography','Form 5','Geografi Manusia',1),
('Malaysia','Geography','Form 5','Urbanisasi',2),
('Malaysia','Geography','Form 5','Pertanian dan Industri',3),
('Malaysia','Geography','Form 5','Perdagangan dan Pembangunan',4),
('Malaysia','Geography','Form 5','SPM Geografi Preparation',5),
('Malaysia','Geography','Form 5','Esei dan Kajian Kes',6),

('Malaysia','Accounting','Form 4','Pengenalan Perakaunan',1),
('Malaysia','Accounting','Form 4','Catatan Bergu',2),
('Malaysia','Accounting','Form 4','Imbangan Duga',3),
('Malaysia','Accounting','Form 4','Penyata Kewangan',4),
('Malaysia','Accounting','Form 4','Pelarasan Akaun',5),
('Malaysia','Accounting','Form 4','Penyesuaian Bank',6),

('Malaysia','Accounting','Form 5','Akaun Perkongsian',1),
('Malaysia','Accounting','Form 5','Akaun Syarikat',2),
('Malaysia','Accounting','Form 5','Analisis Nisbah',3),
('Malaysia','Accounting','Form 5','Penyata Aliran Tunai',4),
('Malaysia','Accounting','Form 5','Perakaunan untuk Inventori',5),
('Malaysia','Accounting','Form 5','Persediaan SPM',6),

('Malaysia','Economics','Form 4','Asas Ekonomi',1),
('Malaysia','Economics','Form 4','Penawaran dan Permintaan',2),
('Malaysia','Economics','Form 4','Elastisiti',3),
('Malaysia','Economics','Form 4','Pasaran dan Campur Tangan Kerajaan',4),
('Malaysia','Economics','Form 4','Pengeluaran dan Kos',5),
('Malaysia','Economics','Form 4','Struktur Pasaran',6),

('Malaysia','Economics','Form 5','Pendapatan Negara',1),
('Malaysia','Economics','Form 5','Wang dan Sistem Perbankan',2),
('Malaysia','Economics','Form 5','Perdagangan Antarabangsa',3),
('Malaysia','Economics','Form 5','Imbangan Pembayaran',4),
('Malaysia','Economics','Form 5','Dasar Fiskal dan Monetari',5),
('Malaysia','Economics','Form 5','Persediaan SPM Ekonomi',6),

-- ============================================================
-- MALAYSIA — PRE-U
-- ============================================================
('Malaysia','Mathematics (T)','Lower Six','Functions',1),
('Malaysia','Mathematics (T)','Lower Six','Sequences and Series',2),
('Malaysia','Mathematics (T)','Lower Six','Matrices',3),
('Malaysia','Mathematics (T)','Lower Six','Complex Numbers',4),
('Malaysia','Mathematics (T)','Lower Six','Differentiation',5),
('Malaysia','Mathematics (T)','Lower Six','Integration',6),

('Malaysia','Mathematics (T)','Upper Six','Differential Equations',1),
('Malaysia','Mathematics (T)','Upper Six','Probability',2),
('Malaysia','Mathematics (T)','Upper Six','Probability Distributions',3),
('Malaysia','Mathematics (T)','Upper Six','Sampling and Estimation',4),
('Malaysia','Mathematics (T)','Upper Six','Hypothesis Testing',5),
('Malaysia','Mathematics (T)','Upper Six','STPM Examination Preparation',6),

('Malaysia','Physics','Lower Six','Physical Quantities and Measurement',1),
('Malaysia','Physics','Lower Six','Kinematics and Dynamics',2),
('Malaysia','Physics','Lower Six','Work Energy and Power',3),
('Malaysia','Physics','Lower Six','Circular and Oscillatory Motion',4),
('Malaysia','Physics','Lower Six','Gravitation and Fields',5),
('Malaysia','Physics','Lower Six','Thermal Physics',6),

('Malaysia','Physics','Upper Six','Waves',1),
('Malaysia','Physics','Upper Six','Optics',2),
('Malaysia','Physics','Upper Six','Electricity and Magnetism',3),
('Malaysia','Physics','Upper Six','Electronics',4),
('Malaysia','Physics','Upper Six','Modern Physics',5),
('Malaysia','Physics','Upper Six','STPM Examination Preparation',6),

('Malaysia','Chemistry','Lower Six','Atomic Structure',1),
('Malaysia','Chemistry','Lower Six','Chemical Bonding',2),
('Malaysia','Chemistry','Lower Six','Energetics',3),
('Malaysia','Chemistry','Lower Six','Electrochemistry',4),
('Malaysia','Chemistry','Lower Six','Reaction Kinetics',5),
('Malaysia','Chemistry','Lower Six','Chemical Equilibria',6),

('Malaysia','Chemistry','Upper Six','Acid-Base Equilibria',1),
('Malaysia','Chemistry','Upper Six','Organic Chemistry',2),
('Malaysia','Chemistry','Upper Six','Transition Elements',3),
('Malaysia','Chemistry','Upper Six','Environmental Chemistry',4),
('Malaysia','Chemistry','Upper Six','Industrial Chemistry',5),
('Malaysia','Chemistry','Upper Six','STPM Examination Preparation',6),

('Malaysia','Biology','Lower Six','Cell Biology',1),
('Malaysia','Biology','Lower Six','Biochemistry',2),
('Malaysia','Biology','Lower Six','Genetics',3),
('Malaysia','Biology','Lower Six','Physiology: Nutrition and Transport',4),
('Malaysia','Biology','Lower Six','Physiology: Gas Exchange and Excretion',5),
('Malaysia','Biology','Lower Six','Coordination and Response',6),

('Malaysia','Biology','Upper Six','Reproduction and Growth',1),
('Malaysia','Biology','Upper Six','Ecology',2),
('Malaysia','Biology','Upper Six','Evolution',3),
('Malaysia','Biology','Upper Six','Biotechnology',4),
('Malaysia','Biology','Upper Six','Microbiology',5),
('Malaysia','Biology','Upper Six','STPM Examination Preparation',6),

('Malaysia','Economics','Lower Six','Introduction to Economics',1),
('Malaysia','Economics','Lower Six','Demand and Supply',2),
('Malaysia','Economics','Lower Six','Elasticity',3),
('Malaysia','Economics','Lower Six','Theory of the Firm',4),
('Malaysia','Economics','Lower Six','Market Structures',5),
('Malaysia','Economics','Lower Six','Market Failure and Government Intervention',6),

('Malaysia','Economics','Upper Six','National Income',1),
('Malaysia','Economics','Upper Six','Money and Banking',2),
('Malaysia','Economics','Upper Six','Macroeconomic Objectives',3),
('Malaysia','Economics','Upper Six','International Trade',4),
('Malaysia','Economics','Upper Six','Economic Development',5),
('Malaysia','Economics','Upper Six','STPM Examination Preparation',6),

('Malaysia','General Studies','Lower Six','Malaysia: Negara Kita',1),
('Malaysia','General Studies','Lower Six','Pembangunan Ekonomi',2),
('Malaysia','General Studies','Lower Six','Isu Sosial',3),
('Malaysia','General Studies','Lower Six','Hubungan Antarabangsa',4),
('Malaysia','General Studies','Lower Six','Kemahiran Penulisan Esei',5),
('Malaysia','General Studies','Lower Six','Kemahiran Menjawab Soalan Struktur',6),

('Malaysia','General Studies','Upper Six','Isu Global',1),
('Malaysia','General Studies','Upper Six','Teknologi Maklumat dan Komunikasi',2),
('Malaysia','General Studies','Upper Six','Alam Sekitar',3),
('Malaysia','General Studies','Upper Six','STPM Persediaan Kertas 1',4),
('Malaysia','General Studies','Upper Six','STPM Persediaan Kertas 2',5),
('Malaysia','General Studies','Upper Six','Strategi Peperiksaan',6);
"); } catch { }

    // Second syllabus seeding pass — covers subjects the first pass missed
    // entirely (Mother Tongue Chinese/Malay/Tamil, Combined Science, Social
    // Studies, Literature in English, Computing, Art, Music, Design &
    // Technology, Food & Nutrition, several Secondary-5/JC/STPM electives,
    // Malaysia History/Islamic Education/Moral Education/Commerce/Business
    // Studies/IT/Islamic Studies/Art & Design/Further Mathematics (T)) plus a
    // few Primary 1-2 Science gaps. Triggered by a tutor being unable to
    // publish a class for Mother Tongue (Chinese) since it had zero seeded
    // topics — see also SetupClass, where syllabus selection was subsequently
    // made optional rather than mandatory as a permanent safety net for
    // whatever this pass still doesn't cover. INSERT IGNORE — safe to re-run.
    try { await context.Database.ExecuteSqlRawAsync(@"
INSERT IGNORE INTO SyllabusTopics (Country,Subject,Level,Topic,SortOrder) VALUES

-- ============================================================
-- SINGAPORE PSLE — Mother Tongue (Chinese/Malay/Tamil), all compulsory
-- subjects that had zero coverage before this pass
-- ============================================================
('Singapore','Mother Tongue (Chinese)','Primary 1','Pinyin and Basic Characters',1),
('Singapore','Mother Tongue (Chinese)','Primary 1','Listening and Speaking',2),
('Singapore','Mother Tongue (Chinese)','Primary 1','Simple Sentences',3),
('Singapore','Mother Tongue (Chinese)','Primary 1','Character Recognition',4),
('Singapore','Mother Tongue (Chinese)','Primary 1','Basic Vocabulary',5),
('Singapore','Mother Tongue (Chinese)','Primary 1','Stroke Order and Writing',6),

('Singapore','Mother Tongue (Chinese)','Primary 2','Sentence Construction',1),
('Singapore','Mother Tongue (Chinese)','Primary 2','Reading Comprehension',2),
('Singapore','Mother Tongue (Chinese)','Primary 2','Vocabulary Expansion',3),
('Singapore','Mother Tongue (Chinese)','Primary 2','Guided Composition',4),
('Singapore','Mother Tongue (Chinese)','Primary 2','Oral Communication',5),
('Singapore','Mother Tongue (Chinese)','Primary 2','Character Writing',6),

('Singapore','Mother Tongue (Chinese)','Primary 3','Paragraph Writing',1),
('Singapore','Mother Tongue (Chinese)','Primary 3','Comprehension: Cloze Passage',2),
('Singapore','Mother Tongue (Chinese)','Primary 3','Idioms and Proverbs',3),
('Singapore','Mother Tongue (Chinese)','Primary 3','Oral: Reading Aloud',4),
('Singapore','Mother Tongue (Chinese)','Primary 3','Grammar Basics',5),
('Singapore','Mother Tongue (Chinese)','Primary 3','Listening Comprehension',6),

('Singapore','Mother Tongue (Chinese)','Primary 4','Composition Writing',1),
('Singapore','Mother Tongue (Chinese)','Primary 4','Comprehension: Open-ended',2),
('Singapore','Mother Tongue (Chinese)','Primary 4','Idioms and Proverbs',3),
('Singapore','Mother Tongue (Chinese)','Primary 4','Oral: Picture Discussion',4),
('Singapore','Mother Tongue (Chinese)','Primary 4','Grammar and Sentence Patterns',5),
('Singapore','Mother Tongue (Chinese)','Primary 4','Vocabulary Cloze',6),

('Singapore','Mother Tongue (Chinese)','Primary 5','Composition: Narrative Writing',1),
('Singapore','Mother Tongue (Chinese)','Primary 5','Comprehension: Inference',2),
('Singapore','Mother Tongue (Chinese)','Primary 5','Synthesis and Transformation',3),
('Singapore','Mother Tongue (Chinese)','Primary 5','Oral: Conversation',4),
('Singapore','Mother Tongue (Chinese)','Primary 5','Advanced Idioms and Proverbs',5),
('Singapore','Mother Tongue (Chinese)','Primary 5','Listening Comprehension',6),

('Singapore','Mother Tongue (Chinese)','Primary 6','PSLE Composition',1),
('Singapore','Mother Tongue (Chinese)','Primary 6','PSLE Comprehension',2),
('Singapore','Mother Tongue (Chinese)','Primary 6','Synthesis and Transformation',3),
('Singapore','Mother Tongue (Chinese)','Primary 6','Oral Examination Practice',4),
('Singapore','Mother Tongue (Chinese)','Primary 6','Vocabulary and Cloze',5),
('Singapore','Mother Tongue (Chinese)','Primary 6','PSLE Examination Strategies',6),

('Singapore','Mother Tongue (Malay)','Primary 1','Sebutan dan Suku Kata',1),
('Singapore','Mother Tongue (Malay)','Primary 1','Mendengar dan Bertutur',2),
('Singapore','Mother Tongue (Malay)','Primary 1','Ayat Mudah',3),
('Singapore','Mother Tongue (Malay)','Primary 1','Kosa Kata Asas',4),
('Singapore','Mother Tongue (Malay)','Primary 1','Membaca Awal',5),
('Singapore','Mother Tongue (Malay)','Primary 1','Tulisan Awal',6),

('Singapore','Mother Tongue (Malay)','Primary 2','Penulisan Ayat',1),
('Singapore','Mother Tongue (Malay)','Primary 2','Pemahaman Bacaan',2),
('Singapore','Mother Tongue (Malay)','Primary 2','Kosa Kata',3),
('Singapore','Mother Tongue (Malay)','Primary 2','Karangan Berpandu',4),
('Singapore','Mother Tongue (Malay)','Primary 2','Lisan',5),
('Singapore','Mother Tongue (Malay)','Primary 2','Ejaan',6),

('Singapore','Mother Tongue (Malay)','Primary 3','Karangan Pendek',1),
('Singapore','Mother Tongue (Malay)','Primary 3','Pemahaman Petikan',2),
('Singapore','Mother Tongue (Malay)','Primary 3','Peribahasa Asas',3),
('Singapore','Mother Tongue (Malay)','Primary 3','Lisan: Bacaan Kuat',4),
('Singapore','Mother Tongue (Malay)','Primary 3','Tatabahasa Asas',5),
('Singapore','Mother Tongue (Malay)','Primary 3','Kefahaman Mendengar',6),

('Singapore','Mother Tongue (Malay)','Primary 4','Karangan',1),
('Singapore','Mother Tongue (Malay)','Primary 4','Pemahaman Terbuka',2),
('Singapore','Mother Tongue (Malay)','Primary 4','Peribahasa dan Simpulan Bahasa',3),
('Singapore','Mother Tongue (Malay)','Primary 4','Lisan: Perbincangan Gambar',4),
('Singapore','Mother Tongue (Malay)','Primary 4','Tatabahasa',5),
('Singapore','Mother Tongue (Malay)','Primary 4','Kosa Kata Kloz',6),

('Singapore','Mother Tongue (Malay)','Primary 5','Karangan Naratif',1),
('Singapore','Mother Tongue (Malay)','Primary 5','Pemahaman: Inferens',2),
('Singapore','Mother Tongue (Malay)','Primary 5','Sintesis dan Transformasi',3),
('Singapore','Mother Tongue (Malay)','Primary 5','Lisan: Perbualan',4),
('Singapore','Mother Tongue (Malay)','Primary 5','Peribahasa Lanjutan',5),
('Singapore','Mother Tongue (Malay)','Primary 5','Kefahaman Mendengar',6),

('Singapore','Mother Tongue (Malay)','Primary 6','Karangan PSLE',1),
('Singapore','Mother Tongue (Malay)','Primary 6','Pemahaman PSLE',2),
('Singapore','Mother Tongue (Malay)','Primary 6','Sintesis dan Transformasi',3),
('Singapore','Mother Tongue (Malay)','Primary 6','Lisan PSLE',4),
('Singapore','Mother Tongue (Malay)','Primary 6','Kosa Kata dan Kloz',5),
('Singapore','Mother Tongue (Malay)','Primary 6','Strategi Peperiksaan PSLE',6),

('Singapore','Mother Tongue (Tamil)','Primary 1','Basic Letters and Sounds',1),
('Singapore','Mother Tongue (Tamil)','Primary 1','Listening and Speaking',2),
('Singapore','Mother Tongue (Tamil)','Primary 1','Simple Sentences',3),
('Singapore','Mother Tongue (Tamil)','Primary 1','Basic Vocabulary',4),
('Singapore','Mother Tongue (Tamil)','Primary 1','Early Reading',5),
('Singapore','Mother Tongue (Tamil)','Primary 1','Early Writing',6),

('Singapore','Mother Tongue (Tamil)','Primary 2','Sentence Writing',1),
('Singapore','Mother Tongue (Tamil)','Primary 2','Reading Comprehension',2),
('Singapore','Mother Tongue (Tamil)','Primary 2','Vocabulary Building',3),
('Singapore','Mother Tongue (Tamil)','Primary 2','Guided Composition',4),
('Singapore','Mother Tongue (Tamil)','Primary 2','Oral Skills',5),
('Singapore','Mother Tongue (Tamil)','Primary 2','Spelling',6),

('Singapore','Mother Tongue (Tamil)','Primary 3','Short Composition',1),
('Singapore','Mother Tongue (Tamil)','Primary 3','Comprehension Passages',2),
('Singapore','Mother Tongue (Tamil)','Primary 3','Basic Idioms',3),
('Singapore','Mother Tongue (Tamil)','Primary 3','Oral: Reading Aloud',4),
('Singapore','Mother Tongue (Tamil)','Primary 3','Basic Grammar',5),
('Singapore','Mother Tongue (Tamil)','Primary 3','Listening Comprehension',6),

('Singapore','Mother Tongue (Tamil)','Primary 4','Composition Writing',1),
('Singapore','Mother Tongue (Tamil)','Primary 4','Comprehension: Open-ended',2),
('Singapore','Mother Tongue (Tamil)','Primary 4','Idioms and Proverbs',3),
('Singapore','Mother Tongue (Tamil)','Primary 4','Oral: Picture Discussion',4),
('Singapore','Mother Tongue (Tamil)','Primary 4','Grammar',5),
('Singapore','Mother Tongue (Tamil)','Primary 4','Vocabulary Cloze',6),

('Singapore','Mother Tongue (Tamil)','Primary 5','Narrative Composition',1),
('Singapore','Mother Tongue (Tamil)','Primary 5','Comprehension: Inference',2),
('Singapore','Mother Tongue (Tamil)','Primary 5','Synthesis and Transformation',3),
('Singapore','Mother Tongue (Tamil)','Primary 5','Oral: Conversation',4),
('Singapore','Mother Tongue (Tamil)','Primary 5','Advanced Idioms',5),
('Singapore','Mother Tongue (Tamil)','Primary 5','Listening Comprehension',6),

('Singapore','Mother Tongue (Tamil)','Primary 6','PSLE Composition',1),
('Singapore','Mother Tongue (Tamil)','Primary 6','PSLE Comprehension',2),
('Singapore','Mother Tongue (Tamil)','Primary 6','Synthesis and Transformation',3),
('Singapore','Mother Tongue (Tamil)','Primary 6','Oral Examination Practice',4),
('Singapore','Mother Tongue (Tamil)','Primary 6','Vocabulary and Cloze',5),
('Singapore','Mother Tongue (Tamil)','Primary 6','PSLE Examination Strategies',6),

-- ============================================================
-- SINGAPORE PSLE — Science gap fill (Primary 1-2; P3-6 already seeded)
-- ============================================================
('Singapore','Science','Primary 1','Observing My Surroundings',1),
('Singapore','Science','Primary 1','Living and Non-living Things',2),
('Singapore','Science','Primary 1','My Five Senses',3),
('Singapore','Science','Primary 1','Plants Around Us',4),
('Singapore','Science','Primary 1','Animals Around Us',5),
('Singapore','Science','Primary 1','Simple Science Tools',6),

('Singapore','Science','Primary 2','Plant and Animal Needs',1),
('Singapore','Science','Primary 2','Materials and their Uses',2),
('Singapore','Science','Primary 2','Push and Pull',3),
('Singapore','Science','Primary 2','Light and Shadows',4),
('Singapore','Science','Primary 2','Simple Measurement',5),
('Singapore','Science','Primary 2','Caring for the Environment',6),

-- ============================================================
-- SINGAPORE N/O-LEVEL — Mother Tongue (Chinese/Malay/Tamil), Secondary 1-5
-- ============================================================
('Singapore','Mother Tongue (Chinese)','Secondary 1','Comprehension: Inference',1),
('Singapore','Mother Tongue (Chinese)','Secondary 1','Composition Writing',2),
('Singapore','Mother Tongue (Chinese)','Secondary 1','Grammar and Sentence Patterns',3),
('Singapore','Mother Tongue (Chinese)','Secondary 1','Idioms and Proverbs',4),
('Singapore','Mother Tongue (Chinese)','Secondary 1','Oral Communication',5),
('Singapore','Mother Tongue (Chinese)','Secondary 1','Listening Comprehension',6),

('Singapore','Mother Tongue (Chinese)','Secondary 2','Comprehension: Evaluation',1),
('Singapore','Mother Tongue (Chinese)','Secondary 2','Argumentative Writing',2),
('Singapore','Mother Tongue (Chinese)','Secondary 2','Grammar and Editing',3),
('Singapore','Mother Tongue (Chinese)','Secondary 2','Idioms and Proverbs',4),
('Singapore','Mother Tongue (Chinese)','Secondary 2','Oral: Stimulus Discussion',5),
('Singapore','Mother Tongue (Chinese)','Secondary 2','Summary Writing',6),

('Singapore','Mother Tongue (Chinese)','Secondary 3','Comprehension: Extended Response',1),
('Singapore','Mother Tongue (Chinese)','Secondary 3','Composition: Argumentative',2),
('Singapore','Mother Tongue (Chinese)','Secondary 3','Summary Writing',3),
('Singapore','Mother Tongue (Chinese)','Secondary 3','Grammar and Editing',4),
('Singapore','Mother Tongue (Chinese)','Secondary 3','Oral: Reading and Discussion',5),
('Singapore','Mother Tongue (Chinese)','Secondary 3','Listening Comprehension',6),

('Singapore','Mother Tongue (Chinese)','Secondary 4','O-Level Comprehension',1),
('Singapore','Mother Tongue (Chinese)','Secondary 4','O-Level Composition',2),
('Singapore','Mother Tongue (Chinese)','Secondary 4','Summary Writing',3),
('Singapore','Mother Tongue (Chinese)','Secondary 4','Grammar and Editing',4),
('Singapore','Mother Tongue (Chinese)','Secondary 4','Oral Examination Practice',5),
('Singapore','Mother Tongue (Chinese)','Secondary 4','Listening Examination Practice',6),

('Singapore','Mother Tongue (Chinese)','Secondary 5','N-Level Comprehension',1),
('Singapore','Mother Tongue (Chinese)','Secondary 5','N-Level Composition',2),
('Singapore','Mother Tongue (Chinese)','Secondary 5','Summary Writing',3),
('Singapore','Mother Tongue (Chinese)','Secondary 5','Grammar and Editing',4),
('Singapore','Mother Tongue (Chinese)','Secondary 5','Oral Examination Practice',5),
('Singapore','Mother Tongue (Chinese)','Secondary 5','Examination Strategies',6),

('Singapore','Mother Tongue (Malay)','Secondary 1','Pemahaman: Inferens',1),
('Singapore','Mother Tongue (Malay)','Secondary 1','Penulisan Karangan',2),
('Singapore','Mother Tongue (Malay)','Secondary 1','Tatabahasa',3),
('Singapore','Mother Tongue (Malay)','Secondary 1','Peribahasa dan Simpulan Bahasa',4),
('Singapore','Mother Tongue (Malay)','Secondary 1','Lisan',5),
('Singapore','Mother Tongue (Malay)','Secondary 1','Kefahaman Mendengar',6),

('Singapore','Mother Tongue (Malay)','Secondary 2','Pemahaman: Penilaian',1),
('Singapore','Mother Tongue (Malay)','Secondary 2','Karangan Hujah',2),
('Singapore','Mother Tongue (Malay)','Secondary 2','Tatabahasa dan Penyuntingan',3),
('Singapore','Mother Tongue (Malay)','Secondary 2','Peribahasa',4),
('Singapore','Mother Tongue (Malay)','Secondary 2','Lisan: Perbincangan',5),
('Singapore','Mother Tongue (Malay)','Secondary 2','Ringkasan',6),

('Singapore','Mother Tongue (Malay)','Secondary 3','Pemahaman Lanjutan',1),
('Singapore','Mother Tongue (Malay)','Secondary 3','Karangan Hujah',2),
('Singapore','Mother Tongue (Malay)','Secondary 3','Ringkasan',3),
('Singapore','Mother Tongue (Malay)','Secondary 3','Tatabahasa dan Penyuntingan',4),
('Singapore','Mother Tongue (Malay)','Secondary 3','Lisan: Bacaan dan Perbincangan',5),
('Singapore','Mother Tongue (Malay)','Secondary 3','Kefahaman Mendengar',6),

('Singapore','Mother Tongue (Malay)','Secondary 4','Pemahaman O-Level',1),
('Singapore','Mother Tongue (Malay)','Secondary 4','Karangan O-Level',2),
('Singapore','Mother Tongue (Malay)','Secondary 4','Ringkasan',3),
('Singapore','Mother Tongue (Malay)','Secondary 4','Tatabahasa dan Penyuntingan',4),
('Singapore','Mother Tongue (Malay)','Secondary 4','Lisan',5),
('Singapore','Mother Tongue (Malay)','Secondary 4','Kefahaman Mendengar',6),

('Singapore','Mother Tongue (Malay)','Secondary 5','Pemahaman N-Level',1),
('Singapore','Mother Tongue (Malay)','Secondary 5','Karangan N-Level',2),
('Singapore','Mother Tongue (Malay)','Secondary 5','Ringkasan',3),
('Singapore','Mother Tongue (Malay)','Secondary 5','Tatabahasa',4),
('Singapore','Mother Tongue (Malay)','Secondary 5','Lisan',5),
('Singapore','Mother Tongue (Malay)','Secondary 5','Strategi Peperiksaan',6),

('Singapore','Mother Tongue (Tamil)','Secondary 1','Comprehension: Inference',1),
('Singapore','Mother Tongue (Tamil)','Secondary 1','Composition Writing',2),
('Singapore','Mother Tongue (Tamil)','Secondary 1','Grammar',3),
('Singapore','Mother Tongue (Tamil)','Secondary 1','Idioms and Proverbs',4),
('Singapore','Mother Tongue (Tamil)','Secondary 1','Oral Communication',5),
('Singapore','Mother Tongue (Tamil)','Secondary 1','Listening Comprehension',6),

('Singapore','Mother Tongue (Tamil)','Secondary 2','Comprehension: Evaluation',1),
('Singapore','Mother Tongue (Tamil)','Secondary 2','Argumentative Writing',2),
('Singapore','Mother Tongue (Tamil)','Secondary 2','Grammar and Editing',3),
('Singapore','Mother Tongue (Tamil)','Secondary 2','Idioms and Proverbs',4),
('Singapore','Mother Tongue (Tamil)','Secondary 2','Oral: Stimulus Discussion',5),
('Singapore','Mother Tongue (Tamil)','Secondary 2','Summary Writing',6),

('Singapore','Mother Tongue (Tamil)','Secondary 3','Comprehension: Extended Response',1),
('Singapore','Mother Tongue (Tamil)','Secondary 3','Composition: Argumentative',2),
('Singapore','Mother Tongue (Tamil)','Secondary 3','Summary Writing',3),
('Singapore','Mother Tongue (Tamil)','Secondary 3','Grammar and Editing',4),
('Singapore','Mother Tongue (Tamil)','Secondary 3','Oral: Reading and Discussion',5),
('Singapore','Mother Tongue (Tamil)','Secondary 3','Listening Comprehension',6),

('Singapore','Mother Tongue (Tamil)','Secondary 4','O-Level Comprehension',1),
('Singapore','Mother Tongue (Tamil)','Secondary 4','O-Level Composition',2),
('Singapore','Mother Tongue (Tamil)','Secondary 4','Summary Writing',3),
('Singapore','Mother Tongue (Tamil)','Secondary 4','Grammar and Editing',4),
('Singapore','Mother Tongue (Tamil)','Secondary 4','Oral Examination Practice',5),
('Singapore','Mother Tongue (Tamil)','Secondary 4','Listening Examination Practice',6),

('Singapore','Mother Tongue (Tamil)','Secondary 5','N-Level Comprehension',1),
('Singapore','Mother Tongue (Tamil)','Secondary 5','N-Level Composition',2),
('Singapore','Mother Tongue (Tamil)','Secondary 5','Summary Writing',3),
('Singapore','Mother Tongue (Tamil)','Secondary 5','Grammar and Editing',4),
('Singapore','Mother Tongue (Tamil)','Secondary 5','Oral Examination Practice',5),
('Singapore','Mother Tongue (Tamil)','Secondary 5','Examination Strategies',6),

-- ============================================================
-- SINGAPORE N/O-LEVEL — remaining electives, Secondary 1-5
-- ============================================================
('Singapore','Combined Science','Secondary 1','Introduction to Science Practical Skills',1),
('Singapore','Combined Science','Secondary 1','Cell Structure',2),
('Singapore','Combined Science','Secondary 1','States of Matter',3),
('Singapore','Combined Science','Secondary 1','Forces and Motion',4),
('Singapore','Combined Science','Secondary 1','Energy',5),
('Singapore','Combined Science','Secondary 1','Simple Chemical Reactions',6),

('Singapore','Combined Science','Secondary 2','Nutrition and Digestion',1),
('Singapore','Combined Science','Secondary 2','Respiration',2),
('Singapore','Combined Science','Secondary 2','The Periodic Table',3),
('Singapore','Combined Science','Secondary 2','Acids Bases and Salts',4),
('Singapore','Combined Science','Secondary 2','Electricity Basics',5),
('Singapore','Combined Science','Secondary 2','Light and Sound',6),

('Singapore','Combined Science','Secondary 3','Chemical Bonding',1),
('Singapore','Combined Science','Secondary 3','Reproduction in Humans',2),
('Singapore','Combined Science','Secondary 3','Forces and Pressure',3),
('Singapore','Combined Science','Secondary 3','Electromagnetism',4),
('Singapore','Combined Science','Secondary 3','Ecosystems',5),
('Singapore','Combined Science','Secondary 3','Rate of Reaction',6),

('Singapore','Combined Science','Secondary 4','Organic Chemistry Basics',1),
('Singapore','Combined Science','Secondary 4','Genetics and Inheritance',2),
('Singapore','Combined Science','Secondary 4','Electricity and Circuits',3),
('Singapore','Combined Science','Secondary 4','Waves',4),
('Singapore','Combined Science','Secondary 4','Redox Reactions',5),
('Singapore','Combined Science','Secondary 4','O-Level Examination Preparation',6),

('Singapore','Combined Science','Secondary 5','Revision: Chemistry Core Topics',1),
('Singapore','Combined Science','Secondary 5','Revision: Physics Core Topics',2),
('Singapore','Combined Science','Secondary 5','Revision: Biology Core Topics',3),
('Singapore','Combined Science','Secondary 5','Practical Skills Revision',4),
('Singapore','Combined Science','Secondary 5','N-Level Examination Preparation',5),
('Singapore','Combined Science','Secondary 5','Examination Strategies',6),

('Singapore','Social Studies','Secondary 1','Living in a Diverse Society',1),
('Singapore','Social Studies','Secondary 1','Singapore''s Governance',2),
('Singapore','Social Studies','Secondary 1','Source-based Skills',3),
('Singapore','Social Studies','Secondary 1','Citizenship and Identity',4),
('Singapore','Social Studies','Secondary 1','Managing Diversity',5),
('Singapore','Social Studies','Secondary 1','Case Studies',6),

('Singapore','Social Studies','Secondary 2','Being Global and Being Singaporean',1),
('Singapore','Social Studies','Secondary 2','Riot and Reconciliation Case Study',2),
('Singapore','Social Studies','Secondary 2','Source-based Skills',3),
('Singapore','Social Studies','Secondary 2','Globalisation Issues',4),
('Singapore','Social Studies','Secondary 2','Media Literacy',5),
('Singapore','Social Studies','Secondary 2','Case Studies',6),

('Singapore','Social Studies','Secondary 3','Governance and Society',1),
('Singapore','Social Studies','Secondary 3','Global Issues',2),
('Singapore','Social Studies','Secondary 3','Source-based Case Study',3),
('Singapore','Social Studies','Secondary 3','Response Question Skills',4),
('Singapore','Social Studies','Secondary 3','Issue Analysis',5),
('Singapore','Social Studies','Secondary 3','Essay Writing Skills',6),

('Singapore','Social Studies','Secondary 4','O-Level SBQ Practice',1),
('Singapore','Social Studies','Secondary 4','O-Level Response Question Practice',2),
('Singapore','Social Studies','Secondary 4','Case Study Revision',3),
('Singapore','Social Studies','Secondary 4','Issue Analysis',4),
('Singapore','Social Studies','Secondary 4','Source Evaluation Skills',5),
('Singapore','Social Studies','Secondary 4','Examination Preparation',6),

('Singapore','Social Studies','Secondary 5','N-Level SBQ Practice',1),
('Singapore','Social Studies','Secondary 5','N-Level Response Question Practice',2),
('Singapore','Social Studies','Secondary 5','Case Study Revision',3),
('Singapore','Social Studies','Secondary 5','Issue Analysis',4),
('Singapore','Social Studies','Secondary 5','Source Evaluation Skills',5),
('Singapore','Social Studies','Secondary 5','Examination Strategies',6),

('Singapore','Literature in English','Secondary 1','Introduction to Poetry',1),
('Singapore','Literature in English','Secondary 1','Introduction to Prose',2),
('Singapore','Literature in English','Secondary 1','Introduction to Drama',3),
('Singapore','Literature in English','Secondary 1','Literary Devices',4),
('Singapore','Literature in English','Secondary 1','Character Analysis',5),
('Singapore','Literature in English','Secondary 1','Response Writing',6),

('Singapore','Literature in English','Secondary 2','Poetry Analysis',1),
('Singapore','Literature in English','Secondary 2','Prose Analysis',2),
('Singapore','Literature in English','Secondary 2','Drama Analysis',3),
('Singapore','Literature in English','Secondary 2','Themes and Context',4),
('Singapore','Literature in English','Secondary 2','Character Analysis',5),
('Singapore','Literature in English','Secondary 2','Essay Writing',6),

('Singapore','Literature in English','Secondary 3','Set Text: Poetry',1),
('Singapore','Literature in English','Secondary 3','Set Text: Prose',2),
('Singapore','Literature in English','Secondary 3','Set Text: Drama',3),
('Singapore','Literature in English','Secondary 3','Unseen Poetry',4),
('Singapore','Literature in English','Secondary 3','Unseen Prose',5),
('Singapore','Literature in English','Secondary 3','Essay Writing Skills',6),

('Singapore','Literature in English','Secondary 4','O-Level Set Text Revision',1),
('Singapore','Literature in English','Secondary 4','Unseen Poetry Practice',2),
('Singapore','Literature in English','Secondary 4','Unseen Prose Practice',3),
('Singapore','Literature in English','Secondary 4','Essay Writing Practice',4),
('Singapore','Literature in English','Secondary 4','Critical Analysis',5),
('Singapore','Literature in English','Secondary 4','Examination Preparation',6),

('Singapore','Literature in English','Secondary 5','N-Level Set Text Revision',1),
('Singapore','Literature in English','Secondary 5','Unseen Poetry Practice',2),
('Singapore','Literature in English','Secondary 5','Unseen Prose Practice',3),
('Singapore','Literature in English','Secondary 5','Essay Writing Practice',4),
('Singapore','Literature in English','Secondary 5','Critical Analysis',5),
('Singapore','Literature in English','Secondary 5','Examination Strategies',6),

('Singapore','Computing','Secondary 1','Introduction to Computational Thinking',1),
('Singapore','Computing','Secondary 1','Basic Programming Concepts',2),
('Singapore','Computing','Secondary 1','Data Representation',3),
('Singapore','Computing','Secondary 1','Simple Algorithms',4),
('Singapore','Computing','Secondary 1','Introduction to the Internet',5),
('Singapore','Computing','Secondary 1','Digital Citizenship',6),

('Singapore','Computing','Secondary 2','Programming: Sequence and Selection',1),
('Singapore','Computing','Secondary 2','Programming: Iteration',2),
('Singapore','Computing','Secondary 2','Data Structures Basics',3),
('Singapore','Computing','Secondary 2','Networks Basics',4),
('Singapore','Computing','Secondary 2','Databases Basics',5),
('Singapore','Computing','Secondary 2','Project Work',6),

('Singapore','Computing','Secondary 3','Algorithms and Flowcharts',1),
('Singapore','Computing','Secondary 3','Object-Oriented Programming Basics',2),
('Singapore','Computing','Secondary 3','Data Structures',3),
('Singapore','Computing','Secondary 3','Computer Systems',4),
('Singapore','Computing','Secondary 3','Networks and Security',5),
('Singapore','Computing','Secondary 3','Project Work',6),

('Singapore','Computing','Secondary 4','Programming Practice',1),
('Singapore','Computing','Secondary 4','Data Structures and Algorithms',2),
('Singapore','Computing','Secondary 4','Databases',3),
('Singapore','Computing','Secondary 4','Computer Systems and Networks',4),
('Singapore','Computing','Secondary 4','School-based Coursework',5),
('Singapore','Computing','Secondary 4','O-Level Examination Preparation',6),

('Singapore','Computing','Secondary 5','Programming Practice',1),
('Singapore','Computing','Secondary 5','Data Structures and Algorithms',2),
('Singapore','Computing','Secondary 5','Databases',3),
('Singapore','Computing','Secondary 5','Computer Systems and Networks',4),
('Singapore','Computing','Secondary 5','School-based Coursework',5),
('Singapore','Computing','Secondary 5','Examination Preparation',6),

('Singapore','Art','Secondary 1','Elements of Art',1),
('Singapore','Art','Secondary 1','Drawing Techniques',2),
('Singapore','Art','Secondary 1','Colour Theory',3),
('Singapore','Art','Secondary 1','Painting Techniques',4),
('Singapore','Art','Secondary 1','Art Appreciation',5),
('Singapore','Art','Secondary 1','Sketchbook Development',6),

('Singapore','Art','Secondary 2','Composition and Design',1),
('Singapore','Art','Secondary 2','Printmaking',2),
('Singapore','Art','Secondary 2','Sculpture Basics',3),
('Singapore','Art','Secondary 2','Mixed Media',4),
('Singapore','Art','Secondary 2','Art History Survey',5),
('Singapore','Art','Secondary 2','Sketchbook Development',6),

('Singapore','Art','Secondary 3','Observational Drawing',1),
('Singapore','Art','Secondary 3','Personal Art Themes',2),
('Singapore','Art','Secondary 3','Digital Art Basics',3),
('Singapore','Art','Secondary 3','Critical Studies',4),
('Singapore','Art','Secondary 3','Coursework Development',5),
('Singapore','Art','Secondary 3','Artist Research',6),

('Singapore','Art','Secondary 4','Coursework Refinement',1),
('Singapore','Art','Secondary 4','Exam Theme Development',2),
('Singapore','Art','Secondary 4','Critical Studies',3),
('Singapore','Art','Secondary 4','Portfolio Presentation',4),
('Singapore','Art','Secondary 4','Artist Research',5),
('Singapore','Art','Secondary 4','O-Level Examination Preparation',6),

('Singapore','Art','Secondary 5','Coursework Refinement',1),
('Singapore','Art','Secondary 5','Exam Theme Development',2),
('Singapore','Art','Secondary 5','Critical Studies',3),
('Singapore','Art','Secondary 5','Portfolio Presentation',4),
('Singapore','Art','Secondary 5','Artist Research',5),
('Singapore','Art','Secondary 5','Examination Preparation',6),

('Singapore','Music','Secondary 1','Music Theory Basics',1),
('Singapore','Music','Secondary 1','Rhythm and Notation',2),
('Singapore','Music','Secondary 1','Listening Skills',3),
('Singapore','Music','Secondary 1','Basic Performance Skills',4),
('Singapore','Music','Secondary 1','Composition Basics',5),
('Singapore','Music','Secondary 1','Music Appreciation',6),

('Singapore','Music','Secondary 2','Intermediate Music Theory',1),
('Singapore','Music','Secondary 2','Ensemble Performance',2),
('Singapore','Music','Secondary 2','Listening and Analysis',3),
('Singapore','Music','Secondary 2','Composition Techniques',4),
('Singapore','Music','Secondary 2','World Music Styles',5),
('Singapore','Music','Secondary 2','Music Appreciation',6),

('Singapore','Music','Secondary 3','Advanced Music Theory',1),
('Singapore','Music','Secondary 3','Performance Practice',2),
('Singapore','Music','Secondary 3','Score Analysis',3),
('Singapore','Music','Secondary 3','Composition Coursework',4),
('Singapore','Music','Secondary 3','Music History Survey',5),
('Singapore','Music','Secondary 3','Aural Skills',6),

('Singapore','Music','Secondary 4','Coursework Refinement',1),
('Singapore','Music','Secondary 4','Performance Assessment Preparation',2),
('Singapore','Music','Secondary 4','Score Analysis',3),
('Singapore','Music','Secondary 4','Composition Refinement',4),
('Singapore','Music','Secondary 4','Aural Skills',5),
('Singapore','Music','Secondary 4','O-Level Examination Preparation',6),

('Singapore','Music','Secondary 5','Coursework Refinement',1),
('Singapore','Music','Secondary 5','Performance Assessment Preparation',2),
('Singapore','Music','Secondary 5','Score Analysis',3),
('Singapore','Music','Secondary 5','Composition Refinement',4),
('Singapore','Music','Secondary 5','Aural Skills',5),
('Singapore','Music','Secondary 5','Examination Preparation',6),

('Singapore','Design & Technology','Secondary 1','Design Process Basics',1),
('Singapore','Design & Technology','Secondary 1','Sketching and Drawing',2),
('Singapore','Design & Technology','Secondary 1','Materials and Tools',3),
('Singapore','Design & Technology','Secondary 1','Simple Mechanisms',4),
('Singapore','Design & Technology','Secondary 1','Model Making',5),
('Singapore','Design & Technology','Secondary 1','Design Evaluation',6),

('Singapore','Design & Technology','Secondary 2','Design Briefs and Specifications',1),
('Singapore','Design & Technology','Secondary 2','Technical Drawing',2),
('Singapore','Design & Technology','Secondary 2','Materials Properties',3),
('Singapore','Design & Technology','Secondary 2','Structures and Mechanisms',4),
('Singapore','Design & Technology','Secondary 2','Prototyping',5),
('Singapore','Design & Technology','Secondary 2','Design Evaluation',6),

('Singapore','Design & Technology','Secondary 3','Design Thinking Process',1),
('Singapore','Design & Technology','Secondary 3','CAD Basics',2),
('Singapore','Design & Technology','Secondary 3','Materials Selection',3),
('Singapore','Design & Technology','Secondary 3','Systems and Control',4),
('Singapore','Design & Technology','Secondary 3','Coursework Development',5),
('Singapore','Design & Technology','Secondary 3','Design Evaluation',6),

('Singapore','Design & Technology','Secondary 4','Coursework Refinement',1),
('Singapore','Design & Technology','Secondary 4','Design Folio Development',2),
('Singapore','Design & Technology','Secondary 4','Prototyping and Testing',3),
('Singapore','Design & Technology','Secondary 4','Systems and Control',4),
('Singapore','Design & Technology','Secondary 4','Design Evaluation',5),
('Singapore','Design & Technology','Secondary 4','O-Level Examination Preparation',6),

('Singapore','Design & Technology','Secondary 5','Coursework Refinement',1),
('Singapore','Design & Technology','Secondary 5','Design Folio Development',2),
('Singapore','Design & Technology','Secondary 5','Prototyping and Testing',3),
('Singapore','Design & Technology','Secondary 5','Systems and Control',4),
('Singapore','Design & Technology','Secondary 5','Design Evaluation',5),
('Singapore','Design & Technology','Secondary 5','Examination Preparation',6),

('Singapore','Food & Nutrition','Secondary 1','Food Safety and Hygiene',1),
('Singapore','Food & Nutrition','Secondary 1','Basic Nutrition',2),
('Singapore','Food & Nutrition','Secondary 1','Kitchen Tools and Equipment',3),
('Singapore','Food & Nutrition','Secondary 1','Basic Cooking Methods',4),
('Singapore','Food & Nutrition','Secondary 1','Meal Planning Basics',5),
('Singapore','Food & Nutrition','Secondary 1','Food Presentation',6),

('Singapore','Food & Nutrition','Secondary 2','Nutrients and Diet',1),
('Singapore','Food & Nutrition','Secondary 2','Food Preparation Techniques',2),
('Singapore','Food & Nutrition','Secondary 2','Special Dietary Needs',3),
('Singapore','Food & Nutrition','Secondary 2','Recipe Modification',4),
('Singapore','Food & Nutrition','Secondary 2','Food Costing',5),
('Singapore','Food & Nutrition','Secondary 2','Food Presentation',6),

('Singapore','Food & Nutrition','Secondary 3','Nutrition Across the Lifespan',1),
('Singapore','Food & Nutrition','Secondary 3','Advanced Cooking Techniques',2),
('Singapore','Food & Nutrition','Secondary 3','Food Science Basics',3),
('Singapore','Food & Nutrition','Secondary 3','Menu Planning',4),
('Singapore','Food & Nutrition','Secondary 3','Coursework Development',5),
('Singapore','Food & Nutrition','Secondary 3','Food Presentation',6),

('Singapore','Food & Nutrition','Secondary 4','Coursework Refinement',1),
('Singapore','Food & Nutrition','Secondary 4','Practical Examination Preparation',2),
('Singapore','Food & Nutrition','Secondary 4','Menu Planning',3),
('Singapore','Food & Nutrition','Secondary 4','Food Science',4),
('Singapore','Food & Nutrition','Secondary 4','Written Paper Revision',5),
('Singapore','Food & Nutrition','Secondary 4','O-Level Examination Preparation',6),

('Singapore','Food & Nutrition','Secondary 5','Coursework Refinement',1),
('Singapore','Food & Nutrition','Secondary 5','Practical Examination Preparation',2),
('Singapore','Food & Nutrition','Secondary 5','Menu Planning',3),
('Singapore','Food & Nutrition','Secondary 5','Food Science',4),
('Singapore','Food & Nutrition','Secondary 5','Written Paper Revision',5),
('Singapore','Food & Nutrition','Secondary 5','Examination Preparation',6),

-- ============================================================
-- SINGAPORE N/O-LEVEL — Secondary 5 fill for existing core subjects
-- ============================================================
('Singapore','English Language','Secondary 5','N-Level Comprehension',1),
('Singapore','English Language','Secondary 5','N-Level Composition',2),
('Singapore','English Language','Secondary 5','Summary Writing',3),
('Singapore','English Language','Secondary 5','Grammar and Editing',4),
('Singapore','English Language','Secondary 5','Oral: Reading Aloud and Conversation',5),
('Singapore','English Language','Secondary 5','Examination Strategies',6),

('Singapore','Mathematics','Secondary 5','Numbers and Algebra Revision',1),
('Singapore','Mathematics','Secondary 5','Geometry and Mensuration Revision',2),
('Singapore','Mathematics','Secondary 5','Statistics Revision',3),
('Singapore','Mathematics','Secondary 5','Graphs and Functions',4),
('Singapore','Mathematics','Secondary 5','Problem Solving Practice',5),
('Singapore','Mathematics','Secondary 5','N-Level Examination Preparation',6),

('Singapore','Additional Mathematics','Secondary 5','Algebra Revision',1),
('Singapore','Additional Mathematics','Secondary 5','Trigonometry Revision',2),
('Singapore','Additional Mathematics','Secondary 5','Calculus Revision',3),
('Singapore','Additional Mathematics','Secondary 5','Coordinate Geometry Revision',4),
('Singapore','Additional Mathematics','Secondary 5','Problem Solving Practice',5),
('Singapore','Additional Mathematics','Secondary 5','Examination Preparation',6),

('Singapore','Physics','Secondary 5','Mechanics Revision',1),
('Singapore','Physics','Secondary 5','Thermal Physics Revision',2),
('Singapore','Physics','Secondary 5','Waves and Optics Revision',3),
('Singapore','Physics','Secondary 5','Electricity and Magnetism Revision',4),
('Singapore','Physics','Secondary 5','Practical Skills Revision',5),
('Singapore','Physics','Secondary 5','N-Level Examination Preparation',6),

('Singapore','Chemistry','Secondary 5','Particulate Nature of Matter Revision',1),
('Singapore','Chemistry','Secondary 5','Acids Bases and Salts Revision',2),
('Singapore','Chemistry','Secondary 5','The Periodic Table Revision',3),
('Singapore','Chemistry','Secondary 5','Organic Chemistry Revision',4),
('Singapore','Chemistry','Secondary 5','Practical Skills Revision',5),
('Singapore','Chemistry','Secondary 5','N-Level Examination Preparation',6),

('Singapore','Biology','Secondary 5','Cells and Nutrition Revision',1),
('Singapore','Biology','Secondary 5','Transport and Respiration Revision',2),
('Singapore','Biology','Secondary 5','Homeostasis and Reproduction Revision',3),
('Singapore','Biology','Secondary 5','Genetics and Evolution Revision',4),
('Singapore','Biology','Secondary 5','Practical Skills Revision',5),
('Singapore','Biology','Secondary 5','N-Level Examination Preparation',6),

('Singapore','History','Secondary 5','Source-based Skills Revision',1),
('Singapore','History','Secondary 5','Essay Writing Practice',2),
('Singapore','History','Secondary 5','Case Study Revision',3),
('Singapore','History','Secondary 5','Timeline and Key Events Review',4),
('Singapore','History','Secondary 5','Practice Questions',5),
('Singapore','History','Secondary 5','Examination Strategies',6),

('Singapore','Geography','Secondary 5','Physical Geography Revision',1),
('Singapore','Geography','Secondary 5','Human Geography Revision',2),
('Singapore','Geography','Secondary 5','Skills Paper Practice',3),
('Singapore','Geography','Secondary 5','Case Study Revision',4),
('Singapore','Geography','Secondary 5','Fieldwork Skills',5),
('Singapore','Geography','Secondary 5','Examination Strategies',6),

('Singapore','Principles of Accounts','Secondary 5','Bookkeeping Revision',1),
('Singapore','Principles of Accounts','Secondary 5','Financial Statements Revision',2),
('Singapore','Principles of Accounts','Secondary 5','Adjustments Revision',3),
('Singapore','Principles of Accounts','Secondary 5','Ratio Analysis',4),
('Singapore','Principles of Accounts','Secondary 5','Practice Questions',5),
('Singapore','Principles of Accounts','Secondary 5','Examination Preparation',6),

-- ============================================================
-- SINGAPORE A-LEVEL — remaining JC electives
-- ============================================================
('Singapore','H2 Further Mathematics','JC1','Complex Numbers',1),
('Singapore','H2 Further Mathematics','JC1','Matrices and Linear Spaces',2),
('Singapore','H2 Further Mathematics','JC1','Further Calculus',3),
('Singapore','H2 Further Mathematics','JC1','Graphing Techniques',4),
('Singapore','H2 Further Mathematics','JC1','Further Vectors',5),
('Singapore','H2 Further Mathematics','JC1','Mathematical Induction',6),

('Singapore','H2 Further Mathematics','JC2','Further Differential Equations',1),
('Singapore','H2 Further Mathematics','JC2','Numerical Methods',2),
('Singapore','H2 Further Mathematics','JC2','Further Probability',3),
('Singapore','H2 Further Mathematics','JC2','Further Statistics',4),
('Singapore','H2 Further Mathematics','JC2','Group Theory Basics',5),
('Singapore','H2 Further Mathematics','JC2','A-Level Examination Preparation',6),

('Singapore','H2 History','JC1','Historiography and Source Skills',1),
('Singapore','H2 History','JC1','The Cold War',2),
('Singapore','H2 History','JC1','Emergence of a Bipolar World',3),
('Singapore','H2 History','JC1','Search for Peace and Security',4),
('Singapore','H2 History','JC1','Essay Writing Skills',5),
('Singapore','H2 History','JC1','Document-based Question Skills',6),

('Singapore','H2 History','JC2','Regional Conflicts and Cooperation',1),
('Singapore','H2 History','JC2','Development of Southeast Asia',2),
('Singapore','H2 History','JC2','A-Level Essay Practice',3),
('Singapore','H2 History','JC2','A-Level DBQ Practice',4),
('Singapore','H2 History','JC2','Case Studies',5),
('Singapore','H2 History','JC2','Examination Strategies',6),

('Singapore','H2 Geography','JC1','Tropical Environments',1),
('Singapore','H2 Geography','JC1','Climatic Change',2),
('Singapore','H2 Geography','JC1','Geographical Skills',3),
('Singapore','H2 Geography','JC1','Fieldwork Techniques',4),
('Singapore','H2 Geography','JC1','Case Studies',5),
('Singapore','H2 Geography','JC1','Essay Writing Skills',6),

('Singapore','H2 Geography','JC2','Development Geography',1),
('Singapore','H2 Geography','JC2','Global Interactions',2),
('Singapore','H2 Geography','JC2','A-Level Essay Practice',3),
('Singapore','H2 Geography','JC2','A-Level Data Response Practice',4),
('Singapore','H2 Geography','JC2','Case Studies',5),
('Singapore','H2 Geography','JC2','Examination Strategies',6),

('Singapore','H2 Literature in English','JC1','Poetry Analysis',1),
('Singapore','H2 Literature in English','JC1','Prose Analysis',2),
('Singapore','H2 Literature in English','JC1','Drama Analysis',3),
('Singapore','H2 Literature in English','JC1','Literary Criticism Basics',4),
('Singapore','H2 Literature in English','JC1','Set Text Study',5),
('Singapore','H2 Literature in English','JC1','Essay Writing Skills',6),

('Singapore','H2 Literature in English','JC2','Comparative Analysis',1),
('Singapore','H2 Literature in English','JC2','Unseen Poetry Practice',2),
('Singapore','H2 Literature in English','JC2','Unseen Prose Practice',3),
('Singapore','H2 Literature in English','JC2','A-Level Essay Practice',4),
('Singapore','H2 Literature in English','JC2','Set Text Revision',5),
('Singapore','H2 Literature in English','JC2','Examination Strategies',6),

('Singapore','H1 Project Work','JC1','Group Project Skills',1),
('Singapore','H1 Project Work','JC1','Research and Planning',2),
('Singapore','H1 Project Work','JC1','Written Report Skills',3),
('Singapore','H1 Project Work','JC1','Oral Presentation Skills',4),
('Singapore','H1 Project Work','JC1','Group Interaction Skills',5),
('Singapore','H1 Project Work','JC1','Assessment Criteria Overview',6),

('Singapore','H1 Project Work','JC2','Report Refinement',1),
('Singapore','H1 Project Work','JC2','Presentation Refinement',2),
('Singapore','H1 Project Work','JC2','Group Interaction Practice',3),
('Singapore','H1 Project Work','JC2','Q&A Preparation',4),
('Singapore','H1 Project Work','JC2','Mock Assessment Practice',5),
('Singapore','H1 Project Work','JC2','Examination Preparation',6),

-- ============================================================
-- MALAYSIA UPSR — History elective, Primary 1-6
-- ============================================================
('Malaysia','History','Primary 1','My Family History',1),
('Malaysia','History','Primary 1','My Community',2),
('Malaysia','History','Primary 1','Important Places Nearby',3),
('Malaysia','History','Primary 1','Simple Timelines',4),
('Malaysia','History','Primary 1','National Symbols',5),
('Malaysia','History','Primary 1','Stories from the Past',6),

('Malaysia','History','Primary 2','Our Local Heritage',1),
('Malaysia','History','Primary 2','Early Settlements',2),
('Malaysia','History','Primary 2','National Symbols and Days',3),
('Malaysia','History','Primary 2','Simple Timelines',4),
('Malaysia','History','Primary 2','Community Leaders',5),
('Malaysia','History','Primary 2','Stories from the Past',6),

('Malaysia','History','Primary 3','Early Malay Kingdoms',1),
('Malaysia','History','Primary 3','Trade and Early Contacts',2),
('Malaysia','History','Primary 3','Local Historical Figures',3),
('Malaysia','History','Primary 3','Historical Sources',4),
('Malaysia','History','Primary 3','National Identity',5),
('Malaysia','History','Primary 3','Timeline Skills',6),

('Malaysia','History','Primary 4','Malacca Sultanate',1),
('Malaysia','History','Primary 4','Colonial Arrival',2),
('Malaysia','History','Primary 4','Historical Figures',3),
('Malaysia','History','Primary 4','Historical Sources',4),
('Malaysia','History','Primary 4','National Identity',5),
('Malaysia','History','Primary 4','Timeline Skills',6),

('Malaysia','History','Primary 5','Colonisation of Malaya',1),
('Malaysia','History','Primary 5','Japanese Occupation Overview',2),
('Malaysia','History','Primary 5','Path to Independence',3),
('Malaysia','History','Primary 5','Historical Sources',4),
('Malaysia','History','Primary 5','National Identity',5),
('Malaysia','History','Primary 5','UPSR Preparation',6),

('Malaysia','History','Primary 6','Formation of Malaysia',1),
('Malaysia','History','Primary 6','Nation Building',2),
('Malaysia','History','Primary 6','Key Historical Figures',3),
('Malaysia','History','Primary 6','Historical Sources',4),
('Malaysia','History','Primary 6','National Identity',5),
('Malaysia','History','Primary 6','UPSR Examination Preparation',6),

-- ============================================================
-- MALAYSIA UPSR — Science gap fill (Primary 1-2; P3-6 already seeded)
-- ============================================================
('Malaysia','Science','Primary 1','Observing My Surroundings',1),
('Malaysia','Science','Primary 1','Living and Non-living Things',2),
('Malaysia','Science','Primary 1','My Five Senses',3),
('Malaysia','Science','Primary 1','Plants Around Us',4),
('Malaysia','Science','Primary 1','Animals Around Us',5),
('Malaysia','Science','Primary 1','Simple Science Tools',6),

('Malaysia','Science','Primary 2','Plant and Animal Needs',1),
('Malaysia','Science','Primary 2','Materials and their Uses',2),
('Malaysia','Science','Primary 2','Push and Pull',3),
('Malaysia','Science','Primary 2','Light and Shadows',4),
('Malaysia','Science','Primary 2','Simple Measurement',5),
('Malaysia','Science','Primary 2','Caring for the Environment',6),

-- ============================================================
-- MALAYSIA PT3 — remaining electives, Form 1-3
-- ============================================================
('Malaysia','Islamic Education','Form 1','Aqidah Basics',1),
('Malaysia','Islamic Education','Form 1','Ibadah Fundamentals',2),
('Malaysia','Islamic Education','Form 1','Seerah of the Prophet',3),
('Malaysia','Islamic Education','Form 1','Akhlak and Adab',4),
('Malaysia','Islamic Education','Form 1','Al-Quran Recitation',5),
('Malaysia','Islamic Education','Form 1','Basic Fiqh',6),

('Malaysia','Islamic Education','Form 2','Aqidah: Pillars of Faith',1),
('Malaysia','Islamic Education','Form 2','Ibadah: Fasting and Zakat',2),
('Malaysia','Islamic Education','Form 2','Seerah: Madinah Period',3),
('Malaysia','Islamic Education','Form 2','Akhlak in Daily Life',4),
('Malaysia','Islamic Education','Form 2','Al-Quran Tajwid',5),
('Malaysia','Islamic Education','Form 2','Fiqh: Muamalat Basics',6),

('Malaysia','Islamic Education','Form 3','Aqidah: Advanced Topics',1),
('Malaysia','Islamic Education','Form 3','Ibadah: Hajj',2),
('Malaysia','Islamic Education','Form 3','Islamic History Overview',3),
('Malaysia','Islamic Education','Form 3','Akhlak and Character Building',4),
('Malaysia','Islamic Education','Form 3','Al-Quran and Hadith Study',5),
('Malaysia','Islamic Education','Form 3','PT3 Preparation',6),

('Malaysia','Moral Education','Form 1','Core Moral Values',1),
('Malaysia','Moral Education','Form 1','Self and Family Values',2),
('Malaysia','Moral Education','Form 1','Values in the Community',3),
('Malaysia','Moral Education','Form 1','Case Study Discussions',4),
('Malaysia','Moral Education','Form 1','Values and the Environment',5),
('Malaysia','Moral Education','Form 1','Reflective Writing',6),

('Malaysia','Moral Education','Form 2','Values in Society',1),
('Malaysia','Moral Education','Form 2','National Values',2),
('Malaysia','Moral Education','Form 2','Values and Human Rights',3),
('Malaysia','Moral Education','Form 2','Case Study Discussions',4),
('Malaysia','Moral Education','Form 2','Values and Technology',5),
('Malaysia','Moral Education','Form 2','Reflective Writing',6),

('Malaysia','Moral Education','Form 3','Values and Global Citizenship',1),
('Malaysia','Moral Education','Form 3','Values in Governance',2),
('Malaysia','Moral Education','Form 3','Case Study Discussions',3),
('Malaysia','Moral Education','Form 3','Values and Sustainability',4),
('Malaysia','Moral Education','Form 3','Ethical Decision Making',5),
('Malaysia','Moral Education','Form 3','PT3 Preparation',6),

('Malaysia','Design & Technology','Form 1','Design Process Basics',1),
('Malaysia','Design & Technology','Form 1','Sketching and Drawing',2),
('Malaysia','Design & Technology','Form 1','Materials and Tools',3),
('Malaysia','Design & Technology','Form 1','Simple Mechanisms',4),
('Malaysia','Design & Technology','Form 1','Model Making',5),
('Malaysia','Design & Technology','Form 1','Design Evaluation',6),

('Malaysia','Design & Technology','Form 2','Design Briefs and Specifications',1),
('Malaysia','Design & Technology','Form 2','Technical Drawing',2),
('Malaysia','Design & Technology','Form 2','Materials Properties',3),
('Malaysia','Design & Technology','Form 2','Structures and Mechanisms',4),
('Malaysia','Design & Technology','Form 2','Prototyping',5),
('Malaysia','Design & Technology','Form 2','Design Evaluation',6),

('Malaysia','Design & Technology','Form 3','Design Thinking Process',1),
('Malaysia','Design & Technology','Form 3','Materials Selection',2),
('Malaysia','Design & Technology','Form 3','Systems and Control',3),
('Malaysia','Design & Technology','Form 3','Coursework Development',4),
('Malaysia','Design & Technology','Form 3','Design Evaluation',5),
('Malaysia','Design & Technology','Form 3','PT3 Preparation',6),

-- ============================================================
-- MALAYSIA SPM — remaining electives, Form 4-5
-- ============================================================
('Malaysia','Commerce','Form 4','Introduction to Commerce',1),
('Malaysia','Commerce','Form 4','Business Organisations',2),
('Malaysia','Commerce','Form 4','Banking Services',3),
('Malaysia','Commerce','Form 4','Insurance Basics',4),
('Malaysia','Commerce','Form 4','Trade and Transport',5),
('Malaysia','Commerce','Form 4','Consumer Protection',6),

('Malaysia','Commerce','Form 5','International Trade',1),
('Malaysia','Commerce','Form 5','E-Commerce',2),
('Malaysia','Commerce','Form 5','Marketing Basics',3),
('Malaysia','Commerce','Form 5','Warehousing and Distribution',4),
('Malaysia','Commerce','Form 5','Practice Questions',5),
('Malaysia','Commerce','Form 5','SPM Examination Preparation',6),

('Malaysia','Business Studies','Form 4','Introduction to Business',1),
('Malaysia','Business Studies','Form 4','Forms of Business Ownership',2),
('Malaysia','Business Studies','Form 4','Business Planning',3),
('Malaysia','Business Studies','Form 4','Marketing Concepts',4),
('Malaysia','Business Studies','Form 4','Human Resource Basics',5),
('Malaysia','Business Studies','Form 4','Business Ethics',6),

('Malaysia','Business Studies','Form 5','Operations Management',1),
('Malaysia','Business Studies','Form 5','Financial Management Basics',2),
('Malaysia','Business Studies','Form 5','Entrepreneurship',3),
('Malaysia','Business Studies','Form 5','Business Growth Strategies',4),
('Malaysia','Business Studies','Form 5','Practice Questions',5),
('Malaysia','Business Studies','Form 5','SPM Examination Preparation',6),

('Malaysia','Information Technology','Form 4','Computer Systems Basics',1),
('Malaysia','Information Technology','Form 4','Programming Fundamentals',2),
('Malaysia','Information Technology','Form 4','Data Representation',3),
('Malaysia','Information Technology','Form 4','Networks Basics',4),
('Malaysia','Information Technology','Form 4','Databases Basics',5),
('Malaysia','Information Technology','Form 4','ICT and Society',6),

('Malaysia','Information Technology','Form 5','Programming Practice',1),
('Malaysia','Information Technology','Form 5','Data Structures',2),
('Malaysia','Information Technology','Form 5','Networks and Security',3),
('Malaysia','Information Technology','Form 5','Databases',4),
('Malaysia','Information Technology','Form 5','Project Work',5),
('Malaysia','Information Technology','Form 5','SPM Examination Preparation',6),

('Malaysia','Moral Education','Form 4','Values and Nationhood',1),
('Malaysia','Moral Education','Form 4','Values and Ethics',2),
('Malaysia','Moral Education','Form 4','Case Study Discussions',3),
('Malaysia','Moral Education','Form 4','Values and Global Issues',4),
('Malaysia','Moral Education','Form 4','Reflective Writing',5),
('Malaysia','Moral Education','Form 4','Essay Writing Skills',6),

('Malaysia','Moral Education','Form 5','Values and Leadership',1),
('Malaysia','Moral Education','Form 5','Values and Sustainability',2),
('Malaysia','Moral Education','Form 5','Case Study Discussions',3),
('Malaysia','Moral Education','Form 5','Ethical Decision Making',4),
('Malaysia','Moral Education','Form 5','Practice Questions',5),
('Malaysia','Moral Education','Form 5','SPM Examination Preparation',6),

('Malaysia','Islamic Studies','Form 4','Aqidah: Advanced Topics',1),
('Malaysia','Islamic Studies','Form 4','Fiqh: Muamalat',2),
('Malaysia','Islamic Studies','Form 4','Seerah and Islamic History',3),
('Malaysia','Islamic Studies','Form 4','Al-Quran and Tafsir',4),
('Malaysia','Islamic Studies','Form 4','Hadith Studies',5),
('Malaysia','Islamic Studies','Form 4','Akhlak and Character',6),

('Malaysia','Islamic Studies','Form 5','Fiqh: Munakahat',1),
('Malaysia','Islamic Studies','Form 5','Islamic Civilisation',2),
('Malaysia','Islamic Studies','Form 5','Al-Quran and Tafsir',3),
('Malaysia','Islamic Studies','Form 5','Hadith Studies',4),
('Malaysia','Islamic Studies','Form 5','Practice Questions',5),
('Malaysia','Islamic Studies','Form 5','SPM Examination Preparation',6),

('Malaysia','Art & Design','Form 4','Elements and Principles of Design',1),
('Malaysia','Art & Design','Form 4','Drawing and Illustration',2),
('Malaysia','Art & Design','Form 4','Colour and Composition',3),
('Malaysia','Art & Design','Form 4','Traditional Malaysian Art Forms',4),
('Malaysia','Art & Design','Form 4','Coursework Development',5),
('Malaysia','Art & Design','Form 4','Critique and Appreciation',6),

('Malaysia','Art & Design','Form 5','Coursework Refinement',1),
('Malaysia','Art & Design','Form 5','Portfolio Development',2),
('Malaysia','Art & Design','Form 5','Critical Studies',3),
('Malaysia','Art & Design','Form 5','Artist Research',4),
('Malaysia','Art & Design','Form 5','Practice Questions',5),
('Malaysia','Art & Design','Form 5','SPM Examination Preparation',6),

-- ============================================================
-- MALAYSIA STPM — remaining electives, Lower Six/Upper Six
-- ============================================================
('Malaysia','Further Mathematics (T)','Lower Six','Complex Numbers',1),
('Malaysia','Further Mathematics (T)','Lower Six','Matrices and Linear Spaces',2),
('Malaysia','Further Mathematics (T)','Lower Six','Further Calculus',3),
('Malaysia','Further Mathematics (T)','Lower Six','Graphing Techniques',4),
('Malaysia','Further Mathematics (T)','Lower Six','Further Vectors',5),
('Malaysia','Further Mathematics (T)','Lower Six','Mathematical Induction',6),

('Malaysia','Further Mathematics (T)','Upper Six','Further Differential Equations',1),
('Malaysia','Further Mathematics (T)','Upper Six','Numerical Methods',2),
('Malaysia','Further Mathematics (T)','Upper Six','Further Probability',3),
('Malaysia','Further Mathematics (T)','Upper Six','Further Statistics',4),
('Malaysia','Further Mathematics (T)','Upper Six','Group Theory Basics',5),
('Malaysia','Further Mathematics (T)','Upper Six','STPM Examination Preparation',6),

('Malaysia','History','Lower Six','Historiography and Source Skills',1),
('Malaysia','History','Lower Six','Nationalism in Southeast Asia',2),
('Malaysia','History','Lower Six','Colonialism and its Impact',3),
('Malaysia','History','Lower Six','Malaysia''s Path to Independence',4),
('Malaysia','History','Lower Six','Essay Writing Skills',5),
('Malaysia','History','Lower Six','Document-based Question Skills',6),

('Malaysia','History','Upper Six','Regional Cooperation in ASEAN',1),
('Malaysia','History','Upper Six','Malaysia in the Global Community',2),
('Malaysia','History','Upper Six','STPM Essay Practice',3),
('Malaysia','History','Upper Six','STPM DBQ Practice',4),
('Malaysia','History','Upper Six','Case Studies',5),
('Malaysia','History','Upper Six','Examination Strategies',6),

('Malaysia','Geography','Lower Six','Physical Geography Fundamentals',1),
('Malaysia','Geography','Lower Six','Climatic Systems',2),
('Malaysia','Geography','Lower Six','Geographical Skills',3),
('Malaysia','Geography','Lower Six','Fieldwork Techniques',4),
('Malaysia','Geography','Lower Six','Case Studies',5),
('Malaysia','Geography','Lower Six','Essay Writing Skills',6),

('Malaysia','Geography','Upper Six','Development Geography',1),
('Malaysia','Geography','Upper Six','Global Interactions',2),
('Malaysia','Geography','Upper Six','STPM Essay Practice',3),
('Malaysia','Geography','Upper Six','STPM Data Response Practice',4),
('Malaysia','Geography','Upper Six','Case Studies',5),
('Malaysia','Geography','Upper Six','Examination Strategies',6),

('Malaysia','Literature in English','Lower Six','Poetry Analysis',1),
('Malaysia','Literature in English','Lower Six','Prose Analysis',2),
('Malaysia','Literature in English','Lower Six','Drama Analysis',3),
('Malaysia','Literature in English','Lower Six','Literary Criticism Basics',4),
('Malaysia','Literature in English','Lower Six','Set Text Study',5),
('Malaysia','Literature in English','Lower Six','Essay Writing Skills',6),

('Malaysia','Literature in English','Upper Six','Comparative Analysis',1),
('Malaysia','Literature in English','Upper Six','Unseen Poetry Practice',2),
('Malaysia','Literature in English','Upper Six','Unseen Prose Practice',3),
('Malaysia','Literature in English','Upper Six','STPM Essay Practice',4),
('Malaysia','Literature in English','Upper Six','Set Text Revision',5),
('Malaysia','Literature in English','Upper Six','Examination Strategies',6),

('Malaysia','Accounting','Lower Six','Introduction to Accounting Principles',1),
('Malaysia','Accounting','Lower Six','Double Entry Bookkeeping',2),
('Malaysia','Accounting','Lower Six','Trial Balance and Adjustments',3),
('Malaysia','Accounting','Lower Six','Financial Statements',4),
('Malaysia','Accounting','Lower Six','Partnership Accounts',5),
('Malaysia','Accounting','Lower Six','Bank Reconciliation',6),

('Malaysia','Accounting','Upper Six','Company Accounts',1),
('Malaysia','Accounting','Upper Six','Ratio Analysis',2),
('Malaysia','Accounting','Upper Six','Cash Flow Statements',3),
('Malaysia','Accounting','Upper Six','Cost Accounting Basics',4),
('Malaysia','Accounting','Upper Six','Practice Questions',5),
('Malaysia','Accounting','Upper Six','STPM Examination Preparation',6),

('Malaysia','Business Studies','Lower Six','Introduction to Business Studies',1),
('Malaysia','Business Studies','Lower Six','Forms of Business Ownership',2),
('Malaysia','Business Studies','Lower Six','Business Planning',3),
('Malaysia','Business Studies','Lower Six','Marketing Concepts',4),
('Malaysia','Business Studies','Lower Six','Human Resource Management Basics',5),
('Malaysia','Business Studies','Lower Six','Business Ethics',6),

('Malaysia','Business Studies','Upper Six','Operations Management',1),
('Malaysia','Business Studies','Upper Six','Financial Management',2),
('Malaysia','Business Studies','Upper Six','Entrepreneurship',3),
('Malaysia','Business Studies','Upper Six','Business Growth Strategies',4),
('Malaysia','Business Studies','Upper Six','Practice Questions',5),
('Malaysia','Business Studies','Upper Six','STPM Examination Preparation',6);
"); } catch { }
    await DbSeeder.SeedAsync(context);
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "LearnSphere API v1");
    c.RoutePrefix = string.Empty; // Swagger at root: http://localhost:5000/
});

app.UseCors("AllowFrontend");

var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(wwwrootPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(wwwrootPath),
    RequestPath = ""
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
