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

import { QuizService } from './quiz.service';
import {
  QuizAttempt,
  QuizAttemptResult,
  StudentQuiz,
  StudentQuizAttemptReview,
} from './quiz.models';
import { studentAuthInterceptor } from '../auth/student-auth.interceptor';
import { StudentAuthStore } from '../auth/student-auth.store';

const BASE = '/api/me/quizzes';

function makeQuiz(over: Partial<StudentQuiz> = {}): StudentQuiz {
  return {
    id: 'uq1',
    gatedSessionId: 'sess-B',
    settings: { timeLimitMinutes: 30, questionCount: 5, attemptCount: 3, minPassPercent: 60 },
    attemptsUsed: 1,
    attemptsRemaining: 2,
    bestPercent: 52,
    passed: false,
    activeAttemptId: null,
    attempts: [
      {
        id: 'att-1',
        number: 1,
        scorePercent: 52,
        status: 'TimedOut',
        flag: 'Timeout',
        startedAtUtc: '2026-06-22T00:00:00Z',
        submittedAtUtc: '2026-06-22T00:30:00Z',
      },
    ],
    ...over,
  };
}

function makeAttempt(over: Partial<QuizAttempt> = {}): QuizAttempt {
  return {
    attemptId: 'att-2',
    number: 2,
    deadlineUtc: '2026-06-22T01:30:00Z',
    serverNowUtc: '2026-06-22T01:00:00Z',
    questions: [
      {
        id: 'aq1',
        order: 1,
        bodyLatex: 'Solve $3x-7=14$',
        imageUrl: null,
        options: [
          { id: 'aq1o1', order: 0, text: 'x = 7' },
          { id: 'aq1o2', order: 1, text: 'x = 3' },
        ],
      },
    ],
    ...over,
  };
}

describe('QuizService (FR-STU-QZ-001..010)', () => {
  let service: QuizService;
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
        QuizService,
      ],
    });

    service = TestBed.inject(QuizService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('quiz() GETs …/by-session/{id} WITH a bearer and maps the INTRO shape (no isCorrect)', () => {
    let result: StudentQuiz | undefined;
    service.quiz('sess-B').subscribe((r) => (result = r));

    const req = httpMock.expectOne(`${BASE}/by-session/sess-B`);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');

    const payload = makeQuiz();
    req.flush(payload);
    expect(result).toEqual(payload);
    // The string-union enums survive the round-trip.
    expect(result?.attempts[0].status).toBe('TimedOut');
    expect(result?.attempts[0].flag).toBe('Timeout');
    // The intro shape never carries correctness (the 5B-2 invariant).
    expect(JSON.stringify(result)).not.toContain('isCorrect');
  });

  it('quiz() surfaces a 404 (no quiz for the session) as an HttpErrorResponse', () => {
    let status: number | undefined;
    service.quiz('sess-none').subscribe({ next: () => fail('no emit'), error: (e) => (status = e.status) });
    httpMock.expectOne(`${BASE}/by-session/sess-none`).flush({}, { status: 404, statusText: 'Not Found' });
    expect(status).toBe(404);
  });

  it('start() POSTs …/{quizId}/attempts WITH a bearer and maps the LIVE attempt (no isCorrect)', () => {
    let attempt: QuizAttempt | undefined;
    service.start('uq1').subscribe((a) => (attempt = a));

    const req = httpMock.expectOne(`${BASE}/uq1/attempts`);
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');

    const payload = makeAttempt();
    req.flush(payload);
    expect(attempt).toEqual(payload);
    // The live attempt shape forbids correctness (the 5B-2 guard).
    expect(JSON.stringify(attempt)).not.toContain('isCorrect');
    expect(JSON.stringify(attempt)).not.toContain('hintUrl');
  });

  it('start() surfaces a 409 (exhausted / already active) as an HttpErrorResponse', () => {
    let status: number | undefined;
    service.start('uq1').subscribe({ next: () => fail('no emit'), error: (e) => (status = e.status) });
    httpMock.expectOne(`${BASE}/uq1/attempts`).flush(
      { detail: 'You already have an attempt in progress.' },
      { status: 409, statusText: 'Conflict' },
    );
    expect(status).toBe(409);
  });

  it('answer() PUTs the exact { selectedOptionId } body to …/attempts/{id}/questions/{aqId}/answer WITH a bearer', () => {
    let done = false;
    service.answer('att-2', 'aq7', 'opt-3').subscribe(() => (done = true));

    const req = httpMock.expectOne(`${BASE}/attempts/att-2/questions/aq7/answer`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ selectedOptionId: 'opt-3' });
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');

    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  it('submit() POSTs …/attempts/{id}/submit WITH a bearer and maps the score-only result', () => {
    let result: QuizAttemptResult | undefined;
    service.submit('att-2').subscribe((r) => (result = r));

    const req = httpMock.expectOne(`${BASE}/attempts/att-2/submit`);
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');

    const dto: QuizAttemptResult = {
      scorePercent: 78,
      status: 'Submitted',
      bestPercent: 78,
      passed: true,
      attemptsRemaining: 1,
    };
    req.flush(dto);
    expect(result).toEqual(dto);
    expect(result?.status).toBe('Submitted');
  });

  it('submit() surfaces a 409 (already terminal — the Hangfire timeout race) as an HttpErrorResponse', () => {
    let status: number | undefined;
    service.submit('att-2').subscribe({ next: () => fail('no emit'), error: (e) => (status = e.status) });
    httpMock.expectOne(`${BASE}/attempts/att-2/submit`).flush({}, { status: 409, statusText: 'Conflict' });
    expect(status).toBe(409);
  });

  it('focus() POSTs the FocusLost body to …/attempts/{id}/focus (204) WITH a bearer', () => {
    let done = false;
    service
      .focus('att-2', { type: 'FocusLost', occurredAtUtc: '2026-06-22T01:05:00Z' })
      .subscribe(() => (done = true));

    const req = httpMock.expectOne(`${BASE}/attempts/att-2/focus`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ type: 'FocusLost', occurredAtUtc: '2026-06-22T01:05:00Z' });
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');

    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  it('focus() carries the durationMs on a FocusReturned event', () => {
    service
      .focus('att-2', { type: 'FocusReturned', occurredAtUtc: '2026-06-22T01:05:09Z', durationMs: 9000 })
      .subscribe();
    const req = httpMock.expectOne(`${BASE}/attempts/att-2/focus`);
    expect(req.request.body).toEqual({
      type: 'FocusReturned',
      occurredAtUtc: '2026-06-22T01:05:09Z',
      durationMs: 9000,
    });
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('review() GETs …/attempts/{id}/review WITH a bearer and maps the answer-key DTO (isCorrect exposed)', () => {
    let review: StudentQuizAttemptReview | undefined;
    service.review('att-1').subscribe((r) => (review = r));

    const req = httpMock.expectOne(`${BASE}/attempts/att-1/review`);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-tok');

    const dto: StudentQuizAttemptReview = {
      attemptId: 'att-1',
      quizId: 'uq1',
      gatedSessionId: 'sess-B',
      sessionTitle: 'Algebra II',
      number: 1,
      status: 'TimedOut',
      scorePercent: 52,
      minPassPercent: 60,
      startedAtUtc: '2026-06-22T00:00:00Z',
      submittedAtUtc: '2026-06-22T00:30:00Z',
      timeSpentSeconds: 702,
      questions: [
        {
          id: 'aq1',
          order: 1,
          bodyLatex: 'Solve $3x-7=14$',
          imageUrl: null,
          mark: 2,
          options: [
            { id: 'aq1o1', order: 0, text: 'x = 7', isCorrect: true },
            { id: 'aq1o2', order: 1, text: 'x = 3', isCorrect: false },
          ],
          selectedOptionId: 'aq1o2',
          isCorrect: false,
        },
      ],
    };
    req.flush(dto);
    expect(review).toEqual(dto);
    expect(review?.questions[0].options[0].isCorrect).toBe(true);
    expect(review?.status).toBe('TimedOut');
  });

  it('review() surfaces a 403 quiz_attempt_in_progress as an HttpErrorResponse (the deep-link edge)', () => {
    let status: number | undefined;
    let reason: string | undefined;
    service.review('att-active').subscribe({
      next: () => fail('should not emit'),
      error: (err) => {
        status = err.status;
        reason = err.error?.reason;
      },
    });

    httpMock.expectOne(`${BASE}/attempts/att-active/review`).flush(
      { reason: 'quiz_attempt_in_progress', detail: 'Finish the quiz to see your answers and score.' },
      { status: 403, statusText: 'Forbidden' },
    );

    expect(status).toBe(403);
    expect(reason).toBe('quiz_attempt_in_progress');
  });

  it('review() surfaces a 404 (IDOR / tenant / unknown) as an HttpErrorResponse', () => {
    let status: number | undefined;
    service.review('foreign').subscribe({
      next: () => fail('should not emit'),
      error: (err) => (status = err.status),
    });
    httpMock.expectOne(`${BASE}/attempts/foreign/review`).flush({}, { status: 404, statusText: 'Not Found' });
    expect(status).toBe(404);
  });
});
