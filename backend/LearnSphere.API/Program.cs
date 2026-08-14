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
    // (not_submitted | pending | approved | rejected); OfferingsUnlocked gates the
    // offering builder until every mandatory document is approved — see
    // TutorsController.ReviewDocument / AddOffering guard in tutor.controller.js.
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Tutors` ADD COLUMN `VerificationStatus` VARCHAR(20) NOT NULL DEFAULT 'not_submitted'"); } catch { }
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `Tutors` ADD COLUMN `OfferingsUnlocked` TINYINT(1) NOT NULL DEFAULT 0"); } catch { }
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

    // Chat read/unread state — see ChatMessage.IsRead / ChatController.GetMessages.
    try { await context.Database.ExecuteSqlRawAsync(
        "ALTER TABLE `ChatMessages` ADD COLUMN `IsRead` TINYINT(1) NOT NULL DEFAULT 0"); } catch { }

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
