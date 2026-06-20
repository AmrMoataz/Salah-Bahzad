/**
 * Presentation helpers for the Dashboard (Phase 5A), matched to the prototype `scrDashboard`
 * (`.claude/Salah Bahzad Teacher Portal/Admin Portal.dc.html`). Pure functions (no Angular deps).
 *
 * The activity-feed helpers (`feedVisual`/`accentBg`/`actionPhrase`/`relativeTime`/…) are a deliberate
 * copy of `feature-audit`'s `audit.presentation.ts`: features don't import each other in this repo
 * (see `code.service.ts`), and the plan (B5) sanctions duplicating this tiny map rather than coupling
 * the two libs. Keep both copies in sync with the prototype seed (lines 1588-1597). A future refactor
 * could lift them into `@sb/shared/util`.
 */
import { AuditCategory, AuditFeedItem, DashboardPeriod, EnrollmentDayPoint } from './data-access/dashboard.models';

export type FeedAccent = 'blue' | 'green' | 'purple' | 'mustard' | 'orange' | 'red' | 'neutral';

/** Outline icons (24×24 grid, ~1.8px stroke) — KPI + quick-action + feed icons from the prototype. */
export type DashIconName =
  | 'inbox'
  | 'users'
  | 'ticket'
  | 'money'
  | 'plus'
  | 'clipboard'
  | 'check'
  | 'unlock'
  | 'book'
  | 'edit'
  | 'device'
  | 'shield'
  | 'user'
  | 'eye'
  | 'x'
  | 'dot';

const ICON_PATHS: Record<DashIconName, string> = {
  inbox: 'M3 12h5l2 3h4l2-3h5M5 5h14l3 7v6a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2v-6z',
  users: 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75',
  ticket: 'M3 9a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2 2 2 0 0 0 0 4 2 2 0 0 1-2 2H5a2 2 0 0 1-2-2 2 2 0 0 0 0-4zM9 7v10',
  money: 'M12 2v20M17 6H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6',
  plus: 'M12 5v14M5 12h14',
  clipboard: 'M9 3h6v2H9zM8 4H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2h-2M9 12l2 2 4-4',
  check: 'M20 6L9 17l-5-5',
  unlock: 'M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2zM7 11V7a5 5 0 0 1 9.9-1',
  book: 'M4 19.5A2.5 2.5 0 0 1 6.5 17H20M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z',
  edit: 'M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7M18.5 2.5a2.12 2.12 0 0 1 3 3L12 15l-4 1 1-4z',
  device: 'M5 2h14a2 2 0 0 1 2 2v16a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2zM11 18h2',
  shield: 'M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z',
  user: 'M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8',
  eye: 'M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7zM12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6',
  x: 'M18 6L6 18M6 6l12 12',
  dot: 'M12 9.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5',
};

/** Full inline `<svg>` for an icon (rendered via a trusted-HTML bypass in the component). */
export function dashIconSvg(name: DashIconName, size = 18): string {
  const d = ICON_PATHS[name] ?? ICON_PATHS.dot;
  return (
    `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" ` +
    `stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="${d}"/></svg>`
  );
}

export function accentBg(accent: FeedAccent): string {
  return accent === 'neutral' ? 'var(--sb-neutral-100)' : `var(--sb-subject-${accent}-bg)`;
}
export function accentFg(accent: FeedAccent): string {
  return accent === 'neutral' ? 'var(--sb-text-muted)' : `var(--sb-subject-${accent}-deep)`;
}

const CATEGORY_ICON: Record<AuditCategory, DashIconName> = {
  approval: 'check',
  code: 'ticket',
  enrollment: 'unlock',
  session: 'book',
  question: 'edit',
  device: 'device',
  staff: 'shield',
  student: 'user',
  audit: 'eye',
  other: 'dot',
};

const CATEGORY_ACCENT: Record<AuditCategory, FeedAccent> = {
  approval: 'green',
  code: 'blue',
  enrollment: 'mustard',
  session: 'purple',
  question: 'blue',
  device: 'orange',
  staff: 'purple',
  student: 'blue',
  audit: 'neutral',
  other: 'neutral',
};

/** Icon + accent for a feed row (with the rejection→x/red and refund→money/green overrides). */
export function feedVisual(
  item: Pick<AuditFeedItem, 'action' | 'category'>,
): { icon: DashIconName; accent: FeedAccent } {
  const action = item.action ?? '';
  if (/reject/i.test(action)) return { icon: 'x', accent: 'red' };
  if (item.category === 'enrollment' && /refund/i.test(action)) return { icon: 'money', accent: 'green' };
  return {
    icon: CATEGORY_ICON[item.category] ?? 'dot',
    accent: CATEGORY_ACCENT[item.category] ?? 'neutral',
  };
}

const ACTION_PHRASE: Record<string, string> = {
  StudentApproved: 'approved',
  StudentRejected: 'rejected',
  StudentDeactivated: 'deactivated',
  StudentReactivated: 'reactivated',
  StudentDeviceCleared: 'cleared device for',
  StudentIdImageViewed: 'viewed the ID image of',
  CodeBatchGenerated: 'generated codes for',
  CodeDisabled: 'disabled a code for',
  CodeEnabled: 'enabled a code for',
  CodeDeleted: 'deleted a code for',
  EnrollmentUnlocked: 'unlocked a session for',
  SessionUnlocked: 'unlocked a session for',
  EnrollmentRefunded: 'refunded enrollment for',
  EnrollmentGranted: 'enrolled',
  SessionPublished: 'published',
  SessionCreated: 'created',
  SessionUpdated: 'edited',
  SessionArchived: 'archived',
  QuestionBankEdited: 'edited question bank for',
  QuestionCreated: 'added a question to',
  QuestionUpdated: 'edited a question in',
  StaffCreated: 'created staff account',
  StaffUpdated: 'updated staff account',
  StaffDeactivated: 'deactivated staff account',
};

export function actionPhrase(action: string): string | null {
  return ACTION_PHRASE[action] ?? null;
}

export function humanizeAction(action: string): string {
  const spaced = action.replace(/([a-z0-9])([A-Z])/g, '$1 $2');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}

export function actorLabel(item: Pick<AuditFeedItem, 'actorName'>): string {
  return item.actorName ?? 'System';
}

export function relativeTime(iso: string | null, now: Date = new Date()): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';

  const diffMs = now.getTime() - d.getTime();
  const sec = Math.round(diffMs / 1000);
  if (sec < 45) return 'Just now';
  const min = Math.round(sec / 60);
  if (min < 60) return `${min} minute${min === 1 ? '' : 's'} ago`;
  const hr = Math.round(min / 60);
  if (hr < 24) return `${hr} hour${hr === 1 ? '' : 's'} ago`;

  const startOf = (x: Date): number => new Date(x.getFullYear(), x.getMonth(), x.getDate()).getTime();
  const dayDiff = Math.round((startOf(now) - startOf(d)) / 86_400_000);
  const hhmm = d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit', hour12: false });
  if (dayDiff <= 1) return `Yesterday, ${hhmm}`;
  if (dayDiff < 7) return `${dayDiff} days ago`;
  return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

// ── Enrollments chart bucketing ────────────────────────────────────────────────────
/** One bar of the enrollments chart. */
export interface ChartBar {
  value: number;
  label: string;
}
export interface ChartData {
  bars: ChartBar[];
  granularity: 'daily' | 'weekly';
}

const PERIOD_DAYS: Record<DashboardPeriod, number> = { '7d': 7, '30d': 30, '90d': 90 };
const DAY_NAMES = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

const utcKey = (d: Date): string =>
  `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}-${String(d.getUTCDate()).padStart(2, '0')}`;

/**
 * Buckets the daily series exactly like the prototype (lines 569-575): a dense day-per-bar series for
 * `7d` (labelled by weekday), groups of 5 → **6** bars for `30d`, groups of 7 → **13** bars for `90d`
 * (labelled by the group's first date). The server may omit zero-count days, so we densify the range
 * `[periodTo − (N−1) days, periodTo]` first, mapping counts by date.
 */
export function bucketEnrollments(
  byDay: readonly EnrollmentDayPoint[],
  period: DashboardPeriod,
  periodTo: string,
): ChartData {
  const days = PERIOD_DAYS[period] ?? 30;
  const counts = new Map<string, number>(byDay.map((p) => [p.date.slice(0, 10), p.count] as [string, number]));

  const end = new Date(periodTo);
  const endUtc = new Date(Date.UTC(end.getUTCFullYear(), end.getUTCMonth(), end.getUTCDate()));
  const daily = Array.from({ length: days }, (_, i) => {
    const d = new Date(endUtc);
    d.setUTCDate(endUtc.getUTCDate() - (days - 1 - i));
    return { date: d, value: counts.get(utcKey(d)) ?? 0 };
  });

  const fmt = (d: Date): string => `${d.getUTCMonth() + 1}/${d.getUTCDate()}`;
  const sum = (slice: { value: number }[]): number => slice.reduce((a, x) => a + x.value, 0);

  if (days === 7) {
    return {
      granularity: 'daily',
      bars: daily.map((x) => ({ value: x.value, label: DAY_NAMES[x.date.getUTCDay()] })),
    };
  }
  if (days === 30) {
    return {
      granularity: 'weekly',
      bars: Array.from({ length: 6 }, (_, g) => {
        const sl = daily.slice(g * 5, (g + 1) * 5);
        return { value: sum(sl), label: fmt(sl[0].date) };
      }),
    };
  }
  return {
    granularity: 'weekly',
    bars: Array.from({ length: 13 }, (_, g) => {
      const sl = daily.slice(g * 7, (g + 1) * 7);
      return sl.length ? { value: sum(sl), label: fmt(sl[0].date) } : null;
    }).filter((b): b is ChartBar => b !== null),
  };
}
