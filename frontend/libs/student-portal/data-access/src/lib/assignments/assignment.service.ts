import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AssignmentEventBody,
  AssignmentProgress,
  StudentAssignment,
  StudentAssignmentReview,
} from './assignment.models';

/**
 * The authenticated **Assignments** surface — the open-book runner (contract
 * `docs/contracts/student-s4-assignments.md` §A, the reused 5B-1 engine: load → answer → behaviour
 * events) plus the **new** answer-key review (§B). Every call is authenticated — they ride the
 * `studentAuthInterceptor` (bearer attached, 401 → refresh-and-replay) and carry the
 * `withCredentials` device cookie. **They are deliberately NOT exempted** in the interceptor's
 * `ANONYMOUS_PATHS` (`/api/me/assignments` is not anonymous).
 *
 * The student id + tenant come from the JWT server-side (no URL id, no IDOR). `{assignmentId}`
 * ownership is proven by `UserAssignment.StudentId == currentUser.UserId`; a foreign / cross-tenant
 * / unknown id resolves to **404**, never another student's data (§0). The runner loads **by
 * session** (which it always has from the S3 session-detail context) — there is no "submit", no
 * "list my assignments", no per-assignment GET-by-id beyond the review read.
 */
@Injectable({ providedIn: 'root' })
export class AssignmentService {
  readonly #http = inject(HttpClient);

  /**
   * `GET /api/me/assignments/by-session/{sessionId}` → the caller's {@link StudentAssignment} for
   * that session (§A #1). A **`404`** means the caller has no enrollment for the session — the
   * runner routes back to `/sessions/{id}`, not a hard error. The re-`GET` returns the **saved
   * answers + accumulated `timeSpentSeconds`** (resumable, `FR-STU-ASG-002`). **No `isCorrect`.**
   */
  assignment(sessionId: string): Observable<StudentAssignment> {
    return this.#http.get<StudentAssignment>(
      `${this.#apiUrl()}/api/me/assignments/by-session/${sessionId}`,
    );
  }

  /**
   * `PUT /api/me/assignments/{assignmentId}/questions/{aqId}/answer` with body `{ selectedOptionId }`
   * → {@link AssignmentProgress} (§A #2). **It is a state change** — call it on each pick (persist
   * each immediately, no client draft). Answering the **last unanswered** question auto-grades
   * server-side (`Status` → `Completed`); re-answering after `Completed` → **`409`**.
   */
  answer(
    assignmentId: string,
    aqId: string,
    selectedOptionId: string,
  ): Observable<AssignmentProgress> {
    return this.#http.put<AssignmentProgress>(
      `${this.#apiUrl()}/api/me/assignments/${assignmentId}/questions/${aqId}/answer`,
      { selectedOptionId },
    );
  }

  /**
   * `POST /api/me/assignments/{assignmentId}/events` → `204` (§A #3). Appends a behaviour event and
   * **accrues time** (`elapsedMs` → `timeSpentSeconds`). The frontend posts `Entered` on open,
   * `Navigated` (with `questionOrder`) on prev/next, and `Left` on exit/route-away with the elapsed
   * delta. **`'Answered'` is NOT a valid `type` here** (it is logged by {@link answer}).
   */
  event(assignmentId: string, body: AssignmentEventBody): Observable<void> {
    return this.#http.post<void>(
      `${this.#apiUrl()}/api/me/assignments/${assignmentId}/events`,
      body,
    );
  }

  /**
   * `GET /api/me/assignments/{assignmentId}/review` → {@link StudentAssignmentReview} (§B) — the
   * caller's own **`Completed`** assignment with the answer key (per-option `isCorrect`, the
   * student's pick, marks, score). The **only** student endpoint that exposes correctness, and only
   * post-completion. A **`403`** with `reason: "assignment_in_progress"` = the caller's own but still
   * `InProgress` (the deep-link edge — surface the friendly "finish first" message); a **`404`** =
   * unknown / another student's / another tenant's (route back to `/sessions`).
   */
  review(assignmentId: string): Observable<StudentAssignmentReview> {
    return this.#http.get<StudentAssignmentReview>(
      `${this.#apiUrl()}/api/me/assignments/${assignmentId}/review`,
    );
  }

  #apiUrl(): string {
    // Injected via main.ts — avoids importing `environment` into a lib (same shim as the stores).
    return (window as unknown as { __SB_API_URL__?: string }).__SB_API_URL__ ?? '';
  }
}
