# FROZEN CONTRACT — Phase 5A · Audit-log browser + Dashboard

> Status: **Frozen** · Created 2026-06-20 · Slice: Phase **5A** (the read-only reporting slice of Phase 5).
> Both the backend and frontend streams build to **exactly** these shapes; the wiring stream proves them with
> **zero drift**. If a field must change, change it **here first** and tell both streams.
>
> Satisfies: `FR-ADM-AUD-001..003`, `FR-PLAT-AUD-004/006` (audit browser); `FR-ADM-DASH-001..003`,
> `FR-PLAT-ATT-003`-adjacent KPIs (dashboard). Read alongside `docs/01-functional-platform-shared.md` §13 and
> `docs/02-functional-admin-portal.md` §B/§L.

## 0. Ground rules (apply to every endpoint)

- **Auth:** platform JWT (admin portal). Server enforces granular permissions via `RequirePermission(...)`,
  **default-deny**. Anonymous → **401**; authenticated-but-missing-permission → **403**.
- **Permissions already exist and are already bundled** (`Domain/Enums/Permission.cs` +
  `Application/Features/Auth/PermissionCatalog.cs`) — **no catalog edit in 5A**:
  - `AuditRead` (900) — Teacher + Assistant. `AuditReadSensitive` (901) — **Teacher only**.
  - `DashboardRead` (1000) — Teacher + Assistant.
- **Tenant isolation (`NFR-SEC-010`) — the load-bearing rule of this slice.** `AuditEntry` is **not**
  `ITenantOwned`, so the EF global query filter does **NOT** scope it. Every audit read (incl. the dashboard's
  recent-activity) **MUST** add `Where(a => a.TenantId == currentTenant.TenantId)` explicitly. The KPI sources
  (`Students`/`Codes`/`Enrollments`) are tenant-owned and auto-filter.
- **Paging:** reuse the existing `PagedResult<T>` (`{ items, total, page, pageSize }`).
- **Dates** are ISO-8601 strings (`DateTimeOffset`). `from`/`to` are inclusive day bounds.

## 1. Endpoints

| # | Method & path | Permission | Returns | Notes |
|---|---|---|---|---|
| 1 | `GET /api/audit` | `AuditRead` | `PagedResult<AuditListDto>` | Filterable feed; Assistant scope hides sensitive rows |
| 2 | `GET /api/audit/{id:guid}` | `AuditRead` | `AuditDetailDto` | Drill-in (before/after + hash chain); sensitive-aware 404; sensitive view is itself audited |
| 3 | `GET /api/audit/facets` *(optional)* | `AuditRead` | `AuditFacetsDto` | Distinct `actions[]` + `entityTypes[]` in tenant, for filter dropdowns. MAY be replaced by client-side constants. |
| 4 | `GET /api/dashboard` | `DashboardRead` | `DashboardDto` | KPI snapshot + recent-activity feed |

### #1 `GET /api/audit`

Query params (all optional except paging defaults):
`actorType` (`Staff`|`Student`|`System`), `actorId` (guid), `action` (string, exact),
`entityType` (string, exact), `entityId` (guid), `studentId` (guid), `sessionId` (guid),
`from` (date), `to` (date), `search` (string — case-insensitive `Summary` contains),
`page` (int = 1), `pageSize` (int = 20, clamp ≤ 100). Order: `OccurredAtUtc` **descending**.

- `studentId`/`sessionId` are convenience aliases that match `EntityId == value` (the entry's primary entity).
  Deeper cross-entity correlation (e.g. every code/enrollment about a student) is a later refinement, **not** 5A.
- **Assistant scope (`FR-ADM-AUD-003`):** when the caller lacks `AuditReadSensitive`, exclude rows whose
  `Action ∈ SensitiveAuditActions` (see §3).

### #2 `GET /api/audit/{id:guid}`

- Returns `AuditDetailDto` for one entry **in the caller's tenant**.
- **404** if the id is not in the tenant, **or** the entry is sensitive and the caller lacks
  `AuditReadSensitive` (do not reveal existence).
- **`FR-PLAT-AUD-006` "watch the watchers":** when the entry is sensitive **and** the caller is permitted to see
  it, write exactly **one** explicit `AuditViewed` entry (via `IAuditWriter`) attributed to the staff actor —
  `Action: "AuditViewed", EntityType: "AuditEntry", EntityId: <viewed id>, Summary: "<actor> viewed audit
  entry <id> (<action>)"`. Non-sensitive drill-ins are **not** re-audited (avoids feedback noise).

### #4 `GET /api/dashboard`

Query: `from?`/`to?` (dates). Default period = **last 30 days** (UTC, `to` = now). Counts are tenant-scoped.

- `pendingApprovals` = `Students` where `Status == Pending`.
- `activeStudents` = `Students` where `Status == Active`.
- `codes` = `{ used: Status==Used, active: Status==Active, total: all-non-deleted }` (soft-deleted hidden by
  the global filter).
- `enrollmentsInPeriod` = `Enrollments` where `EnrolledAtUtc ∈ [periodFrom, periodTo]`.
- `revenueFromCodes` = Σ `Code.Value` where `Status == Used` (redeemed codes; single clean source).
- `recentActivity` = top **8** `AuditListDto` (explicit tenant filter, **non-sensitive**, desc).
- Quick actions are **client-only** and role-gated — no endpoint.

## 2. DTOs (field-for-field)

```jsonc
// AuditListDto
{
  "id": "guid",
  "occurredAtUtc": "2026-06-20T12:34:56.789+00:00",
  "actorType": "Staff | Student | System",
  "actorRole": "Teacher | Assistant | null",
  "actorName": "string | null",      // resolved display name; "System" for system actor
  "action": "string",                // e.g. StudentApproved, CodeBatchGenerated, CodeRedeemed, EnrollmentRefunded
  "entityType": "string",            // e.g. Student, Code, Enrollment, Session, Staff, AuditEntry
  "entityId": "guid | null",
  "summary": "string | null",
  "portal": "admin | student | system | null",
  "ipAddress": "string | null"
}

// AuditDetailDto  (= AuditListDto + the following)
{
  "actorId": "guid | null",
  "beforeJson": "string | null",     // raw JSON snapshot (pretty-printed client-side)
  "afterJson": "string | null",
  "deviceId": "string | null",
  "prevHash": "string | null",
  "hash": "string | null"
}

// AuditFacetsDto (optional, #3)
{ "actions": ["string"], "entityTypes": ["string"] }

// DashboardDto (#4)
{
  "pendingApprovals": 0,
  "activeStudents": 0,
  "codes": { "used": 0, "active": 0, "total": 0 },
  "enrollmentsInPeriod": 0,
  "periodFrom": "2026-05-21T00:00:00+00:00",
  "periodTo":   "2026-06-20T12:34:56+00:00",
  "revenueFromCodes": 0,             // decimal (EGP)
  "recentActivity": [ /* AuditListDto, up to 8 */ ]
}

// PagedResult<T> (existing — do not redefine)
{ "items": [ /* T */ ], "total": 0, "page": 1, "pageSize": 20 }
```

## 3. SensitiveAuditActions (the Assistant-scoped subset)

The "read of sensitive data / who-read-what" entries. Confirmed against the codebase:

```
StudentIdImageViewed   // backend GetStudentIdImageUrlHandler.cs — Phase-2 ID-image signed-URL read
AuditViewed            // this slice — a sensitive audit entry was drilled into
```

Backend keeps this as one constant set (`SensitiveAuditActions`) used by both #1 (list exclusion) and #2
(detail 404). Teachers (`AuditReadSensitive`) bypass the exclusion. If later phases add sensitive reads
(e.g. paid-material downloads), append their action strings here.

## 4. What is frozen vs. owned by a stream

- **Frozen:** the four routes, their permissions, query params, and the DTO field names/types above; the
  `SensitiveAuditActions` set; the explicit-tenant-filter requirement.
- **Backend owns:** handler/projector/filter internals, index usage, the exact `AuditViewed` summary text,
  whether #3 ships or is deferred to client constants.
- **Frontend owns:** layout, labels, chip colors, the drill-in surface (drawer vs modal), and whether the
  dashboard period is a fixed 30-day or a user-selectable range (the endpoint accepts `from`/`to` either way).
```
