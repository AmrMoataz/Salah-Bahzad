/**
 * Wire shapes for the Audit-log browser (Phase 5A) — a faithful mirror of the FROZEN contract
 * (`docs/contracts/phase5a-audit-dashboard.md` §1). Enums serialize as their string names
 * (JsonStringEnumConverter) so they are modelled as TS string-union types; `DateTimeOffset`
 * serializes as an ISO-8601 string.
 *
 * Satisfies the read side of `FR-ADM-AUD-001..003`, `FR-PLAT-AUD-004/006`.
 *
 * NOTE: there is **no** `beforeJson`/`afterJson`/`hash` here — 5A exposes no detail endpoint
 * (contract §1). Drill-in = *navigate to the affected entity* via `targetType` + `targetId`.
 */

/** Who performed the action (contract §1). */
export type ActorType = 'Staff' | 'Student' | 'System';

/**
 * Backend-derived bucket for an audit row (contract §1). Drives the filter chips and the
 * frontend's icon/accent (see `audit.presentation.ts`). The backend owns the `action → category` map.
 */
export type AuditCategory =
  | 'approval'
  | 'code'
  | 'enrollment'
  | 'session'
  | 'question'
  | 'device'
  | 'staff'
  | 'student'
  | 'audit'
  | 'other';

/** Generic server pagination envelope (shared shape with the other slices). */
export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

/**
 * One activity-feed row (contract §1 `AuditFeedItem`). Rendered identically by `scrActivity` and the
 * dashboard's "Recent activity": `«icon» **actor** action **target** · when`. The presentation layer
 * (not the contract) owns the icon, accent, and the action verb-phrase.
 */
export interface AuditFeedItem {
  id: string;
  occurredAtUtc: string;
  actorType: ActorType;
  /** `Teacher | Assistant | null`. */
  actorRole: string | null;
  /** The bold ACTOR ("Mariam Adel"; "System" for system actions). */
  actorName: string | null;
  /** RAW key — e.g. `StudentApproved`, `CodeBatchGenerated`, `EnrollmentRefunded`, `SessionPublished`. */
  action: string;
  category: AuditCategory;
  /** Full readable sentence — fallback text + the "where" in words. */
  summary: string | null;
  /** = `AuditEntry.EntityType` (Student|Session|Code|Staff|…) — drives the "View" link. */
  targetType: string | null;
  /** = `AuditEntry.EntityId` — drives the "View" link. */
  targetId: string | null;
  /** Resolved display name of the affected entity (the bold TARGET). */
  targetLabel: string | null;
  portal: 'admin' | 'student' | 'system' | null;
  ipAddress: string | null;
}

/**
 * Query params for the paged/filterable activity feed (contract §2 #1). The design filter bar uses
 * `actorType` + `category` + `period`; the rest power the per-student/per-session Activity tabs.
 * Empty values are omitted by the service.
 */
export interface AuditListQuery {
  actorId?: string | null;
  actorType?: ActorType | null;
  category?: AuditCategory | null;
  /** `7d | 30d | 90d` (default `7d` on the activity screen) — mutually exclusive with `from`/`to`. */
  period?: AuditPeriod | null;
  from?: string | null;
  to?: string | null;
  /** Entity-tab filters (match `EntityId`/`EntityType`). */
  studentId?: string | null;
  sessionId?: string | null;
  entityType?: string | null;
  entityId?: string | null;
  page?: number;
  pageSize?: number;
}

/** The activity-screen period control (contract §2 #1). */
export type AuditPeriod = '7d' | '30d' | '90d';
