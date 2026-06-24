import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';

// The auth store (pulled in by the interceptor) imports @angular/fire/auth (ESM, unparsable by
// jest). Replace it with CJS doubles; the interceptor only needs the store's token getters.
jest.mock('@angular/fire/auth', () => ({
  Auth: class Auth {},
  GoogleAuthProvider: class GoogleAuthProvider {},
  signInWithEmailAndPassword: jest.fn(),
  signInWithPopup: jest.fn(),
  sendPasswordResetEmail: jest.fn(),
  signOut: jest.fn(),
}));

import { ProfileService } from './profile.service';
import { StudentProfile, UpdateMyStudentProfile } from './profile.models';
import { studentAuthInterceptor } from '../auth/student-auth.interceptor';
import { StudentAuthStore } from '../auth/student-auth.store';

const PROFILE_URL = '/api/me/profile';

function makeProfile(over: Partial<StudentProfile> = {}): StudentProfile {
  return {
    id: 'stu1',
    fullName: 'Lina Hassan',
    phoneNumber: '+20 100 000 0000',
    parentPhonePrimary: '+20 111 111 1111',
    parentPhoneSecondary: '+20 122 222 2222',
    schoolName: 'Cairo STEM',
    gradeId: 'g12',
    gradeName: 'Grade 12',
    cityId: 'c1',
    cityName: 'Cairo',
    regionId: 'r1',
    regionName: 'Nasr City',
    status: 'Active',
    boundDevice: { summary: 'Windows / Chrome', boundAtUtc: '2026-06-20T08:30:00Z' },
    ...over,
  };
}

describe('ProfileService (FR-STU-PRO-001/002/003)', () => {
  let service: ProfileService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    (window as unknown as { __SB_API_URL__: string }).__SB_API_URL__ = '';

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([studentAuthInterceptor])),
        provideHttpClientTesting(),
        // A stub store so the interceptor attaches a bearer (the calls are NOT exempted).
        {
          provide: StudentAuthStore,
          useValue: { getAccessToken: () => 'access-tok', getRefreshToken: () => 'refresh-tok' },
        },
        ProfileService,
      ],
    });

    service = TestBed.inject(ProfileService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getProfile() GETs /api/me/profile WITH a bearer and maps the StudentProfile incl. nested boundDevice', () => {
    let result: StudentProfile | undefined;
    service.getProfile().subscribe((r) => (result = r));

    const req = httpMock.expectOne(PROFILE_URL);
    expect(req.request.method).toBe('GET');
    // The interceptor attached the Student JWT — the read is authenticated, not anonymous (§C.0).
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');

    const payload = makeProfile();
    req.flush(payload);
    expect(result).toEqual(payload);
    expect(result?.boundDevice).toEqual({ summary: 'Windows / Chrome', boundAtUtc: '2026-06-20T08:30:00Z' });
    // String-union status maps through verbatim.
    expect(result?.status).toBe('Active');
  });

  it('getProfile() maps a null boundDevice (no active device bound) and null display names', () => {
    let result: StudentProfile | undefined;
    service.getProfile().subscribe((r) => (result = r));

    const req = httpMock.expectOne(PROFILE_URL);
    req.flush(makeProfile({ boundDevice: null, gradeName: null, cityName: null, regionName: null }));

    expect(result?.boundDevice).toBeNull();
    expect(result?.gradeName).toBeNull();
  });

  it('updateProfile() PUTs /api/me/profile with EXACTLY the seven writable fields (no gradeId/email/status/boundDevice) and a bearer', () => {
    const body: UpdateMyStudentProfile = {
      fullName: 'Lina H.',
      phoneNumber: '+20 100 000 0000',
      schoolName: 'Cairo STEM School',
      cityId: 'c2',
      regionId: 'r2',
      parentPhonePrimary: '+20 111 111 1111',
      parentPhoneSecondary: null,
    };

    let result: StudentProfile | undefined;
    service.updateProfile(body).subscribe((r) => (result = r));

    const req = httpMock.expectOne(PROFILE_URL);
    expect(req.request.method).toBe('PUT');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');

    // Exactly the seven writable keys — and none of the read-only fields (§A.2).
    expect(Object.keys(req.request.body).sort()).toEqual(
      [
        'cityId',
        'fullName',
        'parentPhonePrimary',
        'parentPhoneSecondary',
        'phoneNumber',
        'regionId',
        'schoolName',
      ].sort(),
    );
    expect(req.request.body).not.toHaveProperty('gradeId');
    expect(req.request.body).not.toHaveProperty('email');
    expect(req.request.body).not.toHaveProperty('status');
    expect(req.request.body).not.toHaveProperty('boundDevice');
    // Optional secondary parent phone is sent as null (not '') when blank.
    expect(req.request.body.parentPhoneSecondary).toBeNull();

    const updated = makeProfile({ fullName: 'Lina H.', cityId: 'c2', regionId: 'r2' });
    req.flush(updated);
    expect(result).toEqual(updated);
  });

  it('updateProfile() surfaces a 400 (validation / mismatched city/region) as an HttpErrorResponse', () => {
    let status: number | undefined;
    service
      .updateProfile({
        fullName: 'X',
        phoneNumber: '1',
        schoolName: 'Y',
        cityId: 'c1',
        regionId: 'r-foreign',
        parentPhonePrimary: '1',
        parentPhoneSecondary: null,
      })
      .subscribe({ error: (e) => (status = e.status) });

    const req = httpMock.expectOne(PROFILE_URL);
    req.flush(
      { detail: 'Region does not belong to the selected city.' },
      { status: 400, statusText: 'Bad Request' },
    );
    expect(status).toBe(400);
  });
});
