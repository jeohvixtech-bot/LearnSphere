# LearnSphere – Tutor Matching Platform

**Stack:** AngularJS 1.x Frontend · ASP.NET Core 8 Web API · MySQL + Entity Framework Core

---

## Table of Contents

1. [Project Structure](#project-structure)
2. [Prerequisites](#prerequisites)
3. [Backend Setup](#backend-setup)
4. [Frontend Setup](#frontend-setup)
5. [Demo Credentials](#demo-credentials)
6. [API Reference](#api-reference)
7. [Database Schema](#database-schema)
8. [Features](#features)
9. [EF Core Migrations](#ef-core-migrations-optional)
10. [Production Notes](#production-notes)

---

## Project Structure

```
LearnSphere/
├── backend/
│   └── LearnSphere.API/          # ASP.NET Core 8 Web API
│       ├── Controllers/           # REST API Controllers
│       ├── Models/                # EF Core Entity Models
│       ├── DTOs/                  # Data Transfer Objects
│       ├── Data/                  # DbContext + Seeder
│       ├── Repositories/          # Generic Repository Pattern
│       ├── Services/              # Business Logic (Auth, JWT)
│       ├── Program.cs             # App entry point + DI config
│       └── appsettings.json       # Connection strings & JWT settings
├── frontend/
│   ├── index.html                 # SPA shell (ng-app + ng-view)
│   ├── libs/                      # Vendored AngularJS, ngRoute, Flatpickr, Tabler icons
│   │   └── ...                    # (no CDN dependency — see Frontend Setup)
│   ├── app/
│   │   ├── app.js                 # Angular module + $routeProvider
│   │   ├── services/              # HTTP services (auth, tutor, booking…)
│   │   └── controllers/           # Angular controllers per role
│   ├── views/
│   │   ├── login.html
│   │   ├── parent/                # Parent dashboard views
│   │   ├── tutor/                 # Tutor dashboard views
│   │   └── admin/                 # Admin dashboard views
│   └── styles/
│       └── main.css               # Full custom stylesheet
└── database/
    └── schema.sql                 # Manual MySQL schema reference
```

---

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 8.0+ |
| MySQL | 8.0+ |
| Any static file server | (for frontend) |

---

## Backend Setup

### 1. Configure MySQL Connection

Edit `backend/LearnSphere.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=learnsphere_db;User=root;Password=YOUR_PASSWORD;"
  },
  "Jwt": {
    "Key": "LearnSphere_SuperSecret_JWT_Key_2026_CHANGE_IN_PRODUCTION",
    "Issuer": "LearnSphere.API",
    "Audience": "LearnSphere.Client"
  }
}
```

### 2. Restore & Run

```bash
cd backend/LearnSphere.API
dotnet restore
dotnet run
```

The API starts at `http://localhost:5000`. On first run the app **auto-creates the database** and **seeds initial data**.

### 3. Swagger UI

Open `http://localhost:5000/swagger` to explore all API endpoints.

---

## Frontend Setup

The frontend is plain HTML + AngularJS 1.x — **no build step required**.

> **No CDN dependency:** AngularJS, ngRoute, Flatpickr, and Tabler icons are vendored locally under `frontend/libs/` (not loaded from `ajax.googleapis.com` / `cdn.jsdelivr.net`). This keeps the app working on networks that block those hosts. If you need to update a vendored library version, reinstall it via npm into a scratch folder and copy the new `dist` files into `frontend/libs/`.

### Option A: VS Code Live Server

1. Open the `frontend/` folder in VS Code.
2. Right-click `index.html` → **Open with Live Server**.
3. App opens at `http://127.0.0.1:5500`.

### Option B: Python

```bash
cd frontend
python -m http.server 3000
```

### Option C: npx serve

```bash
cd frontend
npx serve . -p 3000
```

> **CORS note:** The backend allows requests from any `localhost` or `127.0.0.1` origin. For remote deployments update the `AllowFrontend` policy in `Program.cs`.

---

## Demo Credentials

| Role | Email | Password |
|------|-------|----------|
| Parent | sarah.tan@example.com | Parent@123 |
| Tutor | lim.ws@example.com | Tutor@123 |
| Admin | admin@learnsphere.sg | Admin@123 |

---

## API Reference

**Base URL:** `http://localhost:5000`  
**Authentication:** `Authorization: Bearer <JWT>` (token obtained from `POST /api/auth/login`)

### Auth

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/auth/login` | — | Login; returns JWT token |
| POST | `/api/auth/register` | — | Register new account (parent / tutor); under-18 must accept T&C |

### Tutors

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/tutors` | — | List tutors — only `IsVerified && IsOnline` profiles are ever returned (filter: `subject`, `mode`, `search`, `rating`) |
| GET | `/api/tutors/favorites` | JWT (parent) | Get the caller's favorited tutor IDs |
| POST | `/api/tutors/{id}/favorite` | JWT (parent) | Favorite a tutor |
| DELETE | `/api/tutors/{id}/favorite` | JWT (parent) | Unfavorite a tutor |
| GET | `/api/tutors/{id}` | — | Get tutor by ID (public) — same `IsVerified && IsOnline` gate; unverified/offline profiles 404 |
| GET | `/api/tutors/by-user/{userId}` | JWT | Get tutor profile by user ID (self-retrieve) |
| PUT | `/api/tutors/{id}` | JWT | Update tutor profile (bio, image, price, subjects, levels, modes, qualifications) |
| PATCH | `/api/tutors/{id}/online-status` | JWT (owner) | Toggle a tutor's own online/offline visibility |
| PATCH | `/api/tutors/{id}/modes` | JWT (owner) | Replace a tutor's teaching modes |
| DELETE | `/api/tutors/{id}` | JWT | Delete tutor account |
| GET | `/api/tutors/{id}/slots` | JWT | Get full timetable |
| GET | `/api/tutors/{id}/busy-times` | — | Get a tutor's booked times (for the parent-facing availability calendar) |
| POST | `/api/tutors/{id}/slots` | JWT | Add a timetable slot (validates no clash with existing slots) |
| DELETE | `/api/tutors/{id}/slots/{slotId}` | JWT (owner) | Remove a timetable slot. For a preset (Flow B) slot with confirmed/pending bookings riding on it, the optional body `{ proposedDate, proposedTime, proposedEndTime }` decides how: **with** a proposed date, each affected booking gets a pending `PresetCancellationDecision` — nothing changes immediately, the parent must Accept (moves that session to the new date) or Reject (queued for admin review); **without** one, every affected booking is resolved immediately toward a credit (see `PresetCancellationsController` / `AdminController`) |
| GET | `/api/tutors/preset-slots` | — | List a tutor's published preset class slots matching a student's subject/level/country and preferred modes (Flow B — see `?studentId=`, `?country=`). Not currently called by the frontend — the parent catalog reads preset slots straight off `GET /api/tutors`' `timetable` field instead — kept for any future tutor-scoped preset browsing |
| GET | `/api/tutors/match-scores` | — | AI Speed Match score for every verified/online tutor, computed live from admin-configured `ScoringWeightages` percentages combined with each tutor's current rating, experience, and this-calendar-month completed-class/dispute counts. Returns both the final score and each criterion's raw metric + points, per tutor |
| POST | `/api/tutors/{id}/setup-class` | JWT (owner) | Publish one or more preset class slots a parent can book directly, no per-request approval. Every slot in one request is tagged with a shared `PresetGroupId` (`PRESET` + zero-padded id of the batch's first slot) so the catalog groups them as one class rather than merging unrelated batches that share a subject. Each slot may optionally carry its own `durationMinutes`, overriding the request-level default — used when the tutor's UI combines several dragged 30-min grid cells into one longer class |

> To reschedule a class: delete the old slot and add a new one.

### Parents

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/parents` | JWT | Get parent profile |
| POST | `/api/parents` | JWT | Create a parent profile |
| PUT | `/api/parents/{id}` | JWT | Update parent profile |
| DELETE | `/api/parents/{id}` | JWT | Delete parent account |

### Students

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/students` | JWT | Get my students |
| POST | `/api/students` | JWT | Add a new student |
| PUT | `/api/students/{id}` | JWT | Update a student |
| DELETE | `/api/students/{id}` | JWT | Permanently delete a student (erases session/billing history — archive instead to keep records) |
| POST | `/api/students/{id}/archive` | JWT | Archive a student profile (hides from active lists/booking, keeps history) |
| POST | `/api/students/{id}/unarchive` | JWT | Restore an archived student profile |
| PATCH | `/api/students/{id}/preferred-modes` | JWT | Set a student's ranked teaching-mode preference (used by preset-class matching) |
| GET | `/api/students/booking` | JWT | Get student's bookings |
| GET | `/api/students/{id}/slots` | JWT | Get student timetable |
| POST | `/api/students/{id}/slots` | JWT | Add a timetable slot (validates no clash) |
| DELETE | `/api/students/{id}/slots/{slotId}` | JWT | Remove a timetable slot |

> To reschedule a class: delete the old slot and add a new one.

### Bookings

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/bookings` | JWT | Get bookings (role-filtered: parent sees own students, tutor sees own) |
| POST | `/api/bookings` | JWT | Create a booking — auto-assigns `BookingNumber` (BOK00001…) |
| POST | `/api/bookings/preset` | JWT (parent) | Book one or more tutor-preset class slots directly (Flow B) as a SINGLE booking — e.g. every occurrence of a recurring series in one call (`presetSlotIds: [...]`) — auto-confirmed, no per-request approval, since the tutor already published these slots. One `Booking` row + one `BookingClass` per occurrence, tracked via `BookingPresetSlots` so cancelling frees every slot's seat |
| PATCH | `/api/bookings/{id}/status` | JWT (parent/tutor on that booking) | Update status: `confirmed` / `cancelled` / `countered`. A `countered` update inserts a new CounterProposals row rather than overwriting; either party can counter-propose in turn |
| POST | `/api/bookings/{id}/cancel` | JWT (parent, owner) | Cancel a booking — voids any unpaid invoice, frees the seat on every preset slot it covers (Flow B), notifies the tutor if they'd already responded. Blocked once `completed`/`cancelled`, or once the invoice is `Paid` |
| POST | `/api/bookings/{id}/lesson-report` | JWT | Submit a lesson report |
| PATCH | `/api/bookings/{id}/lesson-report` | JWT | Edit an existing lesson report (audit trail saved) |
| POST | `/api/bookings/{id}/issue` | JWT | Report an issue on a booking. Filing again after admin has resolved the previous report reopens it rather than being silently ignored |
| PUT | `/api/bookings/{id}/issue` | JWT (parent, owner) | Edit your own still-unresolved issue report |
| DELETE | `/api/bookings/{id}/issue` | JWT (parent, owner) | Withdraw your own still-unresolved issue report. Blocked once admin has resolved it |

### Preset-Class Cancellations

Parent-facing side of a tutor cancelling a published preset (Flow B) slot — see the `DELETE /api/tutors/{id}/slots/{slotId}` entry above for how these get created. One row per affected booking; a group class's families each decide independently.

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/preset-cancellations/mine` | JWT (parent) | Everything this parent still needs to act on or acknowledge. Sweeps auto-accept first — any pending reschedule whose proposed date/time has already passed with no response is resolved as `auto-accepted` before the list is returned, so it never shows up stale |
| POST | `/api/preset-cancellations/{decisionId}/accept` | JWT (parent, owner) | Accept a proposed reschedule — moves that session to the new date/time. No price/invoice change, no penalty |
| POST | `/api/preset-cancellations/{decisionId}/reject` | JWT (parent, owner) | Decline a proposed reschedule — does **not** process a refund itself, just queues the decision for admin review (see Admin below) |
| POST | `/api/preset-cancellations/{decisionId}/acknowledge` | JWT (parent, owner) | Dismiss the acknowledge-only popup for a straight cancel (no reschedule was ever offered) — the refund/penalty already happened at cancel time; this only marks it seen |

### Incidents

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/incident` | JWT | Report an issue on a booking |
| GET | `/api/incident` | JWT | Get all incidents |

### Class Remarks (Bulletin Board)

Route prefix here is just `api` (spans `bookingclasses/`, `remarks/`, and `tutors/{id}/remarks/` sub-paths per the feature spec) rather than a single controller-name prefix.

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/bookingclasses/{bookingClassId}/remarks` | JWT (parent) | Rate + write a remark for one completed class. One remark per class; recomputes the tutor's rating afterward |
| PUT | `/api/remarks/{id}` | JWT (parent, owner) | Edit your own remark's rating/text; recomputes the tutor's rating afterward |
| DELETE | `/api/remarks/{id}` | JWT (parent, owner) | Delete your own remark; recomputes the tutor's rating afterward |
| POST | `/api/remarks/{id}/dispute` | JWT (tutor, owner) | Request a published remark be hidden, with a required reason — queues it for admin review (see Admin → Remark Disputes below) |
| POST | `/api/remarks/{id}/like` | JWT (parent) | Like a remark (once per parent) |
| GET | `/api/tutors/{id}/remarks` | JWT (parent) | List a tutor's published remarks (Bulletin Board), with like counts |
| GET | `/api/tutors/{id}/remarks/mine` | JWT (tutor, owner) | List all of the caller's own remarks regardless of status, for their own record-keeping |

### Chat

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/chat/{tutorId}/{parentUserId}` | JWT | Get chat messages for a specific tutor–parent thread |
| POST | `/api/chat` | JWT | Send a chat message |

### Notifications

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/notifications` | JWT | Get my notifications (unread count badge supported) |
| PATCH | `/api/notifications/mark-all-read` | JWT | Mark all notifications as read |

### Invoices

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/invoices` | JWT | Get my invoices (role-scoped: parent / tutor); includes `InvoiceNumber` (INV00001…) and `BookingNumber` |
| POST | `/api/invoices/{id}/pay` | JWT | Pay an invoice — updates calendars, enables direct chat |
| POST | `/api/invoices/{id}/refund` | JWT | Refund to student (when class cancelled by tutor) |

> Invoices are **auto-generated** when a tutor confirms a booking (`PATCH /bookings/{id}/status` → `confirmed`).

### Payouts

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/payouts` | JWT | Get payout history (tutor only) |
| POST | `/api/payouts` | JWT | Request a payout — available balance = `Paid` invoices minus already-requested payouts minus `TutorPenalties` (e.g. a preset-class cancellation resolved toward a parent credit) |

### Admin

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/admin/stats` | JWT (admin) | Platform statistics (parents, tutors, sessions, revenue) |
| GET | `/api/admin/tutors/unverified` | JWT (admin) | List unverified tutors pending vetting |
| PATCH | `/api/admin/tutors/{id}/verify` | JWT (admin) | Verify a tutor |
| GET | `/api/admin/disputes` | JWT (admin) | List unresolved disputed bookings |
| PATCH | `/api/admin/disputes/{bookingId}/resolve` | JWT (admin) | Resolve a dispute — marks the `IssueReport` resolved (not deleted) and the booking `completed`, so it moves to the Archive tab instead of vanishing |
| GET | `/api/admin/disputes/archive` | JWT (admin) | List resolved disputes (Archive tab) |
| GET | `/api/admin/remark-disputes` | JWT (admin) | List pending tutor "hide this remark" requests (`Status = dispute_requested`) |
| PATCH | `/api/admin/remark-disputes/{id}/resolve` | JWT (admin) | Approve (`hidden`) or reject (`published`) a remark hide request; recomputes the tutor's rating either way |
| GET | `/api/admin/remark-disputes/archive` | JWT (admin) | List resolved remark hide requests, approved or rejected (Archive tab) |
| GET | `/api/admin/institutions` | — | Search institutions (filter: `country`, `type`, `search`) |
| GET | `/api/admin/scoring-weightages` | — | Get the AI Speed Match scoring config (6 fixed rows: Tutor Rating, Activeness, Disputes, Experience, + 2 reserved). Public read — the parent-facing match score needs these percentages too |
| PUT | `/api/admin/scoring-weightages` | JWT (admin) | Update weightage percentages by `key` (0–100, clamped). Point-scale bands per criterion are fixed/not editable, only the weightage % each contributes to the total score |
| GET | `/api/admin/preset-cancellations` | JWT (admin) | Reschedule Rejections queue — every `pending-admin` decision (a parent declined a tutor's proposed reschedule), separate from the general Resolution Disputes desk above |
| POST | `/api/admin/preset-cancellations/{decisionId}/resolve` | JWT (admin) | Approve the rejection — this is what actually triggers the refund, the 20% `TutorPenalty`, and the AI Speed Match dispute-score hit. Nothing financial happens just from the parent's Reject click |

---

## Database Schema

**Database:** MySQL 8.0 &nbsp;·&nbsp; **ORM:** Entity Framework Core (Pomelo provider)

> All date/time values are stored as `VARCHAR` (e.g. `YYYY-MM-DD`, `hh:mm tt`).  
> All money values use `DECIMAL(10,2)`.

### Users

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `Email` | VARCHAR | Unique login email |
| `PasswordHash` | VARCHAR | BCrypt hash |
| `Role` | VARCHAR | `parent` \| `tutor` \| `admin` |
| `Name` | VARCHAR | Display name |
| `CreatedAt` | DATETIME | UTC timestamp |

### Tutors

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `UserId` | INT (FK → Users.Id) | One-to-one with User |
| `ImageUrl` | VARCHAR | Profile photo URL |
| `Rating` | DOUBLE | Aggregate rating 0–5 |
| `ReviewCount` | INT | |
| `PricePerSession` | DECIMAL(10,2) | Derived from minimum Offering price |
| `ExperienceYears` | INT | |
| `Bio` | VARCHAR | Short biography |
| `IsVerified` | TINYINT(1) | Admin-verified flag |
| `IsOnline` | TINYINT(1) | Tutor-controlled switch; offline hides the profile from parent search/booking entirely (default `1`) |

### TutorOfferings

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id, CASCADE DELETE) | |
| `Subject` | VARCHAR(200) | e.g. `Mathematics` |
| `Level` | VARCHAR(200) | e.g. `O-Level` |
| `Mode` | VARCHAR(200) | `Online` \| `Home Visit` \| `Tutor Place` |
| `Qualification` | VARCHAR(200) | e.g. `NIE Trained` |
| `Price` | DECIMAL(10,2) | Per-session price for this offering |

### TutorSubjects

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | |
| `Subject` | VARCHAR | |
| `Price` | DECIMAL(10,2) NULL | Legacy per-subject price |

### TutorLevels

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | |
| `Level` | VARCHAR | e.g. `Primary 5-6`, `O-Level`, `A-Level` |

### TutorModes

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | |
| `Mode` | VARCHAR | `Online` \| `Home Visit` \| `Tutor Place` |

### TutorQualifications

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | |
| `Qualification` | VARCHAR | e.g. `NIE Trained`, `B.Sc. Mathematics` |

### TutorTimeSlots

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | |
| `Day` | VARCHAR | e.g. `Monday` |
| `Time` | VARCHAR | e.g. `10:00 AM` |
| `Status` | VARCHAR | `Available` \| `Booked` |
| `BookingId` | INT NULL | Set when slot is booked |
| `EndTime` | VARCHAR NULL | Preset-class end time (Flow B only) |
| `DurationMinutes` | INT | Default 60; preset-class length in minutes |
| `Mode` | VARCHAR NULL | Preset-class mode |
| `Subject` | VARCHAR NULL | Preset-class subject |
| `Level` | VARCHAR NULL | Preset-class level |
| `Country` | VARCHAR NULL | Preset-class country |
| `ClassSize` | VARCHAR(20) | `one-to-one` \| `one-to-many` |
| `MaxStudents` | INT | Default 1 |
| `ConfirmedCount` | INT | Number of students currently booked into this slot |
| `IsFull` | TINYINT(1) | Set once `ConfirmedCount >= MaxStudents` |
| `PricePerLesson` | DECIMAL(10,2) | Preset-class price |
| `PresetGroupId` | VARCHAR(20) NULL | Shared across every slot from one Setup Class submission (e.g. all occurrences of a weekly recurring class) — `PRESET` + zero-padded id of the batch's first slot. Lets the catalog group a recurring series as one class instead of merging unrelated batches that share a subject |

> `EndTime` through `PresetGroupId` are only populated for tutor-preset class slots (Flow B) — the slot a tutor publishes ahead of time that a parent can book directly, without a per-request confirmation step.

### StudentPreferredModes

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `StudentId` | INT (FK → Students.Id, CASCADE DELETE) | |
| `Mode` | VARCHAR(50) | `Online` \| `Home Visit` \| `Tutor Place` \| `Tuition Center` |
| `Sequence` | INT | Preference order, ascending — used to rank Flow B preset-class matches |

### Students

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `ParentUserId` | INT (FK → Users.Id) | |
| `Name` | VARCHAR | Child's full name |
| `BirthDate` | VARCHAR | Format `YYYY-MM-DD` |
| `School` | VARCHAR | Current school name |
| `EducationLevel` | VARCHAR | e.g. `Secondary 3` |
| `SubjectSelect` | VARCHAR | Comma-separated subjects needed |
| `LearningGoal` | VARCHAR NULL | Parent-defined milestones |
| `PhotoUrl` | VARCHAR NULL | Profile photo URL |
| `IsArchived` | TINYINT(1) | Archived profiles are hidden from active lists/booking, not deleted (default `0`) |

### Bookings

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | Internal key |
| `BookingNumber` | VARCHAR(20) | Human ID — format `BOK00001` |
| `TutorId` | INT (FK → Tutors.Id, RESTRICT) | |
| `StudentId` | INT (FK → Students.Id, RESTRICT) | |
| `Subject` | VARCHAR | Subject + level string |
| `Mode` | VARCHAR | `Online` \| `Home Visit` \| `Tutor Place` |
| `Date` | VARCHAR | Session date `YYYY-MM-DD` |
| `Time` | VARCHAR | e.g. `04:00 PM - 05:00 PM` |
| `DurationHours` | DOUBLE | Default 1; widened from INT since 15-min-interval preset classes (e.g. 90 min) aren't whole hours |
| `Message` | VARCHAR NULL | Parent's notes to tutor |
| `TotalPrice` | DECIMAL(10,2) | sessions × price per session |
| `Status` | VARCHAR | `pending` \| `countered` \| `confirmed` \| `completed` \| `cancelled` |
| `SlotId` | INT NULL | Legacy; unused |
| `BookingType` | VARCHAR(20) | `parent-offer` (default) \| `tutor-preset` |
| `PresetSlotId` | INT NULL (FK → TutorTimeSlots.Id, RESTRICT) | Set when `BookingType = 'tutor-preset'` — legacy single-slot reference (always just the first slot when a booking spans more than one), kept for bookings created before `BookingPresetSlots` existed |

### BookingPresetSlots

One row per `TutorTimeSlot` a preset-class (Flow B) booking covers — lets a single `Booking` span an entire recurring series (e.g. all 5 occurrences of a weekly class) instead of one booking per occurrence, while still tracking exactly which slots need their seat freed if the booking is cancelled.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `BookingId` | INT (FK → Bookings.Id, CASCADE DELETE) | |
| `TutorTimeSlotId` | INT (FK → TutorTimeSlots.Id, CASCADE DELETE) | |

### CounterProposals

One-to-many log of every reschedule proposal made on a booking, by either party — not just the current one. A new proposal never overwrites an existing row: the previous `pending` row (if any) is marked `superseded` and a new row is inserted. At most one row per booking is ever `pending` at a time.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `BookingId` | INT (FK → Bookings.Id) | Many rows per booking (history) |
| `Date` | VARCHAR | Legacy; superseded by CounterProposalClasses |
| `Time` | VARCHAR | Legacy; superseded by CounterProposalClasses |
| `Message` | VARCHAR | Proposer's explanation |
| `ProposedBy` | VARCHAR(20) | `parent` \| `tutor` — derived server-side from the JWT, never trusted from the client |
| `Status` | VARCHAR(20) | `pending` \| `accepted` \| `superseded` \| `cancelled` |
| `CreatedAt` | DATETIME(6) | |

### LessonReports

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `BookingId` | INT (FK → Bookings.Id) | One-to-one with Booking |
| `Covered` | VARCHAR | Topics covered in session |
| `Performance` | VARCHAR | Student performance notes |
| `Homework` | VARCHAR | Assigned homework |
| `SubmitDate` | VARCHAR | Submission timestamp |

### LessonReportEdits

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `LessonReportId` | INT (FK → LessonReports.Id) | |
| `Date` | VARCHAR | Edit timestamp |
| `Changes` | VARCHAR | Description of changes made |

### IssueReports

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `BookingId` | INT (FK → Bookings.Id) | One-to-one with Booking |
| `IssueType` | VARCHAR | e.g. `Tutor was absent (No show)` |
| `Details` | VARCHAR | Full description |
| `Timestamp` | VARCHAR | Display-only, time-of-day (no date) |
| `CreatedAt` | DATETIME(6) | Real date/time — used by the AI Speed Match "Tutor Dispute (Refresh Monthly)" scoring criterion |
| `Resolved` | TINYINT(1) | Set true (not deleted) when admin resolves the dispute, so the Admin Archive tab keeps a record. A parent can edit/withdraw their report only while `Resolved = 0`; reporting again after resolution reopens the same row |
| `ResolvedAt` | DATETIME(6) NULL | When admin resolved it |

### ClassRemarks

A parent's rating + written remark on one specific completed class (`BookingClass`), not a whole booking — a multi-session booking gets one remark per session. Powers the tutor's public "Bulletin Board" and rolls up into `Tutors.Rating`.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `BookingClassId` | INT (FK → BookingClasses.Id, CASCADE DELETE) | Unique — one remark per class |
| `TutorId` | INT (FK → Tutors.Id, CASCADE DELETE) | |
| `ParentUserId` | INT | |
| `ParentDisplayName` | VARCHAR(200) | Masked from the parent's real name at creation time (see `NameMasking.Mask`) — stored, not recomputed on read, so a later name change doesn't retroactively alter past remarks |
| `Rating` | INT | 1–5 |
| `Text` | VARCHAR | Remark body |
| `Status` | VARCHAR(20) | `published` (visible everywhere) \| `dispute_requested` (tutor asked admin to hide it) \| `hidden` (admin approved the hide — stays visible to the tutor only, greyed out) |
| `DisputeReason` | VARCHAR NULL | Tutor's stated reason for requesting a hide; kept after resolution so the Archive tab can distinguish "never disputed" from "disputed and resolved" |
| `CreatedAt` | DATETIME(6) | |
| `EditedAt` | DATETIME(6) NULL | Set when the parent edits their remark |
| `ResolvedAt` | DATETIME(6) NULL | Set when admin resolves a `dispute_requested` hide request (approve or reject) |

Only `published` remarks count toward `Tutors.Rating` — and it's an **average of per-parent averages**, not a flat average: each parent's ratings are averaged within their own relationship first, then those per-parent averages are averaged together, so a long-running weekly family can't drown out another family's single rated class.

### ClassRemarkLikes

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `ClassRemarkId` | INT (FK → ClassRemarks.Id, CASCADE DELETE) | |
| `ParentUserId` | INT | |
| `CreatedAt` | DATETIME(6) | |

> Unique on `(ClassRemarkId, ParentUserId)` — a parent can like a given remark only once.

### Invoices

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | Internal key |
| `InvoiceNumber` | VARCHAR(20) | Human ID — format `INV00001` |
| `BookingId` | INT (FK → Bookings.Id) | One-to-one with Booking |
| `Date` | VARCHAR | Invoice date `YYYY-MM-DD` |
| `Amount` | DECIMAL(10,2) | Same as Booking.TotalPrice |
| `Status` | VARCHAR | `Unpaid` \| `Paid` \| `Refunded` |
| `Subject` | VARCHAR NULL | Copied from Booking.Subject |

### FavoriteTutors

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `ParentUserId` | INT (FK → Users.Id) | |
| `TutorId` | INT (FK → Tutors.Id) | |
| `CreatedAt` | DATETIME(6) | |

> Unique on `(ParentUserId, TutorId)` — a parent can favorite a given tutor only once.

### ChatMessages

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT | Conversation thread key |
| `ParentUserId` | INT | Conversation thread key — together with `TutorId` scopes messages to one parent–tutor pair (default `0` on rows predating this column) |
| `Sender` | VARCHAR | `parent` \| `tutor` \| `system` |
| `Text` | VARCHAR | Message body |
| `Timestamp` | VARCHAR | e.g. `Jun 21, 2026 3:00 PM` |

### Notifications

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `UserId` | INT (FK → Users.Id) | |
| `Title` | VARCHAR | Short heading |
| `Message` | VARCHAR | Full notification body |
| `Timestamp` | VARCHAR | Format `YYYY-MM-DD hh:mm tt` |
| `Type` | VARCHAR | `booking` \| `message` \| `payment` \| `system` |
| `IsRead` | TINYINT(1) | Read/unread flag |

### Payouts

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | |
| `Date` | VARCHAR | Payout date |
| `Amount` | DECIMAL(10,2) | |
| `Status` | VARCHAR | `Processing` \| `Paid` |

### PresetCancellationDecisions

Per-booking outcome when a tutor cancels a published preset-class slot (see `DELETE /api/tutors/{id}/slots/{slotId}`). One row per affected **booking**, not per cancellation event — a group class can have several independent families on the same slot, each deciding separately. `Original*`/`PricePerLesson` are snapshotted from the `TutorTimeSlot` at cancel time since that row is deleted immediately after.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `BookingId` | INT (FK → Bookings.Id, CASCADE DELETE) | |
| `OriginalDate` / `OriginalTime` / `OriginalEndTime` | VARCHAR | The class session that got cancelled |
| `PricePerLesson` | DECIMAL(10,2) | Snapshotted from the slot — drives the 20% penalty math |
| `ProposedDate` / `ProposedTime` / `ProposedEndTime` | VARCHAR NULL | NULL = straight cancel, no reschedule offered |
| `Status` | VARCHAR(20) | `pending` (awaiting parent) \| `accepted` \| `auto-accepted` (deadline passed, no response) \| `pending-admin` (parent rejected, awaiting admin) \| `resolved` (refund/penalty/dispute-score side effects have run) |
| `AcknowledgedAt` | DATETIME(6) NULL | Straight-cancel popup dismissal only — Path A decisions resolve via Status instead |
| `CreatedAt` / `DecidedAt` / `ResolvedAt` | DATETIME(6) | When created, when the parent/system decided, when the financial side effects ran |
| `AdminNote` | VARCHAR NULL | Optional note left when an admin resolves a `pending-admin` row |

### TutorPenalties

Deduction ledger against a tutor's future payout — e.g. the 20% penalty charged when a preset-class cancellation resolves toward a parent credit. Kept append-only rather than editing `Payouts` rows directly; `PayoutsController`'s available-balance calculation subtracts `SUM(Amount)` here. `BookingId` is a soft reference, not a hard FK — a penalty is a permanent ledger entry that should outlive the booking record it originated from.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id, CASCADE DELETE) | |
| `BookingId` | INT NULL | No FK constraint — see above |
| `Amount` | DECIMAL(10,2) | |
| `Reason` | VARCHAR | Human-readable explanation |
| `CreatedAt` | DATETIME(6) | |

### Institutions

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `Name` | VARCHAR | School / institution name |
| `Country` | VARCHAR | `Singapore` \| `Malaysia` |
| `Type` | VARCHAR | `Primary` \| `Secondary` \| `Junior College` \| `Polytechnic/Vocational` \| `University/Tertiary` |

### ScoringWeightages

AI Speed Match scoring config (Admin → Scoring Config page). Seeded once with 6 fixed rows (`rating`, `activeness`, `disputes`, `experience`, + 2 reserved `na` slots); only `Percent` is admin-editable thereafter — the point-scale bands each criterion converts to are fixed in code, not stored here.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `Key` | VARCHAR(20) UNIQUE | `rating` \| `activeness` \| `disputes` \| `experience` \| `na1` \| `na2` — stable identifier the match-score calculator switches on |
| `Label` | VARCHAR(100) | Display text for the admin panel |
| `Percent` | INT | Default 0; weightage this criterion contributes to the total score |
| `SortOrder` | INT | Display order |

---

## Features

### Parent
- Dashboard: upcoming sessions, student progress, active children profiles
- Add, edit, archive/unarchive, and set teaching-mode preferences for student profiles, with school search (Singapore & Malaysia institutions)
- Tutor catalog with search/filter by subject, mode, and rating — browse and open a tutor's profile first, then pick the child inside the booking form. Each tutor card also shows a preset-class slot strip for next month (grouped by `PresetGroupId` — one chip per Setup Class submission, not merged across unrelated batches that share a subject — with a hover tooltip listing every date/time and fill status) so a parent can spot an instantly-bookable class without leaving the catalog. Click a chip to select that class, then "View & Book" for a booking summary instead of the plain request form
- AI Speed Match: pick a child + subject, ranked results filtered by subject/level/country and teaching-mode overlap with the child's saved preferences, sorted by a live AI Speed Match score (highest first, price breaks ties) — each result also shows its own available-classes slot strip, selectable the same way as the catalog
- Favorite tutors for quick access
- Two booking flows: request a custom session with any tutor (needs their confirmation), or book one or more of a tutor's already-published preset class slots directly as a single booking (Flow B — auto-confirmed and paid immediately via the booking summary, no per-request approval)
- Full booking flow — multi-session support, classes per month, recurring weekly dates
- Session activity log with lesson reports; Reschedule isn't offered for preset-class (Flow B) bookings, since their schedule is fixed and possibly shared with other students
- "Pay Invoice" opens a payment summary for review before charging; Billing & Invoices is read-only history (payment only happens from Sessions & Activity)
- Forced dashboard popup whenever a tutor cancels a confirmed preset (Flow B) class — shown every time the parent lands on the dashboard until resolved. If the tutor proposed a replacement date, it's Accept (moves to the new date, defaults selected) or Reject (queued for admin review — nothing refunds automatically until an admin approves it); if the tutor cancelled with no replacement, it's an acknowledge-only notice that a credit is on its way. Ignoring an Accept/Reject choice past the proposed date/time auto-accepts it next time the dashboard loads
- Direct parent-tutor chat (auto-refreshing every few seconds so replies show up without a reload), scoped per child-tutor conversation
- Notification bell drawer
- Rate + write a remark on each completed class (Bulletin Board); edit or delete your own remark, like other parents' remarks
- Edit or withdraw your own reported issue on a booking while it's still awaiting admin review

### Tutor
- Interactive calendar (paid = green, unpaid = amber); day dot reflects *all* of a day's bookings, not just the first — any unpaid session takes priority over an all-paid day
- Accept or counter-propose booking requests; parents can counter-propose back in turn (no round limit)
- Confirm bookings (auto-generates invoice with INV number)
- Publish preset class slots parents can book directly, no per-request approval (Flow B) — the Setup Class popup uses a 30-minute grid per selected date; click a single slot, or click-and-drag down a column to combine several into one class (first slot's start to last slot's end). The grid grays out anything already occupied — both real confirmed bookings and the tutor's own previously-published slots — so a drag can't be extended through, or resubmitted over, something that's already there. Every slot from one submission shares a `PresetGroupId`
- Published-but-unbooked slots show on the main calendar as a pink-purple dot per day, with full detail (time, mode, fill count, price) in that day's detail panel — each published slot has a Cancel action that requires a choice: propose a replacement date/time (each affected family then decides independently to accept it or ask for a refund — no penalty either way unless a family declines), or cancel outright with nothing offered (every affected family is credited immediately, and the tutor is charged a 20% penalty against their payout balance plus a dispute-score hit)
- Submit and edit lesson reports (with audit trail)
- Teaching offerings builder (subject + level + mode + qualification + price)
- Online/offline visibility switch — going offline immediately hides the profile from parent search and blocks new bookings
- Stats dashboard: live AI Speed Match score (one decimal place) and Gold/Silver/Bronze tier badge — same formula and thresholds as the admin Scoring Config leaderboard, not a separate calculation — plus ready balance
- Direct parent-tutor chat, scoped per child-tutor conversation
- Bulletin Board of parents' ratings/remarks on your classes; request admin to hide a specific remark with a reason

### Admin
- Platform metrics (parents, tutors, sessions, revenue)
- Operations system log
- Tutor vetting queue (verify credentials)
- Dispute resolution desk, plus a separate queue for tutor requests to hide a specific parent remark (approve to hide, reject to keep it published)
- Scoring Config: set AI Speed Match weightage percentages per criterion (Rating, Activeness, Disputes, Experience), view the fixed point-scale reference tables, and a live Tutor Scores leaderboard showing every verified/online tutor's current score with its full breakdown
- Reschedule Rejections queue: a separate desk (not the general dispute log) for preset-class reschedules a parent declined — resolving one is what actually issues the refund and charges the tutor's 20% penalty; nothing financial happens from the parent's Reject click alone
- Archive tab: resolved booking disputes and resolved remark-hide requests are kept here (marked resolved, never deleted) for audit/history instead of disappearing once acted on

### Architecture
- JWT authentication (7-day tokens, role-based routes)
- Repository pattern (`IRepository<T>` → `Repository<T>`)
- Auto-seeded database with tutors, students, institutions, notifications
- EF Core with Pomelo MySQL provider
- Human-readable reference numbers: `BOK00001` (bookings), `INV00001` (invoices)

---

## EF Core Migrations (Optional)

To use migrations instead of `EnsureCreated`:

```bash
cd backend/LearnSphere.API
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## Production Notes

- Change `Jwt:Key` to a long random secret (32+ characters)
- Use environment variables to override `appsettings.json` in production
- Enable HTTPS and update CORS policy origins accordingly
