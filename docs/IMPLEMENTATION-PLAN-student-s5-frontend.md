# Student Portal · S5 — FRONTEND stream (Proctored quiz: intro · runner · results · answer-key review)

> Status: **Planned — not yet built** · Created 2026-06-22 · The **app half** of slice **S5** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S5 — Quizzes, proctored). Builds the single-sitting **quiz intro**
> + **runner** (live countdown, save-as-you-go, focus-loss telemetry, forfeit-on-leave/disconnect) + **results**
> (score-only) + the **new** per-attempt **answer-key review** into the **existing**
> `libs/student-portal/feature-assessment` lib (S4 created it for the assignment runner — S5 **extends** it, no new
> lib). Installs **`@microsoft/signalr`** and adds a **`QuizHub`** client whose **sole** job is to arm
> forfeit-on-disconnect. The S3 session-detail **Prerequisite quiz** card currently dead-ends (its `openQuiz()`
> sets `quizNote → "Opens in the next update."`) — S5 fills the `/sessions/:id/quiz(/...)` routes it should land on.
>
> Run in its **own** Claude session, parallel-safe with the backend stream. **File ownership: `frontend/**` only.**
> Match the **frozen contract** (`docs/contracts/student-s5-quizzes.md`) field-for-field — the **reused** five-route
> engine + the `QuizHub` (§A / §A.1) with its **one additive** `id` on each attempt summary (§A #1), the **new**
> review read (`GET /api/me/quizzes/attempts/{attemptId}/review` → `StudentQuizAttemptReviewDto`, §B) with its
> `403 quiz_attempt_in_progress` / `404` boundary (§B.2), the runner interaction rules (informed intro,
> hub-open-on-start, **local countdown vs server-authoritative Hangfire**, save-as-you-go, manual/auto submit,
> forfeit-by-disconnect/leave, focus-recorded-not-forfeited, §C), and the results-vs-review screen split (§D).
>
> Satisfies: `FR-STU-QZ-001..010`, `FR-PLAT-QZ-001..010` (best-of, `≥`-pass, focus-loss-recorded-not-forfeit,
> forfeit-on-leave, the server-authoritative timer), `FR-STU-RWD-001/002` (responsive), `FR-STU-A11Y-001` (a11y).
> Green gate: `npx nx build student-portal` (AOT type-checks templates) +
> `nx test student-portal-feature-assessment` + the data-access **`quiz.service.spec.ts`**.

---

## Design source of truth (the student prototype, NOT the Teacher portal)

- **Prototype:** `.claude/Salah Bahzad Student Portal/Student Portal.html` (siblings `Dropdown.jsx`, `support.js`).
  S5 builds four screens + one modal from it:
  - **Quiz intro** (`screen === 'quizIntro'`): a rules/time/attempts/best card — the **time limit**, **question
    count**, **randomisation** note, **attempts remaining**, **best score**, and the **"one sitting only — leaving
    forfeits this attempt and records a zero, and it counts as one of your limited attempts"** warning; a primary
    **Start** (when `attemptsRemaining > 0` and no active attempt) or **Resume** (when `activeAttemptId != null`).
  - **Quiz runner** (`screen === 'quiz'`): a header with a **`fmt(sec)` countdown** (the prototype's `fmt`), the
    **question dots** + **prev/next**, a question card (LaTeX/image + MCV options, picked = highlighted); a
    `beforeunload` guard (`_beforeUnload`) and a `visibilitychange → quizTabSwitches` counter; a primary **Submit**.
  - **Quiz results** (`screen === 'quizResults'`, **SCORE-ONLY**): a centred card — a **pass/fail mascot**
    (`salah-passed.png` / `salah-failed.png`), a **score ring** (this attempt's `scorePercent`), a headline + sub,
    **two stat tiles — "This attempt"** and **"Best of"** — and a primary **"Back to session"** (`go('sessionDetail')`).
    **No answer key here.**
  - **"Leave the quiz?" modal** — quote verbatim:
    > **Leave the quiz?**
    > Leaving now forfeits this attempt and records a zero. It also counts as one of your limited attempts.
    > There's no way to resume.
    > buttons: **"Stay in quiz"** / **"Leave & forfeit"**
  - **Answer-key review** — the prototype has **NONE** (the quiz flow ends at the score-only `quizResults`). The §B
    review is a **NEW** student screen, built to the contract and mirroring the **admin / S4** review treatment
    (green-check on the correct option, red on the student's wrong pick, a per-question right/wrong indicator) for
    visual consistency — **re-implemented, never imported** (§ Conventions).
- **Tokens / assets / icons / fonts:** already mirrored into `apps/student-portal` by S0 — **reuse**, don't
  re-mirror. Mascots (`apps/student-portal/src/assets/salah-passed.png` / `salah-failed.png` for results;
  `salah-prerequisite.png` for the "finish first" panel) are available. Outline icons inline via
  `DomSanitizer.bypassSecurityTrustHtml` (the admin/S0/S2/S3/S4 pattern; Angular strips `<svg>` from plain
  `[innerHTML]`).
- When prototype and this doc conflict, **the prototype wins** on layout/copy (the countdown chrome, the dots, the
  results tiles, the modal copy); **the contract wins** on the engine calls + state, the local-vs-server-timer rule,
  the review DTO field names, and the `403`/`404` review boundary.

---

## Conventions (mirror `frontend/CLAUDE.md` + master plan §3.2)
- **Extend the EXISTING lib — no new lib.** S5's quiz pieces go in `libs/student-portal/feature-assessment` (S4
  created it for the assignment runner/review). **Do NOT** scaffold a new lib. Add the quiz components, **export
  them from the lib barrel** (`src/index.ts`), add the **three** quiz route entries to
  `apps/student-portal/src/app/app.routes.ts`, and confirm they resolve at `:4300` — an unrouted lib still builds
  green (the recurring "unrouted feature-attendance" trap from S1/5B-1 wiring). The `tsconfig.base.json` alias
  `@sb/student-portal/feature-assessment` already exists (S4) — reuse it.
- **Module boundaries:** `scope:student-portal` → `scope:shared` only. Reuse `@sb/shared/ui`; **never** import an
  admin-portal lib — the admin quiz/assignment review components are a **visual reference to RE-IMPLEMENT, not
  import**. `feature-assessment` may consume `@sb/student-portal/data-access`. `feature-sessions` navigates **to**
  the quiz via a **route string** (F8) — it does **not** import `feature-assessment` (keep the boundary; S4 already
  set this precedent with `openAssignment()`).
- **No cross-feature import for the results ring.** There is **no shared circular ring** in `@sb/shared/ui`; S3's
  ring lives in `feature-sessions/src/lib/ui/circular-progress.component.ts` (another `scope:student-portal` feature).
  **Re-implement** a small score ring inside `feature-assessment` for the results screen — do **not** cross-import
  `feature-sessions`. (`@sb/shared/ui`'s `Progress` is **linear** only.)
- Angular v20+: standalone, `OnPush`, signal `input()/output()/model()`, `computed()/effect()`, `inject()`, native
  control flow, typed reactive forms where needed. Cite `FR-*`/`NFR-*` in tests.
- **Reuse the shared UI:** `Button` (variants `primary`/`accent`/`secondary`/`ghost`), `LatexPreview` (best-effort
  LaTeX — **no** KaTeX/MathJax dependency, the admin/S3/S4 pattern — for `bodyLatex` + option `text`),
  `Progress` (linear — the results "Best of" / per-question optional bar), `StatusPill` / `Tag` (the attempt flag
  Clean/Timeout/Forfeit + the pass/fail chip), `Alert` (the focus-loss "this was logged" notice + the "finish
  first" panel), `Modal` (`size="confirm"` — the **"Leave the quiz?"** dialog). **There is NO `Timer` component** —
  build a **local countdown** (a `signal` seeded from `deadlineUtc − serverNowUtc`, ticking **down** to zero via a
  `setInterval` started under **`NgZone.runOutsideAngular`** — the S4 gotcha: an in-zone recurring macrotask hangs
  `whenStable()` — rendered `M:SS` via a `fmt(sec)` like the prototype). Keep any new presentational bits in
  `feature-assessment` (promote to `libs/student-portal/ui` only if a later phase reuses them).

> **Reuse, don't widen.** The live attempt shapes `QuizAttemptDto` / `QuizQuestionDto` / `QuizOptionDto` (§A #2)
> and the intro `StudentQuizDto` (§A #1) **forbid `isCorrect`** — the 5B-2 invariant and its guard test stand.
> The **only** correctness-bearing shape is the **distinct** `StudentQuizAttemptReviewDto` (§B), and **only** for a
> **terminal** attempt. Model them as **separate** TS interfaces in data-access; do **not** add `isCorrect` to the
> live/intro interfaces.

---

## Steps

### F1 — Quiz routes + barrel exports (extend feature-assessment; avoid the unrouted-lib trap)
- **No new lib** — work in `libs/student-portal/feature-assessment`. Export the new components from the lib barrel
  (`src/index.ts`): `QuizIntroComponent`, `QuizRunnerComponent`, `QuizResultsComponent`, `QuizReviewComponent`
  (you may merge intro+runner into one routed component if it reads cleaner — keep the routes below stable).
- Add **three lazy routes** under the **authenticated shell** + `authGuard` in `app.routes.ts` (beside the S3
  `sessions/:id` + the S4 `sessions/:id/assignment(/review)` entries):
  - `{ path: 'sessions/:id/quiz', loadComponent: … }` — the **intro + runner** (start there; the intro decides
    Start/Resume → enters the runner). *(If you split them, use `sessions/:id/quiz` for the intro and
    `sessions/:id/quiz/run` for the runner — but a single component switching `phase('intro'|'run')` matches the
    prototype's `screen` flips and avoids a mid-attempt route change.)*
  - `{ path: 'sessions/:id/quiz/results', loadComponent: … QuizResultsComponent }`
  - `{ path: 'sessions/:id/quiz/attempts/:attemptId/review', loadComponent: … QuizReviewComponent }`
- Bind `:id` / `:attemptId` via the existing `withComponentInputBinding()` (the S3/S4 pattern). **No new shell nav
  item** — every screen is reached *from* the S3 session detail / the results card / the intro's attempt rows.
- **Prove all three resolve at `:4300`** (not just a green build — the unrouted-lib trap).

### F2 — Data access: `QuizService` + quiz models (authenticated — bearer + refresh apply)
In `libs/student-portal/data-access` (beside `CatalogueService` / `MySessionsService` / `AssignmentService`), add a
`QuizService`. These are **authenticated** — they ride the existing `studentAuthInterceptor` (bearer attached,
401→refresh replay, `sb_device` cookie via `withCredentials`). **Do not** add them to `ANONYMOUS_PATHS`
(`['/api/auth/', '/api/students/register', '/api/reference/']` — `/api/me/quizzes` is **not** exempt). Use the same
`__SB_API_URL__` shim as `AssignmentService`.
- `quiz(sessionId): Observable<StudentQuiz>` → `GET /api/me/quizzes/by-session/{sessionId}` (§A #1) →
  `StudentQuizDto` (the **intro** shape; no questions, no correctness; includes `activeAttemptId` + each attempt's
  additive **`id`**). A `404` means the session has no quiz → route back to `/sessions/{id}` (not a hard error).
- `start(quizId): Observable<QuizAttempt>` → `POST /api/me/quizzes/{quizId}/attempts` (§A #2) → `QuizAttemptDto`
  (the live attempt: `attemptId`, `deadlineUtc`, `serverNowUtc`, drawn `questions` **without** `isCorrect`). A
  **`409`** = attempts exhausted **or** an attempt already active (the intro should offer **Resume** instead — but
  surface the `detail` if it slips through).
- `answer(attemptId, aqId, selectedOptionId): Observable<void>` →
  `PUT /api/me/quizzes/attempts/{attemptId}/questions/{aqId}/answer` (§A #3), body `{ selectedOptionId }` → `204`.
  **Save-as-you-go** — call on each pick (re-pick re-`PUT`s). A `409` = the attempt is no longer `InProgress` /
  past `deadlineUtc`.
- `submit(attemptId): Observable<QuizAttemptResult>` → `POST /api/me/quizzes/attempts/{attemptId}/submit` (§A #4)
  → `QuizAttemptResultDto` (score-only: `scorePercent`, `status`, `bestPercent`, `passed`, `attemptsRemaining` —
  **no questions, no `attemptId`**; the runner holds it). A **`409`** = the attempt is already terminal (e.g. the
  Hangfire timer already `TimedOut` it — §C handles the race).
- `focus(attemptId, body): Observable<void>` → `POST /api/me/quizzes/attempts/{attemptId}/focus` (§A #5), body
  `{ type: 'FocusLost'|'FocusReturned', occurredAtUtc, durationMs? }` → `204`. **Monitoring only — never forfeits.**
  A `400` = a bad `type`.
- `review(attemptId): Observable<StudentQuizAttemptReview>` →
  `GET /api/me/quizzes/attempts/{attemptId}/review` (§B) → `StudentQuizAttemptReviewDto` (the **only** student
  surface exposing `isCorrect`). A **`403` `quiz_attempt_in_progress`** = the caller's own but still `InProgress`
  (the deep-link edge — surface the friendly "finish first" message, §B.2); a **`404`** = unknown / another
  student's / another tenant's (route back to `/sessions/{id}`).
- **Models** — add to `libs/student-portal/data-access` (a `lib/quizzes/` folder; export from the barrel). Keep the
  **live** and **review** shapes **distinct** (the live ones never carry `isCorrect`):
  - `QuizAttemptStatus = 'InProgress' | 'Submitted' | 'Forfeited' | 'TimedOut'`; `QuizAttemptFlag = 'Clean' |
    'Timeout' | 'Forfeit'`; `FocusEventType = 'FocusLost' | 'FocusReturned'`.
  - `StudentQuiz` (§A #1 `StudentQuizDto`): `{ id, gatedSessionId, settings: QuizSettings, attemptsUsed,
    attemptsRemaining, bestPercent: number|null, passed, activeAttemptId: string|null, attempts:
    StudentQuizAttemptSummary[] }`; `QuizSettings`: `{ timeLimitMinutes, questionCount, attemptCount,
    minPassPercent }`; `StudentQuizAttemptSummary`: `{ id, number, scorePercent: number|null, status:
    QuizAttemptStatus, flag: QuizAttemptFlag, startedAtUtc, submittedAtUtc: string|null }` — **`id` is the additive
    field that deep-links the §B review.**
  - `QuizAttempt` (§A #2 `QuizAttemptDto`, **live**): `{ attemptId, number, deadlineUtc, serverNowUtc, questions:
    QuizAttemptQuestion[] }`; `QuizAttemptQuestion`: `{ id, order, bodyLatex: string|null, imageUrl: string|null,
    options: QuizAttemptOption[] }`; `QuizAttemptOption`: `{ id, order, text }` — **NO `isCorrect`, NO `hintUrl`.**
  - `QuizAttemptResult` (§A #4 `QuizAttemptResultDto`): `{ scorePercent, status: QuizAttemptStatus, bestPercent,
    passed, attemptsRemaining }`.
  - `FocusEventBody`: `{ type: FocusEventType, occurredAtUtc: string, durationMs?: number }`.
  - `StudentQuizAttemptReview` (§B.1 `StudentQuizAttemptReviewDto`): `{ attemptId, quizId, gatedSessionId,
    sessionTitle: string|null, number, status, scorePercent, minPassPercent, startedAtUtc, submittedAtUtc,
    timeSpentSeconds, questions: StudentQuizReviewQuestion[] }`; `StudentQuizReviewQuestion`: `{ id, order,
    bodyLatex: string|null, imageUrl: string|null, mark, options: StudentQuizReviewOption[], selectedOptionId:
    string|null, isCorrect }`; `StudentQuizReviewOption`: `{ id, order, text, isCorrect }` — **`isCorrect` exposed
    (review only).** Keep these **distinct** from the live interfaces.
  - Model all enums as the contract's **string unions**; export the service + all interfaces from
    `libs/student-portal/data-access/src/index.ts`.

### F3 — `QuizHub` SignalR client — arms forfeit-on-disconnect ONLY (`FR-STU-QZ-004`, contract §A.1)
- **Install** `@microsoft/signalr` (`npm i @microsoft/signalr` — it is **not** yet in `frontend/package.json`). The
  student app's `proxy.conf.js` already forwards `/hubs` with `ws:true` (S0) — no proxy change.
- Add a small connection wrapper in `feature-assessment` (e.g. `quiz-hub.client.ts` — a tiny injectable or a helper
  the runner owns). Build it with `HubConnectionBuilder().withUrl('/hubs/quiz', { accessTokenFactory: () =>
  authStore.getAccessToken() ?? '' }).build()` — the JWT rides the SignalR **`access_token`** query scoped to the
  hub path (the contract's auth scheme; `StudentAuthStore.getAccessToken()` already exists in data-access). Use the
  same base URL as the services (the `__SB_API_URL__` shim, or a relative `/hubs/quiz` so the dev proxy handles it).
- **Lifecycle = the forfeit:**
  - **`start()`** the connection **immediately after** the REST `start(quizId)` (#2) resolves — the hub binds this
    connection to the active attempt on `OnConnectedAsync`, so the attempt **must** exist first (§A.1).
  - **`stop()`** on a clean submit / leave-confirm / `ngOnDestroy`.
  - **The disconnect IS the forfeit.** Do **NOT** enable `withAutomaticReconnect()` in a way that implies the
    attempt survives a drop — by the time a reconnect fires the server has already forfeited the attempt (score 0,
    consumed). A silent reconnect that re-binds would be a lie to the student. Keep it **single-shot**: connect on
    start, tear down on exit; if the socket drops mid-sitting, that **is** the forfeit (the contract's
    authoritative behaviour).
- **The hub sends nothing** — no server→client ticks, no client→server methods. Register **no** `.on(...)`
  handlers for data. Holding the connection open is purely to arm `OnDisconnectedAsync`'s forfeit. The countdown is
  the **local** timer (F5), authoritatively backstopped by the server-side Hangfire job — **not** the hub.
- Keep `start()/stop()` behind a **test seam** (an overridable method / an injected factory) so the Jest runner
  specs can assert "connection opened on attempt start, closed on submit/leave" without a real WebSocket.

### F4 — `QuizIntroComponent` (informed intro — `FR-STU-QZ-001/002`) — contract §C
A standalone `OnPush` screen (the `phase('intro')` of the routed quiz component, or a dedicated component) at
`/sessions/:id/quiz` (read the session `id` from the route via input binding):
- On init `quiz(sessionId)` (#1) → set the `StudentQuiz` signal. A **`404`** (no quiz for the session) → route back
  to `/sessions/{id}`.
- **Render the rules from `settings`:** the **time limit** (`timeLimitMinutes`), the **question count**
  (`questionCount`, "randomly drawn"), the **pass mark** (`minPassPercent`), the **attempts**
  (`attemptsRemaining` / `attemptCount`), and the **best score** (`bestPercent`, "—" until the first attempt
  terminates). A **pass/fail** chip when `passed` is meaningful.
- **The one-sitting warning** (prominent `Alert`): "This quiz is **one sitting** — leaving forfeits this attempt,
  records a zero, and uses one of your limited attempts." (The full forfeit copy lives in the runner's leave modal,
  F5.)
- **Primary CTA:**
  - **Start** — enabled only when `attemptsRemaining > 0` **and** `activeAttemptId == null`. Calls `start(quizId)`
    (#2) → on success **flip to the runner** (F5) with the returned `QuizAttempt`. A `409` (raced into active) →
    refetch `quiz(sessionId)` and offer **Resume**.
  - **Resume** — shown when `activeAttemptId != null`: re-enter the runner on that attempt. **Resolved (the contract
    settles this, §A/§A.1):** §A exposes **no** "GET live attempt" route, so the live questions are held in an
    **in-memory signal** carried across the intro→runner flip (a **single** navigation in one lib — the intro hands the
    `QuizAttempt` straight to the runner, like S4's session-detail→runner handoff). Resume is therefore **best-effort
    within one page session**. A **true full-page reload** mid-attempt is **unrecoverable by design** — the hub drop on
    `unload` **forfeits** the attempt (§A.1), so on reload `quiz(sessionId)` will already show that attempt
    `Forfeited`. Resume must **NEVER silently re-`start`** (that `409`s, attempt active); if we somehow lack the live
    questions and the attempt is still `InProgress`, surface a "couldn't resume — reopen the session" message rather
    than re-starting. No contract change; this is the engine's intended single-sitting shape.
  - When `attemptsRemaining == 0` and not passed → a disabled "No attempts left" state; when `passed` → a
    **"Review quiz"** affordance to the best/last terminal attempt's §B review.
- **Attempt history** — render `attempts[]` rows (number, `scorePercent`, the `flag` pill Clean/Timeout/Forfeit);
  each **terminal** row (`status != 'InProgress'`) gets a **"Review"** link →
  `/sessions/:id/quiz/attempts/:attemptId/review` using that row's `id` (§A #1, §D). An `InProgress` row shows no
  Review link (the §B `403` is the deep-link edge only).

### F5 — `QuizRunnerComponent` (the proctored runner) — `FR-STU-QZ-003..007`, contract §C
The runner (the `phase('run')` of the quiz component) renders the drawn `questions` from `start()` (#2):
- **One card at a time** with the prototype's **question dots** + **prev/next** to move between questions; render
  `bodyLatex` via shared `LatexPreview`, the optional `imageUrl` inline, and each option's `text` via
  `LatexPreview`. The student's current pick is highlighted. **No hint** (quiz questions carry none, §0). Picking an
  option → **`answer(attemptId, question.id, optionId)`** (#3) immediately; re-picking re-`PUT`s (allowed). **No
  client-only draft** (§C).
- **Open the hub on start** (F3): immediately after `start()` resolves, open the `QuizHub` connection and keep it
  open for the whole sitting. Tear it down on a clean submit / leave / destroy.
- **Local countdown, server-authoritative** (`FR-STU-QZ-003/006`): seed the countdown from `deadlineUtc −
  serverNowUtc` (correct the local clock by `serverNowUtc`, then count **down** to `deadlineUtc`). Tick via a
  `setInterval` started under **`NgZone.runOutsideAngular`** (the S4 gotcha), nudging a `signal` each second;
  render `M:SS` via `fmt(sec)`. The countdown is a **display** — the **grade is the server's**. (The prototype's
  warn-at-60s visual treatment is layout/copy — apply it.)
- **Auto-submit on local zero** (`FR-STU-QZ-006`): when the local countdown hits 0, call `submit()` (#4). If that
  **`409`s** because the Hangfire job **already `TimedOut`** the attempt, **re-fetch `quiz(sessionId)`** (#1), read
  the just-ended attempt's summary (now `TimedOut`, with its `scorePercent` + `id`), and route to **results** (or
  straight to that attempt's review) — never show an error for the timeout race; the server's clock wins.
- **Manual submit** (`FR-STU-QZ-006`): the runner's **Submit** button calls `submit()` (#4) →
  `QuizAttemptResultDto`; on success tear down the hub and route to **results** (§D, F6) carrying the result + the
  `attemptId` (for the "Review answers" link).
- **Forfeit-on-leave** (`FR-STU-QZ-004`): a `window:beforeunload` HostListener arms the browser's native "leave?"
  prompt while the runner is mounted (the prototype's `_beforeUnload`). An **in-app** navigation away (a nav click /
  back) opens the **"Leave the quiz?"** confirm `Modal` (`size="confirm"`, the verbatim copy + **"Stay in quiz"** /
  **"Leave & forfeit"** from § design anchor). **"Leave & forfeit"** proceeds with the navigation, which tears down
  the hub connection → the server **forfeits** the attempt (score 0, consumed). **"Stay in quiz"** dismisses. **There
  is no client "forfeit" call — leaving IS the forfeit** (§A.1).
- **Focus-loss recorded, not forfeited** (`FR-STU-QZ-005`): a `document:visibilitychange` (+ window `blur`/`focus`)
  HostListener detects tab/window switches; on each, `focus(attemptId, { type, occurredAtUtc, durationMs? })` (#5).
  Track a `FocusLost` timestamp → emit `FocusReturned` with the `durationMs`. **This MUST NOT end the attempt** —
  only a *disconnect* forfeits. The prototype increments a silent counter; the contract permits an optional
  on-screen "this was logged" `Alert` (`FR-STU-QZ-005` "MAY be shown") — keep it non-blocking.
- **Reachable only for quiz-gated sessions** — the runner is entered from the S3 detail's **Prerequisite quiz** card
  (F8), which only renders when `detail.quiz != null`. A direct deep-link to `/sessions/:id/quiz` for a session with
  no quiz hits #1's `404` → routes back to `/sessions/{id}`.
- **Resume note** (resolved — §A/§A.1): §A exposes no "GET live attempt" route, so the runner holds the live
  `QuizAttempt` in an **in-memory signal** across the intro→runner flip (a single navigation in this one lib). Resume is
  **best-effort within the page session**; a **full reload** mid-attempt is **an accepted forfeit by design** (the hub
  drop on `unload` forfeits, §A.1 — on reload `quiz(sessionId)` shows the attempt `Forfeited`). **Never** silently
  re-`start` on Resume (that `409`s); if the live questions are lost while the attempt is still `InProgress`, show a
  "couldn't resume" message, don't re-start. This is the engine's intended single-sitting behaviour, not an open item.
- **Responsive + a11y** (`FR-STU-RWD-001/002`, `FR-STU-A11Y-001`): options are a labelled radio-group
  (`role="radiogroup"` + `aria-checked`); the countdown is `aria-live="off"` (don't announce every tick) with an
  accessible label; the question dots are keyboard-navigable; touch-sized targets; the card column collapses on phone.

### F6 — `QuizResultsComponent` (score-only — `FR-STU-QZ-008`) — contract §D
A standalone `OnPush` screen at `/sessions/:id/quiz/results`, matching the prototype's `quizResults` (the result +
`attemptId` arrive via navigation `state` from the runner, or are re-derived from `quiz(sessionId)`'s latest
terminal attempt):
- **Pass/fail mascot** (`salah-passed.png` when `passed`, else `salah-failed.png`), a **score ring** for this
  attempt's `scorePercent` (re-implement a small ring in `feature-assessment` — do **not** cross-import S3's), a
  **headline** + sub, and **two stat tiles — "This attempt"** (`scorePercent`) and **"Best of"** (`bestPercent`).
- **Primary "Back to session"** → `Router.navigate(['/sessions', id])` (the prototype's `go('sessionDetail')`).
- **A "Review answers" affordance** → `/sessions/:id/quiz/attempts/:attemptId/review` for the **just-finished**
  attempt (the runner holds its `attemptId` from #2 / the timeout race read it from #1). This is the §B review
  entry point from results (the answer key is **not** on this score-only screen, §D/§G).

### F7 — `QuizReviewComponent` (the NEW per-attempt answer-key review) — `FR-STU-QZ-009`, contract §B/§D
A standalone `OnPush` screen at `/sessions/:id/quiz/attempts/:attemptId/review` (read both params via input binding).
**The review read is keyed by `attemptId`** (§B):
- On init `review(attemptId)` (the `review()` service method, §B). On **`200`** render the answer key.
- **Header** (mirrors the staff/S4 review header, student-scoped): **"{sessionTitle} · Quiz review"** +
  **"Attempt {number}"** + the **score** (`{scorePercent}%`, a **pass/fail chip** derived client-side from
  `scorePercent >= minPassPercent`) + **time** (`M:SS` from `timeSpentSeconds`) + the attempt **flag**
  (Clean/Timeout/Forfeit, derived from `status`). `sessionTitle` may be `null` — fall back gracefully.
- **Per question** (§D): the body (LaTeX/image via `LatexPreview`) + each option with **three** visual states from
  per-option `isCorrect` + the question's `selectedOptionId`: the **correct** option marked (green check); the
  student's **wrong** pick marked (red, when `selectedOptionId` is set and that option's `isCorrect == false`); a
  per-question **right/wrong indicator** from the question's `isCorrect`. An **unanswered** question
  (`selectedOptionId == null` — common on a `TimedOut`/`Forfeited` attempt) shows the **correct** option only.
  Re-implement the admin/S4 review option-state styling via `[data-state]` (do **not** import an admin lib).
- **Read-only** — the review never mutates (the attempt is terminal + immutable, `FR-STU-QZ-009`/`FR-PLAT-QZ-009`).
- **Error states** (§B.2): a **`403 quiz_attempt_in_progress`** (deep-link edge — terminal rows are the only ones
  with a Review link) → a friendly **"finish first"** panel (mascot `salah-prerequisite.png` optional) rendering
  the server `detail` ("Finish the quiz to see your answers and score.") + a **"Continue quiz"** button →
  `/sessions/{id}/quiz`; a **`404`** → route back to `/sessions/{id}`. **`401`** is handled by the interceptor;
  staff **`403`** can't happen for a signed-in student.
- Responsive + a11y as F5 (the answer key is a labelled list; the correct/wrong markers carry text, not colour
  alone — `FR-STU-A11Y-001`).

### F8 — Wire S3's session-detail Quiz card → the intro/runner / review (the ONE `feature-sessions` edit)
S3 left `SessionDetailComponent.openQuiz()` as a placeholder
(`frontend/libs/student-portal/feature-sessions/src/lib/session-detail/session-detail.component.ts`): it sets
`quizNote.set(true)` and renders **"Opens in the next update."**, while `quizMeta()` already computes the CTA
**"Start attempt"** (no attempts used) / **"Try again"** (attempts used, not passed) / **"Review quiz"** (passed).
**Replace `openQuiz()` to navigate** (mirroring S4's F6 `openAssignment()` replacement):
- `passed` → `Router.navigate(['/sessions', id, 'quiz'])` landing on the intro, whose **"Review quiz"** affordance
  opens the best/last terminal attempt's review — *or*, if the detail carries a terminal `attemptId`, navigate
  straight to `/sessions/:id/quiz/attempts/:attemptId/review`. (Keep it to the **intro** unless the detail already
  exposes an attempt id — the intro is the canonical hub for attempt history.)
- else → `Router.navigate(['/sessions', id, 'quiz'])` (the intro → Start/Resume/Try-again).
- Pass the session title via navigation `state` (as S4 does) so the runner/results/review header can use it when
  the DTO `sessionTitle` is absent.

Remove the `quizNote` placeholder note (and its signal if now unused — note `quizNote` is **separate** from any
assignment note). This is the **only** `feature-sessions` touch — a **route string**, not an import — keep the
module boundary intact.

### F9 — Tests (Jest; `whenStable()`, never `fakeAsync` — the 5B-1 jsdom gotcha)
Set up like `feature-sessions`' / S4's `*.component.spec.ts` — `TestBed.resetTestingModule()` for re-setup,
`fixture.componentRef.setInput('id', …)`, **`await fixture.whenStable()`** (not `fakeAsync`), and **mock the
data-access barrel** via `jest.mock('@sb/student-portal/data-access', …)` (the ESM-fire gotcha — re-export the
service stub + any consts). Drive the timer / hub / focus through the **test seams** (not real wall-clock /
WebSocket).
- `quiz-runner.component.spec.ts`: renders **one question at a time** with **question dots** + prev/next; picking an
  option calls `answer()` with the **right `aqId` (= `question.id`) + `optionId`**; the **local countdown** seeds
  from `deadlineUtc`/`serverNowUtc` and renders `M:SS`; hitting **local zero** calls `submit()`, and a **`409`**
  from `submit()` triggers a `quiz(sessionId)` re-fetch that reads the `TimedOut` attempt → results/review (no
  error shown); **manual Submit** → `submit()` → results; the **hub connection opens on start** and **closes on
  submit/leave** (assert the seam); an **in-app leave** opens the **"Leave the quiz?"** modal and **"Leave &
  forfeit"** proceeds (the hub tears down = forfeit); **`visibilitychange`/blur** fires `focus()` with the right
  `type` and **does NOT** submit/forfeit; a `404` from `quiz()` (deep-link, no quiz) routes back to `/sessions/{id}`.
- `quiz-intro.component.spec.ts`: renders rules/time/attempts/best; **Start** is enabled only when
  `attemptsRemaining > 0` and `activeAttemptId == null` and calls `start()`; **Resume** shows when
  `activeAttemptId != null`; each **terminal** attempt row exposes a **Review** link to
  `…/attempts/:id/review` and an `InProgress` row does not.
- `quiz-results.component.spec.ts`: **score-only** — pass mascot + score ring + "This attempt" / "Best of" tiles +
  "Back to session"; the **"Review answers"** link targets `…/attempts/:attemptId/review`; **no** answer key is
  rendered here.
- `quiz-review.component.spec.ts`: renders the **answer key** (the **correct** option marked, the student's
  **wrong** pick marked, the per-question right/wrong indicator, the **score** + **time** + flag); an **unanswered**
  question shows the correct option only; a **`403 quiz_attempt_in_progress`** renders the **"finish first"** panel
  + a "Continue quiz" link (and does **not** render the key); a **`404`** routes back to `/sessions/{id}`.
- `quiz.service.spec.ts`: each method hits the **right path WITH a bearer** (not exempted) — `quiz()` →
  `GET …/by-session/{id}`, `start()` → `POST …/{quizId}/attempts`, `answer()` → `PUT
  …/attempts/{attemptId}/questions/{aqId}/answer` with `{ selectedOptionId }`, `submit()` → `POST
  …/attempts/{attemptId}/submit`, `focus()` → `POST …/attempts/{attemptId}/focus` with the body, `review()` →
  `GET …/attempts/{attemptId}/review`; all map the **string-union enums** correctly; the `409` (submit race), `403
  quiz_attempt_in_progress`, and `404` flow through as `HttpErrorResponse`.

## Exit criteria
A signed-in student opens a session's **Prerequisite quiz** from the S3 detail card → an **informed intro** (time,
question count, attempts remaining, best, the one-sitting warning) → **Start** mints an attempt, **opens the
`QuizHub`** (arming forfeit-on-disconnect), and enters the **runner** with a **live local countdown** (server-clock
corrected, M:SS, `NgZone.runOutsideAngular`); the student moves through randomised LaTeX/image questions via the
**dots/prev-next**, each pick **saved immediately**; a **tab switch** logs a focus event **without** forfeiting; an
**in-app leave** raises the **"Leave the quiz?"** modal and **"Leave & forfeit"** drops the hub → the server
forfeits (zero, consumed); on **Submit** (or the local timer hitting zero, with the Hangfire `409` race re-read
cleanly) the **score-only results** show the pass/fail mascot + ring + "This attempt"/"Best of" + "Back to session"
+ a **"Review answers"** link; the **NEW review** screen shows each question's **correct** option, the student's
pick (green/red), the per-question right/wrong, the score + time + flag — **read-only**, with a friendly **"finish
first"** state on a `403 quiz_attempt_in_progress` deep-link and a route-back on `404`; **passing** flips the S3
playlist's video lock state (proven by the wiring stream re-reading `GET /api/me/sessions/{B}`); the screens are
responsive + a11y-clean on phone/tablet/desktop. `npx nx build student-portal` (AOT) +
`nx test student-portal-feature-assessment` + `quiz.service.spec.ts` green. Hand to wiring.

## Out of scope (defer)
**No answer key on the results screen** — the prototype's `quizResults` is score-only (§D); the answer key is the
**§B review** screen, reached from the intro's attempt rows / the results "Review answers" affordance (§G); **no
engine change** — the five §A routes, the randomisation/grading math, the snapshot, the best-of/`≥`-pass rule, the
Hangfire timer, the `QuizHub` forfeit, and the attendance/video-unlock write are reused exactly as 5B-2 shipped them
(any load/start/answer/submit/focus/forfeit/timeout drift is a **5B-2** finding, not S5's, contract §G); **no
real-time hub data channel** — the `QuizHub` stays forfeit-on-disconnect-only (no server→client ticks; the
countdown is local + Hangfire-authoritative); the **notifications hub** seam stays deferred; **no false-hope
auto-reconnect** (a drop IS the forfeit, §A.1); **no file-upload / free-text** (quizzes are MCQ-only — ignore the
shared `FileUpload`); **no staff per-question quiz review** (the admin `QuizReviewDto` stays attempt-level — the new
key is the student's own surface only, §G); the actual **video playback / decrement / deep-link handoff** is **5C/S3**
(S5 only triggers the unlock via the engine, contract §F); profile (S6); any change to the S3 `feature-sessions`
screens beyond the single `openQuiz()` navigation edit (F8).

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the FRONTEND stream of Student-Portal phase S5 (Proctored quiz: intro · runner · results ·
answer-key review) for Salah Bahzad (Angular v20+, Nx). Edit frontend/** ONLY. The app, shell, auth, catalogue,
sessions, the student session, and the S4 assignment runner/review already exist from S0..S4 — you EXTEND the
EXISTING libs/student-portal/feature-assessment lib (S4 created it; add ONLY the quiz pieces, do NOT scaffold a new
lib) and add a QuizService to libs/student-portal/data-access.

Read first, in order:
1. frontend/CLAUDE.md (Angular v20+ conventions, tokens, icons, module boundaries).
2. docs/IMPLEMENTATION-PLAN-student-s5-frontend.md — THIS doc. DESIGN SOURCE OF TRUTH =
   .claude/Salah Bahzad Student Portal/Student Portal.html: screen 'quizIntro' (rules/time/attempts/best + the
   one-sitting warning + Start/Resume), 'quiz' (the RUNNER: an fmt(sec) countdown, question dots + prev/next, MCQ
   options [picked highlighted], a beforeunload guard + visibilitychange->quizTabSwitches counter, a Submit),
   'quizResults' (SCORE-ONLY: pass/fail mascot salah-passed/failed.png + a score ring + "This attempt"/"Best of"
   tiles + "Back to session"), and the "Leave the quiz?" modal (copy: "Leaving now forfeits this attempt and
   records a zero. It also counts as one of your limited attempts. There's no way to resume." buttons "Stay in
   quiz"/"Leave & forfeit"). The prototype has NO answer-key review — the §B REVIEW is a NEW screen, mirroring the
   admin/S4 option-state treatment (green-check correct / red wrong pick) as a VISUAL reference to RE-IMPLEMENT
   (never import an admin-portal lib).
3. docs/contracts/student-s5-quizzes.md — the FROZEN contract: §A (the FIVE REUSED engine routes — GET
   .../by-session/{sessionId} -> StudentQuizDto [intro; NO questions/correctness; activeAttemptId + each attempt
   summary now carries an additive `id`], POST .../{quizId}/attempts -> QuizAttemptDto [START: attemptId,
   deadlineUtc, serverNowUtc, drawn questions, NO isCorrect; 409 if exhausted/active], PUT
   .../attempts/{attemptId}/questions/{aqId}/answer {selectedOptionId} -> 204 [save-as-you-go], POST
   .../attempts/{attemptId}/submit -> QuizAttemptResultDto [score-only, no questions; 409 if already terminal —
   the Hangfire-timeout race], POST .../attempts/{attemptId}/focus {type:FocusLost|FocusReturned,...} -> 204
   [monitoring only, NEVER forfeits]); §A.1 (the QuizHub at /hubs/quiz — JWT via accessTokenFactory; it PUSHES
   NOTHING; its ONLY job is forfeit-on-disconnect: open it right AFTER start(), keep it open for the sitting, drop
   = forfeit score 0 — do NOT auto-reconnect implying survival); §B (the ONE NEW read GET
   /api/me/quizzes/attempts/{attemptId}/review -> StudentQuizAttemptReviewDto [the ONLY student surface exposing
   isCorrect, ONLY for the caller's OWN TERMINAL attempt], §B.2 errors: 403 quiz_attempt_in_progress [InProgress] /
   404 IDOR / 401 anon / 403 staff); §C (runner rules — informed intro, hub-open-on-start, LOCAL countdown seeded
   from deadlineUtc/serverNowUtc but SERVER-AUTHORITATIVE [the hub does NOT tick; the authoritative auto-submit is a
   server Hangfire job at deadlineUtc; on local-zero call submit, on 409 re-fetch by-session and read the TimedOut
   attempt], save-as-you-go, manual/auto submit, forfeit-by-disconnect/leave, focus-recorded-not-forfeited); §D
   (results = score-only; review = NEW screen). The live/intro shapes stay correctness-free; do NOT widen them.
4. The S2/S3/S4 code to reuse/port: libs/student-portal/data-access (CatalogueService/MySessionsService/
   AssignmentService are the pattern for QuizService — the studentAuthInterceptor attaches the bearer + refresh;
   /api/me/quizzes is AUTHENTICATED, do NOT add it to ANONYMOUS_PATHS; StudentAuthStore.getAccessToken() is the
   SignalR accessTokenFactory source; use the __SB_API_URL__ shim; add distinct live vs review interfaces, NO
   isCorrect on live); libs/student-portal/feature-sessions/.../session-detail.component.ts (its openQuiz() is the
   placeholder you replace with Router.navigate — it currently sets quizNote->"Opens in the next update."; quizMeta()
   already computes "Start attempt"/"Try again"/"Review quiz"; AND its spec is the Jest setup template); the
   app.routes.ts + tsconfig.base.json alias pattern (the feature-assessment alias already exists from S4);
   @sb/shared/ui (Button/LatexPreview/Progress[linear]/StatusPill/Tag/Alert/Modal[size confirm] — there is NO Timer
   and NO shared circular ring; build a LOCAL countdown signal + setInterval under NgZone.runOutsideAngular [the S4
   gotcha — an in-zone recurring macrotask hangs whenStable()], and RE-IMPLEMENT a small score ring in
   feature-assessment, do NOT cross-import S3's circular-progress).

Build: install @microsoft/signalr (not yet in package.json; proxy.conf.js already forwards /hubs ws:true). In
feature-assessment add (export from the barrel): QuizIntroComponent (rules/time/attempts/best + one-sitting warning;
Start when attemptsRemaining>0 && activeAttemptId==null else Resume; terminal attempt rows deep-link the §B review),
QuizRunnerComponent (start()->render drawn questions one card + dots + prev/next; pick -> answer PUT immediately;
LOCAL countdown M:SS via NgZone.runOutsideAngular; on local-zero submit, on 409 re-fetch by-session->TimedOut->
results; manual Submit -> results; open the QuizHub on start [forfeit-on-disconnect, no auto-reconnect] and tear
down on submit/leave/destroy; beforeunload guard + an in-app-leave "Leave the quiz?" confirm modal whose "Leave &
forfeit" navigates [hub teardown = forfeit]; visibilitychange/blur -> focus PUT #5, recorded NOT forfeit; reachable
only when detail.quiz!=null), QuizResultsComponent (SCORE-ONLY per prototype: pass/fail mascot + score ring + "This
attempt"/"Best of" tiles + "Back to session"; plus a "Review answers" link -> the §B review for the just-finished
attemptId), QuizReviewComponent (NEW per §B/§D: load review(attemptId); header "{sessionTitle} · Quiz review" +
"Attempt N" + score chip [scorePercent>=minPassPercent] + M:SS + flag; per-question correct green / wrong pick red /
per-question indicator; unanswered shows correct only; read-only; 403 quiz_attempt_in_progress -> finish-first +
"Continue quiz"; 404 -> /sessions/:id). Add the three routes (sessions/:id/quiz, sessions/:id/quiz/results,
sessions/:id/quiz/attempts/:attemptId/review) under the authed shell + authGuard and PROVE they resolve at :4300.
A QuizService (quiz(sessionId), start(quizId), answer(attemptId,aqId,optionId), submit(attemptId),
focus(attemptId,body), review(attemptId) — authenticated; distinct live vs review interfaces; string-union enums).
Replace S3's openQuiz(): Router.navigate to /sessions/:id/quiz (route string, not import; remove the "Opens in the
next update." note).

Jest with whenStable() (NOT fakeAsync; mock the data-access barrel via jest.mock; setup like
session-detail.component.spec.ts with TestBed.resetTestingModule + setInput; drive timer/hub/focus via test seams):
runner renders one question at a time + dots, pick calls answer() with the right aqId+optionId, local countdown
seeds from deadlineUtc/serverNowUtc and renders M:SS, local-zero calls submit() and a submit 409 re-fetches the
TimedOut attempt (no error), manual Submit -> results, the hub opens on start + closes on submit/leave, in-app
leave shows the modal and "Leave & forfeit" proceeds, visibilitychange fires focus() and does NOT forfeit, a 404
from quiz() routes back; intro Start enabled only when attemptsRemaining>0 && no active and calls start(), Resume
shows when active, terminal rows link the review; results are score-only + the "Review answers" link; review
renders the answer key (correct marked, wrong pick marked, per-question right/wrong, score+time+flag), unanswered ->
correct only, 403 -> finish-first, 404 -> back; the service hits the right paths WITH a bearer and maps the
string-union enums. Responsive (FR-STU-RWD-001/002) + a11y (FR-STU-A11Y-001). Green gate:
`npx nx build student-portal` + `nx test student-portal-feature-assessment` + the quiz.service.spec. Report all.
```
