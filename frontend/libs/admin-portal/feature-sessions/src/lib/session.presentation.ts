import { PillVariant } from '@sb/shared/ui';
import { SessionStatus, VideoProcessingStatus } from './data-access/session.models';

/** The design's subject-accent palette keys (see `_design-tokens.scss`). */
const SUBJECTS = ['blue', 'green', 'purple', 'orange', 'mint', 'pink', 'mustard', 'red'] as const;

/** Session status → design-system pill variant. */
export function statusPill(status: SessionStatus): PillVariant {
  switch (status) {
    case 'Published':
      return 'success';
    case 'Draft':
      return 'warning';
    case 'Archived':
      return 'neutral';
  }
}

/** Video transcode status → pill variant (Phase 3 shows processing state only). */
export function videoStatusPill(status: VideoProcessingStatus): PillVariant {
  switch (status) {
    case 'Ready':
      return 'success';
    case 'Processing':
      return 'info';
    case 'Pending':
      return 'warning';
    case 'Failed':
      return 'danger';
  }
}

/** Video length as MM:SS once transcoded; a status hint while it is still processing (FR-PLAT-VID-003). */
export function videoLength(video: { lengthSeconds: number; processingStatus: VideoProcessingStatus }): string {
  if (video.processingStatus !== 'Ready') return 'Processing…';
  const total = Math.max(0, Math.round(video.lengthSeconds));
  const minutes = Math.floor(total / 60);
  const seconds = total % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}

/**
 * Stable subject-accent key for a string (specialization id/name) — matches the prototype's
 * `specAccent`: the icon tiles and tags stay a consistent colour per specialization.
 */
export function subjectAccent(key: string | null | undefined): string {
  const text = key ?? '';
  let hash = 0;
  for (let i = 0; i < text.length; i++) hash = (hash + text.charCodeAt(i)) % SUBJECTS.length;
  return SUBJECTS[hash];
}

/** Egyptian-pound price label; "Free" when zero (matches the prototype). */
export function money(value: number): string {
  return value > 0 ? `EGP ${value}` : 'Free';
}

/** Human file size from a byte count. */
export function fileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

/** Three-letter type badge from a file name/content-type (e.g. "PDF", "PNG"). */
export function fileKind(fileName: string, contentType?: string): string {
  const fromName = fileName.includes('.') ? fileName.split('.').pop() : '';
  const raw = (fromName || contentType?.split('/').pop() || 'file').toUpperCase();
  return raw.slice(0, 4);
}

/** Absolute date-time label for detail panels. */
export function dateTime(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}
