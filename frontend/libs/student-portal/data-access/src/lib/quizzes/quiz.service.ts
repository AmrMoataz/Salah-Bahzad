import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  FocusEventBody,
  QuizAttempt,
  QuizAttemptResult,
  StudentQuiz,
  StudentQuizAttemptReview,
} from './quiz.models';

/**
 * The authenticated **Quizzes** surface — the proctored single-sitting runner (contract
 * `docs/contracts/student-s5-quizzes.md` §A, the REUSED 5B-2 engine: load → start → answer → submit
 * → focus) plus the **new** per-attempt answer-key review (§B). Every call is authenticated — they
 * ride the `studentAuthInterceptor` (bearer attached, 401 → refresh-and-replay) and carry the
 * `withCredentials` device cookie. **They are deliberately NOT exempted** in the interceptor's
 * `ANONYMOUS_PATHS` (`/api/me/quizzes` is not anonymous).
 *
 * The student id + tenant come from the JWT server-side (no URL id, no IDOR). `{attemptId}` ownership
 * is proven through the owning `UserQuiz.StudentId == currentUser.UserId`; a foreign / cross-tenant /
 * unknown id resolves to **404**, never another student's data (§0). The intro loads **by session**
 * (which it always has from the S3 session-detail context); the runner holds the started attempt's
 * `attemptId` from {@link start} — there is **no** per-attempt GET, no "list my quizzes".
 *
 * The **`QuizHub`** (forfeit-on-disconnect) is a SignalR client (`quiz-hub.client.ts` in
 * feature-assessment), **not** an HTTP call — it is not modelled here (§A.1).
 */
@Injectable({ providedIn: 'root' })
export class QuizService {
  readonly #http = inject(HttpClient);

  /**
   * `GET /api/me/quizzes/by-session/{sessionId}` → the caller's gating {@link StudentQuiz} for that
   * session (§A #1) — the **intro** shape (no questions, no correctness; `activeAttemptId` + each
   * attempt's additive `id`). A **`404`** means the session has no quiz — the intro routes back to
   * `/sessions/{id}`, not a hard error.
   */
  quiz(sessionId: string): Observable<StudentQuiz> {
    return this.#http.get<StudentQuiz>(
      `${this.#apiUrl()}/api/me/quizzes/by-session/${sessionId}`,
    );
  }

  /**
   * `POST /api/me/quizzes/{quizId}/attempts` → a fresh live {@link QuizAttempt} (§A #2): `attemptId`,
   * `deadlineUtc`, `serverNowUtc`, and the drawn randomised questions **without** `isCorrect`. A
   * **`409`** = attempts exhausted **or** an attempt already active (offer **Resume** instead).
   * **Open the `QuizHub` immediately after this resolves** (the attempt must exist first, §A.1).
   */
  start(quizId: string): Observable<QuizAttempt> {
    return this.#http.post<QuizAttempt>(
      `${this.#apiUrl()}/api/me/quizzes/${quizId}/attempts`,
      {},
    );
  }

  /**
   * `PUT /api/me/quizzes/attempts/{attemptId}/questions/{aqId}/answer` with body
   * `{ selectedOptionId }` → `204` (§A #3). **Save-as-you-go** — call on each pick (re-picking before
   * terminal re-`PUT`s). A **`409`** = the attempt is no longer `InProgress` / past `deadlineUtc`.
   */
  answer(attemptId: string, aqId: string, selectedOptionId: string): Observable<void> {
    return this.#http.put<void>(
      `${this.#apiUrl()}/api/me/quizzes/attempts/${attemptId}/questions/${aqId}/answer`,
      { selectedOptionId },
    );
  }

  /**
   * `POST /api/me/quizzes/attempts/{attemptId}/submit` → {@link QuizAttemptResult} (§A #4) — grades +
   * seals the attempt, updates best-of + pass, consumes it. Score-only (no questions, no `attemptId`).
   * A **`409`** = the attempt is already terminal (e.g. the Hangfire timer already `TimedOut` it — the
   * runner re-fetches `quiz(sessionId)` and reads the `TimedOut` summary, §C).
   */
  submit(attemptId: string): Observable<QuizAttemptResult> {
    return this.#http.post<QuizAttemptResult>(
      `${this.#apiUrl()}/api/me/quizzes/attempts/${attemptId}/submit`,
      {},
    );
  }

  /**
   * `POST /api/me/quizzes/attempts/{attemptId}/focus` with body
   * `{ type: 'FocusLost'|'FocusReturned', occurredAtUtc, durationMs? }` → `204` (§A #5). **Monitoring
   * only — never forfeits.** A **`400`** = a bad `type`.
   */
  focus(attemptId: string, body: FocusEventBody): Observable<void> {
    return this.#http.post<void>(
      `${this.#apiUrl()}/api/me/quizzes/attempts/${attemptId}/focus`,
      body,
    );
  }

  /**
   * `GET /api/me/quizzes/attempts/{attemptId}/review` → {@link StudentQuizAttemptReview} (§B) — the
   * caller's own **terminal** attempt with the answer key (per-option + per-question `isCorrect`, the
   * student's `selectedOptionId`, marks, score). The **only** student endpoint exposing quiz
   * correctness, and only post-termination. A **`403`** with `reason: "quiz_attempt_in_progress"` =
   * the caller's own but still `InProgress` (the deep-link edge — surface the "finish first" message);
   * a **`404`** = unknown / another student's / another tenant's (route back to `/sessions/{id}`).
   */
  review(attemptId: string): Observable<StudentQuizAttemptReview> {
    return this.#http.get<StudentQuizAttemptReview>(
      `${this.#apiUrl()}/api/me/quizzes/attempts/${attemptId}/review`,
    );
  }

  #apiUrl(): string {
    // Injected via main.ts — avoids importing `environment` into a lib (same shim as the stores).
    return (window as unknown as { __SB_API_URL__?: string }).__SB_API_URL__ ?? '';
  }
}
