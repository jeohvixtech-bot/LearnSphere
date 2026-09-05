using LearnSphere.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Tutor> Tutors { get; set; }
    public DbSet<TutorSubject> TutorSubjects { get; set; }
    public DbSet<TutorLevel> TutorLevels { get; set; }
    public DbSet<TutorMode> TutorModes { get; set; }
    public DbSet<TutorQualification> TutorQualifications { get; set; }
    public DbSet<ClassRemark> ClassRemarks { get; set; }
    public DbSet<ClassRemarkLike> ClassRemarkLikes { get; set; }
    public DbSet<TutorTimeSlot> TutorTimeSlots { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<StudentPreferredMode> StudentPreferredModes { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<BookingClass> BookingClasses { get; set; }
    public DbSet<BookingPresetSlot> BookingPresetSlots { get; set; }
    public DbSet<CounterProposal> CounterProposals { get; set; }
    public DbSet<CounterProposalClass> CounterProposalClasses { get; set; }
    public DbSet<LessonReport> LessonReports { get; set; }
    public DbSet<StudentTutorFirstClass> StudentTutorFirstClasses { get; set; }
    public DbSet<IssueReport> IssueReports { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<Payout> Payouts { get; set; }
    public DbSet<Institution> Institutions { get; set; }
    public DbSet<TutorOffering> TutorOfferings { get; set; }
    public DbSet<TutorDocument> TutorDocuments { get; set; }
    public DbSet<FavoriteTutor> FavoriteTutors { get; set; }
    public DbSet<ScoringWeightage> ScoringWeightages { get; set; }
    public DbSet<PresetCancellationDecision> PresetCancellationDecisions { get; set; }
    public DbSet<TutorPenalty> TutorPenalties { get; set; }
    public DbSet<SyllabusTopic> SyllabusTopics { get; set; }
    public DbSet<PresetGroupSyllabus> PresetGroupSyllabuses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tutor>()
            .HasOne(t => t.User)
            .WithOne(u => u.TutorProfile)
            .HasForeignKey<Tutor>(t => t.UserId);

        modelBuilder.Entity<TutorDocument>()
            .HasOne(d => d.Tutor)
            .WithMany(t => t.Documents)
            .HasForeignKey(d => d.TutorId);

        modelBuilder.Entity<Student>()
            .HasOne(s => s.ParentUser)
            .WithMany(u => u.Students)
            .HasForeignKey(s => s.ParentUserId);

        modelBuilder.Entity<StudentPreferredMode>()
            .HasOne(m => m.Student)
            .WithMany(s => s.PreferredModes)
            .HasForeignKey(m => m.StudentId);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Tutor)
            .WithMany(t => t.Bookings)
            .HasForeignKey(b => b.TutorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Student)
            .WithMany(s => s.Bookings)
            .HasForeignKey(b => b.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.PresetSlot)
            .WithMany()
            .HasForeignKey(b => b.PresetSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BookingClass>()
            .HasOne(bc => bc.Booking)
            .WithMany(b => b.Classes)
            .HasForeignKey(bc => bc.BookingId);

        modelBuilder.Entity<BookingPresetSlot>()
            .HasOne(bps => bps.TutorTimeSlot)
            .WithMany()
            .HasForeignKey(bps => bps.TutorTimeSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CounterProposal>()
            .HasOne(cp => cp.Booking)
            .WithMany(b => b.CounterProposals)
            .HasForeignKey(cp => cp.BookingId);

        modelBuilder.Entity<CounterProposalClass>()
            .HasOne(c => c.CounterProposal)
            .WithMany(cp => cp.Classes)
            .HasForeignKey(c => c.CounterProposalId);

        modelBuilder.Entity<LessonReport>(entity =>
        {
            // One report per student per session date per booking
            entity.HasIndex(e => new
            {
                e.BookingId, e.StudentId, e.SessionDate
            }).IsUnique().HasDatabaseName("UQ_LessonReport_BookingStudentDate");

            entity.HasOne(e => e.Booking)
                .WithMany(b => b.LessonReports)
                .HasForeignKey(e => e.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StudentTutorFirstClass>(entity =>
        {
            entity.HasIndex(e => new
            {
                e.Country, e.Subject, e.Level, e.TutorId, e.StudentId
            }).IsUnique().HasDatabaseName("UQ_StudentTutorFirstClass");

            entity.HasOne(e => e.Tutor)
                .WithMany()
                .HasForeignKey(e => e.TutorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Student)
                .WithMany()
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Booking)
                .WithMany()
                .HasForeignKey(e => e.BookingId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<IssueReport>()
            .HasOne(ir => ir.Booking)
            .WithOne(b => b.IssueReport)
            .HasForeignKey<IssueReport>(ir => ir.BookingId);

        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.Booking)
            .WithOne(b => b.Invoice)
            .HasForeignKey<Invoice>(i => i.BookingId);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId);

        modelBuilder.Entity<Payout>()
            .HasOne(p => p.Tutor)
            .WithMany(t => t.Payouts)
            .HasForeignKey(p => p.TutorId);

        modelBuilder.Entity<Tutor>()
            .Property(t => t.PricePerSession)
            .HasPrecision(10, 2);

        modelBuilder.Entity<TutorSubject>()
            .Property(s => s.Price)
            .HasPrecision(10, 2);

        modelBuilder.Entity<TutorOffering>()
            .Property(o => o.Price)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Invoice>()
            .Property(i => i.Amount)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Booking>()
            .Property(b => b.TotalPrice)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Payout>()
            .Property(p => p.Amount)
            .HasPrecision(10, 2);

        modelBuilder.Entity<TutorTimeSlot>()
            .Property(s => s.PricePerLesson)
            .HasPrecision(10, 2);

        modelBuilder.Entity<ClassRemark>(entity =>
        {
            // One remark per class instance.
            entity.HasIndex(e => e.BookingClassId).IsUnique().HasDatabaseName("UQ_ClassRemark_BookingClass");

            entity.HasOne(e => e.BookingClass)
                .WithMany()
                .HasForeignKey(e => e.BookingClassId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Tutor)
                .WithMany()
                .HasForeignKey(e => e.TutorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClassRemarkLike>(entity =>
        {
            // One like per parent per remark — no unlike, so this is a pure insert guard.
            entity.HasIndex(e => new { e.ClassRemarkId, e.ParentUserId })
                .IsUnique().HasDatabaseName("UQ_ClassRemarkLike_RemarkParent");

            entity.HasOne(e => e.ClassRemark)
                .WithMany(r => r.Likes)
                .HasForeignKey(e => e.ClassRemarkId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SyllabusTopic>(entity =>
        {
            entity.HasIndex(e => new { e.Country, e.Subject, e.Level, e.Topic })
                  .IsUnique()
                  .HasDatabaseName("UQ_SyllabusTopic");
        });

        modelBuilder.Entity<PresetGroupSyllabus>(entity =>
        {
            entity.HasIndex(e => new { e.PresetGroupId, e.SyllabusTopicId })
                  .IsUnique()
                  .HasDatabaseName("UQ_PresetGroupSyllabus");

            entity.HasOne(e => e.SyllabusTopic)
                  .WithMany()
                  .HasForeignKey(e => e.SyllabusTopicId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
