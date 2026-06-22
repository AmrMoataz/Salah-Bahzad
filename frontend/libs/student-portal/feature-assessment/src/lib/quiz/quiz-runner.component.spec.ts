import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

// @microsoft/signalr is pulled in by the hub client (imported transitively) — stub it so no real socket
// is built at module load. The hub is also DI-mocked below, so the real builder is never invoked anyway.
jest.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl() { return this; }
    configureLogging() { return this; }
    build() { return { start: () => Promise.resolve(), stop: () => Promise.resolve() }; }
  },
  LogLevel: { None: 6 },
}));

// The data-access barrel imports @angular/fire (ESM) — replace it with token-only doubles.
jest.mock('@sb/student-portal/data-access', () => ({
  QuizService: class QuizService {},
  StudentAuthStore: class StudentAuthStore {},
}));

import { QuizRunnerComponent } from './quiz-runner.component';
import { QuizHubClient } from './quiz-hub.client';
import { QuizService, QuizAttempt } from '@sb/student-portal/data-access';

function makeAttempt(over: Partial<QuizAttempt> = {}): QuizAttempt {
  return {
    attemptId: 'att-2',
    number: 2,
    // a 90-second window (deadline − serverNow) → "1:30"
    deadlineUtc: '2026-06-22T01:01:30Z',
    serverNowUtc: '2026-06-22T01:00:00Z',
    questions: [
      {
        id: 'q1',
        order: 1,
        bodyLatex: 'Solve $3x-7=14$',
        imageUrl: null,
        options: [
          { id: 'q1o1', order: 0, text: 'x = 7' },
          { id: 'q1o2', order: 1, text: 'x = 3' },
        ],
      },
      {
        id: 'q2',
        order: 2,
        bodyLatex: 'Factor $x^2+7x+12$',
        imageUrl: null,
        options: [
          { id: 'q2o1', order: 0, text: '(x+3)(x+4)' },
          { id: 'q2o2', order: 1, text: '(x+2)(x+6)' },
        ],
      },
      {
        id: 'q3',
        order: 3,
        bodyLatex: 'Vertex x of $y=x^2-6x+5$',
        imageUrl: null,
        options: [
          { id: 'q3o1', order: 0, text: '3' },
          { id: 'q3o2', order: 1, text: '-3' },
        ],
      },
    ],
    ...over,
  };
}

type ServiceMock = {
  quiz: jest.Mock;
  start: jest.Mock;
  answer: jest.Mock;
  submit: jest.Mock;
  focus: jest.Mock;
  review: jest.Mock;
};
type HubMock = { open: jest.Mock; close: jest.Mock };

describe('QuizRunnerComponent (FR-STU-QZ-003..007)', () => {
  let fixture: ComponentFixture<QuizRunnerComponent>;
  let service: ServiceMock;
  let hub: HubMock;
  let router: Router;

  function makeService(over: Partial<ServiceMock> = {}): ServiceMock {
    return {
      quiz: jest.fn(),
      start: jest.fn(),
      answer: jest.fn().mockReturnValue(of(undefined)),
      submit: jest.fn().mockReturnValue(
        of({ scorePercent: 80, status: 'Submitted', bestPercent: 80, passed: true, attemptsRemaining: 1 }),
      ),
      focus: jest.fn().mockReturnValue(of(undefined)),
      review: jest.fn(),
      ...over,
    };
  }

  function setup(
    svc: ServiceMock = makeService(),
    attempt: QuizAttempt = makeAttempt(),
    sessionId = 'sess-1',
  ) {
    TestBed.resetTestingModule();
    service = svc;
    hub = { open: jest.fn(), close: jest.fn() };
    TestBed.configureTestingModule({
      imports: [QuizRunnerComponent],
      providers: [
        provideRouter([]),
        { provide: QuizService, useValue: service },
        { provide: QuizHubClient, useValue: hub },
      ],
    });
    fixture = TestBed.createComponent(QuizRunnerComponent);
    router = TestBed.inject(Router);
    fixture.componentRef.setInput('attempt', attempt);
    fixture.componentRef.setInput('sessionId', sessionId);
    fixture.componentRef.setInput('sessionTitle', 'Algebra II');
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => {
    try {
      fixture?.destroy();
    } catch {
      /* already destroyed */
    }
  });

  const root = () => fixture.nativeElement as HTMLElement;
  const opts = (): HTMLButtonElement[] =>
    Array.from(root().querySelectorAll<HTMLButtonElement>('.qr-opt'));
  const dots = (): HTMLButtonElement[] =>
    Array.from(root().querySelectorAll<HTMLButtonElement>('.qr-dot'));
  const timer = () => root().querySelector('.qr__timer')?.textContent?.trim();
  const primaryBtn = () => root().querySelector<HTMLButtonElement>('.qr__primary button');
  const callTick = () => (fixture.componentInstance as unknown as { tick: () => void }).tick();

  it('renders ONE question at a time with question dots + prev/next', async () => {
    setup();
    await fixture.whenStable();

    expect(root().querySelectorAll('.qr__qlabel')).toHaveLength(1);
    expect(dots()).toHaveLength(3); // a dot per question
    // Only question 1's options are in the DOM.
    expect(root().textContent).toContain('x = 7');
    expect(root().textContent).not.toContain('(x+2)(x+6)');
    // Prev is disabled on the first question.
    const prev = root().querySelector<HTMLButtonElement>('sb-button button');
    expect(prev?.textContent).toContain('Previous');
    expect(prev?.disabled).toBe(true);
  });

  it('opens the QuizHub on start (after the attempt is bound)', async () => {
    setup();
    await fixture.whenStable();
    expect(hub.open).toHaveBeenCalledTimes(1);
  });

  it('picking an option calls answer() with the right aqId (= question.id) + optionId, marked green', async () => {
    setup();
    await fixture.whenStable();

    opts()[1].click(); // option B of question 1
    await fixture.whenStable();
    fixture.detectChanges();

    expect(service.answer).toHaveBeenCalledWith('att-2', 'q1', 'q1o2');
    expect(opts()[1].classList.contains('qr-opt--picked')).toBe(true);
    expect(opts()[1].getAttribute('aria-checked')).toBe('true');
  });

  it('seeds the local countdown from deadlineUtc − serverNowUtc and renders M:SS', async () => {
    setup();
    await fixture.whenStable();
    expect(timer()).toContain('1:30'); // 90s window

    callTick();
    fixture.detectChanges();
    expect(timer()).toContain('1:29'); // ticks down
  });

  it('auto-submits on local zero and routes to results', async () => {
    setup(makeService(), makeAttempt({ deadlineUtc: '2026-06-22T01:00:01Z' })); // 1s window
    await fixture.whenStable();
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);
    expect(timer()).toContain('0:01');

    callTick(); // → 0 → submit()
    await fixture.whenStable();

    expect(service.submit).toHaveBeenCalledWith('att-2');
    expect(nav).toHaveBeenCalledWith(
      ['/sessions', 'sess-1', 'quiz', 'results'],
      expect.objectContaining({ state: expect.objectContaining({ attemptId: 'att-2', scorePercent: 80 }) }),
    );
  });

  it('a submit 409 (Hangfire already TimedOut) re-fetches by-session and routes to results — NO error', async () => {
    const svc = makeService({
      submit: jest.fn().mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409 }))),
      quiz: jest.fn().mockReturnValue(
        of({
          id: 'uq1',
          gatedSessionId: 'sess-1',
          settings: { timeLimitMinutes: 30, questionCount: 3, attemptCount: 3, minPassPercent: 60 },
          attemptsUsed: 2,
          attemptsRemaining: 1,
          bestPercent: 52,
          passed: false,
          activeAttemptId: null,
          attempts: [
            {
              id: 'att-2',
              number: 2,
              scorePercent: 40,
              status: 'TimedOut',
              flag: 'Timeout',
              startedAtUtc: '2026-06-22T01:00:00Z',
              submittedAtUtc: '2026-06-22T01:01:30Z',
            },
          ],
        }),
      ),
    });
    setup(svc);
    await fixture.whenStable();
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);

    fixture.componentInstance.submit();
    await fixture.whenStable();

    expect(svc.quiz).toHaveBeenCalledWith('sess-1');
    // Routed to results carrying the just-ended TimedOut attempt's score — never an error.
    expect(nav).toHaveBeenCalledWith(
      ['/sessions', 'sess-1', 'quiz', 'results'],
      expect.objectContaining({
        state: expect.objectContaining({ attemptId: 'att-2', scorePercent: 40, status: 'TimedOut' }),
      }),
    );
    expect(root().querySelector('.qr__err')).toBeNull();
  });

  it('manual Submit on the last question → submit() → results, and tears the hub down', async () => {
    setup();
    await fixture.whenStable();
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);

    // Advance to the last question.
    expect(primaryBtn()?.textContent?.trim()).toBe('Next');
    primaryBtn()!.click();
    fixture.detectChanges();
    primaryBtn()!.click();
    fixture.detectChanges();
    expect(primaryBtn()?.textContent?.trim()).toBe('Submit quiz');

    primaryBtn()!.click(); // Submit
    await fixture.whenStable();

    expect(service.submit).toHaveBeenCalledWith('att-2');
    expect(hub.close).toHaveBeenCalled();
    expect(nav).toHaveBeenCalledWith(['/sessions', 'sess-1', 'quiz', 'results'], expect.anything());
  });

  it('an in-app leave shows the "Leave the quiz?" modal; "Leave & forfeit" tears the hub down and proceeds', async () => {
    setup();
    await fixture.whenStable();

    let allowed: boolean | undefined;
    fixture.componentInstance.attemptLeave().subscribe((v) => (allowed = v));
    fixture.detectChanges();

    // The modal is open with the forfeit copy.
    expect(root().textContent).toContain('Leave the quiz?');
    expect(root().textContent).toContain('forfeits this attempt');

    // Click "Leave & forfeit".
    const leaveBtn = Array.from(root().querySelectorAll<HTMLButtonElement>('sb-button button')).find((b) =>
      b.textContent?.includes('Leave'),
    );
    leaveBtn!.click();

    expect(hub.close).toHaveBeenCalled(); // the teardown = the forfeit
    expect(allowed).toBe(true);
  });

  it('"Stay in quiz" dismisses the modal and keeps the hub open (does NOT forfeit)', async () => {
    setup();
    await fixture.whenStable();

    let allowed: boolean | undefined;
    fixture.componentInstance.attemptLeave().subscribe((v) => (allowed = v));
    fixture.detectChanges();

    const stayBtn = Array.from(root().querySelectorAll<HTMLButtonElement>('sb-button button')).find((b) =>
      b.textContent?.includes('Stay'),
    );
    stayBtn!.click();

    expect(allowed).toBe(false);
    expect(hub.close).not.toHaveBeenCalled();
  });

  it('a visibilitychange (tab switch) fires focus() but does NOT submit or forfeit', async () => {
    setup();
    await fixture.whenStable();

    Object.defineProperty(document, 'hidden', { configurable: true, get: () => true });
    fixture.componentInstance.onVisibilityChange();

    expect(service.focus).toHaveBeenCalledWith('att-2', expect.objectContaining({ type: 'FocusLost' }));
    expect(service.submit).not.toHaveBeenCalled(); // recorded, NOT forfeited
    expect(hub.close).not.toHaveBeenCalled();

    // Restore.
    Object.defineProperty(document, 'hidden', { configurable: true, get: () => false });
  });

  it('closes the hub on destroy (leaving without a clean submit = forfeit server-side)', async () => {
    setup();
    await fixture.whenStable();
    fixture.destroy();
    expect(hub.close).toHaveBeenCalled();
  });
});
