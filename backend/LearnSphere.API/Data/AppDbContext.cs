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
    public DbSet<TutorReview> TutorReviews { get; set; }
    public DbSet<TutorTimeSlot> TutorTimeSlots { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<BookingClass> BookingClasses { get; set; }
    public DbSet<CounterProposal> CounterProposals { get; set; }
    public DbSet<CounterProposalClass> CounterProposalClasses { get; set; }
    public DbSet<LessonReport> LessonReports { get; set; }
    public DbSet<LessonReportEdit> LessonReportEdits { get; set; }
    public DbSet<IssueReport> IssueReports { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<Payout> Payouts { get; set; }
    public DbSet<Institution> Institutions { get; set; }
    public DbSet<TutorOffering> TutorOfferings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tutor>()
            .HasOne(t => t.User)
            .WithOne(u => u.TutorProfile)
            .HasForeignKey<Tutor>(t => t.UserId);

        modelBuilder.Entity<Student>()
            .HasOne(s => s.ParentUser)
            .WithMany(u => u.Students)
            .HasForeignKey(s => s.ParentUserId);

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

        modelBuilder.Entity<BookingClass>()
            .HasOne(bc => bc.Booking)
            .WithMany(b => b.Classes)
            .HasForeignKey(bc => bc.BookingId);

        modelBuilder.Entity<CounterProposal>()
            .HasOne(cp => cp.Booking)
            .WithOne(b => b.CounterProposal)
            .HasForeignKey<CounterProposal>(cp => cp.BookingId);

        modelBuilder.Entity<CounterProposalClass>()
            .HasOne(c => c.CounterProposal)
            .WithMany(cp => cp.Classes)
            .HasForeignKey(c => c.CounterProposalId);

        modelBuilder.Entity<LessonReport>()
            .HasOne(lr => lr.Booking)
            .WithOne(b => b.LessonReport)
            .HasForeignKey<LessonReport>(lr => lr.BookingId);

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
    }
}
