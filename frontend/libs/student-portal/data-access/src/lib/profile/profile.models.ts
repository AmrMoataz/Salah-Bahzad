// Wire models for the authenticated self-service Profile surface (S6). These mirror the FROZEN
// contract (`docs/contracts/student-s6-profile.md`) field-for-field — §A.1 (GET result) and §A.2
// (PUT body). Use `| null` (the contract's nullables), never optional `?`. Do not add fields the
// API does not send: there is **no** `email` and **no** `avatar` — email is shown read-only from
// Firebase (§C.2), avatar is initials-only (decision 1, §F).

import { StudentStatus } from '../auth/student-auth.models';

/**
 * Nested in {@link StudentProfile} — the caller's **active** `StudentDevice` (contract §A.1 / §C.5).
 * The `DeviceTokenHash` is **never** exposed; there is nothing to hide client-side.
 */
export interface BoundDevice {
  /** `StudentDevice.FingerprintSummary`, e.g. "Windows / Chrome"; `null` → the UI shows a generic label. */
  summary: string | null;
  /** `StudentDevice.BoundAtUtc` (ISO-8601) → rendered as "Bound {date}". */
  boundAtUtc: string;
}

/**
 * `GET /api/me/profile` result (contract §A.1). **NO `email` field** — email is shown client-side
 * from Firebase (§C.2). **NO `avatar`** — the avatar is initials-only (decision 1). The wire shape
 * equals this interface field-for-field, so the response is the model (no mapping).
 */
export interface StudentProfile {
  id: string;
  fullName: string;
  phoneNumber: string;
  parentPhonePrimary: string;
  parentPhoneSecondary: string | null;
  schoolName: string;
  /** Read-only (display only) — grade is staff-managed and is NOT in the PUT body (§C.1). */
  gradeId: string;
  /** Tenant-owned taxonomy display name — the disabled Grade field + the header sub-line. */
  gradeName: string | null;
  cityId: string;
  cityName: string | null;
  regionId: string;
  regionName: string | null;
  /** `StudentStatus` string name; a signed-in student is `'Active'` → the success Chip. */
  status: StudentStatus;
  /** The active bound device; `null` when the caller has no active device bound. */
  boundDevice: BoundDevice | null;
}

/**
 * `PUT /api/me/profile` body (contract §A.2) — the **seven** writable fields only. **NO `gradeId`**
 * (grade is staff-managed, `FR-ADM-STU-005`), **NO `email`** (Firebase identity, §C.2), **NO
 * `status`/`boundDevice`**. The runner-vs-review separation discipline (S4/S5): the PUT body is a
 * **distinct** wire model from the GET result — it must never carry the read-only fields.
 */
export interface UpdateMyStudentProfile {
  fullName: string;
  phoneNumber: string;
  schoolName: string;
  cityId: string;
  regionId: string;
  parentPhonePrimary: string;
  parentPhoneSecondary: string | null;
}
