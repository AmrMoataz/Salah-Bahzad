import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AssignmentReview, BehaviourEvent } from './attendance.models';

/**
 * Signal-backed data access for the Assignment/Behaviour review screen (Phase 5B-1), wired
 * one-method-per-endpoint to the FROZEN contract
 * (`docs/contracts/phase5b1-assignments-attendance.md` §C, endpoints #7–8). The platform JWT is
 * attached by the shared authInterceptor; review is **staff-only** (`AttendanceRead`) and — unlike the
 * student `§A` shape — the payload **exposes `isCorrect`** so the cards can show the correct option.
 *
 * `#review` backs the score header + Assignment tab; `#behaviour` backs the Behaviour-log timeline.
 * (The **Quiz attempts** tab has no endpoint in 5B-1 — it is 5B-2 and renders a disabled placeholder.)
 */
@Injectable({ providedIn: 'root' })
export class ReviewService {
  readonly #http = inject(HttpClient);

  readonly #review = signal<AssignmentReview | null>(null);
  readonly #behaviour = signal<BehaviourEvent[]>([]);
  readonly #isLoading = signal(false);
  readonly #error = signal<string | null>(null);

  readonly review = this.#review.asReadonly();
  readonly behaviour = this.#behaviour.asReadonly();
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

  /** Mirrors AuthStore: the API base URL is injected onto window to keep shared libs env-agnostic. */
  #api(): string {
    return (window as unknown as { __SB_API_URL__?: string }).__SB_API_URL__ ?? '';
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
