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

import { PlanService } from './plan.service';
import { MyPlanDto } from './plan.models';
import { studentAuthInterceptor } from '../auth/student-auth.interceptor';
import { StudentAuthStore } from '../auth/student-auth.store';

const PLAN_URL = '/api/me/plan';

describe('PlanService (FR-STU-SES-001 — weekly plan)', () => {
  let service: PlanService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    (window as unknown as { __SB_API_URL__: string }).__SB_API_URL__ = '';

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([studentAuthInterceptor])),
        provideHttpClientTesting(),
        // A stub store so the interceptor attaches a bearer (the call is NOT exempted).
        {
          provide: StudentAuthStore,
          useValue: { getAccessToken: () => 'access-tok', getRefreshToken: () => 'refresh-tok' },
        },
        PlanService,
      ],
    });

    service = TestBed.inject(PlanService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('plan() GETs /api/me/plan WITH a bearer and NO query params (contract §A)', () => {
    let result: MyPlanDto | undefined;
    service.plan().subscribe((r) => (result = r));

    const req = httpMock.expectOne((r) => r.url === PLAN_URL);
    expect(req.request.method).toBe('GET');
    // Authenticated — NOT in ANONYMOUS_PATHS.
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');
    // No query parameters — the plan is fully server-composed.
    expect(req.request.params.keys()).toHaveLength(0);

    const dto: MyPlanDto = {
      isoWeek: '2026-W25',
      weekStartUtc: '2026-06-15T00:00:00Z',
      weekEndUtc: '2026-06-21T23:59:59Z',
      generatedAtUtc: '2026-06-22T08:00:00Z',
      totalSteps: 2,
      completedSteps: 1,
      overdueSteps: 0,
      kpis: {
        activeSessions: 1,
        videosWatched: 3,
        videosTotal: 8,
        overallProgressPercent: 38,
        completedSessions: 0,
      },
      focus: {
        sessionId: 'sess-1',
        title: 'Algebra Basics',
        specializationName: 'Pure Maths',
        thumbnailUrl: null,
        progressPercent: 38,
        expiresAtUtc: null,
        isExpired: false,
        expiresInDays: null,
        dueState: 'None',
      },
      steps: [
        {
          key: 'videos:sess-1',
          kind: 'Videos',
          title: 'Watch your lessons',
          subtitle: '3 of 8 watched',
          sessionId: 'sess-1',
          sessionTitle: 'Algebra Basics',
          specializationName: 'Pure Maths',
          status: 'Pending',
          blocked: false,
          blockedReason: null,
          dueState: 'None',
          expiresAtUtc: null,
          progress: { done: 3, total: 8 },
          action: { type: 'Navigate', route: '/sessions/sess-1', label: 'Continue' },
        },
      ],
      recentlyEnrolled: [],
    };
    req.flush(dto);

    // The response IS the model — passed through unchanged (string-union enums).
    expect(result).toEqual(dto);
    expect(result?.steps[0].kind).toBe('Videos');
    expect(result?.focus?.dueState).toBe('None');
  });

  it('plan() honours the injected base URL (the window shim)', () => {
    (window as unknown as { __SB_API_URL__: string }).__SB_API_URL__ = 'https://api.example.test';
    service.plan().subscribe();

    const req = httpMock.expectOne('https://api.example.test/api/me/plan');
    expect(req.request.method).toBe('GET');
    req.flush({} as MyPlanDto);
  });
});
