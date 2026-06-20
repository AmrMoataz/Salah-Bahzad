import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AssignmentReview, BehaviourEvent, QuizReview } from './attendance.models';

/**
 * Signal-backed data access for the Assignment/Behaviour review screen (Phase 5B-1, extended in 5B-2
 * with the **Quiz attempts** tab), wired one-method-per-endpoint to the FROZEN contracts
 * (`phase5b1-assignments-attendance.md` §C #7–8; `phase5b2-quizzes.md` §B #6). The platform JWT is
 * attached by the shared authInterceptor; review is **staff-only** (`AttendanceRead`) and — unlike the
 * student `§A` shape — the payload **exposes `isCorrect`** so the cards can show the correct option.
 *
 * `#review` backs the score header + Assignment tab; `#behaviour` backs the Behaviour-log timeline;
 * `#quizReview` backs the Quiz-attempts tab (`#quizMissing` distinguishes a **404 = no gating quiz**
 * empty state from a real load error).
 */
@Injectable({ providedIn: 'root' })
export class ReviewService {
  readonly #http = inject(HttpClient);

  readonly #review = signal<AssignmentReview | null>(null);
  readonly #behaviour = signal<BehaviourEvent[]>([]);
  readonly #quizReview = signal<QuizReview | null>(null);
  readonly #quizMissing = signal(false);
  readonly #quizLoading = signal(false);
  readonly #isLoading = signal(false);
  readonly #error = signal<string | null>(null);

  readonly review = this.#review.asReadonly();
  readonly behaviour = this.#behaviour.asReadonly();
  readonly quizReview = this.#quizReview.asReadonly();
  /** True once a quiz load resolved to a 404 — the gated session has no prerequisite quiz. */
  readonly quizMissing = this.#quizMissing.asReadonly();
  readonly quizLoading = this.#quizLoading.asReadonly();
  readonly isLoading = this.#isLoading.asReadonly();
  readonly error = this.#error.asReadonly();

  // ── Per-question submitted-vs-correct + score + time (#7) ───────────────────────
  async getReview(enrollmentId: string): Promise<AssignmentReview> {
    this.#isLoading.set(true);
    this.#error.set(null);
    try {
      const result = await firstValueFrom(
        this.#http.get<AssignmentReview>(
          `${this.#api()}/api/review/assignments/${enrollmentId}`,
        ),
      );
      this.#review.set(result);
      return result;
    } catch (err: unknown) {
      this.#error.set(this.#message(err));
      throw err;
    } finally {
      this.#isLoading.set(false);
    }
  }

  // ── In-assessment behaviour timeline (#8) ───────────────────────────────────────
  async getBehaviour(enrollmentId: string): Promise<BehaviourEvent[]> {
    try {
      const result = await firstValueFrom(
        this.#http.get<BehaviourEvent[]>(
          `${this.#api()}/api/review/assignments/${enrollmentId}/behaviour`,
        ),
      );
      this.#behaviour.set(result);
      return result;
    } catch (err: unknown) {
      this.#error.set(this.#message(err));
      throw err;
    }
  }

  // ── Quiz-attempts review (5B-2 §B #6) ───────────────────────────────────────────
  /**
   * Loads the gating quiz's attempts for one enrollment. A **404** is an expected empty state (the
   * gated session has no prerequisite quiz) → resolves to `null` and flags `quizMissing`; any other
   * error stores the message and rethrows so the caller can toast it.
   */
  async getQuizReview(enrollmentId: string): Promise<QuizReview | null> {
    this.#quizLoading.set(true);
    this.#quizMissing.set(false);
    this.#quizReview.set(null);
    try {
      const result = await firstValueFrom(
        this.#http.get<QuizReview>(`${this.#api()}/api/review/quizzes/${enrollmentId}`),
      );
      this.#quizReview.set(result);
      return result;
    } catch (err: unknown) {
      if (this.#isNotFound(err)) {
        this.#quizMissing.set(true);
        return null;
      }
      this.#error.set(this.#message(err));
      throw err;
    } finally {
      this.#quizLoading.set(false);
    }
  }

  /** Mirrors AuthStore: the API base URL is injected onto window to keep shared libs env-agnostic. */
  #api(): string {
    return (window as unknown as { __SB_API_URL__?: string }).__SB_API_URL__ ?? '';
  }

  /** A 404 from the quiz-review endpoint means "no gating quiz" — a normal empty state, not an error. */
  #isNotFound(err: unknown): boolean {
    return !!err && typeof err === 'object' && (err as { status?: number }).status === 404;
  }

  #message(err: unknown): string {
    if (err && typeof err === 'object' && 'error' in err) {
      const body = (err as { error?: unknown }).error;
      if (body && typeof body === 'object' && 'detail' in body) {
        const detail = (body as { detail?: unknown }).detail;
        if (typeof detail === 'string') return detail;
      }
    }
    return 'Something went wrong. Please try again.';
  }
}
