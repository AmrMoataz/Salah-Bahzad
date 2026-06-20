import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';
import { ToastService } from '@sb/shared/ui';
import { AssignmentReview, BehaviourEvent } from '../data-access/attendance.models';
import { ReviewService } from '../data-access/review.service';
import { AssignmentReviewComponent } from './assignment-review.component';

// Replace the real data-access module so jest doesn't pull in @angular/fire (ESM) at runtime.
jest.mock('@sb/shared/data-access', () => ({ AuthStore: class AuthStore {} }));

const REVIEW: AssignmentReview = {
  studentName: 'Youssef Ibrahim',
  sessionTitle: 'Kinematics — Motion in 1D',
  correctCount: 1,
  questionCount: 2,
  scoreMarks: 2,
  maxMarks: 5,
  percent: 40,
  timeSpentSeconds: 1104,
  status: 'Completed',
  questions: [
    {
      order: 1,
      bodyLatex: 'What is $v = d/t$?',
      imageUrl: null,
      mark: 2,
      hintUrl: null,
      options: [
        { id: 'o1a', order: 0, text: 'Speed', isCorrect: true },
        { id: 'o1b', order: 1, text: 'Mass', isCorrect: false },
      ],
      selectedOptionId: 'o1a',
      isCorrect: true,
    },
    {
      order: 2,
      bodyLatex: 'Unit of force?',
      imageUrl: null,
      mark: 3,
      hintUrl: null,
      options: [
        { id: 'o2a', order: 0, text: 'Newton', isCorrect: true },
        { id: 'o2b', order: 1, text: 'Joule', isCorrect: false },
      ],
      selectedOptionId: 'o2b',
      isCorrect: false,
    },
  ],
};

const EVENTS: BehaviourEvent[] = [
  { type: 'Entered', label: 'Entered assessment', questionOrder: null, occurredAtUtc: '2026-06-20T09:05:01Z' },
  { type: 'Answered', label: 'Answered Q1', questionOrder: 1, occurredAtUtc: '2026-06-20T09:05:48Z' },
  { type: 'Left', label: 'Focus lost (tab switch)', questionOrder: null, occurredAtUtc: '2026-06-20T09:06:12Z' },
];

function makeReviewMock(review: AssignmentReview | null = REVIEW, events: BehaviourEvent[] = EVENTS) {
  const reviewSig = signal<AssignmentReview | null>(null);
  const behaviourSig = signal<BehaviourEvent[]>([]);
  return {
    review: reviewSig,
    behaviour: behaviourSig,
    isLoading: signal(false),
    error: signal<string | null>(null),
    getReview: jest.fn().mockImplementation(async () => {
      reviewSig.set(review);
      return review;
    }),
    getBehaviour: jest.fn().mockImplementation(async () => {
      behaviourSig.set(events);
      return events;
    }),
  };
}

const toast = { success: jest.fn(), error: jest.fn(), info: jest.fn(), warning: jest.fn(), show: jest.fn() };

describe('AssignmentReviewComponent', () => {
  function setup(opts: { perms?: string[]; service?: ReturnType<typeof makeReviewMock> } = {}) {
    const perms = opts.perms ?? ['AttendanceRead'];
    const service = opts.service ?? makeReviewMock();
    TestBed.configureTestingModule({
      imports: [AssignmentReviewComponent],
      providers: [
        provideRouter([]),
        { provide: ReviewService, useValue: service },
        { provide: AuthStore, useValue: { hasPermission: (p: string) => perms.includes(p), role: () => 'Teacher' } },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ enrollmentId: 'en-1' }) } } },
      ],
    });
    const fixture = TestBed.createComponent(AssignmentReviewComponent);
    return { fixture, service };
  }

  const text = (fixture: { nativeElement: HTMLElement }) => fixture.nativeElement.textContent ?? '';

  beforeEach(() => jest.clearAllMocks());

  it('shows the access gate without AttendanceRead', () => {
    const { fixture, service } = setup({ perms: [] });
    fixture.detectChanges();
    expect(text(fixture)).toContain('Access required');
    expect(service.getReview).not.toHaveBeenCalled();
  });

  it('loads the review by enrollment id and renders the score + time header', async () => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(service.getReview).toHaveBeenCalledWith('en-1');
    const t = text(fixture);
    expect(t).toContain('Youssef Ibrahim');
    expect(t).toContain('Kinematics — Motion in 1D · Assignment review');
    expect(t).toContain('1/2'); // Score = correctCount/questionCount
    expect(t).toContain('18:24'); // Time spent = mmss(1104)
  });

  it('renders question cards with correct (green) and picked-wrong (red) option highlighting', async () => {
    const { fixture } = setup();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const t = text(fixture);
    expect(t).toContain('What is'); // Q1 LaTeX body (prose part)
    expect(t).toContain('Speed');
    expect(t).toContain('Newton');
    expect(t).toContain('+2'); // correct question's mark pill

    const states = [...fixture.nativeElement.querySelectorAll('.rv-opt')].map((o) =>
      o.getAttribute('data-state'),
    );
    expect(states).toContain('correct'); // the correct option is green + check
    expect(states).toContain('picked-wrong'); // the student's wrong pick is red + ×
  });

  it('renders the disabled Quiz attempts placeholder (5B-2)', async () => {
    const { fixture } = setup();
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.componentInstance.onTab('quiz');
    fixture.detectChanges();
    expect(text(fixture)).toContain('Available once the quiz engine ships (5B-2)');
  });

  it('renders the behaviour-log timeline', async () => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(service.getBehaviour).toHaveBeenCalledWith('en-1');
    fixture.componentInstance.onTab('behaviour');
    fixture.detectChanges();
    const t = text(fixture);
    expect(t).toContain('Entered assessment');
    expect(t).toContain('Answered Q1');
    expect(t).toContain('Focus lost (tab switch)');
  });

  it('"Back" navigates to /attendance', async () => {
    const { fixture } = setup();
    const router = TestBed.inject(Router);
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.componentInstance.back();
    expect(nav).toHaveBeenCalledWith(['/attendance']);
  });
});
