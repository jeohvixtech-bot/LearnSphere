# Implementation Plan: Backend API Completion

## Overview

19 tasks across 6 tracks: auth verification, data model, backend endpoints (reviews, payout approval, slot validation), frontend service, and test suite. Auth login/register is task 1. Remaining tasks build bottom-up — model changes and DTO additions first, then controllers, then migration, then tests.

## Task Dependency Graph

```json
{
  "waves": [
    { "wave": 1, "tasks": ["1", "2", "3", "5", "6", "7"] },
    { "wave": 2, "tasks": ["4", "8"] },
    { "wave": 3, "tasks": ["9"] },
    { "wave": 4, "tasks": ["10"] },
    { "wave": 5, "tasks": ["11", "12", "13", "14", "15", "16", "17", "18"] },
    { "wave": 6, "tasks": ["19"] }
  ]
}
```

## Tasks

- [x] 1. Verify and align login & register API with frontend fields
  - Confirm `POST /api/auth/login` accepts `{ email, password }` and returns `{ token, role, name, userId }`
  - Confirm `POST /api/auth/register` accepts `{ email, password, name, role }` and returns `{ token, role, name, userId }`
  - Verify `role` field accepts values `"parent"` and `"tutor"` (matching the frontend select options)
  - Verify that registering with `role = "tutor"` auto-creates a `Tutor` profile record (already in `AuthService.RegisterAsync`)
  - Verify JWT token is returned and can be decoded to expose `ClaimTypes.NameIdentifier` (userId), `ClaimTypes.Role`, and `ClaimTypes.Name`
  - Verify `AuthService` in `auth.service.js` stores `token` as `ls_token` and the full response as `ls_user` in `localStorage` — confirm field names match `res.data.token`, `res.data.role`, `res.data.name`, `res.data.userId`
  - If any field name mismatch is found between `AuthResponseDto` and what the frontend reads, fix `AuthResponseDto` or the frontend `AuthService` accordingly
  - **Files:** `backend/LearnSphere.API/DTOs/AuthDtos.cs`, `backend/LearnSphere.API/Services/AuthService.cs`, `frontend/app/services/auth.service.js`
  - **Requirement:** Auth login and register end-to-end contract

- [x] 2. Add `BookingId` to `TutorReview` model and update `AppDbContext`
  - Add nullable `int? BookingId` property to `TutorReview` class in `Models/Tutor.cs`
  - Add filtered unique index on `(TutorId, BookingId)` in `AppDbContext.OnModelCreating`
  - **Files:** `backend/LearnSphere.API/Models/Tutor.cs`, `backend/LearnSphere.API/Data/AppDbContext.cs`
  - **Requirement:** 1.5

- [x] 3. Add `CreateReviewDto` to `TutorDtos.cs`
  - Add `CreateReviewDto` with `int Rating`, `string Text`, `int? BookingId` properties
  - **Files:** `backend/LearnSphere.API/DTOs/TutorDtos.cs`
  - **Requirement:** 1.1, 1.6, 1.7

- [x] 4. Implement `POST /api/tutors/{id}/reviews` in `TutorsController`
  - Add `[HttpPost("{id}/reviews")]` `[Authorize(Roles = "parent")]` action `AddReview(int id, CreateReviewDto dto)`
  - Resolve caller `userId` and `user.Name` from JWT claims
  - Load tutor by `id`; return `404` if not found
  - Validate `dto.Rating` in `[1,5]`; return `400` with `"Rating must be between 1 and 5."` if invalid
  - Validate `dto.Text` is non-null and non-whitespace; return `400` with `"Review text cannot be empty."` if invalid
  - If `dto.BookingId` is provided, load booking with `Student`; return `400 "No qualifying completed booking found."` if booking status is not `completed`, `TutorId` doesn't match, or student's `ParentUserId` doesn't match caller
  - If `dto.BookingId` is provided, check for existing review with same `BookingId`; return `409 "A review for this booking has already been submitted."` if found
  - Create `TutorReview` with `Author = user.Name`; persist to DB
  - Recalculate `tutor.Rating = (tutor.Rating * tutor.ReviewCount + dto.Rating) / (tutor.ReviewCount + 1)`; increment `tutor.ReviewCount`
  - Save changes; reload tutor with all includes; return `200 OK` with `TutorDto`
  - **Files:** `backend/LearnSphere.API/Controllers/TutorsController.cs`
  - **Requirement:** 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8
  - **Depends on:** 2, 3

- [x] 5. Implement `PATCH /api/admin/payouts/{id}/approve` in `AdminController`
  - Add `[HttpPatch("payouts/{id}/approve")]` action `ApprovePayout(int id)` to `AdminController`
  - Load `Payout` by `id`; return `404` if not found
  - Check `payout.Status == "Processing"`; return `400` with `"Payout is not in an approvable state."` if not
  - Set `payout.Status = "Completed"`; save changes; return `200 OK`
  - Class-level `[Authorize(Roles = "admin")]` already covers auth — no additional decorator needed
  - **Files:** `backend/LearnSphere.API/Controllers/AdminController.cs`
  - **Requirement:** 2.1, 2.2, 2.3, 2.4

- [x] 6. Add slot ownership validation in `BookingsController.Create`
  - At the top of the `Create` action, before the `Booking` entity is constructed, insert guard:
    - If `dto.SlotId.HasValue`, load `TutorTimeSlot` by `dto.SlotId.Value`
    - Return `400 "The specified slot does not exist."` if slot is null
    - Return `400 "The specified slot does not belong to the requested tutor."` if `slot.TutorId != dto.TutorId`
  - Leave null `slotId` path unchanged
  - **Files:** `backend/LearnSphere.API/Controllers/BookingsController.cs`
  - **Requirement:** 4.1, 4.2, 4.3, 4.4

- [x] 7. Add `TutorService.update` method in the frontend
  - Add `self.update = function(id, data)` to `TutorService`
  - Call `$http.put(API_URL + '/tutors/' + id, data, { headers: AuthService.authHeader() })`
  - Return the `$http` promise directly
  - **Files:** `frontend/app/services/tutor.service.js`
  - **Requirement:** 3.1, 3.2, 3.3

- [x] 8. Run EF Core migration for `TutorReview.BookingId`
  - Run `dotnet ef migrations add AddReviewBookingId` from the API project directory
  - Run `dotnet ef database update` to apply
  - Verify migration file is generated with the nullable column and filtered unique index
  - **Files:** `backend/LearnSphere.API/` (migration files auto-generated)
  - **Depends on:** 2

- [x] 9. Verify backend builds without errors
  - Run `dotnet build` from `backend/LearnSphere.API/`
  - Confirm zero build errors on the changed files
  - **Depends on:** 4, 5, 6, 8

- [x] 10. Set up xUnit + FsCheck test project
  - Create a new xUnit test project: `dotnet new xunit -n LearnSphere.Tests` under `backend/`
  - Add project reference to `LearnSphere.API`
  - Add NuGet packages: `FsCheck.Xunit`, `Microsoft.EntityFrameworkCore.InMemory`, `FakeItEasy`
  - Add a `TestDbContextFactory` helper that creates a fresh `AppDbContext` backed by `InMemory` for each test
  - **Files:** `backend/LearnSphere.Tests/LearnSphere.Tests.csproj`, `backend/LearnSphere.Tests/Helpers/TestDbContextFactory.cs`
  - **Depends on:** 9

- [x] 11. Write example-based unit tests for login & register
  - **Login happy path**: POST `{ email, password }` for a seeded user; assert `200`; response contains `token`, `role`, `name`, `userId`
  - **Login wrong password**: assert `401`
  - **Login unknown email**: assert `401`
  - **Register happy path**: POST `{ email, password, name, role: "parent" }`; assert `200`; response contains `token`
  - **Register duplicate email**: assert `400`
  - **Register tutor role**: assert `200`; assert a `Tutor` profile row is created in DB with matching `UserId`
  - **Files:** `backend/LearnSphere.Tests/Controllers/AuthControllerTests.cs`
  - **Depends on:** 10

- [x] 12. Write example-based unit tests for `AddReview` — error paths
  - **Role guard**: build controller with `tutor` role claim; call `AddReview`; assert `403`
  - **Tutor not found**: pass non-existent `id`; assert `404`
  - **Rating out of range**: pass `rating = 0` and `rating = 6`; assert `400` with expected message
  - **Whitespace text**: pass `text = "  "`; assert `400`
  - **BookingId — no matching booking**: pass a `bookingId` that doesn't exist; assert `400`
  - **BookingId — booking not completed**: pass booking with `status = "pending"`; assert `400`
  - **BookingId — tutor mismatch**: pass booking belonging to a different tutor; assert `400`
  - **BookingId — parent mismatch**: pass booking whose student belongs to a different parent; assert `400`
  - **Files:** `backend/LearnSphere.Tests/Controllers/TutorsControllerReviewTests.cs`
  - **Depends on:** 10

- [x] 13. Write example-based unit tests for `ApprovePayout` — error paths
  - **Payout not found**: call `ApprovePayout` with non-existent id; assert `404`
  - **Already completed**: seed payout with `status = "Completed"`; assert `400` with expected message
  - **Happy path**: seed payout with `status = "Processing"`; assert `200`; reload from DB; assert `status == "Completed"`
  - **Files:** `backend/LearnSphere.Tests/Controllers/AdminControllerPayoutTests.cs`
  - **Depends on:** 10

- [x] 14. Write example-based unit tests for slot ownership validation
  - **Slot not found**: POST booking with `slotId` pointing to non-existent slot; assert `400` with `"The specified slot does not exist."`
  - **Slot tutor mismatch**: POST booking where `slot.TutorId != dto.TutorId`; assert `400` with `"The specified slot does not belong to the requested tutor."`
  - **Null slotId bypass**: POST booking with `slotId = null`; assert booking is created (`200`)
  - **Files:** `backend/LearnSphere.Tests/Controllers/BookingsControllerSlotTests.cs`
  - **Depends on:** 10

- [x] 15. Write property-based test: Property 1 — Review submission round trip
  - Tag: `// Feature: backend-api-completion, Property 1: review submission round trip`
  - Generate: random valid `rating` in [1,5], random non-empty `text`; seed tutor and parent user in InMemory DB
  - Assert: response `200`; returned `TutorDto.Reviews` contains entry where `Text` matches and `Author == user.Name`
  - **Files:** `backend/LearnSphere.Tests/Properties/ReviewPropertyTests.cs`
  - **Depends on:** 10

- [x] 16. Write property-based test: Property 2 — Rating recalculation formula
  - Tag: `// Feature: backend-api-completion, Property 2: rating recalculation formula`
  - Generate: `oldRating` (double 0.0–5.0), `oldReviewCount` (int 0–1000), `newRating` (int 1–5)
  - Seed tutor with those values; submit review; reload tutor
  - Assert: `Math.Abs(tutor.Rating - expected) < 1e-9`; `tutor.ReviewCount == oldReviewCount + 1`
  - **Files:** `backend/LearnSphere.Tests/Properties/ReviewPropertyTests.cs`
  - **Depends on:** 10

- [x] 17. Write property-based test: Property 3 — Duplicate review rejection
  - Tag: `// Feature: backend-api-completion, Property 3: duplicate review rejection`
  - Generate: completed booking seeded in DB; valid review DTO using that booking
  - First call: assert `200`; second call with same `bookingId`: assert `409`; `tutor.ReviewCount` unchanged from after first call
  - **Files:** `backend/LearnSphere.Tests/Properties/ReviewPropertyTests.cs`
  - **Depends on:** 10

- [x] 18. Write property-based tests: Properties 4–8 — Input validation, payout and slot
  - **Property 4** tag: `// Feature: backend-api-completion, Property 4: invalid rating rejection`
    - Generate: integers outside [1,5]; assert `400`; assert no new `TutorReview` row in DB
  - **Property 5** tag: `// Feature: backend-api-completion, Property 5: whitespace text rejection`
    - Generate: strings of whitespace chars; assert `400`; assert no new `TutorReview` row in DB
  - **Property 6** tag: `// Feature: backend-api-completion, Property 6: payout approval state transition`
    - Generate: payout with `status = "Processing"`, random positive `Amount`; PATCH; assert `200`; reload; assert `"Completed"`
  - **Property 7** tag: `// Feature: backend-api-completion, Property 7: slot ownership mismatch rejection`
    - Generate: two distinct tutor IDs; slot owned by one; booking payload targeting the other; assert `400`; assert booking count unchanged
  - **Property 8** tag: `// Feature: backend-api-completion, Property 8: null slotId bypasses ownership validation`
    - Generate: valid booking payload with `slotId = null`; assert `200`; assert booking exists in DB
  - **Files:** `backend/LearnSphere.Tests/Properties/ReviewPropertyTests.cs`, `backend/LearnSphere.Tests/Properties/PayoutPropertyTests.cs`, `backend/LearnSphere.Tests/Properties/SlotPropertyTests.cs`
  - **Depends on:** 10

- [x] 19. Run full test suite and confirm all pass
  - Run `dotnet test` from `backend/LearnSphere.Tests/`
  - All example-based and property-based tests must pass
  - **Depends on:** 11, 12, 13, 14, 15, 16, 17, 18

## Notes

- Task 1 is the auth alignment check. The backend `AuthResponseDto` currently returns `Token`, `Role`, `Name`, `UserId` (PascalCase). The frontend reads `res.data.token`, `res.data.role`, `res.data.name`, `res.data.userId` (camelCase). ASP.NET Core's default JSON serialiser (`System.Text.Json`) serialises to camelCase by default — confirm this is configured in `Program.cs` so the casing matches.
- Task 7 (`TutorService.update`) is independent of all backend tasks and can be done in any order.
- Task 8 (EF migration) must be run in an environment where the database connection is available.
- The filtered unique index uses EF Core syntax — the actual SQL filter expression differs between SQL Server and MySQL/SQLite. Verify the generated migration SQL before applying to production.
