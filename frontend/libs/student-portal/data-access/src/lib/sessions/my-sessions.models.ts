// Wire models for the authenticated **My Sessions** surface (contract
// `docs/contracts/student-s3-my-sessions-video.md`). These mirror the FROZEN contract field-for-field
// — §A.2 (`MySessionDto`), §B.1 (`MySessionDetailDto` + `MySessionVideoDto` / `MySessionMaterialDto` /
// `MyAssignmentStatusDto` / `MyQuizStatusDto`), §C (`SignedUrlDto`) and §D.1 (`PlaybackHandoffDto`).
// Enums cross the wire as string names (`JsonStringEnumConverter`), so they are modelled as TS string
// unions. Do not add fields the API does not send.

/**
 * Completion state of an enrolled session (§A.2 / §E.2). Describes completion **only** — expiry rides
 * `isExpired` + `expiresAtUtc`, so a `Completed` session can still show an "Expired" chip.
 */
export type MySessionState = 'NotStarted' | 'InProgress' | 'Completed';

/**
 * The optional server-side filter for `GET /api/me/sessions` (§A.1). `ExpiringSoon` is a *filter*
 * predicate (`!isExpired && expiresAtUtc within 14 days`), not a value of {@link MySessionState}. The
 * happy path filters client-side; this exists for completeness/tests and is honoured server-side.
 */
export type MySessionFilter = 'InProgress' | 'Completed' | 'ExpiringSoon' | 'Expired';

/** Transcode pipeline state of a session video (§B.1). `lengthSeconds` is `0` until `Ready`. */
export type VideoProcessingStatus = 'Pending' | 'Processing' | 'Ready' | 'Failed';

/**
 * Per-video lock state (§E.3) computed in the **same order** the 5C gate authorizes, so the badge
 * *predicts* the Play result. First matching rule wins: `Expired` (session expired) → `QuizLocked`
 * (gating quiz not passed) → `NotReady` (still transcoding) → `Exhausted` (no views left) →
 * `Playable`.
 */
export type VideoLockState = 'Playable' | 'QuizLocked' | 'Expired' | 'Exhausted' | 'NotReady';

/** Gate banner state (§E.4): `Expired` if expired; else `QuizRequired` if quiz-gated & unpassed; else `Open`. */
export type GateState = 'Open' | 'QuizRequired' | 'Expired';

/** Assignment progress (§B.1 `MyAssignmentStatusDto`) — only ever in-flight or done. */
export type AssignmentStatus = 'InProgress' | 'Completed';

/** A row in the My-Sessions hub (contract §A.2 `MySessionDto`), ordered `EnrolledAtUtc` DESC. */
export interface MySession {
  /** the SESSION id (the route param of the detail read). */
  id: string;
  /** the caller's enrollment (client correlation only; never trusted server-side). */
  enrollmentId: string;
  title: string;
  gradeName: string | null;
  subjectName: string | null;
  specializationName: string | null;
  /** short-lived signed R2 URL; `null` when there is no thumbnail. */
  thumbnailUrl: string | null;
  // progress (§E.1)
  videoCount: number;
  /** EnrollmentVideoAccess rows with a spent view (`AccessRemaining < AccessAllowed`). */
  videosWatched: number;
  /** `round(100 × videosWatched / videoCount)`; `0` when `videoCount == 0`. */
  progressPercent: number;
  // expiry (§E.2)
  enrolledAtUtc: string;
  /** `null` == no-expiry session (`ValidityDays == 0`). */
  expiresAtUtc: string | null;
  /** DERIVED: `expiresAtUtc != null && expiresAtUtc <= now`. */
  isExpired: boolean;
  state: MySessionState;
}

/** One playlist row of the session detail (contract §B.1 `MySessionVideoDto`), ordered by `order` asc. */
export interface MySessionVideo {
  id: string;
  title: string;
  order: number;
  /** `0` until `processingStatus == Ready` (ffprobe-computed); rendered `MM:SS` by the UI. */
  lengthSeconds: number;
  processingStatus: VideoProcessingStatus;
  /** the caller's EnrollmentVideoAccess for this video. */
  accessAllowed: number;
  accessRemaining: number;
  lockState: VideoLockState;
}

/** A session material (contract §B.1 `MySessionMaterialDto`) — names only; bytes fetched via the signed-URL read. */
export interface MySessionMaterial {
  id: string;
  fileName: string;
  /** upper-case extension label, e.g. `PDF`. */
  kind: string;
  sizeBytes: number;
}

/** The session assignment status (contract §B.1 `MyAssignmentStatusDto`) — reachable even when expired. */
export interface MyAssignmentStatus {
  userAssignmentId: string;
  status: AssignmentStatus;
  scoreMarks: number | null;
  maxMarks: number;
  correctCount: number | null;
  questionCount: number;
  completedAtUtc: string | null;
}

/** The gating quiz status (contract §B.1 `MyQuizStatusDto`) — `null` in the parent when not quiz-gated. */
export interface MyQuizStatus {
  userQuizId: string;
  passed: boolean;
  bestPercent: number | null;
  minPassPercent: number;
  attemptsUsed: number;
  /** total attempts allowed. */
  attemptCount: number;
  timeLimitMinutes: number;
  questionCount: number;
}

/** The full study view of one enrolled session (contract §B.1 `MySessionDetailDto`). */
export interface MySessionDetail {
  id: string;
  title: string;
  description: string | null;
  gradeId: string;
  gradeName: string | null;
  subjectId: string;
  subjectName: string | null;
  specializationId: string;
  specializationName: string | null;
  thumbnailUrl: string | null;
  // enrollment + progress (§E.1/§E.2)
  enrollmentId: string;
  enrolledAtUtc: string;
  expiresAtUtc: string | null;
  isExpired: boolean;
  videoCount: number;
  videosWatched: number;
  progressPercent: number;
  // gate banner (§E.4)
  gateState: GateState;
  hasGatingQuiz: boolean;
  quizPassed: boolean;
  /** the gate's pass mark (for the banner copy); `0` when not quiz-gated. */
  minPassPercent: number;
  // collections
  videos: MySessionVideo[];
  materials: MySessionMaterial[];
  /** `null` only if the session has no assignment snapshot. */
  assignment: MyAssignmentStatus | null;
  /** `null` when the session is not quiz-gated. */
  quiz: MyQuizStatus | null;
}

/** Short-lived signed R2 URL for a material (contract §C `SignedUrlDto`). */
export interface SignedUrl {
  url: string;
  expiresAtUtc: string;
}

/**
 * The deep-link payload returned by the 5C Play gate (contract §D.1 `PlaybackHandoffDto`). The raw
 * token/URL is NEVER returned here — the browser builds `salah-bahazad://stream?…&handoff={code}` from
 * this and hands off to the native app (§E.5). The code is one-time, ~60 s TTL.
 */
export interface PlaybackHandoff {
  handoffCode: string;
  expiresAtUtc: string;
}
