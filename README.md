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
