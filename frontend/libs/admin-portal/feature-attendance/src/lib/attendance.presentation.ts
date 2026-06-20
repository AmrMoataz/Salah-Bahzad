/**
 * Presentation layer for the Attendance & review screens (Phase 5B-1) — mirrors `audit.presentation.ts`.
 * The contract leaves the **behaviour-event icon/accent + label, the progress rendering, and the
 * pending-column treatment to the frontend** (contract §F "Frontend owns"); this file owns them,
 * matched to the prototype seed (`.claude/Salah Bahzad Teacher Portal/Admin Portal.dc.html`,
 * `scrReview` lines 1126-1134 and `scrAttendance` line 1267). All functions are pure (no Angular deps)
 * so they unit-test trivially.
 */
import { BehaviourEventType, ReviewOption } from './data-access/attendance.models';

/** Subject-accent palette (the `--sb-subject-*` chips), plus a `neutral` fallback (no subject token). */
export type BehaviourAccent = 'blue' | 'green' | 'red' | 'mustard' | 'neutral';

/** Outline icons reused from the prototype's `icon()` map (24×24 grid, ~1.8px stroke). */
export type BehaviourIconName = 'logout' | 'check' | 'x' | 'navigate' | 'dot';

const ICON_PATHS: Record<BehaviourIconName, string> = {
  // `Entered` reuses the prototype's `enter`→`logout` glyph (scrReview line 1133).
  logout: 'M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9',
  check: 'M20 6L9 17l-5-5',
  x: 'M18 6L6 18M6 6l12 12',
  // `Navigated` (moved between questions) — a left/right transfer glyph.
  navigate: 'M16 3l4 4-4 4M20 7H8M8 21l-4-4 4-4M4 17h12',
  dot: 'M12 9.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5',
};

/** Full inline `<svg>` for an icon (rendered via a trusted-HTML bypass in the component). */
export function behaviourIconSvg(name: BehaviourIconName, size = 14): string {
  const d = ICON_PATHS[name] ?? ICON_PATHS.dot;
  return (
    `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" ` +
    `stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="${d}"/></svg>`
  );
}

/**
 * `behaviour type → { icon, accent }` (contract §C / §F presentation map), matched to `scrReview`
 * lines 1131-1134: `Entered`→logout/green, `Answered`→check/blue, `Left`→x/red, `Navigated`→navigate/mustard.
 */
const BEHAVIOUR_VISUAL: Record<BehaviourEventType, { icon: BehaviourIconName; accent: BehaviourAccent }> = {
  Entered: { icon: 'logout', accent: 'green' },
  Answered: { icon: 'check', accent: 'blue' },
  Left: { icon: 'x', accent: 'red' },
  Navigated: { icon: 'navigate', accent: 'mustard' },
};

/** The icon + accent for a behaviour row (falls back to a neutral dot for any unknown type). */
export function behaviourVisual(
  type: BehaviourEventType,
): { icon: BehaviourIconName; accent: BehaviourAccent } {
  return BEHAVIOUR_VISUAL[type] ?? { icon: 'dot', accent: 'neutral' };
}

/** Accent → chip background CSS var (subject token, or neutral surface for `neutral`). */
export function accentBg(accent: BehaviourAccent): string {
  return accent === 'neutral' ? 'var(--sb-neutral-100)' : `var(--sb-subject-${accent}-bg)`;
}

/** Accent → chip foreground CSS var (deep subject ink, or muted text for `neutral`). */
export function accentFg(accent: BehaviourAccent): string {
  return accent === 'neutral' ? 'var(--sb-text-muted)' : `var(--sb-subject-${accent}-deep)`;
}

// ── Attendance matrix helpers ──────────────────────────────────────────────────────────────────

/** Percent-or-dash: a pending/absent metric (`null`) renders as the prototype's em-dash. */
export function percentOrDash(value: number | null | undefined): string {
  return value == null ? '—' : `${value}%`;
}

/** Video completion as a clamped 0-100 percent for the `Progress` bar (0 when there are no videos). */
export function videoPercent(watched: number, total: number): number {
  if (!total || total <= 0) return 0;
  return Math.min(100, Math.max(0, Math.round((watched / total) * 100)));
}

/** `seconds → mm:ss` for the review header's "Time spent" stat (1104 → "18:24"). */
export function mmss(seconds: number | null | undefined): string {
  const s = Math.max(0, Math.floor(seconds ?? 0));
  const m = Math.floor(s / 60);
  const r = s % 60;
  return `${m}:${String(r).padStart(2, '0')}`;
}

/** ISO timestamp → `HH:MM:SS` (local) for the behaviour timeline's mono clock ("09:05:01"). */
export function clockTime(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleTimeString(undefined, {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false,
  });
}

/** Two-letter initials for the avatar (mirrors the students/staff helpers). */
export function initialsOf(name: string): string {
  return name
    .split(' ')
    .filter(Boolean)
    .map((w) => w[0])
    .slice(0, 2)
    .join('')
    .toUpperCase();
}

/** Subject-accent key for a student's avatar — stable per id so the colours don't flicker
 * (mirrors `feature-students`' `avatarSubject`; uses the design system's subject palette). */
export function avatarSubject(id: string): string {
  const subjects = ['blue', 'green', 'purple', 'pink', 'mustard', 'orange', 'mint', 'red'];
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash + id.charCodeAt(i)) % subjects.length;
  return subjects[hash];
}

// ── Review question-card option highlighting ────────────────────────────────────────────────────

/** The visual state of a reviewed option (drives the green-check / red-× treatment). */
export type OptionState = 'correct' | 'picked-wrong' | 'neutral';

/**
 * An option's review state (contract §C, prototype `scrReview` lines 1126-1127): the **correct** option
 * is always `correct` (green + check); the student's pick, when it is *not* the correct option, is
 * `picked-wrong` (red + ×); everything else is `neutral`.
 */
export function optionState(option: ReviewOption, selectedOptionId: string | null): OptionState {
  if (option.isCorrect) return 'correct';
  if (selectedOptionId != null && option.id === selectedOptionId) return 'picked-wrong';
  return 'neutral';
}

/** Option-state → `{ bg, border }` CSS vars (the prototype's success/danger/neutral surfaces). */
export function optionStyle(state: OptionState): { bg: string; border: string } {
  switch (state) {
    case 'correct':
      return { bg: 'var(--sb-success-bg)', border: 'var(--sb-success-border)' };
    case 'picked-wrong':
      return { bg: 'var(--sb-danger-bg)', border: 'var(--sb-danger-border)' };
    default:
      return { bg: 'var(--sb-surface)', border: 'var(--sb-border)' };
  }
}

/** `0 → "A"`, `1 → "B"`, … — the option-letter prefix shown before each choice. */
export function optionLetter(index: number): string {
  return String.fromCharCode(65 + index);
}
