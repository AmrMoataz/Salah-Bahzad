import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { Auth } from '@angular/fire/auth';
import { StudentAuthStore } from './student-auth.store';
import { StudentAuthResponse, StudentBlockReason } from './student-auth.models';

// Replace @angular/fire/auth (ESM, unparsable by jest) with a CJS double. The store calls these
// module functions directly and injects `Auth` as a DI token — both are satisfied here.
jest.mock('@angular/fire/auth', () => ({
  Auth: class Auth {},
  GoogleAuthProvider: class GoogleAuthProvider {},
  signInWithEmailAndPassword: jest.fn(),
  signInWithPopup: jest.fn(),
  signOut: jest.fn(),
  sendPasswordResetEmail: jest.fn(),
}));

// Typed handles to the mocked module functions.
import {
  signInWithEmailAndPassword,
  signInWithPopup,
  signOut,
  sendPasswordResetEmail,
} from '@angular/fire/auth';
const mockEmailSignIn = signInWithEmailAndPassword as jest.Mock;
const mockGoogleSignIn = signInWithPopup as jest.Mock;
const mockSignOut = signOut as jest.Mock;
const mockResetEmail = sendPasswordResetEmail as jest.Mock;

const EXCHANGE_URL = '/api/auth/student/exchange';
const REFRESH_URL = '/api/auth/refresh';

/** Drains microtasks (firebase awaits) so the store's HTTP call is dispatched before we assert. */
const tick = () => new Promise<void>((resolve) => setTimeout(resolve, 0));

function makeResponse(over: Partial<StudentAuthResponse> = {}): StudentAuthResponse {
  return {
    accessToken: 'access-1',
    refreshToken: 'refresh-1',
    accessTokenExpiresAt: '2026-06-21T10:15:00Z',
    refreshTokenExpiresAt: '2026-06-28T10:00:00Z',
    student: {
      id: 'stu-1',
      fullName: 'Lina Hassan',
      status: 'Active',
      boundDevice: { summary: 'Windows · Chrome', boundAtUtc: '2026-06-21T10:00:00Z' },
    },
    ...over,
  };
}

describe('StudentAuthStore', () => {
  let store: StudentAuthStore;
  let httpMock: HttpTestingController;
  let router: { navigate: jest.Mock };

  beforeEach(() => {
    (window as unknown as { __SB_API_URL__: string }).__SB_API_URL__ = '';
    sessionStorage.clear();
    jest.clearAllMocks();
    router = { navigate: jest.fn().mockResolvedValue(true) };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Auth, useValue: {} },
        { provide: Router, useValue: router },
      ],
    });

    store = TestBed.inject(StudentAuthStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('exchanges email/password sign-in: maps StudentAuthResponse, persists, and lands in the shell', async () => {
    mockEmailSignIn.mockResolvedValue({ user: { getIdToken: async () => 'firebase-token' } });

    const done = store.signIn('lina@example.com', 'password');
    await tick();

    const req = httpMock.expectOne(EXCHANGE_URL);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ firebaseIdToken: 'firebase-token' });
    expect(req.request.withCredentials).toBe(true);
    expect(req.request.headers.get('X-Device-Fingerprint')).toBeTruthy();
    req.flush(makeResponse());
    await done;

    expect(store.isAuthenticated()).toBe(true);
    expect(store.student()?.fullName).toBe('Lina Hassan');
    expect(store.firstName()).toBe('Lina');
    expect(sessionStorage.getItem('sb_access_token')).toBe('access-1');
    expect(sessionStorage.getItem('sb_refresh_token')).toBe('refresh-1');
    expect(JSON.parse(sessionStorage.getItem('sb_student') ?? '{}').id).toBe('stu-1');
    expect(router.navigate).toHaveBeenCalledWith(['/']);
    expect(store.status()).toBeNull();
  });

  it('exchanges Google sign-in the same way', async () => {
    mockGoogleSignIn.mockResolvedValue({ user: { getIdToken: async () => 'google-token' } });

    const done = store.signInWithGoogle();
    await tick();

    const req = httpMock.expectOne(EXCHANGE_URL);
    expect(req.request.body).toEqual({ firebaseIdToken: 'google-token' });
    req.flush(makeResponse());
    await done;

    expect(store.isAuthenticated()).toBe(true);
    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });

  // Each blocked 403 reason → the matching status (no form error, route to /status).
  const reasons: { reason: StudentBlockReason; detail: string }[] = [
    { reason: 'account_pending', detail: 'Your account is pending approval.' },
    { reason: 'account_rejected', detail: 'Blurry ID photo — please re-upload.' },
    { reason: 'account_inactive', detail: 'Your account has been deactivated.' },
    { reason: 'device_not_recognized', detail: "This device isn't recognised." },
  ];

  for (const { reason, detail } of reasons) {
    it(`maps a 403 "${reason}" to status() + statusDetail() and routes to /status`, async () => {
      mockEmailSignIn.mockResolvedValue({ user: { getIdToken: async () => 'firebase-token' } });

      const done = store.signIn('lina@example.com', 'password');
      await tick();

      const req = httpMock.expectOne(EXCHANGE_URL);
      req.flush({ reason, detail }, { status: 403, statusText: 'Forbidden' });
      await done;

      expect(store.status()).toBe(reason);
      expect(store.statusDetail()).toBe(detail);
      expect(store.isAuthenticated()).toBe(false);
      expect(store.error()).toBeNull();
      expect(router.navigate).toHaveBeenCalledWith(['/status']);
    });
  }

  it('surfaces a 401 "no student access" as a form error (not a status screen)', async () => {
    mockEmailSignIn.mockResolvedValue({ user: { getIdToken: async () => 'firebase-token' } });

    const done = store.signIn('staff@example.com', 'password');
    await tick();

    const req = httpMock.expectOne(EXCHANGE_URL);
    req.flush(
      { detail: 'This account doesn’t have student access.' },
      { status: 401, statusText: 'Unauthorized' },
    );

    await expect(done).rejects.toBeTruthy();
    expect(store.status()).toBeNull();
    expect(store.error()).toBe('This account doesn’t have student access.');
    expect(store.isAuthenticated()).toBe(false);
  });

  it('maps Firebase auth errors to friendly copy', async () => {
    mockEmailSignIn.mockRejectedValue(Object.assign(new Error('x'), { code: 'auth/invalid-credential' }));

    await expect(store.signIn('lina@example.com', 'nope')).rejects.toBeTruthy();
    expect(store.error()).toBe('Invalid email or password.');
    httpMock.expectNone(EXCHANGE_URL);
  });

  it('single-flights refresh: concurrent 401-driven refreshes hit /api/auth/refresh once', async () => {
    sessionStorage.setItem('sb_refresh_token', 'refresh-seed');

    const first: (string | null)[] = [];
    const second: (string | null)[] = [];
    store.refreshAccessToken().subscribe((t) => first.push(t));
    store.refreshAccessToken().subscribe((t) => second.push(t));

    const req = httpMock.expectOne(REFRESH_URL);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ refreshToken: 'refresh-seed' });
    expect(req.request.withCredentials).toBe(true);
    req.flush(makeResponse({ accessToken: 'access-2' }));

    expect(first).toEqual(['access-2']);
    expect(second).toEqual(['access-2']);
    expect(store.getAccessToken()).toBe('access-2');
  });

  it('clears the session and routes to /login when refresh is rejected', async () => {
    sessionStorage.setItem('sb_refresh_token', 'refresh-seed');

    const out: (string | null)[] = [];
    store.refreshAccessToken().subscribe((t) => out.push(t));

    httpMock.expectOne(REFRESH_URL).flush(
      { detail: 'Your session has expired.' },
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(out).toEqual([null]);
    expect(store.isAuthenticated()).toBe(false);
    expect(sessionStorage.getItem('sb_access_token')).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('restoreSession rehydrates from sessionStorage', () => {
    sessionStorage.setItem('sb_access_token', 'access-r');
    sessionStorage.setItem('sb_refresh_token', 'refresh-r');
    sessionStorage.setItem(
      'sb_student',
      JSON.stringify({ id: 'stu-9', fullName: 'Omar Z', status: 'Active', boundDevice: null }),
    );

    expect(store.restoreSession()).toBe(true);
    expect(store.isAuthenticated()).toBe(true);
    expect(store.fullName()).toBe('Omar Z');
  });

  it('signs out via Firebase, clears the session, and routes to /login', async () => {
    sessionStorage.setItem('sb_access_token', 'access-r');
    mockSignOut.mockResolvedValue(undefined);

    await store.signOut();

    expect(mockSignOut).toHaveBeenCalled();
    expect(sessionStorage.getItem('sb_access_token')).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('requests a Firebase password reset for the given email', async () => {
    mockResetEmail.mockResolvedValue(undefined);
    await store.requestPasswordReset('lina@example.com');
    expect(mockResetEmail).toHaveBeenCalledWith(expect.anything(), 'lina@example.com');
  });
});
