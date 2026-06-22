import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';

// The data-access barrel imports @angular/fire (ESM) — replace it with a token-only double.
jest.mock('@sb/student-portal/data-access', () => ({
  QuizService: class QuizService {},
}));

import { QuizResultsComponent } from './quiz-results.component';
import { QuizService } from '@sb/student-portal/data-access';

type ServiceMock = { quiz: jest.Mock };

describe('QuizResultsComponent (FR-STU-QZ-008)', () => {
  let fixture: ComponentFixture<QuizResultsComponent>;
  let service: ServiceMock;
  let router: Router;

  function setNavState(state: Record<string, unknown> | null): void {
    window.history.replaceState(state, '');
  }

  function setup(svc: ServiceMock = { quiz: jest.fn() }, id = 'sess-1') {
    TestBed.resetTestingModule();
    service = svc;
    TestBed.configureTestingModule({
      imports: [QuizResultsComponent],
      providers: [provideRouter([]), { provide: QuizService, useValue: service }],
    });
    fixture = TestBed.createComponent(QuizResultsComponent);
    router = TestBed.inject(Router);
    fixture.componentRef.setInput('id', id);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => {
    setNavState(null);
    try {
      fixture?.destroy();
    } catch {
      /* already destroyed */
    }
  });

  const root = () => fixture.nativeElement as HTMLElement;

  it('renders SCORE-ONLY — pass mascot + score ring + "This attempt"/"Best of" tiles + "Back to session"', async () => {
    setNavState({ attemptId: 'att-2', scorePercent: 78, bestPercent: 78, passed: true, status: 'Submitted', sessionTitle: 'Algebra' });
    setup();
    await fixture.whenStable();

    // Pass mascot.
    expect(root().querySelector<HTMLImageElement>('.qres__mascot')?.getAttribute('src')).toContain('salah-passed.png');
    // The score ring shows the attempt score.
    expect(root().querySelector('.qres__ring')?.textContent).toContain('78');
    // Two stat tiles.
    const tiles = Array.from(root().querySelectorAll('.qres__tile-lbl')).map((t) => t.textContent?.trim());
    expect(tiles).toEqual(['This attempt', 'Best of']);
    const vals = Array.from(root().querySelectorAll('.qres__tile-val')).map((v) => v.textContent?.trim());
    expect(vals).toEqual(['78%', '78%']);
    // "Back to session".
    expect(root().textContent).toContain('Back to session');
    // NO answer key on this screen (§D/§G).
    expect(root().querySelector('.qrev-q')).toBeNull();
    expect(root().querySelector('[data-state="correct"]')).toBeNull();
  });

  it('shows the FAIL mascot when not passed', async () => {
    setNavState({ attemptId: 'att-3', scorePercent: 40, bestPercent: 52, passed: false, status: 'Submitted', sessionTitle: null });
    setup();
    await fixture.whenStable();
    expect(root().querySelector<HTMLImageElement>('.qres__mascot')?.getAttribute('src')).toContain('salah-failed.png');
  });

  it('the "Review answers" link targets the §B review for the just-finished attempt', async () => {
    setNavState({ attemptId: 'att-2', scorePercent: 78, bestPercent: 78, passed: true, status: 'Submitted', sessionTitle: 'Algebra' });
    setup();
    await fixture.whenStable();
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);

    root().querySelector<HTMLButtonElement>('.qres__review')!.click();
    expect(nav).toHaveBeenCalledWith(['/sessions', 'sess-1', 'quiz', 'attempts', 'att-2', 'review']);
  });

  it('"Back to session" navigates to /sessions/{id}', async () => {
    setNavState({ attemptId: 'att-2', scorePercent: 78, bestPercent: 78, passed: true, status: 'Submitted', sessionTitle: 'Algebra' });
    setup();
    await fixture.whenStable();
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);

    root().querySelector<HTMLButtonElement>('.qres__primary button')!.click();
    expect(nav).toHaveBeenCalledWith(['/sessions', 'sess-1']);
  });

  it('re-derives the result from quiz(sessionId)\'s latest terminal attempt when there is no nav state (refresh)', async () => {
    setNavState(null);
    const svc: ServiceMock = {
      quiz: jest.fn().mockReturnValue(
        of({
          id: 'uq1',
          gatedSessionId: 'sess-1',
          settings: { timeLimitMinutes: 30, questionCount: 5, attemptCount: 3, minPassPercent: 60 },
          attemptsUsed: 2,
          attemptsRemaining: 1,
          bestPercent: 78,
          passed: true,
          activeAttemptId: null,
          attempts: [
            { id: 'att-1', number: 1, scorePercent: 52, status: 'Submitted', flag: 'Clean', startedAtUtc: '2026-06-22T00:00:00Z', submittedAtUtc: '2026-06-22T00:20:00Z' },
            { id: 'att-2', number: 2, scorePercent: 78, status: 'Submitted', flag: 'Clean', startedAtUtc: '2026-06-22T01:00:00Z', submittedAtUtc: '2026-06-22T01:20:00Z' },
          ],
        }),
      ),
    };
    setup(svc);
    await fixture.whenStable();

    expect(svc.quiz).toHaveBeenCalledWith('sess-1');
    // The latest terminal attempt (att-2, 78%) drives the screen.
    expect(root().querySelector('.qres__ring')?.textContent).toContain('78');
    expect(root().querySelector<HTMLImageElement>('.qres__mascot')?.getAttribute('src')).toContain('salah-passed.png');
  });
});
