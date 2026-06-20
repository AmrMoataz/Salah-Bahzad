/**
 * Wire shapes for the Sessions slice — a faithful mirror of the FROZEN Phase 3 contract
 * (`docs/contracts/phase3-sessions.md`). Enums serialize as their string names
 * (JsonStringEnumConverter); `DateTimeOffset` serializes as an ISO string; private media
 * (thumbnails, question/variation images) arrive as short-lived **signed URLs embedded** in the
 * read model, while materials are fetched on demand via a signed-URL endpoint.
 */

/** Session lifecycle state (FR-PLAT-SES-001). */
export type SessionStatus = 'Draft' | 'Published' | 'Archived';

/** Phase-3 video transcode state; no playback URL is issued until Phase 5 (FR-PLAT-VID-007). */
export type VideoProcessingStatus = 'Pending' | 'Processing' | 'Ready' | 'Failed';

/** Generic server pagination envelope (shared shape with the other slices). */
export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

/** A short-lived signed URL for on-demand media (materials, §2.16). */
export interface SignedUrlDto {
  url: string;
  expiresAtUtc: string;
}

/**
 * One catalogue row for the admin sessions list (FR-ADM-SES-001, contract §1 `SessionListDto`).
 * The row renders a specialization-accent tile + title (never a thumbnail), so there is no
 * thumbnail/price here — only the taxonomy names and counts the table shows.
 */
export interface SessionListDto {
  id: string;
  title: string;
  gradeName: string | null;
  subjectName: string | null;
  specializationName: string | null;
  status: SessionStatus;
  questionCount: number;
  videoCount: number;
  /** Always 0 in Phase 3 — enrollment ships in Phase 4. */
  enrolledCount: number;
}

/** An ordered session video with its admin-entered length + per-enrollment access cap (FR-PLAT-SES-002). */
export interface SessionVideoDto {
  id: string;
  title: string;
  order: number;
  /** Admin-entered running time in minutes (no transcode-derived duration until Phase 5). */
  lengthMinutes: number;
  accessCount: number;
  processingStatus: VideoProcessingStatus;
  createdAtUtc: string;
}

/** A downloadable session material (FR-PLAT-SES-003). No embedded URL — fetch via §2.16 on demand. */
export interface SessionMaterialDto {
  id: string;
  fileName: string;
  /** Upper-case file-kind label shown in the UI ("PDF" / "PNG" / "CSV"), computed server-side. */
  kind: string;
  sizeBytes: number;
  createdAtUtc: string;
}

/** Gating-quiz configuration (FR-PLAT-SES-006). Null on the detail when unset. Minutes-based per the contract. */
export interface QuizSettingDto {
  timeLimitMinutes: number;
  questionCount: number;
  attemptCount: number;
  minPassPercent: number;
}

/** The full session record for the detail/edit screens (FR-ADM-SES-007). */
export interface SessionDetailDto {
  id: string;
  title: string;
  description: string | null;
  price: number;
  validityDays: number;
  thumbnailUrl: string | null;
  gradeId: string;
  gradeName: string | null;
  subjectId: string;
  subjectName: string | null;
  specializationId: string;
  specializationName: string | null;
  status: SessionStatus;
  prerequisiteSessionId: string | null;
  prerequisiteTitle: string | null;
  quizSetting: QuizSettingDto | null;
  videos: SessionVideoDto[];
  materials: SessionMaterialDto[];
  questionCount: number;
  quizEligibleQuestionCount: number;
  enrolledCount: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

/** One MCQ answer option (FR-PLAT-QB-001). */
export interface OptionDto {
  id: string;
  text: string;
  isCorrect: boolean;
}

/** An alternate wording/image of a question with its own options (FR-PLAT-QB-003). */
export interface QuestionVariationDto {
  id: string;
  bodyLatex: string | null;
  imageUrl: string | null;
  options: OptionDto[];
}

/** A question-bank entry (FR-PLAT-QB-001..006). `imageUrl` is signed + embedded. */
export interface QuestionDto {
  id: string;
  sessionId: string;
  bodyLatex: string | null;
  imageUrl: string | null;
  mark: number;
  isValidForQuiz: boolean;
  hintUrl: string | null;
  options: OptionDto[];
  variations: QuestionVariationDto[];
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

// ── Taxonomy reference (read via this slice's own service, not feature-taxonomy) ───────────────

/** Minimal grade reference for the list filter + session form. */
export interface GradeRef {
  id: string;
  name: string;
}

/** Minimal subject reference (filters the specialization picker; the list filter accepts a subjectId). */
export interface SubjectRef {
  id: string;
  name: string;
}

/** Minimal specialization reference; `subjectId` lets the form scope the picker to the chosen subject. */
export interface SpecializationRef {
  id: string;
  name: string;
  subjectId: string;
  subjectName: string | null;
}

/**
 * One audit row for the session detail's Activity tab (FR-PLAT-SES-009, §2.27). Every question/video/
 * material/image/lifecycle action is recorded against the session id, so this is the session's full
 * history. `summary` is the human-readable line; `action` is the machine event name.
 */
export interface SessionActivityDto {
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
// Shapes mirror the FROZEN Phase 4 contract (`docs/contracts/phase4-codes-enrollment.md`). They are
// duplicated here (not imported from feature-codes) to respect the Nx feature→feature boundary.

/** Enrollment lifecycle state. */
export type EnrollmentStatus = 'Active' | 'Expired' | 'Refunded';

/** How an enrollment was granted. */
export type EnrollmentMethod = 'Code' | 'Unlock';

/**
 * One row of the session-detail "Enrolled students" tab (contract §1 `EnrollmentListDto`, endpoint #8).
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

/** Result of unlock (#9) / refund (#10) (contract §1 `EnrollmentDto`). */
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
  expiresAtUtc: string | null;
}

/** Lightweight active-student row for the unlock picker (`GET /api/students?status=Active&search=`). */
export interface StudentSearchRow {
  id: string;
  name: string;
  phone: string;
}

// ── Request payloads ───────────────────────────────────────────────────────────────────────────

/** Create/update body for a session (§2.2 / §2.4). Subject is derived server-side from the specialization. */
export interface SaveSessionRequest {
  title: string;
  description: string;
  price: number;
  validityDays: number;
  gradeId: string;
  specializationId: string;
}

/** One option in a question/variation save payload; `id` present only for existing options. */
export interface OptionInput {
  id?: string;
  text: string;
  isCorrect: boolean;
}

/** Create/update body for a base question (§2.19 / §2.20). */
export interface SaveQuestionRequest {
  bodyLatex?: string | null;
  mark: number;
  isValidForQuiz: boolean;
  hintUrl?: string | null;
  options: OptionInput[];
  /**
   * Inline image for an image-only question on **create** (§2.19): base64 (no data-URL prefix) plus its
   * content type. Lets a question with only an image be created in one call; ignored on update (use the
   * dedicated image endpoint to replace afterwards).
   */
  imageBase64?: string | null;
  imageContentType?: string | null;
}

/** Create/update body for a variation (§2.23 / §2.24). */
export interface SaveVariationRequest {
  bodyLatex?: string | null;
  options: OptionInput[];
  /** Inline image for an image-only variation on **add** (§2.24): base64 (no data-URL prefix) + content
   * type. Lets a variation with only an image be created in one call; ignored on update. */
  imageBase64?: string | null;
  imageContentType?: string | null;
}

/** Query params for the paged/filterable sessions list (§2.1: search + grade + subject + status). */
export interface SessionListQuery {
  search?: string;
  gradeId?: string | null;
  subjectId?: string | null;
  status?: SessionStatus | null;
  page?: number;
  pageSize?: number;
}
