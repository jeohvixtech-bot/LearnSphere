# LearnSphere – Tutor Matching Platform

**Stack:** AngularJS 1.x Frontend · ASP.NET Core 8 Web API · MySQL + Entity Framework Core

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

> **CORS note:** The backend allows requests from `http://localhost:3000` and `http://127.0.0.1:5500`. For other ports, add them to the `AllowFrontend` policy in `Program.cs`.

---

## Demo Credentials

| Role | Email | Password |
|------|-------|----------|
| Parent | sarah.tan@example.com | Parent@123 |
| Tutor | lim.ws@example.com | Tutor@123 |
| Admin | admin@learnsphere.sg | Admin@123 |

---

## API Reference

### Auth
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/login` | Login → returns JWT |
| POST | `/api/auth/register` | Register new account |

### Tutors
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/tutors` | List all (filters: `subject`, `mode`, `search`) |
| GET | `/api/tutors/{id}` | Get tutor by ID |
| GET | `/api/tutors/by-user/{userId}` | Get tutor profile by user |

### Students (JWT required)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/students` | Get parent's students |
| POST | `/api/students` | Create student profile |

### Bookings (JWT required)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/bookings` | Get bookings (role-filtered) |
| POST | `/api/bookings` | Create booking |
| PATCH | `/api/bookings/{id}/status` | Update status (confirm/counter/cancel) |
| POST | `/api/bookings/{id}/lesson-report` | Submit lesson report |
| PATCH | `/api/bookings/{id}/lesson-report` | Edit lesson report |
| POST | `/api/bookings/{id}/issue` | Report an issue |

### Chat (JWT required)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/chat/{tutorId}` | Get chat messages |
| POST | `/api/chat` | Send message |

### Notifications (JWT required)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/notifications` | Get user notifications |
| PATCH | `/api/notifications/mark-all-read` | Mark all read |

### Invoices (JWT required)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/invoices` | Get invoices (role-filtered) |
| POST | `/api/invoices/{id}/pay` | Pay invoice |

### Admin (JWT + admin role)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/admin/stats` | Platform statistics |
| GET | `/api/admin/tutors/unverified` | Unverified tutors queue |
| PATCH | `/api/admin/tutors/{id}/verify` | Verify a tutor |
| GET | `/api/admin/disputes` | Active disputes |
| PATCH | `/api/admin/disputes/{bookingId}/resolve` | Resolve dispute |
| GET | `/api/admin/institutions` | Search institutions (public) |

---

## Features

### Parent
- Dashboard: upcoming sessions, student progress, children profiles
- Add student profiles with school search (Singapore & Malaysia institutions)
- Tutor catalog with search/filter by subject and mode
- Full booking flow (date, time, subject, student selection)
- Session activity log with lesson reports
- Invoice payment system
- Direct parent-tutor chat
- Notification bell drawer

### Tutor
- Interactive June 2026 calendar (paid = green, unpaid = amber)
- Accept or counter-propose booking requests
- Confirm bookings (auto-generates invoice)
- Submit and edit lesson reports (with audit trail)
- Direct parent-tutor chat
- Stats dashboard (sessions, rating, balance)

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

---

## Database Schema

Database: **MySQL 8.0** · ORM: **Entity Framework Core (Pomelo provider)**  
All string dates are stored as `VARCHAR` (format `YYYY-MM-DD` or `hh:mm tt`). Decimal money fields use `DECIMAL(10,2)`.

---

### `Users`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `Email` | VARCHAR | Unique login email |
| `PasswordHash` | VARCHAR | BCrypt hash |
| `Role` | VARCHAR | `parent` \| `tutor` \| `admin` |
| `Name` | VARCHAR | Display name |
| `CreatedAt` | DATETIME | UTC timestamp |

---

### `Tutors`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `UserId` | INT (FK → Users.Id) | One-to-one with User |
| `ImageUrl` | VARCHAR | Profile photo URL |
| `Rating` | DOUBLE | Aggregate rating (0–5) |
| `ReviewCount` | INT | |
| `PricePerSession` | DECIMAL(10,2) | Derived from min Offering price |
| `ExperienceYears` | INT | |
| `Bio` | VARCHAR | Short biography |
| `IsVerified` | TINYINT(1) | Admin-verified flag |

---

### `TutorOfferings`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id, CASCADE) | |
| `Subject` | VARCHAR(200) | e.g. "Mathematics" |
| `Level` | VARCHAR(200) | e.g. "O-Level" |
| `Mode` | VARCHAR(200) | `Online` \| `Home Visit` \| `Tutor Place` |
| `Qualification` | VARCHAR(200) | e.g. "NIE Trained" |
| `Price` | DECIMAL(10,2) | Per-session price for this offering |

---

### `TutorSubjects`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | |
| `Subject` | VARCHAR | |
| `Price` | DECIMAL(10,2) (nullable) | Legacy per-subject price |

---

### `TutorLevels`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | |
| `Level` | VARCHAR | e.g. `Primary 5-6`, `O-Level` |

---

### `TutorModes`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | |
| `Mode` | VARCHAR | `Online` \| `Home Visit` \| `Tutor Place` |

---

### `TutorQualifications`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | |
| `Qualification` | VARCHAR | e.g. `NIE Trained`, `B.Sc. Mathematics` |

---

### `TutorReviews`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | |
| `Author` | VARCHAR | Reviewer name |
| `Text` | VARCHAR | Review content |
| `Rating` | INT | 1–5 |

---

### `TutorTimeSlots`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | |
| `Day` | VARCHAR | e.g. `Monday` |
| `Time` | VARCHAR | e.g. `10:00 AM` |
| `Status` | VARCHAR | `Available` \| `Booked` |
| `BookingId` | INT (nullable) | Set when slot is booked |

---

### `Students`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `ParentUserId` | INT (FK → Users.Id) | |
| `Name` | VARCHAR | Child's full name |
| `BirthDate` | VARCHAR | Format: `YYYY-MM-DD` |
| `School` | VARCHAR | Current school name |
| `EducationLevel` | VARCHAR | e.g. `Secondary 3` |
| `SubjectSelect` | VARCHAR | Comma-separated subjects needed |
| `LearningGoal` | VARCHAR (nullable) | Parent-defined milestones |
| `PhotoUrl` | VARCHAR (nullable) | Profile photo URL |

---

### `Bookings`
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
| `Message` | VARCHAR (nullable) | Parent's notes |
| `TotalPrice` | DECIMAL(10,2) | sessions × price per session |
| `Status` | VARCHAR | `pending` \| `countered` \| `confirmed` \| `completed` \| `cancelled` |
| `SlotId` | INT (nullable) | FK to TutorTimeSlots |

---

### `CounterProposals`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `BookingId` | INT (FK → Bookings.Id) | One-to-one with Booking |
| `Date` | VARCHAR | Counter-proposed date |
| `Time` | VARCHAR | Counter-proposed time |
| `Message` | VARCHAR | Tutor's explanation |

---

### `LessonReports`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `BookingId` | INT (FK → Bookings.Id) | One-to-one with Booking |
| `Covered` | VARCHAR | Topics covered |
| `Performance` | VARCHAR | Student performance notes |
| `Homework` | VARCHAR | Assigned homework |
| `SubmitDate` | VARCHAR | Submission timestamp |

---

### `LessonReportEdits`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `LessonReportId` | INT (FK → LessonReports.Id) | |
| `Date` | VARCHAR | Edit timestamp |
| `Changes` | VARCHAR | Description of changes made |

---

### `IssueReports`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `BookingId` | INT (FK → Bookings.Id) | One-to-one with Booking |
| `IssueType` | VARCHAR | e.g. `Tutor was absent (No show)` |
| `Details` | VARCHAR | Full description |
| `Timestamp` | VARCHAR | Report time |

---

### `Invoices`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | Internal key |
| `InvoiceNumber` | VARCHAR(20) | Human ID — format `INV00001` |
| `BookingId` | INT (FK → Bookings.Id) | One-to-one with Booking |
| `Date` | VARCHAR | Invoice date `YYYY-MM-DD` |
| `Amount` | DECIMAL(10,2) | Same as Booking.TotalPrice |
| `Status` | VARCHAR | `Unpaid` \| `Paid` \| `Refunded` |
| `Subject` | VARCHAR (nullable) | Copied from Booking.Subject |

---

### `ChatMessages`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | Conversation thread key |
| `Sender` | VARCHAR | `parent` \| `tutor` \| `system` |
| `Text` | VARCHAR | Message body |
| `Timestamp` | VARCHAR | e.g. `Jun 21, 2026 3:00 PM` |

---

### `Notifications`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `UserId` | INT (FK → Users.Id) | |
| `Title` | VARCHAR | Short heading |
| `Message` | VARCHAR | Full notification body |
| `Timestamp` | VARCHAR | `YYYY-MM-DD hh:mm tt` |
| `Type` | VARCHAR | `booking` \| `message` \| `payment` \| `system` |
| `IsRead` | TINYINT(1) | Read/unread flag |

---

### `Payouts`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `TutorId` | INT (FK → Tutors.Id) | |
| `Date` | VARCHAR | Payout date |
| `Amount` | DECIMAL(10,2) | |
| `Status` | VARCHAR | `Processing` \| `Paid` |

---

### `Institutions`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT (PK, AUTO_INCREMENT) | |
| `Name` | VARCHAR | School / institution name |
| `Country` | VARCHAR | `Singapore` \| `Malaysia` |
| `Type` | VARCHAR | `Primary` \| `Secondary` \| `Junior College` \| `Polytechnic/Vocational` \| `University/Tertiary` |

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
