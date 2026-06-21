# Student Portal · S4 — BACKEND stream (assignment answer-key review read)

> Status: **Planned — not yet built** · Created 2026-06-21 · The **engine half** of slice **S4** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S4 — the assignment runner + review). The **assignment engine** (the
> three `/api/me/assignments` routes: load → answer → behaviour events) **already exists** (Phase 5B-1) and is **reused
> verbatim** — frozen in `docs/contracts/phase5b1-assignments-attendance.md` §A and re-stated in
> `docs/contracts/student-s4-assignments.md` §A. This stream adds **one** new student read:
> **`GET /api/me/assignments/{assignmentId}/review`**.
>
> Satisfies `FR-STU-ASG-007` + `FR-PLAT-ASG-008` (the post-completion **answer-key review**: the student's pick vs. the
> correct answer + their score). **It is the only student-facing surface that exposes `isCorrect`**, and only for the
> caller's own **`Completed`** assignment. **No new aggregate, no migration.** **Change the contract
> (`docs/contracts/student-s4-assignments.md` §B/§D/§E/§G) first if anything moves.**
>
> **Grounding correction to the master plan.** §S4 read *"Backend: none."* That was an oversight: the runner's
> `StudentAssignmentDto` (§A) **deliberately hides `isCorrect`** ("the student shape never carries option correctness"),
> and the only correctness-exposing endpoint is the **staff** `GET /api/review/assignments/{enrollmentId}`
> (`AttendanceRead`-gated). So `FR-STU-ASG-007` had **no** student path — this read closes it, exactly like S1's anonymous
> grades read was the lone backend touch in an otherwise frontend-led slice.
>
> Gate: `dotnet test -c Release` green (the one pre-existing `QuestionBank` image-test failure is the known baseline);
> then the **wiring** stream (`IMPLEMENTATION-PLAN-student-s4-wiring.md`) proves it live on Aspire.

---

## Design reference

This stream ships **no screen**; its one JSON shape feeds the **Student Portal** prototype's **new** assignment-review
screen (the prototype has **no** dedicated review screen — assignments submit straight back to the session detail, so §D
of the contract is a **new** student screen that mirrors the **admin** `AssignmentReviewComponent`'s question/option
treatment — green-check correct option / red wrong pick — for visual consistency). The authority is
`docs/contracts/student-s4-assignments.md` §B (the DTO + error modes) + §D (what the answer key shows). The **runner**
itself (the prototype's `screen === 'assignment'`: accumulated timer, one-question-at-a-time MCQ, hint toggle, progress
bar, prev/next, "Submit assignment" = the last answer `PUT`) is driven by the **existing** §A engine — this stream does
**not** touch it.

---

## 1. Frozen contract (this stream)

Implements **`docs/contracts/student-s4-assignments.md` §B** verbatim:

- `GET /api/me/assignments/{assignmentId}/review` · `RequireStudent` · `200 StudentAssignmentReviewDto` · the caller's
  **own**, **`Completed`** assignment with the **answer key** — per-question **and** per-option `isCorrect`, the student's
  `selectedOptionId`, marks, and the overall score (`correctCount`/`questionCount`/`scoreMarks`/`maxMarks`/`percent` +
  `timeSpentSeconds` + `completedAtUtc`). `{assignmentId}` is the **`UserAssignment` id** (the `id` the runner's §A #1
  returns); ownership is `UserAssignment.StudentId == currentUser.UserId`.
- **Error modes (§B.2):** `401` anon · `403` (no `reason`) staff · **`403` `reason="assignment_in_progress"`** when the
  assignment is the caller's but `Status == InProgress` (the key is **never** revealed pre-completion) · **`404`** when
  `{assignmentId}` is unknown / **another student's** / **another tenant's** (opaque IDOR/tenant boundary) · `200` for the
  caller's own `Completed` assignment.

The three §A engine routes (`GET .../by-session/{sessionId}`, `PUT .../{id}/questions/{aqId}/answer`,
`POST .../{id}/events`) are **untouched** (reused as-is — no signature change). **No new aggregate, no migration.**

## 2. Pre-flight (confirm — do NOT rebuild)

- **The 5B-1 assignment engine** (`AssignmentEndpoints` → `GetMyAssignmentHandler` / `AnswerQuestionHandler` /
  `RecordAssessmentEventsHandler`, `Features/Assignments/*`) — load, answer (with auto-grade-on-last → `Attendance`),
  behaviour events. **Reused as-is.** This stream does **not** edit it; the frontend runner calls
  `GET /api/me/assignments/by-session/{sessionId}` + `PUT …/answer` + `POST …/events` directly.
- **`GetAssignmentReviewHandler`** (`Features/Review/Queries/GetAssignmentReview/GetAssignmentReviewHandler.cs`) — **the
  template** for the new handler. It already builds the answer key: orders `assignment.Questions` by `Order`, signs each
  `q.ImageObjectKey` via `IFileStorage.GetSignedReadUrlAsync`, projects each option `(Id, Order, Text, IsCorrect)`,
  resolves `sessionTitle` from `db.Sessions.IgnoreQueryFilters()`, and computes `correctCount`/`scoreMarks` from the
  answers. **Mirror it.** The new handler differs in exactly five ways:
  1. Keyed by **`{assignmentId}`** (`a.Id == query.AssignmentId`), **not** `EnrollmentId`.
  2. Scoped to **`a.StudentId == currentUser.UserId`** (`ICurrentUserResolver`), **not** `AttendanceRead` — the IDOR
     boundary. A foreign / cross-tenant / unknown id → `NotFoundException` → `404` (the global filter already excludes
     other tenants/soft-deleted, so a cross-tenant id is naturally null).
  3. **Gate `Status == AssignmentStatus.Completed`** — an `InProgress` assignment (the caller's) →
     `ForbiddenException(..., "assignment_in_progress")` → `403` with the machine `reason` (§B.2). *(The staff review has
     no such gate — it shows partial standing; the student one must not.)*
  4. **Drops `studentName`** (it's the caller — no second `IgnoreQueryFilters` join on `db.Students`).
  5. **Adds `id` / `sessionId` / `completedAtUtc`** to the result.
- **`GetAssignmentReviewQuery`** (`…/GetAssignmentReview/GetAssignmentReviewQuery.cs`) — the query-record shape to mirror
  (a `Guid` + `IRequest<…Dto>`); the new one carries `AssignmentId`.
- **`ReviewDtos.cs`** (`Features/Review/DTOs/ReviewDtos.cs`) — `AssignmentReviewDto` / `ReviewQuestionDto` /
  `ReviewOptionDto`, all carrying `IsCorrect`. **The new DTOs mirror these closely** (minus `StudentName`, plus
  `Id`/`SessionId`/`CompletedAtUtc` on the root **and** a per-question `Id` on `StudentReviewQuestionDto` — the staff
  `ReviewQuestionDto` starts at `Order` and omits it), but live in **`Features/Assignments/DTOs/AssignmentDtos.cs`** as **distinct**
  records (§3.4) — **do not** reuse the runner's `StudentAssignmentDto` (which forbids `isCorrect`) and **do not** widen
  it.
- **The reason'd-403 mechanism** — `ForbiddenException(string message, string? reason = null)`
  (`Application/Common/Exceptions/ForbiddenException.cs`, an `IProblemReason`) is mapped by `GlobalExceptionHandler`
  (`Api/Middleware/GlobalExceptionHandler.cs`, L47–48) to a `403` ProblemDetails with `Extensions["reason"]` set. This is
  exactly how the video gate emits `quiz_required` / `no_views_remaining` in `StartVideoPlaybackHandler`
  (`Features/Videos/Commands/StartVideoPlayback/StartVideoPlaybackHandler.cs`, ~L59–64). Throw
  `new ForbiddenException("Finish the assignment to see your answers and score.", "assignment_in_progress")` — the
  message is the contract's verbatim `detail` (§B.2), the reason is the machine code.
- **`NotFoundException`** (`Application/Common/Exceptions/NotFoundException.cs`) → `404` (opaque "Resource not found." —
  never reveal existence) — the same pattern `GetMyAssignmentHandler` (~L22) and `GetAssignmentReviewHandler` (~L19) use.
- **Entities (read-only here):** `UserAssignment` (`Domain/Entities/UserAssignment.cs`: `StudentId`, `SessionId`,
  `Status`, `ScoreMarks`, `MaxMarks`, `CorrectCount`, `QuestionCount`, `TimeSpentSeconds`, `CompletedAtUtc`; computed
  `Percent` — `MaxMarks == 0 ? 0 : round(100 × scoreMarks / maxMarks)`, matching §B.1) owning `AssignmentQuestion`
  (`Order`, `BodyLatex`, `ImageObjectKey`, `Mark`, `HintUrl`, `SelectedOptionId`, computed `IsCorrect`) +
  `AssignmentOption` (`Order`, `Text`, `IsCorrect`). Owned questions/options load with the root. `UserAssignment` is
  `ITenantOwned` → the **global query filter** scopes it to the caller's tenant and excludes soft-deleted rows; **never**
  write a per-handler `Where(x => x.TenantId == …)`.
- **`ICurrentUserResolver`** — `.UserId` = the student id (as `GetMyAssignmentHandler` / `StartVideoPlaybackHandler` use
  it). No `TimeProvider` needed (the review is a pure projection of stored state — no `now`).
- **`RequireStudentExtensions.RequireStudent()`** (`Api/Authorization/RequireStudent.cs`) — anon → 401, staff → 403. The
  new route uses it, like every `/api/me/*` route.

## 3. Application — the new query (`Features/Assignments/Queries/GetMyAssignmentReview/`)

Keep it next to `GetMyAssignment` so the shared student-assignment helpers (the DTOs in
`Features/Assignments/DTOs/AssignmentDtos.cs`, the signed-image helper) are obvious. It resolves the caller via
`ICurrentUserResolver.UserId`; tenant + soft-delete are the global filter.

### 3.1 `GetMyAssignmentReview`
- `GetMyAssignmentReviewQuery(Guid AssignmentId) : IRequest<StudentAssignmentReviewDto>`. **No validator needed.**
- `GetMyAssignmentReviewHandler(IAppDbContext db, ICurrentUserResolver currentUser, IFileStorage fileStorage)`:
  1. `var studentId = currentUser.UserId;`
  2. Resolve the caller's **own** assignment by id:
     `db.UserAssignments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == query.AssignmentId && a.StudentId == studentId, ct)`
     → **`throw new NotFoundException("Assignment", query.AssignmentId)`** if null (the §B.2 `404` — IDOR/tenant boundary;
     the global filter excludes other tenants/soft-deleted, so a foreign/cross-tenant id is naturally null). Owned
     `Questions`/`Options` load with the root.
  3. **`Completed` gate:** `if (assignment.Status != AssignmentStatus.Completed) throw new ForbiddenException("Finish the
     assignment to see your answers and score.", "assignment_in_progress");` (the §B.2 reason'd `403` — the key is never
     revealed pre-completion).
  4. Resolve **`sessionTitle`** from `db.Sessions.IgnoreQueryFilters().Where(s => s.Id == assignment.SessionId)
     .Select(s => s.Title).FirstOrDefaultAsync(ct)` (mirror the staff review — name resolution ignores the query filters
     so an archived session still resolves). **Do not** join `db.Students` (no `studentName` here).
  5. **Questions:** order `assignment.Questions` by `Order` asc; per question sign `q.ImageObjectKey`
     (`fileStorage.GetSignedReadUrlAsync(...).Url`, null when the key is blank — the runner mapping's `SignAsync` shape),
     carry `q.HintUrl` as-is, project options ordered by `Order` asc as `StudentReviewOptionDto(o.Id, o.Order, o.Text,
     o.IsCorrect)` (**`isCorrect` EXPOSED**), and the question's `q.SelectedOptionId` + `q.IsCorrect`.
  6. **Score:** prefer the sealed values on the completed root — `correctCount = assignment.CorrectCount ?? 0`,
     `scoreMarks = assignment.ScoreMarks ?? 0`, `percent = assignment.Percent` (computed; `0` when `maxMarks == 0`) —
     `questionCount`/`maxMarks`/`timeSpentSeconds`/`completedAtUtc` straight off the root. *(The staff review recomputes
     `correctCount`/`scoreMarks` from the answers to show in-progress partials; here the assignment is `Completed`, so the
     sealed `CorrectCount`/`ScoreMarks` are authoritative — either is equivalent post-completion, but the sealed values
     are the source of truth.)*
  7. `return assignment.ToReviewDto(sessionTitle, signedQuestions);` (the manual mapping — §3.4).

### 3.2 DTOs + mapping
Add to **`Features/Assignments/DTOs/AssignmentDtos.cs`** (beside `StudentAssignmentDto` — keep the student shapes
together; the file's comment "the student shape never carries option correctness" applies to the **runner** DTO, not these
review DTOs, which are the deliberate, gated exception). Field order = the contract §B.1 shape:

```csharp
/// <summary>One option in the student's own answer-key review — isCorrect EXPOSED (review only, post-completion).</summary>
public sealed record StudentReviewOptionDto(Guid Id, int Order, string Text, bool IsCorrect);

/// <summary>One question in the student's answer-key review: the snapshot + the student's pick + correctness.</summary>
public sealed record StudentReviewQuestionDto(
    Guid Id, int Order, string? BodyLatex, string? ImageUrl, int Mark, string? HintUrl,
    IReadOnlyList<StudentReviewOptionDto> Options, Guid? SelectedOptionId, bool IsCorrect);

/// <summary>The caller's own COMPLETED assignment with the answer key + score (contract §B.1, FR-STU-ASG-007).
/// Mirrors the staff AssignmentReviewDto minus studentName, plus id/sessionId/completedAtUtc.</summary>
public sealed record StudentAssignmentReviewDto(
    Guid Id, Guid SessionId, string? SessionTitle, AssignmentStatus Status,
    int CorrectCount, int QuestionCount, int ScoreMarks, int MaxMarks, int Percent,
    int TimeSpentSeconds, DateTimeOffset CompletedAtUtc,
    IReadOnlyList<StudentReviewQuestionDto> Questions);
```

Manual `.ToReviewDto(...)` extension (no mapping library; never map in the handler body). The image signing is async, so
build the `StudentReviewQuestionDto` list in the handler loop (as the staff handler does) and pass it into a thin
`.ToReviewDto(sessionTitle, questions)` that assembles the root — **or** make the whole mapping the async extension
(mirroring `StudentAssignmentMappings.ToStudentDtoAsync`). Either is fine; keep the signed-URL shape identical to the
runner's `SignAsync`. `Status` is always `Completed` here (the gate guarantees it). `Percent` comes from the root's
computed property.

## 4. API — endpoint group

Add the **fourth** route to the **existing** `AssignmentEndpoints : IEndpointGroup`
(`Api/Endpoints/AssignmentEndpoints.cs`) — the same `/api/me/assignments` group, the same `RequireStudent()`, mirroring
the three engine routes already there:

```csharp
group.MapGet("/{assignmentId:guid}/review", GetReviewAsync)
    .RequireStudent()
    .WithName("GetMyAssignmentReview")
    .WithSummary("The caller's own COMPLETED assignment with the answer key + score (review only)")
    .Produces<StudentAssignmentReviewDto>()
    .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)   // staff, or assignment_in_progress
    .Produces<ProblemDetails>(StatusCodes.Status404NotFound);   // unknown / foreign / cross-tenant
```

```csharp
private static async Task<IResult> GetReviewAsync(
    Guid assignmentId, ISender sender, CancellationToken cancellationToken)
    => Results.Ok(await sender.Send(new GetMyAssignmentReviewQuery(assignmentId), cancellationToken));
```

A thin `ISender.Send(...)` delegate (like `GetBySessionAsync`). `RequireStudent()` gives the 401/403-staff; the handler
throws for the `403 assignment_in_progress` + `404`. **Do not** add a new endpoint group — the route belongs on the
existing `AssignmentEndpoints` (it's the same `/api/me/assignments` surface, just a new sub-route). Update the group's
XML-doc to note the one route that **does** expose correctness (the gated review), so the "No `isCorrect` is exposed
here" comment stays accurate for the engine routes.

## 5. Migration

**None.** `UserAssignment` + its owned `AssignmentQuestion`/`AssignmentOption` snapshot, and `AssessmentEvent`, all exist
from 5B-1. The review is a **pure read** of the caller's own completed homework. *(No new field, no new aggregate — §F of
the contract: "No engine change.")*

## 6. Tests (`dotnet test -c Release`)

Integration (`WebApplicationFactory` + Testcontainers, **Student-role JWT** — reuse the assignment-engine student helper:
`factory.CreateClientForStudent(tenant, student.Id)` + `RedeemAsync(serial)` to generate-on-enroll, then
`GetMyAssignmentAsync(session.Id)` + `AnswerAsync(...)` to complete it; mirror `AssignmentEngineTests.cs`). Add a new file
**`MyAssignmentReviewApiTests.cs`** (or extend `AssignmentEngineTests`). The §A guard test
(`Student_assignment_shape_never_exposes_isCorrect`, asserting the **by-session** route's raw JSON has no `isCorrect`)
**must stay green** — the new review route is the deliberate exception.

- **Answer key for the caller's own `Completed` assignment (§B.1, the happy path):** enroll → answer **every** question
  (last answer auto-grades → `Completed`) → `GET /api/me/assignments/{id}/review` → `200 StudentAssignmentReviewDto`:
  `id`/`sessionId` echo, `status == "Completed"`, questions **ordered by `Order`** with each option carrying `isCorrect`,
  each question's `selectedOptionId` echoing the student's pick + its `isCorrect`, and the score
  (`correctCount`/`questionCount`/`scoreMarks`/`maxMarks`/`percent` + `timeSpentSeconds` + `completedAtUtc`) matching the
  grade (e.g. answer 1-of-2 correct ⇒ `correctCount == 1`, `percent == 50`). **`isCorrect` IS present** here (the review
  is the one surface that exposes it).
- **`403 assignment_in_progress` on an in-progress assignment (§B.2):** enroll, do **not** complete (leave it
  `InProgress`), `GET …/review` → `403` with `reason == "assignment_in_progress"` and the verbatim `detail` "Finish the
  assignment to see your answers and score." (assert the `reason` extension, not just the status).
- **`404` IDOR / foreign / unknown (§B.2):** a **second** student's completed assignment id → `404` (not the data — opaque,
  never the other student's answers); an **unknown** random GUID → `404`.
- **`404` cross-tenant (`NFR-SEC-010`):** an assignment id that exists in **another tenant** → `404` for a tenant-A
  student (the global filter naturally yields null → `NotFoundException`). Proves tenant isolation on the new read.
- **Auth gating (§B.2):** anonymous (no bearer) → `401`; a **staff** JWT (`StaffRole.Teacher`) → `403` (the
  `RequireStudent` filter, **no** `reason`); Student JWT → `200`/`403 assignment_in_progress`/`404` per the above.
- **Review NOT audited (§E):** assert **no new `audit_entries` row** is written for the `GET …/review` call (parity with
  the `/api/me/sessions` + `/api/me/catalogue` reads; query the audit table before/after). The completion's `System`
  auto-grade audit is 5B-1's — written by the answer `PUT`, **not** by the review read.
- **§A guard still holds (regression):** the existing
  `AssignmentEngineTests.Student_assignment_shape_never_exposes_isCorrect` (raw JSON of the **by-session** route contains
  no `isCorrect`) **stays green** — the runner DTO is unchanged; only the new `/review` route exposes correctness. (Add an
  explicit assertion if extending the file: the **review** raw JSON **does** contain `isCorrect`, the **by-session** raw
  JSON does **not**.)
- **Engine untouched (regression):** the rest of `AssignmentEngineTests` (load, answer, auto-grade → `Attendance`,
  behaviour events, 409-after-complete, IDOR on answer) still passes — this stream adds no change to the three engine
  routes.

## Done = ready for wiring

Contract §B/§D/§E satisfied; the three §A engine routes untouched; the student/staff `isCorrect` split preserved (the
runner DTO stays correctness-free, the new `/review` route is the only — gated — exception); suite green (minus the known
baseline image test); **no migration**. Hand to `IMPLEMENTATION-PLAN-student-s4-wiring.md`.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the BACKEND stream of Student-Portal phase S4 for Salah Bahzad (.NET 10, Clean Architecture +
CQRS + source-gen Mediator). Edit backend/** ONLY. Add ONE new student READ endpoint. The Phase-5B-1 assignment engine
(the three /api/me/assignments routes: by-session load, answer, events) already exists and is REUSED AS-IS — do not touch
it. NO migration, NO new aggregate.

Read first, in order:
1. backend/CLAUDE.md (Multi-tenancy + EF query filters, Minimal API, Authentication/authorization, Testing).
2. docs/contracts/student-s4-assignments.md — the FROZEN contract: §A (the three EXISTING engine routes + the
   StudentAssignmentDto that NEVER carries isCorrect — reused, do not touch), §B (the NEW GET
   /api/me/assignments/{assignmentId}/review + StudentAssignmentReviewDto/StudentReviewQuestionDto/StudentReviewOptionDto
   + the 403 assignment_in_progress / 404 IDOR error modes), §D (what the answer key shows), §E (reads not audited), §G
   (frozen vs stream-owned). Change the contract first if anything moves.
3. The templates to mirror: Application/Features/Review/Queries/GetAssignmentReview/GetAssignmentReviewHandler.cs (the
   answer-key builder — order Questions by Order, sign ImageObjectKey via IFileStorage.GetSignedReadUrlAsync, project
   options with IsCorrect, resolve sessionTitle from db.Sessions.IgnoreQueryFilters; the new handler differs: keyed by
   {assignmentId} not enrollmentId, scoped a.StudentId == ICurrentUserResolver.UserId not AttendanceRead, gate
   Status==Completed, drop studentName, add id/sessionId/completedAtUtc) + its GetAssignmentReviewQuery + the
   Features/Review/DTOs/ReviewDtos.cs records; Application/Features/Assignments/DTOs/AssignmentDtos.cs (where the NEW
   distinct StudentAssignmentReviewDto/StudentReviewQuestionDto/StudentReviewOptionDto records go — do NOT widen the
   runner's StudentAssignmentDto); Application/Features/Videos/Commands/StartVideoPlayback/StartVideoPlaybackHandler.cs +
   Application/Common/Exceptions/ForbiddenException.cs + Api/Middleware/GlobalExceptionHandler.cs (how a reason'd 403 is
   thrown and mapped to ProblemDetails Extensions["reason"]); Api/Endpoints/AssignmentEndpoints.cs (ADD the 4th route to
   THIS existing RequireStudent group); Features/Assignments/Queries/GetMyAssignment/GetMyAssignmentHandler.cs (the
   ICurrentUserResolver.UserId IDOR scope + NotFoundException pattern).

Build: NEW GetMyAssignmentReview query/handler under Features/Assignments/Queries/GetMyAssignmentReview; the
StudentAssignmentReviewDto + StudentReviewQuestionDto + StudentReviewOptionDto records (isCorrect EXPOSED) + a manual
.ToReviewDto() in Features/Assignments/DTOs/AssignmentDtos.cs; resolve the caller's OWN assignment by id
(a.Id == AssignmentId && a.StudentId == currentUser.UserId) — null => NotFoundException => 404 (opaque IDOR/tenant
boundary); gate Status==Completed else throw ForbiddenException("Finish the assignment to see your answers and score.",
"assignment_in_progress") => 403 with reason; resolve sessionTitle via db.Sessions.IgnoreQueryFilters; order questions +
options by Order; sign ImageObjectKey; score from the sealed root (CorrectCount/ScoreMarks/Percent). Add ONE route
GET /{assignmentId:guid}/review to the existing AssignmentEndpoints group (RequireStudent, Produces<…ReviewDto> + 403 +
404). DO NOT touch the three engine routes; DO NOT widen StudentAssignmentDto; NO migration.

Tests (xUnit v3 + Testcontainers + FluentAssertions, Student-role JWT — reuse the AssignmentEngineTests student helper
to enroll + answer-through to Completed): 200 answer-key for the caller's own Completed assignment (per-option isCorrect
present, score correct, selectedOptionId echoed, ordered by Order); 403 with reason="assignment_in_progress" on an
in-progress assignment; 404 for another student's / unknown id; 404 cross-tenant (NFR-SEC-010); 401 anon / 403 staff;
review NOT audited (no new audit_entries row); AND the existing by-session guard
(Student_assignment_shape_never_exposes_isCorrect) STILL passes (the runner DTO is unchanged; only /review exposes
correctness). Green gate: `dotnet test -c Release` (the one pre-existing QuestionBank image test may stay red —
baseline). Report it.
```
