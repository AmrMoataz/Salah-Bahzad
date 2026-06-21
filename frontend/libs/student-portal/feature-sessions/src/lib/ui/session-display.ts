import type {
  MySessionState,
  MySessionVideo,
  VideoLockState,
} from '@sb/student-portal/data-access';

/**
 * Shared, pure display helpers for the My-Sessions hub + session detail. Kept framework-free (no
 * Angular) so they're trivially unit-testable and reused across the tile, list rows, the playlist,
 * and the spotlight hero. The chip/badge **variants** map to the design-system tints
 * (`--sb-*-bg/-fg/-border`) the row/chip components render.
 */

/** Tint key shared by the expiry chip + the lock badge (maps to the DS status tints). */
export type ChipVariant = 'success' | 'warning' | 'danger' | 'info' | 'neutral';

/**
 * Subject-accent palette keys (`--sb-subject-{key}-*`). **Order + the running-sum hash mirror the
 * admin portal's `subjectAccent` and the catalogue's `SessionThumb`** so a given specialization tints
 * the same colour everywhere.
 */
export const SUBJECT_ACCENTS = [
  'blue',
  'green',
  'purple',
  'orange',
  'mint',
  'pink',
  'mustard',
  'red',
] as const;
export type SubjectAccent = (typeof SUBJECT_ACCENTS)[number];

/** Stable subject accent keyed on the specialization name (consistent across cards + both portals). */
export function accentFor(key: string | null | undefined): SubjectAccent {
  const k = key ?? '';
  let hash = 0;
  for (let i = 0; i < k.length; i++) hash = (hash + k.charCodeAt(i)) % SUBJECT_ACCENTS.length;
  return SUBJECT_ACCENTS[hash];
}

/** A small rotation of friendly mascot poses for the tile fallback, keyed deterministically. */
const TILE_MASCOTS = ['salah-mascot.png', 'salah-passed.png', 'super-salah.png'] as const;

/** Stable mascot for a session tile (the prototype's `SessionThumb` mascot), keyed on the name. */
export function mascotFor(key: string | null | undefined): string {
  const k = key ?? '';
  let hash = 0;
  for (let i = 0; i < k.length; i++) hash = (hash + k.charCodeAt(i)) % TILE_MASCOTS.length;
  return `/assets/${TILE_MASCOTS[hash]}`;
}

/** Integer **seconds** → `MM:SS` (the 5C ffprobe-computed length). `0`/unknown → `"—:—"`. */
export function formatDuration(seconds: number): string {
  if (!seconds || seconds < 0) return '—:—';
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
}

/** Humanise a byte count for the materials list (B / KB / MB). */
export function humanizeBytes(bytes: number): string {
  if (!bytes || bytes <= 0) return '0 KB';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** "{watched} of {count} videos" — the progress label on the hero + list rows. */
export function videosLabel(watched: number, count: number): string {
  return `${watched} of ${count} ${count === 1 ? 'video' : 'videos'} watched`;
}

/** The per-row CTA label by completion state (§E.2): Start / Continue / Review. */
export function ctaLabel(state: MySessionState): 'Start' | 'Continue' | 'Review' {
  switch (state) {
    case 'NotStarted':
      return 'Start';
    case 'Completed':
      return 'Review';
    default:
      return 'Continue';
  }
}

/** Progress-bar tint: accent-green when complete, primary while in progress, grey when not started. */
export function progressVariant(state: MySessionState): 'success' | 'primary' | 'info' {
  if (state === 'Completed') return 'success';
  if (state === 'InProgress') return 'primary';
  return 'info';
}

const DAY_MS = 86_400_000;

/** The expiry chip label + variant (§E.2): Expired (danger) / Expires in N days (warning ≤14d) / No expiry. */
export function expiryInfo(
  expiresAtUtc: string | null,
  isExpired: boolean,
  now: number = Date.now(),
): { label: string; variant: ChipVariant } {
  if (isExpired) return { label: 'Expired', variant: 'danger' };
  if (expiresAtUtc === null) return { label: 'No expiry', variant: 'neutral' };

  const days = Math.ceil((new Date(expiresAtUtc).getTime() - now) / DAY_MS);
  const variant: ChipVariant = days <= 14 ? 'warning' : 'neutral';
  if (days <= 0) return { label: 'Expires today', variant: 'warning' };
  if (days === 1) return { label: 'Expires in 1 day', variant };
  return { label: `Expires in ${days} days`, variant };
}

/** Whether a session is "expiring soon" — the filter predicate (§A.1/§E.2): non-expired, within 14 days. */
export function isExpiringSoon(
  s: { expiresAtUtc: string | null; isExpired: boolean },
  now: number = Date.now(),
): boolean {
  if (s.isExpired || s.expiresAtUtc === null) return false;
  const t = new Date(s.expiresAtUtc).getTime();
  return t > now && t <= now + 1 * DAY_MS;
}

/**
 * The per-video access badge (§E.3 → the prototype colours). `Exhausted` → "0 views left" (red);
 * `Expired`/`QuizLocked`/`NotReady` → "Locked" (grey); `Playable` → "{n} of {m} views" (green).
 */
export function lockBadge(video: Pick<MySessionVideo, 'lockState' | 'accessRemaining' | 'accessAllowed'>): {
  label: string;
  variant: ChipVariant;
} {
  switch (video.lockState) {
    case 'Exhausted':
      return { label: '0 views left', variant: 'danger' };
    case 'Playable':
      return {
        label: `${video.accessRemaining} of ${video.accessAllowed} views`,
        variant: 'success',
      };
    default:
      // Expired | QuizLocked | NotReady — all "Locked" (grey).
      return { label: 'Locked', variant: 'neutral' };
  }
}

/** Whether a video can be played (predicts the gate; pressing Play still calls the real gate). */
export function isPlayable(lockState: VideoLockState): boolean {
  return lockState === 'Playable';
}
