# FROZEN CONTRACT — Phase 5B-1 · Assignments engine + Attendance + Assignment/Behaviour review

> Status: **Frozen** · Created 2026-06-20 · Slice: Phase **5B-1** (the open-book half of 5B — **no SignalR/Redis**).
> **Design-anchored** to the prototype `.claude/Salah Bahzad Teacher Portal/Admin Portal.dc.html` —
> **`scrAttendance()` (line 1255)** and **`scrReview()` (line 1112)**, its **Assignment** + **Behaviour log** tabs
> (the **Quiz attempts** tab and quiz columns are **5B-2**). Both streams build to this; wiring proves it with
> **zero drift**. Change here first.
>
> Satisfies: assignments `FR-PLAT-ASG-001..008`; attendance `FR-PLAT-ATT-001..003`; enrollment gate
> `FR-PLAT-ENR-007`; admin reporting `FR-ADM-ATT-001..004`, `FR-ADM-REV-001`/`-003`; snapshot fairness
> `FR-PLAT-SES-007`. (Quiz engine `FR-PLAT-QZ-*`, `FR-ADM-REV-002`, video gate `FR-PLAT-VID-*` → later slices.)

## 0. Ground rules

- **Admin-only engagement.** The assignment **engine** is backend-only — the same endpoints a future student
  portal/app will call (`§A`). 5B-1 ships **no** student-solving UI; the wiring stream drives `§A` with a
  **student JWT** (exactly like Phase-4 redeem #12). The **admin** screens (`§B`/`§C`) are the deliverable.
- **Auth/permissions (already exist — NO catalog change in 5B-1):** engine = `RequireStudent`; attendance + review
  reads = `AttendanceRead`; exports = `AttendanceExport`. Default-deny: anon→401, wrong-principal→403.
- **Tenant scoping:** `UserAssignment`/`AssignmentQuestion`/`AssessmentEvent`/`Attendance` are `ITenantOwned` →
  the EF global filter scopes them automatically (unlike `AuditEntry`). Still cover isolation in tests (`NFR-SEC-010`).
- **Snapshot fairness (`FR-PLAT-SES-007`):** the assignment is an **immutable copy** of the bank at generation
  time — editing/deleting bank questions afterwards never alters an existing `UserAssignment`.
- **Migration required** (gated, never auto-applied): `user_assignments`, `assignment_questions` (owned),
  `assessment_events`; plus writes to the existing `attendance` shell.
- **Money/enums** serialize as strings; `PagedResult<T>` envelope; dates ISO-8601; images are R2 keys → **signed
  URLs on read**.

## A. Assignment engine — student-facing, backend-only (`RequireStudent`)

> Mirrors Phase-4 redeem #12: a student-portal path with **no admin screen**. Kept **minimal** — just enough to
> generate gradeable data + behaviour; the rich solving UX is the future student portal.

| # | Method & path | Returns | Notes |
|---|---|---|---|
| 1 | `GET /api/me/assignments/by-session/{sessionId}` | `StudentAssignmentDto` | The caller's assignment for that session (the side-effect created it on enroll). 404 if the caller has no enrollment. |
| 2 | `PUT /api/me/assignments/{assignmentId}/questions/{aqId}/answer` | `AssignmentProgressDto` | Body `{ selectedOptionId }`. Records the answer (`FR-PLAT-ASG-003`), logs an `Answered` event, accrues time. **When the last unanswered question is answered → auto-grade → write `Attendance.AssignmentScore` (percent), status `Completed`** (`FR-PLAT-ASG-006`); the auto-grade is attributed to the **`System`** actor (`FR-PLAT-AUD-005`). |
| 3 | `POST /api/me/assignments/{assignmentId}/events` | `204` | Body `{ type: Entered\|Left\|Navigated, questionOrder?, occurredAtUtc, elapsedMs? }`. Appends behaviour event(s) + accrues time (`FR-PLAT-ASG-004/005`). High-volume → `assessment_events`, **not** the audit log. |

- **Open-access/resumable (`FR-PLAT-ASG-002`):** no single-sitting constraint; re-`GET` returns saved answers +
  accumulated `timeSpentSeconds`. **Generation is idempotent** — re-enroll/extend reuses the existing
  `UserAssignment` (the assignment is retained even past expiry, `FR-PLAT-ENR-003`).
- `StudentAssignmentDto`: `{ id, sessionId, status, timeSpentSeconds, questions: [{ id, order, bodyLatex,
  imageUrl?, hintUrl?, options: [{ id, order, text }], selectedOptionId? }] }` — **no `isCorrect` exposed to the
  student**. `AssignmentProgressDto`: `{ answeredCount, questionCount, status }`.

## B. Attendance — admin (`AttendanceRead` / export `AttendanceExport`) — `scrAttendance` line 1255

| # | Method & path | Returns | Notes |
|---|---|---|---|
| 4 | `GET /api/attendance/sessions/{sessionId}` | `PagedResult<SessionAttendanceRowDto>` | "By session" cohort matrix (`FR-ADM-ATT-001`). |
| 5 | `GET /api/attendance/students/{studentId}` | `PagedResult<StudentAttendanceRowDto>` | "By student" per-session breakdown (`FR-ADM-ATT-002`). |
| 6 | `GET /api/attendance/sessions/{sessionId}/export` · `GET /api/attendance/students/{studentId}/export` | CSV | Streamed `text/csv` + `Content-Disposition`; **audited explicitly** via `IAuditWriter` (GET bypasses the interceptor — same as Phase-4 code export). `FR-ADM-ATT-004`. |

```jsonc
// SessionAttendanceRowDto (one enrolled student)
{ "enrollmentId":"guid", "studentId":"guid", "studentName":"string",
  "videosWatched": 0, "videosTotal": 3,        // videosWatched is fed by the 5C video gate → 0 in 5B-1
  "assignmentPercent": 80,                       // null until the student completes the assignment
  "bestQuizPercent": null, "quizAttemptCount": 0 } // null/0 until 5B-2
// StudentAttendanceRowDto: { enrollmentId, sessionId, sessionTitle, videosWatched, videosTotal,
//                            assignmentPercent?, bestQuizPercent?, quizAttemptCount }
```
- Rows are the session's (or student's) **`Active`/`Expired` enrollments**, joined to `Attendance` + student/session
  names + the session's video count. `assignmentPercent` = `Attendance.AssignmentScore`. The matrix's **Videos**
  and **Quiz best/Attempts** columns render but stay 0/`—` until 5C/5B-2.

## C. Assignment review — admin (`AttendanceRead`) — `scrReview` line 1112, Assignment + Behaviour tabs

| # | Method & path | Returns | Notes |
|---|---|---|---|
| 7 | `GET /api/review/assignments/{enrollmentId}` | `AssignmentReviewDto` | Per-question submitted-vs-correct + score + time (`FR-ADM-REV-001`). |
| 8 | `GET /api/review/assignments/{enrollmentId}/behaviour` | `BehaviourEventDto[]` | The in-assessment timeline (`FR-ADM-REV-003`). |

```jsonc
// AssignmentReviewDto  (header = scrReview line 1138-1139: name, "{session} · Assignment review", Score, Time)
{ "studentName":"string", "sessionTitle":"string",
  "correctCount": 7, "questionCount": 9, "scoreMarks": 14, "maxMarks": 18, "percent": 78,
  "timeSpentSeconds": 1104, "status": "InProgress|Completed",
  "questions": [ { "order":1, "bodyLatex":"…", "imageUrl":null, "mark":2, "hintUrl":null,
                   "options":[ { "id":"guid","order":0,"text":"…","isCorrect":true } ],
                   "selectedOptionId":"guid|null", "isCorrect": true } ] }
// BehaviourEventDto: { "type":"Entered|Left|Answered|Navigated", "label":"Answered Q1",
//                      "questionOrder": 1, "occurredAtUtc":"…" }
```
- Review is **staff-only** and **shows `isCorrect` + the correct option** (unlike the student `§A` shape).
- The **Quiz attempts** tab is rendered **disabled/empty** in 5B-1 (its data + endpoint are 5B-2).

## D. Enrollment gate (`FR-PLAT-ENR-007`) — enforced in the Phase-4 enroll path
On redeem (#12) / unlock (#9): if `Session.PrerequisiteSessionId != null`, the student must have a **`Completed`**
`UserAssignment` for that prerequisite session → else **409** "Complete the prerequisite assignment first." If the
prerequisite has **no question bank**, the gate passes vacuously (no assignment to complete). This closes the
Phase-4 deferral ("needs real assignments; enforced Phase 5").

## E. Backend model (backend owns the internals)
- `UserAssignment` (root, `ITenantOwned`): `EnrollmentId, StudentId, SessionId, Status (InProgress|Completed),
  ScoreMarks?, MaxMarks, CorrectCount?, QuestionCount, TimeSpentSeconds, StartedAtUtc, CompletedAtUtc?`. Owns
  `AssignmentQuestion` (immutable snapshot: `QuestionId, Order, BodyLatex, ImageObjectKey, Mark, HintUrl`, the
  snapshotted options `{Order,Text,IsCorrect}`, and the student's `SelectedOptionId?`/`AnsweredAtUtc?`).
- `AssessmentEvent` (append-only, `ITenantOwned`, separate high-volume table): `UserAssignmentId, Type, QuestionOrder?,
  OccurredAtUtc` (+ a nullable `DurationMs`). Reused by 5B-2's quiz focus-loss.
- Generation snapshots **one variation per question** (`FR-PLAT-ASG-001`) — pick the base question or a random
  variation; copy its options. `MaxMarks` = Σ question marks; `percent` = `round(ScoreMarks/MaxMarks*100)`.

## F. Frozen vs. stream-owned
- **Frozen:** the 8 routes + their auth; the DTO field names/types; the student-vs-staff `isCorrect` split; the gate
  (409 + vacuous-pass rule); auto-grade → `Attendance.AssignmentScore` (percent) by the `System` actor.
- **Backend owns:** variation-pick strategy, grading math, snapshot/EF mapping, the migration, CSV columns.
- **Frontend owns:** the `scrAttendance`/`scrReview` layout, the behaviour-event icon/accent + label mapping,
  progress-bar rendering, and showing Videos/Quiz columns as pending (0/`—`) until 5C/5B-2.
```
