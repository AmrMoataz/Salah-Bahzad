// Wire models for the authenticated **weekly plan** surface (the personalized Home). These mirror the
// FROZEN contract (`docs/contracts/student-home-weekly-plan.md`) field-for-field — §A.1 (`MyPlanDto`,
// `kpis`, `focus`, `MyPlanStepDto`, `MyPlanRecentDto`) and §B (the four string-union enums). Enums cross
// the wire as string names (`JsonStringEnumConverter`), so they are modelled as TS string unions. The
// wire shape equals the model field-for-field, so the response IS the model. Do NOT add fields the API
// does not send (the plan is derived state — there are no editable fields, no fabricated due dates).

/** §B — the action type. `Redeem` = open the code modal (next session / re-enroll after expiry). */
export type MyPlanStepKind = 'Quiz' | 'Videos' | 'Assignment' | 'Redeem';

/** §B — derived completion. There is **no** `Overdue` value — urgency rides {@link MyPlanDueState}. */
export type MyPlanStepStatus = 'Pending' | 'Completed';

/** §B — from enrollment expiry only. `ExpiringSoon` = active & `ExpiresAtUtc <= now + 14d`. */
export type MyPlanDueState = 'None' | 'ExpiringSoon' | 'Expired';

/** §B — `Navigate` carries an in-portal `route`; `Redeem` carries none (frontend opens `/redeem`). */
export type MyPlanActionType = 'Navigate' | 'Redeem';

/** §A.1 — the KPI roll-up, summed over the caller's enrolled set. */
export interface MyPlanKpis {
  /** enrollments that are active (not expired) AND not Completed. */
  activeSessions: number;
  /** Σ watched over active enrollments (the §E counters). */
  videosWatched: number;
  /** Σ `SessionVideo` count over active enrollments. */
  videosTotal: number;
  /** `videosTotal == 0 ? 0 : round(100 × videosWatched / videosTotal)`. */
  overallProgressPercent: number;
  /** enrollments whose completion state == Completed (incl. expired-but-finished). */
  completedSessions: number;
}

/** §A.1 — the focus session (Path A); `null` when the caller has no active, incomplete enrollment. */
export interface MyPlanFocus {
  sessionId: string;
  title: string;
  specializationName: string | null;
  /** short-lived signed R2 URL (same pattern as `/me/sessions`); `null` if none. */
  thumbnailUrl: string | null;
  progressPercent: number;
  /** `null` == no-expiry session. */
  expiresAtUtc: string | null;
  isExpired: boolean;
  /** `ceil((expiresAtUtc - now)/1d)`; `null` when no expiry; never sent negative (use {@link isExpired}). */
  expiresInDays: number | null;
  dueState: MyPlanDueState;
}

/** §A.1 — one actionable (or completed, kept for the "Completed" sub-list) plan step. */
export interface MyPlanStep {
  /**
   * Stable identity for this step within the plan, so the UI can track/animate it:
   * `quiz:{userQuizId}` | `videos:{sessionId}` | `assignment:{userAssignmentId}` | `redeem:{sessionId}`.
   */
  key: string;
  kind: MyPlanStepKind;
  /** server-composed, honest label (NOT a fabricated due date). */
  title: string;
  /** context line ("3 of 8 lessons watched", "Get a code from your teacher", …); may be null. */
  subtitle: string | null;
  sessionId: string;
  sessionTitle: string;
  /** for the per-row accent chip (same accent system as the catalogue/dashboard). */
  specializationName: string | null;

  status: MyPlanStepStatus;
  /** true when an earlier gate blocks this step (videos blocked until the quiz passes). */
  blocked: boolean;
  /** user-safe reason when blocked ("Pass the quiz to unlock the videos"); null otherwise. */
  blockedReason: string | null;

  /** real-deadline signal — derived ONLY from enrollment expiry (the one honest deadline). */
  dueState: MyPlanDueState;
  /** the step's session expiry (mirrors `focus.expiresAtUtc` for focus-session steps). */
  expiresAtUtc: string | null;

  /** for chunked/multi-item steps (Videos: watched/total; Assignment: answered/total). null for Quiz/Redeem. */
  progress: { done: number; total: number } | null;

  /** what the frontend does — an in-portal route or a redeem intent; NEVER a fabricated external URL. */
  action: {
    type: MyPlanActionType;
    /** in-portal deep link (e.g. `/sessions/{sessionId}`); `null` when `type == "Redeem"`. */
    route: string | null;
    /** CTA text, server-supplied — render verbatim: "Start" | "Continue" | "Watch" | "Open" | "Redeem". */
    label: string;
  };
}

/** §A.1 — the "Recently enrolled" rail (the UI renders "Added N days ago" client-side). */
export interface MyPlanRecent {
  sessionId: string;
  title: string;
  specializationName: string | null;
  enrolledAtUtc: string;
}

/** §A.1 — `200 MyPlanDto`: the caller's current weekly plan (always 200; an empty/onboarding plan otherwise). */
export interface MyPlanDto {
  /** ISO-8601 week label the plan frames (server clock, UTC), e.g. "2026-W25". */
  isoWeek: string;
  /** the Monday..Sunday window of `isoWeek` (for the "This week" header). */
  weekStartUtc: string;
  weekEndUtc: string;
  /** when this snapshot was computed (the cache stamp; lets the UI show freshness). */
  generatedAtUtc: string;

  /** == `steps.length` (Pending + Completed) — the "This week" bar's denominator. */
  totalSteps: number;
  /** == count(steps where `status == "Completed"`) — the bar's numerator. */
  completedSteps: number;
  /** == count(steps where `dueState == "Expired" && status != "Completed"`). */
  overdueSteps: number;

  kpis: MyPlanKpis;
  /** the focus session (Path A); `null` for the onboarding/all-done state. */
  focus: MyPlanFocus | null;
  /** ordered (§E.3); length ≤ 7. */
  steps: MyPlanStep[];
  /** `EnrolledAtUtc` DESC, ≤ 5. */
  recentlyEnrolled: MyPlanRecent[];
}
