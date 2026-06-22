import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

// The runner (mounted on Start) pulls in @microsoft/signalr via the hub client — stub it at module load.
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

import { QuizIntroComponent } from './quiz-intro.component';
import { QuizHubClient } from './quiz-hub.client';
import { QuizService, StudentQuiz, QuizAttempt } from '@sb/student-portal/data-access';

function makeQuiz(over: Partial<StudentQuiz> = {}): StudentQuiz {
  return {
    id: 'uq1',
    gatedSessionId: 'sess-1',
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

function makeAttempt(): QuizAttempt {
  return {
    attemptId: 'att-2',
    number: 2,
    deadlineUtc: '2026-06-22T01:30:00Z',
    serverNowUtc: '2026-06-22T01:00:00Z',
    questions: [{ id: 'q1', order: 1, bodyLatex: 'x?', imageUrl: null, options: [{ id: 'o1', order: 0, text: 'a' }] }],
  };
}

type ServiceMock = { quiz: jest.Mock; start: jest.Mock };

describe('QuizIntroComponent (FR-STU-QZ-001/002)', () => {
  let fixture: ComponentFixture<QuizIntroComponent>;
  let service: ServiceMock;
  let router: Router;

  function makeService(over: Partial<ServiceMock> = {}): ServiceMock {
    return {
      quiz: jest.fn().mockReturnValue(of(makeQuiz())),
      start: jest.fn().mockReturnValue(of(makeAttempt())),
      ...over,
    };
  }

  function setup(svc: ServiceMock = makeService(), id = 'sess-1') {
    TestBed.resetTestingModule();
    service = svc;
    TestBed.configureTestingModule({
      imports: [QuizIntroComponent],
      providers: [
        provideRouter([]),
        { provide: QuizService, useValue: service },
        { provide: QuizHubClient, useValue: { open: jest.fn(), close: jest.fn() } },
      ],
    });
    fixture = TestBed.createComponent(QuizIntroComponent);
    router = TestBed.inject(Router);
    fixture.componentRef.setInput('id', id);
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
  const primaryBtn = () => root().querySelector<HTMLButtonElement>('.qi__body sb-button button');

  it('renders the rules — time limit, attempts left, best score, the one-sitting warning', async () => {
    setup();
    await fixture.whenStable();

    const tiles = Array.from(root().querySelectorAll('.qi__tile-val')).map((t) => t.textContent?.trim());
    expect(tiles).toEqual(['30:00', '2', '52%']); // time limit / attempts left / best score
    expect(root().textContent).toContain('Pass mark is');
    expect(root().textContent).toContain('60%');
    expect(root().textContent).toContain('One sitting only');
  });

  it('Start is enabled (attemptsRemaining>0 && no active attempt) and calls start()', async () => {
    setup();
    await fixture.whenStable();

    const btn = primaryBtn();
    expect(btn?.textContent?.trim()).toBe('Start attempt');
    expect(btn?.disabled).toBe(false);

    btn!.click();
    expect(service.start).toHaveBeenCalledWith('uq1');
  });

  it('shows Resume (not Start) when an attempt is active', async () => {
    setup(makeService({ quiz: jest.fn().mockReturnValue(of(makeQuiz({ activeAttemptId: 'att-9' }))) }));
    await fixture.whenStable();

    expect(primaryBtn()?.textContent?.trim()).toBe('Resume attempt');
    // Resume must NEVER silently re-start.
    const btn = primaryBtn();
    btn!.click();
    expect(service.start).not.toHaveBeenCalled();
  });

  it('shows a disabled "No attempts left" when exhausted and not passed', async () => {
    setup(
      makeService({
        quiz: jest.fn().mockReturnValue(of(makeQuiz({ attemptsRemaining: 0, passed: false }))),
      }),
    );
    await fixture.whenStable();
    const btn = primaryBtn();
    expect(btn?.textContent?.trim()).toBe('No attempts left');
    expect(btn?.disabled).toBe(true);
  });

  it('each TERMINAL attempt row deep-links the §B review; an InProgress row does not', async () => {
    setup(
      makeService({
        quiz: jest.fn().mockReturnValue(
          of(
            makeQuiz({
              activeAttemptId: 'att-2',
              attempts: [
                {
                  id: 'att-1', number: 1, scorePercent: 52, status: 'TimedOut', flag: 'Timeout',
                  startedAtUtc: '2026-06-22T00:00:00Z', submittedAtUtc: '2026-06-22T00:30:00Z',
                },
                {
                  id: 'att-2', number: 2, scorePercent: null, status: 'InProgress', flag: 'Clean',
                  startedAtUtc: '2026-06-22T01:00:00Z', submittedAtUtc: null,
                },
              ],
            }),
          ),
        ),
      }),
    );
    await fixture.whenStable();

    const rows = Array.from(root().querySelectorAll('.qi__row'));
    expect(rows).toHaveLength(2);
    // Terminal row → a Review link to the §B route.
    const reviewLink = rows[0].querySelector<HTMLAnchorElement>('.qi__row-review');
    expect(reviewLink?.getAttribute('href')).toContain('/sessions/sess-1/quiz/attempts/att-1/review');
    // InProgress row → no Review link.
    expect(rows[1].querySelector('.qi__row-review')).toBeNull();
    expect(rows[1].textContent).toContain('In progress');
  });

  it('routes back to /sessions/{id} on a 404 (the session has no quiz)', async () => {
    TestBed.resetTestingModule();
    service = makeService({
      quiz: jest.fn().mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 }))),
    });
    TestBed.configureTestingModule({
      imports: [QuizIntroComponent],
      providers: [
        provideRouter([]),
        { provide: QuizService, useValue: service },
        { provide: QuizHubClient, useValue: { open: jest.fn(), close: jest.fn() } },
      ],
    });
    fixture = TestBed.createComponent(QuizIntroComponent);
    router = TestBed.inject(Router);
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.componentRef.setInput('id', 'sess-1');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(nav).toHaveBeenCalledWith(['/sessions', 'sess-1']);
  });
});
