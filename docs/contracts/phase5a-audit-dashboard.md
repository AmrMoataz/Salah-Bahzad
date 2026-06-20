# FROZEN CONTRACT — Phase 5A · Audit-log browser + Dashboard

> Status: **Frozen** · Created 2026-06-20 · Slice: Phase **5A** (the read-only reporting slice of Phase 5).
> **Design-anchored** to the prototype `.claude/Salah Bahzad Teacher Portal/Admin Portal.dc.html` —
> `scrDashboard()` (line 549) and `scrActivity()` (line 1291). The shapes below are derived **from those screens**,
> not invented. Both streams build to this; the wiring stream proves it with **zero drift**. Change here first.
>
> Satisfies: `FR-ADM-AUD-001..003`, `FR-PLAT-AUD-004/006` (activity log); `FR-ADM-DASH-001..003` (dashboard).

## 0. Ground rules (every endpoint)

- **Auth:** platform JWT (admin portal), `RequirePermission(...)`, default-deny. Anonymous → **401**;
  authenticated-but-missing-permission → **403**.
- **Permissions already exist + are already bundled** (`Permission.cs` + `PermissionCatalog.cs`) — **no edit in 5A**:
  `AuditRead` (Teacher+Assistant), `AuditReadSensitive` (**Teacher only**), `DashboardRead` (Teacher+Assistant).
- **Tenant isolation (`NFR-SEC-010`) — load-bearing.** `AuditEntry` is **not** `ITenantOwned`; the EF global filter
  does **NOT** scope it. Every audit read (activity list **and** dashboard recent-activity) **MUST** add
  `Where(a => a.TenantId == currentTenant.TenantId)` explicitly. KPI sources (Students/Codes/Enrollments) auto-filter.
- **Paging:** existing `PagedResult<T>` (`{ items, total, page, pageSize }`). **Dates** are ISO-8601.

## 1. The activity feed row — `AuditFeedItem` (the design's core shape)

`scrActivity` and the dashboard's "Recent activity" render the **same** row:
`«icon» **actor** action **target** · when`. The backend supplies the data; the **frontend presentation layer owns
the icon, accent color, and the action verb-phrase** (mirrors `code.presentation.ts`). So the row is:

```jsonc
// AuditFeedItem
{
  "id": "guid",
  "occurredAtUtc": "2026-06-20T12:34:56+00:00",
  "actorType": "Staff | Student | System",
  "actorRole": "Teacher | Assistant | null",
  "actorName": "string | null",     // the bold ACTOR ("Mariam Adel"; "System" for system)
  "action":   "string",             // RAW key — e.g. StudentApproved, CodeBatchGenerated, EnrollmentRefunded,
                                     //   SessionPublished, StudentDeviceCleared, StudentIdImageViewed
  "category": "approval | code | enrollment | session | question | device | staff | student | audit | other",
                                     // backend-derived from `action`; drives the filter + the icon/accent
  "summary":  "string | null",      // full readable sentence (fallback text + tooltip); the "where" in words
  "targetType": "string | null",    // = AuditEntry.EntityType (Student|Session|Code|Staff|…) — for the View link
  "targetId":   "guid | null",      // = AuditEntry.EntityId — for the View link
  "targetLabel":"string | null",    // resolved display name of the affected entity (the bold TARGET)
  "portal":   "admin | student | system | null",
  "ipAddress":"string | null"
}
```

**Frontend presentation map** (owns, not frozen): `category` → `{icon, accent}` and `action` → verb-phrase, matching
the prototype seed (e.g. `approval`→`check`/`green`, `code`→`ticket`/`blue`, `enrollment`→`unlock`/`mustard` or
`money`/`green` for refund, `session`→`book`/`purple`, `question`→`edit`/`blue`, `device`→`device`/`orange`,
`staff`→`shield`/`purple`, rejection→`x`/`red`). Unmapped actions fall back to `summary`.

**Drill-in = navigate to the affected entity** (`scrActivity` "View" → `student-detail`/`session-detail`/`codes`/
`staff` via `targetType`+`targetId`). **There is NO before/after-JSON detail endpoint in 5A** (the prototype has no
such screen). Raw before/after exists in `AuditEntry` and MAY be exposed later — out of scope here.

## 2. Endpoints

| # | Method & path | Permission | Returns | Notes |
|---|---|---|---|---|
| 1 | `GET /api/audit` | `AuditRead` | `PagedResult<AuditFeedItem>` | The activity log feed; also powers the per-student/per-session Activity tabs |
| 2 | `GET /api/dashboard` | `DashboardRead` | `DashboardDto` | 4 KPIs + enrollments series + recent-activity (7) |

### #1 `GET /api/audit`
Query params (design filter bar = **actor + category + period**; the rest power the entity Activity tabs):
`actorId?` (guid), `actorType?` (`Staff`|`Student`|`System`), `category?` (one of the §1 enum),
`from?`/`to?` (dates) **or** `period?` (`7d`|`30d`|`90d`, default `7d` on this screen),
`studentId?` (guid) / `sessionId?` (guid) / `entityType?` / `entityId?` (guid) — for `student-detail`/`session-detail`
Activity tabs (match `EntityId`/`EntityType`), `page=1`, `pageSize=20` (≤100). Order `OccurredAtUtc` **desc**.

- **Assistant scope (`FR-ADM-AUD-003`, shown in the prototype's "Scoped view" alert):** when the caller lacks
  `AuditReadSensitive`, exclude rows whose `action ∈ SensitiveAuditActions` (§4). Teachers see all.
- `category` filter maps to the design chips: Approvals→`approval`, Codes→`code`, Sessions→`session`,
  Devices→`device` (other categories surface only under "All actions"). Backend owns the `action → category` map.

### #2 `GET /api/dashboard`
Query: `period?` (`7d`|`30d`|`90d`, default `30d`) **or** `from?`/`to?`. Tenant-scoped counts.

```jsonc
// DashboardDto  (mirrors scrDashboard exactly)
{
  "pendingApprovals": 0,             // Students.Status == Pending   (StatCard "Pending approvals")
  "activeStudents":   0,             // Students.Status == Active     (StatCard "Active students")
  "codesUsed":   0,                  // Codes.Status == Used   ┐ StatCard "Codes used / active" = used / active
  "codesActive": 0,                  // Codes.Status == Active ┘
  "revenueFromCodes": 0,             // Σ Code.Value where Status==Used (decimal EGP) — StatCard "Revenue (by code)"
  "periodFrom": "2026-05-21T00:00:00+00:00",
  "periodTo":   "2026-06-20T12:34:56+00:00",
  "enrollmentsByDay": [              // the "Enrollments — last N days" bar chart (daily granularity;
    { "date": "2026-06-18", "count": 12 }  //   the frontend buckets to weekly for 30d/90d exactly like the prototype)
  ],
  "enrollmentsTotal": 0,             // Σ enrollmentsByDay (chart caption "N total")
  "recentActivity": [ /* AuditFeedItem, up to 7, tenant-filtered, NON-sensitive */ ]
}
```

- Quick actions (Review approvals / Generate codes **[Teacher-only]** / Create session / Open attendance) are
  **client-only**, role-gated — no endpoint. ("Open attendance" targets the 5B screen.)
- StatCard **delta** badges ("3 today", "12.4%") are **demo-only** in the prototype → **out of scope** for 5A
  (no trend math). Omit deltas or render none.

## 3. Backend KPI / series sources (all tenant-scoped; auto-filtered except AuditEntry)
`pendingApprovals`=`Students(Pending)`, `activeStudents`=`Students(Active)`, `codesUsed`=`Codes(Used)`,
`codesActive`=`Codes(Active)`, `revenueFromCodes`=`Σ Code.Value where Used`, `enrollmentsByDay`=group
`Enrollments` by `EnrolledAtUtc::date` within `[periodFrom,periodTo]`, `recentActivity`=top-7 `AuditEntry`
(**explicit tenant filter**, non-sensitive, desc).

## 4. SensitiveAuditActions (Assistant-scoped subset — the "who-read-what")
```
StudentIdImageViewed   // backend GetStudentIdImageUrlHandler.cs (Phase-2 ID-image signed-URL read)
```
5A surfaces no new sensitive **read** screen, so it introduces **no** `AuditViewed` action. The single sensitive
action today is the ID-image view; hide it from Assistants (matches the prototype's "Scoped view" alert). Append
future sensitive-read actions here.

## 5. What is frozen vs. owned by a stream
- **Frozen:** the two routes + permissions; the `AuditFeedItem` and `DashboardDto` field names/types; the
  tenant-filter rule; `category` enum values; `SensitiveAuditActions`.
- **Backend owns:** `action → category` map, `targetLabel` resolution, query internals/indexes, daily-bucket SQL.
- **Frontend owns:** icon/accent per `category`, action→verb-phrase, weekly bucketing of `enrollmentsByDay` for
  30/90d, layout/labels, and whether the period control is `period` enum or a `from/to` range (endpoint accepts both).
```
