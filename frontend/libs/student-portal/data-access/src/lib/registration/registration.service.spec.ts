import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { Auth } from '@angular/fire/auth';
import { RegistrationService } from './registration.service';
import { RegisterFormData } from './registration.models';

// Replace @angular/fire/auth (ESM, unparsable by jest) with a CJS double. The service calls these
// module functions directly and injects `Auth` as a DI token — both are satisfied here.
jest.mock('@angular/fire/auth', () => ({
  Auth: class Auth {},
  GoogleAuthProvider: class GoogleAuthProvider {},
  createUserWithEmailAndPassword: jest.fn(),
  signInWithEmailAndPassword: jest.fn(),
  signInWithPopup: jest.fn(),
}));

import {
  createUserWithEmailAndPassword,
  signInWithEmailAndPassword,
  signInWithPopup,
} from '@angular/fire/auth';
const mockEmailCreate = createUserWithEmailAndPassword as jest.Mock;
const mockEmailSignIn = signInWithEmailAndPassword as jest.Mock;
const mockGooglePopup = signInWithPopup as jest.Mock;

const GRADES_URL = '/api/reference/grades?tenantSlug=salah-bahzad';
const CITIES_URL = '/api/reference/cities';
const REGISTER_URL = '/api/students/register';

/** Drains microtasks (firebase token await) so the register HTTP call is dispatched. */
const tick = () => new Promise<void>((resolve) => setTimeout(resolve, 0));

function makeImage(): File {
  return new File([new Uint8Array([1, 2, 3])], 'id.png', { type: 'image/png' });
}

function makeForm(over: Partial<RegisterFormData> = {}): RegisterFormData {
  return {
    fullName: 'Lina Hassan',
    phoneNumber: '+201000000000',
    parentPhonePrimary: '+201111111111',
    gradeId: 'grade-1',
    cityId: 'city-1',
    regionId: 'region-1',
    schoolName: 'Cairo STEM',
    idImage: makeImage(),
    ...over,
  };
}

describe('RegistrationService', () => {
  let service: RegistrationService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    const w = window as unknown as {
      __SB_API_URL__: string;
      __SB_TENANT__: string;
      __SB_TERMS_VERSION__: string;
    };
    w.__SB_API_URL__ = '';
    w.__SB_TENANT__ = 'salah-bahzad';
    w.__SB_TERMS_VERSION__ = 'v1';
    jest.clearAllMocks();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Auth, useValue: {} },
        RegistrationService,
      ],
    });

    service = TestBed.inject(RegistrationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('grades() reads the anonymous tenant-scoped endpoint with ?tenantSlug= and no bearer (FR-STU-REG-004)', () => {
    let result: { id: string; name: string }[] | undefined;
    service.grades().subscribe((g) => (result = g));

    const req = httpMock.expectOne(GRADES_URL);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([{ id: 'g1', name: 'Grade 1 Secondary' }]);

    expect(result).toEqual([{ id: 'g1', name: 'Grade 1 Secondary' }]);
  });

  it('grades() picks up a runtime tenant override from window.__SB_TENANT__', () => {
    (window as unknown as { __SB_TENANT__: string }).__SB_TENANT__ = 'another-tenant';
    service.grades().subscribe();
    httpMock.expectOne('/api/reference/grades?tenantSlug=another-tenant').flush([]);
  });

  it('cities() and regions(cityId) hit the global reference paths (the cascade)', () => {
    service.cities().subscribe();
    httpMock.expectOne(CITIES_URL).flush([]);

    service.regions('city-7').subscribe();
    const req = httpMock.expectOne('/api/reference/cities/city-7/regions');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('register() builds multipart FormData with the EXACT contract §A.1 field names', async () => {
    mockEmailCreate.mockResolvedValue({ user: { getIdToken: async () => 'fb-token' } });

    await service.createEmailAccount('lina@example.com', 'password1');
    service
      .register(makeForm({ parentPhoneSecondary: '+202222222222' }))
      .subscribe();
    await tick();

    const req = httpMock.expectOne(REGISTER_URL);
    expect(req.request.method).toBe('POST');
    // No Content-Type override — the browser stamps the multipart boundary.
    expect(req.request.headers.has('Content-Type')).toBe(false);
    // Anonymous: no bearer.
    expect(req.request.headers.has('Authorization')).toBe(false);

    const body = req.request.body as FormData;
    expect(body).toBeInstanceOf(FormData);
    expect(body.get('firebaseIdToken')).toBe('fb-token');
    expect(body.get('tenantSlug')).toBe('salah-bahzad');
    expect(body.get('fullName')).toBe('Lina Hassan');
    expect(body.get('phoneNumber')).toBe('+201000000000');
    expect(body.get('parentPhonePrimary')).toBe('+201111111111');
    expect(body.get('parentPhoneSecondary')).toBe('+202222222222');
    expect(body.get('gradeId')).toBe('grade-1');
    expect(body.get('cityId')).toBe('city-1');
    expect(body.get('regionId')).toBe('region-1');
    expect(body.get('schoolName')).toBe('Cairo STEM');
    expect(body.get('termsVersion')).toBe('v1');
    expect(body.get('idImage')).toBeInstanceOf(File);

    req.flush({ studentId: 'stu-1', status: 'Pending' });
  });

  it('register() omits parentPhoneSecondary when not provided', async () => {
    mockEmailCreate.mockResolvedValue({ user: { getIdToken: async () => 'fb-token' } });
    await service.createEmailAccount('lina@example.com', 'password1');

    service.register(makeForm()).subscribe();
    await tick();

    const req = httpMock.expectOne(REGISTER_URL);
    expect((req.request.body as FormData).has('parentPhoneSecondary')).toBe(false);
    req.flush({ studentId: 'stu-1', status: 'Pending' });
  });

  it('signUpWithGoogle() returns the popup prefill and holds the user for the register token', async () => {
    mockGooglePopup.mockResolvedValue({
      user: { displayName: 'Omar Z', email: 'omar@example.com', getIdToken: async () => 'google-token' },
    });

    const profile = await service.signUpWithGoogle();
    expect(profile).toEqual({ fullName: 'Omar Z', email: 'omar@example.com' });

    service.register(makeForm()).subscribe();
    await tick();
    const req = httpMock.expectOne(REGISTER_URL);
    expect((req.request.body as FormData).get('firebaseIdToken')).toBe('google-token');
    req.flush({ studentId: 'stu-2', status: 'Pending' });
  });

  it('signInExistingEmailAccount() holds the existing user so register() carries its token (resubmit path)', async () => {
    mockEmailSignIn.mockResolvedValue({ user: { getIdToken: async () => 'existing-token' } });

    await service.signInExistingEmailAccount('rejected@example.com', 'password1');
    expect(mockEmailSignIn).toHaveBeenCalledWith(expect.anything(), 'rejected@example.com', 'password1');

    service.register(makeForm()).subscribe();
    await tick();
    const req = httpMock.expectOne(REGISTER_URL);
    expect((req.request.body as FormData).get('firebaseIdToken')).toBe('existing-token');
    req.flush({ studentId: 'stu-3', status: 'Pending' });
  });

  it('register() errors out when no Firebase account was created first', async () => {
    let error: unknown;
    service.register(makeForm()).subscribe({ error: (e) => (error = e) });
    await tick();

    httpMock.expectNone(REGISTER_URL);
    expect(error).toBeInstanceOf(Error);
  });
});
