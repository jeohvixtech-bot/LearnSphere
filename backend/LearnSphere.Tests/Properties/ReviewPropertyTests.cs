// Feature: backend-api-completion, Property 1: review submission round trip
// Feature: backend-api-completion, Property 2: rating recalculation formula
// Feature: backend-api-completion, Property 3: duplicate review rejection
// Feature: backend-api-completion, Property 4: invalid rating rejection
// Feature: backend-api-completion, Property 5: whitespace text rejection

using System.Security.Claims;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LearnSphere.API.Controllers;
using LearnSphere.API.DTOs;
using LearnSphere.API.Models;
using LearnSphere.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LearnSphere.Tests.Properties;

/// <summary>
/// Property-based tests for the review submission workflow.
/// Uses FsCheck.Xunit to verify properties hold across many randomly generated inputs.
/// </summary>
public class ReviewPropertyTests
{
    // ── Property 1 ──────────────────────────────────────────────────────────

    /// <summary>
    /// Property 1 — Review submission round trip.
    ///
    /// For any valid rating in [1,5] and any non-whitespace text string, submitting a
    /// review through <see cref="TutorsController.AddReview"/> must:
    ///   • return HTTP 200 OK, and
    ///   • include in the returned TutorDto.Reviews an entry whose Text equals the
    ///     submitted text and whose Author equals the authenticated user's name.
    ///
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    ///
    /// Uses explicit <c>Prop.ForAll</c> with:
    ///   - ratingArb: integers constrained to [1, 5]
    ///   - textArb:   <see cref="NonWhiteSpaceString"/> values (FsCheck built-in type
    ///                that guarantees the generated string is never null or all-whitespace)
    /// </summary>
    [Property(DisplayName = "Property 1: review submission round trip")]
    public Property ReviewSubmission_RoundTrip_HappyPath()
    {
        // rating Arbitrary: integers in [1, 5]
        var ratingArb = Arb.From(Gen.Choose(1, 5));

        // text Arbitrary: non-whitespace strings via FsCheck's built-in NonWhiteSpaceString
        var textArb = ArbMap.Default.ArbFor<NonWhiteSpaceString>();

        return Prop.ForAll(ratingArb, textArb, (rating, nonWsText) =>
        {
            var text = nonWsText.Get;

            // Build a fresh isolated DB for each generated input pair
            var db = TestDbContextFactory.Create();

            // Seed tutor user
            var tutorUser = new User
            {
                Email        = "tutor@test.com",
                PasswordHash = "hash",
                Role         = "tutor",
                Name         = "Tutor Name",
                CreatedAt    = DateTime.UtcNow
            };
            db.Users.Add(tutorUser);
            db.SaveChanges();

            var tutor = new Tutor { UserId = tutorUser.Id };
            db.Tutors.Add(tutor);
            db.SaveChanges();

            // Seed parent user
            var parentUser = new User
            {
                Email        = "parent@test.com",
                PasswordHash = "hash",
                Role         = "parent",
                Name         = "Parent Name",
                CreatedAt    = DateTime.UtcNow
            };
            db.Users.Add(parentUser);
            db.SaveChanges();

            // Build the controller with the parent's identity claims
            var controller = new TutorsController(db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            new[]
                            {
                                new Claim(ClaimTypes.NameIdentifier, parentUser.Id.ToString()),
                                new Claim(ClaimTypes.Name,           parentUser.Name),
                                new Claim(ClaimTypes.Role,           "parent")
                            },
                            authenticationType: "Test"))
                    }
                }
            };

            // Act — synchronous wrapper required because FsCheck [Property] doesn't
            // support async Task return values in FsCheck.Xunit 3.x
            var result = controller.AddReview(tutor.Id, new CreateReviewDto
            {
                Rating = rating,
                Text   = text
            }).GetAwaiter().GetResult();

            // Assert 1: response must be 200 OK
            var ok = result as OkObjectResult;
            if (ok == null)
                return Prop.Label(
                    false,
                    $"Expected OkObjectResult but got {result?.GetType().Name ?? "null"}");

            if (ok.StatusCode != 200)
                return Prop.Label(
                    false,
                    $"Expected status 200 but got {ok.StatusCode}");

            // Assert 2: returned TutorDto must contain the review with matching Text and Author
            var dto = ok.Value as TutorDto;
            if (dto == null)
                return Prop.Label(
                    false,
                    $"Response body could not be cast to TutorDto; actual type: {ok.Value?.GetType().Name ?? "null"}");

            var review = dto.Reviews.FirstOrDefault(r => r.Text == text);
            if (review == null)
                return Prop.Label(
                    false,
                    $"Reviews collection does not contain an entry with Text='{text}'. Reviews: [{string.Join(", ", dto.Reviews.Select(r => r.Text))}]");

            if (review.Author != parentUser.Name)
                return Prop.Label(
                    false,
                    $"Review Author is '{review.Author}' but expected '{parentUser.Name}'");

            return Prop.ToProperty(true);
        });
    }

    // ── Property 2 ──────────────────────────────────────────────────────────

    /// <summary>
    /// Property 2 — Rating recalculation formula.
    ///
    /// For any tutor seeded with an arbitrary <c>oldRating</c> in [0.0, 5.0] and
    /// <c>oldReviewCount</c> in [0, 1000], submitting a review with a valid
    /// <c>newRating</c> in [1, 5] must produce:
    ///   • <c>tutor.Rating ≈ (oldRating * oldReviewCount + newRating) / (oldReviewCount + 1)</c>
    ///     (within 1e-9 absolute tolerance), and
    ///   • <c>tutor.ReviewCount == oldReviewCount + 1</c>.
    ///
    /// **Validates: Requirements 1.2**
    ///
    /// Uses explicit <c>Prop.ForAll</c> with:
    ///   - oldRatingArb:     doubles constrained to [0.0, 5.0] via linear scaling
    ///   - oldReviewCountArb: integers constrained to [0, 1000]
    ///   - newRatingArb:     integers constrained to [1, 5]
    /// </summary>
    [Property(DisplayName = "Property 2: rating recalculation formula")]
    public Property RatingRecalculation_MatchesFormula()
    {
        // oldRating Arbitrary: doubles in [0.0, 5.0] scaled from a [0,1] uniform double
        var oldRatingArb = Arb.From(
            Gen.Choose(0, 50000).Select(n => n / 10000.0));

        // oldReviewCount Arbitrary: integers in [0, 1000]
        var oldReviewCountArb = Arb.From(Gen.Choose(0, 1000));

        // newRating Arbitrary: integers in [1, 5]
        var newRatingArb = Arb.From(Gen.Choose(1, 5));

        return Prop.ForAll(oldRatingArb, oldReviewCountArb, newRatingArb,
            (oldRating, oldReviewCount, newRating) =>
        {
            // Compute expected result using the same formula as the controller
            double expected = (oldRating * oldReviewCount + newRating) / (oldReviewCount + 1);

            // Build a fresh isolated DB for each generated input triple
            var db = TestDbContextFactory.Create();

            // Seed tutor user
            var tutorUser = new User
            {
                Email        = "tutor@recalc.com",
                PasswordHash = "hash",
                Role         = "tutor",
                Name         = "Tutor Recalc",
                CreatedAt    = DateTime.UtcNow
            };
            db.Users.Add(tutorUser);
            db.SaveChanges();

            // Seed tutor with the generated initial Rating and ReviewCount
            var tutor = new Tutor
            {
                UserId      = tutorUser.Id,
                Rating      = oldRating,
                ReviewCount = oldReviewCount
            };
            db.Tutors.Add(tutor);
            db.SaveChanges();

            // Seed parent user
            var parentUser = new User
            {
                Email        = "parent@recalc.com",
                PasswordHash = "hash",
                Role         = "parent",
                Name         = "Parent Recalc",
                CreatedAt    = DateTime.UtcNow
            };
            db.Users.Add(parentUser);
            db.SaveChanges();

            // Build controller with parent role claim
            var controller = new TutorsController(db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            new[]
                            {
                                new Claim(ClaimTypes.NameIdentifier, parentUser.Id.ToString()),
                                new Claim(ClaimTypes.Name,           parentUser.Name),
                                new Claim(ClaimTypes.Role,           "parent")
                            },
                            authenticationType: "Test"))
                    }
                }
            };

            // Act — synchronous wrapper required because FsCheck [Property] doesn't
            // support async Task return values in FsCheck.Xunit 3.x
            var result = controller.AddReview(tutor.Id, new CreateReviewDto
            {
                Rating = newRating,
                Text   = "Property 2 test review"
            }).GetAwaiter().GetResult();

            // Assert HTTP 200
            var ok = result as OkObjectResult;
            if (ok == null)
                return Prop.Label(
                    false,
                    $"Expected OkObjectResult but got {result?.GetType().Name ?? "null"}");

            // Reload tutor directly from DB to verify persisted values
            var reloaded = db.Tutors.Find(tutor.Id);
            if (reloaded == null)
                return Prop.Label(false, "Could not reload tutor from DB after AddReview");

            // Assert ReviewCount incremented
            if (reloaded.ReviewCount != oldReviewCount + 1)
                return Prop.Label(
                    false,
                    $"ReviewCount: expected {oldReviewCount + 1}, got {reloaded.ReviewCount}");

            // Assert Rating matches formula within epsilon
            double diff = Math.Abs(reloaded.Rating - expected);
            if (diff >= 1e-9)
                return Prop.Label(
                    false,
                    $"Rating mismatch: expected {expected:G17}, got {reloaded.Rating:G17}, diff={diff:G3} " +
                    $"(oldRating={oldRating}, oldReviewCount={oldReviewCount}, newRating={newRating})");

            return Prop.ToProperty(true);
        });
    }

    // ── Property 3 ──────────────────────────────────────────────────────────

    /// <summary>
    /// Property 3 — Duplicate review rejection.
    ///
    /// For any valid rating in [1,5] and any non-whitespace text string, submitting
    /// the same review twice for the same completed booking must:
    ///   • First call returns HTTP 200 OK, and
    ///   • Second call with the same <c>BookingId</c> returns HTTP 409 Conflict, and
    ///   • <c>tutor.ReviewCount</c> after the second call equals <c>ReviewCount</c>
    ///     after the first call (i.e. the duplicate is not counted).
    ///
    /// **Validates: Requirements 1.5, 1.7**
    ///
    /// Uses explicit <c>Prop.ForAll</c> with:
    ///   - ratingArb: integers constrained to [1, 5]
    ///   - textArb:   <see cref="NonWhiteSpaceString"/> values (FsCheck built-in type)
    /// </summary>
    [Property(DisplayName = "Property 3: duplicate review rejection")]
    public Property DuplicateReview_Rejected_WithConflict()
    {
        // rating Arbitrary: integers in [1, 5]
        var ratingArb = Arb.From(Gen.Choose(1, 5));

        // text Arbitrary: non-whitespace strings via FsCheck's built-in NonWhiteSpaceString
        var textArb = ArbMap.Default.ArbFor<NonWhiteSpaceString>();

        return Prop.ForAll(ratingArb, textArb, (rating, nonWsText) =>
        {
            var text = nonWsText.Get;

            // Build a fresh isolated DB for each generated input pair
            var db = TestDbContextFactory.Create();

            // Seed tutor user
            var tutorUser = new User
            {
                Email        = "tutor@duptest.com",
                PasswordHash = "hash",
                Role         = "tutor",
                Name         = "Tutor Dup",
                CreatedAt    = DateTime.UtcNow
            };
            db.Users.Add(tutorUser);
            db.SaveChanges();

            var tutor = new Tutor { UserId = tutorUser.Id };
            db.Tutors.Add(tutor);
            db.SaveChanges();

            // Seed parent user
            var parentUser = new User
            {
                Email        = "parent@duptest.com",
                PasswordHash = "hash",
                Role         = "parent",
                Name         = "Parent Dup",
                CreatedAt    = DateTime.UtcNow
            };
            db.Users.Add(parentUser);
            db.SaveChanges();

            // Seed a student linked to the parent
            var student = new Student
            {
                ParentUserId   = parentUser.Id,
                Name           = "Student Dup",
                BirthDate      = "2010-01-01",
                School         = "Test School",
                EducationLevel = "Primary",
                SubjectSelect  = "Math"
            };
            db.Students.Add(student);
            db.SaveChanges();

            // Seed a completed booking linking the tutor and the parent's student
            var booking = new Booking
            {
                TutorId       = tutor.Id,
                StudentId     = student.Id,
                Subject       = "Math",
                Mode          = "Online",
                Status        = "completed",
                BookingNumber = $"BK-DUP-{Guid.NewGuid():N}"
            };
            db.Bookings.Add(booking);
            db.SaveChanges();

            // Build the controller with the parent's identity claims
            var controller = new TutorsController(db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            new[]
                            {
                                new Claim(ClaimTypes.NameIdentifier, parentUser.Id.ToString()),
                                new Claim(ClaimTypes.Name,           parentUser.Name),
                                new Claim(ClaimTypes.Role,           "parent")
                            },
                            authenticationType: "Test"))
                    }
                }
            };

            var dto = new CreateReviewDto
            {
                Rating    = rating,
                Text      = text,
                BookingId = booking.Id
            };

            // ── First call: must return 200 OK ──────────────────────────────
            var firstResult = controller.AddReview(tutor.Id, dto)
                                        .GetAwaiter().GetResult();

            var firstOk = firstResult as OkObjectResult;
            if (firstOk == null)
                return Prop.Label(
                    false,
                    $"First call: expected OkObjectResult but got {firstResult?.GetType().Name ?? "null"}");

            if (firstOk.StatusCode != 200)
                return Prop.Label(
                    false,
                    $"First call: expected status 200 but got {firstOk.StatusCode}");

            // Capture ReviewCount after first successful submission
            var tutorAfterFirst = db.Tutors.Find(tutor.Id);
            if (tutorAfterFirst == null)
                return Prop.Label(false, "Could not reload tutor after first call");

            int reviewCountAfterFirst = tutorAfterFirst.ReviewCount;

            // ── Second call: must return 409 Conflict ───────────────────────
            var secondResult = controller.AddReview(tutor.Id, dto)
                                         .GetAwaiter().GetResult();

            var conflict = secondResult as ConflictObjectResult;
            if (conflict == null)
                return Prop.Label(
                    false,
                    $"Second call: expected ConflictObjectResult (409) but got {secondResult?.GetType().Name ?? "null"}");

            if (conflict.StatusCode != 409)
                return Prop.Label(
                    false,
                    $"Second call: expected status 409 but got {conflict.StatusCode}");

            // ── ReviewCount unchanged after duplicate is rejected ───────────
            // Detach cached entity and reload fresh from DB
            db.Entry(tutorAfterFirst).Reload();
            var tutorAfterSecond = db.Tutors.Find(tutor.Id);
            if (tutorAfterSecond == null)
                return Prop.Label(false, "Could not reload tutor after second call");

            if (tutorAfterSecond.ReviewCount != reviewCountAfterFirst)
                return Prop.Label(
                    false,
                    $"ReviewCount after duplicate: expected {reviewCountAfterFirst} " +
                    $"but got {tutorAfterSecond.ReviewCount}");

            return Prop.ToProperty(true);
        });
    }

    // ── Property 4 ──────────────────────────────────────────────────────────

    /// <summary>
    /// Property 4 — Invalid rating rejection.
    ///
    /// For any integer <c>rating</c> outside the range [1, 5] (i.e. &lt; 1 or &gt; 5),
    /// submitting a review must:
    ///   • return HTTP 400 Bad Request, and
    ///   • leave the <c>TutorReviews</c> table unchanged (no new row).
    ///
    /// **Validates: Requirements 1.4**
    ///
    /// Generator: integers &lt; 1 or &gt; 5, produced by combining Gen.Choose(int.MinValue/2, 0)
    /// with Gen.Choose(6, int.MaxValue/2) via Gen.OneOf.
    /// </summary>
    [Property(DisplayName = "Property 4: invalid rating rejection")]
    public Property InvalidRating_IsRejected_WithBadRequest()
    {
        // Generate integers strictly outside [1,5]: either ≤ 0 or ≥ 6
        var outOfRangeRatingArb = Arb.From(
            Gen.OneOf(
                Gen.Choose(int.MinValue / 2, 0),
                Gen.Choose(6, int.MaxValue / 2)));

        return Prop.ForAll(outOfRangeRatingArb, rating =>
        {
            // Fresh isolated DB per generated value
            var db = TestDbContextFactory.Create();

            // Seed tutor user
            var tutorUser = new User
            {
                Email        = "tutor@prop4.com",
                PasswordHash = "hash",
                Role         = "tutor",
                Name         = "Tutor Prop4",
                CreatedAt    = DateTime.UtcNow
            };
            db.Users.Add(tutorUser);
            db.SaveChanges();

            var tutor = new Tutor { UserId = tutorUser.Id };
            db.Tutors.Add(tutor);
            db.SaveChanges();

            // Seed parent user
            var parentUser = new User
            {
                Email        = "parent@prop4.com",
                PasswordHash = "hash",
                Role         = "parent",
                Name         = "Parent Prop4",
                CreatedAt    = DateTime.UtcNow
            };
            db.Users.Add(parentUser);
            db.SaveChanges();

            // Capture review count before the call
            int reviewsBefore = db.TutorReviews.Count();

            // Build controller with parent identity
            var controller = new TutorsController(db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            new[]
                            {
                                new Claim(ClaimTypes.NameIdentifier, parentUser.Id.ToString()),
                                new Claim(ClaimTypes.Name,           parentUser.Name),
                                new Claim(ClaimTypes.Role,           "parent")
                            },
                            authenticationType: "Test"))
                    }
                }
            };

            // Act
            var result = controller.AddReview(tutor.Id, new CreateReviewDto
            {
                Rating = rating,
                Text   = "Valid review text"
            }).GetAwaiter().GetResult();

            // Assert 1: must be 400 Bad Request
            var bad = result as BadRequestObjectResult;
            if (bad == null)
                return Prop.Label(
                    false,
                    $"rating={rating}: expected BadRequestObjectResult but got {result?.GetType().Name ?? "null"}");

            if (bad.StatusCode != 400)
                return Prop.Label(
                    false,
                    $"rating={rating}: expected status 400 but got {bad.StatusCode}");

            // Assert 2: no new TutorReview row
            int reviewsAfter = db.TutorReviews.Count();
            if (reviewsAfter != reviewsBefore)
                return Prop.Label(
                    false,
                    $"rating={rating}: DB review count changed from {reviewsBefore} to {reviewsAfter}");

            return Prop.ToProperty(true);
        });
    }

    // ── Property 5 ──────────────────────────────────────────────────────────

    /// <summary>
    /// Property 5 — Whitespace text rejection.
    ///
    /// For any non-empty string composed entirely of whitespace characters,
    /// submitting a review must:
    ///   • return HTTP 400 Bad Request, and
    ///   • leave the <c>TutorReviews</c> table unchanged (no new row).
    ///
    /// **Validates: Requirements 1.4**
    ///
    /// Generator: non-empty strings where every character is one of the standard
    /// whitespace characters (' ', '\t', '\n', '\r', '\f', '\v').
    /// </summary>
    [Property(DisplayName = "Property 5: whitespace text rejection")]
    public Property WhitespaceText_IsRejected_WithBadRequest()
    {
        // Whitespace characters to pick from
        char[] wsChars = { ' ', '\t', '\n', '\r', '\f', '\v' };

        // Generate a non-empty list of whitespace chars, then convert to string
        var whitespaceStringArb = Arb.From(
            Gen.NonEmptyListOf(Gen.Elements(wsChars))
               .Select(chars => new string(chars.ToArray())));

        return Prop.ForAll(whitespaceStringArb, wsText =>
        {
            // Fresh isolated DB per generated value
            var db = TestDbContextFactory.Create();

            // Seed tutor user
            var tutorUser = new User
            {
                Email        = "tutor@prop5.com",
                PasswordHash = "hash",
                Role         = "tutor",
                Name         = "Tutor Prop5",
                CreatedAt    = DateTime.UtcNow
            };
            db.Users.Add(tutorUser);
            db.SaveChanges();

            var tutor = new Tutor { UserId = tutorUser.Id };
            db.Tutors.Add(tutor);
            db.SaveChanges();

            // Seed parent user
            var parentUser = new User
            {
                Email        = "parent@prop5.com",
                PasswordHash = "hash",
                Role         = "parent",
                Name         = "Parent Prop5",
                CreatedAt    = DateTime.UtcNow
            };
            db.Users.Add(parentUser);
            db.SaveChanges();

            // Capture review count before the call
            int reviewsBefore = db.TutorReviews.Count();

            // Build controller with parent identity
            var controller = new TutorsController(db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(
                            new[]
                            {
                                new Claim(ClaimTypes.NameIdentifier, parentUser.Id.ToString()),
                                new Claim(ClaimTypes.Name,           parentUser.Name),
                                new Claim(ClaimTypes.Role,           "parent")
                            },
                            authenticationType: "Test"))
                    }
                }
            };

            // Act
            var result = controller.AddReview(tutor.Id, new CreateReviewDto
            {
                Rating = 3,    // valid rating — only text is the variable under test
                Text   = wsText
            }).GetAwaiter().GetResult();

            // Assert 1: must be 400 Bad Request
            var bad = result as BadRequestObjectResult;
            if (bad == null)
                return Prop.Label(
                    false,
                    $"Expected BadRequestObjectResult but got {result?.GetType().Name ?? "null"}");

            if (bad.StatusCode != 400)
                return Prop.Label(
                    false,
                    $"Expected status 400 but got {bad.StatusCode}");

            // Assert 2: no new TutorReview row
            int reviewsAfter = db.TutorReviews.Count();
            if (reviewsAfter != reviewsBefore)
                return Prop.Label(
                    false,
                    $"DB review count changed from {reviewsBefore} to {reviewsAfter}");

            return Prop.ToProperty(true);
        });
    }
}
