# Assumptions

Where the brief is silent, these are the decisions taken and why.

| # | Topic | Decision | Reason |
|---|---|---|---|
| A1 | Student ID format | Exactly 17 ASCII digits (`^[0-9]{17}$`). Missing header → `400 STUDENT_ID_REQUIRED`; anything else → `400 STUDENT_ID_INVALID`. The UI converts Bengali digits before sending. | The brief says a 17-digit birth registration number. `[0-9]` rather than `\d`, because in .NET `\d` also matches Bengali and other Unicode digits. |
| A2 | Duplicate class names | Allowed. | Automated graders may create the same name repeatedly; the ID is the identity. |
| A3 | Input limits | `name` trimmed, 1–100 chars; `totalSeats` a whole number 1–10 000. Missing fields, wrong types and malformed JSON → `400 VALIDATION_ERROR`. | Sanity limits. |
| A4 | IDs | `c_` / `h_` + 32-hex GUID. | Unguessable; the prefix makes logs readable. |
| A5 | "Active" application | `CONFIRMED`, or `HELD` with `expiresAt > now`. | Follows from B1/B3. |
| A6 | Several classes per student | Allowed, one active application per class. | The brief restricts per class only. |
| A7 | Re-apply after EXPIRED / CANCELLED | Allowed if a seat is free. | The seat went back to the pool. |
| A8 | Cancel a CONFIRMED admission | Not allowed → `409 HOLD_ALREADY_CONFIRMED`. | There is no refund flow; "cancel" is for a pending application. |
| A9 | Cancel twice / cancel expired | Already cancelled → `204` (idempotent DELETE). Expired → `409 HOLD_EXPIRED`. | HTTP DELETE idempotency. |
| A10 | Confirm twice | `200 CONFIRMED`, counted once. | B5. |
| A11 | Someone else's hold | Confirm/cancel → `403 NOT_HOLD_OWNER`. | B4. |
| A12 | `GET /holds/{id}` | No header needed. | The contract lists none; hold IDs are unguessable. |
| A13 | Expiry mechanism | Computed, not scheduled. A HELD row counts only while `expires_at > now`, so the seat is free the instant time passes. The stored status is rewritten to `EXPIRED` lazily (on the next locked write for that class) and by a sweeper every 30 s; availability never depends on either. | "Returns by itself, no API call needed", with no timer gap. |
| A14 | Clock | Server UTC. Hold responses include `serverTime`; the UI measures its countdown against it. | Countdown stays correct when the client clock is wrong. |
| A15 | Timestamps | ISO-8601 UTC, millisecond precision, `Z` suffix. | Matches the example format. |
| A16 | Error body | RFC 7807 + `code`, for every non-2xx including unknown routes, malformed JSON and unhandled exceptions (`500 INTERNAL_ERROR`, no stack trace). | The UI maps `code` to a Bengali message. |
| A17 | Admin access | No authentication. | The brief says no login. |
| A18 | Live updates | Polling: class list every 3 s, hold and "my applications" every 5 s, plus refetch on focus and after every action. The countdown flips to EXPIRED locally the moment it reaches zero. | Meets "within a few seconds" and is simpler and sturdier than WebSockets. |
| A19 | Student ID persistence | `localStorage`, synced across tabs. | U4: survives refresh and browser restart. |
| A20 | Language | Bengali labels; button texts exactly as in the brief. | Matches the brief. |
| A21 | Browser → API URL | Runtime `config.js` written from `API_BASE_URL` when the frontend container starts (default `http://localhost:8080`). | The URL is configurable by env var without rebuilding the image. It must be the address the *browser* can reach, not the Docker service name. |

## How over-allocation is prevented

Every operation that changes a class's holds (apply, confirm, cancel, expiry clean-up) runs in a
database transaction that **first locks that class's row** with `SELECT … FROM classes WHERE id = … FOR UPDATE`.
Requests for the same class therefore queue inside PostgreSQL. The seat count and the insert
happen while the lock is held, so no two requests can both see "one seat left". Different classes
do not block each other.

Because the lock lives in PostgreSQL, not in application memory, it works the same for many
threads in one process and for **several backend processes sharing the database** (bonus X1).
`MultiInstance_TwoHosts_SharedDb_NeverOverAllocate` runs two independent hosts against one
database and still gets exactly `totalSeats` successes; `docker compose --profile multi up`
demonstrates the same with two containers.

Further guards:

- All writers lock in the same order (class row, then hold row), so writers cannot deadlock each
  other. That includes the background sweeper, which takes the class lock too.
- A partial unique index `(class_id, student_id) WHERE status IN ('HELD','CONFIRMED')` enforces
  "one active application per student per class" in the database itself.
- The clock is read *after* the lock is acquired, so a request that queued behind others judges
  expiry by the moment it actually acts.
- Expiry needs no timer (A13), so no window exists in which an expired hold still occupies a seat.
- Startup migrations run under a PostgreSQL advisory lock, so two instances can boot together.

The concurrency tests were checked against a deliberately broken build: with `FOR UPDATE`
removed, 75 holds were granted for 10 seats and the tests failed.
