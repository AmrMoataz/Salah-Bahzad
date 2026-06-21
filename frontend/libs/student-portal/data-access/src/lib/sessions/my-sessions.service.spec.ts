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

import { MySessionsService } from './my-sessions.service';
import {
  MySession,
  MySessionDetail,
  PlaybackHandoff,
  SignedUrl,
} from './my-sessions.models';
import { studentAuthInterceptor } from '../auth/student-auth.interceptor';
import { StudentAuthStore } from '../auth/student-auth.store';

const SESSIONS_URL = '/api/me/sessions';

describe('MySessionsService (FR-STU-SES-001..004, FR-STU-VID-001/003/004)', () => {
  let service: MySessionsService;
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
        MySessionsService,
      ],
    });

    service = TestBed.inject(MySessionsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('mySessions() GETs /api/me/sessions WITH a bearer and no params', () => {
    let result: MySession[] | undefined;
    service.mySessions().subscribe((r) => (result = r));

    const req = httpMock.expectOne((r) => r.url === SESSIONS_URL);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');
    expect(req.request.params.keys()).toHaveLength(0);

    const payload: MySession[] = [];
    req.flush(payload);
    expect(result).toEqual(payload);
  });

  it('mySessions("Expired") forwards ?state=Expired (contract §A.1)', () => {
    service.mySessions('Expired').subscribe();
    const req = httpMock.expectOne((r) => r.url === SESSIONS_URL);
    expect(req.request.params.get('state')).toBe('Expired');
    req.flush([]);
  });

  it('session(id) GETs /api/me/sessions/{id} WITH a bearer and maps the detail', () => {
    let result: MySessionDetail | undefined;
    service.session('sess-1').subscribe((r) => (result = r));

    const req = httpMock.expectOne(`${SESSIONS_URL}/sess-1`);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');

    const dto = { id: 'sess-1', gateState: 'Open', videos: [], materials: [] } as unknown as MySessionDetail;
    req.flush(dto);
    expect(result).toEqual(dto);
  });

  it('materialUrl(sessionId, materialId) GETs the nested signed-URL path', () => {
    let result: SignedUrl | undefined;
    service.materialUrl('sess-1', 'mat-9').subscribe((r) => (result = r));

    const req = httpMock.expectOne(`${SESSIONS_URL}/sess-1/materials/mat-9/url`);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');

    const dto: SignedUrl = { url: 'https://r2/x', expiresAtUtc: '2026-06-21T00:05:00Z' };
    req.flush(dto);
    expect(result).toEqual(dto);
  });

  it('startPlayback(videoId) POSTs to the 5C gate WITH a bearer and maps the handoff (§D)', () => {
    let result: PlaybackHandoff | undefined;
    service.startPlayback('vid-7').subscribe((r) => (result = r));

    const req = httpMock.expectOne('/api/me/videos/vid-7/playback');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');

    const dto: PlaybackHandoff = { handoffCode: '5f3c', expiresAtUtc: '2026-06-21T00:01:00Z' };
    req.flush(dto);
    expect(result).toEqual(dto);
  });
});
