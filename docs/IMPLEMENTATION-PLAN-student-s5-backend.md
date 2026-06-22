# Student Portal · S5 — BACKEND stream (per-attempt quiz answer-key review read)

> Status: **Planned — not yet built** · Created 2026-06-22 · The **engine half** of slice **S5** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S5 — the proctored quiz runner + per-attempt answer-key review). The
> **quiz engine** (the five `/api/me/quizzes` routes: by-session load → start → answer → submit → focus, **plus** the
> `QuizHub` forfeit-on-disconnect, the `QuizAutoSubmitJob` Hangfire timer and the Redis backplane) **already exists**
> (Phase 5B-2) and is **reused verbatim** — frozen in `docs/contracts/phase5b2-quizzes.md` §A and re-stated in
> `docs/contracts/student-s5-quizzes.md` §A. This stream adds **one** new student read —
> **`GET /api/me/quizzes/attempts/{attemptId}/review`** — and **one** purely-additive field — **`id`** (the attempt
> `Guid`) on the existing `StudentQuizAttemptSummaryDto` (§A #1).
>
> Satisfies `FR-STU-QZ-009` + `FR-PLAT-QZ-009` (the post-termination **answer-key review**: the student's pick vs. the
> correct answer + the attempt's score). **It is the only student-facing surface that exposes quiz `isCorrect`**, and only
> for the caller's own **terminal** (`Submitted`|`TimedOut`|`Forfeited`) attempt. **No new aggregate, no migration.**
> **Change the contract (`docs/contracts/student-s5-quizzes.md` §A/§B/§E/§H) first if anything moves.**
>
> **Grounding correction to the master plan.** §S5 read *"Backend: none."* That was an oversight — identical to S4's. The
> live student quiz shapes **deliberately hide `isCorrect`** (5B-2: `QuizOptionDto` "no correctness leaked";
> `QuizAttemptOption` "Correctness is never exposed to the student shape — only the staff review"), **and the staff quiz
> review (`GET /api/review/quizzes/{enrollmentId}` → `QuizReviewDto`) is attempt-level scores only** — `GetQuizReviewHandler`
> projects `QuizReviewAttemptDto(Number, ScorePercent, TimeSpentSeconds, Flag, Status, StartedAtUtc, isBest)` and **never**
> touches the per-question snapshot. So **`FR-STU-QZ-009`** ("review each attempt's questions, their answers, and the correct
> answers") had **no** path at all — neither student nor staff. S5 closes it with the one read in §B, the standard backend +
> frontend + wiring three-stream split (like S4's `FR-STU-ASG-007` review read). *(Second master-plan correction, owned by
> the frontend stream: §4.2's "server-synced via QuizHub" is wrong — the hub pushes nothing; the countdown is local and the
> authoritative timer is the Hangfire job. No backend work falls out of that here.)*
>
> Gate: `dotnet test -c Release` green (the one pre-existing `QuestionBank` image-test failure is the known baseline);
> then the **wiring** stream (`IMPLEMENTATION-PLAN-student-s5-wiring.md`) proves it live on Aspire.

---

## Design reference

This stream ships **no screen**; its one JSON shape feeds two things on the prototype side. (1) The **new** quiz-review
screen — the prototype (`.claude/Salah Bahzad Student Portal/Student Portal.html`) has a score-only `quizResults` screen
(mascot + score ring + "This attempt"/"Best of" tiles + "Back to session") and **no** answer-key screen, so §D's review is
a **new** student screen that mirrors the **admin** `AssignmentReviewComponent`/quiz-review treatment — green-check correct
option / red wrong pick — for visual consistency. (2) The additive `id` lets the **intro's** (`quizIntro`) `attempts[]`
rows deep-link each terminal attempt's review. The authority is `docs/contracts/student-s5-quizzes.md` §B (the DTO + error
modes) + §D (what the answer key shows). The **runner** itself (the prototype's `quiz` screen — countdown, question dots,
prev/next, `beforeunload` guard, `visibilitychange` → focus pings, the "Leave the quiz?" modal) is driven by the
**existing** §A engine — this stream does **not** touch it.

---

## 1. Frozen contract (this stream)

Implements **`docs/contracts/student-s5-quizzes.md` §B** verbatim, plus the **one additive field** in §A #1:

- **NEW read** — `GET /api/me/quizzes/attempts/{attemptId}/review` · `RequireStudent` · `200 StudentQuizAttemptReviewDto`
  · the caller's **own**, **terminal** (`Submitted`|`TimedOut`|`Forfeited`) attempt with the **answer key** — per-question
  **and** per-option `isCorrect`, the student's `selectedOptionId`, `mark`, and the attempt's score (`scorePercent` /
  `minPassPercent` / `timeSpentSeconds` / `startedAtUtc` / `submittedAtUtc`), plus `quizId` / `gatedSessionId` /
  `sessionTitle` / `number` / `status` (§B.1). `{attemptId}` is the **`QuizAttempt.Id`** (the `id` the intro's §A #1 list
  now returns); ownership is proven through the owning root — `UserQuiz.StudentId == currentUser.UserId`.
- **Error modes (§B.2):** `401` anon · `403` (no `reason`) staff · **`403` `reason="quiz_attempt_in_progress"`** with the
  verbatim `detail` "Finish the quiz to see your answers and score." when the attempt is the caller's but
  `Status == InProgress` (the key is **never** revealed mid-sitting) · **`404`** when `{attemptId}` is unknown / **another
  student's** / **another tenant's** (opaque IDOR/tenant boundary — never reveal existence) · `200` for the caller's own
  **terminal** attempt.
- **ONE additive field (§A #1)** — `StudentQuizAttemptSummaryDto` gains **`id`** (the `QuizAttempt.Id`). Purely additive —
  a new field on an existing record, nothing renamed or removed — so each terminal attempt is addressable by the new read.
  `QuizMappings.ToStudentDto` passes `a.Id`; the 5B-2 "no `isCorrect`" guards on the live shapes are untouched.

The five §A engine routes (`GET .../by-session/{sessionId}`, `POST .../{quizId}/attempts`,
`PUT .../attempts/{attemptId}/questions/{aqId}/answer`, `POST .../attempts/{attemptId}/submit`,
`POST .../attempts/{attemptId}/focus`), the `QuizHub`, the `QuizAutoSubmitJob` Hangfire timer and the Redis backplane are
**untouched** (reused as-is — no signature change). **No new aggregate, no migration.**

## 2. Pre-flight (confirm — do NOT rebuild)

- **The 5B-2 quiz engine** (`Features/Quizzes/Queries/GetMyQuiz` + `Features/Quizzes/Commands/{StartQuizAttempt,
  AnswerQuizQuestion,SubmitQuizAttempt,RecordQuizFocusEvent}`, surfaced by `Api/Endpoints/QuizEndpoints.cs`) — load, start,
  answer, submit, focus. **Reused as-is.** This stream does **not** edit any of the five routes; the frontend runner calls
  them directly.
- **The `QuizHub`** (`Api/Hubs/QuizHub.cs`) + the **`QuizLifecycleService`** (forfeit/timeout orchestration) + the
  **`QuizAutoSubmitJob`** (`Infrastructure/Jobs/QuizAutoSubmitJob.cs`, the Hangfire auto-submit at `DeadlineUtc`) + the
  Redis connection↔attempt map — **all reused verbatim**. The hub pushes nothing (no server→client / client→server
  methods); its sole job is forfeit-on-disconnect (§A.1). **This stream touches none of them** — the new read is a pure
  projection of the snapshot they wrote.
- **The `QuizAttempt` snapshot** (`Domain/Entities/QuizAttempt.cs`, **read-only here**) owned by `UserQuiz`
  (`Domain/Entities/UserQuiz.cs`): `QuizAttempt` carries `Number`, `Status`, `ScorePercent`, `StartedAtUtc`,
  `SubmittedAtUtc`, computed `TimeSpentSeconds` (`submitted−started`, or the full window on timeout, `0` while active), and
  owns `QuizAttemptQuestion[]`. Each `QuizAttemptQuestion` carries `Order` (**1-based**), `BodyLatex`, `ImageObjectKey`,
  `Mark`, `SelectedOptionId` (the student's pick, `null` when unanswered), computed `IsCorrect`
  (`SelectedOptionId is Guid sel && options.Any(o => o.Id == sel && o.IsCorrect)`), and owns `QuizAttemptOption[]`. Each
  `QuizAttemptOption` carries `Order` (**0-based** "DisplayOrder"), `Text`, `IsCorrect`. **Everything the §B DTO needs is
  already on the snapshot** — the review is a pure projection, **no recomputation, no migration**. `UserQuiz` is
  `TenantEntityBase` + `IAuditViaEventOnly` (and its owned attempts/questions/options are `IAuditViaEventOnly`) → the
  global query filter scopes it to the caller's tenant and excludes soft-deleted rows; **never** write a per-handler
  `Where(x => x.TenantId == …)`.
- **`QuizMappings.ToStudentDto`** (`Features/Quizzes/DTOs/QuizDtos.cs`, L56–74) — the intro mapping. The **only** edit to
  the existing engine surface: add `a.Id` as the new first positional arg to the `StudentQuizAttemptSummaryDto` constructor
  in the `quiz.Attempts.OrderBy(a => a.Number).Select(...)` projection. **Do not** touch `ToAttemptDtoAsync` /
  `ToResultDto` / the `QuizQuestionDto`/`QuizOptionDto` live shapes (which forbid `isCorrect`).
- **Why the staff `GetQuizReviewHandler` is NOT the template.** `Features/Review/Queries/GetQuizReview/GetQuizReviewHandler.cs`
  resolves `UserQuiz` **by `EnrollmentId`** (not the IDOR student scope) and projects **attempt-level scores only**
  (`QuizReviewDto(BestPercent, Passed, MinPassPercent, AttemptsUsed, AttemptCount, attempts[])` where each
  `QuizReviewAttemptDto` is `Number/ScorePercent/TimeSpentSeconds/Flag/Status/StartedAtUtc/isBest`) — it **never** reads the
  per-question snapshot, so it has no answer key to mirror. The **per-question** template is **the snapshot itself**
  (`QuizAttempt.Questions` → ordered, signed, projected with `isCorrect`) shaped exactly like S4's
  **`GetMyAssignmentReviewHandler`** (`Features/Assignments/Queries/GetMyAssignmentReview/`): resolve the caller's own row,
  gate terminal/completed, resolve `sessionTitle` via `IgnoreQueryFilters`, order questions + options by `Order`, sign the
  image key, project per-option + per-question `isCorrect` + `selectedOptionId`. **Mirror the S4 review handler's shape**,
  not the staff quiz review.
- **The reason'd-403 mechanism** — `ForbiddenException(string message, string? reason = null)`
  (`Application/Common/Exceptions/ForbiddenException.cs`, an `IProblemReason`) is mapped by `GlobalExceptionHandler`
  (`Api/Middleware/GlobalExceptionHandler.cs`) to a `403` ProblemDetails with `Extensions["reason"]` set — the same pattern
  the video gate (`quiz_required` / `no_views_remaining`) and the S4 review (`assignment_in_progress`) use. Throw
  `new ForbiddenException("Finish the quiz to see your answers and score.", "quiz_attempt_in_progress")` — the message is
  the contract's verbatim `detail` (§B.2), the reason is the machine code.
- **`NotFoundException`** (`Application/Common/Exceptions/NotFoundException.cs`) → `404` (opaque — never reveal existence) —
  the same pattern `GetMyQuizHandler` (~L20) and the S4 review handler use for an unknown/foreign/cross-tenant id.
- **`ICurrentUserResolver`** — `.UserId` = the student id (as `GetMyQuizHandler` and the S4 review handler use it).
  `.TenantId` is the global filter's job — not a per-handler `Where`. No `TimeProvider` needed (the review is a pure
  projection of stored, terminal state — no `now`).
- **`RequireStudentExtensions.RequireStudent()`** (`Api/Authorization/RequireStudent.cs`) — anon → 401, staff → 403. The
  new route uses it, like every `/api/me/*` route and the existing five quiz routes.

## 3. Application — the new query (`Features/Quizzes/Queries/GetMyQuizAttemptReview/`) + the additive id

Keep it next to `GetMyQuiz` so the shared quiz DTOs/mappings (`Features/Quizzes/DTOs/QuizDtos.cs`) are obvious. It resolves
the caller via `ICurrentUserResolver.UserId`; tenant + soft-delete are the global filter.

### 3.1 `GetMyQuizAttemptReview`
- `GetMyQuizAttemptReviewQuery(Guid AttemptId) : IRequest<StudentQuizAttemptReviewDto>`. **No validator needed.**
- `GetMyQuizAttemptReviewHandler(IAppDbContext db, ICurrentUserResolver currentUser, IFileStorage fileStorage)`:
  1. `var studentId = currentUser.UserId;`
  2. **Resolve the attempt THROUGH its owning `UserQuiz`** — the IDOR/tenant scope is on the root, the attempt is owned:
     ```csharp
     var quiz = await db.UserQuizzes
         .AsNoTracking()
         .FirstOrDefaultAsync(
             q => q.StudentId == studentId && q.Attempts.Any(a => a.Id == query.AttemptId), cancellationToken)
         ?? throw new NotFoundException("Quiz attempt", query.AttemptId);
     var attempt = quiz.Attempts.First(a => a.Id == query.AttemptId);
     ```
     A foreign / cross-tenant / unknown id → the root resolves null (the global filter excludes other tenants/soft-deleted;
     the `StudentId` predicate excludes another student in the same tenant) → `NotFoundException` → **`404`** (the §B.2
     IDOR/tenant boundary). Owned `Attempts` → `Questions` → `Options` load with the root.
  3. **Terminal gate:** `if (attempt.IsInProgress) throw new ForbiddenException("Finish the quiz to see your answers and
     score.", "quiz_attempt_in_progress");` (the §B.2 reason'd `403` — the key is never revealed mid-sitting; equivalently
     gate `attempt.Status == QuizAttemptStatus.InProgress`). Every other status (`Submitted`|`TimedOut`|`Forfeited`) is
     terminal → proceed.
  4. Resolve **`sessionTitle`** from `db.Sessions.IgnoreQueryFilters().Where(s => s.Id == quiz.GatedSessionId)
     .Select(s => s.Title).FirstOrDefaultAsync(ct)` (mirror the S4 review / staff name-resolution — name resolution ignores
     the query filters so an archived session still resolves a title). Header line "{sessionTitle} · Quiz review" (§B.1).
  5. **Questions:** order `attempt.Questions` by `Order` asc (1-based); per question sign `q.ImageObjectKey`
     (`fileStorage.GetSignedReadUrlAsync(...).Url`, null when the key is blank — the `QuizMappings.SignAsync` shape), and
     project options ordered by `Order` asc (0-based "DisplayOrder") as
     `StudentQuizReviewOptionDto(o.Id, o.Order, o.Text, o.IsCorrect)` (**`isCorrect` EXPOSED**); carry the question's
     `q.Mark`, `q.SelectedOptionId` (the student's pick, `null` when unanswered — common on a `TimedOut`/`Forfeited`
     attempt) and `q.IsCorrect`. **No `hintUrl`** — quiz questions carry none (§0; `QuizAttemptQuestion` has no hint field).
  6. **Score:** straight off the snapshot + root — `scorePercent = attempt.ScorePercent ?? 0` (`0` for a `Forfeited`
     attempt), `minPassPercent = quiz.MinPassPercent`, `timeSpentSeconds = attempt.TimeSpentSeconds` (the computed
     property — submitted−started, full window on timeout), `startedAtUtc`/`submittedAtUtc` off the attempt, `status` off
     the attempt, `quizId = quiz.Id`, `gatedSessionId = quiz.GatedSessionId`, `number = attempt.Number`. **No recompute** —
     the snapshot is the source of truth. *(Note `submittedAtUtc` is non-null for every terminal attempt — the gate
     guarantees terminality — so it maps as a `DateTimeOffset`, matching §B.1.)*
  7. `return quiz.ToReviewDto(attempt, sessionTitle, signedQuestions);` (the manual mapping — §3.2).

### 3.2 DTOs + mapping
Add to **`Features/Quizzes/DTOs/QuizDtos.cs`** (beside `StudentQuizDto` — keep the student quiz shapes together; the file's
comment "the student shape never carries option correctness" applies to the **live** `QuizOptionDto`/`QuizQuestionDto`, not
these review DTOs, which are the deliberate, gated post-termination exception). These are **distinct** records — do **not**
reuse or widen the live `QuizAttemptDto`/`QuizQuestionDto`/`QuizOptionDto` (which forbid `isCorrect`). Field order = the
contract §B.1 shape:

```csharp
/// <summary>One option in the student's own answer-key review — isCorrect EXPOSED (review only, post-termination).</summary>
public sealed record StudentQuizReviewOptionDto(Guid Id, int Order, string Text, bool IsCorrect);

/// <summary>One question in the student's quiz answer-key review: the drawn snapshot + the student's pick + correctness.
/// No hintUrl — quiz questions carry none (FR-PLAT-QB-005).</summary>
public sealed record StudentQuizReviewQuestionDto(
    Guid Id, int Order, string? BodyLatex, string? ImageUrl, int Mark,
    IReadOnlyList<StudentQuizReviewOptionDto> Options, Guid? SelectedOptionId, bool IsCorrect);

/// <summary>The caller's own TERMINAL quiz attempt with the answer key + score (contract §B.1, FR-STU-QZ-009). The ONLY
/// student surface that exposes quiz isCorrect, and only post-termination. Projects the immutable QuizAttempt snapshot.</summary>
public sealed record StudentQuizAttemptReviewDto(
    Guid AttemptId, Guid QuizId, Guid GatedSessionId, string? SessionTitle, int Number, QuizAttemptStatus Status,
    int ScorePercent, int MinPassPercent, DateTimeOffset StartedAtUtc, DateTimeOffset SubmittedAtUtc, int TimeSpentSeconds,
    IReadOnlyList<StudentQuizReviewQuestionDto> Questions);
```

Manual `.ToReviewDto(...)` extension in `QuizMappings` (no mapping library — never map in the handler body). The image
signing is async, so build the `StudentQuizReviewQuestionDto` list in the handler loop (as the S4 review handler does —
reuse the existing private `QuizMappings.SignAsync`) and pass it into a thin
`.ToReviewDto(this UserQuiz quiz, QuizAttempt attempt, string? sessionTitle, IReadOnlyList<StudentQuizReviewQuestionDto>
questions)` that assembles the root. `Status` is always terminal here (the gate guarantees it); `SubmittedAtUtc` is non-null
(terminal). Keep the signed-URL shape identical to `ToAttemptDtoAsync`'s.

### 3.3 The additive `id` on `StudentQuizAttemptSummaryDto`
In `QuizDtos.cs`, add `Guid Id` as the **first** positional field of `StudentQuizAttemptSummaryDto` (the §A #1 shape — `id`
leads, then `number`, `scorePercent`, …), and in `QuizMappings.ToStudentDto` pass `a.Id` as the first arg of the
`StudentQuizAttemptSummaryDto(...)` constructor inside the `quiz.Attempts.OrderBy(a => a.Number).Select(...)` projection.
**Purely additive** — nothing renamed/removed; the 5B-2 "no `isCorrect`" guard on the live shapes is untouched (the
summary never carried correctness and still doesn't).

## 4. API — endpoint group

Add the **sixth** route to the **existing** `QuizEndpoints : IEndpointGroup` (`Api/Endpoints/QuizEndpoints.cs`) — the same
`/api/me/quizzes` group, the same `RequireStudent()`, mirroring the five engine routes already there:

```csharp
group.MapGet("/attempts/{attemptId:guid}/review", GetReviewAsync)
    .RequireStudent()
    .WithName("GetMyQuizAttemptReview")
    .WithSummary("The caller's own TERMINAL attempt with the answer key + score (review only)")
    .Produces<StudentQuizAttemptReviewDto>()
    .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)   // staff, or quiz_attempt_in_progress
    .Produces<ProblemDetails>(StatusCodes.Status404NotFound);   // unknown / foreign / cross-tenant
```

```csharp
private static async Task<IResult> GetReviewAsync(
    Guid attemptId, ISender sender, CancellationToken cancellationToken)
    => Results.Ok(await sender.Send(new GetMyQuizAttemptReviewQuery(attemptId), cancellationToken));
```

A thin `ISender.Send(...)` delegate (like `GetBySessionAsync`). `RequireStudent()` gives the 401/403-staff; the handler
throws for the `403 quiz_attempt_in_progress` + `404`. **Do not** add a new endpoint group — the route belongs on the
existing `QuizEndpoints` (it's the same `/api/me/quizzes` surface, just a new sub-route). Update the group's XML-doc: the
class comment currently reads "No `isCorrect` is exposed here." — amend it to note the **one** gated exception (this review
route exposes the answer key for the caller's own **terminal** attempt only), so the comment stays accurate for the five
engine routes.

## 5. Migration

**None.** `UserQuiz` + its owned `QuizAttempt`/`QuizAttemptQuestion`/`QuizAttemptOption` snapshot, and `AssessmentEvent`,
all exist from 5B-2 — and the snapshot already stores `SelectedOptionId` + per-option `IsCorrect` + `Order`/`Text`/`Mark`/
`BodyLatex`/`ImageObjectKey`. The review is a **pure read** of the caller's own terminal attempt. The additive `id` is a
DTO field, not a column. *(No new field, no new aggregate — §0/§H of the contract: "No engine change beyond the one additive
`id` field and the new §B read.")*

## 6. Tests (`dotnet test -c Release`)

Integration (`WebApplicationFactory` + Testcontainers (Postgres + Redis), **Student-role JWT**). **Precondition for the
happy path:** a **quiz-gated** enrollment + a **terminal** attempt — reuse the 5B-2 quiz test helpers (enrol on a
quiz-gated session via `RedeemAsync`, then `GET /by-session/{B}` → `POST /{quizId}/attempts` → `PUT …/answer` for each drawn
question → `POST …/submit` to reach `Submitted`; for a `TimedOut`/`Forfeited` attempt drive the existing 5B-2 timer/hub
paths). Add a new file **`MyQuizAttemptReviewApiTests.cs`** (or extend the 5B-2 quiz integration suite). The §A guard test
(the live attempt route's raw JSON has **no** `isCorrect`) **must stay green** — the new review route is the deliberate
exception.

- **Answer key for the caller's own terminal attempt (§B.1, the happy path):** enrol → start → answer (e.g. 1-of-2 correct)
  → submit → `GET /api/me/quizzes/attempts/{attemptId}/review` → `200 StudentQuizAttemptReviewDto`: `attemptId`/`quizId`/
  `gatedSessionId` echo, `status == "Submitted"`, `sessionTitle` resolved; questions **ordered by `Order` asc (1-based)**,
  each with options **ordered by `Order` asc (0-based)** carrying `isCorrect`; each question's `selectedOptionId` echoes the
  student's pick + its `isCorrect`; the score (`scorePercent`/`minPassPercent`/`timeSpentSeconds`/`startedAtUtc`/
  `submittedAtUtc`) **matches the snapshot** (e.g. 1-of-2 ⇒ `scorePercent == 50` when marks are equal). **`isCorrect` IS
  present** here (the one surface that exposes it).
- **The additive `id` on the intro list (§A #1):** after the attempt terminates, `GET /api/me/quizzes/by-session/{B}` →
  `StudentQuizDto.attempts[]` each now carries `id` (== the `QuizAttempt.Id`), and that `id` is the one the review route
  accepts (assert the round-trip: `attempts[0].id` → `GET …/attempts/{that}/review` → `200`).
- **`403 quiz_attempt_in_progress` on the active attempt (§B.2):** start an attempt but do **not** submit (leave it
  `InProgress`), `GET …/attempts/{activeAttemptId}/review` → `403` with `reason == "quiz_attempt_in_progress"` and the
  verbatim `detail` "Finish the quiz to see your answers and score." (assert the `reason` extension, not just the status).
- **`404` IDOR / foreign / unknown (§B.2):** a **second** student's terminal attempt id → `404` (not the data — opaque,
  never the other student's answers); an **unknown** random GUID → `404`.
- **`404` cross-tenant (`NFR-SEC-010`):** an attempt id that exists in **another tenant** → `404` for a tenant-A student
  (the global filter on `UserQuiz` yields null → `NotFoundException`). Proves tenant isolation on the new read.
- **Auth gating (§B.2):** anonymous (no bearer) → `401`; a **staff** JWT (`StaffRole.Teacher`) → `403` (the `RequireStudent`
  filter, **no** `reason`); Student JWT → `200`/`403 quiz_attempt_in_progress`/`404` per the above.
- **Review NOT audited (§E):** assert **no new `audit_entries` row** is written for the `GET …/review` call (parity with the
  `/api/me/sessions` + `/api/me/catalogue` + S4 assignment-review reads; query the audit table before/after). The engine's
  state-change audit (start/submit = **student**, forfeit/timeout = **`System`**) is 5B-2's — written by those calls, **not**
  by the review read.
- **The 5B-2 `isCorrect` split holds (regression):** the existing 5B-2 guard — the **live** attempt's raw JSON (start `POST
  /{quizId}/attempts` → `QuizAttemptDto`) contains **no** `isCorrect` — **stays green**; add the mirror assertion that the
  **review** raw JSON **does** contain `isCorrect` (per-option + per-question), and the **by-session** intro raw JSON does
  **not** (only the additive `id`).
- **Engine untouched (regression):** the rest of the 5B-2 quiz suite (start randomisation, answer save-as-you-go, submit
  grade + best-of + `≥`-pass, focus → `assessment_events`, forfeit-on-disconnect → score 0, Hangfire timeout → `TimedOut`)
  still passes — this stream adds no change to the five engine routes, the hub, the lifecycle service or the timer job.

## Done = ready for wiring

Contract §B/§E/§H satisfied; the five §A engine routes + the `QuizHub` + the `QuizAutoSubmitJob` untouched; the
student/staff `isCorrect` split preserved (the live shapes stay correctness-free, the new `/review` route is the only —
gated, terminal-only — exception); the additive `id` lands on the intro summary; suite green (minus the known baseline
image test); **no migration**. Hand to `IMPLEMENTATION-PLAN-student-s5-wiring.md`.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the BACKEND stream of Student-Portal phase S5 for Salah Bahzad (.NET 10, Clean Architecture +
CQRS + source-gen Mediator). Edit backend/** ONLY. Add ONE new student READ endpoint + ONE additive DTO field. The
Phase-5B-2 quiz engine (the five /api/me/quizzes routes: by-session load, start, answer, submit, focus + the QuizHub +
the QuizAutoSubmitJob Hangfire timer + the Redis backplane) already exists and is REUSED AS-IS — do not touch it. NO
migration, NO new aggregate.

Read first, in order:
1. backend/CLAUDE.md (Multi-tenancy + EF query filters, Minimal API, Authentication/authorization, Testing).
2. docs/contracts/student-s5-quizzes.md — the FROZEN contract: §A (the five EXISTING engine routes + the live
   StudentQuizDto/QuizAttemptDto/QuizOptionDto that NEVER carry isCorrect — reused, do not touch — PLUS the one additive
   field: id on StudentQuizAttemptSummaryDto), §A.1 (the QuizHub pushes nothing — forfeit-on-disconnect only — do not
   touch it), §B (the NEW GET /api/me/quizzes/attempts/{attemptId}/review + StudentQuizAttemptReviewDto/
   StudentQuizReviewQuestionDto/StudentQuizReviewOptionDto + the 403 quiz_attempt_in_progress / 404 IDOR error modes), §E
   (reads not audited), §H (frozen vs stream-owned). Change the contract first if anything moves.
3. The templates to mirror: Application/Features/Assignments/Queries/GetMyAssignmentReview/GetMyAssignmentReviewHandler.cs
   (S4's per-question answer-key builder — resolve the caller's OWN row, gate terminal/completed, resolve sessionTitle via
   db.Sessions.IgnoreQueryFilters, order questions + options by Order, sign ImageObjectKey via IFileStorage, project
   options with isCorrect + per-question selectedOptionId + isCorrect — THIS is the per-question template, NOT the staff
   GetQuizReviewHandler which is attempt-level-scores-only and reads no snapshot) + its query record + the distinct review
   DTOs in Features/Assignments/DTOs/AssignmentDtos.cs; Features/Quizzes/DTOs/QuizDtos.cs (where the additive id on
   StudentQuizAttemptSummaryDto + its QuizMappings.ToStudentDto wire-up go — pass a.Id — and where the NEW distinct
   StudentQuizAttemptReviewDto/StudentQuizReviewQuestionDto/StudentQuizReviewOptionDto records + a manual .ToReviewDto() go;
   reuse the private QuizMappings.SignAsync); Domain/Entities/QuizAttempt.cs (READ-ONLY: the owned snapshot — Order is
   1-based on questions, 0-based on options, computed IsCorrect, SelectedOptionId, the attempt is owned by UserQuiz so
   resolve THROUGH the root); Features/Videos/Commands/StartVideoPlayback/StartVideoPlaybackHandler.cs +
   Application/Common/Exceptions/ForbiddenException.cs + Api/Middleware/GlobalExceptionHandler.cs (how a reason'd 403 is
   thrown and mapped to ProblemDetails Extensions["reason"]); Application/Common/Exceptions/NotFoundException.cs +
   Features/Quizzes/Queries/GetMyQuiz/GetMyQuizHandler.cs (the ICurrentUserResolver.UserId IDOR scope + NotFoundException
   pattern); Api/Endpoints/QuizEndpoints.cs (ADD the 6th route to THIS existing RequireStudent group + amend the class
   XML-doc "No isCorrect is exposed here" to note the one gated exception).

Build: NEW GetMyQuizAttemptReview query/handler under Features/Quizzes/Queries/GetMyQuizAttemptReview; the
StudentQuizAttemptReviewDto + StudentQuizReviewQuestionDto + StudentQuizReviewOptionDto records (isCorrect EXPOSED) + a
manual .ToReviewDto() in Features/Quizzes/DTOs/QuizDtos.cs; resolve the attempt THROUGH its owning UserQuiz
(db.UserQuizzes.FirstOrDefault(q => q.StudentId == currentUser.UserId && q.Attempts.Any(a => a.Id == attemptId)), then
pick the attempt) — null => NotFoundException => 404 (opaque IDOR/tenant boundary); gate the attempt TERMINAL (not
InProgress) else throw ForbiddenException("Finish the quiz to see your answers and score.", "quiz_attempt_in_progress")
=> 403 with reason; resolve sessionTitle of GatedSessionId via db.Sessions.IgnoreQueryFilters; order questions by Order
asc (1-based) + options by Order asc (0-based); sign ImageObjectKey; project per-option + per-question isCorrect +
selectedOptionId; score straight off the snapshot scorePercent + the root minPassPercent + TimeSpentSeconds. ALSO add the
purely-additive id (QuizAttempt.Id) as the FIRST field of StudentQuizAttemptSummaryDto and pass a.Id in
QuizMappings.ToStudentDto. Add ONE route GET /attempts/{attemptId:guid}/review to the existing QuizEndpoints group
(RequireStudent, Produces<…ReviewDto> + 403 + 404). DO NOT touch the five engine routes / the QuizHub / the
QuizAutoSubmitJob; DO NOT widen the live QuizAttemptDto/QuizQuestionDto/QuizOptionDto; NO migration.

Tests (xUnit v3 + Testcontainers (Postgres + Redis) + FluentAssertions, Student-role JWT — reuse the 5B-2 quiz helpers to
enrol on a quiz-gated session, start, answer, submit to reach a terminal attempt): 200 answer-key for the caller's own
terminal attempt (per-option + per-question isCorrect present, selectedOptionId echoed, ordered by Order, score matches the
snapshot); the additive id now appears on the intro StudentQuizDto.attempts[] and round-trips to the review route; 403 with
reason="quiz_attempt_in_progress" on the InProgress active attempt; 404 for another student's / unknown id; 404 cross-tenant
(NFR-SEC-010); 401 anon / 403 staff; review NOT audited (no new audit_entries row); the 5B-2 guard (the live attempt route's
raw JSON has NO isCorrect) STILL green + the review raw JSON DOES (per-option + per-question) + the by-session intro raw
JSON does NOT; engine untouched (start/answer/submit/focus/forfeit/timeout unchanged). Green gate: `dotnet test -c Release`
(the one pre-existing QuestionBank image test may stay red — baseline). Report it.
```
