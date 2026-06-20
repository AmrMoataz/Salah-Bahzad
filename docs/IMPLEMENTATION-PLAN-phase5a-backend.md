# Phase 5A — BACKEND stream (Audit-log browser + Dashboard)

> Run this in its **own** Claude session, in parallel with the frontend stream. Created 2026-06-20.
>
> **Read first:** `backend/CLAUDE.md` (conventions, domain model, audit rules) and the **frozen contract**
> `docs/contracts/phase5a-audit-dashboard.md` (the API shape you must produce).
> **Template to mirror:** the Phase-4 `Features/Codes` read slice — `ListCodesHandler`, `CodeFilters`,
> `CodeListProjector`, `ExportCodesHandler` (explicit audit), `CodeEndpoints`. Copy their structure verbatim.
>
> **File ownership (do not cross):** this stream edits **`backend/**` only**. Do not touch `frontend/`. The only
> coupling to the frontend is the frozen contract — match it field-for-field.

## Goal
Expose the four read endpoints in the frozen contract (audit list, audit detail, optional facets, dashboard)
end-to-end (Application → Api → tests). **This is purely additive read code:** no migration, no new `DbSet`, no
domain change, **no permission/catalog edit**. Green gate: `dotnet build -c Release` + `dotnet test -c Release`
(needs Docker for Postgres Testcontainers).

## What ALREADY exists (reuse, don't reinvent)
- **`IAppDbContext` already exposes** `AuditEntries`, `Students`, `Codes`, `Enrollments`, `Attendances`
  (`Application/Common/Interfaces/IAppDbContext.cs`). No DbSet work.
- **Permissions already declared + bundled** — `AuditRead`/`AuditReadSensitive`/`DashboardRead` in
  `Domain/Enums/Permission.cs`; Teacher has all three + sensitive, Assistant has `AuditRead`+`DashboardRead`
  (no sensitive) in `Application/Features/Auth/PermissionCatalog.cs`. **Do not change these.**
- **`AuditEntry`** (`Domain/Entities/AuditEntry.cs`) fields: `Id, TenantId, ActorId, ActorRole, ActorType,
  Action, EntityType, EntityId, Summary, BeforeJson, AfterJson, IpAddress, Portal, DeviceId, OccurredAtUtc,
  PrevHash, Hash`. Indexes already present (`AuditEntryConfiguration.cs`): `(TenantId, OccurredAtUtc)`,
  `(EntityType, EntityId)`, `(ActorId)`, `(TenantId, Action)` — they cover every contract filter.
- **`IAuditWriter.WriteAsync(new AuditWriteRequest(Action:, EntityType:, EntityId:, Summary:))`** — the explicit
  audit seam for GET reads (the interceptor only fires on writes). Pattern: `ExportCodesHandler.cs` (lines
  23-29) and `GetStudentIdImageUrlHandler.cs`. Actor/tenant/IP/portal/hash-chain are filled from context.
- **`ICurrentTenantResolver { Guid TenantId; bool IsResolved; }`** (`Application/Common/Interfaces/`) — inject
  this to get the tenant id for the explicit audit filter.
- **`PagedResult<T>`** (`Application/Common/Models/`); endpoints auto-discovered via `IEndpointGroup`.

## ⚠️ The one correctness rule that must not be missed
`AuditEntry` is **NOT** `ITenantOwned` → `AppDbContext.ApplyGlobalQueryFilters` skips it → **no automatic tenant
filter**. Every query over `db.AuditEntries` (list, detail, dashboard recent-activity) **MUST** include
`.Where(a => a.TenantId == tenant.TenantId)` using the injected `ICurrentTenantResolver`. This is the #1 thing
the wiring/integration tests assert (`NFR-SEC-010`). The KPI counts over `Students`/`Codes`/`Enrollments` are
auto-filtered (those are tenant-owned) — do **not** add manual tenant `Where` there.

## Steps

### A1 — Application: Audit DTOs + sensitive-actions constant
- `Application/Features/Audit/DTOs/AuditDtos.cs` — `AuditListDto`, `AuditDetailDto`, `AuditFacetsDto` (contract §2).
- `Application/Features/Audit/SensitiveAuditActions.cs` — `static readonly HashSet<string> { "StudentIdImageViewed",
  "AuditViewed" }` (contract §3) + a `Contains(action)` helper.

### A2 — Application: audit list query (`Features/Audit/Queries/ListAudit/`)
- `ListAuditQuery(actorType?, actorId?, action?, entityType?, entityId?, studentId?, sessionId?, from?, to?,
  search?, page=1, pageSize=20, **bool includeSensitive**)` : `IRequest<PagedResult<AuditListDto>>`.
  `includeSensitive` is supplied by the endpoint (A6), **not** derived in the handler.
- `ListAuditValidator` — `page ≥ 1`, `pageSize ∈ [1,100]`, `from ≤ to` (mirror `ListCodesValidator`).
- `AuditFilters.cs` (mirror `CodeFilters`) — apply each filter; `studentId`/`sessionId` ⇒ `EntityId == value`;
  `search` ⇒ `EntityFunctions`/`ILike` on `Summary` (mirror how `CodeFilters` does the serial/name search);
  when `!includeSensitive` add `!SensitiveAuditActions.Contains(a.Action)`.
- `ListAuditHandler(IAppDbContext db, ICurrentTenantResolver tenant)` —
  `db.AuditEntries.AsNoTracking().Where(a => a.TenantId == tenant.TenantId)` → `AuditFilters.Apply(...)` →
  `CountAsync` → `OrderByDescending(a => a.OccurredAtUtc).Skip(...).Take(...)` → project via A4.

### A3 — Application: audit detail query (`Features/Audit/Queries/GetAuditEntry/`)
- `GetAuditEntryQuery(Guid Id, bool includeSensitive) : IRequest<AuditDetailDto>`.
- `GetAuditEntryHandler(IAppDbContext db, ICurrentTenantResolver tenant, IAuditWriter auditWriter)` —
  fetch `a => a.Id == Id && a.TenantId == tenant.TenantId`; if null → `NotFoundException("Audit entry", Id)`.
  If `SensitiveAuditActions.Contains(a.Action)`: when `!includeSensitive` → throw `NotFoundException` (don't
  reveal); when allowed → `await auditWriter.WriteAsync(new AuditWriteRequest(Action: "AuditViewed",
  EntityType: "AuditEntry", EntityId: a.Id, Summary: $"Viewed audit entry {a.Id} ({a.Action})"))` then return.
  Resolve `actorName` via A4. (`NotFoundException`/`ForbiddenException` already map to 404/403 — see
  `Api` exception middleware; `IAuditViaEventOnly` not relevant here.)

### A4 — Application: actor-name projector (`Features/Audit/AuditListProjector.cs`)
Mirror `CodeListProjector`. Given audit rows, batch-resolve `actorName`:
- `ActorType == "System"` ⇒ `"System"`.
- `ActorType == "Staff"` ⇒ `Staff.DisplayName` (lookup `db.Staff.IgnoreQueryFilters().Where(id ∈ staffActorIds)`).
- `ActorType == "Student"` ⇒ `Student.FullName` (lookup `db.Students.IgnoreQueryFilters()...`).
Use `IgnoreQueryFilters()` for the name lookups (ids already came from tenant-scoped rows) so a deactivated
actor still shows a name. Expose `ToListDtosAsync(db, rows, ct)` and a `ToDetailDto(row, actorName)`.

### A5 — Application: dashboard query (`Features/Dashboard/Queries/GetDashboard/`)
- `GetDashboardQuery(DateTimeOffset? From, DateTimeOffset? To) : IRequest<DashboardDto>`;
  `DTOs/DashboardDtos.cs` (`DashboardDto`, `CodeCountsDto`).
- `GetDashboardHandler(IAppDbContext db, ICurrentTenantResolver tenant, TimeProvider clock)` — compute
  `periodTo = To ?? clock.GetUtcNow()`, `periodFrom = From ?? periodTo.AddDays(-30)`. KPI counts off the
  auto-filtered DbSets (`StudentStatus.Pending/Active`, `CodeStatus.Used/Active`, `Codes.CountAsync()` total,
  `Enrollments` where `EnrolledAtUtc` in range, `revenueFromCodes = Codes.Where(Status==Used).SumAsync(Value)`).
  `recentActivity`: reuse the **A2 path with `includeSensitive: false`** (or call the projector directly over
  `db.AuditEntries.Where(tenant)…Take(8)`) — **explicit tenant filter**.

### A6 — Api: endpoints (`Api/Endpoints/AuditEndpoints.cs`, `DashboardEndpoints.cs`)
Mirror `CodeEndpoints` (`IEndpointGroup`, `MapGroup`, `.RequirePermission(...)`, `.WithName/.WithSummary/.Produces<>`).
- `/api/audit` group, `RequirePermission(Permission.AuditRead)`:
  - `GET /` → `ListAuditQuery`. **Compute `includeSensitive` here**: check the caller's permission claims for
    `AuditReadSensitive` and pass the bool in. Reuse the exact claim check used by
    `Api/Authorization/RequirePermission.cs` (factor a small `HttpContext.HasPermission(Permission)` helper if
    one doesn't already exist). Map `[FromQuery]` params per contract #1.
  - `GET /{id:guid}` → `GetAuditEntryQuery(id, includeSensitive)`; `.Produces<AuditDetailDto>()` + 404.
  - *(optional)* `GET /facets` → distinct actions/entityTypes (tenant-filtered). Skip if you prefer client
    constants — note the decision in the contract.
- `/api/dashboard` group, `RequirePermission(Permission.DashboardRead)`:
  - `GET /` (`[FromQuery] from?, to?`) → `GetDashboardQuery`; `.Produces<DashboardDto>()`.

### A7 — Tests (`tests/SalahBahazad.IntegrationTests`, mirror `SessionApiTests` + `SalahBahazadApiFactory`)
- **`NFR-SEC-010` tenant isolation (must-have):** seed audit rows for tenant A and tenant B; a tenant-B token
  gets **zero** of A's rows from `GET /api/audit` and **404** on A's `GET /api/audit/{id}`.
- **Scope:** Assistant token's list omits `StudentIdImageViewed`/`AuditViewed`; Teacher token includes them.
- **Filters:** assert each of `actorType/actorId/action/entityType/entityId/studentId/sessionId/from/to/search`
  narrows; paging + desc order.
- **Detail:** 404 cross-tenant; 404 sensitive-for-Assistant; sensitive-for-Teacher returns the entry **and**
  writes exactly **one** `AuditViewed` row (assert count delta = 1, hash chain intact).
- **Dashboard:** seed N students/codes/enrollments → KPI counts exact; `revenueFromCodes` = Σ used-code values;
  `recentActivity` ≤ 8, tenant-scoped, excludes sensitive; period filter respected.
- **Default-deny:** anonymous → 401; a token without `AuditRead`/`DashboardRead` (e.g. a student-role or
  empty-permission token) → 403.
- Unit (optional, `UnitTests`): `AuditFilters` predicate composition; `SensitiveAuditActions.Contains`.

## Exit criteria
All four contract endpoints return the documented shapes; `dotnet build -c Release` + `dotnet test -c Release`
green; Scalar/OpenAPI shows new **Audit** + **Dashboard** groups; the tenant-isolation + scope tests pass.
Hand off to the wiring stream.

## Out of scope (defer — documented, not skipped)
- **Attendance** endpoints/screens (`FR-ADM-ATT-*`) — scores are written by the 5B engine; `Attendance` columns
  are null today. → 5B.
- **Audit CSV export** — not required by `FR-ADM-AUD`; add later only if asked.
- Deep cross-entity audit correlation (every code/enrollment *about* a student) beyond `EntityId` match — later.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the BACKEND stream of Phase 5A (Audit-log browser + Dashboard) for the Salah Bahzad admin
portal. This is additive READ-ONLY code — no migration, no new DbSet, no permission/catalog change.

Read first, in order:
1. backend/CLAUDE.md (conventions, domain model, audit rules)
2. docs/contracts/phase5a-audit-dashboard.md (the FROZEN API contract — build to it field-for-field)
3. docs/IMPLEMENTATION-PLAN-phase5a-backend.md (your step-by-step, A1–A7)

Mirror the Phase-4 Features/Codes read slice (ListCodesHandler, CodeFilters, CodeListProjector,
ExportCodesHandler, CodeEndpoints). Edit backend/** ONLY — do not touch frontend/.

CRITICAL: AuditEntry is NOT ITenantOwned, so the EF global query filter does not scope it. Every query over
db.AuditEntries (list, detail, dashboard recent-activity) MUST add .Where(a => a.TenantId == tenant.TenantId)
via the injected ICurrentTenantResolver, or audit data leaks across tenants (NFR-SEC-010). The integration test
that proves this is mandatory.

Deliver: Features/Audit (DTOs, SensitiveAuditActions, ListAudit, GetAuditEntry, AuditListProjector,
AuditFilters), Features/Dashboard (GetDashboard + DTOs), Api/Endpoints/AuditEndpoints + DashboardEndpoints,
and IntegrationTests covering tenant isolation, Assistant-vs-Teacher sensitive scoping, every filter, the
AuditViewed-on-sensitive-drill-in rule, dashboard KPI accuracy, and default-deny (401/403).

Green gate: `cd backend && dotnet build -c Release && dotnet test -c Release` (Docker required for Testcontainers).
Report the tenant-isolation and sensitive-scoping test results explicitly when done.
```
