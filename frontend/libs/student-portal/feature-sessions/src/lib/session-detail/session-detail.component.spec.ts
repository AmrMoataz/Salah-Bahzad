import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';

// The data-access barrel imports @angular/fire (ESM) — replace it with a token-only double.
jest.mock('@sb/student-portal/data-access', () => ({
  MySessionsService: class MySessionsService {},
}));

import { SessionDetailComponent } from './session-detail.component';
import {
  MySessionsService,
  MySessionDetail,
  MySessionVideo,
} from '@sb/student-portal/data-access';

function makeVideo(over: Partial<MySessionVideo> = {}): MySessionVideo {
  return {
    id: 'v1',
    title: 'Intro',
    order: 0,
    lengthSeconds: 125,
    processingStatus: 'Ready',
    accessAllowed: 3,
    accessRemaining: 3,
    lockState: 'Playable',
    ...over,
  };
}

function makeDetail(over: Partial<MySessionDetail> = {}): MySessionDetail {
  return {
    id: 'sess-1',
    title: 'Algebra',
    description: 'A friendly intro.',
    gradeId: 'g',
    gradeName: 'Grade 1',
    subjectId: 'sub',
    subjectName: 'Maths',
    specializationId: 'spec',
    specializationName: 'Pure Maths',
    thumbnailUrl: null,
    enrollmentId: 'e1',
    enrolledAtUtc: '2026-06-01T00:00:00Z',
    expiresAtUtc: null,
    isExpired: false,
    videoCount: 2,
    videosWatched: 1,
    progressPercent: 50,
    gateState: 'Open',
    hasGatingQuiz: false,
    quizPassed: false,
    minPassPercent: 0,
    videos: [makeVideo({ id: 'v1', title: 'First', order: 0 }), makeVideo({ id: 'v2', title: 'Second', order: 1 })],
    materials: [],
    assignment: null,
    quiz: null,
    ...over,
  };
}

type ServiceMock = {
  session: jest.Mock;
  startPlayback: jest.Mock;
  materialUrl: jest.Mock;
};

describe('SessionDetailComponent (FR-STU-SES-001..004)', () => {
  let fixture: ComponentFixture<SessionDetailComponent>;
  let component: SessionDetailComponent;
  let service: ServiceMock;
  let router: Router;

  function setup(detail: MySessionDetail, id = 'sess-1') {
    // Allow a test to re-`setup()` with a different fixture (the quiz-present/absent pair).
    TestBed.resetTestingModule();
    service = {
      session: jest.fn().mockReturnValue(of(detail)),
      startPlayback: jest.fn(),
      materialUrl: jest.fn(),
    };
    TestBed.configureTestingModule({
      imports: [SessionDetailComponent],
      providers: [provideRouter([]), { provide: MySessionsService, useValue: service }],
    });
    fixture = TestBed.createComponent(SessionDetailComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    fixture.componentRef.setInput('id', id);
    fixture.detectChanges();
    return fixture;
  }

  const root = () => fixture.nativeElement as HTMLElement;
  const rows = (): HTMLElement[] => Array.from(root().querySelectorAll<HTMLElement>('sb-video-row'));
  const badges = (): HTMLElement[] => Array.from(root().querySelectorAll<HTMLElement>('.row__badge'));

  it('renders the video playlist in order', async () => {
    setup(makeDetail());
    await fixture.whenStable();

    const titles = rows().map((r) => r.querySelector('.row__title')?.textContent?.trim());
    expect(titles).toEqual(['1. First', '2. Second']);
  });

  it('renders the per-video access badge by lockState (§E.3)', async () => {
    setup(
      makeDetail({
        videos: [
          makeVideo({ id: 'a', lockState: 'Playable', accessRemaining: 2, accessAllowed: 3 }),
          makeVideo({ id: 'b', lockState: 'Exhausted', accessRemaining: 0, accessAllowed: 3 }),
          makeVideo({ id: 'c', lockState: 'QuizLocked' }),
        ],
      }),
    );
    await fixture.whenStable();

    const labels = badges().map((b) => b.textContent?.trim());
    expect(labels).toEqual(['2 of 3 views', '0 views left', 'Locked']);
    const variants = badges().map((b) => b.getAttribute('data-variant'));
    expect(variants).toEqual(['success', 'danger', 'neutral']);
  });

  it('shows the gate banner for QuizRequired with the pass mark, and locks Play', async () => {
    setup(
      makeDetail({
        gateState: 'QuizRequired',
        hasGatingQuiz: true,
        quizPassed: false,
        minPassPercent: 60,
        videos: [makeVideo({ id: 'v1', lockState: 'QuizLocked' })],
      }),
    );
    await fixture.whenStable();

    const banner = root().querySelector('.gate');
    expect(banner).toBeTruthy();
    expect(banner?.textContent).toContain('60%');
    // A QuizLocked video's Play button is the disabled 🔒.
    const playBtn = root().querySelector<HTMLButtonElement>('sb-video-row button');
    expect(playBtn?.disabled).toBe(true);
    expect(playBtn?.textContent?.trim()).toBe('🔒');
  });

  it('shows the gate banner for Expired', async () => {
    setup(makeDetail({ gateState: 'Expired', isExpired: true, expiresAtUtc: new Date(Date.now() - 86_400_000).toISOString() }));
    await fixture.whenStable();
    expect(root().querySelector('.gate')).toBeTruthy();
    expect(root().querySelector('.gate')?.getAttribute('data-state')).toBe('Expired');
  });

  it('has no gate banner when gateState is Open', async () => {
    setup(makeDetail({ gateState: 'Open' }));
    await fixture.whenStable();
    expect(root().querySelector('.gate')).toBeNull();
  });

  it('keeps the Assignment card reachable (button enabled) when the session is expired (FR-STU-SES-001)', async () => {
    setup(
      makeDetail({
        isExpired: true,
        gateState: 'Expired',
        expiresAtUtc: new Date(Date.now() - 86_400_000).toISOString(),
        assignment: {
          userAssignmentId: 'ua1',
          status: 'InProgress',
          scoreMarks: null,
          maxMarks: 20,
          correctCount: null,
          questionCount: 10,
          completedAtUtc: null,
        },
      }),
    );
    await fixture.whenStable();

    const asgCard = root().querySelector('.sd__entry-icon--asg')?.closest('.sd__card');
    expect(asgCard).toBeTruthy();
    const asgBtn = asgCard?.querySelector<HTMLButtonElement>('button');
    expect(asgBtn).toBeTruthy();
    expect(asgBtn?.disabled).toBe(false); // expiry must NOT disable the assignment
  });

  it('shows the Quiz card only when a gating quiz is present', async () => {
    setup(makeDetail({ quiz: null }));
    await fixture.whenStable();
    expect(root().querySelector('.sd__entry-icon--quiz')).toBeNull();

    setup(
      makeDetail({
        quiz: {
          userQuizId: 'uq1',
          passed: false,
          bestPercent: null,
          minPassPercent: 60,
          attemptsUsed: 0,
          attemptCount: 3,
          timeLimitMinutes: 30,
          questionCount: 8,
        },
      }),
    );
    await fixture.whenStable();
    expect(root().querySelector('.sd__entry-icon--quiz')).toBeTruthy();
    expect(root().textContent).toContain('Start attempt');
  });

  it('navigates to the quiz INTRO carrying the session title (F8 — a route string, not an import)', async () => {
    setup(
      makeDetail({
        quiz: {
          userQuizId: 'uq1',
          passed: false,
          bestPercent: null,
          minPassPercent: 60,
          attemptsUsed: 0,
          attemptCount: 3,
          timeLimitMinutes: 30,
          questionCount: 8,
        },
      }),
    );
    await fixture.whenStable();
    const navSpy = jest.spyOn(router, 'navigate').mockResolvedValue(true);

    const quizCard = root().querySelector('.sd__entry-icon--quiz')?.closest('.sd__card');
    quizCard?.querySelector<HTMLButtonElement>('sb-button button')?.click();

    expect(navSpy).toHaveBeenCalledWith(['/sessions', 'sess-1', 'quiz'], {
      state: { sessionTitle: 'Algebra' },
    });
    // The "Opens in the next update." placeholder is gone.
    expect(root().textContent).not.toContain('Opens in the next update');
  });

  it('routes back to /sessions on a 404 (not enrolled / unknown / other tenant) (§B.2)', async () => {
    service = {
      session: jest.fn().mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 }))),
      startPlayback: jest.fn(),
      materialUrl: jest.fn(),
    };
    TestBed.configureTestingModule({
      imports: [SessionDetailComponent],
      providers: [provideRouter([]), { provide: MySessionsService, useValue: service }],
    });
    fixture = TestBed.createComponent(SessionDetailComponent);
    router = TestBed.inject(Router);
    const navSpy = jest.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.componentRef.setInput('id', 'missing');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(navSpy).toHaveBeenCalledWith(['/sessions']);
  });

  it('navigates to the assignment RUNNER (InProgress) carrying the session title + userAssignment id (F6)', async () => {
    setup(
      makeDetail({
        assignment: {
          userAssignmentId: 'ua1',
          status: 'InProgress',
          scoreMarks: null,
          maxMarks: 20,
          correctCount: null,
          questionCount: 10,
          completedAtUtc: null,
        },
      }),
    );
    await fixture.whenStable();
    const navSpy = jest.spyOn(router, 'navigate').mockResolvedValue(true);

    const asgCard = root().querySelector('.sd__entry-icon--asg')?.closest('.sd__card');
    asgCard?.querySelector<HTMLButtonElement>('sb-button button')?.click();

    expect(navSpy).toHaveBeenCalledWith(['/sessions', 'sess-1', 'assignment'], {
      state: { sessionTitle: 'Algebra', userAssignmentId: 'ua1' },
    });
  });

  it('navigates to the assignment REVIEW when the assignment is Completed (F6)', async () => {
    setup(
      makeDetail({
        assignment: {
          userAssignmentId: 'ua9',
          status: 'Completed',
          scoreMarks: 14,
          maxMarks: 20,
          correctCount: 7,
          questionCount: 10,
          completedAtUtc: '2026-06-22T00:00:00Z',
        },
      }),
    );
    await fixture.whenStable();
    const navSpy = jest.spyOn(router, 'navigate').mockResolvedValue(true);

    const asgCard = root().querySelector('.sd__entry-icon--asg')?.closest('.sd__card');
    asgCard?.querySelector<HTMLButtonElement>('sb-button button')?.click();

    expect(navSpy).toHaveBeenCalledWith(['/sessions', 'sess-1', 'assignment', 'review'], {
      state: { sessionTitle: 'Algebra', userAssignmentId: 'ua9' },
    });
  });
});
