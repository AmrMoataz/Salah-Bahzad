# Student Portal · Home — FRONTEND stream (personalized weekly study plan)

> Status: **Planned — not yet built** · Created 2026-06-21 · The **lead stream** of the net-new **Home** slice (beyond the
> master plan's S0–S6 — the master treats "Home" as the *catalogue*; this adds a *personalized* Home). Builds the
> **HomeComponent** (hero + 4 KPI cards + "Your plan" list + "This week" bar + "Recently enrolled" rail + mascot empty-
> state) into a **new** `libs/student-portal/feature-home` lib, replacing `HomePlaceholderComponent` at the shell's empty
> route. The S0 shell already renders the guarded layout and the **Home** nav item is already enabled (`shell.component.ts`
> lines 30-43) — this stream fills the `''` route it lands on.
>
> Home is **FE-led**: every datum is composed server-side into one read. This stream runs in its **own** Claude session,
> parallel-safe with the backend stream. **File ownership: `frontend/**` only.** Match the **frozen contract**
> (`docs/contracts/student-home-weekly-plan.md`) field-for-field — the `MyPlanDto` / `MyPlanStepDto` / `MyPlanRecentDto` /
> `kpis` / `focus` shapes (§A.1), the four enums (§B), the always-200 / empty-plan semantics (§A.2), and the algorithm's
> *rendered* output (§E). **Never redefine a field shape — point to the contract.**
>
> Satisfies: `FR-STU-SES-001` (progress + the one real deadline), `FR-PLAT-ENR-003/-007` (validity window + prereq gate),
> `FR-PLAT-QZ-008` / `FR-STU-QZ-010` (gating quiz unlocks the same session's videos), `FR-STU-CAT-003` (enrollment is
> **code-only** → `Redeem` steps), `FR-STU-RWD-001` (responsive), `NFR-A11Y-001` (keyboard/AT). Green gate:
> `npx nx build student-portal` (AOT type-checks templates) + `nx test student-portal-feature-home`.

---

## Design source of truth (the Student Portal prototype + DS tokens)

- **Prototype:** `.claude/Salah Bahzad Student Portal/Student Portal.html` (siblings `Dropdown.jsx`, `support.js`). Home
  reconciles the proposed mock to the platform's real capabilities (contract §0). The mock's editable checkboxes,
  per-task "Due in 3d/5d/7d" badges, and "Enroll/Renew" buttons are **dropped** (contract §0 / §G) — render **read-only**
  completion ticks, `dueState` badges from **enrollment expiry only**, and `Redeem` (never Enroll/Renew).
- **Tokens / assets / icons / fonts:** already mirrored into `apps/student-portal` by S0 — **reuse**, don't re-mirror. Use
  the canonical `--sb-*` token names (`frontend/CLAUDE.md`). Mascots (`assets/salah-*.png`): `salah-relaxing.png` (empty
  Home / all-caught-up). Outline icons (24×24, ~1.8px, `stroke="currentColor"`) inline via
  `DomSanitizer.bypassSecurityTrustHtml` (the admin/S0/S2 pattern — Angular strips `<svg>` from plain `[innerHTML]`).
- **The KPI cards copy the admin dashboard's stat-card by REPLICATION, not import** (see "KPI cards" below): the visual is
  `.db-stat` / `.db-stat__top|__label|__icon|__value` + `accentBg`/`accentFg` + the inline-svg icon helper in
  `frontend/libs/admin-portal/feature-dashboard/src/lib/dashboard.presentation.ts` (`dashIconSvg`, lines 54-67) and
  `dashboard.component.ts` (markup lines 74-93, styles 180-191). Features don't import features in this repo — duplicate
  the tiny map (the dashboard itself duplicates `feature-audit`'s — `dashboard.presentation.ts` lines 1-10).
- When prototype and this doc conflict, **the prototype wins** on layout/copy; **the contract wins** on field names,
  enums, the empty/expired/all-done shapes, and the read-only / no-fabricated-date stance.

---

## Conventions (mirror `frontend/CLAUDE.md` + master plan §3.2)

- **New lib** `libs/student-portal/feature-home` — `project.json` tags `["scope:student-portal","type:feature"]`,
  `prefix:"sb"`, `@nx/jest` test target (byte-for-byte the shape of `feature-catalogue/project.json`). **You must also**
  add the `@sb/student-portal/feature-home` path alias to `frontend/tsconfig.base.json`, swap the `''` child route in
  `apps/student-portal/src/app/app.routes.ts` to lazy-load `HomeComponent` (replacing `HomePlaceholderComponent`), and
  keep/confirm the shell's **Home** nav item enabled (`shell.component.ts` line 32 — already enabled). An unrouted lib
  still builds green (the S1-wiring "unrouted feature-attendance" gotcha) — prove `:4300/` resolves to the real Home.
- **Module boundaries:** `scope:student-portal` → `scope:shared` only. Reuse `@sb/shared/ui`; **never** import an
  admin-portal lib (hence KPI replication). `feature-home` consumes `@sb/student-portal/data-access`; it must **not**
  import `feature-catalogue` / `feature-sessions` — it **routes** to `/sessions/{id}` and `/redeem` (route strings).
- Angular v20+: standalone, `OnPush`, signal `input()/output()/model()`, `computed()/effect()`, `inject()`, native
  control flow (`@if`/`@for`/`@switch`). Cite `FR-*`/`NFR-*` in tests.
- **Reuse the shared UI:** `Button` (+ variants), `Tag`/`StatusPill`/`Chip` (for `dueState` badges), `Progress` (linear —
  the "This week" bar), `Card`, `EmptyState`. Keep Home-specific presentational bits (the plan row, the KPI card, the
  recent rail tile) **local** to `feature-home`.
- **Greeting + relative time are frontend-owned** (contract §0 / §E.5): the DTO carries **no** PII beyond session titles.
  Greeting first name from `StudentAuthStore.firstName` (`student-auth.store.ts` line 69). "Expires in N days" comes from
  the DTO (`focus.expiresInDays` / `step.expiresAtUtc`); "Added N days ago" is computed client-side from
  `recentlyEnrolled[].enrolledAtUtc` (replicate the small `relativeTime`/day-diff helper, do not import the dashboard's).

---

## Frozen vs. frontend-owned (point to the contract; never restate field shapes)

- **Frozen (cite, never redefine — `docs/contracts/student-home-weekly-plan.md`):** the `GET /api/me/plan` path +
  `RequireStudent` + **no params** + **always-200** (incl. the empty plan — never 404) (§A); the `MyPlanDto` /
  `MyPlanStepDto` / `MyPlanRecentDto` / `kpis` / `focus` field names + types (§A.1); the four enums `MyPlanStepKind` /
  `MyPlanStepStatus` / `MyPlanDueState` / `MyPlanActionType` (§B); the read is **not audited** (§F); the
  empty/expired-only/all-done shapes (§E.4); the deferred/not-built stance (§G). The plan is **derived state** the UI
  renders read-only — no editable checkboxes, no fabricated dates (§0).
- **Frontend owns (this doc):** the `feature-home` lib + the `''`-route swap; the `PlanService` in `data-access`; the
  hero (greeting + "You have N tasks — M overdue" from `totalSteps`/`overdueSteps` + Redeem/Browse CTAs); the 4 KPI cards
  (replicated dashboard stat-card); the "Your plan" list (Pending + collapsed Completed sub-list, read-only ticks,
  `blocked` disabled rows + `blockedReason`, `dueState` badges incl. "expires in N days", per-`kind` CTA → `action.route`
  Navigate or `/redeem` Redeem); the "This week" bar (`completedSteps/totalSteps`); the "Recently enrolled" rail ("Added
  N days ago" client-side); the mascot empty-state; full responsiveness + WCAG keyboard/AT; the Jest specs.
- **Backend owns:** the `GET /api/me/plan` handler, the DTO mapping, the Redis cache + invalidation. **Wiring owns:**
  proving the focus + gate-ordered steps + KPIs + cache invalidation live across the matrix.

---

## Steps

### F1 — Lib scaffold + route swap (avoid the unrouted-lib trap)
- `nx g @nx/angular:library feature-home --directory=libs/student-portal/feature-home` (or copy
  `feature-catalogue/project.json`); confirm the **tags** `["scope:student-portal","type:feature"]`, `prefix:"sb"`, and
  the `@nx/jest` `test` target. Export `HomeComponent` from `libs/student-portal/feature-home/src/index.ts`.
- Add `@sb/student-portal/feature-home → libs/student-portal/feature-home/src/index.ts` to `frontend/tsconfig.base.json`.
- In `apps/student-portal/src/app/app.routes.ts`, **replace** the `''` child's `loadComponent` (currently
  `./placeholders/home-placeholder.component … HomePlaceholderComponent`, lines 31-36) with
  `import('@sb/student-portal/feature-home').then((m) => m.HomeComponent)`. Leave the `authGuard` on the parent shell
  route untouched. The shell **Home** nav item is already enabled (`shell.component.ts` line 32) and `ROUTE_META['']`
  already maps to `['Welcome','Home']` (line 47) — no shell change required; confirm both.
- Delete `apps/student-portal/src/app/placeholders/home-placeholder.component.ts` (now dead) **only after** the swap
  builds — or leave it unreferenced (build stays green either way; prefer deleting to avoid drift).
- Prove `:4300/` renders the real Home (not just a green build).

### F2 — Data access: `PlanService` (authenticated — bearer + refresh apply)
In `libs/student-portal/data-access` (beside `CatalogueService` / `MySessionsService`), add a `PlanService`. It is
**authenticated** — it rides the existing `studentAuthInterceptor` (bearer attached, 401→refresh-replay, `sb_device`
cookie via `withCredentials`). **Do not** add `/api/me/plan` to `ANONYMOUS_PATHS`. Mirror the `#apiUrl()` window-shim
pattern from `my-sessions.service.ts` (lines 72-75).
- `plan(): Observable<MyPlanDto>` → `GET /api/me/plan` (**no query params** — contract §A) → `MyPlanDto` (§A.1). The wire
  shape equals the model field-for-field, so the response **is** the model. Always `200`; a `401`/`403` is an interceptor
  concern, not a Home concern.
- Add `plan/plan.models.ts` modelling the DTOs as TS interfaces with the contract's **string-union** enums — keep names
  identical to §A.1 / §B:
  - `type MyPlanStepKind = 'Quiz' | 'Videos' | 'Assignment' | 'Redeem';` (§B)
  - `type MyPlanStepStatus = 'Pending' | 'Completed';` (no `Overdue` — urgency rides `dueState`)
  - `type MyPlanDueState = 'None' | 'ExpiringSoon' | 'Expired';`
  - `type MyPlanActionType = 'Navigate' | 'Redeem';`
  - `MyPlanDto` (`isoWeek`, `weekStartUtc`, `weekEndUtc`, `generatedAtUtc`, `totalSteps`, `completedSteps`,
    `overdueSteps`, `kpis`, `focus: MyPlanFocus | null`, `steps: MyPlanStep[]`, `recentlyEnrolled: MyPlanRecent[]`),
    `MyPlanKpis` (`activeSessions`, `videosWatched`, `videosTotal`, `overallProgressPercent`, `completedSessions`),
    `MyPlanFocus` (`sessionId`, `title`, `specializationName`, `thumbnailUrl`, `progressPercent`, `expiresAtUtc`,
    `isExpired`, `expiresInDays: number | null`, `dueState`), `MyPlanStep` (per §A.1 — `key`, `kind`, `title`, `subtitle`,
    `sessionId`, `sessionTitle`, `specializationName`, `status`, `blocked`, `blockedReason`, `dueState`, `expiresAtUtc`,
    `progress: { done: number; total: number } | null`, `action: { type; route: string | null; label: string }`),
    `MyPlanRecent` (`sessionId`, `title`, `specializationName`, `enrolledAtUtc`).
- Export `PlanService` + all the model types from the data-access **barrel** (`data-access/src/index.ts` — the existing
  `MySessionsService` / `CatalogueService` export block is the template). **Do not add fields the API does not send.**

### F3 — `HomeComponent` (the personalized Home) — `FR-STU-SES-001`
A standalone `OnPush` screen at the shell's `''` route. On init call `plan()` → render. Hold the result in a signal;
derive everything else with `computed()`. Layout (top → bottom), all from `MyPlanDto`:

**1) Hero band** (replaces the placeholder hero, `home-placeholder.component.ts` lines 15-26 is the visual reference):
- Greeting: `Welcome back{firstName ? ', ' + firstName : ''}!` from `StudentAuthStore.firstName`.
- Headline counts line: `You have {totalSteps} task(s){overdueSteps ? ' — ' + overdueSteps + ' overdue' : ''}` from
  `totalSteps` / `overdueSteps` (§A.1). When `totalSteps === 0` → a congratulatory "You're all caught up" lede + the
  `salah-relaxing.png` mascot (the all-done / onboarding state — §E.4).
- CTAs: **Redeem a code** → `routerLink="/redeem"` (the shell's redeem target — always present, code-only enrollment,
  `FR-STU-CAT-003`); **Browse the catalogue** → `routerLink="/catalogue"` (prominent in the empty/onboarding state).

**2) The 4 KPI cards** — from `kpis` (§A.1), in this fixed order, **replicating** the dashboard stat-card visual:
| Card | Value | Icon (dashboard `DashIconName`) | Accent (`FeedAccent`) |
|---|---|---|---|
| Active sessions | `kpis.activeSessions` | `book` | `purple` |
| Videos watched | `{kpis.videosWatched} / {kpis.videosTotal}` | `eye` | `blue` |
| Overall progress | `{kpis.overallProgressPercent}%` | `check` | `green` |
| Completed sessions | `kpis.completedSessions` | `unlock` | `mustard` |
- Replicate `dashIconSvg(name,size)` (presentation.ts lines 54-60) + `accentBg`/`accentFg` (lines 62-67) + the `.db-stat`
  markup/styles **locally** in `feature-home` (rename to `sb-home-kpi` / `home-presentation.ts`) — **no** feature→feature
  import. Render the icon via `DomSanitizer.bypassSecurityTrustHtml`. These KPI cards are **not** clickable (no `route`) —
  drop the dashboard's `--clickable`/`onStatClick` affordance.

**3) "Your plan" list** — from `steps` (§A.1, length ≤ 7), the heart of the screen — `FR-STU-SES-001`:
- Split client-side into **Pending** (`status === 'Pending'`) and **Completed** (`status === 'Completed'`). Render Pending
  inline; render Completed inside a **collapsed `<details>`/disclosure** sub-list ("Completed (N)") so it doesn't crowd.
- Each row (a local `HomePlanRow` presentational piece) shows, from the step DTO (**never** invent):
  - a **read-only completion tick** — checked when `status === 'Completed'`, an empty circle otherwise. This is a
    *rendered state*, **not** an editable checkbox (contract §0) — use a non-interactive icon / `aria-hidden` glyph, never
    an `<input type=checkbox>` the student can toggle.
  - `title` (server-composed) + `subtitle` (may be null — `@if`); a per-row accent chip from `specializationName` (same
    accent system as the KPI cards / catalogue).
  - a `kind` glyph (Quiz / Videos / Assignment / Redeem) via the local icon helper.
  - `progress` (when non-null): a compact `{done}/{total}` label or a tiny inline bar (Videos/Assignment only — null for
    Quiz/Redeem, §A.1).
  - a **`dueState` badge** (shared `Chip`/`Tag`): `Expired` → `danger` variant + "Expired"; `ExpiringSoon` → `warning`
    variant + **"Expires in N days"** computed from `step.expiresAtUtc` (client-side day-diff) — the **only** time
    pressure rendered (contract §0 / §E.3); `None` → no badge.
  - **blocked rows** (`blocked === true`, e.g. Videos blocked behind an unpassed gating quiz): render the row **disabled /
    dimmed**, the CTA inert (`disabled` + `aria-disabled`), and show `blockedReason` ("Pass the quiz to unlock the
    videos") as helper text. Never hide a blocked row — surface why.
  - **per-`kind` CTA** driven by `action` (§A.1): `action.type === 'Navigate'` → a router link to `action.route`
    (e.g. `/sessions/{sessionId}`) with `action.label` ("Start"/"Continue"/"Watch"/"Open"); `action.type === 'Redeem'` →
    a button → `routerLink="/redeem"` (no route in the DTO — `action.route` is null), label "Redeem". The CTA label is
    **server-supplied** — render it verbatim, don't recompute.
- Section header "Your plan" + subhead matching the prototype.

**4) "This week" bar** — a shared linear **`Progress`** with value = `totalSteps === 0 ? 0 : round(100 × completedSteps /
totalSteps)`, plus a "{completedSteps} of {totalSteps} done this week" caption and the `isoWeek` label (from §A.1). Mirror
the contract's "This week" % definition (§A.1 `completedSteps`).

**5) "Recently enrolled" rail** — from `recentlyEnrolled` (≤ 5, already `EnrolledAtUtc` DESC — §E.5): a horizontal rail of
compact tiles, each = title + specialization accent chip + **"Added N days ago"** computed client-side from
`enrolledAtUtc` (local day-diff helper). Tapping a tile routes to `/sessions/{sessionId}`. Hide the whole section when the
array is empty.

**6) Mascot empty-state** — when `focus === null && steps.length === 0` (or the single onboarding `Redeem` step, §E.4):
`salah-relaxing.png` + "Redeem a code to unlock your first session" + the **Browse the catalogue** CTA. (When `steps`
contains only a `Redeem` onboarding step per §E.4, render it through the normal plan-row path — the Redeem CTA already
points at `/redeem`.)

- **Loading / error:** show a lightweight skeleton or spinner while `plan()` is in flight; a hard failure (rare —
  endpoint is always-200) → a friendly retry, not a crash.
- **Responsive** (`FR-STU-RWD-001`): KPI cards `repeat(auto-fit, minmax(…))` like the dashboard; the plan list and rail
  collapse to single-column / horizontal-scroll on phone; touch-sized targets; works inside the shell's mobile padding
  (`shell.component.ts` lines 117-119).
- **A11y** (`NFR-A11Y-001`): the plan list is a labelled `<ul>`/`role="list"`; completion ticks carry an `aria-label`
  ("Completed" / "Not done") and are **not** focusable controls (read-only); blocked CTAs are `aria-disabled` with the
  reason associated via `aria-describedby`; the Completed disclosure is a native `<details>` (keyboard-operable);
  `dueState` badges have text, not colour-only meaning; the KPI icons are decorative (`aria-hidden`) with the label as the
  accessible name.

### F4 — Local presentational pieces + helpers (`feature-home`-local)
- **`HomeKpiCard`** — the replicated stat-card (label + accent icon chip + value); inputs: label, value, icon name,
  accent. Backed by a local `home-presentation.ts` carrying the replicated `homeIconSvg` + `accentBg`/`accentFg` + the
  `MyPlanStepKind → icon` map.
- **`HomePlanRow`** — one plan step row (tick, glyph, title/subtitle, accent chip, progress, `dueState` badge,
  blocked-dimming + reason, per-`kind` CTA). Pure presentational; inputs: the `MyPlanStep`.
- **`RecentTile`** — a recently-enrolled rail tile (title + accent chip + "Added N days ago"); input: `MyPlanRecent`.
- **`daysUntil(iso)` / `addedAgo(iso)`** helpers — local replications of the day-diff maths (do **not** import the
  dashboard's `relativeTime`, presentation.ts lines 148-167 — feature boundary). `expiresInDays` is also already on
  `focus` from the DTO — prefer it where present; the helper covers `step.expiresAtUtc` rows and the recent rail.

### F5 — Tests (Jest; `whenStable()`, never `fakeAsync` — the 5B-1 jsdom gotcha)
- `home.component.spec.ts`:
  - **hero counts**: a plan with `totalSteps:4, overdueSteps:1` renders "You have 4 tasks — 1 overdue"; `totalSteps:0`
    renders the all-caught-up state + `salah-relaxing.png`.
  - **KPI cards**: the four cards render `activeSessions` / `videosWatched`/`videosTotal` / `overallProgressPercent`% /
    `completedSessions` in order.
  - **plan list**: Pending steps render inline, Completed steps live behind the disclosure; a `Completed` step shows the
    filled read-only tick (and **no** toggle-able `<input type=checkbox>`); a **`blocked`** step renders disabled with its
    `blockedReason` and an inert CTA; a `dueState:'ExpiringSoon'` step shows "Expires in N days", `dueState:'Expired'`
    shows "Expired".
  - **per-kind CTA**: a `kind:'Videos'` step with `action.type:'Navigate', route:'/sessions/abc'` links there with its
    `action.label`; a `kind:'Redeem'` step with `action.type:'Redeem'` links to `/redeem`.
  - **This week bar**: `completedSteps:1, totalSteps:4` → 25%.
  - **recently enrolled**: renders ≤5 tiles with "Added N days ago"; empty array → the rail section is absent.
  - **empty plan** (`focus:null, steps:[]` or the single onboarding Redeem step): the mascot empty-state + **Browse the
    catalogue** CTA show.
  - Cite `FR-STU-SES-001` / `FR-STU-CAT-003` / `NFR-A11Y-001` in the relevant `it()` names.
- `plan.service.spec.ts`: `plan()` hits `GET /api/me/plan` **with no query params** and **with** a bearer (i.e. not in
  `ANONYMOUS_PATHS`), and maps the `MyPlanDto` (string-union enums) through unchanged. Mirror `my-sessions.service.spec.ts`.
- **Jest data-access mock:** specs that render `HomeComponent` must `jest.mock` the `@sb/student-portal/data-access`
  barrel (the ESM-fire gotcha from S2/S3) — re-export `PlanService` + the model types from the mock.

## Exit criteria
A signed-in student lands on **Home** and sees: a greeting + "You have N tasks — M overdue", four KPI cards from `kpis`,
the "Your plan" list (Pending inline with read-only ticks, `dueState` badges, blocked rows disabled with their reason,
per-kind CTAs that navigate to `/sessions/{id}` or `/redeem`, and a collapsed Completed sub-list), a "This week" bar
(`completedSteps/totalSteps`), a "Recently enrolled" rail ("Added N days ago"), and — when there's nothing to do — the
mascot empty-state with a **Browse the catalogue** CTA. The screen is responsive + a11y-clean on phone/tablet/desktop.
`npx nx build student-portal` (AOT) + `nx test student-portal-feature-home` green. Hand to wiring.

## Intentional non-implementations (contract §0 / §G)
- **No editable to-do items.** Plan steps are **derived state** rendered read-only (ticks, not togglable checkboxes) —
  the student never authors or checks off a task; `status` flips when the underlying engine state changes.
- **No fabricated due dates / reminders / countdowns** on videos/assignments/quizzes. The **only** deadline rendered is
  enrollment expiry (`dueState` / "Expires in N days") — the mock's "Due in 3d/5d/7d" is dropped.
- **No streaks / gamification / points / badges.** Out of scope (net-new domain + infra — §G).
- **No Enroll / Renew buttons.** Enrollment is code-only — "get into the next session" / "your access expired" renders as
  **Redeem** (→ `/redeem`), `FR-STU-CAT-003` / §E.4.
- **No client-side plan computation.** The frontend renders the server-composed plan; it does **not** re-derive focus
  selection, step ordering, or the cap of 7 (those are frozen server logic — contract §E). It only formats
  greeting/relative-time and the "This week" % from the DTO.
- **No feature→feature imports.** KPI stat-card visual and the relative-time helper are **replicated**, not imported from
  `feature-dashboard`. Home **routes** to `/sessions/{id}` and `/redeem` (route strings, not imports of
  `feature-sessions` / `feature-catalogue`).
- **Not built here:** the `GET /api/me/plan` handler + Redis cache + invalidation (backend stream); the weekly Hangfire
  pre-warm job + notifications (contract §G/§I, deferred); any change to the 5C Play gate or the session-detail screen
  (reused as-is via the route).

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the FRONTEND stream of the Student-Portal HOME slice (a personalized weekly study plan) for Salah
Bahzad (Angular v20+, Nx). Edit frontend/** ONLY. The app, shell, auth, catalogue, and student sessions already exist
(S0–S3); the shell's Home nav item is already enabled and the '' route currently loads a placeholder — you replace it
with a NEW libs/student-portal/feature-home lib.

Read first, in order:
1. frontend/CLAUDE.md (Angular v20+ conventions, tokens, icons, module boundaries).
2. docs/IMPLEMENTATION-PLAN-student-home-frontend.md — THIS doc. DESIGN SOURCE OF TRUTH = .claude/Salah Bahzad Student
   Portal/Student Portal.html (Home mock — but render read-only ticks, no fabricated dates, Redeem not Enroll).
3. docs/contracts/student-home-weekly-plan.md — the FROZEN contract: §A (GET /api/me/plan, RequireStudent, no params,
   ALWAYS 200 incl. empty plan), §A.1 (MyPlanDto / kpis / focus / MyPlanStepDto / MyPlanRecentDto — match field-for-
   field), §B (the four string-union enums), §E.4 (empty/expired/all-done shapes), §F (read, not audited), §G (deferred).
   The plan is DERIVED state rendered read-only — no editable checkboxes, no fabricated due dates.
4. Code to reuse/replicate (DO NOT import features): libs/admin-portal/feature-dashboard (dashboard.presentation.ts
   dashIconSvg/accentBg/accentFg + dashboard.component.ts .db-stat markup/styles — REPLICATE the KPI stat-card locally),
   libs/student-portal/data-access (MySessionsService is the pattern for PlanService; export from the barrel index.ts),
   StudentAuthStore.firstName for the greeting, app.routes.ts (swap the '' child to HomeComponent), shell.component.ts
   (Home nav already enabled).

Build: scaffold libs/student-portal/feature-home (tags scope:student-portal/type:feature, prefix sb, @nx/jest) + its
tsconfig.base.json alias + swap the '' route to HomeComponent (replace HomePlaceholderComponent). A PlanService in
data-access (plan() -> GET /api/me/plan, authenticated, NO params, string-union models exported from the barrel). A
HomeComponent: hero (greeting + "You have N tasks — M overdue" + Redeem/Browse CTAs), 4 KPI cards from kpis (replicated
dashboard stat-card, not clickable), "Your plan" list (Pending inline + collapsed Completed sub-list, READ-ONLY ticks,
blocked rows disabled w/ blockedReason, dueState badges incl. "Expires in N days", per-kind CTA -> action.route Navigate
or /redeem Redeem — render action.label verbatim), "This week" bar (completedSteps/totalSteps), "Recently enrolled" rail
("Added N days ago" client-side), mascot empty-state (salah-relaxing.png + Browse catalogue). Responsive + a11y.

Jest with whenStable() (NOT fakeAsync): hero counts; KPI cards; plan list (read-only tick, blocked disabled + reason,
dueState badges, per-kind CTA navigate vs /redeem, Completed disclosure); this-week %; recent rail; empty-plan mascot.
plan.service.spec hits GET /api/me/plan with a bearer + no params. jest.mock the data-access barrel (ESM-fire gotcha).
Green gate: `npx nx build student-portal` + `nx test student-portal-feature-home`. Report both.
```
