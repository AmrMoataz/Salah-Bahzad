import { Injectable, signal, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Auth, sendPasswordResetEmail, signInWithEmailAndPassword, signOut } from '@angular/fire/auth';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthState, AuthTokenResponse, StaffInfo } from './auth.models';

const TOKEN_KEY = 'sb_access_token';
const REFRESH_KEY = 'sb_refresh_token';

@Injectable({ providedIn: 'root' })
export class AuthStore {
  readonly #firebaseAuth = inject(Auth);
  readonly #http = inject(HttpClient);
  readonly #router = inject(Router);

  readonly #state = signal<AuthState>({
    staff: null,
    accessToken: null,
    refreshToken: null,
    isLoading: false,
    error: null,
  });

  readonly staff = computed(() => this.#state().staff);
  readonly isAuthenticated = computed(() => this.#state().staff !== null);
  readonly isLoading = computed(() => this.#state().isLoading);
  readonly error = computed(() => this.#state().error);
  readonly role = computed(() => this.#state().staff?.role ?? null);

  hasPermission(permission: string): boolean {
    return this.#state().staff?.permissions.includes(permission) ?? false;
  }

  getAccessToken(): string | null {
    return this.#state().accessToken ?? sessionStorage.getItem(TOKEN_KEY);
  }

  async signIn(email: string, password: string): Promise<void> {
    this.#state.update((s) => ({ ...s, isLoading: true, error: null }));

    try {
      // Step 1: Authenticate with Firebase
      const userCredential = await signInWithEmailAndPassword(
        this.#firebaseAuth,
        email,
        password,
      );
      const firebaseIdToken = await userCredential.user.getIdToken();

      // Step 2: Exchange Firebase token for platform JWT
      const apiUrl = this.#getApiUrl();
      const response = await firstValueFrom(
        this.#http.post<AuthTokenResponse>(`${apiUrl}/api/auth/exchange`, {
          firebaseIdToken,
        }),
      );

      this.#applyTokenResponse(response);
      await this.#router.navigate(['/dashboard']);
    } catch (err: unknown) {
      const message = this.#extractErrorMessage(err);
      this.#state.update((s) => ({ ...s, isLoading: false, error: message }));
      throw err;
    }
  }

  async signOut(): Promise<void> {
    await signOut(this.#firebaseAuth);
    this.#clearSession();
    await this.#router.navigate(['/login']);
  }

  /**
   * Sends a Firebase password-reset email to the signed-in user's address. Password management is
   * delegated entirely to Firebase self-service — the platform stores no passwords (FR-PLAT-AUTH-009).
   */
  async requestPasswordReset(): Promise<void> {
    const email = this.#state().staff?.email;
    if (!email) throw new Error('No signed-in user.');
    await sendPasswordResetEmail(this.#firebaseAuth, email);
  }

  /**
   * Persists a new display name for the signed-in staff member (Settings → Profile, FR-ADM-SET-001)
   * and refreshes the cached identity so the new name appears across the app immediately. Email and
   * password stay managed by the authentication provider (Firebase) and are not changed here.
   */
  async updateDisplayName(displayName: string): Promise<void> {
    const apiUrl = this.#getApiUrl();
    const updated = await firstValueFrom(
      this.#http.put<{ displayName: string }>(`${apiUrl}/api/profile`, { displayName }),
    );

    const current = this.#state().staff;
    if (!current) return;

    const staff: StaffInfo = { ...current, displayName: updated.displayName };
    sessionStorage.setItem('sb_staff', JSON.stringify(staff));
    this.#state.update((s) => ({ ...s, staff }));
  }

  restoreSession(): boolean {
    const accessToken = sessionStorage.getItem(TOKEN_KEY);
    const staffJson = sessionStorage.getItem('sb_staff');

    if (!accessToken || !staffJson) return false;

    try {
      const staff = JSON.parse(staffJson) as StaffInfo;
      this.#state.update((s) => ({ ...s, staff, accessToken }));
      return true;
    } catch {
      return false;
    }
  }

  #applyTokenResponse(response: AuthTokenResponse): void {
    sessionStorage.setItem(TOKEN_KEY, response.accessToken);
    sessionStorage.setItem(REFRESH_KEY, response.refreshToken);
    sessionStorage.setItem('sb_staff', JSON.stringify(response.staff));

    this.#state.update((s) => ({
      ...s,
      staff: response.staff,
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
      isLoading: false,
      error: null,
    }));
  }

  #clearSession(): void {
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(REFRESH_KEY);
    sessionStorage.removeItem('sb_staff');
    this.#state.set({
      staff: null,
      accessToken: null,
      refreshToken: null,
      isLoading: false,
      error: null,
    });
  }

  #getApiUrl(): string {
    // Injected via environment — avoids importing environment directly in shared lib
    return (window as unknown as { __SB_API_URL__?: string }).__SB_API_URL__ ?? '';
  }

  #extractErrorMessage(err: unknown): string {
    if (err instanceof Error) {
      const code = (err as { code?: string }).code;
      if (code?.startsWith('auth/')) {
        return this.#firebaseErrorToMessage(code);
      }
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
      case 'auth/network-request-failed':
        return 'Network error. Please check your connection.';
      default:
        return 'Sign-in failed. Please try again.';
    }
  }
}
