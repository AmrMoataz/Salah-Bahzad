# Phase 5B-2 — FRONTEND stream (Quiz-attempts review + live attendance quiz columns)

> Run in its **own** Claude session, parallel with the backend stream. Created 2026-06-20.
>
> **Read first:** `frontend/CLAUDE.md` and the **frozen contract** `docs/contracts/phase5b2-quizzes.md` (`§B`).
> **This is a SMALL stream** — the screens already exist from 5B-1; you fill the **Quiz attempts** tab and light up
> the attendance **Quiz** columns. **No new lib, no new route, no quiz-taking UI** (the engine is API-only).
>
> **File ownership:** `frontend/**` only — almost entirely inside the existing **`feature-attendance`** lib.

## Goal
Replace the 5B-1 **disabled "Quiz attempts" placeholder** in `assignment-review` (`scrReview` line 1112) with the
real attempts table, and surface the now-populated **Quiz best / Attempts** attendance columns + quiz **focus-loss**
behaviour rows. Green gate: `npx nx build admin-portal` + `nx test admin-portal-feature-attendance`.

## Design source of truth
`.claude/Salah Bahzad Teacher Portal/Admin Portal.dc.html` — **`scrReview` "Quiz attempts" tab (lines 1128-1130)**:
a table *Attempt / Score / Time spent / Flags / When* with the best attempt marked; **Flags** are pills —
`Clean`→active, `Timeout`→rejected, `Forfeit`→pending. The **Behaviour log** tab (1131-1134) already renders a
timeline; quiz `FocusLost` rows ("Focus lost (tab switch)" → `x`/red) now appear in it. **`scrAttendance`
(lines 1269-1270)**: the **Quiz best** + **Attempts** columns (5B-1 rendered `—`/`0`) now show real values.

## What ALREADY exists (reuse, don't reinvent)
- **`feature-attendance`** (5B-1): `assignment-review.component.ts` already has a 3-tab shell with the **Quiz
  attempts** tab as a disabled "Available once the quiz engine ships (5B-2)" placeholder — **replace that placeholder**.
  `attendance.component.ts` already **renders** `bestQuizPercent` + `quizAttemptCount` (they were just `null/0`).
  `review.service.ts`, `attendance.presentation.ts` (icon/accent + pill helpers), the table/tabs/pill `shared/ui`.
- The attendance models already carry `bestQuizPercent` + `quizAttemptCount` — **no model change** for those.

## Steps

### B1 — Quiz-review data-access (`feature-attendance/src/lib/data-access/`)
Add to `review.service.ts` (or a small `quiz-review.service.ts`): `getQuizReview(enrollmentId): Promise<QuizReview>`
→ `GET /api/review/quizzes/{enrollmentId}`. Models (in `attendance.models.ts`): `QuizReview`, `QuizAttemptRow`
exactly per contract §B (`bestPercent, passed, minPassPercent, attemptsUsed, attemptsAllowed, attempts:[{ number,
scorePercent, timeSpentSeconds, flag, status, startedAtUtc, isBest }]`); `QuizFlag = 'Clean'|'Timeout'|'Forfeit'`.

### B2 — Fill the "Quiz attempts" tab (`assignment-review.component.ts`)
Replace the disabled placeholder: on activating the **Quiz attempts** tab, load `getQuizReview(enrollmentId)` and
render the `shared/ui` Table per `scrReview` — **Attempt** (`Attempt {number}` + a "best" marker when `isBest`),
**Score** (`{scorePercent}%`, bold), **Time spent** (`mm:ss` from `timeSpentSeconds`), **Flags** (pill via B3),
**When** (`startedAtUtc`, relative). Show a small header line "Best {bestPercent}% · {passed ? 'Passed' :
'Not passed'} (min {minPassPercent}%) · {attemptsUsed}/{attemptsAllowed} attempts". Empty-state when the gated
session has no quiz (404 → "No gating quiz for this session").

### B3 — `attendance.presentation.ts`: flag → pill
`quizFlagPill(flag)` → `{ label, variant }`: `Clean`→`success`/active, `Timeout`→`danger`/rejected,
`Forfeit`→`warning`/pending (match `scrReview` line 1129). Ensure the behaviour map has `FocusLost`→`x`/red
(and `FocusReturned`→`mustard`) so quiz focus-loss rows render in the Behaviour tab.

### B4 — Attendance quiz columns (light touch)
In `attendance.component.ts`, the **Quiz best** + **Attempts** columns already bind `bestQuizPercent`/
`quizAttemptCount`; render `bestQuizPercent` as `{n}%` (or `—` when null) and drop the 5B-1 "quiz pending" caption
(only **Videos** stays pending for 5C). No data-access change.

### B5 — Tests (Jest; `whenStable()`)
`quiz-review` service spec (GET mapping); `assignment-review.component.spec.ts` — the **Quiz attempts** tab renders
the attempts table with flag pills + the best marker, and the empty-state on 404; `attendance.component.spec.ts` —
quiz columns show real values.

## Exit criteria
The Quiz attempts tab renders against the contract; attendance quiz columns show real values; `npx nx build
admin-portal` + `nx test admin-portal-feature-attendance` green. Hand to wiring.

## Out of scope (defer)
- Any **quiz-taking** UI (start/answer/timer/forfeit) — that's the student portal; the engine is API/hub-only.
- Real **Videos watched** numbers → **5C**. Per-attempt question-level drill-in (the prototype shows only the
  attempts table) — not in 5B-2 unless the design adds it.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the FRONTEND stream of Phase 5B-2 (Quiz-attempts review + attendance quiz columns) for the
Salah Bahzad admin portal (Angular v20+, Nx). SMALL stream — the screens already exist from 5B-1; no new lib, no new
route, no quiz-taking UI.

Read first, in order:
1. frontend/CLAUDE.md (conventions; DESIGN SOURCE OF TRUTH = .claude/Salah Bahzad Teacher Portal/Admin Portal.dc.html)
2. docs/contracts/phase5b2-quizzes.md (FROZEN contract — §B)
3. docs/IMPLEMENTATION-PLAN-phase5b2-frontend.md (your step-by-step, B1–B5)

Work inside the existing feature-attendance lib. Replace the 5B-1 disabled "Quiz attempts" placeholder in
assignment-review with the real attempts table per scrReview (lines 1128-1130): Attempt (+best marker) / Score% /
Time mm:ss / Flags pill (Clean=success, Timeout=danger, Forfeit=warning) / When, loaded from
GET /api/review/quizzes/{enrollmentId}. Add a "Best X% · Passed/Not passed (min Y%) · used/allowed" header line and a
404 empty-state. Light up the attendance Quiz best / Attempts columns (already bound — bestQuizPercent/
quizAttemptCount become non-null; drop the quiz "pending" caption, keep it only for Videos/5C). Add FocusLost->x/red
to the behaviour map so quiz focus-loss rows show in the Behaviour tab. Edit frontend/** ONLY. Jest specs with
whenStable().

Green gate: `npx nx build admin-portal` + `nx test admin-portal-feature-attendance`. Report both.
```
