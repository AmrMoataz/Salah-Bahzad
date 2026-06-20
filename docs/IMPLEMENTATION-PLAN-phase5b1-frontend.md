# Phase 5B-1 — FRONTEND stream (Attendance + Assignment/Behaviour review)

> Run in its **own** Claude session, parallel with the backend stream. Created 2026-06-20.
>
> **Read first:** `frontend/CLAUDE.md` (Angular v20+ conventions, design source of truth, tokens, icons) and the
> **frozen contract** `docs/contracts/phase5b1-assignments-attendance.md` (`§B` attendance, `§C` review).
> **Templates to mirror:** the Phase-5A `feature-audit`/`feature-dashboard` libs (signal service, table, presentation
> map, role gate) and the Phase-4 `feature-codes` CSV-export-download pattern.
>
> **File ownership:** `frontend/**` only. Match the frozen contract field-for-field. **The engine (`§A`) is
> backend-only — build NO student-solving UI.**

## Goal
Build the two admin screens from the prototype: **Attendance & reporting** (`scrAttendance`, line 1255) and the
**Assignment/Behaviour review** (`scrReview`, line 1112 — its **Assignment** + **Behaviour log** tabs; the **Quiz
attempts** tab is a disabled "coming in 5B-2" placeholder). Green gate: `npx nx build admin-portal` +
`nx test admin-portal-feature-attendance`.

## Design source of truth
`.claude/Salah Bahzad Teacher Portal/Admin Portal.dc.html` — **`scrAttendance` (1255)** and **`scrReview` (1112)**.
Match the tabs, columns, combos, the cohort-matrix progress bars, the review question-card correct/picked
highlighting, the score/time header, and the behaviour timeline. Tokens from
`apps/admin-portal/src/styles/_design-tokens.scss`; inline outline `<svg>` via `DomSanitizer` (see 5A).

## What ALREADY exists (reuse, don't reinvent)
- **`feature-audit`/`feature-dashboard` (5A)** — the exact patterns: signal-backed `@Injectable` service
  (`inject(HttpClient)`, `firstValueFrom`, `#api()`, ProblemDetails `detail`), `shared/ui` Table/Tabs/Card/Button/
  Select/Progress/EmptyState, `AuthStore` role gating, a `*.presentation.ts` for icon/accent/label maps.
- **`feature-codes` CSV download** (`code.service.ts` `export()`): GET `responseType:'blob'`, honour
  `Content-Disposition` — reuse verbatim for the attendance CSV exports (`§B #6`).
- **Phase-3 dependency-free LaTeX preview** (shared) — reuse to render `bodyLatex` in the review question cards.
- The dashboard's **"Open attendance"** quick action already routes to `/attendance` (it currently bounces via the
  `**` wildcard — **this slice makes it resolve**, closing the 5A pre-flight note).

## Steps

### B1 — New lib `feature-attendance`
`npx nx g @nx/angular:library admin-portal-feature-attendance --directory=libs/admin-portal/feature-attendance
--standalone --style=scss` (copy `feature-audit/project.json` tags + `test-setup.ts`). Hosts both route components
(attendance matrix + assignment review) and their data-access.

### B2 — Data-access (`feature-attendance/src/lib/data-access/`)
- `attendance.models.ts` — `SessionAttendanceRow`, `StudentAttendanceRow`, `PagedResult<T>` (contract §B); plus the
  review shapes `AssignmentReview`, `ReviewQuestion`, `ReviewOption`, `BehaviourEvent` (contract §C). Field names
  exactly per contract.
- `attendance.service.ts` — `listBySession(sessionId, page)`, `listByStudent(studentId, page)`,
  `exportSession(sessionId)` / `exportStudent(studentId)` (blob download, mirror `code.service.ts#export`).
- `review.service.ts` — `getReview(enrollmentId)`, `getBehaviour(enrollmentId)`.
- Session/student reference lists for the combos: read `/api/sessions` and `/api/students` directly (stay within
  the Nx boundary — no `feature-sessions`/`feature-students` import; mirror how `code.service.ts` loads sessions).

### B3 — Attendance screen (`attendance/attendance.component.ts`) — match `scrAttendance`
- `pageHead('Attendance & reporting', 'Cross-student progress across videos, assignments & quizzes', <Export>)`.
- **Tabs** (`shared/ui` Tabs): **By session** / **By student**.
- **By session:** a session **combo-select** → cohort matrix table (`shared/ui` Table): **Student** (avatar+name) ·
  **Videos watched** (`Progress` bar + `watched/total`) · **Assignment** (`assignmentPercent%` or `—`) · **Quiz best**
  (`bestQuizPercent%` or `—`) · **Attempts** (`quizAttemptCount`) · **"Drill in"** → `/review/{enrollmentId}`.
- **By student:** a student combo → per-session breakdown (Session · Videos · Quiz best · **"Review"** →
  `/review/{enrollmentId}`).
- **Export** button → `attendance.service.export*()` for the active tab's selection (CSV download).
- Render the **Videos** and **Quiz best/Attempts** columns but show `0/total` and `—`/`0` (5C/5B-2 pending) — a tiny
  caption/tooltip "populated when video + quiz tracking ship" is a nice touch.
- Gate the whole screen on `AuthStore.hasPermission('AttendanceRead')`.

### B4 — Assignment review screen (`assignment-review/assignment-review.component.ts`) — match `scrReview`
- `backLink('Back', '/attendance')`; **header**: student avatar + name, subtitle `{sessionTitle} · Assignment
  review`, and two big stats — **Score** `{correctCount}/{questionCount}` and **Time spent** `mm:ss`(from
  `timeSpentSeconds`).
- **Tabs**: **Assignment** · **Quiz attempts** *(disabled — empty-state "Available once the quiz engine ships
  (5B-2)")* · **Behaviour log**.
- **Assignment tab:** one `Card` per question — `Q{order}. {bodyLatex}` (LaTeX preview; `imageUrl` if image) + a
  pill (`+{mark}` `success` when `isCorrect`, else `0` `danger`); an options grid where the **correct** option is
  green+check, a **picked-but-wrong** option is red+×, others neutral (match `scrReview` lines 1126-1127).
- **Behaviour tab:** a timeline `Card` from `getBehaviour()` — each row an icon circle + `label` + mono timestamp
  (`Entered`→`logout`/green, `Answered`→`check`/blue, `Left`→`x`/red, `Navigated`→`mustard`; match lines 1131-1134).
- `attendance.presentation.ts` — behaviour `type→{icon,accent,label}`, `percentOrDash`, `mmss(seconds)`, option-state
  → token classes. Pure functions (mirror `audit.presentation.ts`).

### B5 — Shell wiring (`apps/admin-portal/src/app/app.routes.ts` + `feature-shell`)
- Add routes: `attendance` → `AttendanceComponent` (`canActivate: [permissionGuard('AttendanceRead')]`);
  `review/:enrollmentId` → `AssignmentReviewComponent` (`AttendanceRead`).
- Add the **"Attendance"** sidebar nav item (prototype nav line 1414, icon `clipboard`), gated `AttendanceRead`.
- Confirm the dashboard "Open attendance" quick action now resolves to the real screen.

### B6 — Tests (Jest; `whenStable()`, not `fakeAsync`)
- `attendance.service.spec.ts` — param building + blob-export request; `review.service.spec.ts` — GET mapping.
- `attendance.component.spec.ts` — renders the session matrix, switches tabs, "Drill in" navigates to
  `/review/{enrollmentId}` (mock Router), hides for non-`AttendanceRead`.
- `assignment-review.component.spec.ts` — renders the score header, question cards with correct/picked highlighting,
  the disabled Quiz tab, and the behaviour timeline.

## Exit criteria
Both screens render against the contract shapes; `npx nx build admin-portal` (AOT) green; `nx test
admin-portal-feature-attendance` green; "Open attendance" + "Drill in"/"Review" navigation works. Hand to wiring.

## Out of scope (defer)
- **Quiz attempts** review tab + quiz/attempts columns data → **5B-2** (rendered disabled now). Real **Videos
  watched** numbers → **5C** (rendered 0/total now). Any **student-solving UI** — engine is API-only.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the FRONTEND stream of Phase 5B-1 (Attendance + Assignment/Behaviour review) for the Salah
Bahzad admin portal (Angular v20+, Nx). Admin screens consuming an existing API — NO student-solving UI.

Read first, in order:
1. frontend/CLAUDE.md (conventions; DESIGN SOURCE OF TRUTH = .claude/Salah Bahzad Teacher Portal/Admin Portal.dc.html)
2. docs/contracts/phase5b1-assignments-attendance.md (FROZEN contract — §B attendance, §C review)
3. docs/IMPLEMENTATION-PLAN-phase5b1-frontend.md (your step-by-step, B1–B6)

BUILD THE PROTOTYPE'S SCREENS: scrAttendance (line 1255) — tabs By session / By student, cohort matrix with a video
progress bar + Assignment% + Quiz best + Attempts + Drill-in, and CSV Export; and scrReview (line 1112) — back-link,
student header with Score (correct/total) + Time spent, tabs Assignment (question cards: correct option green/check,
picked-wrong red/×, +mark pill) / Quiz attempts (DISABLED placeholder, 5B-2) / Behaviour log (icon timeline). In
5B-1 the Videos column is 0/total and Quiz columns are —/0 (5C/5B-2 pending) — render them as pending.

Mirror the Phase-5A feature-audit/feature-dashboard libs (signal service, presentation map, role gate) and the
feature-codes CSV blob-download. Reuse the Phase-3 dependency-free LaTeX preview for question bodies. Edit
frontend/** ONLY.

Deliver a NEW lib feature-attendance (attendance.service + review.service + models, attendance component, assignment-
review component, attendance.presentation), and wire routes /attendance + /review/:enrollmentId + the "Attendance"
nav item (gated AttendanceRead) — which also makes the dashboard's "Open attendance" quick action resolve. Jest
specs with whenStable().

Green gate: `npx nx build admin-portal` + `nx test admin-portal-feature-attendance`. Report both.
```
