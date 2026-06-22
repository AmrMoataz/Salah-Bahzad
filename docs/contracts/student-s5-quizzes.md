# FROZEN CONTRACT — Student Portal · S5 · Quizzes (proctored runner + per-attempt answer-key review)

> Status: **Frozen** · Created 2026-06-22 · Slice: Student-Portal **S5** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S5 — Quizzes). **Design anchor:** the prototype's **`quizIntro`**,
> **`quiz`** (runner) and **`quizResults`** screens (+ the **"Leave the quiz?"** modal) in
> `.claude/Salah Bahzad Student Portal/Student Portal.html`. Behaviour authority is `FR-STU-QZ-001..010` and
> `FR-PLAT-QZ-001..010`.
>
> Satisfies: the proctored **single-sitting** quiz — an **informed intro** (rules, time, attempts remaining, best
> score, the "one sitting / leaving forfeits" warning, `FR-STU-QZ-001/002`); a **runner** with a visible **countdown**
> whose **authoritative timer is server-side** (`FR-STU-QZ-003`), **forfeit** on close/navigate/disconnect with score 0
> (`FR-STU-QZ-004`), **focus-loss detection recorded — not forfeited** (`FR-STU-QZ-005`), **auto-submit** on expiry
> (`FR-STU-QZ-006`), **randomised** LaTeX/image questions (`FR-STU-QZ-007`); **results** showing the attempt score + the
> **best-of** (`FR-STU-QZ-008`); and — **the new piece this slice adds to the backend** — a **per-attempt answer-key
> review**: the student reviews each attempt's questions, **their answers, and the correct answers**
> (`FR-STU-QZ-009`); passing (best `≥` min) unlocks the session's **videos** (`FR-STU-QZ-010`).
>
> **The quiz *engine* already exists** — Phase 5B-2 shipped the five `/api/me/quizzes` routes + the `QuizHub`, proven
> live, frozen in `docs/contracts/phase5b2-quizzes.md` §A. This contract **re-states that engine** (§A, reused) with
> **one additive field** (an attempt `id` on the summary list, §A #1) and adds **one** new student read —
> **`GET /api/me/quizzes/attempts/{attemptId}/review`** (§B) — the **only** student-facing surface that exposes quiz
> `isCorrect`, gated to the caller's own **terminal** attempt. Frontend + wiring cite this file field-for-field.
> **Change this file first if anything moves.**

## 0. Ground rules

- **Backend = ONE new read + ONE additive engine-DTO field; the rest of the engine is reused verbatim.** The five
  engine routes (`GET .../by-session/{sessionId}`, `POST .../{quizId}/attempts`,
  `PUT .../attempts/{attemptId}/questions/{aqId}/answer`, `POST .../attempts/{attemptId}/submit`,
  `POST .../attempts/{attemptId}/focus`) **already exist** (5B-2, §A) and are **reused** with **no signature change**.
  S5 adds **only** `GET /api/me/quizzes/attempts/{attemptId}/review` (§B) **and** adds one field — **`id`** (the attempt
  `Guid`) — to the existing `StudentQuizAttemptSummaryDto` (§A #1) so the intro's attempts list can address each
  terminal attempt's review. **No new aggregate, no migration** (`UserQuiz` + its owned `QuizAttempt` /
  `QuizAttemptQuestion` / `QuizAttemptOption` snapshot, and `AssessmentEvent`, all exist from 5B-2 — and the snapshot
  already stores `SelectedOptionId` + per-option `IsCorrect` + `Order`/`Text`/`Mark`/`BodyLatex`/`ImageObjectKey`, so
  the review read is a pure projection).
- **Grounding correction to the master plan (two of them).** §S5 read *"Backend: none."* That was an oversight —
  identical to S4's. The student quiz shapes **deliberately hide `isCorrect`** (5B-2: `QuizOptionDto` "no correctness
  leaked"; `QuizAttemptOption` "Correctness is never exposed to the student shape — only the staff review"), **and the
  staff quiz review (`GET /api/review/quizzes/{enrollmentId}` → `QuizReviewDto`) is attempt-level scores only — it has
  no per-question answer key either.** So **`FR-STU-QZ-009`** ("review each attempt's questions, their answers, and the
  correct answers") has **no** path at all. S5 closes it with the one read in §B — the standard backend + frontend +
  wiring three-stream split (like S4's `FR-STU-ASG-007` review read). **Second correction:** the master plan §4.2 calls
  the runner timer *"server-synced via QuizHub."* **The `QuizHub` pushes nothing** — it has **no** server→client
  methods (no countdown ticks, no submit/timeout signal) and **no** client→server methods. Its **sole** job is
  **single-sitting forfeit-on-disconnect** (§A, `FR-PLAT-QZ-004`). The countdown is a **local** timer seeded from the
  start response's `deadlineUtc` + `serverNowUtc`; the **authoritative** auto-submit is a server-side **Hangfire** job
  at `deadlineUtc` (§C).
- **Authenticated student surface.** Every endpoint here uses **`RequireStudent()`** (anon → 401, staff → 403) —
  identical to `/api/me/catalogue|sessions|assignments|quizzes|videos`. **The student id + tenant come from the JWT**
  (`ICurrentUserResolver.UserId` / `.TenantId`), never a URL id. `{attemptId}` ownership is proven through the owning
  `UserQuiz.StudentId == currentUser.UserId`; a foreign / cross-tenant / unknown id resolves to **404**, never the
  other student's data (no IDOR surface, `NFR-SEC-007`).
- **Tenant isolation is automatic.** `UserQuiz` (and its owned `QuizAttempt`*) and `AssessmentEvent` are `ITenantOwned`
  → the EF global query filter scopes them to the caller's tenant and excludes soft-deleted rows. **Never** write a
  per-handler `Where(x => x.TenantId == …)`. Cross-tenant isolation is covered by an integration test on the new read
  (`NFR-SEC-010`).
- **The student/staff `isCorrect` split is preserved.** The live-attempt `QuizAttemptDto`/`QuizQuestionDto`/
  `QuizOptionDto` (§A #2) and the intro `StudentQuizDto` (§A #1) **never** carry correctness — the 5B-2 invariant and
  its guard test (raw attempt JSON never contains `isCorrect`) stand **unchanged**. The **only** student surface that
  reveals the answer key is §B, and **only** for the caller's own **terminal** attempt (an `InProgress` attempt → `403`,
  §B.2) — you can never see the key mid-sitting (there is exactly one active attempt at a time).
- **Reads not audited; engine state-changes are.** The new review (`GET .../attempts/{id}/review`, §B) and the engine
  load (`GET .../by-session`, §A #1) are **pure reads of the caller's own quiz** — **not** audited (parity with
  `/api/me/catalogue` + `/api/me/sessions` + the S4 assignment review). The engine's **state changes** are recorded by
  5B-2's design (unchanged): **start** and **submit** write an audit row (**student** actor); **forfeit** (hub
  disconnect) and **timeout** (Hangfire) write an audit row (**`System`** actor); **focus-loss** pings go to
  `assessment_events` — **high-volume telemetry, not the audit log** (`FR-PLAT-QZ-010`, §E).
- **Enums over the wire are string names** (`JsonStringEnumConverter`) — the frontend models them as string unions.
  Dates are ISO-8601 `…AtUtc`; the client corrects its local clock with `serverNowUtc` (§C). Times are integer
  **seconds** (`timeSpentSeconds`), rendered `M:SS` by the UI (the prototype's `fmt(sec)`). Question images are R2 keys
  → **signed URLs on read** (`imageUrl`). **Quiz questions carry NO hint** (`hintUrl`) — hints are an assignment-only
  aid (`FR-PLAT-QB-005`; `QuizAttemptQuestion` carries none).

## A. Quiz engine — **EXISTS** (frozen by 5B-2 §A · re-stated for the frontend · `RequireStudent`)

The intro + runner build against these five routes **unchanged** (one additive field, #1). Authority =
`docs/contracts/phase5b2-quizzes.md` §A; re-stated here so the frontend has the shapes in one place. **The frontend
must not assume any other engine route** (there is no per-attempt GET, no "list my quizzes" — the intro loads **by
session**, which it always has from the S3 session-detail context; the runner holds the started attempt's `attemptId`
from #2).

| # | Method & path | Returns | Notes |
|---|---|---|---|
| 1 | `GET /api/me/quizzes/by-session/{sessionId}` | `StudentQuizDto` | The caller's gating quiz for session **B** (summary; **no** questions/answers). **`404`** if the session has no quiz (no prerequisite / no quiz settings / no eligible questions). |
| 2 | `POST /api/me/quizzes/{quizId}/attempts` | `QuizAttemptDto` | **Start** an attempt: draws an independently **randomised** subset of `questionCount` quiz-eligible questions (one variation each, `FR-STU-QZ-007`). **`409`** if attempts are exhausted or an attempt is already active; **`404`** unknown/foreign quiz; **`403`** staff. Returns the `attemptId`, `deadlineUtc`, `serverNowUtc`, and the drawn questions — **no `isCorrect`**. |
| 3 | `PUT /api/me/quizzes/attempts/{attemptId}/questions/{aqId}/answer` | `204` | Body `{ "selectedOptionId": "guid" }`. Records the answer; **save-as-you-go** (re-answering before terminal is allowed). **`409`** if the attempt is not `InProgress` or past its `deadlineUtc`; **`404`** foreign attempt; **`403`** staff. |
| 4 | `POST /api/me/quizzes/attempts/{attemptId}/submit` | `QuizAttemptResultDto` | **Grade** + seal the attempt (`Submitted`), update **best-of** + **pass**, consume the attempt. **`409`** if the attempt is already terminal (e.g. the Hangfire timer already `TimedOut` it — §C). |
| 5 | `POST /api/me/quizzes/attempts/{attemptId}/focus` | `204` | Body `{ "type": "FocusLost"\|"FocusReturned", "occurredAtUtc": "…", "durationMs"?: int }` → `assessment_events`. **Monitoring only — never auto-forfeits** (`FR-STU-QZ-005`/`FR-PLAT-QZ-006`). **`400`** on a bad `type`. |

```jsonc
// StudentQuizDto (#1) — the INTRO shape; NO questions, NO correctness
{
  "id": "guid",                         // the userQuiz id — pass to #2 to start an attempt
  "gatedSessionId": "guid",             // session B (the one whose videos this quiz unlocks)
  "settings": { "timeLimitMinutes": 30, "questionCount": 5, "attemptCount": 3, "minPassPercent": 60 },
  "attemptsUsed": 1,
  "attemptsRemaining": 2,
  "bestPercent": 52,                    // null until the first attempt terminates
  "passed": false,                      // bestPercent >= minPassPercent (>= ; the 5B-2 fix)
  "activeAttemptId": "guid|null",       // a resumable in-progress attempt, if any
  "attempts": [
    {
      "id": "guid",                     // ← THE ONE ADDITIVE FIELD (S5): the attempt id, to deep-link §B review
      "number": 1,
      "scorePercent": 52,               // null while InProgress
      "status": "TimedOut",             // "InProgress" | "Submitted" | "Forfeited" | "TimedOut"
      "flag": "Timeout",                // UI pill: "Clean" | "Timeout" | "Forfeit" (derived from status)
      "startedAtUtc": "…",
      "submittedAtUtc": "…|null"
    }
  ]
}
// QuizAttemptDto (#2, START) — the live attempt; questions WITHOUT isCorrect, no hint
{
  "attemptId": "guid",                  // pass to #3/#4/#5 and (post-terminal) §B review
  "number": 2,
  "deadlineUtc": "…",                   // authoritative end instant (start + timeLimitMinutes)
  "serverNowUtc": "…",                  // the server's clock at start — correct the local countdown against it
  "questions": [
    { "id": "guid", "order": 1, "bodyLatex": "string|null", "imageUrl": "string|null",
      "options": [ { "id": "guid", "order": 0, "text": "string" } ] }   // NO isCorrect, NO hint
  ]
}
// QuizAttemptResultDto (#4, SUBMIT) — score-only; carries NO questions and NO attemptId (the runner holds it from #2)
{ "scorePercent": 78, "status": "Submitted", "bestPercent": 78, "passed": true, "attemptsRemaining": 1 }
```

- **The one additive field (#1).** `StudentQuizAttemptSummaryDto` gains **`id`** (the `QuizAttempt.Id`). This is
  **purely additive** (a new field on an existing record — nothing renamed or removed); it is what makes each terminal
  attempt addressable by §B. The backend updates `QuizMappings.ToStudentDto` to pass `a.Id`; the 5B-2 "no `isCorrect`"
  guards on the live shapes are untouched.
- **Best-of + pass (`FR-STU-QZ-008/010`).** `bestPercent` = **max** across attempts; `passed` =
  `bestPercent >= minPassPercent` (**`≥`**). Passing flips the enrollment's **videos-unlocked** state read by the **5C
  video gate** + the **S3** session-detail playlist (`FR-STU-QZ-010`, §F). All attempts stay visible in `attempts[]`.

### A.1 SignalR `QuizHub` — **EXISTS** (path `/hubs/quiz`, JWT-authenticated, Redis-backed) — **pushes nothing**

The hub is **not** a data channel. It has **no** server→client methods and **no** client→server methods beyond the
connection lifecycle. The frontend's **only** reason to hold an open hub connection during a sitting is to **arm the
forfeit**:

- **Auth:** the platform JWT rides the SignalR **`access_token`** query, scoped to the `/hubs/quiz` path and validated
  as a full JWT (`NFR-SEC-005`; not query-string credentials). The student app sets it via the SignalR client's
  `accessTokenFactory`. A non-student principal is aborted.
- **Connect:** on `OnConnectedAsync`, if the caller already has an active attempt (started via REST #2), the hub binds
  this connection to it (Redis map + a SignalR group). **So the frontend starts the attempt via #2 first, then opens
  the hub connection.**
- **Forfeit-on-disconnect (`FR-STU-QZ-004`/`FR-PLAT-QZ-004`):** on `OnDisconnectedAsync` (tab close, navigation away,
  network loss) the hub **forfeits** the active attempt with **score 0**, status `Forfeited`, **consuming** the
  attempt. There is **no** "forfeit" REST endpoint — **forfeit happens by disconnecting** (the connection↔attempt map
  lives in Redis so it survives across instances; a DB lookup is the fallback). Therefore **holding the hub connection
  open for the whole sitting is load-bearing** for `FR-STU-QZ-004`.

## B. Per-attempt answer-key review — `GET /api/me/quizzes/attempts/{attemptId}/review` (**NEW** · `RequireStudent`)

`RequireStudent` · `200 StudentQuizAttemptReviewDto`. The caller's **own**, **terminal**
(`Submitted`|`TimedOut`|`Forfeited`) attempt with the **answer key** — per-question and per-option `isCorrect`, the
student's `selectedOptionId`, marks, and the attempt's score (`FR-STU-QZ-009`). This is the **only** student endpoint
that exposes quiz correctness, and **only** post-termination. Backend resolves the attempt **through its owning
`UserQuiz`** (`UserQuiz.StudentId == currentUser.UserId`, the IDOR/tenant boundary) and projects the immutable
`QuizAttempt` snapshot — **no recomputation, no migration**.

### B.1 Result — `StudentQuizAttemptReviewDto`

```jsonc
// 200 · StudentQuizAttemptReviewDto
{
  "attemptId": "guid",                  // echo of the route param
  "quizId": "guid",                     // the owning userQuiz id
  "gatedSessionId": "guid",             // session B (the quiz unlocks its videos)
  "sessionTitle": "string|null",        // session B's title — for the header: "{sessionTitle} · Quiz review"
  "number": 2,                          // the sitting number
  "status": "Submitted",                // "Submitted" | "TimedOut" | "Forfeited" (terminal — the endpoint gates it, §B.2)
  // score (FR-STU-QZ-008/009)
  "scorePercent": 78,                   // this attempt's score (0 for a Forfeited attempt)
  "minPassPercent": 60,
  "startedAtUtc": "…", "submittedAtUtc": "…",
  "timeSpentSeconds": 702,              // submitted−started (the full window on timeout)
  // the answer key (the drawn snapshot for THIS attempt)
  "questions": [
    {
      "id": "guid", "order": 1,
      "bodyLatex": "string|null", "imageUrl": "string|null",
      "mark": 2,                        // the question's weight in this attempt
      "options": [
        { "id": "guid", "order": 0, "text": "string", "isCorrect": true }   // isCorrect EXPOSED (review only)
      ],
      "selectedOptionId": "guid|null",  // what the student picked this attempt (null = unanswered)
      "isCorrect": true                 // selectedOptionId is the correct option
    }
  ]
}
```

- It is a **distinct DTO** (`StudentQuizAttemptReviewDto` / `StudentQuizReviewQuestionDto` /
  `StudentQuizReviewOptionDto`) — do **not** reuse or widen the live `QuizAttemptDto`/`QuizQuestionDto`/`QuizOptionDto`
  (which forbid `isCorrect`). Questions are ordered by `Order` asc (1-based); options by `Order` asc (0-based — the
  snapshot's `DisplayOrder`). **No `hintUrl`** (quiz questions carry none, §0). The header score line shows
  `scorePercent` / `minPassPercent`; the per-attempt pass/fail is derived client-side (`scorePercent >= minPassPercent`)
  — note the **quiz-level** `passed` (best-of) lives on the intro `StudentQuizDto` (§A #1), not here.

### B.2 Error modes — ProblemDetails

| Status | Machine `reason` | Readable `detail` (render it) | When |
|---|---|---|---|
| `401` | — | (unauthorized) | No bearer (anonymous). |
| `403` | — | (forbidden) | A **staff** JWT (the `RequireStudent` filter). |
| `403` | `quiz_attempt_in_progress` | "Finish the quiz to see your answers and score." | The attempt is the caller's but **`Status == InProgress`** — the key is **never** revealed mid-sitting (`FR-STU-QZ-009` is "after submission"). |
| `404` | — | (not found) | `{attemptId}` is unknown, **another student's**, or **another tenant's** — the IDOR/tenant boundary (opaque: never reveal existence). |
| `200` | — | — | The caller's own **terminal** attempt → the answer key. |

> The intro's **"Review"** affordance on an attempt row only renders for **terminal** attempts (`status != InProgress`),
> so the `403 quiz_attempt_in_progress` path is the deep-link/edge case — surfaced as a friendly "finish first" message,
> not an error. (There is at most one `InProgress` attempt, and it is the active sitting.)

## C. Runner interaction rules (frozen semantics the frontend implements against §A)

The prototype draws the **`quiz`** screen with a question card + option buttons, question dots, prev/next, a countdown,
and a guarded exit. These rules bind the **behaviour**; the prototype binds the **layout/copy** (§ design anchor).
Where they conflict, the prototype wins on pixels/copy, this contract wins on the engine calls + state.

- **Informed intro (`FR-STU-QZ-001/002`):** the **`quizIntro`** screen renders from `StudentQuizDto` (#1) — the rules
  (time limit, `questionCount`, randomisation), **attempts remaining**, **best score**, and the **"one sitting only —
  leaving forfeits this attempt (zero) and consumes it"** warning. **Start** is enabled only when
  `attemptsRemaining > 0` and there is no active attempt (if `activeAttemptId != null`, offer **Resume** instead — it
  re-enters the runner on that attempt). **Start** calls `POST /{quizId}/attempts` (#2).
- **One sitting, save-as-you-go (`FR-STU-QZ-003/007`):** on start (#2) the runner renders the drawn `questions` (one
  card; the prototype shows **question dots** + prev/next to move between them) with LaTeX/image. Each MCQ pick →
  `PUT …/answer` (#3) for the current question's `{aqId}` with `{ selectedOptionId }`; re-picking before submit
  re-`PUT`s (allowed). **Persist each immediately — no client-only draft.**
- **Open the hub on start (`FR-STU-QZ-004`):** immediately after #2 succeeds, **open the `QuizHub` connection**
  (`/hubs/quiz`, JWT via `accessTokenFactory`) and **keep it open for the whole sitting**. This arms the
  forfeit-on-disconnect (§A.1) — it is the **only** mechanism that enforces single-sitting forfeit. Do **not** enable
  resumable auto-reconnect that implies the attempt survives a drop: by the time a reconnect fires the server has
  already forfeited the attempt (the forfeit is authoritative). Tear the connection down on a clean submit/leave.
- **Local countdown, server-authoritative timer (`FR-STU-QZ-003/006`):** the visible countdown is **local**, seeded
  from `deadlineUtc − serverNowUtc` (correct the local clock by `serverNowUtc`, then count down to `deadlineUtc`). The
  **authoritative** auto-submit is a server-side **Hangfire** job at `deadlineUtc` — the hub does **not** tick. When the
  **local** countdown hits 0, the client calls `POST …/submit` (#4); if that **`409`s** because the Hangfire job
  **already `TimedOut`** the attempt, the client **re-fetches `GET …/by-session`** (#1) and reads the just-ended
  attempt's summary (now `TimedOut`, with its `scorePercent` and `id`) → goes to results/review. **The server's clock
  wins** in every race (the client countdown is a display; the grade is the server's).
- **Manual submit (`FR-STU-QZ-006`):** the runner's **Submit** button calls `POST …/submit` (#4) → `QuizAttemptResultDto`.
  On success, go to the **results** screen (§D) for this attempt.
- **Forfeit-on-leave (`FR-STU-QZ-004`):** a `window:beforeunload` HostListener arms the browser's native "leave?" prompt
  while the runner is mounted (the prototype's `_beforeUnload`). An **in-app** navigation away (clicking another nav
  item / back) opens the **"Leave the quiz?"** confirm modal (§D copy); **"Leave & forfeit"** proceeds with the
  navigation, which tears down the hub connection → the server **forfeits** the attempt (score 0, consumed). **"Stay in
  quiz"** dismisses. There is no client "forfeit" call — leaving *is* the forfeit.
- **Focus-loss recorded, not forfeited (`FR-STU-QZ-005`):** a `document:visibilitychange` (+ window blur/focus)
  HostListener detects tab/window switches; on each, `POST …/focus` (#5) with `{ type: "FocusLost"|"FocusReturned",
  occurredAtUtc, durationMs? }`. This is **monitoring only — it does NOT forfeit** (only a *disconnect* forfeits). The
  prototype increments a counter silently; the contract permits an optional on-screen "this was logged" notice
  (`FR-STU-QZ-005` "MAY be shown") but **must not** end the attempt.
- **Immutable, randomised (`FR-STU-QZ-007`/`FR-PLAT-QZ-009`):** each attempt's question set is a fixed snapshot for that
  sitting; a terminal attempt never re-opens; a new attempt may start while `attemptsRemaining > 0`.
- **LaTeX + image (`FR-STU-QZ-007`):** render `bodyLatex` via the shared `LatexPreview` (best-effort, the admin/S3/S4
  pattern — no KaTeX/MathJax dependency) and `imageUrl` inline; option `text` likewise. **No hint** in the quiz runner.

## D. Results + review screen semantics (frozen — what each screen shows)

Two distinct post-attempt screens. **The prototype has a score-only results screen and NO answer-key review screen** —
so, exactly like S4, the **review** is a **new** student screen built to §B, while **results** matches the prototype:

- **Results (`quizResults`, score-only — `FR-STU-QZ-008`):** after submit (#4), the prototype's centred card shows a
  **pass/fail mascot** (`salah-passed.png` / `salah-failed.png`), a **score ring** (the attempt `scorePercent`), a
  **headline** + sub, and **two stat tiles — "This attempt"** and **"Best of"** — then a primary **"Back to session"**
  (`go('sessionDetail')`). It is **score-only** — **no** per-question answer key here. (Source: §B is reached from the
  **intro's attempt rows / a "Review answers" affordance**, not from this results card — keep the results screen as the
  prototype draws it.)
- **Per-attempt review (NEW screen — `FR-STU-QZ-009`):** driven by §B's `StudentQuizAttemptReviewDto`. The prototype has
  **none**, so this is a **new** student screen, mirroring the **admin/S4** review treatment for visual consistency
  (re-implemented, **never** imported):
  - **Header:** `"{sessionTitle} · Quiz review"` + **"Attempt {number}"** + the **score** (`{scorePercent}%`, a pass/fail
    chip from `scorePercent >= minPassPercent`) + **time** (`M:SS` from `timeSpentSeconds`) + the attempt **flag**
    (Clean/Timeout/Forfeit, from `status`).
  - **Per question:** the body (LaTeX/image) + each option with **three** visual states from per-option `isCorrect` +
    `selectedOptionId`: the **correct** option marked (green check); the student's **wrong** pick marked (red, when
    `selectedOptionId` is set and that option's `isCorrect == false`); a per-question right/wrong indicator from the
    question's `isCorrect`. An **unanswered** question (`selectedOptionId == null` — common on a `TimedOut`/`Forfeited`
    attempt) shows the **correct** option only.
  - **Read-only** — the review never mutates (the attempt is terminal + immutable, `FR-STU-QZ-009`/`FR-PLAT-QZ-009`).
  - **Reachable for every terminal attempt** — from the intro's `attempts[]` rows (each now carries its `id`, §A #1) and
    from the results screen's "Review answers" affordance for the just-finished attempt (the runner holds its
    `attemptId` from #2). A `403 quiz_attempt_in_progress` (deep-link edge) shows a friendly "finish first" panel; a
    `404` routes back to the session detail.

## E. Audit (`FR-PLAT-QZ-010`, `FR-PLAT-AUD-002`)

- `GET /api/me/quizzes/by-session/{sessionId}` (§A #1) and `GET /api/me/quizzes/attempts/{attemptId}/review` (§B) —
  **pure reads of the caller's own quiz, not audited** (parity with `/api/me/catalogue` + `/api/me/sessions` + the S4
  assignment review).
- The engine's **state changes** are recorded by 5B-2's design (unchanged): **start** (#2) and **submit** (#4) write one
  audit row each (**student** actor); **forfeit** (hub disconnect) and **timeout** (Hangfire) write one audit row each
  (**`System`** actor); the answer `PUT` (#3) records the snapshot answer (no field-diff row — `IAuditViaEventOnly`);
  **focus** (#5) appends to `assessment_events` — **telemetry, not the audit log**.

## F. Video-unlock boundary (with S3 / 5C)

Passing the quiz (`FR-STU-QZ-010`, `FR-PLAT-QZ-008`) flips the enrollment's **videos-unlocked** state on **submit/timeout**
(best `≥` minPass). **S5 only triggers it via the engine** (#4 / the Hangfire timeout) — it does **not** re-implement the
gate. The S3 session-detail playlist + the **5C** video gate **read** that state: a `QuizLocked` video becomes
`Playable` once `passed == true`. The wiring stream proves the pass→unlock transition by re-reading
`GET /api/me/sessions/{B}` (S3). The actual playback gate (active enrollment + remaining count + quiz-passed, the
per-video decrement, the deep-link handoff) is **5C/S3**, untouched.

## G. Deferred / **NOT built** (master plan §3.3 / §7)

- **No per-question answer key on the results screen** — the prototype's `quizResults` is score-only (§D); the answer
  key is the **§B review** screen, reached from the intro's attempt rows / a "Review answers" affordance.
- **No engine change** beyond the one additive `id` field (§A #1) and the new §B read — the five engine routes, the
  randomisation/grading math, the snapshot, the best-of/pass rule, the Hangfire timer, the `QuizHub` forfeit, and the
  attendance write are reused exactly as 5B-2 shipped them. Any drift in load/start/answer/submit/focus/forfeit/timeout
  is a **5B-2** finding, not S5's.
- **No real-time data channel** — the `QuizHub` stays forfeit-on-disconnect-only (§A.1). S5 does **not** add hub
  server→client ticks (the timer is local + Hangfire-authoritative); the notifications hub seam stays deferred.
- **No file-upload / free-text answers** — quizzes are MCQ-only; ignore the shared `FileUpload`.
- **Staff per-question quiz review** — out of scope (the admin `QuizReviewDto` stays attempt-level; the new key is the
  **student's own** surface only). If staff per-question review is wanted later it is a separate admin slice.

## H. Frozen vs. stream-owned

- **Frozen (this file):** the **reuse** of the five §A engine routes (no signature change) + the `QuizHub`
  forfeit-on-disconnect contract (§A.1); the **one additive** `id` field on `StudentQuizAttemptSummaryDto` (§A #1); the
  **new** `GET /api/me/quizzes/attempts/{attemptId}/review` path + `RequireStudent` + the
  `StudentQuizAttemptReviewDto` / `StudentQuizReviewQuestionDto` / `StudentQuizReviewOptionDto` field names + types +
  ordering (§B.1); the **terminal-attempt** gate + the `403 quiz_attempt_in_progress` / `404` IDOR boundary (§B.2); the
  **student-vs-staff `isCorrect` split** (correctness exposed **only** in §B, **only** for the caller's own terminal
  attempt; the live shapes stay correctness-free, §0/§A); the runner semantics — informed intro, hub-open-on-start,
  local-countdown-vs-server-authoritative-Hangfire, save-as-you-go, manual/auto submit, forfeit-by-disconnect/leave,
  focus-recorded-not-forfeited (§C); the results-vs-review screen split (§D); "reads not audited" + the engine audit
  actor split (§E); the pass→videos-unlock boundary (§F).
- **Backend owns:** the query folder/name (`Features/Quizzes/Queries/GetMyQuizAttemptReview/` — implementer's call, keep
  the route + DTO frozen), the DTO + `.ToReviewDto()` mapping location (`Features/Quizzes/DTOs/QuizDtos.cs`), the resolve
  path through the owning `UserQuiz` (the IDOR scope), the terminal gate + the reason'd-`403`/`404`, the `sessionTitle`
  resolution (`IgnoreQueryFilters`), the additive `id` on `StudentQuizAttemptSummaryDto` + its `QuizMappings.ToStudentDto`
  wire-up, the route on the **existing** `QuizEndpoints` group, and the integration tests.
- **Frontend owns:** the **new** quiz pieces in `libs/student-portal/feature-assessment` (intro + runner + results +
  review), the `QuizService` in `data-access` (`quiz(sessionId)` / `start(quizId)` / `answer(attemptId, aqId, optionId)`
  / `submit(attemptId)` / `focus(attemptId, …)` / `review(attemptId)`), the **`QuizHub` SignalR client** (install
  `@microsoft/signalr`; connect-on-start, forfeit-on-disconnect), the **local countdown** (no shared `Timer` exists —
  build one; `NgZone.runOutsideAngular` for the interval, the S4 gotcha), the leave-confirm modal + `beforeunload` +
  focus listeners, the `/sessions/:id/quiz(/...)` routes, the replacement of S3's `SessionDetailComponent.openQuiz()`
  placeholder with real navigation, and the Jest specs (`whenStable()`, not `fakeAsync`).
- **Wiring owns:** proving the slice live on the Aspire stack — intro → start (hub open) → answer-through → submit (or
  Hangfire timeout) → best-of/`≥`-pass → the **pass→videos-unlock** S3 re-read → the **§B review** returns the answer
  key for the caller's own terminal attempt (per-option + per-question `isCorrect`, `selectedOptionId` echoed, score),
  with the **`403 quiz_attempt_in_progress`** on the active attempt, the **404** IDOR/tenant/foreign boundary, **401**
  anon / **403** staff, tenant isolation, the **forfeit-on-disconnect** (drop the hub → attempt `Forfeited`/0), the
  **Hangfire timeout** auto-submit, the audit actor split (start/submit=student, forfeit/timeout=System; focus→
  `assessment_events`), and the `isCorrect` split (live vs review) — all **zero drift**. The **engine** itself is
  5B-2's — any drift there is a 5B-2 finding, not S5's.
