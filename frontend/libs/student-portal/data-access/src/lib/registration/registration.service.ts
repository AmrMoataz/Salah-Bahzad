import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import {
  Auth,
  GoogleAuthProvider,
  User,
  createUserWithEmailAndPassword,
  signInWithPopup,
} from '@angular/fire/auth';
import { Observable, from, switchMap } from 'rxjs';
import {
  CityRef,
  GoogleProfile,
  GradeRef,
  RegisterFormData,
  RegisterResult,
  RegionRef,
  RegistrationConfig,
} from './registration.models';

/**
 * Resolves the client-supplied registration constants from the `window.__SB_*__` runtime shim
 * (set in `main.ts` from `environment`, contract §F). Kept as a free function so the wizard, the
 * grades fetch, and the register POST share one source — and so tests can pin the values.
 */
export function registrationConfig(): RegistrationConfig {
  const w = window as unknown as { __SB_TENANT__?: string; __SB_TERMS_VERSION__?: string };
  return {
    tenantSlug: w.__SB_TENANT__ ?? 'salah-bahzad',
    termsVersion: w.__SB_TERMS_VERSION__ ?? 'v1',
  };
}

/**
 * The anonymous half of self-registration (contract §A/§B). Runs *before* a student exists, so it
 * carries **no platform JWT** — identity is the Firebase ID token the register form ships. It owns
 * two responsibilities:
 *
 *  1. **Firebase account creation** (`FR-STU-REG-002`, `FR-PLAT-AUTH-003`) — `createEmailAccount`
 *     (email/password, minted at submit) or `signUpWithGoogle` (popup at Step 1, prefills name +
 *     email). It holds the resulting Firebase user and, unlike the sign-in store, **never calls the
 *     student exchange** (there is no student yet). `register` reads a fresh ID token off the held
 *     user just before posting.
 *  2. **The anonymous HTTP** — the three reference reads for the dropdowns and the multipart
 *     `POST /api/students/register` with the **exact** contract §A.1 field names. `Content-Type` is
 *     left unset so the browser stamps the multipart boundary.
 */
@Injectable({ providedIn: 'root' })
export class RegistrationService {
  readonly #http = inject(HttpClient);
  readonly #firebaseAuth = inject(Auth);

  /** The Firebase user created/connected during the wizard; its token authenticates the register. */
  #firebaseUser: User | null = null;

  // ── Reference reads (contract §B) ───────────────────────────────────────────
  /** Tenant-scoped grades for the wizard (anonymous, resolved by `?tenantSlug=`). */
  grades(): Observable<GradeRef[]> {
    const slug = encodeURIComponent(registrationConfig().tenantSlug);
    return this.#http.get<GradeRef[]>(`${this.#apiUrl()}/api/reference/grades?tenantSlug=${slug}`);
  }

  /** Global cities. */
  cities(): Observable<CityRef[]> {
    return this.#http.get<CityRef[]>(`${this.#apiUrl()}/api/reference/cities`);
  }

  /** Global regions for a city (the city→region cascade). */
  regions(cityId: string): Observable<RegionRef[]> {
    return this.#http.get<RegionRef[]>(`${this.#apiUrl()}/api/reference/cities/${cityId}/regions`);
  }

  // ── Firebase identity (contract §A.1 `firebaseIdToken`) ──────────────────────
  /**
   * Email/password sign-up — minted at **final submit** so the short-lived ID token is fresh when
   * the register POST carries it. Holds the credential for `register` to read the token from.
   */
  async createEmailAccount(email: string, password: string): Promise<void> {
    const credential = await createUserWithEmailAndPassword(this.#firebaseAuth, email, password);
    this.#firebaseUser = credential.user;
  }

  /**
   * Google social sign-up — runs the popup at **Step 1**, holds the signed-in user, and returns the
   * `displayName`/`email` to prefill the wizard (the email is shown read-only). The token is read
   * later at submit via `register`.
   */
  async signUpWithGoogle(): Promise<GoogleProfile> {
    const credential = await signInWithPopup(this.#firebaseAuth, new GoogleAuthProvider());
    this.#firebaseUser = credential.user;
    return { fullName: credential.user.displayName, email: credential.user.email };
  }

  // ── Register (contract §A) ───────────────────────────────────────────────────
  /**
   * Posts the multipart registration. Reads a **fresh** Firebase ID token off the held user, then
   * assembles `FormData` with the exact frozen field names (§A.1) — the `tenantSlug`/`termsVersion`
   * constants from the runtime config and the picked ID image (appended last). No `Content-Type`
   * header: the browser sets the multipart boundary.
   */
  register(form: RegisterFormData): Observable<RegisterResult> {
    return from(this.#freshIdToken()).pipe(
      switchMap((firebaseIdToken) => {
        const { tenantSlug, termsVersion } = registrationConfig();
        const body = new FormData();
        body.append('firebaseIdToken', firebaseIdToken);
        body.append('tenantSlug', tenantSlug);
        body.append('fullName', form.fullName);
        body.append('phoneNumber', form.phoneNumber);
        body.append('parentPhonePrimary', form.parentPhonePrimary);
        if (form.parentPhoneSecondary) {
          body.append('parentPhoneSecondary', form.parentPhoneSecondary);
        }
        body.append('gradeId', form.gradeId);
        body.append('cityId', form.cityId);
        body.append('regionId', form.regionId);
        body.append('schoolName', form.schoolName);
        body.append('termsVersion', termsVersion);
        // File part last (the handler binds [FromForm] params + IFormFile separately).
        body.append('idImage', form.idImage, form.idImage.name);

        return this.#http.post<RegisterResult>(`${this.#apiUrl()}/api/students/register`, body);
      }),
    );
  }

  /** Forgets the held Firebase user (e.g. when the student backs out / a fresh attempt starts). */
  reset(): void {
    this.#firebaseUser = null;
  }

  async #freshIdToken(): Promise<string> {
    if (!this.#firebaseUser) {
      throw new Error('No Firebase account — create one (email/password or Google) before submitting.');
    }
    return this.#firebaseUser.getIdToken();
  }

  #apiUrl(): string {
    // Injected via main.ts — avoids importing `environment` into a lib (same shim as the store).
    return (window as unknown as { __SB_API_URL__?: string }).__SB_API_URL__ ?? '';
  }
}
