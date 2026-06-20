import { PillVariant } from '@sb/shared/ui';
import { CodeStatus, EnrollmentMethod } from './data-access/code.models';

/** Code status → design-system pill variant (matches the prototype's pill colours). */
export function codeStatusPill(status: CodeStatus): PillVariant {
  switch (status) {
    case 'Active':
      return 'success';
    case 'Used':
      return 'info';
    case 'Inactive':
      return 'neutral';
  }
}

/** Code status → UI label. The prototype renders `Inactive` as "Disabled". */
export function codeStatusLabel(status: CodeStatus): string {
  return status === 'Inactive' ? 'Disabled' : status;
}

/** Enrollment method → pill variant (`Code` vs manual `Unlock`). */
export function methodPill(method: EnrollmentMethod): PillVariant {
  return method === 'Unlock' ? 'warning' : 'info';
}

/** Egyptian-pound label, always `EGP {value}` (codes always carry a value). */
export function egp(value: number): string {
  return `EGP ${value}`;
}

/** Egyptian-pound amount; "Free" when zero (enrollment transactions, matches the prototype). */
export function amount(value: number): string {
  return value > 0 ? `EGP ${value}` : 'Free';
}

/** Absolute date-time label for table cells. */
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

/** Compact date-only label (e.g. the register's "Created" column). */
export function dateOnly(iso: string | null): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}
