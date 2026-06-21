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

import { AssignmentService } from './assignment.service';
import {
  AssignmentProgress,
  StudentAssignment,
  StudentAssignmentReview,
} from './assignment.models';
import { studentAuthInterceptor } from '../auth/student-auth.interceptor';
import { StudentAuthStore } from '../auth/student-auth.store';

const BASE = '/api/me/assignments';

function makeAssignment(over: Partial<StudentAssignment> = {}): StudentAssignment {
  return {
    id: 'ua1',
    sessionId: 'sess-1',
    status: 'InProgress',
    timeSpentSeconds: 42,
    questions: [
      {
        id: 'q1',
        order: 1,
        bodyLatex: 'Solve $x^2 = 4$',
        imageUrl: null,
        hintUrl: null,
        options: [
          { id: 'o1', order: 0, text: 'x = 2' },
          { id: 'o2', order: 1, text: 'x = 3' },
        ],
        selectedOptionId: null,
      },
    ],
    ...over,
  };
}

describe('AssignmentService (FR-STU-ASG-001..007)', () => {
  let service: AssignmentService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    (window as unknown as { __SB_API_URL__: string }).__SB_API_URL__ = '';

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([studentAuthInterceptor])),
        provideHttpClientTesting(),
        // A stub store so the interceptor attaches a bearer (these calls are NOT exempted).
        {
          provide: StudentAuthStore,
          useValue: { getAccessToken: () => 'access-tok', getRefreshToken: () => 'refresh-tok' },
        },
        AssignmentService,
      ],
    });

    service = TestBed.inject(AssignmentService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('assignment() GETs …/by-session/{id} WITH a bearer (authenticated, not exempted)', () => {
    let result: StudentAssignment | undefined;
    service.assignment('sess-1').subscribe((r) => (result = r));

    const req = httpMock.expectOne(`${BASE}/by-session/sess-1`);
    expect(req.request.method).toBe('GET');
    // The interceptor attached the Student JWT — the read is authenticated, not anonymous.
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');

    const payload = makeAssignment();
    req.flush(payload);
    expect(result).toEqual(payload);
    // The runner shape never carries correctness (5B-1 invariant).
    expect(JSON.stringify(result)).not.toContain('isCorrect');
  });

  it('answer() PUTs the exact { selectedOptionId } body to …/questions/{aqId}/answer WITH a bearer', () => {
    let progress: AssignmentProgress | undefined;
    service.answer('ua1', 'q7', 'opt-3').subscribe((p) => (progress = p));

    const req = httpMock.expectOne(`${BASE}/ua1/questions/q7/answer`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ selectedOptionId: 'opt-3' });
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');

    const dto: AssignmentProgress = { answeredCount: 5, questionCount: 9, status: 'InProgress' };
    req.flush(dto);
    expect(progress).toEqual(dto);
  });

  it('answer() maps the auto-grade Completed status on the last answer', () => {
    let progress: AssignmentProgress | undefined;
    service.answer('ua1', 'q9', 'opt-1').subscribe((p) => (progress = p));

    const req = httpMock.expectOne(`${BASE}/ua1/questions/q9/answer`);
    req.flush({ answeredCount: 9, questionCount: 9, status: 'Completed' });
    expect(progress?.status).toBe('Completed');
  });

  it('event() POSTs the behaviour body to …/events (204) WITH a bearer and never type:Answered', () => {
    let done = false;
    service
      .event('ua1', { type: 'Navigated', questionOrder: 3, occurredAtUtc: '2026-06-22T00:00:00Z', elapsedMs: 1200 })
      .subscribe(() => (done = true));

    const req = httpMock.expectOne(`${BASE}/ua1/events`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      type: 'Navigated',
      questionOrder: 3,
      occurredAtUtc: '2026-06-22T00:00:00Z',
      elapsedMs: 1200,
    });
    expect(req.request.body.type).not.toBe('Answered');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');

    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  it('review() GETs …/{id}/review WITH a bearer and maps the answer-key DTO (isCorrect exposed)', () => {
    let review: StudentAssignmentReview | undefined;
    service.review('ua1').subscribe((r) => (review = r));

    const req = httpMock.expectOne(`${BASE}/ua1/review`);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');

    const dto: StudentAssignmentReview = {
      id: 'ua1',
      sessionId: 'sess-1',
      sessionTitle: 'Algebra',
      status: 'Completed',
      correctCount: 7,
      questionCount: 9,
      scoreMarks: 14,
      maxMarks: 18,
      percent: 78,
      timeSpentSeconds: 1104,
      completedAtUtc: '2026-06-22T00:00:00Z',
      questions: [
        {
          id: 'q1',
          order: 1,
          bodyLatex: 'Solve $x^2 = 4$',
          imageUrl: null,
          mark: 2,
          hintUrl: null,
          options: [
            { id: 'o1', order: 0, text: 'x = 2', isCorrect: true },
            { id: 'o2', order: 1, text: 'x = 3', isCorrect: false },
          ],
          selectedOptionId: 'o1',
          isCorrect: true,
        },
      ],
    };
    req.flush(dto);
    expect(review).toEqual(dto);
    expect(review?.questions[0].options[0].isCorrect).toBe(true);
  });

  it('review() surfaces a 403 assignment_in_progress as an HttpErrorResponse (the deep-link edge)', () => {
    let status: number | undefined;
    let reason: string | undefined;
    service.review('ua1').subscribe({
      next: () => fail('should not emit'),
      error: (err) => {
        status = err.status;
        reason = err.error?.reason;
      },
    });

    const req = httpMock.expectOne(`${BASE}/ua1/review`);
    req.flush(
      { reason: 'assignment_in_progress', detail: 'Finish the assignment to see your answers and score.' },
      { status: 403, statusText: 'Forbidden' },
    );

    expect(status).toBe(403);
    expect(reason).toBe('assignment_in_progress');
  });

  it('review() surfaces a 404 (IDOR / tenant / unknown) as an HttpErrorResponse', () => {
    let status: number | undefined;
    service.review('foreign').subscribe({
      next: () => fail('should not emit'),
      error: (err) => (status = err.status),
    });

    const req = httpMock.expectOne(`${BASE}/foreign/review`);
    req.flush({}, { status: 404, statusText: 'Not Found' });
    expect(status).toBe(404);
  });
});
