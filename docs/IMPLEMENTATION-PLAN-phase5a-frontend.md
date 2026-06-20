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

### B2 — Audit data-access (`feature-audit/src/lib/data-access/`)
- `audit.models.ts` — `AuditListItem`, `AuditDetail`, `AuditFacets`, `AuditListQuery`, `PagedResult<T>`,
  `DashboardSummary`-not-here. Field names exactly per contract §2 (e.g. `occurredAtUtc`, `actorType`,
  `actorName`, `beforeJson`). `actorType` is the union `'Staff'|'Student'|'System'`.
- `audit.service.ts` — mirror `code.service.ts`. Methods: `list(query): Promise<PagedResult<AuditListItem>>`
  (signal-backed `#entries/#total/#isLoading/#error`), `get(id): Promise<AuditDetail>`, optional
  `facets(): Promise<AuditFacets>`. Build `HttpParams` from the query (omit empty filters).

### B3 — Audit-log screen (`feature-audit/src/lib/audit-log/audit-log.component.ts`)
- **Filter bar:** actor type (select), action (select/typeahead from facets or client constants), entity type,
  date range (from/to), free-text search, plus optional `studentId`/`sessionId` deep-link params. Debounce
  search; reset to page 1 on filter change.
- **Feed/table:** columns who (actorName + actorType chip) / action / entity (type + short id) / when
  (relative + absolute tooltip) / where (portal + ip). Pagination via the shared component. Empty-state when no
  rows.
- **Drill-in:** a `shared/ui` drawer (or modal) showing the full `AuditDetail` — who/what/when/where +
  pretty-printed `beforeJson`/`afterJson` diff + the `prevHash → hash` chain. Opening a row calls `service.get(id)`.
  (A sensitive entry the caller can't see returns 404 → show a friendly "not available" state.)
- `audit.presentation.ts` — action→label map, entityType→label/icon, actorType→`--sb-subject-*` chip color,
  relative-time formatter. Keep all label/format logic here (mirror `code.presentation.ts`).

### B4 — Dashboard data-access + screen (`feature-dashboard`)
- `data-access/dashboard.models.ts` (`DashboardSummary`, `CodeCounts`, reuse `AuditListItem` shape) +
  `data-access/dashboard.service.ts` (mirror `code.service.ts`; `load(from?, to?): Promise<DashboardSummary>`).
- Rewrite `lib/dashboard/dashboard.component.ts` per `scrDashboard`: **KPI cards** (pending approvals, active
  students, codes used/active/total, enrollments-in-period, revenue-from-codes) with `--sb-subject-*` accent
  chips; **recent-activity feed** (maps `recentActivity` to the same row presentation as B3 — consider sharing a
  small presentation helper or duplicating per Nx boundary rules); **quick actions** (review approvals /
  generate codes / create session) gated by `AuthStore` role (hide code-generation for Assistant — server still
  enforces). Cards deep-link into the relevant feature routes.

### B5 — Shell wiring (`libs/admin-portal/feature-shell`)
Mirror how `feature-codes` is registered. Add an `activity` route → `feature-audit` route component; add an
"Activity log" sidebar nav item (outline icon from the prototype). Dashboard already routes at the app root —
just ensure it renders the rebuilt component. Nav visibility by role from `AuthStore` (Assistant still sees
Activity log; sensitive rows are filtered server-side). Optional deep-links: student/session detail screens can
link to `/activity?studentId=…` / `?sessionId=…`.

### B6 — Tests (Jest, mirror `code.service.spec.ts` + `code-list.component.spec.ts`)
- `audit.service.spec.ts` — param building (filters omitted when empty; paging), response mapping, error
  extraction. `dashboard.service.spec.ts` — `load()` GET + mapping.
- `audit-log.component.spec.ts` — renders rows, applies a filter, opens the drill-in (mock service), empty-state.
  `dashboard.component.spec.ts` — renders KPI values, hides Assistant-only quick action when role=Assistant
  (mock `AuthStore`). Use `whenStable()` (not `fakeAsync`) for the promise-based services (Phase-4 gotcha).

## Exit criteria
Both screens render against the contract shapes; `npx nx build admin-portal` (AOT) green; both `nx test` targets
green; Assistant vs Teacher quick-action gating verified in specs. Hand off to the wiring stream (which runs both
screens against the live backend).

## Out of scope (defer)
- **Attendance** screens (`FR-ADM-ATT-*`) → 5B (scores are null until the 5B engine). **Assignment/quiz review**
  (`FR-ADM-REV-*`) → 5B.
- Audit CSV export (not required by `FR-ADM-AUD`).
- Live/streaming audit updates — the feed is request/refresh only in 5A.

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

Deliver: a NEW lib feature-audit (audit.service + models, audit-log component with filter bar + pagination +
drill-in drawer showing before/after + hash chain, audit.presentation labels/chips); build out the existing
feature-dashboard stub (dashboard.service + models, KPI cards + recent-activity feed + role-gated quick actions
per scrDashboard); wire the `activity` route + "Activity log" nav in feature-shell. Gate nav/quick-actions by
AuthStore role (security is server-side; UI only reflects it). Jest specs for services + components (use
whenStable(), not fakeAsync).

Green gate: `npx nx build admin-portal` (AOT type-checks templates) + `nx test admin-portal-feature-audit` +
`nx test admin-portal-feature-dashboard`. Report all three results when done.
```
