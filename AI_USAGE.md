# AI Usage

> **TODO(owner): review and edit.** This draft was written by the AI assistant from its working
> log. Confirm every statement is accurate, and add anything you did yourself, before submitting.

## Tools

- **Claude (Anthropic), via Claude Code:** planning (the implementation plan), and writing most of
  the backend, frontend, tests, Docker setup and documentation.

## Where AI was used

| Area | AI involvement |
|---|---|
| Plan, API contract, assumptions | Drafted with AI from the brief, then used as the spec. |
| Backend (.NET API, locking, migrations, sweeper) | Written by AI, checked by tests and manual `curl` runs. |
| Backend tests (incl. concurrency, multi-instance) | Written by AI; checked by deliberately breaking the lock (see below). |
| Frontend (React pages, hooks, error handling) | Written by AI, checked by Vitest and by a scripted headless-browser walkthrough. |
| Docker, compose, nginx, docs | Written by AI, verified by running the stack. |

## Where the AI was wrong, and how it was caught

1. **Student ID regex accepted Bengali digits.** The plan specified `^\d{17}$`. In .NET, `\d`
   matches any Unicode digit, so `২০১৫…` would have passed server-side validation. Caught while
   implementing; changed to `^[0-9]{17}$` and covered by `MissingOrBadStudentId_Returns400`.
2. **Sweeper could deadlock with writers.** The plan's sweeper ran one global `UPDATE` over all
   overdue holds without taking the class lock, so it could lock hold rows in a different order
   from a request holding a class lock. Caught while reviewing lock ordering during implementation;
   the sweeper now locks each class row first, like every other writer.
3. **Frontend container crash-looped.** The first `40-runtime-config.sh` used a `sed` escaping
   expression that busybox `sed` rejects (`bad option in substitution expression`). Caught by
   checking `docker compose ps` (frontend "Restarting") and the container logs; replaced with a
   simple `tr` filter and tested in an Alpine container.
4. **A frontend test asserted the wrong thing.** The "confirms on pay" test's mock kept returning
   the old HELD hold on the refetch that follows a payment, so the badge reverted. The app was right
   (the real server returns CONFIRMED); the mock was fixed.
5. **A false alarm in browser QA.** The scripted browser check reported that a fresh hold was
   already EXPIRED. Timestamps showed the script itself had stalled 30 s: Playwright's `isEnabled()`
   on a missing element waits for its default timeout. The script was fixed, not the app.
6. **Proving the concurrency tests are not vacuous.** Passing tests alone do not show they can
   fail. With `FOR UPDATE` removed temporarily, three tests failed (75 holds for 10 seats; 59 for
   15 across two hosts), which confirms they detect over-allocation.

## Verification done

- Backend: 30 xUnit tests green locally and in Docker (`backend-tests`).
- Frontend: 36 Vitest tests green locally and in Docker (`frontend-tests`).
- Headless-browser walkthrough of U1–U5 against the Docker stack with 30-second holds: apply,
  live count update in a second window (about 3 s), countdown, EXPIRED without reload, pay, cancel,
  refresh/deep link, new tab, foreign hold, 404s, backend stopped (message shown, no blank page).
- Two real backend containers on one database: 100 concurrent applications for 7 seats → exactly 7
  succeeded.
