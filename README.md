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
| GET | `/api/tutors` | — | List all tutors (filter: `subject`, `mode`, `search`, `rating`) |
| GET | `/api/tutors/{id}` | — | Get tutor by ID (public) |
| GET | `/api/tutors/by-user/{userId}` | JWT | Get tutor profile by user ID (self-retrieve) |
| PUT | `/api/tutors/{id}` | JWT | Update tutor profile (bio, image, price, subjects, levels, modes, qualifications) |
| DELETE | `/api/tutors/{id}` | JWT | Delete tutor account |
| GET | `/api/tutors/booking` | JWT | Get tutor's bookings |
| GET | `/api/tutors/{id}/slots` | JWT | Get full timetable |
| POST | `/api/tutors/{id}/slots` | JWT | Add a timetable slot (validates no clash with existing slots) |
| DELETE | `/api/tutors/{id}/slots/{slotId}` | JWT | Remove a timetable slot |

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
| DELETE | `/api/students/{id}` | JWT | Delete a student |
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
| PATCH | `/api/bookings/{id}/status` | JWT | Update status: `confirmed` / `cancelled` / `countered` |
| POST | `/api/bookings/{id}/lesson-report` | JWT | Submit a lesson report |
| PATCH | `/api/bookings/{id}/lesson-report` | JWT | Edit an existing lesson report (audit trail saved) |
| POST | `/api/bookings/{id}/issue` | JWT | Report an issue on a booking |

### Incidents

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/incident` | JWT | Report an issue on a booking |
| GET | `/api/incident` | JWT | Get all incidents |

### Chat

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/chat/{tutorId}` | JWT | Get chat messages for a tutor–parent thread |
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
| POST | `/api/payouts/request` | JWT | Request a payout |

### Admin

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/admin/stats` | JWT (admin) | Platform statistics (parents, tutors, sessions, revenue) |
| GET | `/api/admin/tutors/unverified` | JWT (admin) | List unverified tutors pending vetting |
| PATCH | `/api/admin/tutors/{id}/verify` | JWT (admin) | Verify a tutor |
| GET | `/api/admin/disputes` | JWT (admin) | List disputed bookings |
| PATCH | `/api/admin/disputes/{bookingId}/resolve` | JWT (admin) | Resolve a dispute |
| GET | `/api/admin/institutions` | — | Search institutions (filter: `country`, `type`, `search`) |

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

### TutorReviews

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | |
| `Author` | VARCHAR | Reviewer name |
| `Text` | VARCHAR | Review content |
| `Rating` | INT | 1–5 |

### TutorTimeSlots

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | |
| `Day` | VARCHAR | e.g. `Monday` |
| `Time` | VARCHAR | e.g. `10:00 AM` |
| `Status` | VARCHAR | `Available` \| `Booked` |
| `BookingId` | INT NULL | Set when slot is booked |

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
| `DurationHours` | INT | Default 1 |
| `Message` | VARCHAR NULL | Parent's notes to tutor |
| `TotalPrice` | DECIMAL(10,2) | sessions × price per session |
| `Status` | VARCHAR | `pending` \| `countered` \| `confirmed` \| `completed` \| `cancelled` |
| `SlotId` | INT NULL | FK to TutorTimeSlots |

### CounterProposals

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `BookingId` | INT (FK → Bookings.Id) | One-to-one with Booking |
| `Date` | VARCHAR | Counter-proposed date |
| `Time` | VARCHAR | Counter-proposed time |
| `Message` | VARCHAR | Tutor's explanation |

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
| `Timestamp` | VARCHAR | Report time |

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

### ChatMessages

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | Conversation thread key |
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

### Institutions

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `Name` | VARCHAR | School / institution name |
| `Country` | VARCHAR | `Singapore` \| `Malaysia` |
| `Type` | VARCHAR | `Primary` \| `Secondary` \| `Junior College` \| `Polytechnic/Vocational` \| `University/Tertiary` |

---

## Features

### Parent
- Dashboard: upcoming sessions, student progress, children profiles
- Add & edit student profiles with school search (Singapore & Malaysia institutions)
- Tutor catalog with search/filter by subject, mode, and rating
- Full booking flow — multi-session support, classes per month, recurring weekly dates
- Session activity log with lesson reports
- Invoice payment system with BOK/INV reference numbers
- Direct parent-tutor chat
- Notification bell drawer

### Tutor
- Interactive calendar (paid = green, unpaid = amber)
- Accept or counter-propose booking requests
- Confirm bookings (auto-generates invoice with INV number)
- Submit and edit lesson reports (with audit trail)
- Teaching offerings builder (subject + level + mode + qualification + price)
- Stats dashboard (sessions, rating, balance)
- Direct parent-tutor chat

### Admin
- Platform metrics (parents, tutors, sessions, revenue)
- Operations system log
- Tutor vetting queue (verify credentials)
- Dispute resolution desk

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
