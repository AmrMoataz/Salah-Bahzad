# Phase 5B-2 — WIRING stream (prove the quiz engine + quiz review end-to-end)

> Run **after** the backend and frontend streams are independently green. Created 2026-06-20.
>
> **Read first:** the **frozen contract** `docs/contracts/phase5b2-quizzes.md`, the two stream docs, and
> `docs/IMPLEMENTATION-PLAN-phase5b1-wiring.md` (the proven technique + its lessons: **verify new routes AND the
> tsconfig alias**, AppHost up + `:4200/api`, direct-JWT incl. a student token, **apply the gated migration with
> `dotnet ef --configuration Release`**, `docker exec psql` with PascalCase quoted columns via a Bash heredoc,
> **refund→unlock to regenerate** assessments for old enrollments).
>
> **File ownership:** both sides, **only to fix drift** (never extend the contract). Log every fix.

## Goal
Prove end-to-end with **zero contract drift**: a quiz is generated from the **prerequisite's** bank+settings on
enrol, a student (API-driven) takes randomised attempts, **best-of + `≥` pass** unlock the videos-state, focus-loss
is recorded (not forfeited), every event is audited with the right actor, and the admin **Quiz attempts** review +
the now-real attendance **Quiz** columns show it. Tenant isolation, IDOR, default-deny, and the **JWT-gated hub +
Redis backplane** verified live.

## Stack & access (5B-1 model + Redis)
- AppHost up — **now includes Redis** (the backend stream added it). Confirm the **redis** container is running and
  the API connected (Aspire dashboard / `docker ps`). Apply the gated **`AddQuizzes`** migration deliberately
  (`dotnet ef database update --configuration Release` with the fixed `ConnectionStrings__DefaultConnection`);
  confirm `user_quizzes` / `quiz_attempts` exist. Drive REST through **`:4200/api`**.
- **Direct-JWT:** Teacher, second-tenant, and a **Student** whose `nameid` is a real tenant-A student id.
- **Data setup for a quiz to exist:** a quiz generates for session **B** only if its prerequisite **A** has a
  `QuizSetting` **and** quiz-eligible questions. The dev chain is *Phase 3 smoke* (B) → prereq *Session 1* (A, 1q).
  If Session 1 has no quiz settings / its question isn't `IsValidForQuiz`, **set them via the Teacher API**
  (quiz-settings `PUT` + mark the question quiz-eligible), then enroll the student in B (**refund→unlock** if already
  active) so the side-effect generates the `UserQuiz`.

## Smoke checklist (script the HTTP parts; lean on the integration suite for the SignalR/timer parts)
1. **Generation:** after enrolling in B (prereq A has quiz settings + eligible questions), a `UserQuiz` exists for
   the enrollment, sourced from **A's** bank + settings; `GET /api/me/quizzes/by-session/{B}` (student) returns the
   settings, `attemptsRemaining`, `passed=false`, no active attempt.
2. **Attempt flow (student JWT, REST):** `POST …/quizzes/{quizId}/attempts` → randomised `questionCount` subset,
   server `deadlineUtc`, **no `isCorrect`** → `PUT …/answer` ×N → `POST …/submit` → `scorePercent`, `bestPercent`,
   `passed`. Run a **second** attempt → `bestPercent` = **max** (best-of). **Exhaust** attempts → start → **409**.
3. **`≥` pass + unlock state (fixes #7):** an attempt scoring **exactly `minPassPercent`** sets `passed=true` and the
   enrollment's **videos-unlocked** state (assert via DB/`UserQuiz.Passed`). (Video *playback* is 5C.)
4. **Focus-loss (`FR-PLAT-QZ-006`):** `POST …/attempts/{id}/focus` → recorded in `assessment_events`, the attempt is
   **not** forfeited (still `InProgress`).
5. **Admin (`AttendanceRead`):** `GET /api/review/quizzes/{enrollmentId}` → attempts table (number/score/time/flag/
   status/`isBest`), best/passed/min/used/allowed. **Attendance** `bestQuizPercent` + `quizAttemptCount` are now
   **real** (no longer null/0) in `GET /api/attendance/sessions/{B}` and `/students/{id}`.
6. **Audit (`FR-PLAT-QZ-010`):** `QuizAttemptStarted`/`Submitted` = **student** actor; `TimedOut`/`Forfeited` =
   **System**; focus-loss writes **no** audit row (it's in `assessment_events`). (Query the DB.)
7. **Hub + Redis live:** the `redis` container is up; `GET /hubs/quiz/negotiate` (or a WS upgrade) **without** a token
   → **401**, **with** the student token → **200/negotiation** (proves the hub's JWT gate). 
8. **SignalR semantics (forfeit-on-disconnect + timer auto-submit):** primarily proven by the **backend integration
   suite** (SignalR `HubConnection` test client + a short test time-limit). For a *live* check, best-effort: connect a
   small **`@microsoft/signalr`** (Node) or `.NET HubConnection` client → start an attempt → **dispose** → re-`GET`
   the quiz → the attempt is `Forfeited` (score 0, consumed). If a live SignalR client isn't feasible in-session,
   record that the semantics are covered by the green integration tests and the hub-auth check (#7).
9. **Security:** tenant isolation (second-tenant token → **404** on B's quiz review/attendance); **IDOR** (a different
   student's token on this quiz/attempt → **403/404**); default-deny (anon→401; **staff**→403 on `/api/me/*`;
   **student**→403 on `/api/review/*`).
10. **Frontend:** the `scrReview` **Quiz attempts** tab renders the attempts + flag pills + best marker; the
    attendance **Quiz best / Attempts** columns show real values; Behaviour tab shows quiz `FocusLost` rows. Zero
    shape mismatch.

## Drift log
Record any mismatch + the one-line fix that returned a side to the contract. **Re-check (5B-1 lesson):** if a new
route/component or lib alias was added, confirm `app.routes.ts` **and** `tsconfig.base.json`. Target: zero drift.

## Exit criteria
The REST flow + admin surface pass live; best-of/`≥`-pass/attempts-exhausted/focus-loss hold; audit actor split,
tenant isolation, IDOR, default-deny verified; the hub is JWT-gated and Redis is up; the forfeit/timer semantics are
green in the integration suite (+ best-effort live). Append a dated run log here, then mark Phase **5B-2 Met** in
`docs/IMPLEMENTATION-PLAN-admin-portal.md`.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root, after both streams are green)

```
You are running the WIRING stream of Phase 5B-2 (proctored quiz engine + quiz review) for Salah Bahzad. Backend +
frontend are green. Prove the slice end-to-end on the running Aspire stack with ZERO contract drift; fix only drift.

Read first:
1. docs/contracts/phase5b2-quizzes.md (FROZEN contract)
2. docs/IMPLEMENTATION-PLAN-phase5b2-wiring.md (run book + 10-point smoke)
3. docs/IMPLEMENTATION-PLAN-phase5b1-wiring.md (technique + lessons: verify routes AND tsconfig alias; AppHost up,
   :4200/api, direct student JWT; apply migration with `dotnet ef --configuration Release`; docker exec psql with
   PascalCase quoted columns via a Bash heredoc; refund→unlock to regenerate assessments)

Confirm the new **redis** container is up and apply the gated AddQuizzes migration. A quiz generates for session B
only if its prerequisite A has a QuizSetting + quiz-eligible questions — set those on the dev chain (Phase 3 smoke ->
prereq Session 1) via the Teacher API if needed, then enroll the student (refund->unlock if already active). Drive
the REST flow with a student JWT: GET quiz state -> start attempt (randomised, no isCorrect, deadline) -> answer ->
submit -> best-of -> a >=-minPass attempt sets passed + videos-unlocked state -> exhaust attempts (409) -> focus
event (assessment_events, NOT forfeited). Verify admin GET /api/review/quizzes/{enrollmentId} + the now-real
attendance Quiz columns; audit actor split (start/submit=student, timeout/forfeit=System, focus-loss not audited);
the hub is JWT-gated (negotiate 401 without token, ok with); tenant isolation (404), IDOR (403), default-deny.
Forfeit-on-disconnect + timer auto-submit are primarily proven by the green backend integration suite (SignalR test
client) — do a best-effort live SignalR connect+dispose if feasible, else record the integration coverage + the
hub-auth check.

Fix drift back ONTO the contract and log it. Append a dated run log to docs/IMPLEMENTATION-PLAN-phase5b2-wiring.md
and report the best-of, >=-pass-boundary, and audit-actor-split results explicitly.
```
