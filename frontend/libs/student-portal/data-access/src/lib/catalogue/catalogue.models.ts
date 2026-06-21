// Wire models for the authenticated catalogue surface. These mirror the FROZEN contract
// (`docs/contracts/student-s2-catalogue-enroll.md`) field-for-field — §A.2 (`CatalogueSessionDto`),
// §C (the `enrollmentState` union) and §B.2 (`EnrollmentDto`). Enums cross the wire as string names
// (`JsonStringEnumConverter`), so they are modelled as TS string unions. Do not add fields the API
// does not send.

/**
 * The caller's own state for a catalogue session (§C.1). `Expired` is **derived** server-side from
 * `ExpiresAtUtc` vs now — the writer never flips `Status` to `Expired`. Drives the card CTA:
 * `Enrolled` → Open; everything else → Enroll (a re-redeem extends in place for Expired/Refunded).
 */
export type EnrollmentState = 'NotEnrolled' | 'Enrolled' | 'Expired' | 'Refunded';

/** A published session as shown on the catalogue card (contract §A.2 `CatalogueSessionDto`). */
export interface CatalogueSession {
  id: string;
  title: string;
  description: string | null;
  /** decimal EGP; `0` renders as "Free". */
  price: number;
  /** short-lived signed R2 URL; `null` when there is no thumbnail (the card shows a placeholder). */
  thumbnailUrl: string | null;
  gradeId: string;
  gradeName: string | null;
  subjectId: string;
  subjectName: string | null;
  specializationId: string;
  specializationName: string | null;
  /** for the card's "N videos" line. */
  videoCount: number;
  /** for the card's "N materials" line. */
  materialCount: number;
  /** `0` == "no expiry"; else "N-day access". */
  validityDays: number;
  /** whether the session has a gating quiz — drives the "Quiz" content badge. */
  hasQuiz: boolean;
  /** whether the session has an assignment — drives the "Assignment" content badge. */
  hasAssignment: boolean;
  // Prerequisite (§A.2 / §C.2)
  prerequisiteSessionId: string | null;
  prerequisiteTitle: string | null;
  /** vacuously `true` when there is no prerequisite; `false` disables Enroll with a hint (UX gate). */
  prerequisiteSatisfied: boolean;
  // The caller's state (§C.1)
  enrollmentState: EnrollmentState;
  /** when `Enrolled`: the active enrollment's expiry (`null` == no-expiry session); else `null`. */
  enrolledExpiresAtUtc: string | null;
}

/** Optional narrowing filters for `GET /api/me/catalogue` (contract §A.1). */
export interface CatalogueFilters {
  gradeId?: string;
  subjectId?: string;
  specializationId?: string;
  search?: string;
}

/** `201 Created` result of `POST /api/enrollments/redeem` (contract §B.2 `EnrollmentDto`). */
export interface Enrollment {
  id: string;
  studentId: string;
  studentName: string | null;
  sessionId: string;
  sessionTitle: string | null;
  status: string;
  method: string;
  amount: number;
  codeId: string | null;
  codeSerial: string | null;
  enrolledAtUtc: string;
  /** `null` when the session's `validityDays == 0` (no-expiry). */
  expiresAtUtc: string | null;
}
