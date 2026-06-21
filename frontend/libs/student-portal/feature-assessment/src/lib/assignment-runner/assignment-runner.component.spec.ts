import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

// The data-access barrel imports @angular/fire (ESM) — replace it with a token-only double.
jest.mock('@sb/student-portal/data-access', () => ({
  AssignmentService: class AssignmentService {},
}));

import { AssignmentRunnerComponent } from './assignment-runner.component';
import {
  AssignmentService,
  AssignmentProgress,
  StudentAssignment,
  StudentAssignmentQuestion,
} from '@sb/student-portal/data-access';

function makeQuestion(over: Partial<StudentAssignmentQuestion> = {}): StudentAssignmentQuestion {
  return {
    id: 'q1',
    order: 1,
    bodyLatex: 'Solve $x^2 = 4$',
    imageUrl: null,
    hintUrl: null,
    options: [
      { id: 'q1o1', order: 0, text: 'x = 2' },
      { id: 'q1o2', order: 1, text: 'x = 3' },
    ],
    selectedOptionId: null,
    ...over,
  };
}

function makeAssignment(over: Partial<StudentAssignment> = {}): StudentAssignment {
  return {
    id: 'ua1',
    sessionId: 'sess-1',
    status: 'InProgress',
    timeSpentSeconds: 0,
    questions: [
      makeQuestion({ id: 'q1', order: 1 }),
      makeQuestion({
        id: 'q2',
        order: 2,
        bodyLatex: 'Solve $y = 5$',
        options: [
          { id: 'q2o1', order: 0, text: 'y = 5' },
          { id: 'q2o2', order: 1, text: 'y = 6' },
        ],
      }),
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

describe('AssignmentRunnerComponent (FR-STU-ASG-001..006)', () => {
  let fixture: ComponentFixture<AssignmentRunnerComponent>;
  let service: ServiceMock;
  let router: Router;

  function makeService(over: Partial<ServiceMock> = {}): ServiceMock {
    return {
      assignment: jest.fn().mockReturnValue(of(makeAssignment())),
      answer: jest.fn().mockReturnValue(
        of<AssignmentProgress>({ answeredCount: 1, questionCount: 2, status: 'InProgress' }),
      ),
      event: jest.fn().mockReturnValue(of(undefined)),
      review: jest.fn(),
      ...over,
    };
  }

  function setup(svc: ServiceMock = makeService(), id = 'sess-1') {
    TestBed.resetTestingModule();
    service = svc;
    TestBed.configureTestingModule({
      imports: [AssignmentRunnerComponent],
      providers: [provideRouter([]), { provide: AssignmentService, useValue: service }],
    });
    fixture = TestBed.createComponent(AssignmentRunnerComponent);
    router = TestBed.inject(Router);
    fixture.componentRef.setInput('id', id);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => {
    try {
      fixture?.destroy();
    } catch {
      /* already destroyed by a test */
    }
  });

  const root = () => fixture.nativeElement as HTMLElement;
  const opts = (): HTMLButtonElement[] =>
    Array.from(root().querySelectorAll<HTMLButtonElement>('.run-opt'));
  const navButtons = (): HTMLButtonElement[] =>
    Array.from(root().querySelectorAll<HTMLButtonElement>('sb-button button'));
  const primaryBtn = () => root().querySelector<HTMLButtonElement>('.run__primary button');
  const qLabel = () => root().querySelector('.run__qlabel')?.textContent?.trim();
  const posLabel = () => root().querySelector('.run__count')?.textContent?.trim();

  it('renders ONE question at a time (the current question card only)', async () => {
    setup();
    await fixture.whenStable();

    expect(root().querySelectorAll('.run__qlabel')).toHaveLength(1);
    expect(qLabel()).toBe('Question 1');
    // Question 2's options are not in the DOM yet (one card at a time).
    expect(opts().map((o) => o.textContent)).toEqual(
      expect.arrayContaining([expect.stringContaining('x = 2')]),
    );
    expect(root().textContent).not.toContain('y = 6');
  });

  it('picking an option calls answer() with the right aqId (= question.id) + optionId, and marks it green', async () => {
    setup();
    await fixture.whenStable();

    opts()[1].click(); // pick option B of question 1
    await fixture.whenStable();
    fixture.detectChanges();

    expect(service.answer).toHaveBeenCalledWith('ua1', 'q1', 'q1o2');
    expect(opts()[1].classList.contains('run-opt--picked')).toBe(true);
    expect(opts()[1].getAttribute('aria-checked')).toBe('true');
  });

  it('updates the "X of Y answered" progress after an answer', async () => {
    setup();
    await fixture.whenStable();
    expect(posLabel()).toBe('0 of 2 answered');

    opts()[0].click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(posLabel()).toBe('1 of 2 answered'); // from the AssignmentProgressDto
  });

  it('disables "← Previous" on the first question', async () => {
    setup();
    await fixture.whenStable();

    const prev = navButtons()[0];
    expect(prev.textContent?.trim()).toContain('Previous');
    expect(prev.disabled).toBe(true);
  });

  it('the last question shows "Submit assignment" and submitting navigates to /sessions/{id} (no inline results)', async () => {
    setup();
    await fixture.whenStable();
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);

    // Go to the last question.
    expect(primaryBtn()?.textContent?.trim()).toBe('Next question');
    primaryBtn()!.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(qLabel()).toBe('Question 2');
    expect(primaryBtn()?.textContent?.trim()).toBe('Submit assignment');

    // Answer the last question (persists immediately → auto-grade server-side).
    opts()[0].click();
    await fixture.whenStable();
    expect(service.answer).toHaveBeenCalledWith('ua1', 'q2', 'q2o1');

    // Submit → back to the session detail (no inline results screen).
    primaryBtn()!.click();
    expect(nav).toHaveBeenCalledWith(['/sessions', 'sess-1']);
  });

  it('starts on the first UNANSWERED question (resume-friendly)', async () => {
    setup(
      makeService({
        assignment: jest.fn().mockReturnValue(
          of(
            makeAssignment({
              questions: [
                makeQuestion({ id: 'q1', order: 1, selectedOptionId: 'q1o1' }),
                makeQuestion({ id: 'q2', order: 2, selectedOptionId: null }),
              ],
            }),
          ),
        ),
      }),
    );
    await fixture.whenStable();
    expect(qLabel()).toBe('Question 2'); // q1 already answered → resume at q2
  });

  it('shows the hint toggle only when hintUrl is present, and reveals the hintUrl when toggled', async () => {
    setup(
      makeService({
        assignment: jest.fn().mockReturnValue(
          of(
            makeAssignment({
              questions: [makeQuestion({ id: 'q1', order: 1, hintUrl: 'https://cdn/hint.mp4' })],
            }),
          ),
        ),
      }),
    );
    await fixture.whenStable();

    const toggle = root().querySelector<HTMLButtonElement>('.run__hinttoggle');
    expect(toggle).toBeTruthy();
    expect(toggle?.textContent?.trim()).toContain('Show hint');
    expect(root().querySelector('.run__hint')).toBeNull();

    toggle!.click();
    fixture.detectChanges();
    const link = root().querySelector<HTMLAnchorElement>('.run__hint a');
    expect(link?.getAttribute('href')).toBe('https://cdn/hint.mp4');
    expect(root().querySelector('.run__hinttoggle')?.textContent?.trim()).toContain('Hide hint');
  });

  it('hides the hint control entirely when hintUrl is null', async () => {
    setup(
      makeService({
        assignment: jest.fn().mockReturnValue(
          of(makeAssignment({ questions: [makeQuestion({ id: 'q1', order: 1, hintUrl: null })] })),
        ),
      }),
    );
    await fixture.whenStable();
    expect(root().querySelector('.run__hinttoggle')).toBeNull();
  });

  it('fires Entered on open, Navigated on next (target order), and Left on destroy', async () => {
    setup();
    await fixture.whenStable();

    expect(service.event).toHaveBeenCalledWith(
      'ua1',
      expect.objectContaining({ type: 'Entered' }),
    );

    primaryBtn()!.click(); // Next → question 2
    await fixture.whenStable();
    expect(service.event).toHaveBeenCalledWith(
      'ua1',
      expect.objectContaining({ type: 'Navigated', questionOrder: 2 }),
    );

    fixture.destroy();
    expect(service.event).toHaveBeenCalledWith('ua1', expect.objectContaining({ type: 'Left' }));
  });

  it('the accumulated timer resumes from timeSpentSeconds and ticks up', async () => {
    setup(
      makeService({
        assignment: jest.fn().mockReturnValue(of(makeAssignment({ timeSpentSeconds: 125 }))),
      }),
    );
    await fixture.whenStable();
    expect(root().querySelector('.run__timer')?.textContent?.trim()).toContain('2:05');

    // Drive the timer seam (no real wall clock).
    (fixture.componentInstance as unknown as { tick: () => void }).tick();
    fixture.detectChanges();
    expect(root().querySelector('.run__timer')?.textContent?.trim()).toContain('2:06');
  });

  it('routes back to /sessions/{id} on a 404 (no enrollment for the session)', async () => {
    // The 404 navigation happens during the first detectChanges → install the spy BEFORE it.
    TestBed.resetTestingModule();
    service = makeService({
      assignment: jest.fn().mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 }))),
    });
    TestBed.configureTestingModule({
      imports: [AssignmentRunnerComponent],
      providers: [provideRouter([]), { provide: AssignmentService, useValue: service }],
    });
    fixture = TestBed.createComponent(AssignmentRunnerComponent);
    router = TestBed.inject(Router);
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.componentRef.setInput('id', 'sess-1');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(nav).toHaveBeenCalledWith(['/sessions', 'sess-1']);
  });
});
