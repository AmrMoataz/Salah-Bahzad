/**
 * Presentation helpers for the personalized Home (the weekly study plan), matched to the Student
 * Portal Home mock + DS tokens. Pure functions (no Angular deps).
 *
 * The icon/accent helpers are a deliberate **replication** of the admin dashboard's
 * `dashboard.presentation.ts` (`dashIconSvg` / `accentBg` / `accentFg`) and the day-diff maths is a
 * local replication of its `relativeTime`: features don't import each other in this repo (the
 * student-portal can't depend on an admin lib), and the plan (`IMPLEMENTATION-PLAN-student-home-
 * frontend.md` §F4) sanctions duplicating these tiny maps rather than coupling the libs. A future
 * refactor could lift them into `@sb/shared/util`.
 */
import { MyPlanStepKind } from '@sb/student-portal/data-access';

/** Accent palette keys shared with the dashboard stat-card (`--sb-subject-{x}-bg|-deep`). */
export type HomeAccent =
  | 'blue'
  | 'green'
  | 'purple'
  | 'mustard'
  | 'mint'
  | 'orange'
  | 'pink'
  | 'red'
  | 'neutral';

/** Outline icons (24×24 grid, ~1.8px stroke) — KPI widget glyphs + list affordances from the mock. */
export type HomeIconName =
  | 'tv'
  | 'play'
  | 'chart'
  | 'check-square'
  | 'book'
  | 'chevron';

const ICON_PATHS: Record<HomeIconName, string> = {
  // KPI widgets
  tv: 'M3 5h18a1 1 0 0 1 1 1v11a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1zM8 21h8',
  play: 'M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20M10 8.5l5.5 3.5L10 15.5z',
  chart: 'M3 3v18h18M7 14l3.5-4 3 2.5L18 8',
  'check-square': 'M9 11l2.5 2.5L20 5M20 11v7a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h9',
  // list affordances
  book: 'M4 19.5A2.5 2.5 0 0 1 6.5 17H20M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z',
  chevron: 'M9 6l6 6-6 6',
};

/** Full inline `<svg>` for an icon (rendered via a trusted-HTML bypass in the component). */
export function homeIconSvg(name: HomeIconName, size = 18): string {
  const d = ICON_PATHS[name] ?? ICON_PATHS.book;
  return (
    `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" ` +
    `stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="${d}"/></svg>`
  );
}

/** Tint background for an accent chip/icon-square. */
export function accentBg(accent: string): string {
  return accent === 'neutral' ? 'var(--sb-neutral-100)' : `var(--sb-subject-${accent}-bg)`;
}
/** Deep foreground for an accent chip/icon-square. */
export function accentFg(accent: string): string {
  return accent === 'neutral' ? 'var(--sb-text-muted)' : `var(--sb-subject-${accent}-deep)`;
}
/** Base (saturated) colour for an accent — used for outlined-CTA borders. */
export function accentBase(accent: string): string {
  return accent === 'neutral' ? 'var(--sb-border-strong)' : `var(--sb-subject-${accent})`;
}

/** The colored "type" pill label per plan-step kind, mirroring the mock (Video / Assignment / Quiz / Renew). */
const STEP_KIND_LABEL: Record<MyPlanStepKind, string> = {
  Quiz: 'Quiz',
  Videos: 'Video',
  Assignment: 'Assignment',
  Redeem: 'Renew',
};
export function stepTypeLabel(kind: MyPlanStepKind): string {
  return STEP_KIND_LABEL[kind] ?? 'Task';
}

/** Subject-accent per plan-step kind (drives the type-pill tint AND the outlined CTA accent). */
const STEP_KIND_ACCENT: Record<MyPlanStepKind, HomeAccent> = {
  Quiz: 'purple',
  Videos: 'blue',
  Assignment: 'green',
  Redeem: 'red',
};
export function stepTypeAccent(kind: MyPlanStepKind): HomeAccent {
  return STEP_KIND_ACCENT[kind] ?? 'neutral';
}

/**
 * Stable subject accent per **specialization** (keyed on the name) — ported (not imported) from the
 * catalogue's `SessionThumb` so a given specialization tints the same colour across portals.
 */
const SUBJECT_ACCENTS: HomeAccent[] = ['blue', 'green', 'purple', 'orange', 'mint', 'pink', 'mustard', 'red'];
export function subjectAccent(name: string | null): HomeAccent {
  const key = name ?? '';
  let hash = 0;
  for (let i = 0; i < key.length; i++) hash = (hash + key.charCodeAt(i)) % SUBJECT_ACCENTS.length;
  return SUBJECT_ACCENTS[hash];
}

// ── Relative-time helpers (local replication — NOT imported from the dashboard) ──────────────────

/** `ceil((iso - now)/1d)`; `null` for no-expiry/invalid. Mirrors the contract's `expiresInDays`. */
export function daysUntil(iso: string | null, now: Date = new Date()): number | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return null;
  return Math.ceil((d.getTime() - now.getTime()) / 86_400_000);
}

/**
 * The **only** time pressure rendered in the plan (contract §0/§E.3): the enrollment-expiry deadline,
 * worded like the mock's amber "Due in Nd" pill. Used for `dueState == 'ExpiringSoon'` rows.
 */
export function dueLabel(iso: string | null, now: Date = new Date()): string {
  const n = daysUntil(iso, now);
  if (n === null) return 'Expiring soon';
  if (n <= 0) return 'Due today';
  return `Due in ${n}d`;
}

/**
 * Expired enrollment deadline, worded like the mock's red "Overdue by Nd" pill. Used for
 * `dueState == 'Expired'` rows. Computed from the same real `expiresAtUtc` — no fabricated date.
 */
export function overdueLabel(iso: string | null, now: Date = new Date()): string {
  if (!iso) return 'Overdue';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return 'Overdue';
  const days = Math.floor((now.getTime() - d.getTime()) / 86_400_000);
  return days >= 1 ? `Overdue by ${days}d` : 'Overdue';
}

/** "Added N days ago" for the recently-enrolled list (calendar-day diff, like the dashboard). */
export function addedAgo(iso: string, now: Date = new Date()): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const startOf = (x: Date): number => new Date(x.getFullYear(), x.getMonth(), x.getDate()).getTime();
  const days = Math.round((startOf(now) - startOf(d)) / 86_400_000);
  if (days <= 0) return 'Added today';
  if (days === 1) return 'Added yesterday';
  return `Added ${days} days ago`;
}

/** Time-of-day greeting prefix ("Good morning/afternoon/evening"), matching the mock's hero. */
export function greetingPrefix(now: Date = new Date()): string {
  const h = now.getHours();
  if (h < 12) return 'Good morning';
  if (h < 18) return 'Good afternoon';
  return 'Good evening';
}
