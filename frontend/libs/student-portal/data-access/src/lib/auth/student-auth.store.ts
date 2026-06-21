import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  Auth,
  GoogleAuthProvider,
  sendPasswordResetEmail,
  signInWithEmailAndPassword,
  signInWithPopup,
  signOut,
} from '@angular/fire/auth';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, firstValueFrom, map, of, shareReplay } from 'rxjs';
import {
  StudentAuthProblem,
  StudentAuthResponse,
  StudentAuthState,
  StudentBlockReason,
  StudentInfo,
} from './student-auth.models';
import { getDeviceFingerprint } from './device-fingerprint';

const TOKEN_KEY = 'sb_access_token';
const REFRESH_KEY = 'sb_refresh_token';
const STUDENT_KEY = 'sb_student';

const BLOCK_REASONS: readonly StudentBlockReason[] = [
  'account_pending',
  'account_rejected',
  'account_inactive',
  'device_not_recognized',
];

/**
 * Student-shaped sign-in store — the learner counterpart to the staff `AuthStore`. It is a
 * *ported* copy, not a shared one: it stores `sb_student`, navigates the student shell, and calls
 * the dedicated `POST /api/auth/student/exchange` (the shared store is staff-shaped and calls
 * `/api/auth/exchange`). Firebase verifies identity; the exchange trades the Firebase ID token for
 * a Student-role platform JWT pair, binds/enforces the one device (via the HttpOnly `sb_device`
 * cookie carried by `withCredentials`), and returns `StudentInfo`. A blocked `403` becomes a
 * `status` signal the router renders as a status screen rather than a form error (§1.4).
 */
@Injectable({ providedIn: 'root' })
export class StudentAuthStore {
  readonly #firebaseAuth = inject(Auth);
  readonly #http = inject(HttpClient);
  readonly #router = inject(Router);

  readonly #state = signal<StudentAuthState>({
    student: null,
    accessToken: null,
    refreshToken: null,
    isLoading: false,
    error: null,
    status: null,
    statusDetail: null,
  });

  /** Shared in-flight refresh so a burst of concurrent 401s triggers exactly one refresh call. */
  #refreshInFlight: Observable<string | null> | null = null;

  readonly student = computed(() => this.#state().student);
  readonly isAuthenticated = computed(() => this.#state().student !== null);
  readonly isLoading = computed(() => this.#state().isLoading);
  readonly error = computed(() => this.#state().error);
  /** Blocked-sign-in reason (`null` when not blocked) — the router/status screen reads this. */
  readonly status = computed(() => this.#state().status);
  readonly statusDetail = computed(() => this.#state().statusDetail);
  readonly fullName = computed(() => this.#state().student?.fullName ?? '');
  readonly firstName = computed(() => this.fullName().split(' ')[0] ?? '');

  getAccessToken(): string | null {
    return this.#state().accessToken ?? sessionStorage.getItem(TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return this.#state().refreshToken ?? sessionStorage.getItem(REFRESH_KEY);
  }

  /**
   * Exchanges the stored refresh token for a fresh access+refresh pair (FR-PLAT-AUTH-002). The
   * student refresh re-checks `Active` + the bound device server-side and preserves `device_id`.
   * Concurrent callers share a single in-flight request (one server hit). Emits the new access
   * token, or `null` when there is no refresh token or the server rejects it; in that failure
   * case the session is cleared and the user is redirected to `/login`.
   */
  refreshAccessToken(): Observable<string | null> {
    if (this.#refreshInFlight) return this.#refreshInFlight;

    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      this.#handleRefreshFailure();
      return of(null);
    }

    const apiUrl = this.#getApiUrl();
    this.#refreshInFlight = this.#http
      .post<StudentAuthResponse>(
        `${apiUrl}/api/auth/refresh`,
        { refreshToken },
        // The device cookie rides so the server can re-verify the binding on refresh.
        { withCredentials: true },
      )
      .pipe(
        map((response) => {
          this.#applyTokenResponse(response);
          this.#refreshInFlight = null;
          return response.accessToken;
        }),
        catchError(() => {
          this.#refreshInFlight = null;
          this.#handleRefreshFailure();
          return of<string | null>(null);
        }),
        shareReplay(1),
      );

    return this.#refreshInFlight;
  }

  /** Refresh is no longer possible — drop the dead session and send the user back to sign in. */
  #handleRefreshFailure(): void {
    this.#clearSession();
    void this.#router.navigate(['/login']);
  }

  /** Email + password sign-in: Firebase → student exchange. */
  async signIn(email: string, password: string): Promise<void> {
    this.#beginLoading();
    try {
      const credential = await signInWithEmailAndPassword(this.#firebaseAuth, email, password);
      const firebaseIdToken = await credential.user.getIdToken();
      await this.#exchange(firebaseIdToken);
    } catch (err: unknown) {
      this.#failLoading(err);
      throw err;
    }
  }

  /** Google social sign-in: Firebase popup → student exchange. */
  async signInWithGoogle(): Promise<void> {
    this.#beginLoading();
    try {
      const credential = await signInWithPopup(this.#firebaseAuth, new GoogleAuthProvider());
      const firebaseIdToken = await credential.user.getIdToken();
      await this.#exchange(firebaseIdToken);
    } catch (err: unknown) {
      this.#failLoading(err);
      throw err;
    }
  }

  /**
   * Trades a verified Firebase ID token for a Student platform JWT pair. Sends `withCredentials`
   * (so the `Set-Cookie: sb_device` is stored) and an `X-Device-Fingerprint` header (§1.3). On a
   * blocked `403` it parks a `status` and routes to `/status`; any other failure is rethrown so
   * the caller surfaces a form error.
   */
  async #exchange(firebaseIdToken: string): Promise<void> {
    const apiUrl = this.#getApiUrl();
    try {
      const response = await firstValueFrom(
        this.#http.post<StudentAuthResponse>(
          `${apiUrl}/api/auth/student/exchange`,
          { firebaseIdToken },
          {
            withCredentials: true,
            headers: { 'X-Device-Fingerprint': getDeviceFingerprint() },
          },
        ),
      );
      this.#applyTokenResponse(response);
      await this.#router.navigate(['/']);
    } catch (err: unknown) {
      const blocked = this.#asBlockReason(err);
      if (blocked) {
        this.#state.update((s) => ({
          ...s,
          isLoading: false,
          error: null,
          status: blocked.reason,
          statusDetail: blocked.detail,
        }));
        await this.#router.navigate(['/status']);
        return;
      }
      throw err;
    }
  }

  async signOut(): Promise<void> {
    await signOut(this.#firebaseAuth);
    this.#clearSession();
    await this.#router.navigate(['/login']);
  }

  /**
   * Sends a Firebase password-reset email (FR-PLAT-AUTH-009 — the platform stores no passwords).
   * The student is not signed in at this point, so the address comes from the login form.
   */
  async requestPasswordReset(email: string): Promise<void> {
    if (!email) throw new Error('Enter your email first.');
    await sendPasswordResetEmail(this.#firebaseAuth, email);
  }

  /** Clears a parked blocked-status (e.g. "Back to sign in" from a status screen). */
  clearStatus(): void {
    this.#state.update((s) => ({ ...s, status: null, statusDetail: null, error: null }));
  }

  restoreSession(): boolean {
    const accessToken = sessionStorage.getItem(TOKEN_KEY);
    const refreshToken = sessionStorage.getItem(REFRESH_KEY);
    const studentJson = sessionStorage.getItem(STUDENT_KEY);

    if (!accessToken || !studentJson) return false;

    try {
      const student = JSON.parse(studentJson) as StudentInfo;
      this.#state.update((s) => ({ ...s, student, accessToken, refreshToken }));
      return true;
    } catch {
      return false;
    }
  }

  #beginLoading(): void {
    this.#state.update((s) => ({ ...s, isLoading: true, error: null, status: null, statusDetail: null }));
  }

  #failLoading(err: unknown): void {
    this.#state.update((s) => ({ ...s, isLoading: false, error: this.#extractErrorMessage(err) }));
  }

  #applyTokenResponse(response: StudentAuthResponse): void {
    sessionStorage.setItem(TOKEN_KEY, response.accessToken);
    sessionStorage.setItem(REFRESH_KEY, response.refreshToken);
    sessionStorage.setItem(STUDENT_KEY, JSON.stringify(response.student));

    this.#state.update((s) => ({
      ...s,
      student: response.student,
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
      isLoading: false,
      error: null,
      status: null,
      statusDetail: null,
    }));
  }

  #clearSession(): void {
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(REFRESH_KEY);
    sessionStorage.removeItem(STUDENT_KEY);
    this.#state.set({
      student: null,
      accessToken: null,
      refreshToken: null,
      isLoading: false,
      error: null,
      status: null,
      statusDetail: null,
    });
  }

  #getApiUrl(): string {
    // Injected via main.ts — avoids importing `environment` into a lib.
    return (window as unknown as { __SB_API_URL__?: string }).__SB_API_URL__ ?? '';
  }

  /** Recognises a blocked-sign-in `403` and pulls out its machine `reason` + readable `detail`. */
  #asBlockReason(err: unknown): { reason: StudentBlockReason; detail: string | null } | null {
    if (err instanceof HttpErrorResponse && err.status === 403) {
      const body = (err.error ?? {}) as StudentAuthProblem;
      if (body.reason && BLOCK_REASONS.includes(body.reason)) {
        return { reason: body.reason, detail: body.detail ?? null };
      }
    }
    return null;
  }

  #extractErrorMessage(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      // 401 = "this account doesn't have student access"; prefer the server's readable detail.
      const detail = (err.error as StudentAuthProblem | undefined)?.detail;
      if (detail) return detail;
      if (err.status === 401) return 'This account doesn’t have student access.';
      if (err.status === 0) return 'Network error. Please check your connection.';
      return 'Sign-in failed. Please try again.';
    }
    if (err instanceof Error) {
      const code = (err as { code?: string }).code;
      if (code?.startsWith('auth/')) return this.#firebaseErrorToMessage(code);
      return err.message;
    }
    return 'An unexpected error occurred.';
  }

  #firebaseErrorToMessage(code: string): string {
    switch (code) {
      case 'auth/invalid-credential':
      case 'auth/wrong-password':
      case 'auth/user-not-found':
        return 'Invalid email or password.';
      case 'auth/too-many-requests':
        return 'Too many sign-in attempts. Please wait a moment and try again.';
      case 'auth/user-disabled':
        return 'Your account has been disabled.';
      case 'auth/popup-closed-by-user':
      case 'auth/cancelled-popup-request':
        return 'Google sign-in was cancelled.';
      case 'auth/popup-blocked':
        return 'Your browser blocked the Google sign-in popup. Allow popups and try again.';
      case 'auth/network-request-failed':
        return 'Network error. Please check your connection.';
      default:
        return 'Sign-in failed. Please try again.';
    }
  }
}
