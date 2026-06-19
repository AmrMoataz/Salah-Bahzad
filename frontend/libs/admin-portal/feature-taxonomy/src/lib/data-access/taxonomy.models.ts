/** The three editable taxonomy kinds (drives the tabbed CRUD UI). */
export type TaxonomyKind = 'grade' | 'subject' | 'specialization';

/** A teacher-managed grade level (FR-PLAT-TAX-001). */
export interface Grade {
  id: string;
  name: string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

/** A teacher-managed subject; <code>specializationCount</code> gates delete-in-use (FR-PLAT-TAX-004). */
export interface Subject {
  id: string;
  name: string;
  specializationCount: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

/** A teacher-managed specialization belonging to one subject (FR-PLAT-TAX-002). */
export interface Specialization {
  id: string;
  name: string;
  subjectId: string;
  subjectName: string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

/** A seeded Egypt city/governorate — global, read-only reference data (FR-PLAT-TAX-003). */
export interface City {
  id: string;
  nameEn: string;
  nameAr: string;
}

/** A seeded Egypt region/district under a city — global, read-only reference data (FR-PLAT-TAX-003). */
export interface Region {
  id: string;
  cityId: string;
  nameEn: string;
  nameAr: string;
}

/** Payload emitted by the taxonomy form. <code>subjectId</code> is set only for specializations. */
export interface TaxonomyFormValue {
  name: string;
  subjectId?: string;
}
