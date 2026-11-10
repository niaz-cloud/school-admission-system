# School Admission System

Online admission for a private school on a **first-come-first-served** basis. A guardian applies
for a seat in a class (each shift is its own class), which **holds** the seat for a limited time.
Paying the admission fee within that time **confirms** it; otherwise the seat returns to the pool
by itself.

- **Backend:** C# / .NET 8 Minimal API, EF Core 8 + Npgsql, PostgreSQL 16
- **Frontend:** React 18 + TypeScript (Vite), React Router, TanStack Query
- **Seat safety:** every write to a class's holds runs under a PostgreSQL row lock
  (`SELECT … FOR UPDATE` on the class row), so `held + confirmed` can never exceed `totalSeats`,
  even with many backend processes. See [ASSUMPTIONS.md](ASSUMPTIONS.md#how-over-allocation-is-prevented).

## Run it (Docker only)

```bash
docker compose up --build
```

| What | URL |
|---|---|
| **Frontend (open this)** | **http://localhost:3000** |
| Backend API | http://localhost:8080 |
| Health check | http://localhost:8080/health |

Data lives in the `pgdata` volume and survives `docker compose down` / `up`.
To wipe everything: `docker compose down -v`.

To watch expiry quickly, start with a short hold:

```bash
HOLD_DURATION_SECONDS=30 docker compose up --build        # bash / zsh
$env:HOLD_DURATION_SECONDS=30; docker compose up --build  # PowerShell
```

## Using the UI

1. Type a 17-digit birth registration number in the box at the top (e.g. `20152691234567890`)
   and press **সংরক্ষণ**. Bengali digits (০-৯) are accepted too. The number is remembered in the
   browser, so it survives a refresh or a browser restart.
2. **নতুন class তৈরি (admin)** → create a class with a name and seat count.
3. **শ্রেণি তালিকা** lists every class with total / held / confirmed / free seats, refreshed every
   3 seconds. Press **আবেদন করুন** to apply.
4. The application page shows the status and a live countdown. **ভর্তি ফি দিন** confirms the seat
   (no real payment gateway). **বাতিল** releases it after an inline confirmation. When time runs out
   the page switches to **মেয়াদ শেষ** on its own and the pay button is disabled.
5. **আমার আবেদন** lists all of this student's applications, newest first.

Every error (no seats, expired, invalid input, server unreachable) is shown as a Bengali message
on the page.

## Running the tests

All test suites run in Docker; nothing else needs to be installed.

```bash
# Backend: 30 xUnit tests against a throwaway PostgreSQL (includes concurrency + multi-instance)
docker compose --profile test run --rm backend-tests

# Frontend: 36 Vitest + React Testing Library tests
docker compose --profile test run --rm frontend-tests

# Clean up the test database container afterwards
docker compose --profile test down
```

Without Docker (needs .NET 8 SDK and Node 20+):

```bash
# a PostgreSQL that allows ~300 connections, on port 5440 (or set TEST_DATABASE_URL)
docker run -d --name sa-test-pg -p 5440:5432 -e POSTGRES_PASSWORD=postgres postgres:16-alpine -c max_connections=300
cd backend && dotnet test

cd frontend && npm ci && npm test
```

`TEST_DATABASE_URL` overrides the backend test connection string. The tests never use an
in-memory database: EF InMemory and SQLite do not implement `SELECT … FOR UPDATE`, so the
concurrency tests would pass for the wrong reason.

### What the concurrency tests prove

| Test | Scenario | Asserted |
|---|---|---|
| `Concurrency_ManyStudents_NeverOverAllocate` | 200 simultaneous applications, 10 seats | exactly 10 × 201, 190 × 409 `SEATS_FULL`; 10 active rows in the DB |
| `Concurrency_SameStudent_OnlyOneHold` | 50 simultaneous applications from one student | exactly 1 × 201, rest `ALREADY_APPLIED` |
| `Concurrency_ConfirmRace_CountsOnce` | 20 simultaneous confirms of one hold | all 200, `confirmedSeats == 1` |
| `Concurrency_MixedHoldsCancelsAndConfirms_NeverOverAllocate` | waves of applies racing cancels/confirms | never more than `totalSeats` used |
| `MultiInstance_TwoHosts_SharedDb_NeverOverAllocate` | two separate API hosts, one DB, 200 requests | exactly `totalSeats` successes |

As a sanity check, removing `FOR UPDATE` from the lock query makes three of these fail
(75 holds granted for 10 seats), so they really do detect over-allocation.

## Two backend processes (bonus)

```bash
docker compose --profile multi up --build
```

This starts a second API on **http://localhost:8081** that shares the same database. Requests
spread over both ports still never exceed a class's seats: the lock lives in PostgreSQL, not in
either process. Both instances also migrate safely at startup (advisory lock around migrations).

## Configuration

| Variable | Service | Default | Meaning |
|---|---|---|---|
| `ConnectionStrings__Default` | backend | `Host=localhost;Port=5432;Database=school_admission;Username=postgres;Password=postgres` | PostgreSQL connection |
| `HOLD_DURATION_SECONDS` | backend | `600` | How long a hold lasts |
| `SWEEPER_INTERVAL_SECONDS` | backend | `30` | How often stored statuses of overdue holds are tidied (seat counts never depend on it) |
| `CORS_ALLOWED_ORIGINS` | backend | `*` | Comma-separated origins, or `*` |
| `ASPNETCORE_HTTP_PORTS` | backend | `8080` | Listening port |
| `API_BASE_URL` | frontend | `http://localhost:8080` | Backend URL **as seen by the browser**; written to `/config.js` when the container starts, so no rebuild is needed |
| `VITE_API_BASE_URL` | frontend (dev) | — | Build-time fallback for `npm run dev` |

## API

All JSON, camelCase. Student identity is the `X-Student-Id` header (17 ASCII digits).
Every non-2xx response is RFC 7807 with a machine-readable `code`:

```json
{ "type": "about:blank", "title": "Conflict", "status": 409, "code": "SEATS_FULL", "detail": "No seats are available in this class." }
```

| Method & path | Header | Success | Errors |
|---|---|---|---|
| `POST /classes` `{name,totalSeats}` | – | `201` + `Location` | `400 VALIDATION_ERROR` |
| `GET /classes` | – | `200` array, oldest first | – |
| `GET /classes/{classId}` | – | `200` | `404 CLASS_NOT_FOUND` |
| `POST /classes/{classId}/holds` | required | `201` | `400 STUDENT_ID_*`, `404 CLASS_NOT_FOUND`, `409 SEATS_FULL`, `409 ALREADY_APPLIED` (+`holdId`) |
| `GET /holds/{holdId}` | – | `200` | `404 HOLD_NOT_FOUND` |
| `POST /holds/{holdId}/confirm` | required | `200` | `400 STUDENT_ID_*`, `403 NOT_HOLD_OWNER`, `404 HOLD_NOT_FOUND`, `409 HOLD_EXPIRED`, `409 HOLD_CANCELLED` |
| `DELETE /holds/{holdId}` | required | `204` | `400 STUDENT_ID_*`, `403 NOT_HOLD_OWNER`, `404 HOLD_NOT_FOUND`, `409 HOLD_EXPIRED`, `409 HOLD_ALREADY_CONFIRMED` |
| `GET /me/holds` | required | `200` array, newest first | `400 STUDENT_ID_*` |
| `GET /health` | – | `200 {"status":"ok"}` | – |

`status` is always one of `HELD | CONFIRMED | EXPIRED | CANCELLED` and is the *effective* status:
a hold whose time has passed reads as `EXPIRED` immediately. Hold responses also carry
`serverTime`, which the UI uses to correct its countdown for client clock skew.

## Project structure

```
backend/
  src/SchoolAdmission.Api/
    Program.cs                  wiring: JSON, errors, CORS, endpoints, startup migration
    Domain/                     SchoolClass, Hold, HoldStatus
    Data/                       AppDbContext, migrations, DbInitializer (advisory-locked migrate)
    Features/Classes/           create / list / get, seat-count query
    Features/Holds/             HoldService (row locking), endpoints, DTOs
    Infrastructure/             error bodies, exception handler, X-Student-Id, sweeper, options
  tests/SchoolAdmission.Api.Tests/   xUnit against real PostgreSQL
frontend/
  src/api/                      typed client + endpoints
  src/lib/                      Bengali error messages, time/skew helpers, student ID helpers
  src/hooks/                    useStudentId (localStorage), useCountdown
  src/components/               StudentIdBar, StatusBadge, Countdown, alerts, toasts, error boundary
  src/pages/                    class list, create class, application, my applications
  nginx.conf, docker-entrypoint.d/   SPA fallback and runtime API URL
docker-compose.yml              db + backend + frontend; `test` and `multi` profiles
```
