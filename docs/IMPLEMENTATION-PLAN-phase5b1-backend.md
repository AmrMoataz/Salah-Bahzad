# Phase 5B-1 — BACKEND stream (Assignments engine + Attendance + Assignment review)

> Run in its **own** Claude session, parallel with the frontend stream. Created 2026-06-20.
>
> **Read first:** `backend/CLAUDE.md` (domain model — note the planned `UserAssignment → AssignmentQuestion` +
> `AssessmentEvent` aggregates — audit rules, business rules) and the **frozen contract**
> `docs/contracts/phase5b1-assignments-attendance.md`.
> **Templates to mirror:** the `Session`/`Question` aggregates (snapshot-style owned children); the Phase-4
> `Enrollment` aggregate + `EnrollmentSideEffectsHandler` (the event seam you make real); the Phase-4
> `Features/Codes` read+export slice (`ListCodesHandler`/`CodeListProjector`/`ExportCodesHandler` with explicit
> `IAuditWriter`); the Phase-5A `Features/Audit` read slice.
>
> **File ownership:** `backend/**` only. Match the frozen contract field-for-field.

## Goal
Build the open-book **assignment engine** (no SignalR/Redis) end-to-end and the admin **attendance** + **assignment
review** read APIs. Make the Phase-4 `IEnrollmentSideEffects` stub **real**, auto-grade → `Attendance`, and enforce
the `FR-PLAT-ENR-007` prerequisite gate. Green gate: `dotnet build -c Release` + `dotnet test -c Release`.

## What ALREADY exists (reuse, don't reinvent)
- **`IEnrollmentSideEffects.GenerateAssessmentsAsync(enrollmentId)`** + `EnrollmentSideEffectsHandler` (subscribes to
  `EnrollmentCreatedEvent` **and** `EnrollmentExtendedEvent`) — currently `StubEnrollmentSideEffects` (logs only).
  **Replace the stub** with the real generator; keep the prerequisite-**quiz** part a no-op (5B-2).
- **`Attendance`** shell (per `(student, session)`, `IAuditViaEventOnly`): nullable `AssignmentScore`,
  `BestQuizPercent`, `VideosWatched` — 5B-1 writes `AssignmentScore` (percent). Created already on enroll.
- **`Question`** (root, soft-deletable) owns `QuestionOption` (`Order/Text/IsCorrect`) + `QuestionVariation` (own
  options); `Mark`, `IsValidForQuiz`, `HintUrl`, `BodyLatex`, `ImageObjectKey`. The **snapshot source**.
- **`Session.PrerequisiteSessionId`** (self-ref, Phase-3) for the gate; `Session.Videos` for the matrix's
  `videosTotal`; `IFileStorage` signed-URL seam (Phase-3) for question/option image reads.
- **Auth:** `RequireStudent` (Phase-4, used by redeem #12) for `§A`; `RequirePermission(AttendanceRead/Export)`
  (already bundled to Teacher+Assistant) for `§B`/`§C`. **No catalog change.**
- Pipeline/read patterns: `ITransactionalRequest`+`TransactionBehavior`, `PagedResult<T>`, `IEndpointGroup`, manual
  `.ToDto()`, `ICodeExporter`-style CSV.

## Steps

### A1 — Domain: `UserAssignment` + `AssignmentQuestion` (snapshot) + `AssessmentEvent`
`Domain/Entities/UserAssignment.cs` (`: TenantEntityBase`), `AssessmentEvent.cs`; enums
`AssignmentStatus (InProgress|Completed)`, `AssessmentEventType (Entered|Left|Answered|Navigated)`; events
`AssignmentGeneratedEvent`, `AssignmentGradedEvent` (both `IAuditableDomainEvent`, **`System` actor**).
- `UserAssignment` fields per contract §E. Factory `GenerateFor(tenantId, enrollment, session, questions, pickVariation, now)`
  → snapshots **one variation per question** into immutable `AssignmentQuestion` children (copy body/image/mark/hint +
  options `{Order,Text,IsCorrect}`); `MaxMarks` = Σ marks; `QuestionCount` set; raises `AssignmentGeneratedEvent`.
- `Answer(assignmentQuestionId, selectedOptionId, now)`: set the child's `SelectedOptionId`/`AnsweredAtUtc`; **when
  every question is answered → grade** (`ScoreMarks` = Σ marks of correct picks, `CorrectCount`, `Status=Completed`,
  `CompletedAtUtc`) and raise `AssignmentGradedEvent(percent)`. Re-answering before completion is allowed
  (open-access); after `Completed` it is rejected (immutable result).
- `AddTime(seconds)` accrues `TimeSpentSeconds` (`FR-PLAT-ASG-004`).
- `AssignmentQuestion` is **immutable** except the student's answer fields (snapshot fairness `FR-PLAT-SES-007`).
- **Unit-test** (mirror `SessionTests`): snapshot is independent of later bank edits; grading math
  (marks + percent rounding); "answer after Completed → throws"; idempotent generation.

### A2 — Infrastructure: real side-effects (replace the stub)
`Infrastructure/Services/EnrollmentSideEffects.cs` (rename/replace `StubEnrollmentSideEffects`; re-register in
`InfrastructureServiceExtensions`). `GenerateAssessmentsAsync`: load the enrollment → session (+ `Questions` with
`Variations`/`Options`); **idempotent** — if a `UserAssignment` already exists for the enrollment, return (re-enroll
keeps the existing assignment + progress, `FR-PLAT-ENR-003`); else `UserAssignment.GenerateFor(...)` + save.
Prerequisite-**quiz** generation stays a logged no-op (5B-2). Runs post-commit (events dispatch after the tx).

### A3 — Infrastructure: EF config + migration
`Configurations/`: `UserAssignmentConfiguration` (+ `OwnsMany(AssignmentQuestion)` → `OwnsMany(options)` or a child
table; immutable), `AssessmentEventConfiguration`. DbSets on `IAppDbContext` + `AppDbContext`:
`UserAssignments`, `AssessmentEvents` (`AssignmentQuestion` navigation-only). Tenant global filters apply
automatically (they're `ITenantOwned`). Indexes: `(TenantId, EnrollmentId)` unique on `UserAssignment`;
`(TenantId, UserAssignmentId, OccurredAtUtc)` on `AssessmentEvent`. Migration `AddAssignments` (gated;
Infrastructure-as-startup / `-c Release` per the VS-lock workaround).

### A4 — Application: auto-grade → Attendance (`System` actor)
`INotificationHandler<AssignmentGradedEvent>` → `AttendanceScoringHandler`: write `Attendance.AssignmentScore`
(percent) for the `(student, session)` (`FR-PLAT-ASG-006`, `FR-PLAT-ATT-002`). Because it's triggered inside the
student's answer request, the **grade audit entry must be attributed to `System`** (`FR-PLAT-AUD-005`), not the
answering student — carry a `System` actor on the `AssignmentGradedEvent` audit (the per-answer/behaviour rows go to
`assessment_events`, **never** the audit log).

### A5 — Application: engine CQRS (`Features/Assignments/`, `RequireStudent`)
`Queries/GetMyAssignment` (by `sessionId` → resolves the caller's enrollment+assignment → `StudentAssignmentDto`,
**no `isCorrect`**, image keys → signed URLs); `Commands/AnswerQuestion` (`ITransactionalRequest`; validates the
caller owns the assignment + the option belongs to that question; records + maybe-grades) → `AssignmentProgressDto`;
`Commands/RecordAssessmentEvents` (append events + `AddTime`). **IDOR:** every handler checks the assignment's
`StudentId == currentUser.UserId` (GUID-in-URL is not authorization, `NFR-SEC-007`).

### A6 — Application: enrollment gate (`FR-PLAT-ENR-007`)
In the Phase-4 `EnrollOrExtend` path (`RedeemCode` + `UnlockSession`): before granting, if
`session.PrerequisiteSessionId is Guid pre`, require a `Completed` `UserAssignment` for `(studentId, pre)` →
else `ConflictException` "Complete the prerequisite assignment first." (→409). If the prerequisite has **no
questions**, pass vacuously. Add the integration assertions in A10.

### A7 — Application: attendance read + export (`Features/Attendance/`, `AttendanceRead`/`Export`)
`Queries/ListSessionAttendance` (`sessionId` → paged `SessionAttendanceRowDto`: join the session's `Active/Expired`
enrollments → `Attendance` + student name + `videosTotal` from `Session.Videos`; `assignmentPercent` =
`Attendance.AssignmentScore`; `videosWatched`/quiz fields 0/null), `ListStudentAttendance` (`studentId` → per-session),
`ExportSessionAttendance`/`ExportStudentAttendance` (CSV via an `IAttendanceExporter`; **audited explicitly** with
`IAuditWriter` — mirror `ExportCodesHandler`, the GET-bypasses-interceptor case). A `AttendanceProjector` batches the
student/session/video joins (mirror `CodeListProjector`).

### A8 — Application: assignment review (`Features/Review/`, `AttendanceRead`)
`Queries/GetAssignmentReview` (`enrollmentId` → `AssignmentReviewDto`: header score `correct/total` +
`marks` + `percent` + `timeSpentSeconds`; questions with options incl. **`isCorrect`** + `selectedOptionId` +
per-question `isCorrect`; image keys → signed URLs), `GetAssignmentBehaviour` (`enrollmentId` → ordered
`BehaviourEventDto[]` from `assessment_events`, with a readable `label`). Staff-only (shows answers).

### A9 — Api: endpoints
`Api/Endpoints/AssignmentEndpoints.cs` (`/api/me/assignments`, **`RequireStudent`** — contract §A #1-3),
`AttendanceEndpoints.cs` (`/api/attendance`, `RequirePermission(AttendanceRead)`; exports `AttendanceExport` —
§B #4-6), `ReviewEndpoints.cs` (`/api/review`, `AttendanceRead` — §C #7-8). `.Produces<>` the shapes +
`ProblemDetails`; `RequireStudent` confirmed **not** reachable by staff tokens and vice-versa.

### A10 — Tests
- **Unit** (`UnitTests/Domain`): snapshot immutability vs bank edits; grading marks→percent; answer-after-complete
  throws; idempotent generation.
- **Integration** (mirror `SessionApiTests` + `SalahBahazadApiFactory`):
  - **Engine (student JWT):** enroll → assignment auto-generated (snapshot) → `GET by-session` (no `isCorrect`) →
    `PUT answer` ×N → on the **last** answer auto-graded → `Attendance.AssignmentScore` written; events recorded +
    time accrued; re-`GET` resumes saved answers.
  - **Gate (`FR-PLAT-ENR-007`):** session with prerequisite → enroll **409** until the prereq assignment is
    `Completed`; vacuous pass when the prereq has no questions.
  - **Admin:** session matrix + student breakdown shapes; review shows submitted-vs-correct + score + time +
    behaviour; **CSV export audited** (one `IAuditWriter` row, GET path).
  - **Audit:** `AssignmentGenerated` + `AssignmentGraded` attributed to **`System`**; behaviour/answers write **no**
    audit rows (they're `assessment_events`).
  - **Security:** tenant isolation (`NFR-SEC-010`); IDOR (student A cannot read/answer student B's assignment →
    403/404); default-deny (anon→401; staff→403 on `/api/me/*`; student→403 on `/api/attendance|review/*`).

## Exit criteria
All 8 contract endpoints return the documented shapes; the stub is gone; auto-grade + gate work; `dotnet build
-c Release` + `dotnet test -c Release` green; Scalar shows `Assignments`/`Attendance`/`Review` groups. Hand to wiring.

## Out of scope (defer — documented)
- **Quiz engine** (`FR-PLAT-QZ-*`), SignalR + Redis backplane (issue #6), server timer, forfeit, focus-loss,
  best-of, the `≥` pass-rule (issue #7), quiz review (`FR-ADM-REV-002`) → **5B-2**.
- **Video gate** (`FR-PLAT-VID-*`) → **5C**; `videosWatched` stays 0 until then.
- Any **student-portal UI** — engine is API-only this engagement.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the BACKEND stream of Phase 5B-1 (Assignments engine + Attendance + Assignment review) for
the Salah Bahzad admin portal. This is the OPEN-BOOK half of 5B — NO SignalR/Redis, no quiz engine.

Read first, in order:
1. backend/CLAUDE.md (domain model: UserAssignment → AssignmentQuestion + AssessmentEvent; audit/business rules)
2. docs/contracts/phase5b1-assignments-attendance.md (the FROZEN contract — build to it field-for-field)
3. docs/IMPLEMENTATION-PLAN-phase5b1-backend.md (your step-by-step, A1–A10)

Mirror the Session/Question snapshot style, the Phase-4 Enrollment + EnrollmentSideEffectsHandler seam (make the
StubEnrollmentSideEffects REAL), and the Phase-4/5A read+export slices (ListCodes/CodeListProjector/ExportCodes
with explicit IAuditWriter). Edit backend/** ONLY.

Deliver: Domain UserAssignment + AssignmentQuestion (immutable snapshot) + AssessmentEvent + grade-on-complete;
real EnrollmentSideEffects (idempotent assignment generation; prerequisite-QUIZ stays a no-op for 5B-2); migration
AddAssignments; AssignmentGradedEvent → AttendanceScoringHandler writing Attendance.AssignmentScore attributed to
the SYSTEM actor; the FR-PLAT-ENR-007 prerequisite-assignment gate (409, vacuous pass when prereq has no
questions); Features/Assignments (RequireStudent, student-scoped, IDOR-checked, no isCorrect to students),
Features/Attendance (AttendanceRead + audited CSV export), Features/Review (AttendanceRead, shows isCorrect +
behaviour); Api endpoints; and IntegrationTests for the engine flow, the gate, the admin shapes, System-actor
audit, tenant isolation, IDOR, and default-deny.

Green gate: `cd backend && dotnet build -c Release && dotnet test -c Release` (Docker for Testcontainers). Report
the gate-409 and auto-grade(System-actor) test results explicitly.
```
