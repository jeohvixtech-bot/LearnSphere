using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearnSphere.API.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewBookingId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BookingId column and filtered unique index already exist on the TutorReviews table
            // (applied directly to the database before EF migrations were introduced).
            // This migration is a no-op to register the change in __EFMigrationsHistory.
            //
            // Actual schema additions already present in DB:
            //   - TutorReviews.BookingId  INT NULL
            //   - UQ_TutorReviews_Booking unique index on (TutorId, BookingIdUniq)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // To roll back manually:
            //   ALTER TABLE TutorReviews DROP INDEX UQ_TutorReviews_Booking;
            //   ALTER TABLE TutorReviews DROP COLUMN BookingIdUniq;
            //   ALTER TABLE TutorReviews DROP COLUMN BookingId;
        }
    }
}
