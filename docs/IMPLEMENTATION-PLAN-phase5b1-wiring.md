# Phase 5B-1 — WIRING stream (prove Assignments + Attendance + review end-to-end)

> Run **after** the backend and frontend streams are independently green. Created 2026-06-20.
>
> **Read first:** the **frozen contract** `docs/contracts/phase5b1-assignments-attendance.md`, the two stream docs,
> and `docs/IMPLEMENTATION-PLAN-phase5a-wiring.md` (the proven technique: AppHost up, drive via `:4200/api`,
> **direct-JWT** incl. a **student** token, **`docker exec psql`** for DB ground truth — PascalCase quoted columns,
> Bash heredoc).
>
> **File ownership:** both sides, but **only to fix drift from the frozen contract** (never extend it). Log every fix.

## Goal
Prove end-to-end with **zero contract drift**: a student (driven by API, no student UI) is auto-issued an assignment
on enrollment, solves it, gets auto-graded → attendance; the prerequisite gate blocks/permits enrollment correctly;
and the admin attendance + review screens show it. Audit (`System`-actor grade), tenant isolation, IDOR, and
default-deny verified live.

## Stack & access (reuse the 5A run model)
- AppHost up (Postgres + pgAdmin + MinIO + API + Angular). **Apply the gated `AddAssignments` migration deliberately**
  (it does not auto-apply) — verify the three new tables (`user_assignments`, `assignment_questions`,
  `assessment_events`) in `__EFMigrationsHistory`. Drive through the stable **`:4200/api`** proxy.
- **Direct-JWT:** HS256 over the dev `Jwt:Secret`, claims `nameid/tenant_id/role/token_type` + `iss/aud/exp`. Mint a
  **Teacher** (tenant A `019ed7e6-98bb-7db2-afbb-575170e45a50`), a **second-tenant** Teacher, and a **Student** —
  **the student token's `nameid` MUST be a real tenant-A student id that holds an enrollment** (the engine scopes the
  assignment to `StudentId == caller`, unlike 5A's reads). Read student/enrollment ids from the DB via `docker exec`.

## Smoke checklist (script it; assert, don't eyeball)
1. **Generation:** enroll a student (redeem #12 or unlock) on a session **with a question bank** → a `UserAssignment`
   (+ `AssignmentQuestion` snapshots) is created (`GET /api/me/assignments/by-session/{sessionId}` as the student
   returns the questions, `status=InProgress`, **no `isCorrect` leaked**).
2. **Solve + auto-grade (student JWT):** `PUT …/answer` for each question; `POST …/events` for a couple of
   behaviour rows + time. On the **last** answer → `status=Completed`, and `Attendance.AssignmentScore` (percent) is
   written (verify via the admin matrix **and** DB). Re-`GET` resumes saved answers + accumulated time.
3. **Enrollment gate (`FR-PLAT-ENR-007`):** on a session whose **prerequisite has questions**, enrolling the student
   returns **409** until that prerequisite's assignment is `Completed`; then it succeeds. A prerequisite with **no
   questions** passes vacuously.
4. **Admin attendance (`AttendanceRead`):** `GET /api/attendance/sessions/{id}` (by session) and
   `/students/{id}` (by student) show the row with `assignmentPercent` set, `videosWatched=0/total`, quiz `null/0`.
   CSV **export** streams (`Content-Disposition`) **and** writes exactly one audit row (GET → explicit `IAuditWriter`).
5. **Admin review (`AttendanceRead`):** `GET /api/review/assignments/{enrollmentId}` shows per-question
   submitted-vs-correct (+ `isCorrect`, correct option, per-question mark), header score `correct/total` + marks +
   percent + `timeSpentSeconds`; `…/behaviour` returns the ordered timeline.
6. **Snapshot fairness (`FR-PLAT-SES-007`):** edit/delete a bank question on that session **after** enrollment →
   the existing `UserAssignment`/review is **unchanged**.
7. **Audit:** `AssignmentGenerated` + `AssignmentGraded` rows are attributed to **`System`** (`FR-PLAT-AUD-005`);
   per-answer/behaviour produce **no** audit rows (they live in `assessment_events`); the CSV export is audited.
8. **Security:** **tenant isolation** (2nd-tenant token sees none of tenant A's attendance/review); **IDOR** — a
   student token for student B is **403/404** on student A's assignment; **default-deny** — anon→401, a **staff**
   token→403 on `/api/me/*`, a **student** token→403 on `/api/attendance|review/*`.
9. **Frontend render:** load **Attendance** (both tabs, the cohort matrix, Export) and drill into **Review**
   (Assignment tab highlighting + score header + Behaviour timeline; Quiz tab disabled) against live data — and
   confirm the dashboard **"Open attendance"** quick action now resolves. **Zero** shape mismatches with the contract.

## Drift log
Record any contract mismatch + the one-line fix that returned a side to the contract. Target: **zero drift**.

## Exit criteria
All nine checks pass on the live stack; the gate + auto-grade(System) + snapshot fairness + isolation/IDOR/default-deny
hold; frontend renders with zero drift. Append a dated run log here, then mark Phase **5B-1 Met** in
`docs/IMPLEMENTATION-PLAN-admin-portal.md`.

---

## Run log — executed 2026-06-20

**Prerequisite gates green** (per the backend/frontend streams). Static drift review: backend **zero drift** (DTOs,
endpoints, permissions, params, the ENR-007 gate, System-actor auto-grade, IDOR, no-`isCorrect`-leak all match the
contract); frontend models **zero drift**.

**Drift FOUND + FIXED (the one real item).** The `feature-attendance` lib was built but **not reachable**: (1)
`app.routes.ts` had no `attendance` or `review/:enrollmentId` route, and (2) the lib's TS path alias was missing from
`tsconfig.base.json`. So the "Attendance" nav item and the dashboard "Open attendance" quick action dead-ended to the
dashboard via the `**` wildcard; `nx build` stayed green because the lib compiles independently of the app router.
**Fixed both** (added the two lazy routes gated `permissionGuard('AttendanceRead')`; registered
`@sb/admin-portal/feature-attendance`) — back onto frontend plan B5. AOT build re-validated green.

**Stack & migration.** AppHost up (Postgres 17.4 + pgAdmin + MinIO; Angular `:4200`; Postgres `:5432`). The gated
`AddAssignments` migration was **not** in the persisted dev DB → applied deliberately via
`dotnet ef database update --configuration Release` against the fixed AppHost connection
(`Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=DefaultConnection`); confirmed
`user_assignments` / `assignment_questions` / `assessment_events` created. Drove through the stable **`:4200/api`**
proxy with direct HS256 JWTs (Teacher, second-tenant, two students). **Test-fixture note:** Phase-4 enrollments were
created with the *stubbed* side-effect, so they hold **no** assignment and `unlock` correctly 409s on an
already-active row (FR-PLAT-ENR-006). To exercise the real engine we **refunded** Student Test's Session 1 enrollment
then **unlocked** it → `Extend` fired `EnrollmentExtendedEvent` → the real side-effect generated the assignment.

**Result: 21/21 logical checks PASS · ZERO product drift.** Live JSON matched the frozen shapes field-for-field.
The natural prerequisite chain (Session 1 [1q] → prereq Session 2 [0q]; Phase 3 [6q] → prereq Session 1 [1q]) drove
both gate paths:
1. **Enrollment gate (`FR-PLAT-ENR-007`):** unlocking **Phase 3** (prereq Session 1 has questions) → **409**
   "Complete the prerequisite assignment first"; after Session 1's assignment was Completed → the same unlock → **201**.
   Vacuous pass confirmed (Session 1's prereq Session 2 has 0 questions → unlock 201).
2. **Generation:** the Extend created a `UserAssignment` (1 snapshotted question), `status=InProgress`, **no
   `isCorrect` leaked** to the student shape.
3. **Solve → auto-grade (System):** `POST events` (204) + `PUT answer` → `status=Completed`, `answeredCount=1/1`;
   `Attendance.AssignmentScore` written (percent **0** — the smoke deliberately picked the first option, which was
   wrong, so grading correctly scored 0/1). DB: `AssignmentGenerated`/`AssignmentGraded` attributed to **`System`**
   (`FR-PLAT-AUD-005`); answers/behaviour wrote **0** audit rows (they live in `assessment_events`: Entered+Answered).
4. **Admin review:** per-question submitted-vs-correct (+`isCorrect`, correct option, mark), header score
   `0/1` + marks `0/1` + percent + `timeSpentSeconds` + `status=Completed`; behaviour timeline (Entered, Answered).
5. **Admin attendance:** by-session matrix row (`assignmentPercent=0`, `videosWatched=0/2`, `bestQuizPercent=null`,
   `quizAttemptCount=0`) and by-student breakdown (Session 1 present with the score); **CSV export 200 + audited**
   (`AttendanceExported`).
6. **Security:** **tenant isolation** — a second-tenant token gets **404** on tenant A's
   `/api/attendance/sessions/{id}` **and** `/students/{id}` (session/student invisible cross-tenant, stronger than an
   empty list). **IDOR** — a *different* student's token answering this assignment → **403**
   ("belongs to another student"). **Default-deny** — anon→**401** on `/api/me/*`; a **staff** token→**403** on
   `/api/me/*` (`RequireStudent`); a **student** token→**403** on `/api/attendance/*`.
7. **Frontend:** Angular SPA served **200** at `:4200`; the `/attendance` + `/review/:enrollmentId` routes resolve
   (post-fix), so the screens and the dashboard "Open attendance" quick action are now reachable.

**Drift log:** one item — the unrouted `feature-attendance` screens + missing TS alias — **fixed onto the plan**
(routes + alias added). The frozen contract was **not** amended; backend and frontend matched it field-for-field.
**Pre-existing (NOT 5B-1):** the shared dev DB still carries the Phase-4 UUIDv7 hash-chain-fork caveat.

**Two non-blocking notes for follow-up:** (a) the smoke's two reported "failures" were assertion-strictness bugs in
the test script (tenant-B returns 404 not empty-200; PowerShell `.Count` on a single `Where-Object` result), not
product issues — re-verified by inspecting the raw rows. (b) The 5A polish items still stand (the dashboard
"Open attendance" target now exists; `audit.presentation` phrase keys unchanged).

---

## Kickoff prompt (paste into a fresh Claude session at the repo root, after both streams are green)

```
You are running the WIRING stream of Phase 5B-1 (Assignments + Attendance + Assignment review) for Salah Bahzad.
Backend + frontend are green. Prove the slice end-to-end on the running Aspire stack with ZERO contract drift; fix
only drift (never extend the contract).

Read first:
1. docs/contracts/phase5b1-assignments-attendance.md (FROZEN contract)
2. docs/IMPLEMENTATION-PLAN-phase5b1-wiring.md (run book + 9-point smoke)
3. docs/IMPLEMENTATION-PLAN-phase5a-wiring.md (technique: AppHost up, :4200/api, direct-JWT incl. a student token,
   docker exec psql with PascalCase quoted columns via a Bash heredoc)

Bring up the stack; apply the gated AddAssignments migration; mint Teacher / second-tenant / Student JWTs — the
STUDENT token's nameid must be a real tenant-A student id that holds an enrollment (read ids from the DB). The
assignment engine is API-only (no student UI): drive GET by-session → PUT answer ×N → POST events with the student
token, assert auto-grade-on-last-answer writes Attendance.AssignmentScore. Then run the 9-point smoke: generation,
solve+auto-grade, the FR-PLAT-ENR-007 prerequisite gate (409 then pass; vacuous when no prereq questions), admin
attendance (matrix + audited CSV export), admin review (submitted-vs-correct + behaviour), snapshot fairness,
audit (AssignmentGenerated/Graded = System actor; answers/behaviour NOT audited), security (tenant isolation, IDOR,
default-deny 401/403), and frontend render of Attendance + Review (and the dashboard "Open attendance" now resolves).

Fix drift back ONTO the contract and log it. Append a dated run log to docs/IMPLEMENTATION-PLAN-phase5b1-wiring.md
and report the gate, auto-grade(System-actor), and IDOR results explicitly.
```
