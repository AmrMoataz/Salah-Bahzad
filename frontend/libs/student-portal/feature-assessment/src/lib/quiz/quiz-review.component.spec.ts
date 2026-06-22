import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

// The data-access barrel imports @angular/fire (ESM) — replace it with a token-only double.
jest.mock('@sb/student-portal/data-access', () => ({
  QuizService: class QuizService {},
}));

import { QuizReviewComponent } from './quiz-review.component';
import { QuizService, StudentQuizAttemptReview } from '@sb/student-portal/data-access';

function makeReview(over: Partial<StudentQuizAttemptReview> = {}): StudentQuizAttemptReview {
  return {
    attemptId: 'att-1',
    quizId: 'uq1',
    gatedSessionId: 'sess-1',
    sessionTitle: 'Algebra II',
    number: 2,
    status: 'TimedOut',
    scorePercent: 40,
    minPassPercent: 60,
    startedAtUtc: '2026-06-22T00:00:00Z',
    submittedAtUtc: '2026-06-22T00:11:42Z',
    timeSpentSeconds: 702,
    questions: [
      {
        id: 'q1', order: 1, bodyLatex: 'Solve $3x-7=14$', imageUrl: null, mark: 2,
        options: [
          { id: 'q1a', order: 0, text: 'x = 7', isCorrect: true },
          { id: 'q1b', order: 1, text: 'x = 3', isCorrect: false },
        ],
        selectedOptionId: 'q1a', // correct
        isCorrect: true,
      },
      {
        id: 'q2', order: 2, bodyLatex: 'Factor $x^2+7x+12$', imageUrl: null, mark: 2,
        options: [
          { id: 'q2a', order: 0, text: '(x+3)(x+4)', isCorrect: true },
          { id: 'q2b', order: 1, text: '(x+2)(x+6)', isCorrect: false },
        ],
        selectedOptionId: 'q2b', // wrong pick
        isCorrect: false,
      },
      {
        id: 'q3', order: 3, bodyLatex: 'Vertex x of $y=x^2-6x+5$', imageUrl: null, mark: 2,
        options: [
          { id: 'q3a', order: 0, text: '3', isCorrect: true },
          { id: 'q3b', order: 1, text: '-3', isCorrect: false },
        ],
        selectedOptionId: null, // unanswered (timed out)
        isCorrect: false,
      },
    ],
    ...over,
  };
}

type ServiceMock = { review: jest.Mock };

describe('QuizReviewComponent (FR-STU-QZ-009)', () => {
  let fixture: ComponentFixture<QuizReviewComponent>;
  let service: ServiceMock;
  let router: Router;

  function configure(reviewReturn: jest.Mock, attemptId = 'att-1', id = 'sess-1'): void {
    TestBed.resetTestingModule();
    service = { review: reviewReturn };
    TestBed.configureTestingModule({
      imports: [QuizReviewComponent],
      providers: [provideRouter([]), { provide: QuizService, useValue: service }],
    });
    fixture = TestBed.createComponent(QuizReviewComponent);
    router = TestBed.inject(Router);
    fixture.componentRef.setInput('id', id);
    fixture.componentRef.setInput('attemptId', attemptId);
    fixture.detectChanges();
  }

  afterEach(() => {
    try {
      fixture?.destroy();
    } catch {
      /* already destroyed */
    }
  });

  const root = () => fixture.nativeElement as HTMLElement;

  it('loads by attemptId and renders the answer key — correct green, wrong pick red, per-question pills + score/time/flag', async () => {
    const reviewFn = jest.fn().mockReturnValue(of(makeReview()));
    configure(reviewFn);
    await fixture.whenStable();

    // Keyed directly by attemptId (the §B read).
    expect(reviewFn).toHaveBeenCalledWith('att-1');

    // Header: title + score + time + flag + pass/fail chip.
    expect(root().querySelector('.qrev__title')?.textContent).toContain('Algebra II · Quiz review');
    expect(root().textContent).toContain('Attempt 2');
    expect(root().textContent).toContain('40%');
    expect(root().textContent).toContain('11:42'); // M:SS from 702s
    expect(root().textContent).toContain('Below pass'); // 40 < 60
    expect(root().textContent).toContain('Timeout'); // flag from status

    // 3 correct markers (one per question), 1 wrong-pick marker (only q2).
    expect(root().querySelectorAll('.qrev-opt__mark--ok')).toHaveLength(3);
    expect(root().querySelectorAll('.qrev-opt__mark--bad')).toHaveLength(1);

    // q2: the correct option is marked AND the wrong pick is flagged.
    const q2 = root().querySelectorAll('.qrev-q')[1];
    expect(q2.querySelector('.qrev-opt[data-state="correct"]')).toBeTruthy();
    expect(q2.querySelector('.qrev-opt[data-state="picked-wrong"]')).toBeTruthy();

    // Per-question right/wrong pills (+mark / 0).
    const pills = Array.from(root().querySelectorAll('.qrev-q__pill')).map((p) => p.textContent?.trim());
    expect(pills).toEqual(['+2', '0', '0']);
  });

  it('an unanswered question shows the correct option only (no picked-wrong marker)', async () => {
    configure(jest.fn().mockReturnValue(of(makeReview())));
    await fixture.whenStable();

    const q3 = root().querySelectorAll('.qrev-q')[2];
    expect(q3.querySelector('.qrev-opt__mark--ok')).toBeTruthy();
    expect(q3.querySelector('.qrev-opt__mark--bad')).toBeNull();
  });

  it('shows a "passed" chip when scorePercent >= minPassPercent', async () => {
    configure(jest.fn().mockReturnValue(of(makeReview({ scorePercent: 80, status: 'Submitted' }))));
    await fixture.whenStable();
    expect(root().textContent).toContain('Passed');
    expect(root().textContent).toContain('Clean'); // Submitted → Clean flag
  });

  it('renders a friendly "finish first" panel on a 403 quiz_attempt_in_progress (no answer key)', async () => {
    const err = new HttpErrorResponse({
      status: 403,
      error: { reason: 'quiz_attempt_in_progress', detail: 'Finish the quiz to see your answers and score.' },
    });
    configure(jest.fn().mockReturnValue(throwError(() => err)));
    await fixture.whenStable();

    expect(root().querySelector('.qrev__gate')).toBeTruthy();
    expect(root().textContent).toContain('Finish the quiz to see your answers and score.');
    expect(root().querySelector('.qrev__qs')).toBeNull(); // the key is NOT rendered

    // Continue quiz → the runner route.
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);
    root().querySelector<HTMLButtonElement>('.qrev__gate button')!.click();
    expect(nav).toHaveBeenCalledWith(['/sessions', 'sess-1', 'quiz']);
  });

  it('routes back to /sessions/{id} on a 404 (unknown / another student / another tenant)', async () => {
    TestBed.resetTestingModule();
    service = { review: jest.fn().mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 }))) };
    TestBed.configureTestingModule({
      imports: [QuizReviewComponent],
      providers: [provideRouter([]), { provide: QuizService, useValue: service }],
    });
    fixture = TestBed.createComponent(QuizReviewComponent);
    router = TestBed.inject(Router);
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.componentRef.setInput('id', 'sess-1');
    fixture.componentRef.setInput('attemptId', 'foreign');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(nav).toHaveBeenCalledWith(['/sessions', 'sess-1']);
  });
});
