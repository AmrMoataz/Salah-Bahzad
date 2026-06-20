/**
 * Wire shapes for the Dashboard (Phase 5A) — a faithful mirror of the FROZEN contract
 * (`docs/contracts/phase5a-audit-dashboard.md` §2 `DashboardDto`). Satisfies `FR-ADM-DASH-001..003`.
 * `DateTimeOffset` serializes as ISO-8601; `category` serializes as a lowercase string.
 *
 * The `AuditFeedItem`/`AuditCategory` shapes are intentionally re-declared here (identical to the
 * `feature-audit` lib) so the dashboard stays self-contained — features don't import each other
 * (matches the repo's decoupling convention; see `code.service.ts`). Keep both copies in sync.
 */

export type ActorType = 'Staff' | 'Student' | 'System';

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

/** One activity-feed row (contract §1) — rendered by the dashboard's "Recent activity" card. */
export interface AuditFeedItem {
  id: string;
  occurredAtUtc: string;
  actorType: ActorType;
  actorRole: string | null;
  actorName: string | null;
  action: string;
  category: AuditCategory;
  summary: string | null;
  targetType: string | null;
  targetId: string | null;
  targetLabel: string | null;
  portal: 'admin' | 'student' | 'system' | null;
  ipAddress: string | null;
}

/** One daily point of the "Enrollments — last N days" series (contract §2). */
export interface EnrollmentDayPoint {
  date: string;
  count: number;
}

/** The dashboard payload (contract §2 `DashboardDto`) — 4 KPIs + enrollments series + recent activity. */
export interface DashboardSummary {
  pendingApprovals: number;
  activeStudents: number;
  codesUsed: number;
  codesActive: number;
  revenueFromCodes: number;
  periodFrom: string;
  periodTo: string;
  enrollmentsByDay: EnrollmentDayPoint[];
  enrollmentsTotal: number;
  recentActivity: AuditFeedItem[];
}

/** The dashboard period control (contract §2; default `30d`). */
export type DashboardPeriod = '7d' | '30d' | '90d';

/** Query for `GET /api/dashboard` — `period` OR a `from`/`to` range. */
export interface DashboardQuery {
  period?: DashboardPeriod | null;
  from?: string | null;
  to?: string | null;
}
