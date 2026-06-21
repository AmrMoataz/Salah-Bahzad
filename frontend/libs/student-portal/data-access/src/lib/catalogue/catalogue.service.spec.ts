import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';

// The auth store (pulled in by the interceptor) imports @angular/fire/auth (ESM, unparsable by
// jest). Replace it with CJS doubles; the interceptor only needs the store's token + two getters.
jest.mock('@angular/fire/auth', () => ({
  Auth: class Auth {},
  GoogleAuthProvider: class GoogleAuthProvider {},
  signInWithEmailAndPassword: jest.fn(),
  signInWithPopup: jest.fn(),
  sendPasswordResetEmail: jest.fn(),
  signOut: jest.fn(),
}));

import { CatalogueService } from './catalogue.service';
import { CatalogueSession, Enrollment } from './catalogue.models';
import { studentAuthInterceptor } from '../auth/student-auth.interceptor';
import { StudentAuthStore } from '../auth/student-auth.store';

const CATALOGUE_URL = '/api/me/catalogue';
const REDEEM_URL = '/api/enrollments/redeem';

function makeSession(over: Partial<CatalogueSession> = {}): CatalogueSession {
  return {
    id: 's1',
    title: 'Algebra Basics',
    description: 'Start here.',
    price: 150,
    thumbnailUrl: null,
    gradeId: 'g1',
    gradeName: 'Grade 1',
    subjectId: 'sub1',
    subjectName: 'Maths',
    specializationId: 'spec1',
    specializationName: 'Pure Maths',
    videoCount: 4,
    materialCount: 2,
    validityDays: 30,
    hasQuiz: true,
    hasAssignment: true,
    prerequisiteSessionId: null,
    prerequisiteTitle: null,
    prerequisiteSatisfied: true,
    enrollmentState: 'NotEnrolled',
    enrolledExpiresAtUtc: null,
    ...over,
  };
}

describe('CatalogueService (FR-STU-CAT-001/003)', () => {
  let service: CatalogueService;
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
        CatalogueService,
      ],
    });

    service = TestBed.inject(CatalogueService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('catalogue() GETs /api/me/catalogue WITH a bearer (authenticated, not exempted)', () => {
    let result: CatalogueSession[] | undefined;
    service.catalogue().subscribe((r) => (result = r));

    const req = httpMock.expectOne((r) => r.url === CATALOGUE_URL);
    expect(req.request.method).toBe('GET');
    // The interceptor attached the Student JWT — the read is authenticated, not anonymous.
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');
    expect(req.request.params.keys()).toHaveLength(0);

    const payload = [makeSession()];
    req.flush(payload);
    expect(result).toEqual(payload);
  });

  it('catalogue(filters) forwards the optional query params (contract §A.1)', () => {
    service
      .catalogue({ gradeId: 'g1', subjectId: 'sub2', specializationId: 'spec3', search: 'algebra' })
      .subscribe();

    const req = httpMock.expectOne((r) => r.url === CATALOGUE_URL);
    expect(req.request.params.get('gradeId')).toBe('g1');
    expect(req.request.params.get('subjectId')).toBe('sub2');
    expect(req.request.params.get('specializationId')).toBe('spec3');
    expect(req.request.params.get('search')).toBe('algebra');
    req.flush([]);
  });

  it('catalogue() omits params that are not provided', () => {
    service.catalogue({ search: 'physics' }).subscribe();
    const req = httpMock.expectOne((r) => r.url === CATALOGUE_URL);
    expect(req.request.params.keys()).toEqual(['search']);
    req.flush([]);
  });

  it('redeem(serial) POSTs the exact { serial } body WITH a bearer and maps the EnrollmentDto', () => {
    let result: Enrollment | undefined;
    service.redeem('SB-ABCDE-FGHIJ').subscribe((r) => (result = r));

    const req = httpMock.expectOne(REDEEM_URL);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ serial: 'SB-ABCDE-FGHIJ' });
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');

    const dto: Enrollment = {
      id: 'e1',
      studentId: 'stu1',
      studentName: 'Lina',
      sessionId: 's1',
      sessionTitle: 'Algebra Basics',
      status: 'Active',
      method: 'Code',
      amount: 150,
      codeId: 'c1',
      codeSerial: 'SB-ABCDE-FGHIJ',
      enrolledAtUtc: '2026-06-21T00:00:00Z',
      expiresAtUtc: '2026-07-21T00:00:00Z',
    };
    req.flush(dto, { status: 201, statusText: 'Created' });
    expect(result).toEqual(dto);
  });
});
