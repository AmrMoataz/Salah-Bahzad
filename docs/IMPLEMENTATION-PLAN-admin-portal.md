# Implementation Plan — Teacher/Admin Portal (Backend + Frontend)

> Status: **Active — foundation in progress** · Created & updated 2026-06-17 · Scope: Salah Bahzad **Teacher/Admin portal only** (the platform/shared backend it depends on + the Angular admin app). Student portal frontend and the Flutter app are **out of scope** here, though the shared backend engines built now will serve them later.

Written against the specs in `docs/` (`README.md`, `01`–`09`, `tokens.*`) and the two ready HTML mockups. Requirement IDs (e.g. `FR-PLAT-AUD-001`) are referenced throughout so every phase is traceable.

### Progress so far (2026-06-17)
- ✅ **.NET Claude Kit installed**; `/dotnet-init` run → generated `backend/CLAUDE.md` + a Clean Architecture .NET 10 solution skeleton.
- ✅ **Repo restructured** to a monorepo: `backend/` (solution), `frontend/` (Angular — to be added), `docs/` (specs + tokens + this plan). Kit plugin config kept at root `.claude/`.
- ✅ Added root `README.md`, root `CLAUDE.md`, `.gitignore`; fixed spec-doc paths in `backend/CLAUDE.md` to `../docs/`.
- ⏭️ **Next:** you run `git init` + first commit on Windows (§2), then Phase 0 hardens the skeleton.

---

## 1. Decisions captured

| Decision | Choice |
|---|---|
| Backend base | **Greenfield**, clean target-state .NET 10 solution (scaffolded); port proven domain rules from the old backend where useful |
| Architecture | ✅ **Resolved by the kit** — Clean Architecture + CQRS using the **source-generated Mediator** (Artem Shykhermanov). Authoritative record: `backend/CLAUDE.md`. See §4 |
| Repo layout | ✅ **backend/ + frontend/ + docs/** (your choice); `.claude/` kit config kept at the root |
| Build/run | Hybrid — **I scaffold + write code into your folder; you build/run and drive the kit in your Claude Code CLI** (see §2) |
| Sequencing | **Vertical-slice delivery** after a short foundation phase — see §3 (please confirm) |

Architecture, mediator, and layout are settled by the kit's `/dotnet-init`. Remaining open items are in §7.

---

## 2. What I do here vs. what you do (Cowork limits)

The .NET Claude Kit is a **Claude Code CLI plugin** — its Roslyn MCP server, agents, and `/slash` commands run in *your* Claude Code on Windows. This Cowork session's Linux sandbox has **no .NET SDK** and **cannot run git on the mounted folder** (it's a FUSE mount without stable file-locking/deletes — `git init` there leaves a corrupt repo). So:

### I do here (Cowork)
- Read/write code, configs, and docs directly into the mounted repo (`backend/`, `frontend/`, `docs/`).
- Scaffold projects, entities, endpoints, the Angular app, and the 30 design-system components from the mockups.
- Author migrations, tests, CI YAML, and the conventions docs.

### You do (Windows / your Claude Code CLI) — then I continue
1. ✅ *Done* — kit installed and `/dotnet-init` run (produced the solution + `backend/CLAUDE.md`).
2. **Initialize git** at the repo root (the sandbox can't — `.gitignore` is already in place):
   ```
   cd "C:\Users\Amr\source\repos\SalahBahazad\New System\Salah bahzad"
   git init -b main
   git add .
   git commit -m "chore: scaffold monorepo (backend solution, docs, frontend)"
   ```
3. **Build check** when I hand off Phase 0: `cd backend && dotnet restore && dotnet build` then `dotnet test`. Paste me any errors and I'll fix them.
4. **Provision cloud accounts** before the auth/video phases (not needed for Phase 0): a Firebase project (Auth) and Cloudflare R2 + CDN. I'll give exact env vars when we reach them.

> If you'd prefer I verify builds myself, I can install the .NET 10 SDK inside the sandbox and compile/test there (Linux) before you pull — say the word.

---

## 3. "Vertical slices" — definition + recommended sequencing

The term is overloaded; both senses matter here.

**(a) Vertical Slice *Architecture* (VSA)** — a *code-organization* style (one of the advisor's options): code grouped **by feature** (`Features/Students/ApproveStudent.cs` = request + handler + validator + endpoint together) rather than by technical layer. The kit chose **Clean Architecture** instead, but we still group the Application layer by feature for cohesion.

**(b) Vertical slice *delivery*** — a *sequencing* approach: build one feature **end-to-end** (DB → API → Angular screen) so there's always something demoable, instead of building the whole backend then the whole frontend ("horizontal" layering).

**Recommended sequencing:** one short **horizontal foundation phase** (Phase 0 — auth, tenancy, audit, app shell; unavoidable shared plumbing), then **vertical-slice delivery** feature-by-feature (Students → Sessions → Codes → …). Each slice ships backend + Angular together and is independently demoable; an OpenAPI contract is produced inside each slice so it stays contract-clean.

---

## 4. Architecture (resolved)

`/dotnet-init` settled this: **Clean Architecture + CQRS** with the **source-generated Mediator** (Artem Shykhermanov — zero-reflection, compile-time dispatch). The authoritative, detailed record is **`backend/CLAUDE.md`** (full domain model, business rules, EF/audit/security conventions). Stack it locked in:

- .NET 10 / C# 14, ASP.NET Core **Minimal API** endpoints (controllers optional), EF Core + Npgsql (PostgreSQL).
- CQRS via **Mediator** (source-gen) + **FluentValidation** pipeline behaviour; **manual** `.ToDto()`/`.ToDomain()` mapping (no AutoMapper).
- Firebase Auth (staff + students; platform stores no passwords) → short-lived platform JWT; **Redis** HybridCache (L1+L2) + SignalR Redis backplane; **Hangfire** background jobs; **Cloudflare R2** (HLS + signed URLs); **Scalar** OpenAPI; **xUnit v3 + Testcontainers + FluentAssertions + Verify**; **Serilog** + **OpenTelemetry**.

Actual generated structure (greenfield skeleton — placeholder `Class1.cs` / Hello-World `Program.cs` to be replaced):

```
Salah bahzad/                  (repo root — .claude/ kit config, root README + CLAUDE.md, .gitignore)
├─ backend/
│   ├─ CLAUDE.md               // authoritative backend conventions (kit-generated, tuned)
│   ├─ SalahBahazad.slnx · Directory.Build.props   (+ Directory.Packages.props to add)
│   ├─ src/   SalahBahazad.Domain · .Application · .Infrastructure · .Api
│   └─ tests/ SalahBahazad.UnitTests · .IntegrationTests
├─ frontend/  admin-portal/    // Angular (added in Phase 0)
└─ docs/      specs (01–09), design system, tokens, this plan
```

Dependency direction (verified in the `.csproj` references): **Api → Infrastructure → Application → Domain**. The one convention divergence from the old code — MediatR vs the kit's source-gen Mediator — is resolved in favour of the kit's Mediator.

---

## 5. The phased plan

Each phase: Goal · Backend · Frontend (Angular) · key requirement IDs · Exit criteria. Phase 0 is horizontal foundation; Phases 1–5 are vertical-slice deliveries.

### Phase 0 — Foundations & cross-cutting plumbing
**Goal:** turn the generated skeleton into a correct, secure foundation; fix the old system's P0 findings up front.

- **Skeleton hardening:** replace placeholder `Class1.cs` and the Hello-World `Program.cs`; add **`Directory.Packages.props`** (central package management); align test projects to **xUnit v3** (generated as v2.9.3) per `backend/CLAUDE.md`; add `.editorconfig`; you make the first **git** commit (§2).
- **Backend:** config/secrets from environment only — no committed secrets (`NFR-SEC-002`, fixes gap issue #3); EF Core + PostgreSQL, migrations **gated, not auto-applied on boot** (`NFR-AVAIL-004`, issue #4); Result/ProblemDetails/FluentValidation pipeline; global exception → correct HTTP codes (issue #12); Scalar/OpenAPI; health/readiness (`NFR-AUD-004`); Serilog + OpenTelemetry, no PII/tokens in logs (`NFR-OBS-001`, `NFR-PRIV-005`).
  - **Tenancy seam:** `TenantId` on the entity base; current-tenant resolver; EF **global query filter**; seed the single tenant (`FR-PLAT-TEN-001..005`, `NFR-SCAL-003`).
  - **Audit core:** `AuditEntry` append-only + `SaveChangesInterceptor` (atomic with the action, `NFR-AUD-003`), optional hash-chain (`NFR-AUD-001`), `System` actor, separate `AssessmentEvent` seam (`FR-PLAT-AUD-001..006`).
  - **AuthN/Z:** Firebase ID-token verification → platform JWT (`userId/tenantId/role/deviceId`); refresh + revocation (`FR-PLAT-AUTH-002/006`); permission-based policies, default-deny (`FR-PLAT-AUTH-007/008`, `NFR-SEC-003`); rate limiting on auth (`NFR-SEC-006`).
- **Frontend:** create `frontend/admin-portal` (Angular v20+: standalone, signals, OnPush, functional guards, `httpResource`/interceptors); wire `docs/tokens.css`; port the base 30-component library (Button, Input, Select, Table, Modal, Drawer, Tabs, Pagination, StatCard, Tag/StatusPill, Toast, EmptyState); app **shell** (sidebar + topbar per `docs/03-components.md`); **Login** (Firebase) + empty **Dashboard** shell.
- **Also:** CI — build + test + analyzers + dependency vuln scan (`NFR-MAINT-002`, `NFR-SEC-011`).
- **Exit:** staff log in via Firebase; an audited no-op writes an `AuditEntry`; tenant filter proven by a test (`NFR-SEC-010`); `dotnet build`/`test` green; CI green.

### Phase 1 — Staff, roles & taxonomy (the spine)
- **Backend:** Staff CRUD with role no-higher-than-self (`FR-ADM-STAFF-001..004`, `FR-PLAT-ROLE-002/003`); granular permission catalog; taxonomy CRUD — Grades/Subjects/Specializations, block-delete-in-use (`FR-PLAT-TAX-001/002/004`, `FR-ADM-TAX-*`); Cities/Regions **seeded Egypt reference data**, global/read-only, anonymous read for sign-up (`FR-PLAT-TAX-003/005`).
- **Frontend:** Staff screens; Taxonomy (tabbed Grades/Subjects/Specializations); Settings (own profile/password via Firebase self-service).
- **Exit:** a Teacher manages staff + taxonomy; every change audited; Assistant blocked from Teacher-only actions (server-enforced).

### Phase 2 — Students (review, device, history)
- **Backend:** Student aggregate w/ Firebase link, City/Region refs, terms acceptance (`FR-STU-REG-006`, `NFR-PRIV-003`), ID image in **R2 private + signed URL + audited access** (`FR-PLAT-AST-003`, `NFR-PRIV-001/002`); approve / reject-with-reason / deactivate (`FR-ADM-STU-003..006`); **device binding** + staff-clear with reason (`FR-PLAT-DEV-001..006`, `FR-ADM-STU-007`); login/enrollment/activity history (`FR-ADM-STU-008`).
- **Frontend:** Students list (filter/search/status); Student detail (profile, ID image, device panel, history tabs); Approvals queue (inline approve / reject-with-reason).
- **Exit:** full pending→active lifecycle with reasons; device clear works; ID-image views audited.

### Phase 3 — Sessions, content & question bank
- **Backend:** Session aggregate (details, 0–365 validity, grade/specialization, draft/published/archived) (`FR-PLAT-SES-001/008`); videos w/ per-video access count + **R2 upload pipeline** + transcode-to-HLS job **seam** (`FR-PLAT-SES-002`, `FR-PLAT-VID-007`, `FR-PLAT-AST-001`); materials in R2 (`FR-PLAT-SES-003`); prerequisite, no cycles (`FR-PLAT-SES-004`); quiz settings (`FR-PLAT-SES-006`); question bank — MCQ, LaTeX and/or image, variations, quiz-eligible flag, assignment-only hint, **snapshot-on-edit** (`FR-PLAT-QB-001..006`, `FR-PLAT-SES-007`).
- **Frontend:** Sessions list; Session create/edit (videos + access count, materials, prerequisite picker, quiz settings, publish); Session detail (tabbed); **Question editor** (LaTeX live preview + image upload, variations, flags); Quiz settings with bank validation (`FR-ADM-QZ-002`).
- **Exit:** ✅ author a full session with videos, materials, a question bank, and a gating quiz config.
  **Met (2026-06-19):** the wiring stream connected both streams against the running Aspire stack — an
  end-to-end smoke (create → thumbnail → video [→ `Ready` via the transcode stub] → material [+ signed-URL
  download] → LaTeX/image question + options + variation → quiz settings → prerequisite → publish) passed;
  the publish gate (`FR-ADM-QZ-002`: quiz over-count → 409) and server-side default-deny (Assistant → 403,
  anonymous → 401) were verified live; every mutation wrote a hash-chained `AuditEntry` and private media is
  stored as R2 object keys only (short-lived signed URLs on read). Gates green: `dotnet test -c Release`
  (113 unit + 48 integration) and `nx build admin-portal` + `nx test admin-portal-feature-sessions` (25).

### Phase 4 — Enrollment, codes & payments seam
- **Backend:** Code batches — Teacher-only generate, **Excel export**, lifecycle disable/enable/delete (soft), register with full usage join (`FR-PLAT-COD-001..006`, `FR-ADM-COD-*`, fixes issue #1); enrollment by code (value==price, one-shot) + **staff unlock** + **refund/revoke** + re-enroll/extend resets counters (`FR-PLAT-ENR-001..008`); payment abstraction + `PaymentTransaction` seam, gateway disabled (`FR-PLAT-PAY-001/002`); enrollment side-effects: generate assignment, provision video counters, generate prerequisite quiz (`FR-PLAT-ENR-005`).
- **Frontend:** Codes list (filter by status/batch/session) + Generate batch (Excel download); unlock-for-student and refund flows.
- **Exit:** ✅ mint→export→redeem→refund a code, all audited; counters/expiry correct on re-enroll.
  **Met (2026-06-20):** the wiring stream connected both streams against the running Aspire stack — an end-to-end
  smoke (generate batch [value defaults to session price] → CSV export #3 + batch re-export #4 [documented columns] →
  disable→`Inactive`→enable → **redeem #12 with a student JWT** [code→`Used`, enrollment `Active`/`Code`, per-video
  counters provisioned, `Completed` `PaymentTransaction`, attendance shell, `EnrollmentCreated`] → Enrolled tab →
  **unlock** a different student → **refund** [enrollment `Refunded`, code returns to `Active`, reversing payment] →
  re-enroll **reuses the same row** with reset counters + pushed expiry [`FR-PLAT-ENR-004/005/006/008`] → student
  enrollments → real `enrolledCount`) passed (58 assertions). Default-deny verified live (Assistant blocked on
  generate/disable/delete but **allowed** unlock+refund per the `EnrollmentsRefund` catalog change; anonymous → 401;
  student token → 403 on staff routes; staff token → 403 on #12); tenant isolation holds (`NFR-SEC-010`). Every
  lifecycle action wrote a hash-chained `AuditEntry` — including the **read-only CSV export, audited explicitly**
  (`FR-PLAT-AUD-002`) — redeem attributed to the `Student` actor, child rows suppressed. Frozen contract
  (`docs/contracts/phase4-codes-enrollment.md`) matched on both sides with **zero drift**. Gates green:
  `dotnet test -c Release` (132 unit + Phase-4 integration) and `nx build admin-portal` +
  `nx test admin-portal-feature-codes` (22). Full log in `IMPLEMENTATION-PLAN-phase4-wiring.md`.
  **Planned (2026-06-20):** design-anchored to the `.claude` prototype (`scrCodes`/`scrCodesGenerate`/`scrSessionDetail`
  unlock+enrolled/`scrStudentDetail` enrollments) and split into three parallel-safe streams + a frozen contract, exactly
  like Phase 3 — `docs/contracts/phase4-codes-enrollment.md` (12 endpoints) and
  `IMPLEMENTATION-PLAN-phase4-{backend,frontend,wiring}.md`. Key scope calls: **codes are session-bound + value-matched**;
  enrollment side-effects ship as a **stubbed `IEnrollmentSideEffects` seam** (assignment/quiz snapshot engines + the
  `FR-PLAT-ENR-007` prerequisite gate are Phase 5, mirroring Phase 3’s transcode stub); **redeem (#12) is backend-only**
  (student-portal path, no admin screen); Assistant bundle gains `EnrollmentsRefund` to match the role matrix + prototype.

### Phase 5 — Assessment review, attendance, video gate & dashboard
- **Backend:** assignment auto-grade → attendance (`FR-PLAT-ASG-006`, `FR-PLAT-ATT-002`); quiz engine — **server-side timer auto-submit**, single-sitting forfeit-on-disconnect, focus-loss telemetry (recorded, not auto-forfeit), best-of, **`≥` pass rule** (fixes issue #7), all events audited (`FR-PLAT-QZ-001..010`); SignalR hubs authenticated via JWT + Redis backplane (fixes issue #6, `NFR-SCAL-002`, `NFR-SEC-005`); video access gate → **short-lived signed HLS URL** + view accounting + audit + one-time deep-link handoff code (`FR-PLAT-VID-001..007`); attendance queries per session/per student.
- **Frontend:** Assignment & quiz **review** screens (`FR-ADM-REV-001..003`); Attendance matrices (per session, per student) + Excel/CSV export (`FR-ADM-ATT-001..004`); **Activity/audit log** browser with filters + drill-in (`FR-ADM-AUD-001..003`); Dashboard KPIs + recent-activity feed (`FR-ADM-DASH-001..003`).
- **Exit:** staff review any student's work; attendance exports; audit log is searchable; dashboard live.
  **Planned (2026-06-20):** Phase 5 is the largest/riskiest phase and bundles four loosely-coupled concerns, so it
  is **split into three sub-phases**, delivered in dependency order:
  - **5A — Audit-log browser + Dashboard** (read-only reporting over data that *already exists*): `FR-ADM-AUD-001..003`,
    `FR-PLAT-AUD-004/006`, `FR-ADM-DASH-001..003`. No engines, no new infra, no migration, no permission/catalog
    change (the `AuditRead`/`AuditReadSensitive`/`DashboardRead` permissions are already declared + bundled). Lowest
    risk, fastest to demo, finally surfaces the hash-chained audit trail every prior phase has been writing.
    **Design-anchored** to the prototype (`scrDashboard` + `scrActivity` in `.claude/.../Admin Portal.dc.html`): the
    activity log is an actor/action/target feed with a category icon, filtered by actor + action-category + period,
    where drill-in *navigates to the affected entity* (no before/after-JSON screen); the dashboard is 4 stat cards +
    a period selector + an enrollments bar chart + 4 role-gated quick actions + a 7-row recent-activity card. Planned
    as a frozen contract + 3 parallel-safe streams exactly like Phase 3/4: `docs/contracts/phase5a-audit-dashboard.md`
    (2 endpoints — audit feed + dashboard) and `IMPLEMENTATION-PLAN-phase5a-{backend,frontend,wiring}.md`. **Key
    correctness call:** `AuditEntry` is not `ITenantOwned`, so every audit read must filter `TenantId` *explicitly*
    (`NFR-SEC-010`).
    **Met (2026-06-20):** built across the three streams and proven end-to-end on the running Aspire stack —
    **21/21** live smoke checks, **ZERO** contract drift. Tenant isolation (`NFR-SEC-010`) and Assistant-vs-Teacher
    sensitive scoping verified live (Teacher feed total 229 vs Assistant 218, Δ=11 `StudentIdImageViewed`); dashboard
    KPIs reconciled to DB ground truth (pending 0 / active 2 / codesUsed 1 / codesActive 58 / revenue EGP 150 /
    enrollments 5); drill-in navigates to the entity (no `/api/audit/{id}`, no `AuditViewed` written); default-deny
    (anon→401, Student-role→403) holds. Full log in `IMPLEMENTATION-PLAN-phase5a-wiring.md`. **5B is next.**
  - **5B — Assessment engines + review + attendance** (the core remaining domain): assignment + quiz aggregates
    (server-side timer auto-submit, single-sitting forfeit, focus-loss telemetry, best-of, the `≥` pass-rule fix
    issue #7), the real `IEnrollmentSideEffects` (replacing the Phase-4 stub), the `FR-PLAT-ENR-007` enrollment gate,
    SignalR hubs (JWT auth + Redis backplane, issue #6), attendance scoring; then `FR-ADM-REV-*` review screens +
    `FR-ADM-ATT-*` matrices/export. Largest stream; needs Redis wired in Aspire.
  - **5C — Secure video gate** (`FR-PLAT-VID-001..007`): server access gate + per-video counter decrement + audited
    playback + short-lived signed HLS URL + one-time handoff code. Backend-only in this engagement; needs R2/MinIO +
    HLS infra wired in Aspire. No admin player screen (student-portal/native-app surface).

### Cross-cutting (every phase)
Business-rule tests as features land — enrollment, grading, quiz scoring/forfeit, code lifecycle, tenant isolation (`NFR-MAINT-001`); OpenAPI kept current; responsive phone/tablet/desktop (`NFR-COMPAT-002`); WCAG 2.1 AA (`NFR-A11Y-001`); naming avoids the old typos (`NFR-MAINT-004`).

---

## 6. Out of scope (this engagement)
Student portal **frontend**, the Flutter native app, online payment gateway, notifications (`§14` backlog), tenant onboarding/billing/theming. The backend engines (assignment/quiz/video/audit) are built to serve them later, but only admin-facing endpoints + the admin Angular app are delivered now.

---

## 7. Open items to confirm
1. **Sequencing:** confirm **vertical-slice delivery** (foundation → Students → Sessions → Codes → …) — §3.
2. **Angular workspace:** single Angular CLI app, or **Nx** (the `nx-monorepo` skill is available; better if the student portal will later share libraries)?
3. **Build verification:** keep the hybrid (you build in your CLI), or have me install .NET in the sandbox to compile/test before you pull? — §2.

*Resolved:* backend base = greenfield · architecture = Clean Architecture + CQRS + source-gen Mediator · repo layout = backend/frontend/docs (see §1).
