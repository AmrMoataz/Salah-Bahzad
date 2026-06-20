import { AvatarStatus, PillVariant } from '@sb/shared/ui';
import { EnrollmentMethod, StudentStatus } from './data-access/student.models';

/** Two-letter initials for the avatar (mirrors the staff list helper). */
export function studentInitials(name: string): string {
  return name
    .split(' ')
    .filter(Boolean)
    .map((w) => w[0])
    .slice(0, 2)
    .join('')
    .toUpperCase();
}

/** Status → design-system pill variant. */
export function statusPill(status: StudentStatus): PillVariant {
  switch (status) {
    case 'Active':
      return 'success';
    case 'Pending':
      return 'warning';
    case 'Rejected':
      return 'danger';
    case 'Inactive':
      return 'neutral';
  }
}

/** Status → avatar status dot. */
export function statusDot(status: StudentStatus): AvatarStatus {
  switch (status) {
    case 'Active':
      return 'active';
    case 'Pending':
      return 'pending';
    default:
      return 'inactive';
  }
}

/** Enrollment method → pill variant (`Code` redemption vs manual `Unlock`). */
export function methodPill(method: EnrollmentMethod): PillVariant {
  return method === 'Unlock' ? 'warning' : 'info';
}

/** Egyptian-pound amount; "Free" when zero (enrollment transactions, matches the prototype). */
export function amount(value: number): string {
  return value > 0 ? `EGP ${value}` : 'Free';
}

/** Subject-accent key for a student's avatar — stable per id so the colours don't flicker.
 * Uses the design system's subject palette (see `ds/tokens/colors.css`). */
export function avatarSubject(id: string): string {
  const subjects = ['blue', 'green', 'purple', 'pink', 'mustard', 'orange', 'mint', 'red'];
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash + id.charCodeAt(i)) % subjects.length;
  return subjects[hash];
}

/** Relative "x ago" label from an ISO timestamp; `Never` when null/unparseable. */
export function relativeTime(iso: string | null): string {
  if (!iso) return 'Never';
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return 'Never';
  const minutes = Math.floor((Date.now() - then) / 60000);
  if (minutes < 1) return 'Just now';
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days < 7) return `${days}d ago`;
  return new Date(iso).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

/** Absolute date-time label for detail panels and audit rows. */
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
