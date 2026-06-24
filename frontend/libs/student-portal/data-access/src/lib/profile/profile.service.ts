import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { StudentProfile, UpdateMyStudentProfile } from './profile.models';

/**
 * The authenticated self-service **Profile** surface (contract `docs/contracts/student-s6-profile.md`
 * §A). Like the catalogue/sessions reads — and **unlike** the anonymous registration/reference reads —
 * both calls are authenticated: they ride the `studentAuthInterceptor` (bearer attached, 401 →
 * refresh-and-replay) and carry the `withCredentials` device cookie. They are deliberately **not**
 * exempted in the interceptor's `ANONYMOUS_PATHS`.
 *
 * The student id + tenant come from the JWT server-side (no URL id, no IDOR). The caller's `Student`
 * row is the JWT subject, so it **always exists** — there is **no 404-self** and **no 409** (§B); a
 * `400` (validation: empty/too-long, or an unknown/mismatched city/region) surfaces as an
 * `HttpErrorResponse` for the component to render inline.
 */
@Injectable({ providedIn: 'root' })
export class ProfileService {
  readonly #http = inject(HttpClient);

  /**
   * `GET /api/me/profile` → the caller's own {@link StudentProfile} with resolved grade/city/region
   * names + the active bound-device summary (§A #1). The wire shape **equals** {@link StudentProfile}
   * field-for-field, so the response is the model (no mapping). Pure read — **not audited** (§E).
   */
  getProfile(): Observable<StudentProfile> {
    return this.#http.get<StudentProfile>(`${this.#apiUrl()}/api/me/profile`);
  }

  /**
   * `PUT /api/me/profile` with the **seven** writable fields (§A #2) → the **re-read**
   * {@link StudentProfile} (grade left unchanged; any email/grade in the body is ignored server-side).
   * The update is **audited automatically** by the `SaveChanges` interceptor (§E).
   */
  updateProfile(body: UpdateMyStudentProfile): Observable<StudentProfile> {
    return this.#http.put<StudentProfile>(`${this.#apiUrl()}/api/me/profile`, body);
  }

  #apiUrl(): string {
    // Injected via main.ts — avoids importing `environment` into a lib (same shim as the stores).
    return (window as unknown as { __SB_API_URL__?: string }).__SB_API_URL__ ?? '';
  }
}
