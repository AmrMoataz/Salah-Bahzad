# Phase 4 — FRONTEND stream (`feature-codes` + unlock/refund/enrollment-history)

> Run this in its **own** Claude session, in parallel with the backend stream. Created 2026-06-20.
>
> **Read first:** `frontend/CLAUDE.md` and the **design source of truth** — the
> `.claude/Salah Bahzad Teacher Portal/` prototype + `apps/admin-portal/src/styles/_design-tokens.scss`
> (NOT `docs/03-components.md`). Plus the **frozen contract** `docs/contracts/phase4-codes-enrollment.md`.
> **Template to mirror:** the Phase 3 `libs/admin-portal/feature-sessions` slice — copy its structure
> (`lib/data-access/{models,service}.ts`, component folders, `*.presentation.ts`, `index.ts`, `test-setup.ts`).
>
> **File ownership (do not cross):** this stream edits **`frontend/` only** — the new
> `libs/admin-portal/feature-codes/`, plus scoped edits to `feature-sessions` (unlock/refund/Enrolled tab) and
> `feature-students` (Enrollments tab), `app.routes.ts`, and the `feature-shell` sidebar. Do not touch `backend/`.
> Build entirely against the frozen contract with **HttpClient mocked** — you do NOT need a running backend.

## Goal
Ship the admin Codes experience (register + generate) as a new Nx feature lib, and add the enrollment actions
(unlock, refund, Enrolled tab, student Enrollments tab) to the existing slices — all wired to the frozen contract.
Green gate: `npx nx build admin-portal` + `npx nx test admin-portal-feature-codes` (+ the touched slices’ tests).

## Established patterns to reuse
- Angular v20+ **standalone, signals, OnPush, functional guards**; data via `HttpClient` in a signal-backed service
  (mirror `SessionService`). Enums arrive as **string names**; model them as TS string-union types.
- Nx lib `type:feature`, `scope:admin-portal`. **A `type:feature` lib cannot import another feature**
  (eslint `enforce-module-boundaries`). The unlock picker calls `/api/students?status=Active&search=` from this slice’s
  **own** service — do **not** import `feature-students`.
- Permissions via `AuthStore.hasPermission('CodesRead' | 'CodesGenerate' | 'CodesDisable' | 'CodesDelete' |
  'EnrollmentsUnlock' | 'EnrollmentsRefund')`. Hide Teacher-only controls; the server still enforces.
- Shared DS components in `@sb/shared/ui` (`libs/shared/ui`): `table`, `combobox`, `select`, `status-pill`, `switch`,
  `alert`, `confirm-dialog`, `modal`, `pagination`, `card`, `empty-state`, `button`, `tag`, `toast`, `clipboard`.
  **No new shared component is needed.** Toasts on every mutation.
- Jest is configured workspace-wide (shared `frontend/jest.preset.js`, per-lib `test-setup.ts`).

## Steps

### B1 — Lib scaffold + typed contract
`nx g @nx/angular:library feature-codes --directory=libs/admin-portal/feature-codes` (tags `type:feature`,
`scope:admin-portal`). Add:
- `lib/data-access/code.models.ts` — TS interfaces + string-union enums **mirroring the frozen contract**
  (`CodeListDto`, `CodeBatchDto`, `EnrollmentDto`, `CodeStatus`, `EnrollmentMethod`, `EnrollmentStatus`,
  `PagedResult<T>`; plus a lightweight `StudentSearchRow { id, name, phone }` for the unlock picker).
- `lib/data-access/code.service.ts` (signal-backed over `HttpClient`) — one method per contract endpoint #1–7 (codes)
  + `loadSessions()` (for the filter + generate combos, hitting `/api/sessions`). **CSV download helpers:** `export()`
  (#3) / `exportBatch(batchId)` (#4) trigger a file download from the streamed response (`responseType:'blob'` +
  object-URL), and a client-side `exportRows(rows)` builds the **selection** CSV in-browser (matches `scrCodes`).

### B2 — Codes register  (`scrCodes`, `FR-ADM-COD-002..005`)
New route screen. `pageHead('Codes', '{n} codes · {a} active · enrollment register', actions=[Export #3, Generate
(Teacher)])`. Assistant sees a read-only **Alert** ("Assistants can view the register…"). Filter-bar card: search
(serial\|student) + status select (All/Active/Used/Disabled→`Inactive`) + session combobox. `sb-table` columns exactly
per the prototype: **select** (Teacher-only checkbox, **disabled for `Used`**) / **Serial** (mono) / **Value** (`EGP n`,
right) / **Status** (`status-pill`; `Inactive`→"Disabled") / **Batch** / **Session** / **Redeemed by** (name + date, or
"—") / **Created** / **actions** (Teacher-only, hidden when `Used`: Enable↔Disable #5/#6, Delete #7 via `confirm-dialog`).
Bulk bar when rows selected: "{n} selected" · **Disable** (loop #5) · **Export selection** (client CSV) · **Clear**.
Pagination. Mirror `session-list` / `student-list`.

### B3 — Codes generate  (`scrCodesGenerate`, `FR-ADM-COD-001`)
New route, **Teacher-only** (otherwise render the `roleGate` like `scrCodesGenerate` does). 2-col grid: **left** *Batch
settings* card — Session combobox (`loadSessions`), Value (EGP) **pre-filled from the picked session’s price**, Quantity;
**Generate {qty} codes** primary → #2. **Right** card flips Preview → *Batch ready* on success (success check, "{qty}
codes minted", "{session} · EGP {value} each", **Download Excel** → `exportBatch(batchId)` #4). Keep the prototype’s
"serials are unique and tenant-isolated" preview copy.

### B4 — Session detail: Unlock + Enrolled tab + Refund  (`scrSessionDetail`, `FR-ADM-SES-009/010`) — edits `feature-sessions`
- **"Unlock for student"** secondary button in the detail header (gate on `EnrollmentsUnlock`). Modal mirrors
  `unlockBody()`: a `combobox` over `SessionService.searchActiveStudents(q)` → `/api/students?status=Active&search=`
  (map to `{value:id,label:name,description:phone}`), copy "Grant access bypassing code & price", confirm "Unlock
  session" → #9 → toast + refresh the Enrolled tab.
- **Enrolled tab** (replace the Phase 3 placeholder): `sb-table` of `EnrollmentListDto` — Student cell (avatar+name),
  Progress bar* + Quiz best* (*0 placeholders, Phase 5), per-row **Review** (disabled/Phase 5) + **Refund** →
  `confirm-dialog` "Refund enrollment? … Refund & revoke" (danger) → #10 → toast. Data via `SessionService` (extend it
  with `listEnrollments(sessionId)`, `unlock(...)`, `refund(enrollmentId)`, `searchActiveStudents(q)`).

### B5 — Student detail: Enrollments & transactions tab  (`scrStudentDetail`, `FR-ADM-STU-008`) — edits `feature-students`
The history tab `enroll` already exists in the prototype (`Enrollments & transactions`). Wire it to live data: add
`listEnrollments(studentId)` to `StudentService` hitting #11; render `StudentEnrollmentDto` rows — **Session** /
**Method** (`status-pill` `Code`/`Unlock`) / **Amount** (`EGP n` or "Free" when 0) / **When**. (Phase 2 may have left
this tab on seed/empty data — replace with the real query.)

### B6 — Routes + nav
- `apps/admin-portal/src/app/app.routes.ts`: `/codes`, `/codes/generate` (lazy `loadComponent`,
  `withComponentInputBinding`), guarded by `AuthStore.hasPermission('CodesRead')` (generate additionally Teacher-only).
- Sidebar: add **Codes** (`ticket` icon) to the **Operations** group in
  `feature-shell/src/lib/shell/shell.component.ts`, **between Sessions and Attendance** (matches `navConfig()`).
  Visibility guarded by `CodesRead`.

### B7 — Tests
Jest specs: `CodeService` (mocked `HttpClient` — assert URLs/payloads/filters match the contract, incl. the CSV
blob download and the redeem path **absent** from the admin service), plus key components: register filters +
Teacher-only action gating + bulk select (used rows uncheckable), generate value-pre-fill + success/Download flow,
session-detail refund confirm, unlock picker. Gate: `nx build admin-portal` + `nx test admin-portal-feature-codes`
(and the `feature-sessions` / `feature-students` specs you touched) green.

## Working without a backend
Mock `HttpClient` in tests and (optionally) an in-memory fixture for `nx serve` review. All shapes come from the frozen
contract — when wiring connects the real API, only the base URL changes. If a real shape mismatches the contract,
**do not silently adapt** — record it for the wiring stream’s reconciliation.

## Exit criteria
Register + Generate screens built to the contract; Unlock/Refund/Enrolled + student Enrollments tabs wired; sidebar +
routes added; `nx build admin-portal` + `nx test admin-portal-feature-codes` green; design matches the prototype.
Hand off to the wiring stream.

## Out of scope (defer)
Redeem-by-code UI (student portal, future — no admin screen) · Enrolled-tab **progress/quiz-best** real data + **Review**
(Phase 5 attendance/review) · Dashboard codes/revenue cards (Phase 5) · the audit-log browser (Phase 5).
