# Student Portal · S4 — FRONTEND stream (Assignment runner + answer-key review)

> Status: **Planned — not yet built** · Created 2026-06-21 · The **app half** of slice **S4** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S4 — Assignments). Builds the open-book **Assignment runner** +
> the **answer-key review** into a **new** `libs/student-portal/feature-assessment` lib. The S3 session-detail
> **Assignment** card currently dead-ends (it just sets a "Opens in the next update" note) — S4 fills the
> `/sessions/:id/assignment` + `/sessions/:id/assignment/review` routes it should land on.
>
> Run in its **own** Claude session, parallel-safe with the backend stream. **File ownership: `frontend/**` only.**
> Match the **frozen contract** (`docs/contracts/student-s4-assignments.md`) field-for-field — the reused engine
> shapes (`StudentAssignmentDto` / `AssignmentProgressDto`, §A), the runner interaction rules (one-at-a-time save,
> auto-grade-on-last, accumulated timer via the events `elapsedMs`, behaviour events, §C), the **new** review read
> (`GET /api/me/assignments/{assignmentId}/review` → `StudentAssignmentReviewDto`, §B) with its `403
> assignment_in_progress` / `404` boundary (§B.2), and the review-screen semantics (§D).
>
> Satisfies: `FR-STU-ASG-001..007`, `FR-PLAT-ASG-002/003/004/006/007/008`, `FR-STU-RWD-001/002` (responsive),
> `FR-STU-A11Y-001` (a11y). Green gate: `npx nx build student-portal` (AOT type-checks templates) +
> `nx test student-portal-feature-assessment`.

---

## Design source of truth (the student prototype, NOT the Teacher portal)

- **Prototype:** `.claude/Salah Bahzad Student Portal/Student Portal.html` (siblings `Dropdown.jsx`, `support.js`).
  The runner is the **`ASSIGNMENT RUNNER`** screen (`screen === 'assignment'`). S4 builds:
  - **Assignment runner** (`screen === 'assignment'`): a header **breadcrumb** "Assignment" + a title
    **"{session} — Homework"**; an **accumulated** timer counting **up** (text `m:ss` via the prototype's
    `fmt(sec)`); a shared **`Progress` bar** (variant `success`) + a **"X of Y answered"** label; a **question
    card** in a `max-width:620px` column — a green **"Question N"** label, the body text, an optional **formula
    chip**, the MCQ **A/B/C/D** options (the picked option turns **green**); a per-question **hint toggle**
    ("Show hint" / "Hide hint") that reveals the hint inline; a **"← Previous"** (disabled on the first question)
    + a primary **"Next question"** / **"Submit assignment"** (on the last question); on submit it goes **back to
    the session detail** (`go('sessionDetail')`) — **there is NO inline results screen in the runner**.
  - **Answer-key review** — the prototype has **NO** assignment-review screen (assignments submit straight back to
    the session detail). The §D review is a **NEW** student screen, built to the contract and mirroring the
    **admin** `AssignmentReviewComponent`'s question/option treatment (green-check on the correct option, red on
    the student's wrong pick, a per-question right/wrong indicator) for visual consistency — re-implemented, never
    imported (§ Conventions).
- **Tokens / assets / icons / fonts:** already mirrored into `apps/student-portal` by S0 — **reuse**, don't
  re-mirror. Mascots (`assets/salah-*.png`) are available if a friendly state needs one (e.g. the `403
  assignment_in_progress` "finish first" panel). Outline icons inline via
  `DomSanitizer.bypassSecurityTrustHtml` (the admin/S0/S2/S3 pattern; Angular strips `<svg>` from plain
  `[innerHTML]`).
- When prototype and this doc conflict, **the prototype wins** on layout/copy; **the contract wins** on the engine
  calls + state, the review DTO field names, and the `403`/`404` review boundary.

---

## Conventions (mirror `frontend/CLAUDE.md` + master plan §3.2)
- **New lib** `libs/student-portal/feature-assessment` — `project.json` tags `["scope:student-portal","type:feature"]`,
  `prefix:"sb"`, `@nx/jest` test target (byte-for-byte the shape of S2's `feature-catalogue` / S3's `feature-sessions`
  `project.json`). **You must also** add the `@sb/student-portal/feature-assessment` path alias to
  `frontend/tsconfig.base.json`, the **two** route entries to `apps/student-portal/src/app/app.routes.ts`, **and**
  export the two components from the lib barrel — an unrouted lib still builds green (the recurring "unrouted
  feature-attendance" trap from S1/5B-1 wiring); prove `/sessions/:id/assignment` + `/sessions/:id/assignment/review`
  resolve at `:4300`. **This lib will ALSO house S5's quiz pieces later — S4 builds ONLY the assignment runner +
  review.**
- **Module boundaries:** `scope:student-portal` → `scope:shared` only. Reuse `@sb/shared/ui`; **never** import an
  admin-portal lib — the admin `AssignmentReviewComponent`
  (`libs/admin-portal/feature-attendance/src/lib/assignment-review/`) is a **visual reference to RE-IMPLEMENT, not
  import**. `feature-assessment` may consume `@sb/student-portal/data-access`. `feature-sessions` navigates **to**
  the runner via a **route string** — it does **not** import `feature-assessment` (keep the boundary).
- Angular v20+: standalone, `OnPush`, signal `input()/output()/model()`, `computed()/effect()`, `inject()`, native
  control flow, typed reactive forms where needed. Cite `FR-*`/`NFR-*` in tests.
- **Reuse the shared UI:** `Button` (variants `primary`/`accent`/`secondary`/`ghost`, sizes `sm`/`md`/`lg`),
  `LatexPreview` (best-effort LaTeX — **no** KaTeX/MathJax dependency, the admin/S3 pattern — for `bodyLatex` + option
  `text`), `Progress` (linear, variant `success` — the "X of Y answered" bar), `Card`, `StatusPill`, `Alert`,
  `Modal`. **There is NO `Timer` component** — build a **local accumulated up-counter** (a signal + an interval that
  starts from `timeSpentSeconds` and ticks up, formatted `M:SS` like the prototype's `fmt(sec)`); flush the elapsed
  delta via the events `POST` `elapsedMs` on navigate/leave (F4). Keep any new presentational bits in
  `feature-assessment` (promote to `libs/student-portal/ui` only if a later phase reuses them).

> **Reuse, don't widen.** The runner DTO `StudentAssignmentDto` (§A) **forbids `isCorrect`** — the 5B-1 invariant
> and its guard test stand. The **only** correctness-bearing shape is the **distinct** `StudentAssignmentReviewDto`
> (§B). Model them as **separate** TS interfaces in data-access; do **not** add `isCorrect` to the runner interface.

---

## Steps

### F1 — Lib scaffold + routing (avoid the unrouted-lib trap)
- `nx g @nx/angular:library feature-assessment --directory=libs/student-portal/feature-assessment` (or copy
  `feature-sessions`'s `project.json`); confirm the **tags** (`scope:student-portal`/`type:feature`), `prefix:"sb"`,
  and the `@nx/jest` target. Name the test project `student-portal-feature-assessment`.
- Add `@sb/student-portal/feature-assessment → libs/student-portal/feature-assessment/src/index.ts` to
  `frontend/tsconfig.base.json`; export `AssignmentRunnerComponent` + `AssignmentReviewComponent` from the lib
  barrel.
- Add **two lazy routes** under the **authenticated shell** + `authGuard` in `app.routes.ts` (beside the S3
  `sessions/:id` entries):
  `{ path: 'sessions/:id/assignment', loadComponent: … AssignmentRunnerComponent }` and
  `{ path: 'sessions/:id/assignment/review', loadComponent: … AssignmentReviewComponent }`. Bind `:id` via the
  existing `withComponentInputBinding()` (the S3 pattern). Confirm both resolve at `:4300` (not just a green build).
  **No new shell nav item** — both screens are reached *from* the S3 session detail, not the sidebar.

### F2 — Data access: `AssignmentService` (authenticated — bearer + refresh apply)
In `libs/student-portal/data-access` (beside `CatalogueService` / `MySessionsService`), add an `AssignmentService`.
These are **authenticated** — they ride the existing `studentAuthInterceptor` (bearer attached, 401→refresh replay,
`sb_device` cookie via `withCredentials`). **Do not** add them to `ANONYMOUS_PATHS` (`['/api/auth/',
'/api/students/register','/api/reference/']` — `/api/me/assignments` is **not** exempt). Use the same `__SB_API_URL__`
shim as `MySessionsService`.
- `assignment(sessionId): Observable<StudentAssignment>` → `GET /api/me/assignments/by-session/{sessionId}` (§A #1)
  → `StudentAssignmentDto`. A `404` means the caller has no enrollment for the session → route back to
  `/sessions/{id}` (not a hard error). The re-`GET` returns **saved answers + accumulated `timeSpentSeconds`**
  (resumable).
- `answer(assignmentId, aqId, selectedOptionId): Observable<AssignmentProgress>` → `PUT
  /api/me/assignments/{assignmentId}/questions/{aqId}/answer` (§A #2), body `{ selectedOptionId }`. **It is a state
  change** — call it on each pick; answering the **last** unanswered question auto-grades server-side (no separate
  "submit"). A `409` means the assignment is already `Completed` (re-answer blocked).
- `event(assignmentId, body): Observable<void>` → `POST /api/me/assignments/{assignmentId}/events` (§A #3), body
  `{ type: 'Entered'|'Left'|'Navigated', questionOrder?, occurredAtUtc, elapsedMs? }` → `204`. **`'Answered'` is NOT
  a valid `type` here** (it's logged by the answer `PUT`).
- `review(assignmentId): Observable<StudentAssignmentReview>` → `GET /api/me/assignments/{assignmentId}/review` (§B)
  → `StudentAssignmentReviewDto`. A **`403` `assignment_in_progress`** = the caller's own but still `InProgress`
  (the deep-link edge — surface the friendly "finish first" message, §B.2); a **`404`** = unknown / another
  student's / another tenant's (route back to `/sessions`).
- **Models** — add to `libs/student-portal/data-access` (a `lib/assignments/` folder; export from the barrel):
  - `AssignmentRunStatus = 'InProgress' | 'Completed'` *(reuse the existing `AssignmentStatus` if you prefer — the
    data-access barrel already exports `AssignmentStatus` + `MyAssignmentStatus` for S3; do not duplicate the union
    name).*
  - `StudentAssignment` (§A `StudentAssignmentDto`): `{ id, sessionId, status, timeSpentSeconds, questions:
    StudentAssignmentQuestion[] }`; `StudentAssignmentQuestion`: `{ id, order, bodyLatex: string|null, imageUrl:
    string|null, hintUrl: string|null, options: StudentAssignmentOption[], selectedOptionId: string|null }`;
    `StudentAssignmentOption`: `{ id, order, text }` — **NO `isCorrect`.**
  - `AssignmentProgress` (§A `AssignmentProgressDto`): `{ answeredCount, questionCount, status }`.
  - `AssignmentEventType = 'Entered' | 'Left' | 'Navigated'`; `AssignmentEventBody`: `{ type: AssignmentEventType,
    questionOrder?: number, occurredAtUtc: string, elapsedMs?: number }`.
  - `StudentAssignmentReview` (§B.1 `StudentAssignmentReviewDto`): `{ id, sessionId, sessionTitle: string|null,
    status, correctCount, questionCount, scoreMarks, maxMarks, percent, timeSpentSeconds, completedAtUtc,
    questions: StudentReviewQuestion[] }`; `StudentReviewQuestion`: `{ id, order, bodyLatex: string|null, imageUrl:
    string|null, mark, hintUrl: string|null, options: StudentReviewOption[], selectedOptionId: string|null,
    isCorrect: boolean }`; `StudentReviewOption`: `{ id, order, text, isCorrect }` — **`isCorrect` exposed (review
    only).** Keep these **distinct** from the runner interfaces.
  - Model all enums as the contract's **string unions**; export the service + all interfaces from
    `libs/student-portal/data-access/src/index.ts`.

### F3 — `AssignmentRunnerComponent` (the runner — one-at-a-time MCQ) — `FR-STU-ASG-001..006`
A standalone `OnPush` screen at `/sessions/:id/assignment` (read the session `id` from the route via input binding):
- On init `assignment(id)` → set the assignment signal; track a **current-question index** (start at the first
  **unanswered** question, or 0 — resume-friendly, `FR-STU-ASG-002`). A `404` → route back to `/sessions/{id}`.
- **Header:** the breadcrumb "Assignment" + the title (prototype "{session} — Homework" — use the session title if
  available from the navigation/route, else a generic "Homework" — the runner DTO carries no title, so keep it
  graceful). The **accumulated timer** (F4) renders `M:SS` top-right.
- **Progress:** the shared **`Progress`** (variant `success`, value = `answeredCount / questionCount × 100`) + a
  **"{answeredCount} of {questionCount} answered"** label, both derived from the **latest
  `AssignmentProgress`** (seed `answeredCount` from the loaded questions' non-null `selectedOptionId` count, then
  update from each answer response).
- **Question card** (`max-width:620px`): the green **"Question N"** label; `bodyLatex` rendered via shared
  `LatexPreview`; the optional `imageUrl` inline; the MCQ **A/B/C/D** options (option `text` via `LatexPreview`),
  the student's `selectedOptionId` highlighted **green**. Picking an option → **`answer(assignmentId, question.id,
  optionId)`** (§A #2, the question's `{aqId}` is `question.id`) and optimistically mark it selected; re-picking a
  different option before completion re-`PUT`s (allowed). **Persist each immediately — no client-only draft batch**
  (§C).
- **Hint** (`FR-STU-ASG-004`): a per-question **"Show hint" / "Hide hint"** toggle that reveals `hintUrl` inline;
  **hide the control when `hintUrl == null`**. If `hintUrl` is a video/explainer link, open it (the runner does not
  embed a player) — otherwise render it inline.
- **Prev/next + auto-submit on last** (§C): **"← Previous"** (disabled on the first question), and a primary button
  that reads **"Next question"** on non-last questions and **"Submit assignment"** on the **last** question. The
  "Submit assignment" click is the **answer `PUT` for the last unanswered question** (which auto-grades server-side,
  §0) — after it resolves `Completed`, **navigate back to `/sessions/{id}`** (the prototype's `go('sessionDetail')`).
  **There is NO inline results screen** — the score lives in the §D review + the S3 detail card. (If the last
  question is already answered, "Submit assignment" just navigates back.)
- **Reachable when expired** (`FR-STU-SES-001`): the runner is **not** gated by the enrollment's `ExpiresAtUtc` —
  #1 still returns the assignment, #2/#3 still work. Do not block on expiry.
- **Responsive** (`FR-STU-RWD-001/002`) + **a11y** (`FR-STU-A11Y-001`): options are a labelled radio-group
  (`role="radiogroup"` + `aria-checked`); the timer is `aria-live="off"` (don't announce every tick); touch-sized
  targets; the card column collapses gracefully on phone.

### F4 — Behaviour events + the accumulated timer (§C / `FR-STU-ASG-003`, `FR-PLAT-ASG-004/005`)
Wire the timer + behaviour trail in the runner (this is the load-bearing engine glue):
- **Accumulated timer:** the displayed timer **starts from the loaded `timeSpentSeconds`** (authoritative) and ticks
  up locally (a `signal` + a `setInterval` of 1 s; render `M:SS`). On re-entry it resumes from the **new**
  `timeSpentSeconds`. Track the **elapsed delta since the last flush** so you can post it as `elapsedMs`.
- **Behaviour events** via `event(assignmentId, …)` (§A #3), each with the `elapsedMs` delta since the last post and
  an `occurredAtUtc`:
  - **`Entered`** on open (after the assignment loads).
  - **`Navigated`** (with `questionOrder` = the **target** question's `order`) on **Previous** / **Next**.
  - **`Left`** on exit / route-away (`ngOnDestroy` + a `window:beforeunload` / `document:visibilitychange`
    HostListener, the S3 pattern) — flush the remaining `elapsedMs`.
- **Flush cadence is frontend-owned** (the contract: send `elapsedMs` on each navigate and on leave) — the engine is
  the source of truth; the runner is just accruing. **Never** send `'Answered'` here (it's the answer `PUT`'s job).
- Keep the timer/flush in **test seams** (e.g. a `protected tick()` / overridable interval start) so the Jest specs
  can drive it without real wall-clock time (mirror the S3 `openExternal`/`#armInstallPrompt` seam approach).

### F5 — `AssignmentReviewComponent` (the answer-key review) — `FR-STU-ASG-007`, contract §B/§D
A standalone `OnPush` screen at `/sessions/:id/assignment/review` (read the session `id` from the route; the runner
DTO id is the `userAssignment` id, which you have from the S3 detail's `assignment.userAssignmentId` — pass it via
navigation `state` or re-derive by loading `assignment(sessionId)` then calling `review(assignment.id)`). **The
review read is keyed by `assignmentId`, not `sessionId`** (§B):
- On init, resolve the `assignmentId` then `review(assignmentId)` (§B). On **`200`** render the answer key.
- **Header** (mirrors the staff `scrReview` header, student-scoped): **"{sessionTitle} · Assignment review"** + a
  **score** (e.g. `{percent}%` / `{scoreMarks}/{maxMarks} marks` / `{correctCount} of {questionCount} correct`) +
  **time** (`M:SS` from `timeSpentSeconds`). `sessionTitle` may be `null` — fall back gracefully.
- **Per question** (§D): the body (LaTeX/image via `LatexPreview`) + each option with **three** visual states from
  per-option `isCorrect` + the question's `selectedOptionId`: the **correct** option marked (green check); the
  student's **wrong** pick marked (red, when `selectedOptionId` is set and that option's `isCorrect == false`); a
  per-question **right/wrong indicator** from the question's `isCorrect` (a `+{mark}` / `0` pill, the admin
  treatment). An **unanswered** question (`selectedOptionId == null`) shows the **correct** option only. Re-implement
  the admin `AssignmentReviewComponent`'s option-state styling (do **not** import it).
- **Read-only** — the review never re-`PUT`s an answer (the assignment is `Completed` + immutable, mirroring the quiz
  rule `FR-STU-QZ-009`).
- **Error states** (§B.2): a **`403 assignment_in_progress`** (deep-link edge — the S3 "Review assignment" CTA only
  shows when `Completed`) → a friendly **"finish first"** panel (mascot optional) rendering the server `detail`
  ("Finish the assignment to see your answers and score.") + a **"Continue assignment"** button →
  `/sessions/{id}/assignment`; a **`404`** → route back to `/sessions/{id}`. **`401`** is handled by the interceptor;
  staff **`403`** can't happen for a signed-in student.
- Responsive + a11y as F3 (the answer key is a labelled list; the correct/wrong markers carry text, not colour
  alone — `FR-STU-A11Y-001`).

### F6 — Wire S3's session-detail Assignment card → the runner / review (the ONE `feature-sessions` edit)
S3 left `SessionDetailComponent.openAssignment()` as a placeholder
(`frontend/libs/student-portal/feature-sessions/src/lib/session-detail/session-detail.component.ts`): it sets
`assignmentNote.set(true)` and renders **"Opens in the next update — your progress is saved."**, while
`assignmentMeta()` already computes the CTA **"Continue assignment"** (`InProgress`) / **"Review assignment"**
(`Completed`, with **"Completed · {scoreMarks}/{maxMarks} marks"**). **Replace `openAssignment()` to navigate:**
- `Completed` → `Router.navigate(['/sessions', id, 'assignment', 'review'])`;
- else → `Router.navigate(['/sessions', id, 'assignment'])`.

Remove the `assignmentNote` placeholder note (and its signal if now unused). This is the **only** `feature-sessions`
touch — a **route string**, not an import — keep the module boundary intact (the quiz card's "Opens in the next
update" note stays until S5).

### F7 — Tests (Jest; `whenStable()`, never `fakeAsync` — the 5B-1 jsdom gotcha)
Set up like `feature-sessions`' `session-detail.component.spec.ts` — `TestBed.resetTestingModule()` for re-setup,
`fixture.componentRef.setInput('id', …)`, **`await fixture.whenStable()`** (not `fakeAsync`), and **mock the
data-access barrel** via `jest.mock('@sb/student-portal/data-access', …)` (the ESM-fire gotcha — re-export the
service stub + any consts).
- `assignment-runner.component.spec.ts`: renders **one question at a time**; picking an option calls
  `answer()` with the **right `aqId` (= `question.id`) + `optionId`**; **Previous is disabled on the first**
  question; the **last** question's primary button reads **"Submit assignment"**, and clicking it answers the last
  question then **navigates to `/sessions/{id}`** (no inline results); the **"X of Y answered"** progress updates
  after an answer; the **hint toggle** shows/hides `hintUrl` and is **absent when `hintUrl == null`**; `Entered` on
  open, `Navigated` on prev/next, `Left` on destroy/route-away **fire** (assert `event()` called with the right
  `type` + `questionOrder`); a `404` from `assignment()` routes back to `/sessions/{id}`.
- `assignment-review.component.spec.ts`: renders the **answer key** (the **correct** option marked, the student's
  **wrong** pick marked, the per-question right/wrong indicator, the **score** + **time**); an **unanswered**
  question shows the correct option only; a **`403 assignment_in_progress`** renders the **"finish first"** panel +
  a "Continue assignment" link (and does **not** render the key); a **`404`** routes back to `/sessions/{id}`.
- `assignment.service.spec.ts`: each method hits the **right path WITH a bearer** (not exempted) — `assignment()` →
  `GET …/by-session/{id}`, `answer()` → `PUT …/{id}/questions/{aqId}/answer` with `{ selectedOptionId }`, `event()`
  → `POST …/{id}/events` with the body (and **never** `type:'Answered'`), `review()` → `GET …/{id}/review`; all map
  the **string-union enums** correctly; the `403 assignment_in_progress` and `404` flow through as `HttpErrorResponse`.

## Exit criteria
A signed-in student opens a session's **Assignment** from the S3 detail card, answers **one question at a time** with
each pick **saved immediately**, sees the **"X of Y answered"** progress climb and an **accumulated timer** that
**resumes** on re-entry, toggles a **hint** where present, and on the **last** question hits **"Submit assignment"**
— which answers the final question (auto-grading server-side) and **returns to the session detail** (no inline
results); the **behaviour trail** (`Entered`/`Navigated`/`Left`) + the accumulated time land in the engine; back on
the detail the card flips to **"Review assignment"**, which opens the **answer-key review** showing each question's
correct option, the student's pick (green/red), the per-question right/wrong, and the score + time — read-only, with
a friendly **"finish first"** state on a `403 assignment_in_progress` deep-link and a route-back on `404`; the
screens are responsive + a11y-clean on phone/tablet/desktop. `npx nx build student-portal` (AOT) +
`nx test student-portal-feature-assessment` green. Hand to wiring.

## Out of scope (defer)
**No inline assignment results screen in the runner** (the prototype submits straight to the session detail — the
graded outcome lives in the §D review + the S3 detail card, contract §F); **no engine change** — the three §A routes,
the grading math, the snapshot, the `Attendance` write, and the prerequisite gate are reused exactly as 5B-1 shipped
them (any runner-engine drift is a 5B-1 finding, not S4's, contract §G); **no file-upload / free-text answers**
(assignments are MCQ-only — ignore the shared `FileUpload`, contract §F); the **proctored quiz runner / results / the
`QuizHub`** (S5 — the same `feature-assessment` lib, a separate slice; the S3 session-detail **Quiz** card stays a
placeholder until then); profile (S6); any change to the S3 `feature-sessions` screens beyond the single
`openAssignment()` navigation edit (F6).

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the FRONTEND stream of Student-Portal phase S4 (Assignment runner + answer-key review) for
Salah Bahzad (Angular v20+, Nx). Edit frontend/** ONLY. The app, shell, auth, catalogue, sessions, and student
session already exist from S0/S1/S2/S3 — you add a NEW libs/student-portal/feature-assessment lib (which will ALSO
house S5's quizzes later — build ONLY the assignment pieces now).

Read first, in order:
1. frontend/CLAUDE.md (Angular v20+ conventions, tokens, icons, module boundaries).
2. docs/IMPLEMENTATION-PLAN-student-s4-frontend.md — THIS doc. DESIGN SOURCE OF TRUTH =
   .claude/Salah Bahzad Student Portal/Student Portal.html, screen === 'assignment' (the ASSIGNMENT RUNNER:
   breadcrumb "Assignment" + "{session} — Homework" title, an accumulated UP timer m:ss via fmt(sec), a success
   Progress bar + "X of Y answered", a question card with "Question N" + body + formula chip + A/B/C/D options
   [picked=green] + a "Show hint"/"Hide hint" toggle, "← Previous" [disabled on first] + "Next question"/"Submit
   assignment", submit -> back to session detail, NO inline results). The prototype has NO review screen — the
   answer-key REVIEW is a NEW screen, mirroring the ADMIN AssignmentReviewComponent's option treatment (green-check
   correct / red wrong pick) as a VISUAL reference to RE-IMPLEMENT (never import an admin-portal lib).
3. docs/contracts/student-s4-assignments.md — the FROZEN contract: §A (the THREE reused engine routes — GET
   .../by-session/{sessionId} -> StudentAssignmentDto [NO isCorrect; resumable; saved answers + accumulated
   timeSpentSeconds], PUT .../{id}/questions/{aqId}/answer {selectedOptionId} -> AssignmentProgressDto [answering the
   LAST unanswered question AUTO-GRADES; there is NO separate "submit"; re-answer-after-Completed -> 409], POST
   .../{id}/events {type: Entered|Left|Navigated, questionOrder?, occurredAtUtc, elapsedMs?} -> 204 ["Answered" is
   NOT valid here]); §B (the ONE NEW read GET /api/me/assignments/{assignmentId}/review -> StudentAssignmentReviewDto
   [the ONLY student surface exposing isCorrect, ONLY for the caller's OWN Completed assignment], with §B.2 errors:
   403 assignment_in_progress / 404 IDOR / 401 anon / 403 staff); §C (runner interaction rules — one-at-a-time save,
   auto-grade-on-last, accumulated timer via the events elapsedMs, behaviour events, reachable-when-expired); §D
   (review-screen semantics). The runner DTO stays correctness-free; do NOT widen it.
4. The S2/S3 code to reuse/port: libs/student-portal/data-access (CatalogueService + MySessionsService are the
   pattern for AssignmentService — the studentAuthInterceptor attaches the bearer + refresh; these reads/writes are
   AUTHENTICATED, do NOT add /api/me/assignments to ANONYMOUS_PATHS; the barrel already exports MyAssignmentStatus +
   AssignmentStatus — reuse the union, add the runner/review interfaces); libs/student-portal/feature-sessions/.../
   session-detail.component.ts (its openAssignment() is the placeholder you replace with Router.navigate, AND its
   spec is the Jest setup template); the app.routes.ts + tsconfig.base.json alias pattern; @sb/shared/ui
   (Button/LatexPreview/Progress[variant success]/Card/StatusPill/Alert/Modal — there is NO Timer, build a local
   accumulated up-counter). The admin AssignmentReviewComponent in libs/admin-portal/feature-attendance is a VISUAL
   reference to RE-IMPLEMENT, NOT import.

Build: scaffold libs/student-portal/feature-assessment (tags scope:student-portal/type:feature, prefix sb, @nx/jest)
AND wire its tsconfig alias + the /sessions/:id/assignment and /sessions/:id/assignment/review routes under the
authenticated shell (an unrouted lib still builds green — prove both resolve at :4300). An AssignmentService
(assignment(sessionId), answer(assignmentId, aqId, selectedOptionId), event(assignmentId, body), review(assignmentId)
— authenticated; distinct runner vs review interfaces, NO isCorrect on the runner). An AssignmentRunnerComponent
(one-question-at-a-time MCQ with LaTeX/image, picked=green, success Progress + "X of Y answered", per-question hint
toggle [hidden when hintUrl null], accumulated UP timer resuming from timeSpentSeconds, "← Previous" [disabled on
first] + "Next question"/"Submit assignment"; each pick -> answer PUT immediately [no draft]; the last question's
Submit answers it [auto-grade] then navigates to /sessions/:id [NO inline results]; Entered/Navigated/Left events
with elapsedMs deltas; reachable when EXPIRED). An AssignmentReviewComponent (NEW screen: "{sessionTitle} · Assignment
review" + score + M:SS; per-question correct option green-check / wrong pick red / per-question right-wrong;
read-only; 403 assignment_in_progress -> friendly "finish first" + Continue button; 404 -> back to /sessions/:id).
Replace S3's SessionDetailComponent.openAssignment(): Completed -> /sessions/:id/assignment/review, else ->
/sessions/:id/assignment (route string, not import; remove the "Opens in the next update" note).

Jest with whenStable() (NOT fakeAsync; mock the data-access barrel via jest.mock; setup like
session-detail.component.spec.ts with TestBed.resetTestingModule + setInput): runner renders one question at a time,
picking calls answer() with the right aqId+optionId, prev disabled on first, last shows "Submit assignment" and
submitting answers the last + navigates to /sessions/:id, "X of Y answered" updates, hint toggle shows/hides hintUrl,
Entered/Navigated/Left fire; review renders the answer key (correct marked, wrong pick marked, per-question
right/wrong, score+time), 403 -> finish-first, 404 -> back; the service hits the right paths WITH a bearer and maps
the string-union enums. Responsive (FR-STU-RWD-001/002) + a11y (FR-STU-A11Y-001). Green gate:
`npx nx build student-portal` + `nx test student-portal-feature-assessment`. Report both.
```
