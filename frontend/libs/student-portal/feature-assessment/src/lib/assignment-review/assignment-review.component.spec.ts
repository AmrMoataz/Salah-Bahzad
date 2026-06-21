import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

// The data-access barrel imports @angular/fire (ESM) — replace it with a token-only double.
jest.mock('@sb/student-portal/data-access', () => ({
  AssignmentService: class AssignmentService {},
}));

import { AssignmentReviewComponent } from './assignment-review.component';
import {
  AssignmentService,
  StudentAssignment,
  StudentAssignmentReview,
} from '@sb/student-portal/data-access';

function loadedAssignment(): StudentAssignment {
  return { id: 'ua1', sessionId: 'sess-1', status: 'Completed', timeSpentSeconds: 1104, questions: [] };
}

function makeReview(over: Partial<StudentAssignmentReview> = {}): StudentAssignmentReview {
  return {
    id: 'ua1',
    sessionId: 'sess-1',
    sessionTitle: 'Algebra',
    status: 'Completed',
    correctCount: 1,
    questionCount: 3,
    scoreMarks: 2,
    maxMarks: 8,
    percent: 25,
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
          { id: 'q1a', order: 0, text: 'x = 2', isCorrect: true },
          { id: 'q1b', order: 1, text: 'x = 3', isCorrect: false },
        ],
        selectedOptionId: 'q1a', // answered correctly
        isCorrect: true,
      },
      {
        id: 'q2',
        order: 2,
        bodyLatex: 'Solve $y = 5$',
        imageUrl: null,
        mark: 3,
        hintUrl: null,
        options: [
          { id: 'q2a', order: 0, text: 'y = 5', isCorrect: true },
          { id: 'q2b', order: 1, text: 'y = 6', isCorrect: false },
        ],
        selectedOptionId: 'q2b', // wrong pick
        isCorrect: false,
      },
      {
        id: 'q3',
        order: 3,
        bodyLatex: 'Solve $z = 9$',
        imageUrl: null,
        mark: 3,
        hintUrl: null,
        options: [
          { id: 'q3a', order: 0, text: 'z = 9', isCorrect: true },
          { id: 'q3b', order: 1, text: 'z = 8', isCorrect: false },
        ],
        selectedOptionId: null, // unanswered
        isCorrect: false,
      },
    ],
    ...over,
  };
}

type ServiceMock = {
  assignment: jest.Mock;
  answer: jest.Mock;
  event: jest.Mock;
  review: jest.Mock;
};

describe('AssignmentReviewComponent (FR-STU-ASG-007)', () => {
  let fixture: ComponentFixture<AssignmentReviewComponent>;
  let service: ServiceMock;
  let router: Router;

  function makeService(reviewReturn: jest.Mock): ServiceMock {
    return {
      assignment: jest.fn().mockReturnValue(of(loadedAssignment())),
      answer: jest.fn(),
      event: jest.fn(),
      review: reviewReturn,
    };
  }

  function configure(svc: ServiceMock, id = 'sess-1'): void {
    TestBed.resetTestingModule();
    service = svc;
    TestBed.configureTestingModule({
      imports: [AssignmentReviewComponent],
      providers: [provideRouter([]), { provide: AssignmentService, useValue: service }],
    });
    fixture = TestBed.createComponent(AssignmentReviewComponent);
    router = TestBed.inject(Router);
    fixture.componentRef.setInput('id', id);
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

  it('renders the answer key — correct option green-checked, wrong pick red, per-question pills + score/time', async () => {
    configure(makeService(jest.fn().mockReturnValue(of(makeReview()))));
    await fixture.whenStable();

    // Header score + time.
    expect(root().querySelector('.rev__title')?.textContent).toContain('Algebra · Assignment review');
    expect(root().textContent).toContain('25%');
    expect(root().textContent).toContain('2/8'); // marks
    expect(root().textContent).toContain('1/3'); // correct
    expect(root().textContent).toContain('18:24'); // time M:SS from 1104s

    // One correct marker per question (3), one wrong-pick marker (only q2).
    expect(root().querySelectorAll('.rev-opt__mark--ok')).toHaveLength(3);
    expect(root().querySelectorAll('.rev-opt__mark--bad')).toHaveLength(1);

    // The correct option of q1 carries data-state=correct; the wrong pick of q2 carries picked-wrong.
    const q2 = root().querySelectorAll('.rev-q')[1];
    expect(q2.querySelector('.rev-opt[data-state="picked-wrong"]')).toBeTruthy();
    expect(q2.querySelector('.rev-opt[data-state="correct"]')).toBeTruthy();

    // Per-question right/wrong pills.
    const pills = Array.from(root().querySelectorAll('.rev-q__pill')).map((p) => p.textContent?.trim());
    expect(pills).toEqual(['+2', '0', '0']);
  });

  it('an unanswered question shows the correct option only (no picked-wrong marker)', async () => {
    configure(makeService(jest.fn().mockReturnValue(of(makeReview()))));
    await fixture.whenStable();

    const q3 = root().querySelectorAll('.rev-q')[2];
    expect(q3.querySelector('.rev-opt__mark--ok')).toBeTruthy(); // the correct option still marked
    expect(q3.querySelector('.rev-opt__mark--bad')).toBeNull(); // nothing picked → no red
  });

  it('renders a friendly "finish first" panel on a 403 assignment_in_progress (no answer key)', async () => {
    const err = new HttpErrorResponse({
      status: 403,
      error: { reason: 'assignment_in_progress', detail: 'Finish the assignment to see your answers and score.' },
    });
    configure(makeService(jest.fn().mockReturnValue(throwError(() => err))));
    await fixture.whenStable();

    expect(root().querySelector('.rev__gate')).toBeTruthy();
    expect(root().textContent).toContain('Finish the assignment to see your answers and score.');
    expect(root().querySelector('.rev__qs')).toBeNull(); // the key is NOT rendered

    // Continue button navigates into the runner.
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);
    root().querySelector<HTMLButtonElement>('.rev__gate button')!.click();
    expect(nav).toHaveBeenCalledWith(['/sessions', 'sess-1', 'assignment']);
  });

  it('routes back to /sessions/{id} on a 404 (unknown / another student / another tenant)', async () => {
    // The 404 navigation happens during the first detectChanges → spy before it.
    TestBed.resetTestingModule();
    service = makeService(jest.fn().mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 }))));
    TestBed.configureTestingModule({
      imports: [AssignmentReviewComponent],
      providers: [provideRouter([]), { provide: AssignmentService, useValue: service }],
    });
    fixture = TestBed.createComponent(AssignmentReviewComponent);
    router = TestBed.inject(Router);
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.componentRef.setInput('id', 'sess-1');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(nav).toHaveBeenCalledWith(['/sessions', 'sess-1']);
  });

  it('derives the userAssignment id from the session, then loads the review by that id', async () => {
    const reviewFn = jest.fn().mockReturnValue(of(makeReview()));
    configure(makeService(reviewFn));
    await fixture.whenStable();

    expect(service.assignment).toHaveBeenCalledWith('sess-1');
    expect(reviewFn).toHaveBeenCalledWith('ua1'); // the review read is keyed by assignmentId, not sessionId
  });
});
