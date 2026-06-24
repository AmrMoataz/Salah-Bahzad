import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { MyPlanDto } from './plan.models';

/**
 * The authenticated **weekly plan** surface for the personalized Home (contract §A). The single call
 * is authenticated — it rides the `studentAuthInterceptor` (bearer attached, 401 → refresh-and-replay)
 * and carries the `withCredentials` device cookie. It is deliberately **not** exempted in the
 * interceptor's `ANONYMOUS_PATHS`.
 *
 * The student id + tenant come from the JWT server-side (no URL id, no IDOR). The plan is **derived
 * state** the UI renders read-only; the read itself changes no state and is **not** audited (§F).
 */
@Injectable({ providedIn: 'root' })
export class PlanService {
  readonly #http = inject(HttpClient);

  /**
   * `GET /api/me/plan` (**no query params** — contract §A) → the caller's current {@link MyPlanDto}:
   * KPI roll-up, the focus session, the gate-ordered steps (≤ 7), and the recently-enrolled rail.
   * **Always `200`** — an empty/onboarding plan when the caller has no active enrollments (never 404).
   * A `401`/`403` is an interceptor concern, not a Home concern. The wire shape equals the model
   * field-for-field, so the response is the model.
   */
  plan(): Observable<MyPlanDto> {
    return this.#http.get<MyPlanDto>(`${this.#apiUrl()}/api/me/plan`);
  }

  #apiUrl(): string {
    // Injected via main.ts — avoids importing `environment` into a lib (same shim as the stores).
    return (window as unknown as { __SB_API_URL__?: string }).__SB_API_URL__ ?? '';
  }
}
