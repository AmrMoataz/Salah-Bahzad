import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AssignmentReview, BehaviourEvent } from './attendance.models';
import { ReviewService } from './review.service';

const review = (over: Partial<AssignmentReview> = {}): AssignmentReview => ({
  studentName: 'Youssef Ibrahim',
  sessionTitle: 'Kinematics — Motion in 1D',
  correctCount: 7,
  questionCount: 9,
  scoreMarks: 14,
  maxMarks: 18,
  percent: 78,
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
        { id: 'o1', order: 0, text: 'Speed', isCorrect: true },
        { id: 'o2', order: 1, text: 'Mass', isCorrect: false },
      ],
      selectedOptionId: 'o1',
      isCorrect: true,
    },
  ],
  ...over,
});

const event = (over: Partial<BehaviourEvent> = {}): BehaviourEvent => ({
  type: 'Answered',
  label: 'Answered Q1',
  questionOrder: 1,
  occurredAtUtc: '2026-06-20T09:05:48Z',
  ...over,
});

describe('ReviewService', () => {
  let service: ReviewService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ReviewService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ReviewService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getReview() GETs /api/review/assignments/{enrollmentId} and stores the payload', async () => {
    const promise = service.getReview('en-1');

    const req = http.expectOne((r) => r.url.endsWith('/api/review/assignments/en-1'));
    expect(req.request.method).toBe('GET');
    req.flush(review());

    const result = await promise;
    expect(result.correctCount).toBe(7);
    expect(service.review()?.questions[0].options[0].isCorrect).toBe(true);
    expect(service.isLoading()).toBe(false);
  });

  it('getBehaviour() GETs the behaviour timeline endpoint and stores the events', async () => {
    const promise = service.getBehaviour('en-1');

    const req = http.expectOne((r) => r.url.endsWith('/api/review/assignments/en-1/behaviour'));
    expect(req.request.method).toBe('GET');
    req.flush([event({ type: 'Entered', label: 'Entered assessment' }), event()]);

    const result = await promise;
    expect(result.length).toBe(2);
    expect(service.behaviour()[0].type).toBe('Entered');
  });

  it('surfaces the ProblemDetails detail on a failed review load', async () => {
    const promise = service.getReview('en-1');

    const req = http.expectOne((r) => r.url.endsWith('/api/review/assignments/en-1'));
    req.flush({ detail: 'No assignment for this enrollment.' }, { status: 404, statusText: 'Not Found' });

    await expect(promise).rejects.toBeTruthy();
    expect(service.error()).toBe('No assignment for this enrollment.');
  });
});
