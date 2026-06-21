// Wire models for the student sign-in surface. These mirror the FROZEN backend contract
// (`docs/IMPLEMENTATION-PLAN-student-s0-backend.md` §1.2 / §1.4) field-for-field — do not
// add fields the API does not send (email/avatar are S6's `/api/me/profile` concern).

/** Student account lifecycle (`Student.Status`). */
export type StudentStatus = 'Active' | 'Pending' | 'Rejected' | 'Inactive';

/**
 * The four machine `reason` codes a `403` carries on a blocked sign-in (§1.4). The router maps
 * each to a status screen instead of a form error. (`device_not_recognized` is the one-device
 * enforcement; the rest are the status gate.)
 */
export type StudentBlockReason =
  | 'account_pending'
  | 'account_rejected'
  | 'account_inactive'
  | 'device_not_recognized';

/** The device currently bound to the student (§1.2 `BoundDeviceInfo`). */
export interface BoundDeviceInfo {
  /** Human summary built from the fingerprint, e.g. "Android · Chrome" (may be absent). */
  summary: string | null;
  boundAtUtc: string;
}

/** Minimal student identity returned by the exchange (§1.2 `StudentInfo`). */
export interface StudentInfo {
  id: string;
  fullName: string;
  status: StudentStatus;
  boundDevice: BoundDeviceInfo | null;
}

/**
 * Response of `POST /api/auth/student/exchange` (success) and of `POST /api/auth/refresh`
 * when the refresh token is a student's (§1.2). Parallels the staff `AuthTokenResponse`,
 * but carries `student` instead of `staff`.
 */
export interface StudentAuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
  student: StudentInfo;
}

/** ProblemDetails shape for a blocked sign-in: machine `reason` + readable `detail` (§1.4). */
export interface StudentAuthProblem {
  reason?: StudentBlockReason;
  detail?: string;
}

export interface StudentAuthState {
  student: StudentInfo | null;
  accessToken: string | null;
  refreshToken: string | null;
  isLoading: boolean;
  /** Form-level error (bad credentials / no student access / network) — shown on the login card. */
  error: string | null;
  /** Set on a blocked `403`; drives the status screen instead of erroring. */
  status: StudentBlockReason | null;
  /** Readable detail for the blocked status (for `account_rejected` this is the `RejectionReason`). */
  statusDetail: string | null;
}
