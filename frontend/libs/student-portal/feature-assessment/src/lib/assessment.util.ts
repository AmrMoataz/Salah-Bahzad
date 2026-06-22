/**
 * Shared presentational helpers for the assessment screens (the runner + the answer-key review).
 * Kept inside `feature-assessment` (not promoted to `libs/student-portal/ui`) until a later phase
 * reuses them — module boundaries (`frontend/CLAUDE.md`).
 */

/** Format integer **seconds** as `M:SS` — the prototype's `fmt(sec)` (e.g. `42 → "0:42"`, `125 → "2:05"`). */
export function mmss(seconds: number | null | undefined): string {
  const s = Math.max(0, Math.floor(seconds ?? 0));
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
}

/** The MCQ option letter for a 0-based index (`0 → "A"`, `1 → "B"`, …). */
export function optionLetter(index: number): string {
  return String.fromCharCode(65 + index);
}

/** A quiz attempt's terminal status (the live union, mirrored here to avoid a data-access import). */
type AttemptStatus = 'InProgress' | 'Submitted' | 'Forfeited' | 'TimedOut';

/**
 * Derive the UI flag from a terminal attempt `status` — the §B review only carries `status`, so the
 * review header re-derives the Clean/Timeout/Forfeit pill the intro's `attempts[]` get for free (§A #1).
 */
export function quizFlagFromStatus(status: AttemptStatus): 'Clean' | 'Timeout' | 'Forfeit' {
  switch (status) {
    case 'TimedOut':
      return 'Timeout';
    case 'Forfeited':
      return 'Forfeit';
    default:
      return 'Clean'; // Submitted (or the never-reviewed InProgress)
  }
}

/** The `StatusPill` variant for an attempt flag — Clean reads neutral, Timeout warns, Forfeit alarms. */
export function quizFlagVariant(
  flag: 'Clean' | 'Timeout' | 'Forfeit',
): 'neutral' | 'warning' | 'danger' {
  return flag === 'Forfeit' ? 'danger' : flag === 'Timeout' ? 'warning' : 'neutral';
}
