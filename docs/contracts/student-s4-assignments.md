# FROZEN CONTRACT — Student Portal · S4 · Assignments (runner + answer-key review)

> Status: **Frozen** · Created 2026-06-21 · Slice: Student-Portal **S4** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S4). **Design anchor:** the prototype's **`ASSIGNMENT RUNNER`**
> screen (`screen === 'assignment'`) in `.claude/Salah Bahzad Student Portal/Student Portal.html`. Behaviour authority is
> `FR-STU-ASG-001..007` and `FR-PLAT-ASG-002/003/004/006/007/008`.
>
> Satisfies: the open-book **assignment runner** — open at any time, answer **one question at a time** with answers saved
> as the student goes (`FR-STU-ASG-001`), leave/resume freely (`FR-STU-ASG-002`), an **accumulated** timer that resumes
> on re-entry (`FR-STU-ASG-003`), a per-question **hint** (`FR-STU-ASG-004`), **LaTeX/image** rendering
> (`FR-STU-ASG-005`), and **auto-grade on completion** that updates the student's progress/score
> (`FR-STU-ASG-006`); and — **the new piece this slice adds to the backend** — an **answer-key review**: after
> completion the student reviews **their answer vs. the correct answer + their score** (`FR-STU-ASG-007`,
> `FR-PLAT-ASG-008`).
>
> **The assignment *engine* already exists** — Phase 5B-1 shipped the three `/api/me/assignments` routes (load → answer →
> behaviour events), proven live, frozen in `docs/contracts/phase5b1-assignments-attendance.md` §A. This contract
> **re-states that engine as-is** (§A, reused verbatim) and adds **one** new student read —
> **`GET /api/me/assignments/{assignmentId}/review`** (§B) — the **only** student-facing surface that exposes
> `isCorrect`, gated to the caller's own **`Completed`** assignment. Frontend + wiring cite this file field-for-field.
> **Change this file first if anything moves.**

## 0. Ground rules

- **Backend = ONE new read; the engine is reused.** The three solving routes (`GET .../by-session/{sessionId}`,
  `PUT .../{id}/questions/{aqId}/answer`, `POST .../{id}/events`) **already exist** (5B-1, §A) and are **reused
  verbatim** — no signature change. S4 adds **only** `GET /api/me/assignments/{assignmentId}/review` (§B). **No new
  aggregate, no migration** (`UserAssignment` + its owned `AssignmentQuestion`/`AssignmentOption` snapshot, and
  `AssessmentEvent`, all exist from 5B-1).
- **Grounding correction to the master plan.** §S4 read *"Backend: none."* That was an oversight: the existing student
  DTO **deliberately hides `isCorrect`** (5B-1 §A: "the student shape never carries option correctness"), and the only
  endpoint exposing correctness is the **staff** `GET /api/review/assignments/{enrollmentId}` (`AttendanceRead`-gated).
  So **`FR-STU-ASG-007`** ("review their answers vs. correct answers") has **no** student path yet. S4 closes that with
  the one read in §B — the standard backend + frontend + wiring three-stream split (like S1's anonymous grades read was
  the only backend work in an otherwise frontend-led slice).
- **Authenticated student surface.** Every endpoint here uses **`RequireStudent()`** (anon → 401, staff → 403) — identical
  to `/api/me/catalogue|sessions|assignments|quizzes|videos`. **The student id + tenant come from the JWT**
  (`ICurrentUserResolver.UserId` / `.TenantId`), never a URL id. `{assignmentId}` ownership is proven by
  `UserAssignment.StudentId == currentUser.UserId`; a foreign / cross-tenant / unknown id resolves to **404**, never the
  other student's data (no IDOR surface, `NFR-SEC-007`).
- **Tenant isolation is automatic.** `UserAssignment`/`AssignmentQuestion`/`AssessmentEvent` are `ITenantOwned` → the EF
  global query filter scopes them to the caller's tenant and excludes soft-deleted rows. **Never** write a per-handler
  `Where(x => x.TenantId == …)`. Cross-tenant isolation is covered by an integration test on the new read (`NFR-SEC-010`).
- **The student/staff `isCorrect` split is preserved.** The runner's `StudentAssignmentDto` (§A) **never** carries
  correctness — the 5B-1 invariant and its guard test (`AssignmentEngineTests`: "raw JSON never contains `isCorrect`")
  stand **unchanged**. The **only** student surface that reveals the answer key is §B, and **only** for the caller's own
  **`Completed`** assignment (an in-progress assignment → `403`, §B.2) — you can never see the key before you submit.
- **Reads are not audited.** Both the engine load (`GET .../by-session`) and the new review (`GET .../{id}/review`) are
  **pure reads of the caller's own homework** — **not** audited (parity with `/api/me/catalogue` + `/api/me/sessions`;
  low-sensitivity, not the audited private ID-image read). The **state changes** in the engine *are* recorded as the 5B-1
  design dictates: the answer `PUT` writes an `Answered` `AssessmentEvent` (and, on the last answer, the **`System`**
  auto-grade), and `POST /events` appends behaviour to `assessment_events` — **high-volume telemetry, not the audit log**
  (§A, frozen by 5B-1).
- **Auto-grade on the last answer — there is no separate "submit" call.** The prototype's **"Submit assignment"** button
  is just the answer `PUT` for the **last** unanswered question; the engine auto-grades server-side at that moment
  (`FR-PLAT-ASG-006`) — sets `Status = Completed`, computes `ScoreMarks`/`CorrectCount`, writes `Attendance.AssignmentScore`
  (percent, `System` actor). **The frontend never posts a "submit"** — it answers the last question, then navigates.
- **Enums over the wire are string names** (`JsonStringEnumConverter`) — the frontend models them as string unions. Dates
  are ISO-8601 `…AtUtc`. Times are integer **seconds** (`timeSpentSeconds`), rendered `M:SS` by the UI (the prototype's
  `fmt(sec)`). Question/option images are R2 keys → **signed URLs on read** (`imageUrl`); the hint is a `hintUrl`.

## A. Assignment engine — **EXISTS** (frozen by 5B-1 §A · re-stated for the frontend · `RequireStudent`)

The runner builds against these three routes **unchanged**. Authority = `docs/contracts/phase5b1-assignments-attendance.md`
§A; re-stated here so the frontend has the shapes in one place. **The frontend must not assume any other engine route**
(there is no "submit", no "list my assignments", no per-assignment GET-by-id — the runner loads **by session**, which it
always has from the S3 session-detail context).

| # | Method & path | Returns | Notes |
|---|---|---|---|
| 1 | `GET /api/me/assignments/by-session/{sessionId}` | `StudentAssignmentDto` | The caller's assignment for that session (the enroll side-effect created it). **`404`** if the caller has no enrollment for the session. Re-`GET` returns **saved answers + accumulated `timeSpentSeconds`** (resumable, `FR-STU-ASG-002`). **No `isCorrect`.** |
| 2 | `PUT /api/me/assignments/{assignmentId}/questions/{aqId}/answer` | `AssignmentProgressDto` | Body `{ "selectedOptionId": "guid" }`. Records the answer (`FR-STU-ASG-001`), logs an `Answered` event. **When the last unanswered question is answered → auto-grade → `Status` `Completed` + `Attendance.AssignmentScore`** (`FR-STU-ASG-006`, `System` actor). Re-answering a question before completion is allowed; answering after `Completed` → **`409`**. |
| 3 | `POST /api/me/assignments/{assignmentId}/events` | `204` | Body `{ "type": "Entered"\|"Left"\|"Navigated", "questionOrder"?: int, "occurredAtUtc": "…", "elapsedMs"?: int }`. Appends behaviour event(s) **and accrues time** (`elapsedMs` → `timeSpentSeconds`) (`FR-STU-ASG-003`, `FR-PLAT-ASG-004/005`). High-volume → `assessment_events`, **not** the audit log. **`Answered` is NOT a valid `type` here** (it is logged by #2). |

```jsonc
// StudentAssignmentDto (#1) — the SOLVING shape; correctness is NEVER present (5B-1 invariant)
{
  "id": "guid",                         // the userAssignment id — pass to #2/#3 and §B
  "sessionId": "guid",
  "status": "InProgress",               // "InProgress" | "Completed"
  "timeSpentSeconds": 0,                // accumulated across sittings (authoritative; the UI's timer resumes from this)
  "questions": [
    {
      "id": "guid",                     // the assignmentQuestion id — the {aqId} of #2
      "order": 1,                       // 1-based
      "bodyLatex": "string|null",
      "imageUrl": "string|null",        // short-lived signed R2 URL; null if no image
      "hintUrl": "string|null",         // the per-question hint (FR-STU-ASG-004); null if none configured
      "options": [
        { "id": "guid", "order": 0, "text": "string" }   // NO isCorrect
      ],
      "selectedOptionId": "guid|null"   // the student's saved choice; null until answered (resume)
    }
  ]
}
// AssignmentProgressDto (#2) — { "answeredCount": 0, "questionCount": 0, "status": "InProgress" }
```

## B. Assignment review — `GET /api/me/assignments/{assignmentId}/review` (**NEW** · `RequireStudent`)

`RequireStudent` · `200 StudentAssignmentReviewDto`. The caller's **own**, **`Completed`** assignment with the **answer
key** — per-question and per-option `isCorrect`, the student's `selectedOptionId`, marks, and the overall score
(`FR-STU-ASG-007`, `FR-PLAT-ASG-008`). This is the **only** student endpoint that exposes correctness, and **only** post-
completion. Backend builds it like the staff `GetAssignmentReviewHandler` (`Features/Review/…`) but keyed by
**`assignmentId`** and scoped to **`StudentId == currentUser.UserId`** (not `AttendanceRead`), with a **`Completed`** gate.

### B.1 Result — `StudentAssignmentReviewDto`

```jsonc
// 200 · StudentAssignmentReviewDto
{
  "id": "guid",                         // the userAssignment id (echo of the route param)
  "sessionId": "guid",
  "sessionTitle": "string|null",        // for the review header: "{sessionTitle} · Assignment review"
  "status": "Completed",                // always "Completed" here (the endpoint gates it — §B.2)
  // score (FR-STU-ASG-007)
  "correctCount": 7, "questionCount": 9,
  "scoreMarks": 14, "maxMarks": 18, "percent": 78,   // percent = round(100 × scoreMarks / maxMarks); 0 when maxMarks == 0
  "timeSpentSeconds": 1104,
  "completedAtUtc": "…",
  // the answer key
  "questions": [
    {
      "id": "guid", "order": 1,
      "bodyLatex": "string|null", "imageUrl": "string|null",
      "mark": 2,                        // the question's weight
      "hintUrl": "string|null",
      "options": [
        { "id": "guid", "order": 0, "text": "string", "isCorrect": true }   // isCorrect EXPOSED (review only)
      ],
      "selectedOptionId": "guid|null",  // what the student picked
      "isCorrect": true                 // selectedOptionId is the correct option
    }
  ]
}
```

- The shape mirrors the **staff** `AssignmentReviewDto` (5B-1 §C) **minus `studentName`** (it's the caller) **plus
  `id`/`sessionId`/`completedAtUtc`**. It is a **distinct DTO** (`StudentAssignmentReviewDto`/`StudentReviewQuestionDto`/
  `StudentReviewOptionDto`) — do **not** reuse the runner's `StudentAssignmentDto` (which forbids `isCorrect`), and do
  **not** widen the runner DTO. Questions are ordered by `Order` asc; options by `Order` asc.

### B.2 Error modes — ProblemDetails

| Status | Machine `reason` | Readable `detail` (render it) | When |
|---|---|---|---|
| `401` | — | (unauthorized) | No bearer (anonymous). |
| `403` | — | (forbidden) | A **staff** JWT (the `RequireStudent` filter). |
| `403` | `assignment_in_progress` | "Finish the assignment to see your answers and score." | The assignment is the caller's but **`Status == InProgress`** — the key is **never** revealed pre-completion (`FR-STU-ASG-007` is "after completion"). |
| `404` | — | (not found) | `{assignmentId}` is unknown, **another student's**, or **another tenant's** — the IDOR/tenant boundary (opaque: never reveal existence). |
| `200` | — | — | The caller's own **`Completed`** assignment → the answer key. |

> The S3 session-detail **"Review assignment"** CTA only renders when `assignment.status == "Completed"`, so the `403
> assignment_in_progress` path is the deep-link/edge case — surfaced as a friendly "finish first" message, not an error.

## C. Runner interaction rules (frozen semantics the frontend implements against §A)

The prototype draws one question at a time inside a `max-width:620px` card. These rules bind the **behaviour**; the
prototype binds the **layout/copy** (§ design anchor). Where they conflict, the prototype wins on pixels/copy, this
contract wins on the engine calls + state.

- **One question at a time, saved as you go (`FR-STU-ASG-001/003`):** each MCQ pick → `PUT …/answer` (#2) for the current
  question's `{aqId}` with `{ selectedOptionId }`; the `AssignmentProgressDto` updates the answered count. Picking a
  different option before completion re-`PUT`s (allowed). **Do not batch** answers — persist each immediately (the engine
  is the store; there is no client-only draft).
- **Prev/next + auto-submit on last:** `← Previous` / `Next question`; on the **last** question the primary button reads
  **"Submit assignment"** and its click is the **answer `PUT` for the last unanswered question** — which auto-grades
  server-side (§0). After it resolves `Completed`, navigate back to the **session detail** (the prototype's
  `go('sessionDetail')`) — **there is no separate results screen in the runner** (§ design anchor: assignments have no
  inline results page; the score lives in the §B review + the S3 detail card).
- **Accumulated, resumable timer (`FR-STU-ASG-002/003`):** the displayed timer **starts from `timeSpentSeconds`** (the
  authoritative accumulated total from #1) and ticks up locally; the elapsed delta is **accrued server-side via the
  events `POST` `elapsedMs`** (#3) on `Navigated`/`Left` — **not** via the answer `PUT` (whose body is `{ selectedOptionId }`
  only). On re-entry the timer resumes from the new `timeSpentSeconds`. The exact flush cadence is **frontend-owned**
  (e.g. send `elapsedMs` on each navigate and on leave) — the engine is the source of truth.
- **Behaviour trail (`FR-PLAT-ASG-005`):** `POST /events` (#3) `Entered` on open, `Navigated` (with `questionOrder`) on
  prev/next, `Left` on exit/route-away — these feed the staff behaviour timeline (5B-1 §C #8). `Answered` is logged by the
  answer `PUT`, **never** posted here.
- **Hint (`FR-STU-ASG-004`):** the per-question **"Show hint"/"Hide hint"** toggle reveals `hintUrl` when present (hide
  the control when `hintUrl == null`). The prototype renders the hint inline; if `hintUrl` is a video/explainer link,
  open it (the runner does not embed a player).
- **LaTeX + image (`FR-STU-ASG-005`):** render `bodyLatex` via the shared `LatexPreview` (best-effort, the admin/S3
  pattern — no KaTeX/MathJax dependency) and `imageUrl` inline; option `text` likewise.
- **Reachable even when the session is expired (`FR-STU-SES-001`):** the assignment opens and is solvable/reviewable after
  the enrollment's `ExpiresAtUtc` has passed — the runner is **not** gated by expiry (only videos + the quiz are). #1
  still returns the assignment; #2/#3 still work; §B still reviews.

## D. Review screen semantics (frozen — what the answer key shows)

Driven by §B's `StudentAssignmentReviewDto` (`FR-STU-ASG-007`). The prototype has **no** dedicated assignment-review
screen (assignments submit straight back to the session detail), so the review screen is a **new** student screen built
to the contract, mirroring the **admin** `AssignmentReviewComponent`'s question/option treatment for visual consistency:

- **Header:** `"{sessionTitle} · Assignment review"` + a **score** (e.g. `78%` / `{scoreMarks}/{maxMarks} marks` /
  `{correctCount} of {questionCount} correct`) + **time** (`M:SS` from `timeSpentSeconds`) — the staff `scrReview`
  header, student-scoped.
- **Per question:** the body (LaTeX/image) + each option with **three** visual states from `isCorrect` +
  `selectedOptionId`: the **correct** option marked (green check), the student's **wrong** pick marked (red, when
  `selectedOptionId` is set and that option's `isCorrect == false`), and a per-question right/wrong indicator from the
  question's `isCorrect`. An **unanswered** question (`selectedOptionId == null`) shows the correct option only.
- **Read-only** — the review never re-`PUT`s an answer (the assignment is `Completed` and immutable, mirroring the quiz
  rule `FR-STU-QZ-009`).

## E. Audit (`FR-PLAT-AUD-002`)

- `GET /api/me/assignments/by-session/{sessionId}` (§A #1) and `GET /api/me/assignments/{assignmentId}/review` (§B) —
  **pure reads of the caller's own homework, not audited** (parity with `/api/me/catalogue` + `/api/me/sessions`).
- The engine's **state changes** are recorded by 5B-1's design (unchanged): the answer `PUT` (§A #2) logs an `Answered`
  `AssessmentEvent` and the **`System`** auto-grade on completion (`FR-PLAT-AUD-005`); `POST /events` (§A #3) appends to
  `assessment_events` — **telemetry, not the audit log**.

## F. Deferred / **NOT built** (master plan §3.3 / §7)

- **No inline assignment results screen in the runner** — the prototype submits straight to the session detail; the
  graded outcome lives in the **§B review** + the S3 detail card. (The dedicated *review* screen, §D, is reached from the
  S3 **"Review assignment"** CTA / the runner's post-submit navigation — it is **not** an in-runner results page.)
- **No engine change** — the three §A routes, the grading math, the snapshot, the `Attendance` write, and the prerequisite
  gate are reused exactly as 5B-1 shipped them. S4 touches **only** the new §B read.
- **No file-upload / free-text answers** — assignments are MCQ-only (`FR-PLAT-QB-001`); ignore the shared `FileUpload`
  for the runner.
- **Quizzes (S5)** — the proctored quiz runner + results + the `QuizHub` are **S5**, a separate slice in the same
  `feature-assessment` lib. S4 ships **only** the assignment runner + review; the S3 session-detail **Quiz** card stays a
  placeholder until S5.

## G. Frozen vs. stream-owned

- **Frozen (this file):** the **reuse** of the three §A engine routes (no signature change); the **new**
  `GET /api/me/assignments/{assignmentId}/review` path + `RequireStudent` + the `StudentAssignmentReviewDto` /
  `StudentReviewQuestionDto` / `StudentReviewOptionDto` field names + types + ordering (§B.1); the **`Completed`** gate +
  the `403 assignment_in_progress` / `404` IDOR boundary (§B.2); the **student-vs-staff `isCorrect` split** (correctness
  is exposed **only** in §B, **only** for the caller's own `Completed` assignment; the runner DTO stays correctness-free,
  §0/§A); the runner interaction semantics — one-at-a-time save, auto-grade-on-last (no "submit" call), accumulated timer
  via the events `elapsedMs`, behaviour events, expiry-doesn't-lock-the-assignment (§C); the review screen semantics
  (§D); "reads not audited" (§E).
- **Backend owns:** the query folder/name (`Features/Assignments/Queries/GetMyAssignmentReview/` — implementer's call,
  keep the route + DTO frozen), the DTO + `.ToReviewDto()` mapping location, the `AssignmentEndpoints` (the existing
  `IEndpointGroup` mapping `/api/me/assignments`) wiring of the new route, the `sessionTitle` resolution join
  (`IgnoreQueryFilters` on the name, mirroring the staff
  review), the `Completed`-gate + IDOR check, and the integration tests.
- **Frontend owns:** the **new** `libs/student-portal/feature-assessment` lib, the `AssignmentService` in data-access
  (`assignment(sessionId)` / `answer(assignmentId, aqId, optionId)` / `event(assignmentId, …)` / `review(assignmentId)`),
  the **runner** screen (one-question-at-a-time MCQ, LaTeX/image, hint toggle, accumulated timer, progress, prev/next,
  auto-submit-on-last → session detail), the **answer-key review** screen (§D), the `/sessions/:id/assignment(/review)`
  routes, the replacement of S3's `SessionDetailComponent.openAssignment()` placeholder with real navigation, and the
  Jest specs (`whenStable()`, not `fakeAsync`).
- **Wiring owns:** proving the slice live on the Aspire stack — load → answer-through → auto-grade-on-last (the
  `Attendance.AssignmentScore` write + `Status Completed` + the S3 progress flip) → the **§B review** returns the answer
  key for the caller's own completed assignment (per-option `isCorrect`, score), with the **`403 assignment_in_progress`**
  on an in-progress assignment, the **404** IDOR/tenant/foreign boundary, **401** anon / **403** staff, tenant isolation,
  and the runner's behaviour events + accumulated timer landing in `assessment_events`/`timeSpentSeconds`. The **runner
  engine** itself is 5B-1's — any drift there is a 5B-1 finding, not S4's.
