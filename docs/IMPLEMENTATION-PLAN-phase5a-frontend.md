# Phase 5A — FRONTEND stream (Audit-log browser + Dashboard)

> Run this in its **own** Claude session, in parallel with the backend stream. Created 2026-06-20.
>
> **Read first:** `frontend/CLAUDE.md` (Angular v20+ conventions, design source of truth, tokens, icons) and the
> **frozen contract** `docs/contracts/phase5a-audit-dashboard.md` (the API shape you consume).
> **Template to mirror:** the Phase-4 `feature-codes` lib — `code.service.ts`, `code.models.ts`,
> `code-list.component.ts`, `code.presentation.ts`, and their `.spec.ts` files. Copy the structure verbatim.
>
> **File ownership (do not cross):** this stream edits **`frontend/**` only**. Do not touch `backend/`. The only
> coupling to the backend is the frozen contract — match the field names/types exactly.

## Goal
Build two admin screens against the frozen contract: a **filterable Audit-log browser with drill-in**
(`FR-ADM-AUD-001..003`) and a **Dashboard** of KPI cards + recent-activity + role-gated quick actions
(`FR-ADM-DASH-001..003`). Green gate: `npx nx build admin-portal` (AOT type-checks templates) +
`nx test admin-portal-feature-audit` + `nx test admin-portal-feature-dashboard`.

## Design source of truth
`.claude/Salah Bahzad Teacher Portal/Admin Portal.dc.html` — the `scrDashboard` screen and the activity/audit
screen. Follow it for layout, class names, token values, icons, copy. Tokens live in
`apps/admin-portal/src/styles/_design-tokens.scss` (canonical names per `frontend/CLAUDE.md` — e.g.
`--sb-subject-*` chip backgrounds for KPI/actor accents, never white-on-saturated). Icons are inline outline
`<svg>` via `DomSanitizer.bypassSecurityTrustHtml` (see `sidebar.component.ts`/`dashboard.component.ts`).

## What ALREADY exists (reuse, don't reinvent)
- **`feature-dashboard` lib exists** but is a Phase-0 stub: only `src/index.ts` +
  `src/lib/dashboard/dashboard.component.ts`. You **build it out** (don't regenerate the lib).
- **`feature-codes` is the canonical data-access + list pattern**: signal-backed service
  (`@Injectable({providedIn:'root'})`, `inject(HttpClient)`, `#signal`+`.asReadonly()`, `firstValueFrom`,
  `#api()` reads `window.__SB_API_URL__`, ProblemDetails `detail` extraction, `HttpParams` builder).
- **`shared/ui`** — reuse the existing table, tag/status-pill, pagination, drawer, empty-state, button, select,
  input components. **`shared/data-access`** has `auth/auth.store.ts` (role for nav gating) and
  `approvals/pending-approvals.store.ts` (existing pending-count source you MAY reuse on the dashboard).
- The platform JWT is attached by the shared `authInterceptor`; the server enforces permissions (default-deny),
  so the UI only **reflects** role — it never gates security.

## Steps

### B1 — New lib `feature-audit`
`npx nx g @nx/angular:library admin-portal-feature-audit --directory=libs/admin-portal/feature-audit
--standalone --style=scss` (match the existing libs' generator flags — check `feature-codes/project.json` and
copy its `name`/tags/`test-setup.ts` setup). Expose the route component from `src/index.ts`.

> **Build the prototype's screens, not generic ones.** `scrActivity` (line 1291) and `scrDashboard` (line 549)
> are the spec. Open them and match the cards, columns, filters, chart, and copy.

### B2 — Audit data-access (`feature-audit/src/lib/data-access/`)
- `audit.models.ts` — `AuditFeedItem` exactly per contract §1 (`id, occurredAtUtc, actorType, actorRole,
  actorName, action, category, summary, targetType, targetId, targetLabel, portal, ipAddress`); `AuditCategory`
  union; `AuditListQuery` (`actorId?, actorType?, category?, period?/from?/to?, studentId?, sessionId?,
  entityType?, entityId?, page, pageSize`); `PagedResult<T>`. **No `AuditDetail`/`beforeJson`** — there is no
  detail endpoint in 5A.
- `audit.service.ts` — mirror `code.service.ts`. One method `list(query): Promise<PagedResult<AuditFeedItem>>`
  (signal-backed `#items/#total/#isLoading/#error`). Build `HttpParams` (omit empty filters).

### B3 — Activity-log screen (`feature-audit/src/lib/audit-log/audit-log.component.ts`) — match `scrActivity`
- `pageHead('Activity log', …)` — subtitle by role: Teacher = "Full audit feed — who did what, when & where";
  Assistant = "Scoped audit feed for your actions and assigned areas".
- **Assistant "Scoped view" alert** (`shared/ui` Alert, `variant:'info'`), shown when `AuthStore` role ≠ Teacher:
  *"Assistants see a subset of the audit log. Sensitive entries (e.g. who-read-what) are visible to Admins only."*
- **Filter bar** (`filterBar`): **Actor** select (`All actors` + distinct actor names from the current result
  set — no facet endpoint in 5A; populate from loaded rows like the prototype, send `actorId`), **Action** select
  (`All actions / Approvals / Codes / Sessions / Devices` → `category` param), **Period** select
  (`7d/30d/90d`, default `7d`). Reset to page 1 on change.
- **Table** (`shared/ui` Table) columns exactly per the prototype: **Actor** (bold `actorName`) · **Action**
  (a `--sb-subject-{accent}` icon circle + the action verb-phrase + bold `targetLabel`) · **When** (relative) ·
  right **"View"** button → **navigate to the affected entity** using `targetType`+`targetId`
  (`student-detail` / `session-detail` / `codes` / `staff`), toast "No linked entity" when none. Pagination +
  empty-state. **No detail drawer.**
- Read optional `studentId`/`sessionId` route query params (so `student-detail`/`session-detail` Activity tabs
  can reuse this list later).

### B4 — `feature-audit/src/lib/audit.presentation.ts`
Owns all label/format logic (mirror `code.presentation.ts`), matched to the prototype seed (lines 1588-1597):
- `CATEGORY_ICON` + `CATEGORY_ACCENT`: `approval`→`check`/`green`, `code`→`ticket`/`blue`,
  `enrollment`→`unlock`/`mustard` (refund→`money`/`green`), `session`→`book`/`purple`, `question`→`edit`/`blue`,
  `device`→`device`/`orange`, `staff`→`shield`/`purple`, `student`→`user`/`blue`, `audit`→`eye`/`neutral`,
  `other`→`dot`/`neutral`. Rejections render `x`/`red`.
- `actionPhrase(action)` → verb phrase ("approved", "rejected", "generated codes for", "unlocked a session for",
  "published", "cleared device for", "edited question bank for", "refunded enrollment for",
  "created staff account", …); fall back to `summary` for unmapped actions.
- `relativeTime(iso)`; `targetRoute(targetType, targetId)`.

### B5 — Dashboard (`feature-dashboard`) — match `scrDashboard`
- `data-access/dashboard.models.ts` (`DashboardSummary` per contract `DashboardDto`, incl.
  `enrollmentsByDay: { date; count }[]` + `recentActivity: AuditFeedItem[]`) + `dashboard.service.ts`
  (`load(period|from/to): Promise<DashboardSummary>`).
- Rewrite `lib/dashboard/dashboard.component.ts`:
  - `pageHead('Dashboard', 'Operational pulse across your academy', <period select 7d/30d/90d default 30d>)`.
  - **4 StatCards** (`shared/ui` StatCard) — "Pending approvals" (accent `mustard`, click → `approvals`),
    "Active students" (`blue`), "Codes used / active" = `${codesUsed} / ${codesActive}` (`green`),
    "Revenue (by code)" = `EGP ${revenueFromCodes.toLocaleString()}` (`purple`). **No delta badges.**
  - **Enrollments chart** card ("Enrollments — last N days"): bar chart from `enrollmentsByDay`; **bucket exactly
    like the prototype** (lines 569-575) — daily for 7d, groups of 5 → 6 bars for 30d, groups of 7 → 13 bars for
    90d; caption "`{total}` total · daily|weekly"; last bar `--sb-primary`, rest `--sb-primary-200`.
  - **Quick actions** card — **4** buttons (`scrDashboard` line 579): Review approvals, Generate codes
    (**Teacher-only** via `AuthStore`), Create session, Open attendance; `--sb-subject-*` chips; navigate.
    ("Open attendance" targets the 5B screen — wire the route now, screen lands in 5B.)
  - **Recent activity** card — top **7** `recentActivity` rows rendered with the **same** presentation as B3/B4
    (icon circle + bold actor + phrase + bold target + relative when); header action "View all" → `activity`.
    (Reuse the B4 presentation helpers; if the Nx module boundary blocks importing `feature-audit`, lift the tiny
    presentation map into `shared/ui`/`shared/util` or duplicate it — note the choice.)

### B6 — Shell wiring (`libs/admin-portal/feature-shell`)
Mirror how `feature-codes` is registered. Add the `activity` route → `feature-audit`; add the **"Activity log"**
nav item (icon `activity`) — see the prototype nav (`{ id:'activity', label:'Activity log', icon:'activity' }`,
line 1419). Dashboard already routes at the app root — ensure it renders the rebuilt component. Nav by role from
`AuthStore` (Assistant still sees Activity log; scoping is server-side).

### B7 — Tests (Jest, mirror `code.service.spec.ts` + `code-list.component.spec.ts`; use `whenStable()` not `fakeAsync`)
- `audit.service.spec.ts` — param building (empties omitted; `category`/`period`/paging), response mapping, errors.
- `audit-log.component.spec.ts` — renders feed rows, applies the category filter, **"View" navigates** to the
  target route (mock Router), shows the Assistant alert when role≠Teacher, empty-state.
- `dashboard.service.spec.ts` — `load()` GET + mapping. `dashboard.component.spec.ts` — renders the 4 KPI values,
  buckets the enrollments chart for 30d, **hides "Generate codes"** quick action when role=Assistant (mock `AuthStore`).

## Exit criteria
Both screens render against the contract shapes; `npx nx build admin-portal` (AOT) green; both `nx test` targets
green; Assistant vs Teacher quick-action gating verified in specs. Hand off to the wiring stream (which runs both
screens against the live backend).

## Out of scope (defer)
- **Before/after-JSON detail drawer** — not in the prototype; drill-in is *navigate to the entity*. (Raw
  before/after exists server-side; surface only if a future design asks.)
- **Attendance** screens (`FR-ADM-ATT-*`) → 5B (scores are null until the 5B engine). **Assignment/quiz review**
  (`FR-ADM-REV-*`) → 5B.
- Audit CSV export (not required by `FR-ADM-AUD`); StatCard trend deltas (demo-only); live/streaming updates
  (request/refresh only in 5A).

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the FRONTEND stream of Phase 5A (Audit-log browser + Dashboard) for the Salah Bahzad admin
portal (Angular v20+, Nx). Read-only data screens consuming an existing API.

Read first, in order:
1. frontend/CLAUDE.md (Angular conventions, DESIGN SOURCE OF TRUTH = .claude/Salah Bahzad Teacher Portal/
   Admin Portal.dc.html, tokens, icons)
2. docs/contracts/phase5a-audit-dashboard.md (the FROZEN API contract — consume it field-for-field)
3. docs/IMPLEMENTATION-PLAN-phase5a-frontend.md (your step-by-step, B1–B6)

Mirror the Phase-4 feature-codes lib (code.service.ts, code-list.component.ts, code.presentation.ts + specs) for
structure and the signal-backed data-access pattern. Edit frontend/** ONLY — do not touch backend/.

BUILD THE PROTOTYPE'S SCREENS, NOT GENERIC ONES — open scrActivity (line 1291) and scrDashboard (line 549) and
match them. The activity log is a feed of actor/action/target with a category icon, filtered by actor + action-
category (Approvals/Codes/Sessions/Devices) + period (7/30/90d); the row "View" NAVIGATES to the affected entity
(NO before/after drawer). The dashboard has 4 stat cards (Pending approvals / Active students / Codes used·active /
Revenue by code), a period selector, an enrollments bar chart (bucket weekly for 30/90d), 4 role-gated quick
actions, and a Recent-activity card of 7 feed rows.

Deliver: a NEW lib feature-audit (audit.service + AuditFeedItem models, audit-log component matching scrActivity
incl. the Assistant "Scoped view" alert, audit.presentation = category→icon/accent + action→verb-phrase); build
out the existing feature-dashboard stub to match scrDashboard exactly; wire the `activity` route + "Activity log"
nav in feature-shell. Gate nav/quick-actions by AuthStore role (security is server-side). Jest specs (use
whenStable(), not fakeAsync) incl. "View navigates" and "Generate codes hidden for Assistant".

Green gate: `npx nx build admin-portal` (AOT type-checks templates) + `nx test admin-portal-feature-audit` +
`nx test admin-portal-feature-dashboard`. Report all three results when done.
```
