# FROZEN CONTRACT — Phase 5B-2 · Proctored quiz engine + quiz review

> Status: **Frozen** · Created 2026-06-20 · Slice: Phase **5B-2** (the proctored half of 5B — **adds SignalR + Redis**).
> **Design-anchored** to the prototype `.claude/Salah Bahzad Teacher Portal/Admin Portal.dc.html`:
> **`scrReview()` line 1112 — the "Quiz attempts" tab** (lines 1128-1130) + the **Behaviour log** tab's focus-loss
> rows (1131-1134); **`scrAttendance()` line 1255 — the "Quiz best" + "Attempts" columns** (1269-1270);
> **`scrQuizSettings()` line 1091** (already built, Phase 3) defines the time/#questions/#attempts/min-pass rules the
> engine enforces. Both streams build to this; wiring proves it with **zero drift**. Change here first.
>
> Satisfies: quiz engine `FR-PLAT-QZ-001..010`; admin quiz review `FR-ADM-REV-002`; attendance quiz metrics
> `FR-PLAT-ATT-001/002`, `FR-ADM-ATT-001/002`; fixes gap **#7** (`≥` pass) and does **#6 right** (SignalR JWT + Redis
> backplane). The video-playback gate itself is **5C** (`FR-PLAT-VID-*`); 5B-2 only produces the *quiz-passed →
> videos-unlocked* state it will read.

## 0. Ground rules

- **Admin-only engagement.** The quiz **engine** (`§A`) is backend-only — the endpoints + the `QuizHub` a future
  student portal/app calls. 5B-2 ships **no** quiz-taking UI; wiring drives `§A` with a **student JWT** (+ a SignalR
  test client for disconnect/timer). The **admin** surface (`§B`) is the deliverable.
- **Auth:** engine REST = `RequireStudent`; the **`QuizHub`** authenticates with the **same platform JWT** — issue #6
  done right: the token rides the SignalR `access_token` query param **scoped to the hub path** and is **validated as
  a full JWT** (not the legacy insecure query-string-params scheme; `NFR-SEC-005`). Quiz review = `AttendanceRead`.
  Default-deny: anon→401, wrong-principal→403. **No permission/catalog change.**
- **New infra (this slice):** **Redis** added to the AppHost → SignalR **Redis backplane** (`NFR-SCAL-002`) + promote
  HybridCache to L1+L2. **Hangfire** (already in the stack) runs the authoritative auto-submit timer.
- **Tenant-scoped:** `UserQuiz`/`QuizAttempt` are `ITenantOwned` (auto-filtered); cover isolation in tests
  (`NFR-SEC-010`). **Snapshot fairness (`FR-PLAT-SES-007`):** each `QuizAttempt` is an immutable copy of the drawn
  questions; later bank edits never alter an existing attempt.
- **Migration required** (gated): `user_quizzes`, `quiz_attempts` (+ owned attempt questions/options); reuses the
  5B-1 `assessment_events` table (adds a `FocusLost`/`FocusReturned` type) and writes the existing `Attendance`
  shell's `BestQuizPercent`.

## A. Quiz engine — student-facing, backend-only

### Generation (real — replaces the 5B-1 `EnrollmentSideEffects` quiz no-op)
On `EnrollmentCreated`/`Extended` for session **B**: **iff** `B.PrerequisiteSessionId = A` **and** A has a
`QuizSetting` **and** A has **quiz-eligible** questions (`Question.IsValidForQuiz`), generate one `UserQuiz` for the
enrollment (`FR-PLAT-QZ-001`). The quiz is **sourced from A's** quiz-eligible bank and uses **A's** `QuizSetting`
(`FR-PLAT-QZ-002`, `FR-PLAT-SES-006`); passing it unlocks **B's** videos. **Idempotent** (one `UserQuiz` per
enrollment). No prerequisite, no quiz settings, or no eligible questions ⇒ **no quiz** (videos aren't quiz-gated).

### REST (`RequireStudent`)
| # | Method & path | Returns | Notes |
|---|---|---|---|
| 1 | `GET /api/me/quizzes/by-session/{sessionId}` | `StudentQuizDto` | The caller's quiz for gated session B; 404 if none. |
| 2 | `POST /api/me/quizzes/{quizId}/attempts` | `QuizAttemptDto` | Start an attempt: **409** if attempts exhausted or an attempt is already active. |
| 3 | `PUT /api/me/quizzes/attempts/{attemptId}/questions/{aqId}/answer` | `204` | Record an answer; **409** if the attempt is not `InProgress` or past its deadline. |
| 4 | `POST /api/me/quizzes/attempts/{attemptId}/submit` | `QuizAttemptResultDto` | Grade, update best-of + pass, consume the attempt. |
| 5 | `POST /api/me/quizzes/attempts/{attemptId}/focus` | `204` | Body `{ type: FocusLost\|FocusReturned, occurredAtUtc, durationMs? }` → `assessment_events`; **monitoring only, never auto-forfeit** (`FR-PLAT-QZ-006`). |

- **`StudentQuizDto`**: `{ id, gatedSessionId, settings:{ timeLimitMinutes, questionCount, attemptCount, minPassPercent },
  attemptsUsed, attemptsRemaining, bestPercent?, passed, activeAttemptId?, attempts:[{ number, scorePercent?, status,
  flag, startedAtUtc, submittedAtUtc? }] }`.
- **`QuizAttemptDto`** (start, #2): `{ attemptId, number, deadlineUtc, serverNowUtc, questions:[{ id, order,
  bodyLatex, imageUrl?, options:[{ id, order, text }] }] }` — **no `isCorrect`**; each attempt draws an independently
  **randomised** subset of `questionCount` quiz-eligible questions, one variation each (`FR-PLAT-QZ-003`).
- **`QuizAttemptResultDto`** (submit, #4): `{ scorePercent, status:'Submitted', bestPercent, passed, attemptsRemaining }`.
- **Best-of + pass (`FR-PLAT-QZ-007/008`, fixes #7):** `bestPercent` = **max** across attempts; `passed` =
  `bestPercent >= minPassPercent` (**`≥`**, the bug fix). Passing sets the enrollment's **videos-unlocked** state
  (read by the 5C video gate). All attempts stay visible.

### SignalR `QuizHub` (path `/hubs/quiz`, JWT-authenticated, Redis backplane)
- The student connects and **joins their active attempt**; the hub pushes countdown ticks and the
  submit/timeout/forfeit signal.
- **Single-sitting forfeit (`FR-PLAT-QZ-004`):** `OnDisconnectedAsync` (page close / connection loss) → **forfeit**
  the active attempt with **score 0**, status `Forfeited`, **consuming** the attempt. Connection↔attempt mapping
  lives in **Redis** so it holds across instances.
- **Server-side timer (`FR-PLAT-QZ-005`):** start schedules a **Hangfire** job at `deadlineUtc`; if the attempt is
  still `InProgress` at the deadline it **auto-submits** whatever is answered, status `TimedOut` (authoritative;
  survives a dropped connection / restart). Submit/forfeit cancels the job.
- **Immutability (`FR-PLAT-QZ-009`):** a `Submitted`/`Forfeited`/`TimedOut` attempt never re-opens; a new attempt may
  start while attempts remain.
- **Audit (`FR-PLAT-QZ-010`):** `QuizAttemptStarted` / `Submitted` (student actor) and `Forfeited` / `TimedOut`
  (**`System`** actor) each write one audit row; **focus-loss is high-volume → `assessment_events`, not the audit log.**

## B. Admin quiz review + attendance — `AttendanceRead` (`scrReview` Quiz tab; `scrAttendance` columns)

| # | Method & path | Returns | Notes |
|---|---|---|---|
| 6 | `GET /api/review/quizzes/{enrollmentId}` | `QuizReviewDto` | The `scrReview` "Quiz attempts" tab. `FR-ADM-REV-002`. |

```jsonc
// QuizReviewDto (scrReview lines 1128-1130: Attempt / Score / Time spent / Flags / When, "best" marked)
{ "bestPercent": 78, "passed": true, "minPassPercent": 60, "attemptsUsed": 2, "attemptsAllowed": 3,
  "attempts": [ { "number": 1, "scorePercent": 52, "timeSpentSeconds": 898, "flag": "Timeout",
                  "status": "TimedOut", "startedAtUtc": "…", "isBest": false },
                { "number": 2, "scorePercent": 78, "timeSpentSeconds": 702, "flag": "Clean",
                  "status": "Submitted", "startedAtUtc": "…", "isBest": true } ] }
// flag (UI pill): "Clean" (active) | "Timeout" (rejected) | "Forfeit" (pending) — derived from status.
```

- **Attendance columns are populated (no DTO change).** The 5B-1 `SessionAttendanceRowDto`/`StudentAttendanceRowDto`
  already carry `bestQuizPercent` + `quizAttemptCount`; 5B-2 only updates the attendance **queries** to join `UserQuiz`
  so those fields are real instead of `null/0`. The 5B-1 attendance + review endpoints are otherwise unchanged.
- The `scrReview` **Behaviour log** tab now also surfaces the quiz attempt's `FocusLost` `assessment_events`
  (the 5B-1 behaviour endpoint already reads that table — extend it to a quiz attempt, or add a sibling).

## C. Backend model (backend owns the internals)
- `UserQuiz` (root, `ITenantOwned`): `EnrollmentId, StudentId, GatedSessionId (B), SourceSessionId (A)`, a **snapshot**
  of A's `QuizSetting` (time/count/attempts/minPass — immune to later edits), `AttemptsUsed, BestPercent?, Passed`.
  Owns `QuizAttempt`.
- `QuizAttempt` (owned/immutable): `Number, Status (InProgress|Submitted|Forfeited|TimedOut), ScorePercent?,
  StartedAtUtc, DeadlineUtc, SubmittedAtUtc?`, and the **drawn questions** (immutable snapshot: body/image/mark +
  options `{Order,Text,IsCorrect}` + the student's `SelectedOptionId?`). `timeSpentSeconds` = submitted−started (or
  deadline−started on timeout).
- Reuses `AssessmentEvent` (+ `FocusLost`/`FocusReturned` types). A `QuizGradedEvent` → the attendance scorer writes
  `Attendance.BestQuizPercent` (`IAuditViaEventOnly`; the semantic audit is on the start/submit/forfeit/timeout event).

## D. Video-unlock state (boundary with 5C)
Passing the quiz (`FR-PLAT-QZ-008`) flips the enrollment's **videos-unlocked** state (e.g. `UserQuiz.Passed`, surfaced
on the enrollment). **5B-2 only records it.** The actual playback gate — checking *active enrollment + remaining
count + quiz-passed*, the per-video decrement, signed HLS and handoff code (`FR-PLAT-VID-001..007`) — is **5C**.
Attendance "videos watched" likewise stays 0 until 5C.

## E. Infrastructure (this slice)
- **AppHost:** `var redis = builder.AddRedis("redis"); api.WithReference(redis).WaitFor(redis);` (+ a fixed dev port
  like the Postgres/MinIO precedent so it's reachable for wiring).
- **API:** `AddSignalR().AddStackExchangeRedis(<redis conn>)`; map `/hubs/quiz`; configure JWT for the hub via
  `JwtBearerEvents.OnMessageReceived` reading `access_token` **only for the `/hubs/quiz` path**. Promote HybridCache to
  the Redis L2. Hangfire schedules/cancels the per-attempt auto-submit job.

## F. Frozen vs. stream-owned
- **Frozen:** the 6 routes + the `QuizHub` path/auth; the DTO field names/types; the student-vs-staff `isCorrect`
  split; the `≥` pass rule; best-of; the forfeit-on-disconnect / timer-auto-submit / focus-loss-not-forfeit
  semantics; generation from the **prerequisite's** bank+settings; the audit actor split (start/submit=student,
  timeout/forfeit=System; focus-loss→`assessment_events`).
- **Backend owns:** REST-vs-hub split for answers, randomisation + variation pick, grading math, the Redis key shape,
  the Hangfire job, EF mapping + migration.
- **Frontend owns:** the `scrReview` Quiz-tab layout + flag→pill mapping, time formatting, and reading the now-real
  attendance quiz columns. (No quiz-taking UI — engine is API-only.)
```
