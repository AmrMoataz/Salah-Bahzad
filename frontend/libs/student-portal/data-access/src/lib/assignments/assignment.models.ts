// Wire models for the authenticated **Assignments** surface — the open-book runner (contract
// `docs/contracts/student-s4-assignments.md` §A, the reused 5B-1 engine) and the **new** answer-key
// review (§B). Enums cross the wire as string names (`JsonStringEnumConverter`), modelled here as TS
// string unions; dates are ISO-8601 `…AtUtc`; times are integer **seconds** (`timeSpentSeconds`).
//
// ⚠️ The student/staff `isCorrect` split is load-bearing (§0/§A): the runner shape
// {@link StudentAssignment} **NEVER** carries option correctness (the 5B-1 invariant + its guard
// test stand). The **only** surface that reveals the answer key is the **distinct**
// {@link StudentAssignmentReview} (§B), and only for the caller's own **`Completed`** assignment.
// Keep the two families of interfaces separate — never widen the runner DTO with `isCorrect`.

import { AssignmentStatus } from '../sessions/my-sessions.models';

// Re-use the existing `'InProgress' | 'Completed'` union (already exported by the barrel for S3) —
// do not duplicate the union name (master plan F2).
export type { AssignmentStatus };

// ── §A · the runner (solving) shapes — correctness is NEVER present ──────────────────────────────

/** §A `StudentAssignmentDto` — the SOLVING shape (the enroll side-effect created it). Resumable. */
export interface StudentAssignment {
  /** the **userAssignment** id — pass to {@link AssignmentService.answer}/`event`/`review`. */
  id: string;
  sessionId: string;
  status: AssignmentStatus;
  /** accumulated across sittings (authoritative); the runner's up-timer resumes from this. */
  timeSpentSeconds: number;
  questions: StudentAssignmentQuestion[];
}

/** One runner question (§A). `selectedOptionId` is the resumed saved choice (`null` until answered). */
export interface StudentAssignmentQuestion {
  /** the **assignmentQuestion** id — the `{aqId}` of the answer `PUT`. */
  id: string;
  /** 1-based. */
  order: number;
  bodyLatex: string | null;
  /** short-lived signed R2 URL; `null` if no image. */
  imageUrl: string | null;
  /** the per-question hint (`FR-STU-ASG-004`); `null` if none configured. */
  hintUrl: string | null;
  options: StudentAssignmentOption[];
  /** the student's saved choice; `null` until answered (resume). */
  selectedOptionId: string | null;
}

/** A runner option (§A) — **NO `isCorrect`** (the 5B-1 invariant; only the review DTO exposes it). */
export interface StudentAssignmentOption {
  id: string;
  order: number;
  text: string;
}

/** §A `AssignmentProgressDto` — returned by the answer `PUT`; drives the "X of Y answered" bar. */
export interface AssignmentProgress {
  answeredCount: number;
  questionCount: number;
  status: AssignmentStatus;
}

/** Behaviour event types (§A #3). **`'Answered'` is NOT valid here** — it's logged by the answer `PUT`. */
export type AssignmentEventType = 'Entered' | 'Left' | 'Navigated';

/** §A #3 behaviour event body — `elapsedMs` accrues to `timeSpentSeconds` (the accumulated timer). */
export interface AssignmentEventBody {
  type: AssignmentEventType;
  /** the **target** question's `order` (for `Navigated`). */
  questionOrder?: number;
  occurredAtUtc: string;
  /** the elapsed delta since the last flush → the engine accrues it to `timeSpentSeconds`. */
  elapsedMs?: number;
}

// ── §B · the answer-key review shapes — `isCorrect` exposed (review only, post-completion) ────────

/** §B.1 `StudentAssignmentReviewDto` — the **only** student surface exposing the answer key. */
export interface StudentAssignmentReview {
  /** the userAssignment id (echo of the route param). */
  id: string;
  sessionId: string;
  /** for the header "{sessionTitle} · Assignment review"; may be `null`. */
  sessionTitle: string | null;
  /** always `'Completed'` here (the endpoint gates it — §B.2). */
  status: AssignmentStatus;
  correctCount: number;
  questionCount: number;
  scoreMarks: number;
  maxMarks: number;
  /** `round(100 × scoreMarks / maxMarks)`; `0` when `maxMarks == 0`. */
  percent: number;
  timeSpentSeconds: number;
  completedAtUtc: string;
  questions: StudentReviewQuestion[];
}

/** One review question (§B.1) — `isCorrect` = the student picked the correct option. */
export interface StudentReviewQuestion {
  id: string;
  order: number;
  bodyLatex: string | null;
  imageUrl: string | null;
  /** the question's weight. */
  mark: number;
  hintUrl: string | null;
  options: StudentReviewOption[];
  /** what the student picked; `null` if unanswered. */
  selectedOptionId: string | null;
  isCorrect: boolean;
}

/** A review option (§B.1) — **`isCorrect` exposed** (review only). */
export interface StudentReviewOption {
  id: string;
  order: number;
  text: string;
  isCorrect: boolean;
}
