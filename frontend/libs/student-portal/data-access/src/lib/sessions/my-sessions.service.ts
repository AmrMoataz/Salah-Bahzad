import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  MySession,
  MySessionDetail,
  MySessionFilter,
  PlaybackHandoff,
  SignedUrl,
} from './my-sessions.models';

/**
 * The authenticated **My Sessions** surface (contract §A/§B/§C + the 5C Play gate §D). Every call is
 * authenticated — they ride the `studentAuthInterceptor` (bearer attached, 401 → refresh-and-replay)
 * and carry the `withCredentials` device cookie. They are deliberately **not** exempted in the
 * interceptor's `ANONYMOUS_PATHS`.
 *
 * The student id + tenant come from the JWT server-side (no URL id, no IDOR). `{id}` in the detail /
 * material reads is a **session** id whose ownership is proven by the caller's own enrollment — a
 * non-enrolled / cross-student / cross-tenant id resolves to **404**, never another student's data.
 */
@Injectable({ providedIn: 'root' })
export class MySessionsService {
  readonly #http = inject(HttpClient);

  /**
   * `GET /api/me/sessions` (+ optional `?state=`) → the caller's enrolled sessions (`Active` incl.
   * past-expiry; **not** `Refunded`/soft-deleted), newest-enrolled first (§A.2). The wire shape equals
   * {@link MySession} field-for-field, so the response is the model. The happy path calls this with
   * **no** param and filters client-side; `state` exists for completeness and is honoured server-side.
   */
  mySessions(state?: MySessionFilter): Observable<MySession[]> {
    let params = new HttpParams();
    if (state) params = params.set('state', state);
    return this.#http.get<MySession[]>(`${this.#apiUrl()}/api/me/sessions`, { params });
  }

  /**
   * `GET /api/me/sessions/{id}` → the full study view for one enrolled session (§B.1). A **`404`**
   * means the id is not a session the caller is enrolled in (unknown / other tenant / only a
   * `Refunded` enrollment) — the caller routes back to `/sessions`, **not** a hard error.
   */
  session(id: string): Observable<MySessionDetail> {
    return this.#http.get<MySessionDetail>(`${this.#apiUrl()}/api/me/sessions/${id}`);
  }

  /**
   * `GET /api/me/sessions/{id}/materials/{materialId}/url` → a short-lived signed R2 URL to download
   * one material of an enrolled session (§C). Available while expired (materials stay reachable after
   * expiry — `FR-STU-SES-001`); a `404` means the material isn't part of an enrolled session.
   */
  materialUrl(sessionId: string, materialId: string): Observable<SignedUrl> {
    return this.#http.get<SignedUrl>(
      `${this.#apiUrl()}/api/me/sessions/${sessionId}/materials/${materialId}/url`,
    );
  }

  /**
   * `POST /api/me/videos/{videoId}/playback` → the frozen 5C Play gate (§D). The handler authorizes →
   * **decrements one view** → audits → issues a one-time handoff code. **It is a state change** — call
   * it **once** per Play, never speculatively. On `200` the browser builds the deep link from the
   * returned {@link PlaybackHandoff}; a gate failure returns a ProblemDetails with one of the five
   * `reason` codes (§D.2), rendered verbatim by the UI.
   */
  startPlayback(videoId: string): Observable<PlaybackHandoff> {
    return this.#http.post<PlaybackHandoff>(
      `${this.#apiUrl()}/api/me/videos/${videoId}/playback`,
      {},
    );
  }

  #apiUrl(): string {
    // Injected via main.ts — avoids importing `environment` into a lib (same shim as the stores).
    return (window as unknown as { __SB_API_URL__?: string }).__SB_API_URL__ ?? '';
  }
}
