// Wire models for the anonymous registration surface. These mirror the FROZEN contract
// (`docs/contracts/student-s1-registration.md`) field-for-field — §A.1 (register form), §B
// (reference reads), §F (client constants). Do not add fields the API does not send.

import { StudentStatus } from '../auth/student-auth.models';

/** A tenant-scoped grade option for the wizard (contract §B#3 — reads only `id` + `name`). */
export interface GradeRef {
  id: string;
  name: string;
}

/** A global city option (contract §B#1 — `CityDto`). */
export interface CityRef {
  id: string;
  nameEn: string;
  nameAr: string;
}

/** A global region option, scoped to a city (contract §B#2 — `RegionDto`). */
export interface RegionRef {
  id: string;
  cityId: string;
  nameEn: string;
  nameAr: string;
}

/** How the student created their Firebase identity (email/password vs Google popup). */
export type RegistrationMethod = 'manual' | 'google';

/**
 * The wizard's collected values, handed to `RegistrationService.register`. The service adds the
 * Firebase ID token (from the held credential) and the `tenantSlug`/`termsVersion` constants, then
 * builds the multipart body with the exact contract §A.1 field names.
 */
export interface RegisterFormData {
  fullName: string;
  phoneNumber: string;
  parentPhonePrimary: string;
  parentPhoneSecondary?: string;
  gradeId: string;
  cityId: string;
  regionId: string;
  schoolName: string;
  idImage: File;
}

/** The prefill Google hands back from the popup (read-only email + display name). */
export interface GoogleProfile {
  fullName: string | null;
  email: string | null;
}

/** `201 Created` result of `POST /api/students/register` (contract §A.2). */
export interface RegisterResult {
  studentId: string;
  status: StudentStatus;
}

/** Client-supplied constants resolved from the `window.__SB_*__` shim (contract §F). */
export interface RegistrationConfig {
  tenantSlug: string;
  termsVersion: string;
}

/** ID-image client guard mirroring the server's authoritative rules (contract §D). */
export const ID_IMAGE_MAX_BYTES = 5 * 1024 * 1024;
export const ID_IMAGE_ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const;
/** `accept` attribute for the file picker (file extensions matching the accepted MIME types). */
export const ID_IMAGE_ACCEPT_ATTR = 'image/jpeg,image/png,image/webp';
