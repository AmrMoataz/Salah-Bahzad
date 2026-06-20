/**
 * Wire shapes for the Students slice — a faithful mirror of the backend DTOs
 * (`Application/Features/Students/DTOs/StudentDtos.cs`). Enums serialize as their
 * names (JsonStringEnumConverter); `DateTimeOffset` serializes as an ISO string.
 */

/** Lifecycle state of a student account (FR-ADM-STU-001..006). */
export type StudentStatus = 'Pending' | 'Active' | 'Rejected' | 'Inactive';

/**
 * Minimal grade reference for the list filter and the contact-edit picker. Read from the taxonomy
 * endpoint directly (rather than importing the taxonomy feature) to keep this slice self-contained
 * and within the Nx feature→feature boundary rule.
 */
export interface GradeRef {
  id: string;
  name: string;
}

/** Generic server pagination envelope (shared shape with the other slices). */
export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

/** A student list row for the admin triage table (FR-ADM-STU-001). */
export interface StudentListItem {
  id: string;
  fullName: string;
  phoneNumber: string;
  status: StudentStatus;
  gradeId: string;
  gradeName: string | null;
  cityName: string | null;
  schoolName: string;
  parentPhonePrimary: string;
  /** Label of the bound device (or a placeholder) when one is active; null when no device is bound. */
  activeDeviceSummary: string | null;
  createdAtUtc: string;
  lastSeenAtUtc: string | null;
}

/** The bound/cleared device shown on the detail screen (FR-PLAT-DEV-006). The token hash is never exposed. */
export interface StudentDevice {
  id: string;
  fingerprintSummary: string | null;
  boundAtUtc: string;
  isActive: boolean;
  clearedAtUtc: string | null;
  clearReason: string | null;
}

/** The 360° student record for the detail screen (FR-ADM-STU-002). */
export interface StudentDetail {
  id: string;
  fullName: string;
  phoneNumber: string;
  status: StudentStatus;
  rejectionReason: string | null;
  gradeId: string;
  gradeName: string | null;
  cityId: string;
  cityName: string | null;
  regionId: string;
  regionName: string | null;
  schoolName: string;
  parentPhonePrimary: string;
  parentPhoneSecondary: string | null;
  /** Whether an ID image exists; the bytes are fetched on demand via a signed URL. */
  hasIdImage: boolean;
  termsVersion: string | null;
  termsAcceptedAtUtc: string | null;
  lastSeenAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  activeDevice: StudentDevice | null;
}

/** A short-lived signed URL for the student's ID-verification image (FR-PLAT-AST-003). */
export interface StudentIdImageUrl {
  url: string;
  expiresAtUtc: string;
}

/** An audit row projected for the student history tabs (FR-ADM-STU-008). */
export interface StudentAuditEntry {
  id: string;
  action: string;
  summary: string | null;
  actorId: string | null;
  actorRole: string | null;
  actorType: string;
  ipAddress: string | null;
  occurredAtUtc: string;
}

// ── Enrollment (Phase 4) ─────────────────────────────────────────────────────────────────────
// Shapes mirror the FROZEN Phase 4 contract (`docs/contracts/phase4-codes-enrollment.md`). Defined
// here (not imported from feature-codes) to respect the Nx feature→feature boundary.

/** Enrollment lifecycle state. */
export type EnrollmentStatus = 'Active' | 'Expired' | 'Refunded';

/** How an enrollment was granted. */
export type EnrollmentMethod = 'Code' | 'Unlock';

/**
 * One row of the student-detail "Enrollments & transactions" tab (contract §1 `StudentEnrollmentDto`,
 * endpoint #11). `amount` 0 renders as "Free".
 */
export interface StudentEnrollmentDto {
  enrollmentId: string;
  sessionId: string;
  sessionTitle: string;
  method: EnrollmentMethod;
  status: EnrollmentStatus;
  amount: number;
  enrolledAtUtc: string;
  codeSerial: string | null;
}

/** Query params for the paged/filterable student list (FR-ADM-STU-001). */
export interface StudentListQuery {
  search?: string;
  status?: StudentStatus | null;
  gradeId?: string | null;
  page?: number;
  pageSize?: number;
}

/** Payload to correct a student's grade and contact numbers (FR-ADM-STU-005). */
export interface UpdateStudentContactRequest {
  gradeId: string;
  phoneNumber: string;
  parentPhonePrimary: string;
  parentPhoneSecondary?: string | null;
}
