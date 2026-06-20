/**
 * Wire shapes for the Attendance & Assignment/Behaviour review screens (Phase 5B-1) — a faithful
 * mirror of the FROZEN contract (`docs/contracts/phase5b1-assignments-attendance.md`, §B attendance
 * and §C review). Enums serialize as their string names (JsonStringEnumConverter) so they are modelled
 * as TS string-union types; `DateTimeOffset` serializes as an ISO-8601 string; images are R2 keys the
 * server resolves to signed URLs on read.
 *
 * Satisfies the read side of `FR-ADM-ATT-001..004`, `FR-ADM-REV-001`/`-003`.
 */

/** Generic server pagination envelope (shared shape with the other slices). */
export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ── §B Attendance ────────────────────────────────────────────────────────────────────────────────

/**
 * One enrolled student's row in the "By session" cohort matrix (contract §B `SessionAttendanceRowDto`,
 * `scrAttendance` line 1265). `videosWatched` is fed by the 5C video gate → **0** until then;
 * `assignmentPercent` is null until the student completes the assignment; `bestQuizPercent`/
 * `quizAttemptCount` are populated by the 5B-2 quiz engine (null/0 only when the session has no
 * gating quiz or no attempts yet).
 */
export interface SessionAttendanceRow {
  enrollmentId: string;
  studentId: string;
  studentName: string;
  videosWatched: number;
  videosTotal: number;
  /** = `Attendance.AssignmentScore` (percent); null until the assignment is completed. */
  assignmentPercent: number | null;
  bestQuizPercent: number | null;
  quizAttemptCount: number;
}

/**
 * One session's row in the "By student" per-session breakdown (contract §B `StudentAttendanceRowDto`,
 * `scrAttendance` line 1277). Same column caveats as the cohort matrix.
 */
export interface StudentAttendanceRow {
  enrollmentId: string;
  sessionId: string;
  sessionTitle: string;
  videosWatched: number;
  videosTotal: number;
  assignmentPercent: number | null;
  bestQuizPercent: number | null;
  quizAttemptCount: number;
}

// ── §C Assignment review ─────────────────────────────────────────────────────────────────────────

/** Assignment lifecycle state (contract §A/§C). */
export type AssignmentStatus = 'InProgress' | 'Completed';

/** A snapshotted MCQ option on a reviewed question — staff-only, so it **carries `isCorrect`**. */
export interface ReviewOption {
  id: string;
  order: number;
  text: string;
  isCorrect: boolean;
}

/**
 * One reviewed assignment question (contract §C `AssignmentReviewDto.questions[]`). Unlike the
 * student `§A` shape, the review **exposes `isCorrect`** (per question and per option) and the
 * `selectedOptionId` the student picked, so the card can highlight correct vs. picked-wrong.
 */
export interface ReviewQuestion {
  order: number;
  bodyLatex: string;
  imageUrl: string | null;
  mark: number;
  hintUrl: string | null;
  options: ReviewOption[];
  selectedOptionId: string | null;
  /** Whether the student answered this question correctly. */
  isCorrect: boolean;
}

/**
 * The full assignment-review payload (contract §C `AssignmentReviewDto`). Drives `scrReview`'s header
 * (`name`, `{session} · Assignment review`, **Score** = `correctCount/questionCount`, **Time spent** =
 * `timeSpentSeconds`) and the Assignment-tab question cards.
 */
export interface AssignmentReview {
  studentName: string;
  sessionTitle: string;
  correctCount: number;
  questionCount: number;
  scoreMarks: number;
  maxMarks: number;
  percent: number;
  timeSpentSeconds: number;
  status: AssignmentStatus;
  questions: ReviewQuestion[];
}

/**
 * Behaviour-event kind (contract §A/§C; §B adds the quiz attempt's `FocusLost`/`FocusReturned` rows,
 * sourced from `assessment_events`). Drives the timeline icon/accent (see `attendance.presentation.ts`).
 */
export type BehaviourEventType =
  | 'Entered'
  | 'Left'
  | 'Answered'
  | 'Navigated'
  | 'FocusLost'
  | 'FocusReturned';

/**
 * One in-assessment behaviour event (contract §C `BehaviourEventDto`, `scrReview` lines 1131-1134).
 * The backend supplies the human `label` ("Answered Q1"); the frontend owns the icon/accent map.
 */
export interface BehaviourEvent {
  type: BehaviourEventType;
  label: string;
  questionOrder: number | null;
  occurredAtUtc: string;
}

// ── §B Quiz-attempts review (Phase 5B-2) ───────────────────────────────────────────────────────────

/**
 * A quiz attempt's outcome flag (contract §B, `scrReview` line 1129) — derived server-side from the
 * attempt `status`. Drives the Flags pill (`Clean`→success, `Timeout`→danger, `Forfeit`→warning;
 * see `quizFlagPill` in `attendance.presentation.ts`).
 */
export type QuizFlag = 'Clean' | 'Timeout' | 'Forfeit';

/** A quiz attempt's lifecycle status (contract §C `QuizAttempt.Status`). */
export type QuizAttemptStatus = 'InProgress' | 'Submitted' | 'Forfeited' | 'TimedOut';

/**
 * One row in the `scrReview` "Quiz attempts" table (contract §B `QuizReviewDto.attempts[]`,
 * lines 1128-1130): Attempt / Score / Time spent / Flags / When, with the best attempt marked.
 */
export interface QuizAttemptRow {
  number: number;
  scorePercent: number;
  /** `submitted − started` (or `deadline − started` on timeout); rendered `mm:ss`. */
  timeSpentSeconds: number;
  flag: QuizFlag;
  status: QuizAttemptStatus;
  startedAtUtc: string;
  isBest: boolean;
}

/**
 * The full quiz-review payload (contract §B `QuizReviewDto`) for one enrollment's gating quiz — drives
 * the header line ("Best {bestPercent}% · {passed} (min {minPassPercent}%) · {used}/{allowed}") and the
 * attempts table. A **404** means the gated session has no prerequisite quiz → the empty state.
 */
export interface QuizReview {
  bestPercent: number;
  passed: boolean;
  minPassPercent: number;
  attemptsUsed: number;
  attemptsAllowed: number;
  attempts: QuizAttemptRow[];
}

// ── Combo reference lists (read directly to stay within the Nx feature boundary) ──────────────────

/** A session option for the "By session" combo (read from `/api/sessions`). */
export interface SessionOption {
  id: string;
  title: string;
}

/** A student option for the "By student" combo (read from `/api/students?status=Active`). */
export interface StudentOption {
  id: string;
  name: string;
}
