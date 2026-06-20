/**
 * Presentation layer for the activity feed (Phase 5A) — mirrors `code.presentation.ts`. The contract
 * leaves the **icon, accent and action verb-phrase to the frontend** (contract §1, §5); this file owns
 * them, matched to the prototype seed (`.claude/Salah Bahzad Teacher Portal/Admin Portal.dc.html`
 * lines 1588-1597). All functions are pure (no Angular deps) so they unit-test trivially and can be
 * reused by the dashboard's "Recent activity" feed.
 */
import { AuditCategory, AuditFeedItem } from './data-access/audit.models';

/** Subject-accent palette (the `--sb-subject-*` chips), plus a `neutral` fallback (no subject token). */
export type FeedAccent = 'blue' | 'green' | 'purple' | 'mustard' | 'orange' | 'red' | 'neutral';

/** Outline icons reused from the prototype's `icon()` map (24×24 grid, ~1.8px stroke). */
export type AuditIconName =
  | 'check'
  | 'ticket'
  | 'unlock'
  | 'money'
  | 'book'
  | 'edit'
  | 'device'
  | 'shield'
  | 'user'
  | 'eye'
  | 'x'
  | 'dot';

const ICON_PATHS: Record<AuditIconName, string> = {
  check: 'M20 6L9 17l-5-5',
  ticket: 'M3 9a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2 2 2 0 0 0 0 4 2 2 0 0 1-2 2H5a2 2 0 0 1-2-2 2 2 0 0 0 0-4zM9 7v10',
  unlock: 'M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2zM7 11V7a5 5 0 0 1 9.9-1',
  money: 'M12 2v20M17 6H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6',
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
export function auditIconSvg(name: AuditIconName, size = 16): string {
  const d = ICON_PATHS[name] ?? ICON_PATHS.dot;
  return (
    `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" ` +
    `stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="${d}"/></svg>`
  );
}

/** `category → icon` (contract §1 presentation map). */
const CATEGORY_ICON: Record<AuditCategory, AuditIconName> = {
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

/** `category → accent` (contract §1 presentation map). */
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

/**
 * The icon + accent for a row. Two action-driven overrides on top of the category map (contract §1):
 * any rejection renders `x`/`red`, and an enrollment **refund** renders `money`/`green`
 * (vs. `unlock`/`mustard` for other enrollments).
 */
export function feedVisual(
  item: Pick<AuditFeedItem, 'action' | 'category'>,
): { icon: AuditIconName; accent: FeedAccent } {
  const action = item.action ?? '';
  if (/reject/i.test(action)) return { icon: 'x', accent: 'red' };
  if (item.category === 'enrollment' && /refund/i.test(action)) return { icon: 'money', accent: 'green' };
  return {
    icon: CATEGORY_ICON[item.category] ?? 'dot',
    accent: CATEGORY_ACCENT[item.category] ?? 'neutral',
  };
}

/** Accent → chip background CSS var (subject token, or neutral surface for `neutral`). */
export function accentBg(accent: FeedAccent): string {
  return accent === 'neutral' ? 'var(--sb-neutral-100)' : `var(--sb-subject-${accent}-bg)`;
}

/** Accent → chip foreground CSS var (deep subject ink, or muted text for `neutral`). */
export function accentFg(accent: FeedAccent): string {
  return accent === 'neutral' ? 'var(--sb-text-muted)' : `var(--sb-subject-${accent}-deep)`;
}

/**
 * RAW action key → verb phrase, keyed on the contract's documented action keys (§1) plus their
 * siblings, matched to the prototype seed. Returns `null` for unmapped actions so the caller falls
 * back to `summary` (a full readable sentence guaranteed by the contract).
 */
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

/** Verb phrase for an action, or `null` when unmapped (caller falls back to `summary`). */
export function actionPhrase(action: string): string | null {
  return ACTION_PHRASE[action] ?? null;
}

/** Last-resort label for an unmapped action with no `summary`: "StudentApproved" → "Student approved". */
export function humanizeAction(action: string): string {
  const spaced = action.replace(/([a-z0-9])([A-Z])/g, '$1 $2');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}

/** The bold actor label; system actions have no `actorName`. */
export function actorLabel(item: Pick<AuditFeedItem, 'actorName'>): string {
  return item.actorName ?? 'System';
}

/**
 * Relative "when" label matching the prototype copy ("8 minutes ago", "Yesterday, 18:20", "2 days
 * ago", then an absolute date). `now` is injectable for deterministic tests.
 */
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

/**
 * The router path the row's "View" navigates to, from `targetType` + `targetId` (contract §1 drill-in).
 * Per-entity detail screens take the id; the code register and staff list are list routes. Returns
 * `null` when there is no linked entity (caller shows a "No linked entity" toast).
 */
export function targetRoute(targetType: string | null, targetId: string | null): string[] | null {
  switch ((targetType ?? '').toLowerCase()) {
    case 'student':
      return targetId ? ['/students', targetId] : null;
    case 'session':
      return targetId ? ['/sessions', targetId] : null;
    case 'code':
      return ['/codes'];
    case 'staff':
      return ['/staff'];
    default:
      return null;
  }
}
