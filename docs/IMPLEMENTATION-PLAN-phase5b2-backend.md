# Phase 5B-2 — BACKEND stream (Proctored quiz engine + quiz review)

> Run in its **own** Claude session, parallel with the frontend stream. Created 2026-06-20.
>
> **Read first:** `backend/CLAUDE.md` (domain model — `UserQuiz → QuizAttempt`; SignalR JWT + Redis backplane; the
> `≥` pass rule; Hangfire), and the **frozen contract** `docs/contracts/phase5b2-quizzes.md`.
> **Templates to mirror:** the **5B-1** assignment engine (`UserAssignment`/`AssignmentQuestion` snapshot,
> `EnrollmentSideEffects`, `AnswerQuestionHandler` IDOR, `AttendanceScoringHandler` System-actor grade,
> `assessment_events`); the Phase-4/5A read slices; the `QuizSetting`/`Question.IsValidForQuiz` bank.
>
> **File ownership:** `backend/**` only. Match the contract field-for-field. **The engine is API/hub-only — no
> quiz-taking UI.** This is the largest backend stream (SignalR + Redis + Hangfire); budget accordingly.

## Goal
The proctored quiz engine end-to-end: generated from the **prerequisite's** bank+settings on enrol, randomised
attempts, **server-side timer auto-submit** (Hangfire), **single-sitting forfeit-on-disconnect** (SignalR + Redis),
focus-loss telemetry (recorded, not auto-forfeit), **best-of** + **`≥` pass** (fixes #7) → videos-unlocked state,
all events audited; plus the admin **quiz review** + populated attendance quiz columns. Green gate:
`dotnet build -c Release` + `dotnet test -c Release` (Postgres **+ Redis** Testcontainers).

## What ALREADY exists (reuse, don't reinvent)
- **`EnrollmentSideEffects.GenerateAssessmentsAsync`** generates the assignment and explicitly leaves the
  **prerequisite-quiz** part a logged no-op (*"stays a no-op until 5B-2"*) — **make it real here**; reuse its
  `PickForm` variation strategy and idempotency pattern.
- **`QuizSetting`** (owned 1:1 by `Session`: `TimeLimitMinutes/QuestionCount/AttemptCount/MinPassPercent`) and
  **`Question.IsValidForQuiz`** (+ `Variations`/`Options`) — the quiz source. `Session.PrerequisiteSessionId` links B→A.
- **5B-1 patterns:** `AssessmentEvent` (append-only telemetry — add `FocusLost`/`FocusReturned` types);
  `AttendanceScoringHandler` (event → `Attendance` write, `IAuditViaEventOnly`); the `System`-actor audit-event
  mechanism (`ISystemActorAuditEvent`/equivalent the 5B-1 backend added); `RequireStudent`; IDOR via
  `currentUser.UserId`; signed image URLs.
- **Hangfire** is already wired (background jobs). **HybridCache** is L1-only today (*"wire Redis L2 when the caching
  phase arrives"* — that phase is now). There is **no SignalR/Redis yet** — build the hub correctly from scratch.

## Steps

### A1 — Domain: `UserQuiz` + `QuizAttempt` (immutable attempt snapshot)
`Domain/Entities/UserQuiz.cs` (`: TenantEntityBase`), `QuizAttempt` (owned); enum `QuizAttemptStatus
(InProgress|Submitted|Forfeited|TimedOut)`; events `QuizAttemptStartedEvent`/`SubmittedEvent` (**student** actor),
`QuizAttemptForfeitedEvent`/`TimedOutEvent` (**System** actor), `QuizGradedEvent`. Fields per contract §C.
- Factory `GenerateFor(tenantId, enrollment, gatedSession B, prerequisite A, aQuizSetting, aEligibleQuestions, now)`
  → snapshots A's `QuizSetting`; raises generation.
- `StartAttempt(pickQuestions, now)`: guard attempts-remaining + no active attempt (else throw → 409); draw a
  **random** `QuestionCount` subset of A's eligible questions, one variation each (immutable snapshot); set
  `DeadlineUtc = now + TimeLimit`; raise `StartedEvent`.
- `Answer(attemptId, aqId, optionId, now)` (guard `InProgress` + `now ≤ deadline`); `Submit(attemptId, now)` →
  grade %, status `Submitted`, recompute `BestPercent`=max, `AttemptsUsed`++, `Passed = BestPercent >= MinPassPercent`
  (**`≥`**, #7), raise `SubmittedEvent` + `QuizGradedEvent`; `Forfeit(attemptId, now)` (score 0, status `Forfeited`,
  consume) raise `ForfeitedEvent`; `TimeOut(attemptId, now)` (grade answered, status `TimedOut`) raise `TimedOutEvent`
  + `QuizGradedEvent`. A terminal attempt never re-opens (`FR-PLAT-QZ-009`).
- **Unit-test:** randomisation differs across attempts; best-of; **`≥` boundary (exactly min passes)**; snapshot
  immutable vs bank edits; attempts-exhausted; answer-after-terminal throws.

### A2 — Infrastructure: real quiz generation (extend `EnrollmentSideEffects`)
After the assignment block: if `session.PrerequisiteSessionId is Guid preId`, load A + its `QuizSetting` + eligible
questions (`IsValidForQuiz`, with `Variations`); if a `QuizSetting` exists **and** eligible questions exist →
`UserQuiz.GenerateFor(...)` (idempotent: skip if a `UserQuiz` exists for the enrollment). Else no quiz.

### A3 — Infrastructure: EF config + migration
`UserQuizConfiguration` (+ `OwnsMany(QuizAttempt)` → owned questions/options, immutable), DbSets `UserQuizzes`
(+ `QuizAttempts` navigation). Add the `FocusLost`/`FocusReturned` `AssessmentEventType` values. Indexes
`(TenantId, EnrollmentId)` unique on `UserQuiz`; `(TenantId, …)` as needed. Migration `AddQuizzes` (gated; build
Release / Infrastructure-as-startup per the VS-lock note — apply in wiring with `--configuration Release`).

### A4 — Infrastructure: Redis + SignalR wiring (issue #6 done right)
- **AppHost** (`SalahBahazad.AppHost/Program.cs`): `var redis = builder.AddRedis("redis", port: 6379).WithDataVolume();`
  then `api.WithReference(redis).WaitFor(redis)` (fixed dev port, mirroring Postgres/MinIO so wiring can reach it).
- **`InfrastructureServiceExtensions`:** `services.AddSignalR().AddStackExchangeRedis(redisConn)` (backplane,
  `NFR-SCAL-002`); promote HybridCache to the Redis L2. **Hub JWT:** add `JwtBearerEvents.OnMessageReceived` that
  reads `access_token` from the query **only when `path.StartsWithSegments("/hubs/quiz")`** — validated as a normal
  JWT (NOT the legacy query-string-creds scheme; `NFR-SEC-005`).

### A5 — Api: `QuizHub` (`/hubs/quiz`)
`Api/Hubs/QuizHub.cs` (`[Authorize]`, student principal). On connect → join the caller's active attempt group;
track **connection↔attempt in Redis** (survives horizontal scale). `OnDisconnectedAsync` → **forfeit** the active
`InProgress` attempt (`FR-PLAT-QZ-004`) via the application layer. Push countdown ticks + the submit/timeout/forfeit
signal to the group. Map `app.MapHub<QuizHub>("/hubs/quiz")`.

### A6 — Infrastructure: authoritative timer (Hangfire)
On `StartAttempt`, schedule a Hangfire job at `DeadlineUtc` → if the attempt is still `InProgress`, **auto-submit**
(grade answered, status `TimedOut`, **System** actor) (`FR-PLAT-QZ-005`). Submit/forfeit **cancels** the job. The
job is the source of truth (survives a dropped connection or API restart).

### A7 — Application: engine CQRS (`Features/Quizzes/`, `RequireStudent`, IDOR-checked)
`GetMyQuiz` (by `sessionId` → `StudentQuizDto`, **no `isCorrect`**, image keys→signed URLs), `StartAttempt`
(`ITransactionalRequest`; 409 guards; schedule the A6 job), `AnswerQuizQuestion`, `SubmitAttempt`
(→ `QuizAttemptResultDto`), `RecordFocusEvent` (→ `assessment_events`, **never** forfeits). Every handler checks the
quiz/attempt's `StudentId == currentUser.UserId` (`NFR-SEC-007`).

### A8 — Application: attendance scoring + columns
`INotificationHandler<QuizGradedEvent>` → write `Attendance.BestQuizPercent` (`FR-PLAT-ATT-002`, `IAuditViaEventOnly`).
Update the **5B-1 attendance queries** (`ListSessionAttendance`/`ListStudentAttendance`) to **join `UserQuiz`** so
`bestQuizPercent` + `quizAttemptCount` are real (the DTOs already carry the fields — **no contract change**).

### A9 — Application: quiz review (`Features/Review/`, `AttendanceRead`)
`GetQuizReview(enrollmentId)` → `QuizReviewDto` (best/passed/minPass/attemptsUsed/allowed + per-attempt
number/score/time/flag/status/startedAt/isBest — contract §B). Extend the 5B-1 behaviour endpoint to surface the
quiz attempt's `FocusLost` events in the `scrReview` Behaviour tab.

### A10 — Api: endpoints
`Api/Endpoints/QuizEndpoints.cs` (`/api/me/quizzes`, **`RequireStudent`** — contract §A #1-5), extend
`ReviewEndpoints` with `/api/review/quizzes/{enrollmentId}` (`AttendanceRead` — #6), and `MapHub<QuizHub>`. `.Produces<>`
the shapes + `ProblemDetails`; confirm `RequireStudent`/hub-auth reject staff and vice-versa.

### A11 — Tests (Postgres **+ Redis** Testcontainers)
- **Unit:** best-of; **`≥` boundary**; randomisation; snapshot immutability; attempts-exhausted; terminal-attempt guard.
- **Integration** (mirror 5B-1 + add a **SignalR `HubConnection` test client** via `WebApplicationFactory`):
  - generation only when prereq has a `QuizSetting` + eligible questions; sourced from A; idempotent.
  - **happy path:** start (randomised subset, no `isCorrect`) → answer → submit → `scorePercent`/`bestPercent`;
    **pass at exactly `minPass`** unlocks (`Passed`/videos-unlocked state set); attempts-exhausted → **409**.
  - **timer auto-submit:** short limit → the Hangfire job submits → status `TimedOut`, **System** actor.
  - **forfeit-on-disconnect:** connect the hub, start, **dispose the connection** → active attempt `Forfeited`
    (score 0, consumed), **System** actor.
  - **focus-loss:** recorded in `assessment_events`, attempt **not** forfeited.
  - **audit (`FR-PLAT-QZ-010`):** start/submit = **student** actor; forfeit/timeout = **System**; focus-loss writes
    **no** audit row.
  - **admin:** quiz review shape + `isBest`/flags; **attendance `bestQuizPercent`/`quizAttemptCount` now real**.
  - **security:** tenant isolation; IDOR (student B → 403/404 on A's quiz/attempt); default-deny (anon→401, staff→403
    on `/api/me/*`, student→403 on `/api/review/*`; hub rejects a non-student / missing token).

## Exit criteria
All 6 REST endpoints + the `QuizHub` behave per contract; generation/best-of/`≥`/timer/forfeit/focus-loss all hold;
attendance quiz columns real; `dotnet build -c Release` + `dotnet test -c Release` green; Scalar shows the `Quizzes`
group + the hub. Hand to wiring.

## Out of scope (defer — documented)
- **Video playback gate** (`FR-PLAT-VID-*`, per-video decrement, signed HLS, handoff code) → **5C**; 5B-2 only sets
  the *videos-unlocked* state. **Videos-watched** attendance → 5C. Any **student quiz-taking UI** — engine is API-only.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the BACKEND stream of Phase 5B-2 (Proctored quiz engine + quiz review) for the Salah Bahzad
admin portal. This is the PROCTORED half of 5B — it ADDS SignalR + a Redis backplane + a Hangfire timer. Largest
backend stream so far.

Read first, in order:
1. backend/CLAUDE.md (domain model UserQuiz→QuizAttempt; SignalR JWT + Redis backplane; >= pass; Hangfire)
2. docs/contracts/phase5b2-quizzes.md (the FROZEN contract — build to it field-for-field)
3. docs/IMPLEMENTATION-PLAN-phase5b2-backend.md (your step-by-step, A1–A11)

Mirror the 5B-1 assignment engine (UserAssignment/AssignmentQuestion snapshot, EnrollmentSideEffects — make its
prerequisite-QUIZ no-op REAL, reusing PickForm; AnswerQuestionHandler IDOR; AttendanceScoringHandler System-actor
grade; assessment_events). Edit backend/** ONLY.

Deliver: Domain UserQuiz + QuizAttempt (immutable snapshot, best-of, >= pass [fixes #7], randomised attempts);
quiz generation from the PREREQUISITE's bank+settings (idempotent); migration AddQuizzes; Redis added to the AppHost
+ AddSignalR().AddStackExchangeRedis backplane + HybridCache L2; a JWT-authenticated QuizHub at /hubs/quiz
(OnDisconnectedAsync forfeits the active attempt with score 0 — FR-PLAT-QZ-004; access_token read ONLY for the hub
path, validated as a real JWT — issue #6 done right); a Hangfire job that auto-submits at the deadline (TimedOut,
System actor — FR-PLAT-QZ-005); Features/Quizzes (RequireStudent, IDOR, no isCorrect to students, focus-loss ->
assessment_events never auto-forfeit); QuizGradedEvent -> Attendance.BestQuizPercent + fill the attendance quiz
columns (no DTO change); Features/Review GetQuizReview; Api QuizEndpoints + MapHub; and IntegrationTests using a
SignalR HubConnection test client for the forfeit-on-disconnect + timer auto-submit, plus best-of/>=-pass/attempts-
exhausted/audit-actor-split/tenant-isolation/IDOR/default-deny. Use Postgres + Redis Testcontainers.

Green gate: `cd backend && dotnet build -c Release && dotnet test -c Release` (Docker for Testcontainers). Report
the forfeit-on-disconnect, timer-auto-submit(System), and >=-pass-boundary test results explicitly.
```
