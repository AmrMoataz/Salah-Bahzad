import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CatalogueFilters, CatalogueSession, Enrollment } from './catalogue.models';

/**
 * The authenticated catalogue surface (contract §A/§B). **Unlike the anonymous registration reads,
 * both calls are authenticated** — they ride the `studentAuthInterceptor` (bearer attached, 401 →
 * refresh-and-replay) and carry the `withCredentials` device cookie. They are deliberately **not**
 * exempted in the interceptor's `ANONYMOUS_PATHS`.
 *
 * The student id + tenant come from the JWT server-side (no URL id, no IDOR). The happy path calls
 * `catalogue()` with **no params** and filters by specialization client-side; the optional query
 * params exist for completeness and are honoured by the server (contract §A.1).
 */
@Injectable({ providedIn: 'root' })
export class CatalogueService {
  readonly #http = inject(HttpClient);

  /**
   * `GET /api/me/catalogue` → the tenant's **published** sessions (newest first), each with display
   * fields, the prerequisite badge + satisfied flag, and the caller's own enrollment state (§A.2).
   * The wire shape equals {@link CatalogueSession} field-for-field, so the response is the model.
   */
  catalogue(filters?: CatalogueFilters): Observable<CatalogueSession[]> {
    let params = new HttpParams();
    if (filters?.gradeId) params = params.set('gradeId', filters.gradeId);
    if (filters?.subjectId) params = params.set('subjectId', filters.subjectId);
    if (filters?.specializationId) params = params.set('specializationId', filters.specializationId);
    if (filters?.search) params = params.set('search', filters.search);

    return this.#http.get<CatalogueSession[]>(`${this.#apiUrl()}/api/me/catalogue`, { params });
  }

  /**
   * `POST /api/enrollments/redeem` with the frozen body `{ serial }` (contract §B.1) → `201 Created`
   * {@link Enrollment}. The serial is trimmed + upper-cased server-side; plain JSON, no `Content-Type`
   * fuss. A `400` (empty/too-long) or `409` (the six §B.3 cases) is surfaced to the caller verbatim.
   */
  redeem(serial: string): Observable<Enrollment> {
    return this.#http.post<Enrollment>(`${this.#apiUrl()}/api/enrollments/redeem`, { serial });
  }

  #apiUrl(): string {
    // Injected via main.ts — avoids importing `environment` into a lib (same shim as the stores).
    return (window as unknown as { __SB_API_URL__?: string }).__SB_API_URL__ ?? '';
  }
}
