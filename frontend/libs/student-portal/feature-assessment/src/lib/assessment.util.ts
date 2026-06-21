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
