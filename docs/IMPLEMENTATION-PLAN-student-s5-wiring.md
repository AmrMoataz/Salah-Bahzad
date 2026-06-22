# Student Portal · S5 — WIRING stream (prove the proctored quiz runner + per-attempt answer-key review live)

> Status: **Planned — not yet built** · Created 2026-06-22 · Proves slice **S5**
> (`docs/IMPLEMENTATION-PLAN-student-portal.md` §S5) end-to-end on the **running Aspire stack** (Postgres + **Redis** +
> MinIO + API + both Angular apps), exactly like the prior wiring streams. Goal: **zero drift** vs
> `docs/contracts/student-s5-quizzes.md` — the **reused** Phase-5B-2 quiz **engine** (§A: the five `/api/me/quizzes`
> routes) + the **`QuizHub`** (§A.1, forfeit-on-disconnect) proven through the runner's calls; the **one new read**
> (`GET /api/me/quizzes/attempts/{attemptId}/review`, §B) proven the **only** student surface that exposes
> `isCorrect`, gated to the caller's **own terminal** attempt; **forfeit-on-disconnect** (drop the hub → `Forfeited`/0),
> the **Hangfire** timeout auto-submit (`TimedOut`), **best-of + `≥`-pass**, and the **pass→videos-unlock** S3 re-read
> all proven live.
>
> Runs **after** the backend + frontend streams merge. Reuses the prior wiring techniques: read the Aspire-assigned
> ports from the dashboard (reassigned every run; discover the PG/MinIO/**Redis** containers **by image**, renamed on
> every restart); verify DB state with `docker exec -i <pg> psql -d DefaultConnection` (`PGPASSWORD=postgres`;
> snake_case tables, **PascalCase quoted columns** — real names `user_quizzes` / `quiz_attempts` /
> `quiz_attempt_questions` / `quiz_attempt_question_options` (owned) / `assessment_events` / `attendance` (**singular**)
> / `audit_entries`; the owned `Order` columns are stored as **`"DisplayOrder"`**; pipe SQL via **stdin** — PS 5.1
> mangles inline `-c "…\"col\"…"`); drive the student endpoints with a **Student-role JWT** (the reusable direct-JWT
> mint from S0/S2/S3/S4/phase5b — short claims `nameid`/`role` + `tenant_id`/`token_type`/`device_id`, HS256,
> `iss=salah-bahazad-api`, `aud=salah-bahazad-admin` — or a real S0 sign-in via the `:4300` proxy). **The `/api/me/*`
> routes do NOT check the device**; the **`QuizHub` does** require `role=Student` (it aborts any other principal) and
> reads `tenant_id` from the same JWT.

---

## Design reference

This stream verifies behaviour, not pixels, but the **acceptance copy** is the **Student Portal** prototype
(`.claude/Salah Bahzad Student Portal/Student Portal.html`): the **`quizIntro`** screen (rules, time limit, attempts
remaining, best score, the "one sitting / leaving forfeits" warning), the **`quiz`** runner (`fmt(sec)` countdown,
question dots, prev/next, the `beforeunload` guard, `visibilitychange → quizTabSwitches`), the **`quizResults`**
**score-only** screen (pass/fail mascot `salah-passed.png`/`salah-failed.png`, score ring, **"This attempt"/"Best of"**
tiles, **"Back to session"**), and the **"Leave the quiz?"** modal (copy verbatim: *"Leaving now forfeits this attempt
and records a zero. It also counts as one of your limited attempts. There's no way to resume."* — buttons **"Stay in
quiz"** / **"Leave & forfeit"**). Confirm the running screens at **:4300** match the prototype responsively while
driving the browser check. The **answer-key review** screen (§B/§D) is a **NEW** student screen — the prototype has
**none** — mirroring the **admin/S4** review question/option treatment (green-check correct option / red wrong pick /
per-question right-wrong indicator) for visual consistency.

## Pre-flight
- Backend + frontend streams merged; `dotnet test -c Release` green (minus the known baseline image test);
  `npx nx build student-portal` green (`@microsoft/signalr` is a **new** frontend dep the frontend stream adds —
  confirm it installed and the hub-client chunk built). **No migration** for S5 — confirm the Aspire Postgres already
  has `user_quizzes`, `quiz_attempts`, `quiz_attempt_questions`, `quiz_attempt_question_options` (owned),
  `assessment_events`, `attendance`, `enrollments`, `sessions`, `audit_entries` (all from Phase 5B-2 / earlier phases;
  **S5 adds no table, no column** — the additive `id` field on `StudentQuizAttemptSummaryDto` (§A #1) is a projection
  of the existing `QuizAttempt.Id`, not a schema change).
- Start via **AppHost (F5)**. Read the API port + both web ports from the dashboard. **If
  `GET /api/me/quizzes/attempts/{id}/review` 404s as a ROUTE (not a 401/403/200 auth result), the running API is
  stale** — restart the AppHost (the recurring 5B-2/5C/S0..S4 gotcha: Aspire won't hot-add new routes). The **five
  engine routes** (`by-session` / `attempts` start / `answer` / `submit` / `focus`) already shipped in 5B-2, so a stale
  API still serves *them* — **only** the **new `…/attempts/{id}/review`** route flips 404→401 after the restart. Probe
  it with **no bearer**: a **401** (not a route 404) means it's live.
- **Redis MUST be up.** It is load-bearing twice over: the **`QuizHub`** forfeit map (`quizhub:conn:{connectionId}` →
  quizId, the connection↔attempt binding `QuizHub.OnConnectedAsync` writes and `OnDisconnectedAsync` reads) **and** the
  SignalR **backplane**. Confirm the **redis** container is running (discover by image `redis:*` / Aspire dashboard) and
  the API connected. The hub has a DB fallback (`FindActiveAttemptQuizIdAsync`) when the Redis map is missing, so
  forfeit is correct even without it — but verify Redis is up so the live forfeit check exercises the real path.
- **Hangfire MUST be running** — it is the **authoritative** timer (`QuizAutoSubmitJob`, scheduled at the attempt's
  `DeadlineUtc` on start). Confirm the Hangfire server is alive (dashboard / `hangfire.*` tables in Postgres). The local
  countdown is a display only; the grade on timeout is the server's.
- **Precondition: an `Active`, device-bound student enrolled on a QUIZ-GATED session** — a session **B** whose
  **prerequisite A** has a `QuizSetting` **and** quiz-eligible questions, so the S2 redeem side-effect generated a
  **`UserQuiz`** (`GET /api/me/quizzes/by-session/{B}` → **200**, not 404). **S4's wiring left exactly this:** the
  quiz-gated session **"S4 LaTeX Demo - Algebra and Calculus"** (`019eec27`, Math·Algebra, **quiz 5/6 pass 60**, 6
  rich-LaTeX MCQs). **Either** enroll a student on a session that has *that* session as its **prerequisite** (so a
  `UserQuiz` gating *its* videos is generated), **or** mint a fresh quiz-gated chain as staff (set `QuizSetting` +
  quiz-eligible questions on the prereq, publish B, mint a code, redeem as the student — the 5B-2 setup). Confirm
  `students."Status"=1` (Active) and a `user_quizzes` row exists for the enrollment.
- **A staff JWT** (Teacher) for fixtures — provisioning a fresh quiz-gated chain if needed, and for the **staff-403**
  auth check on the review read. Reuse the admin wiring's staff principal.
- The auth **rate-limit is one global ~10/min bucket** shared by `/auth/*` + `/register` — it does **not** gate
  `/api/me/*` or `/hubs/*`, but if you mix in sign-ins, space them.
- **LaTeX bodies** (the quiz questions are deliberately rich-LaTeX) — the shell/tool layer **collapses `\\`→`\`**, so
  any JSON body carrying LaTeX 400s on bind unless POSTed **from a file** (`curl --data-binary @body.json`); an
  **em-dash** in a title likewise breaks JSON binding (use ASCII). The runner reads the already-seeded snapshot, so this
  only bites if you re-provision questions in this run.

## Fixtures (reuse seeded data where possible)
- **A quiz-gated enrollment with a `UserQuiz`** — the happy-path quiz. The S4-left session
  **"S4 LaTeX Demo - Algebra and Calculus"** is quiz-gated; enroll a student on a session whose **prerequisite** is that
  one (or mint+redeem a fresh chain). Confirm `GET /api/me/quizzes/by-session/{B}` → **200 `StudentQuizDto`** with
  `settings` (5/6 pass 60), `attemptsRemaining > 0`, `passed=false`, `activeAttemptId=null`, and `attempts[]` (empty
  until the first attempt). This is the quiz the runner **answers through** (checks #1–#4) and **reviews** (check #7).
- **A started attempt to answer through** — `POST …/{quizId}/attempts` once → the live `QuizAttemptDto` (randomised
  `questionCount` draw, `deadlineUtc`, `serverNowUtc`, **no `isCorrect`**). Answer each `aqId` via `PUT …/answer`, then
  **`POST …/submit`** → terminal `Submitted`. (Answer the **correct** options **by text** when you want a known score —
  the 5B-2 gotcha: a question variation can flip the correct option's `DisplayOrder`, so "pick order 0" is unreliable;
  pick by `text`.)
- **A terminal attempt for the review** — the just-submitted attempt above (or a `TimedOut`/`Forfeited` one) is the §B
  fixture: `GET …/attempts/{attemptId}/review` → the answer key.
- **A still-active (`InProgress`) attempt** — for the **`403 quiz_attempt_in_progress`** review gate (#7b). Start an
  attempt and **do not** submit it; `GET …/attempts/{thatAttemptId}/review` → `403`. (There is at most one `InProgress`
  attempt per quiz at a time; `activeAttemptId` on the intro points at it.) Then submit or let it time out to free the
  quiz for later checks.
- **A second student + a second tenant** — for IDOR/isolation (#7c): student A's review of **A's own** terminal attempt
  never leaks B's; **A cannot review B's** `{attemptId}` (→ 404), and a **cross-tenant** `{attemptId}` → 404. Confirm via
  psql that the foreign attempt **does** exist (so 404 is the ownership/tenant boundary, not a missing row).
- **A SignalR test client for the forfeit drop** — a small **`@microsoft/signalr`** (Node) or `.NET HubConnection`
  client against `/hubs/quiz` with the **Student JWT** (`accessTokenFactory` → the token rides the `access_token` query,
  validated as a full JWT scoped to `/hubs/quiz`). Start an attempt via REST **first** (#2 binds it on
  `OnConnectedAsync`), connect, then **dispose** → `OnDisconnectedAsync` forfeits. If a live SignalR client isn't
  feasible in-session, record that forfeit-on-disconnect is covered by the **green 5B-2 backend integration suite**
  (SignalR `HubConnection` test client) + the hub-auth check.
- **An attempt whose deadline is back-dated, OR invoke `QuizAutoSubmitJob` directly** — for the **Hangfire timeout**
  (#6). Either back-date `quiz_attempts."DeadlineUtc"` to the past via psql and let the scheduled job fire, or invoke
  `QuizAutoSubmitJob.RunAsync(quizId, attemptId, tenantId)` directly (the 5B-2 technique — the job is idempotent and a
  no-op once terminal). Either path → `Status=TimedOut`, what was answered is graded, score is the partial.

## Live checks (target: all green, zero drift)

**Engine reuse — Phase 5B-2, proven through the runner's calls (§A):**
1. **Intro load (§A #1), NOW carrying the additive `id`:** `GET /api/me/quizzes/by-session/{B}` (Student JWT) →
   **`200 StudentQuizDto`**: `id`/`gatedSessionId`/`settings`(`timeLimitMinutes`/`questionCount`/`attemptCount`/
   `minPassPercent`)/`attemptsUsed`/`attemptsRemaining`/`bestPercent`/`passed`/`activeAttemptId`/`attempts[]`. **Assert
   the raw JSON contains NO `"isCorrect"`** (the 5B-2 invariant holds — §0/§A). After ≥1 terminal attempt, **assert each
   `attempts[]` row carries the new `"id"`** (the `QuizAttempt.Id`, §A #1) alongside `number`/`scorePercent`/`status`/
   `flag`/`startedAtUtc`/`submittedAtUtc` — this is the deep-link key for the §B review. `404` if the session has no quiz
   (no prerequisite / no quiz settings / no eligible questions).
2. **Start an attempt (§A #2):** `POST /api/me/quizzes/{quizId}/attempts` → **`200 QuizAttemptDto`**: `attemptId`,
   `number`, `deadlineUtc`, `serverNowUtc`, and `questions[]` (each `id`/`order`(1-based)/`bodyLatex`/`imageUrl`(signed)/
   `options[]`(`id`/`order`(0-based)/`text`)). **Assert NO `isCorrect`** and **NO `hintUrl`** in the raw JSON (the live
   shape is correctness-free + hint-free — §0/§A). `409` if attempts are exhausted or an attempt is already active; `404`
   unknown/foreign quiz. **DB:** a `quiz_attempts` row, `Status=InProgress(0)`, `DeadlineUtc = StartedAtUtc + timeLimit`,
   `quiz_attempt_questions` = `questionCount` rows.
3. **Answer save-as-you-go (§A #3):** `PUT /api/me/quizzes/attempts/{attemptId}/questions/{aqId}/answer`
   `{ "selectedOptionId": "guid" }` → **`204`**; re-`PUT`ing a different option before terminal → **`204`** (allowed).
   **DB:** `quiz_attempt_questions."SelectedOptionId"`/`"AnsweredAtUtc"` set. **`409`** when the attempt is **not
   `InProgress`** (after submit/timeout/forfeit) **or past its `DeadlineUtc`**; **`404`** a foreign attempt; **`403`**
   staff.
4. **Submit + best-of + `≥`-pass (§A #4, §F):** `POST /api/me/quizzes/attempts/{attemptId}/submit` →
   **`200 QuizAttemptResultDto`** (`scorePercent`/`status: "Submitted"`/`bestPercent`/`passed`/`attemptsRemaining`).
   Run a **second** attempt → `bestPercent` = **max** across attempts. The **`≥`-pass boundary**: an attempt scoring
   **exactly `minPassPercent`** sets `passed=true` (the 5B-2 `>`→`≥` fix). **DB:** `quiz_attempts."Status"=Submitted(1)`,
   `"ScorePercent"`/`"SubmittedAtUtc"` set; `user_quizzes."BestPercent"`/`"Passed"` updated; `attendance."BestQuizPercent"`
   written (the **only** attendance quiz column — the attempt count lives on `user_quizzes."AttemptsUsed"`, asserted in
   #6a, **not** on `attendance`). Re-submitting a terminal attempt → **`409`**.
5. **Focus telemetry, NEVER forfeits (§A #5, §E):** `POST /api/me/quizzes/attempts/{attemptId}/focus`
   `{ "type": "FocusLost"|"FocusReturned", "occurredAtUtc": "…", "durationMs"?: int }` → **`204`**; the attempt stays
   **`InProgress`** (focus is monitoring-only — only a *disconnect* forfeits). **DB:** an `assessment_events` row tied to
   the `QuizAttemptId`, **and NO `audit_entries` row** for the focus ping. A bad `type` → **`400`**.

**Forfeit-on-disconnect (§A.1, `FR-STU-QZ-004`/`FR-PLAT-QZ-004`):**
6a. Start an attempt via REST (#2), open a **SignalR client** to `/hubs/quiz` with the Student JWT, then **drop it**
   (dispose/close). **DB:** `quiz_attempts."Status"=Forfeited(2)`, `"ScorePercent"=0`, `"SubmittedAtUtc"` set, the
   attempt **consumed** (`user_quizzes."AttemptsUsed"` incremented, `attemptsRemaining` down). **Audit:** one
   `audit_entries` row, **`System`** actor, `QuizAttemptForfeited`. Re-`GET …/by-session/{B}` (#1) → the attempt now
   reads `status: "Forfeited"`, `flag: "Forfeit"`. *(If a live SignalR client isn't feasible in-session, record the
   5B-2 integration-suite coverage + the hub-auth check below and proceed.)*
6b. **Hub auth (§A.1):** a `/hubs/quiz` negotiate/upgrade **without** a token → **401**; **with** the Student token →
   negotiation **succeeds**; a **staff** token → the hub **aborts** the connection (`role != Student`). (Through the
   **`:4300`** proxy, which forwards `/hubs` with `ws:true` — the **admin :4200** proxy forwards only `/api`, so the hub
   is **not** reachable there by design.)

**Hangfire timeout (§C, `FR-STU-QZ-006`):**
7. An attempt **past its deadline** (back-date `quiz_attempts."DeadlineUtc"` OR invoke
   `QuizAutoSubmitJob.RunAsync(quizId, attemptId, tenantId)`) → **DB:** `Status=TimedOut(3)`,
   `"SubmittedAtUtc" = DeadlineUtc`, what was answered is graded (`ScorePercent` = the partial), the attempt consumed.
   **Audit:** one `audit_entries` row, **`System`** actor, `QuizAttemptTimedOut`. The client-side race (local countdown
   hits 0 → `POST …/submit` **409s** because Hangfire already `TimedOut` it → the runner re-fetches `…/by-session` and
   reads the `TimedOut` summary) is the §C semantic — assert the **`409`-then-`TimedOut`-summary** path if you can stage
   it, else note the server's clock wins.

**The new review read (§B):**
8. `GET /api/me/quizzes/attempts/{attemptId}/review` (Student JWT) on the **terminal** attempt (#4) →
   **`200 StudentQuizAttemptReviewDto`**: `attemptId`(echo of the route param)/`quizId`/`gatedSessionId`/
   **`sessionTitle`** (set, resolved via `IgnoreQueryFilters`)/`number`/`status`/`scorePercent`/`minPassPercent`/
   `startedAtUtc`/`submittedAtUtc`/`timeSpentSeconds`, and `questions[]` each with `id`/`order`(1-based)/`bodyLatex`/
   `imageUrl`(signed)/`mark`/`options[]`(each `id`/`order`(0-based)/`text`/**`isCorrect`**)/`selectedOptionId`/
   **per-question `isCorrect`**. **Assert:** `selectedOptionId` echoes what #3 answered; the per-option/per-question
   `isCorrect` match the snapshot's correct options (Q1–5 correct → `true`, Q6 wrong → `false`, mirroring the S4 LaTeX
   bank); questions ordered by `order` asc, options by `order` asc; `scorePercent`/`minPassPercent` match the intro. This
   is a **distinct DTO** (`StudentQuizAttemptReviewDto`/`StudentQuizReviewQuestionDto`/`StudentQuizReviewOptionDto`) —
   **not** a widened `QuizAttemptDto`. On a **`TimedOut`/`Forfeited`** attempt, unanswered questions show
   `selectedOptionId: null` and per-question `isCorrect: false`, with the correct option still flagged.
9. **`403 quiz_attempt_in_progress` gate (§B.2):** `GET …/review` on the **still-`InProgress`** fixture → **`403`
   ProblemDetails** with machine `reason == "quiz_attempt_in_progress"` + readable `detail` *"Finish the quiz to see your
   answers and score."* — the key is **never** revealed mid-sitting.
10. **`404` IDOR / tenant / unknown (§B.2):** `GET …/review` for **another student's** `{attemptId}`, a **cross-tenant**
   `{attemptId}`, and an **unknown** id → **`404`** each (opaque — never the other student's data, never reveal
   existence). Confirm via psql the foreign attempt **does** exist (so 404 is the ownership/tenant boundary).

**Auth + the isCorrect split + not-audited:**
11. **Auth (§B.2):** anonymous (no bearer) → **`401`**; a **staff** (Teacher) JWT → **`403`** (the `RequireStudent`
   filter); the Student JWT → **`200`** — on the review read.
12. **The `isCorrect` SPLIT, side by side (§0/§A/§B):** for the **same** student + **same** terminal attempt, the
   **start/`by-session` raw JSON has NO `isCorrect`** (#1/#2) while the **review raw JSON DOES** (#8) — the live shapes
   stay correctness-free; the review is the deliberate, gated exception (the 5B-2 "raw attempt JSON never contains
   isCorrect" guard is **unchanged**).
13. **Not audited (§E):** snapshot `audit_entries` count **before/after** the review `GET` → **NO new row** (and the
   `by-session` `GET` likewise — pure reads of the caller's own quiz, parity with `/api/me/catalogue` +
   `/api/me/sessions` + the S4 assignment review). Cross-tabulate against the **engine** audit split confirmed in #4/#6a/
   #7: **start/submit = student** actor; **forfeit/timeout = `System`** actor; **focus → `assessment_events`, not the
   audit log**.

**Pass → videos-unlock boundary (§F, with S3 / 5C):**
14. After a passing best-of (#4, best `≥` minPass), re-read **S3** `GET /api/me/sessions/{B}` → the session-detail
   playlist's **`QuizLocked`** video flips to **`Playable`** (the `gateState` clears `QuizRequired`; the 5C video gate +
   S3 playlist **read** `UserQuiz.Passed`). S5 only **triggers** the unlock via the engine (submit/timeout) — it does
   **not** re-implement the gate (the per-video decrement + deep-link handoff stay **5C/S3**).

**The screens, live in the browser (`FR-STU-RWD-001/002`, `FR-STU-A11Y-001`):**
15. Open the student app at **:4300**, sign in, open a quiz-gated session → **quiz intro** (time/attempts/best, the "one
   sitting only — leaving forfeits" alert) → **Start** → the **runner**: the **local countdown** ticking down (seeded
   from `deadlineUtc − serverNowUtc`), **question dots** + prev/next, a **focus-loss** notice when you switch tabs (logged,
   **not** forfeited), and the **"Leave the quiz?"** confirm modal on an in-app nav away (verbatim copy; **"Leave &
   forfeit"** tears down the hub → forfeit, **"Stay in quiz"** dismisses) → **Submit** → the **score-only** results
   (mascot + score ring + "This attempt"/"Best of" + "Back to session") → **"Review answers"** → the **answer key**
   renders (correct option green-checked / your wrong pick red / per-question indicator, marks + score + time). Resize:
   intro + runner + results + review reflow to phone, comfortable targets, matches the prototype across
   phone/tablet/desktop. *(The visual walkthrough is the user's step, as with S0 #9 / S1 #7 / S2 #9 / S3 #10 / S4 #10.)*

## Sign-off
- Log the run (counts + the `quiz_attempts` `Status`/`ScorePercent`/`SubmittedAtUtc` before/after for the submit,
  forfeit, and timeout paths + the `user_quizzes` `BestPercent`/`Passed`/`AttemptsUsed` transitions + the
  `attendance."BestQuizPercent"` write (the only attendance quiz column) + the `assessment_events` focus rows + the
  review `isCorrect`/`selectedOptionId`/`scorePercent`
  assertion + the **audit no-op** before/after the review GET + the **audit actor split** start/submit=student,
  forfeit/timeout=`System` + the **pass→S3 `QuizLocked`→`Playable`** flip) into this file like the prior wiring logs.
  Update the master plan's **S5** line from *Planned* → **Met** with the date + headline result. Record a memory entry
  (`student-s5-wiring`). Note any gotchas (expect: **stale-API-needs-restart** for the **new `…/review`** route — the
  **five engine routes pre-exist** from 5B-2 so they won't 404; Aspire **renames containers + reassigns ports each run**
  — discover Postgres/MinIO/Redis by image, drive via the `:4300` proxy not the dynamic API port; the **hub is NOT
  proxied through the admin :4200 app but IS through :4300** (`ws:true`); **forfeit needs Redis up** (DB fallback covers
  correctness, but verify Redis); the **timer is Hangfire** — back-date the deadline or invoke `QuizAutoSubmitJob`
  directly; the **`≥`-pass boundary** (best == minPass **passes**); **un-redoing a consumed attempt needs a fresh
  enrollment** — a terminal attempt never reopens and attempts are consumed, so to re-run the answer-through you
  cascade-delete + refund→unlock to regenerate the `UserQuiz`, or enroll a fresh student; the **runner/hub engine is
  5B-2** — any drift in load/start/answer/submit/focus/forfeit/timeout is a **5B-2** finding, not S5's; pick correct
  options **by text** — a variation can flip the correct option's `DisplayOrder`).
- **S5 unblocks S6** (profile) — the final student slice. The quiz-gated session left passed (videos unlocked) + the
  terminal attempts left reviewable are the engagement the S6 profile build sits beside.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are running the WIRING stream of Student-Portal phase S5 (proctored quizzes) for Salah Bahzad. Prove the quiz slice
live on the running Aspire stack (Postgres + Redis + MinIO + API + both Angular apps): the REUSED Phase-5B-2 engine
(GET /api/me/quizzes/by-session/{sessionId} -> POST /{quizId}/attempts start -> PUT .../questions/{aqId}/answer ->
POST .../submit -> POST .../focus) driven through the runner + the QuizHub (/hubs/quiz, forfeit-on-disconnect), and the
ONE new read GET /api/me/quizzes/attempts/{attemptId}/review — the only student surface that exposes isCorrect, gated to
the caller's own TERMINAL attempt. Zero contract drift.

Read first, in order:
1. docs/IMPLEMENTATION-PLAN-student-s5-wiring.md (this doc — the 15 live checks + the Student-JWT + docker-exec-psql +
   discover-Aspire-containers-by-image + SignalR-test-client + invoke-QuizAutoSubmitJob techniques).
2. docs/contracts/student-s5-quizzes.md (the FROZEN contract you're proving — §A the reused engine + the one additive
   `id` field on StudentQuizAttemptSummaryDto + §A.1 the QuizHub forfeit-on-disconnect, §B the new GET .../review +
   StudentQuizAttemptReviewDto + the 403 quiz_attempt_in_progress / 404 IDOR boundary, §C the runner rules
   (local-countdown vs server-authoritative-Hangfire), §D results-vs-review, §E reads-not-audited + the engine audit
   actor split, §F pass->videos-unlock).
3. docs/IMPLEMENTATION-PLAN-phase5b2-wiring.md + the prior student wiring logs (student-s4-wiring, student-s3-wiring,
   student-s2-wiring) for the Student-role JWT mint (HS256, iss=salah-bahazad-api, aud=salah-bahazad-admin, claims
   nameid/role/tenant_id/token_type/device_id), docker-exec-psql (PascalCase quoted columns; quiz Order cols stored as
   "DisplayOrder"; singular attendance; pipe SQL via stdin), "Aspire reassigns ports & renames containers (resolve by
   image)", "stale AppHost 404 -> restart for the NEW route only", the SignalR HubConnection test-client + invoke-the-
   Hangfire-job-directly techniques, best-of=max + the >=-pass boundary (best==minPass passes), and "answer correct
   options by TEXT (a variation flips the correct option's order)".

Do: F5; confirm GET /api/me/quizzes/attempts/{id}/review is reachable (no-bearer probe -> 401, not a route 404; else
restart for the new route — the five engine routes pre-exist from 5B-2); confirm Redis (hub forfeit map + backplane) and
Hangfire (authoritative timer) are up; get an Active, device-bound student enrolled on a QUIZ-GATED session so a UserQuiz
exists (S4 left "S4 LaTeX Demo - Algebra and Calculus" quiz 5/6 pass 60 — enroll a student on a session whose prereq is
it, or mint+redeem a fresh quiz-gated chain as staff); get a staff JWT for fixtures. Run all checks — intro load (200
StudentQuizDto NOW carrying the additive attempt `id`, NO isCorrect); start (200 QuizAttemptDto, attemptId/deadlineUtc/
serverNowUtc, NO isCorrect/NO hintUrl); answer (204 save-as-you-go, 409 when terminal/past deadline); submit
(QuizAttemptResultDto, best-of=max, >=-pass boundary best==minPass passes, attendance quiz cols, 409 re-submit); focus
(204 -> assessment_events, NEVER forfeits, NO audit row, 400 bad type); forfeit-on-disconnect (open a SignalR client on
an active attempt, DROP it -> DB Forfeited/ScorePercent 0/consumed, audit System|QuizAttemptForfeited — or record the
5B-2 integration-suite coverage + hub-auth: negotiate 401 no-token / ok student / aborted staff, via :4300 not :4200);
Hangfire timeout (back-date DeadlineUtc OR invoke QuizAutoSubmitJob.RunAsync(quizId, attemptId, tenantId) -> TimedOut,
partial graded, audit System|QuizAttemptTimedOut); the new review GET on a TERMINAL attempt -> 200
StudentQuizAttemptReviewDto (per-option AND per-question isCorrect, selectedOptionId echoed, sessionTitle set, ordered);
403 quiz_attempt_in_progress on the active attempt; 404 IDOR/cross-tenant/unknown; 401 anon / 403 staff / 200 student;
the isCorrect SPLIT side-by-side (start/by-session has none, review does, same student+attempt); not-audited
(audit_entries before==after for the review GET + the by-session GET); the audit actor split (start/submit=student,
forfeit/timeout=System, focus->assessment_events); pass->videos-unlock (two attempts -> best>=minPass -> re-read S3 GET
/api/me/sessions/{B}: QuizLocked video flips Playable); and the browser screens at :4300 (intro -> start -> runner with
local countdown + question dots + focus warning + Leave-quiz modal -> submit -> score-only results -> Review answers ->
answer key renders, responsive). The engine is 5B-2's — any drift in load/start/answer/submit/focus/forfeit/timeout is a
5B-2 finding, not S5's. Log the run, flip the master plan S5 bullet to Met, write the student-s5-wiring memory.
```

---

## Run log — 2026-06-22 (✅ Met · S5's own surface proven with ZERO drift · 1 pre-existing 5B-2 finding · browser #15 = user's step)

**Environment.** Drove the API **directly** at its Aspire-assigned port (the `:4300` student proxy was **stale** — the
AppHost recycled repeatedly mid-run, so the proxy's injected `services__api__http__0` pointed at a dead port → 500; REST +
the hub were driven straight at the live API instead). The stack **churned hard**: the AppHost itself restarted several
times (PID `55748→89028→…`), reassigning the **API port** every time (`59473→60394→57346→61267→…`) and **renaming every
container**. Made the run resilient by **re-resolving the API port from the live `SalahBahazad.Api.exe` PID** and
**discovering Postgres/Redis by image** (`postgres:17.4` / `redis:7.4`) on every call. DB asserted via
`docker exec -i <pg> psql -d DefaultConnection` (`PGPASSWORD=postgres`; snake_case tables, **PascalCase quoted cols** —
note **both** `QuizAttemptQuestion.Order` **and** the option order are stored as **`"DisplayOrder"`**; `attendance` is
**singular**). Student endpoints driven with a **direct-minted Student JWT** (HS256, `iss=salah-bahzad-api`,
`aud=salah-bahzad-admin`, claims `nameid`/`role`/`tenant_id`/`token_type`/`device_id`). The new `…/attempts/{id}/review`
route returned **401** (not route-404) on a no-bearer probe → the running API **already carried the S5 code** after a
recycle (no manual restart needed; the five engine routes pre-exist from 5B-2). The **`QuizHub` forfeit** was proven with a
**live `@microsoft/signalr` Node client** (`ws` present → real WebSocket transport).

**Fixtures (reused seeded data — single tenant `019ed7e6` "salah-bahzad").** Happy path = **Amr Moataz** (`019eea33`,
Active) → **UserQuiz `019eeaa8`** gating session **B `019ee0ff`** ("Phase 3 wiring smoke 17:48:12", **5 q · 3 attempts ·
pass 80% · 50 min**, 1 mark/q). Amr's three attempts drove the whole engine: **A1 `019eee14`** answered **4 correct + 1
wrong → 80% Submitted** (the `≥`-boundary), **A2 `019eee19`** used for focus + the in-progress `403` then **forfeited via
the hub**, **A3 `019eee1e`** answered **5/5 → 100% Submitted**. IDOR fixture = **Student Test** (`019ee01d`) UserQuiz
`019ee6ad`'s terminal attempt `019ee6ad-8770` (Amr → 404).

| # | Check | Result |
|---|---|---|
| 1 | intro load `by-session` (+ no isCorrect) | **200** `StudentQuizDto` exact shape (settings 5/3/**80**/50, attemptsRemaining 3); **raw JSON has NO `isCorrect`** |
| 1b | **additive `id` on `attempts[]` (the S5 DTO change)** | after A1, every `attempts[]` row carries `"id"` (the `QuizAttempt.Id`) alongside number/scorePercent/status/flag/started/submitted — the §B deep-link key, **live** |
| 2 | start attempt | **200** `QuizAttemptDto` (`attemptId`/`number`/`deadlineUtc`/`serverNowUtc`/`questions`); **NO `isCorrect`, NO `hintUrl`** in raw; DB `quiz_attempts` InProgress, 5 `quiz_attempt_questions` |
| 3 | answer save-as-you-go | each `PUT …/answer` → **204**; **re-answer** (wrong→correct) → **204** (allowed); DB `SelectedOptionId`/`AnsweredAtUtc` set; **answer after terminal → 409** |
| 4 | submit + **`≥`-pass boundary** | **200** `{scorePercent:80,status:Submitted,bestPercent:80,passed:true,attemptsRemaining:2}` — **80 == minPass 80 → passed=true** (the 5B-2 `>`→`≥` fix); DB attempt Submitted/80, `user_quizzes` AttemptsUsed/BestPercent 80/Passed, **`attendance."BestQuizPercent"`=80** (the only attendance quiz col); **re-submit → 409** |
| (best-of) | best-of = **max** | A2 forfeit **0** left best **80** (lower ignored); A3 **100** raised best **80→100**; DB `BestPercent=100, Passed=t, AttemptsUsed=3` |
| 5 | focus telemetry | FocusLost/Returned → **204**; bad type → **400**; `assessment_events` **+2** for the attempt; **audit unchanged** (not audited); attempt stays **InProgress** (focus never forfeits) |
| 6a | **forfeit-on-disconnect** (live hub) | student `@microsoft/signalr` client connected to InProgress A2 → **dropped** → DB **`Status=Forfeited(2)`, `ScorePercent=0`, consumed**; intro row flips `flag:"Forfeit"`. **⚠ NO audit row written — see Finding.** |
| 6b | hub auth | `/hubs/quiz/negotiate`: no-token → **401**, student → **200**, staff → 200-at-negotiate (an authenticated principal) but **aborted at `OnConnectedAsync`** (`role != Student`); reached via the live API (the admin `:4200` proxy intentionally doesn't forward `/hubs`) |
| 7 | Hangfire timer infra | **2 Hangfire servers alive** (heartbeat); **3 `QuizAutoSubmitJob`** scheduled (one per start) + correctly **cancelled (Deleted)** on early terminate. The actual **`TimedOut` transition + its audit** weren't forced live (needs the job to fire / be invoked in-process) — **5B-2-engine + integration-suite** territory; **see Finding** (the timeout audit shares the forfeit gap) |
| 8 | **new review read** (terminal) | **200** `StudentQuizAttemptReviewDto` on A1: `attemptId`/`quizId`/`gatedSessionId`/**`sessionTitle`** ("Phase 3 wiring smoke 17:48:12")/`number`/`status Submitted`/`scorePercent 80`/`minPassPercent 80`/`timeSpentSeconds 196`; questions **ordered 1..5**, options ordered; **per-option AND per-question `isCorrect` present**; **Q1–4 `isCorrect=true`, Q5 `false`** matching the answers; `selectedOptionId` echoes each pick. Also proven on a **Forfeited** attempt (A2): 200, score 0, all `selectedOptionId=null` + per-q `isCorrect=false`, correct option still flagged |
| 9 | review on **InProgress** | **403** `reason="quiz_attempt_in_progress"`, detail *"Finish the quiz to see your answers and score."* (key never revealed mid-sitting) |
| 10 | **404** IDOR / unknown | Amr → Student Test's terminal attempt **404** (the foreign attempt **exists** → ownership boundary, not a missing row); unknown id **404**. *(Cross-tenant: only one tenant lives in this stack → covered by the `MyQuizAttemptReviewApiTests` cross-tenant integration test, `NFR-SEC-010`.)* |
| 11 | auth on review | anon **401** · staff (Teacher) **403** · student **200** |
| 12 | **isCorrect split** (same attempt) | by-session/start raw → **NO** `isCorrect`; review raw → **YES** — the live shapes stay correctness-free; the review is the gated exception |
| 13 | review **not audited** | `audit_entries` **542 → 542** across the review GET + the by-session GET. **Audit actor split:** start/submit = **`Student`** (verified live); **forfeit/timeout = `System` — NOT verified (see Finding)**; focus → `assessment_events` (verified) |
| 14 | **pass → videos-unlock** (S3) | `GET /api/me/sessions/{B}` → quiz `passed:true`; the video **"Lecture 1 — Intro edit"** reads **`lockState: Playable`** (the 5C/S3 gate reads `UserQuiz.Passed`). S5 only **triggers** the unlock via submit |
| 15 | browser walkthrough at `:4300` | **user's step** — note the **proxy is stale** after the AppHost churn; a fresh AppHost start (so the npm app re-reads the API port) is needed before the browser walk |

**Drift on S5's own surface: none.** The new `GET /api/me/quizzes/attempts/{attemptId}/review` matches the frozen contract
**§B** field-for-field (DTO shape, ordering, per-option + per-question `isCorrect`, the `403 quiz_attempt_in_progress`
reason'd gate, the `404` IDOR boundary, terminal-only including Forfeited, reads-not-audited); the **additive `id`** on
`StudentQuizAttemptSummaryDto` (§A #1) is live and correct; the `isCorrect` split holds; the reused 5B-2 engine
(load/start/answer/submit/focus/forfeit) behaves per §A/§C; pass→unlock per §F; start/submit audit = `Student` per §E.

### ✅ Finding — FIXED 2026-06-22 (pre-existing **5B-2 engine** concern, surfaced by S5 wiring — **NOT** S5 drift)

> **Resolved.** Root cause: `AuditSaveChangesInterceptor` only audited entities in EF **Added/Modified/Deleted** state. On
> a forfeit/timeout of a **later** attempt, `RecomputeBest()` rewrites `BestPercent`/`Passed` with the **same** value (a
> prior attempt already set the best), so the `UserQuiz` **root is `Unchanged`** and only the owned `QuizAttempt` moves —
> the interceptor never read the root's buffered `IAuditableDomainEvent`, so the row was dropped. (The existing 5B-2 tests
> passed because they forfeit/timeout the **first** attempt, where `BestPercent` goes `null`→value → root Modified.) **Fix:**
> the interceptor now also includes **`Unchanged` `EntityBase` entries that carry a buffered `IAuditableDomainEvent`**, so a
> semantic lifecycle event always leaves exactly one row (this also closes the latent non-improving-*submit* case).
> **Tests:** two new regression tests in `QuizProctoringTests` —
> `Forfeiting_a_later_attempt_that_does_not_improve_best_is_still_audited_as_System` and the `Timing_out_…` twin (both
> red before the fix, green after); **63/63 audit+quiz integration tests green**, no regressions. Files:
> `AuditSaveChangesInterceptor.cs` + `QuizProctoringTests.cs`. Goes live on the next API rebuild/restart (the running API
> still carries the pre-fix binary). **NOT committed.**

**(original finding, for the record)**

**Forfeit-on-disconnect (and timeout) write no audit row.** The hub forfeit transitions the attempt correctly
(`Forfeited`/`0`/consumed) **but emits no `audit_entries` row** — `QuizAttemptForfeited` and `QuizAttemptTimedOut` have
**never** appeared in the audit log (only `QuizAttemptStarted`/`QuizAttemptSubmitted` exist). Contract **§E** (and 5B-2 §A
`FR-PLAT-QZ-010`) expect a **`System`-actor** forfeit/timeout row. The likely cause is a **post-commit dispatch-after-dispose**
in the hub's / Hangfire job's **no-`HttpContext`** path (the same class of bug the 5B-1 `ExecuteInTransactionAsync` fix
addressed): the lifecycle service commits the state change, but the domain-event handler that writes the audit row runs
after the short-lived scope is torn down. The **5B-2 integration suite asserts this in-process** (which is why it passed
there), but the **live hub/job path does not produce the row**. This is a **5B-2 engine fix** — it does **not** affect S5's
deliverable (the review read + additive `id` are fully correct). Recommend a 5B-2 ticket to route the forfeit/timeout audit
through the same post-commit dispatch the request-path uses (or set `ISystemOperationContext` + flush before scope dispose).

**Not forced live (deferred, by design / environment):** the **`TimedOut` transition** (needs the Hangfire job to fire at
the 50-min deadline or be invoked in-process — infra proven alive, transition is 5B-2/suite); **cross-tenant review 404**
(single live tenant — covered by the integration test); **browser walkthrough #15** (user's visual step; the `:4300` proxy
needs an AppHost refresh first).

**Fixtures left in place:** Amr's UserQuiz `019eeaa8` is **exhausted** (A1 Submitted 80 / A2 Forfeited 0 / A3 Submitted 100,
all **terminal → reviewable**; `BestPercent 100`, `Passed`, videos unlocked). Re-running the answer-through needs a **fresh
enrollment** (a terminal attempt never reopens; attempts are consumed). The append-only `audit_entries` (start/submit rows)
must not be deleted.

**Gotchas confirmed:** (1) the AppHost **restarted repeatedly mid-run** — re-resolve the **API port from the live PID** and
discover **PG/Redis by image** every call; the **`:4300` proxy goes stale** on each restart (drive REST + the hub at the API
directly). (2) both quiz `Order` columns are stored as **`"DisplayOrder"`**. (3) the live attempt shape hides `isCorrect`,
so **read the snapshot from the DB to drive a known score** (answer by the snapshot's correct option id, sidestepping the
variation/DisplayOrder flip). (4) `@microsoft/signalr` + `ws` are installed → a **Node hub client** proves forfeit-on-disconnect
live. (5) the forfeit/timeout **audit gap** above.

**S5 unblocks S6** (profile — the final student slice). The quiz-gated session left **passed** (videos unlocked) + the three
terminal attempts left **reviewable** are the engagement S6 sits beside.
