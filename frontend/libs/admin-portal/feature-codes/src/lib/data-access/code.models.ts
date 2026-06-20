/**
 * Wire shapes for the Codes & enrollment slice — a faithful mirror of the FROZEN Phase 4 contract
 * (`docs/contracts/phase4-codes-enrollment.md`). Enums serialize as their string names
 * (JsonStringEnumConverter) so they are modelled as TS string-union types; `DateTimeOffset`
 * serializes as an ISO string; money is a `decimal` EGP rendered `EGP {value}` by the UI.
 */

/** Code lifecycle state (contract §0). The prototype's "Disabled" label = `Inactive`; soft-deleted
 * codes are hidden by the global query filter, not a queryable status. */
export type CodeStatus = 'Active' | 'Inactive' | 'Used';

/** Enrollment lifecycle state (contract §0). */
export type EnrollmentStatus = 'Active' | 'Expired' | 'Refunded';

/** How an enrollment was granted (contract §0). */
export type EnrollmentMethod = 'Code' | 'Unlock';

/** Generic server pagination envelope (shared shape with the other slices). */
export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

/**
 * One register row for the admin codes table (contract §1 `CodeListDto`, `scrCodes`). A code carries
 * **both** `sessionId` and `value` — it is redeemable only for its session when `value == session.price`.
 */
export interface CodeListDto {
  id: string;
  serial: string;
  value: number;
  status: CodeStatus;
  batchId: string;
  batchLabel: string;
  sessionId: string;
  sessionTitle: string;
  redeemedByStudentId: string | null;
  redeemedByStudentName: string | null;
  redeemedAtUtc: string | null;
  createdByName: string | null;
  createdAtUtc: string;
}

/** The "Batch ready" panel summary returned by generate (#2, contract §1 `CodeBatchDto`). No inline
 * code list — the Excel/CSV comes from the batch re-export (#4). */
export interface CodeBatchDto {
  batchId: string;
  label: string;
  sessionId: string;
  sessionTitle: string;
  value: number;
  quantity: number;
  createdAtUtc: string;
}

/** Result of unlock (#9) / refund (#10) / redeem (#12) (contract §1 `EnrollmentDto`). */
export interface EnrollmentDto {
  id: string;
  studentId: string;
  studentName: string;
  sessionId: string;
  sessionTitle: string;
  status: EnrollmentStatus;
  method: EnrollmentMethod;
  amount: number;
  codeId: string | null;
  codeSerial: string | null;
  enrolledAtUtc: string;
  /** Null when the session's validityDays == 0 (no expiry). */
  expiresAtUtc: string | null;
}

/**
 * One row of the session-detail "Enrolled students" tab (#8, contract §1 `EnrollmentListDto`).
 * `quizBestPercent` / `videosWatched` / `videosTotal` are Phase-5 placeholders (always 0 now).
 */
export interface EnrollmentListDto {
  enrollmentId: string;
  studentId: string;
  studentName: string;
  studentInitials: string;
  method: EnrollmentMethod;
  status: EnrollmentStatus;
  enrolledAtUtc: string;
  quizBestPercent: number;
  videosWatched: number;
  videosTotal: number;
}

/**
 * One row of the student-detail "Enrollments & transactions" tab (#11, contract §1
 * `StudentEnrollmentDto`). `amount` 0 renders as "Free".
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

/**
 * Lightweight student row for the unlock picker — reuses Phase 2 `GET /api/students?status=Active&search=`
 * (`StudentListDto` carries `fullName` + `phoneNumber`), mapped to `{ id, name, phone }` and rendered as
 * `{ value: id, label: name, description: phone }` exactly like `scrSessionDetail.unlockBody()`.
 */
export interface StudentSearchRow {
  id: string;
  name: string;
  phone: string;
}

/** A session option for the register filter + the generate combo (read from `/api/sessions`). */
export interface SessionOption {
  id: string;
  title: string;
}

/** Query params for the paged/filterable codes register (#1 / #3 share these filters). */
export interface CodeListQuery {
  search?: string;
  status?: CodeStatus | null;
  batchId?: string | null;
  sessionId?: string | null;
  page?: number;
  pageSize?: number;
}

/** Create body for a batch of codes (#2). `value` defaults to the session's current price (UI pre-fill). */
export interface GenerateCodesRequest {
  sessionId: string;
  value: number;
  quantity: number;
}
