// Wire models for the authenticated **Quizzes** surface — the proctored single-sitting runner
// (contract `docs/contracts/student-s5-quizzes.md` §A, the REUSED 5B-2 engine: load → start → answer
// → submit → focus) plus the **new** per-attempt answer-key review (§B). Enums cross the wire as
// string names (`JsonStringEnumConverter`), modelled here as TS string unions; dates are ISO-8601
// `…AtUtc`; times are integer **seconds** (`timeSpentSeconds`), rendered `M:SS` by the UI.
//
// ⚠️ The student/staff `isCorrect` split is load-bearing (§0/§A): the **live** shapes
// {@link QuizAttempt} / {@link QuizAttemptQuestion} / {@link QuizAttemptOption} and the **intro**
// {@link StudentQuiz} **NEVER** carry option correctness (the 5B-2 invariant + its guard test stand).
// The **only** surface that reveals the answer key is the **distinct** {@link StudentQuizAttemptReview}
// (§B), and only for the caller's own **terminal** attempt. Keep the two families separate — never
// widen the live/intro interfaces with `isCorrect`.

// ── enums (string unions over the wire, §0) ───────────────────────────────────────────────────────

/** An attempt's lifecycle state (§A #1). Terminal = anything but `InProgress`. */
export type QuizAttemptStatus = 'InProgress' | 'Submitted' | 'Forfeited' | 'TimedOut';

/** The UI flag pill (§A #1) — derived server-side from {@link QuizAttemptStatus}. */
export type QuizAttemptFlag = 'Clean' | 'Timeout' | 'Forfeit';

/** Focus-loss telemetry event type (§A #5). Monitoring only — never forfeits. */
export type FocusEventType = 'FocusLost' | 'FocusReturned';

// ── §A · the INTRO shape (`StudentQuizDto`, #1) — NO questions, NO correctness ─────────────────────

/** The quiz's settings block (§A #1) — drives the intro rules card. */
export interface QuizSettings {
  timeLimitMinutes: number;
  questionCount: number;
  /** total attempts allowed. */
  attemptCount: number;
  minPassPercent: number;
}

/**
 * One attempt summary row (§A #1) — surfaced in the intro's attempt history. **`id` is the additive
 * S5 field** that deep-links the §B review for a **terminal** row (`status != 'InProgress'`).
 */
export interface StudentQuizAttemptSummary {
  /** the attempt id — pass to {@link QuizService.review} (terminal rows only). */
  id: string;
  /** 1-based sitting number. */
  number: number;
  /** `null` while `InProgress`. */
  scorePercent: number | null;
  status: QuizAttemptStatus;
  flag: QuizAttemptFlag;
  startedAtUtc: string;
  submittedAtUtc: string | null;
}

/**
 * §A #1 `StudentQuizDto` — the **intro** shape for `GET /api/me/quizzes/by-session/{sessionId}`.
 * Summary only: **no** questions, **no** correctness. `activeAttemptId` is a resumable in-progress
 * attempt (if any); `bestPercent` is `null` until the first attempt terminates.
 */
export interface StudentQuiz {
  /** the userQuiz id — pass to {@link QuizService.start} to mint an attempt. */
  id: string;
  /** session B — the session whose videos this quiz unlocks. */
  gatedSessionId: string;
  settings: QuizSettings;
  attemptsUsed: number;
  attemptsRemaining: number;
  bestPercent: number | null;
  /** `bestPercent >= minPassPercent` (`>=`; the 5B-2 fix). */
  passed: boolean;
  /** a resumable in-progress attempt id, or `null`. */
  activeAttemptId: string | null;
  attempts: StudentQuizAttemptSummary[];
}

// ── §A #2 · the LIVE attempt (`QuizAttemptDto`, START) — questions WITHOUT isCorrect, no hint ──────

/** A live attempt option (§A #2) — **NO `isCorrect`** (the 5B-2 invariant; only the review exposes it). */
export interface QuizAttemptOption {
  id: string;
  /** 0-based display order. */
  order: number;
  text: string;
}

/** A live attempt question (§A #2) — **NO `isCorrect`, NO `hintUrl`** (quizzes carry no hint, §0). */
export interface QuizAttemptQuestion {
  /** the attemptQuestion id — the `{aqId}` of the answer `PUT`. */
  id: string;
  /** 1-based. */
  order: number;
  bodyLatex: string | null;
  /** short-lived signed R2 URL; `null` if no image. */
  imageUrl: string | null;
  options: QuizAttemptOption[];
}

/**
 * §A #2 `QuizAttemptDto` — the **live** attempt returned by `POST …/{quizId}/attempts` (START). The
 * runner seeds its **local** countdown from `deadlineUtc − serverNowUtc` (server-authoritative; the
 * real auto-submit is a server Hangfire job at `deadlineUtc`, §C). Questions carry **no** correctness.
 */
export interface QuizAttempt {
  /** pass to {@link QuizService.answer}/`submit`/`focus` and (post-terminal) {@link QuizService.review}. */
  attemptId: string;
  number: number;
  /** authoritative end instant (`start + timeLimitMinutes`). */
  deadlineUtc: string;
  /** the server's clock at start — correct the local countdown against it. */
  serverNowUtc: string;
  questions: QuizAttemptQuestion[];
}

// ── §A #4 · the SUBMIT result (`QuizAttemptResultDto`) — score-only, no questions ─────────────────

/**
 * §A #4 `QuizAttemptResultDto` — the score-only result of `POST …/submit`. **No questions, no
 * `attemptId`** (the runner holds the `attemptId` from START for the review link, §D).
 */
export interface QuizAttemptResult {
  scorePercent: number;
  status: QuizAttemptStatus;
  bestPercent: number;
  passed: boolean;
  attemptsRemaining: number;
}

// ── §A #5 · focus telemetry body ─────────────────────────────────────────────────────────────────

/** §A #5 focus event body → `assessment_events`. **Monitoring only — never forfeits.** */
export interface FocusEventBody {
  type: FocusEventType;
  occurredAtUtc: string;
  /** the dwell time of a `FocusLost` window, attached to the matching `FocusReturned`. */
  durationMs?: number;
}

// ── §B · the per-attempt answer-key review — `isCorrect` exposed (review only, post-terminal) ─────

/** A review option (§B.1) — **`isCorrect` exposed** (review only). */
export interface StudentQuizReviewOption {
  id: string;
  /** 0-based display order. */
  order: number;
  text: string;
  isCorrect: boolean;
}

/** One review question (§B.1) — `isCorrect` = the student picked the correct option this attempt. */
export interface StudentQuizReviewQuestion {
  id: string;
  /** 1-based. */
  order: number;
  bodyLatex: string | null;
  imageUrl: string | null;
  /** the question's weight in this attempt. */
  mark: number;
  options: StudentQuizReviewOption[];
  /** what the student picked this attempt; `null` = unanswered (common on Timeout/Forfeit). */
  selectedOptionId: string | null;
  isCorrect: boolean;
}

/**
 * §B.1 `StudentQuizAttemptReviewDto` — the **only** student surface exposing the answer key, and
 * **only** for the caller's own **terminal** attempt (an `InProgress` attempt → `403`, §B.2). Distinct
 * from the live {@link QuizAttempt} — keep `isCorrect` out of the live shapes.
 */
export interface StudentQuizAttemptReview {
  /** echo of the route param. */
  attemptId: string;
  /** the owning userQuiz id. */
  quizId: string;
  /** session B (the quiz unlocks its videos). */
  gatedSessionId: string;
  /** session B's title for the header "{sessionTitle} · Quiz review"; may be `null`. */
  sessionTitle: string | null;
  /** the sitting number. */
  number: number;
  /** terminal here (the endpoint gates it — §B.2): `Submitted` | `TimedOut` | `Forfeited`. */
  status: QuizAttemptStatus;
  /** this attempt's score (`0` for a `Forfeited` attempt). */
  scorePercent: number;
  minPassPercent: number;
  startedAtUtc: string;
  submittedAtUtc: string;
  /** `submitted − started` (the full window on timeout); rendered `M:SS`. */
  timeSpentSeconds: number;
  questions: StudentQuizReviewQuestion[];
}
