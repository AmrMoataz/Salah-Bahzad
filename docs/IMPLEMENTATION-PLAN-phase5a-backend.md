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
Expose the **two** read endpoints in the frozen contract (`GET /api/audit` feed + `GET /api/dashboard`)
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
- **`ICurrentTenantResolver { Guid TenantId; bool IsResolved; }`** (`Application/Common/Interfaces/`) — inject
  this to get the tenant id for the explicit audit filter.
- **`PagedResult<T>`** (`Application/Common/Models/`); endpoints auto-discovered via `IEndpointGroup`.

## ⚠️ The one correctness rule that must not be missed
`AuditEntry` is **NOT** `ITenantOwned` → `AppDbContext.ApplyGlobalQueryFilters` skips it → **no automatic tenant
filter**. Every query over `db.AuditEntries` (list, detail, dashboard recent-activity) **MUST** include
`.Where(a => a.TenantId == tenant.TenantId)` using the injected `ICurrentTenantResolver`. This is the #1 thing
the wiring/integration tests assert (`NFR-SEC-010`). The KPI counts over `Students`/`Codes`/`Enrollments` are
auto-filtered (those are tenant-owned) — do **not** add manual tenant `Where` there.

## Steps (design-anchored to `scrActivity` line 1291 + `scrDashboard` line 549)

### A1 — Application: feed DTO + action→category map + sensitive set
- `Application/Features/Audit/DTOs/AuditDtos.cs` — `AuditFeedItem` (contract §1). **No detail DTO / no
  `/audit/{id}` in 5A** — the design's drill-in is *navigate to the entity* via `targetType`+`targetId`.
- `Application/Features/Audit/AuditActionCategory.cs` — the `action → category` map (enum/dictionary):
  `approval | code | enrollment | session | question | device | staff | student | audit | other`. Enumerate the
  real action keys first: `grep -rn "AuditWriteRequest(" backend/src` **and** the domain `IAuditableDomainEvent`
  summaries (e.g. `StudentApproved/Rejected`, `CodeBatchGenerated`, `CodeDisabled/Enabled/Deleted`,
  `CodeRedeemed`, `EnrollmentCreated/Refunded`, `SessionPublished`, `Question*`, `StudentDeviceCleared`,
  `Staff*`, `StudentIdImageViewed`). Provide `CategoryOf(action)` + `ActionsInCategory(category)`.
- `Application/Features/Audit/SensitiveAuditActions.cs` — `{ "StudentIdImageViewed" }` (contract §4) + `Contains`.
  (No `AuditViewed` in 5A — there is no sensitive *view* screen.)

### A2 — Application: audit feed query (`Features/Audit/Queries/ListAudit/`)
- `ListAuditQuery(actorId?, actorType?, category?, from?, to?, period?, studentId?, sessionId?, entityType?,
  entityId?, page=1, pageSize=20, **bool includeSensitive**)` : `IRequest<PagedResult<AuditFeedItem>>`.
  `includeSensitive` is supplied by the endpoint (A5), **not** derived in the handler.
- `ListAuditValidator` — `page ≥ 1`, `pageSize ∈ [1,100]`, `from ≤ to` (mirror `ListCodesValidator`).
- `AuditFilters.cs` (mirror `CodeFilters`) — `actorId`/`actorType`; `category` ⇒ `EF.Constant(actions).Contains(a.Action)`
  via `ActionsInCategory` (keeps filtering in SQL for correct paging); `from`/`to` (resolve `period` → range);
  `studentId`/`sessionId`/`entityId` ⇒ `EntityId == value`, `entityType` ⇒ `EntityType == value`; when
  `!includeSensitive` add `!SensitiveAuditActions.Contains(a.Action)`.
- `ListAuditHandler(IAppDbContext db, ICurrentTenantResolver tenant)` —
  `db.AuditEntries.AsNoTracking().Where(a => a.TenantId == tenant.TenantId)` → `AuditFilters.Apply(...)` →
  `CountAsync` → `OrderByDescending(a => a.OccurredAtUtc).Skip/Take` → project via A3.

### A3 — Application: feed projector (`Features/Audit/AuditFeedProjector.cs`)
Mirror `CodeListProjector` (batch the joins; **`IgnoreQueryFilters`** for name lookups since ids came from
tenant-scoped rows). For a page of rows resolve:
- `actorName`: `System`→"System"; `Staff`→`Staff.DisplayName`; `Student`→`Student.FullName`.
- `targetLabel`: switch on `EntityType` → batched dictionary lookups: `Student`→`FullName`, `Session`→`Title`,
  `Code`→`Serial`, `Staff`→`DisplayName`, `Enrollment`→(student+session) or null; unknown → null.
- `category`: `AuditActionCategory.CategoryOf(action)`.
Expose `ToFeedItemsAsync(db, rows, ct)`. (Reused verbatim by the dashboard's recent-activity.)

### A4 — Application: dashboard query (`Features/Dashboard/Queries/GetDashboard/`)
- `GetDashboardQuery(string? Period, DateTimeOffset? From, DateTimeOffset? To) : IRequest<DashboardDto>`;
  `DTOs/DashboardDtos.cs` (`DashboardDto`, `EnrollmentDayDto { DateOnly Date; int Count }`).
- `GetDashboardHandler(IAppDbContext db, ICurrentTenantResolver tenant, TimeProvider clock)` — resolve period
  (`Period` `7d/30d/90d` → days; else `From/To`; default **30d**). KPI counts off the auto-filtered DbSets:
  `pendingApprovals`/`activeStudents` (`StudentStatus`), `codesUsed`/`codesActive` (`CodeStatus`),
  `revenueFromCodes = Codes.Where(Status==Used).SumAsync(c => c.Value)`. `enrollmentsByDay`: EF
  `GroupBy(e => e.EnrolledAtUtc.Date)` over the range → **fill empty days** to a continuous series in-memory;
  `enrollmentsTotal` = Σ. `recentActivity`: A3 projector over the top-7 tenant-filtered **non-sensitive** entries
  (`includeSensitive:false`) — **explicit tenant filter**.

### A5 — Api: endpoints (`Api/Endpoints/AuditEndpoints.cs`, `DashboardEndpoints.cs`)
Mirror `CodeEndpoints` (`IEndpointGroup`, `MapGroup`, `.RequirePermission(...)`, `.Produces<>`).
- `/api/audit`, `RequirePermission(Permission.AuditRead)`: `GET /` → `ListAuditQuery`. **Compute
  `includeSensitive` here** from the caller's `AuditReadSensitive` permission claim (reuse the claim check in
  `Api/Authorization/RequirePermission.cs`; factor a `HttpContext.HasPermission(Permission)` helper if absent),
  pass the bool in. Map `[FromQuery]` params per contract #1. `.Produces<PagedResult<AuditFeedItem>>()`.
- `/api/dashboard`, `RequirePermission(Permission.DashboardRead)`: `GET /` (`[FromQuery] period?, from?, to?`) →
  `GetDashboardQuery`; `.Produces<DashboardDto>()`.

### A6 — Tests (`tests/SalahBahazad.IntegrationTests`, mirror `SessionApiTests` + `SalahBahazadApiFactory`)
- **`NFR-SEC-010` tenant isolation (must-have):** seed audit rows for tenant A and tenant B; a tenant-B token
  gets **zero** of A's rows from `GET /api/audit`.
- **Scope:** Assistant token's feed omits `StudentIdImageViewed`; Teacher token includes it.
- **Filters:** assert `actorId/actorType/category/from-to(period)/studentId/sessionId/entityType/entityId` each
  narrow; paging + desc order; `category` maps to the right action set; `targetLabel`/`category` resolved.
- **Dashboard:** seed N students/codes/enrollments → `pendingApprovals/activeStudents/codesUsed/codesActive`
  exact; `revenueFromCodes` = Σ used-code values; `enrollmentsByDay` buckets sum to `enrollmentsTotal` over the
  period (incl. zero-filled empty days); `recentActivity` ≤ 7, tenant-scoped, excludes sensitive; period respected.
- **Default-deny:** anonymous → 401; a token without `AuditRead`/`DashboardRead` → 403.
- Unit (optional): `AuditActionCategory.CategoryOf` mapping; `SensitiveAuditActions.Contains`.

## Exit criteria
All four contract endpoints return the documented shapes; `dotnet build -c Release` + `dotnet test -c Release`
green; Scalar/OpenAPI shows new **Audit** + **Dashboard** groups; the tenant-isolation + scope tests pass.
Hand off to the wiring stream.

## Out of scope (defer — documented, not skipped)
- **Audit *detail* endpoint (`GET /api/audit/{id}` with before/after JSON + hash chain) + `AuditViewed`
  "watch-the-watchers" write** — the prototype has no such screen (drill-in = navigate to the entity). The raw
  before/after already lives in `AuditEntry`; expose it only if a future design asks. **Not in 5A.**
- **Attendance** endpoints/screens (`FR-ADM-ATT-*`) — scores are written by the 5B engine; `Attendance` columns
  are null today. → 5B.
- **Audit CSV export** — not required by `FR-ADM-AUD`; add later only if asked.
- StatCard trend **deltas** (demo-only in the prototype) — no trend math in 5A.

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

This is DESIGN-ANCHORED to the prototype (.claude/Salah Bahzad Teacher Portal/Admin Portal.dc.html — scrActivity
line 1291, scrDashboard line 549): the activity log is a feed of actor/action/target with a category icon, and
drill-in NAVIGATES to the entity (there is NO before/after-JSON detail endpoint and NO AuditViewed write in 5A).

Deliver: Features/Audit (AuditFeedItem DTO, AuditActionCategory action→category map, SensitiveAuditActions =
{StudentIdImageViewed}, ListAudit query/filters, AuditFeedProjector resolving actorName + targetLabel +
category), Features/Dashboard (GetDashboard returning 4 KPIs + codesUsed/codesActive + revenueFromCodes +
enrollmentsByDay series + 7 recentActivity), Api/Endpoints/AuditEndpoints + DashboardEndpoints, and
IntegrationTests covering tenant isolation, Assistant-vs-Teacher sensitive scoping, every filter (actor/
category/period/student/session), dashboard KPI + enrollments-series accuracy, and default-deny (401/403).

Green gate: `cd backend && dotnet build -c Release && dotnet test -c Release` (Docker required for Testcontainers).
Report the tenant-isolation and sensitive-scoping test results explicitly when done.
```
