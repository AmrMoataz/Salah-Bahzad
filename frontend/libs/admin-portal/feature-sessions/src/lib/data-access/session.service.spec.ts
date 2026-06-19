import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { QuestionDto, SessionDetailDto, SessionListDto } from './session.models';
import { SessionService } from './session.service';

const listRow = (over: Partial<SessionListDto> = {}): SessionListDto => ({
  id: 's1',
  title: 'Kinematics — Motion in 1D',
  gradeName: 'Grade 12',
  subjectName: 'Physics',
  specializationName: 'Mechanics',
  status: 'Draft',
  questionCount: 12,
  videoCount: 4,
  enrolledCount: 0,
  ...over,
});

const detail = (over: Partial<SessionDetailDto> = {}): SessionDetailDto => ({
  id: 's1',
  title: 'Kinematics — Motion in 1D',
  description: 'Intro to motion.',
  price: 150,
  validityDays: 90,
  thumbnailUrl: null,
  gradeId: 'g1',
  gradeName: 'Grade 12',
  subjectId: 'sub1',
  subjectName: 'Physics',
  specializationId: 'sp1',
  specializationName: 'Mechanics',
  status: 'Draft',
  prerequisiteSessionId: null,
  prerequisiteTitle: null,
  quizSetting: null,
  videos: [],
  materials: [],
  questionCount: 0,
  quizEligibleQuestionCount: 0,
  enrolledCount: 0,
  createdAtUtc: '2026-06-01T00:00:00Z',
  updatedAtUtc: null,
  ...over,
});

const question = (over: Partial<QuestionDto> = {}): QuestionDto => ({
  id: 'q1',
  sessionId: 's1',
  bodyLatex: 'What is $v$?',
  imageUrl: null,
  mark: 2,
  isValidForQuiz: true,
  hintUrl: null,
  options: [
    { id: 'o1', text: '10 m/s', isCorrect: true },
    { id: 'o2', text: '20 m/s', isCorrect: false },
  ],
  variations: [],
  createdAtUtc: '2026-06-01T00:00:00Z',
  updatedAtUtc: null,
  ...over,
});

describe('SessionService', () => {
  let service: SessionService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SessionService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(SessionService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('list() GETs /api/sessions with grade/subject/status filters and updates signals', async () => {
    const promise = service.list({
      search: 'kin',
      gradeId: 'g1',
      subjectId: 'sub1',
      status: 'Published',
      page: 1,
      pageSize: 20,
    });

    const req = http.expectOne((r) => r.url.endsWith('/api/sessions'));
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('search')).toBe('kin');
    expect(req.request.params.get('gradeId')).toBe('g1');
    expect(req.request.params.get('subjectId')).toBe('sub1');
    expect(req.request.params.get('specializationId')).toBeNull();
    expect(req.request.params.get('status')).toBe('Published');
    req.flush({ items: [listRow()], total: 1, page: 1, pageSize: 20, totalPages: 1 });

    await promise;
    expect(service.sessions().length).toBe(1);
    expect(service.total()).toBe(1);
    expect(service.isLoading()).toBe(false);
  });

  it('create() POSTs the session details', async () => {
    const body = {
      title: 'New',
      description: 'd',
      price: 100,
      validityDays: 90,
      gradeId: 'g1',
      specializationId: 'sp1',
    };
    const promise = service.create(body);

    const req = http.expectOne((r) => r.url.endsWith('/api/sessions'));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush(detail({ title: 'New' }));

    expect((await promise).title).toBe('New');
  });

  it('update() PUTs to /api/sessions/{id}', async () => {
    const promise = service.update('s1', {
      title: 'Edited',
      description: 'd',
      price: 120,
      validityDays: 30,
      gradeId: 'g1',
      specializationId: 'sp1',
    });

    const req = http.expectOne((r) => r.url.endsWith('/api/sessions/s1'));
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.title).toBe('Edited');
    req.flush(detail({ title: 'Edited' }));

    expect((await promise).title).toBe('Edited');
  });

  it('setPrerequisite() PUTs { prerequisiteSessionId } (null clears)', async () => {
    const promise = service.setPrerequisite('s1', null);

    const req = http.expectOne((r) => r.url.endsWith('/api/sessions/s1/prerequisite'));
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ prerequisiteSessionId: null });
    req.flush(detail());

    await promise;
  });

  it('updateQuizSettings() PUTs the QuizSettingDto in minutes', async () => {
    const settings = { timeLimitMinutes: 15, questionCount: 10, attemptCount: 2, minPassPercent: 60 };
    const promise = service.updateQuizSettings('s1', settings);

    const req = http.expectOne((r) => r.url.endsWith('/api/sessions/s1/quiz-settings'));
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(settings);
    req.flush(detail({ quizSetting: settings }));

    expect((await promise).quizSetting?.timeLimitMinutes).toBe(15);
  });

  it('publish() and archive() POST to their endpoints', async () => {
    const p1 = service.publish('s1');
    const r1 = http.expectOne((r) => r.url.endsWith('/api/sessions/s1/publish'));
    expect(r1.request.method).toBe('POST');
    r1.flush(detail({ status: 'Published' }));
    expect((await p1).status).toBe('Published');

    const p2 = service.archive('s1');
    const r2 = http.expectOne((r) => r.url.endsWith('/api/sessions/s1/archive'));
    expect(r2.request.method).toBe('POST');
    r2.flush(detail({ status: 'Archived' }));
    expect((await p2).status).toBe('Archived');
  });

  it('remove() DELETEs the session', async () => {
    const promise = service.remove('s1');
    const req = http.expectOne((r) => r.url.endsWith('/api/sessions/s1'));
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    await promise;
  });

  it('reorderVideos() PUTs { orderedVideoIds }', async () => {
    const promise = service.reorderVideos('s1', ['v2', 'v1']);
    const req = http.expectOne((r) => r.url.endsWith('/api/sessions/s1/videos/reorder'));
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ orderedVideoIds: ['v2', 'v1'] });
    req.flush([]);
    await promise;
  });

  it('updateVideo() PUTs multipart metadata to /videos/{id} (contract §2.13)', async () => {
    const promise = service.updateVideo('s1', 'v1', 'Lecture 1', 8, 5);
    const req = http.expectOne((r) => r.url.endsWith('/api/sessions/s1/videos/v1'));
    expect(req.request.method).toBe('PUT');
    expect(req.request.body instanceof FormData).toBe(true);
    const form = req.request.body as FormData;
    expect(form.get('title')).toBe('Lecture 1');
    expect(form.get('lengthMinutes')).toBe('8');
    expect(form.get('accessCount')).toBe('5');
    req.flush({
      id: 'v1',
      title: 'Lecture 1',
      order: 0,
      lengthMinutes: 8,
      accessCount: 5,
      processingStatus: 'Ready',
      createdAtUtc: '2026-06-01T00:00:00Z',
    });
    expect((await promise).accessCount).toBe(5);
  });

  it('addVideo() POSTs multipart form-data (file + title + lengthMinutes + accessCount)', async () => {
    const file = new File(['x'], 'lec.mp4', { type: 'video/mp4' });
    const promise = service.addVideo('s1', file, 'Lecture 1', 8, 3);

    const req = http.expectOne((r) => r.url.endsWith('/api/sessions/s1/videos'));
    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBe(true);
    const form = req.request.body as FormData;
    expect(form.get('title')).toBe('Lecture 1');
    expect(form.get('lengthMinutes')).toBe('8');
    expect(form.get('accessCount')).toBe('3');
    req.flush({
      id: 'v9',
      title: 'Lecture 1',
      order: 0,
      lengthMinutes: 8,
      accessCount: 3,
      processingStatus: 'Pending',
      createdAtUtc: '2026-06-01T00:00:00Z',
    });
    expect((await promise).processingStatus).toBe('Pending');
  });

  it('getMaterialUrl() GETs the on-demand signed-URL endpoint', async () => {
    const promise = service.getMaterialUrl('s1', 'm1');
    const req = http.expectOne((r) => r.url.endsWith('/api/sessions/s1/materials/m1/url'));
    expect(req.request.method).toBe('GET');
    req.flush({ url: 'https://signed/x', expiresAtUtc: '2026-06-19T12:15:00Z' });
    expect((await promise).url).toContain('https://');
  });

  it('listQuestions() GETs the paged question bank', async () => {
    const promise = service.listQuestions('s1', 2, 10);
    const req = http.expectOne((r) => r.url.endsWith('/api/sessions/s1/questions'));
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('10');
    req.flush({ items: [question()], total: 1, page: 2, pageSize: 10, totalPages: 1 });
    expect((await promise).items.length).toBe(1);
  });

  it('createQuestion() POSTs the MCQ payload', async () => {
    const body = {
      bodyLatex: 'Q?',
      mark: 2,
      isValidForQuiz: true,
      hintUrl: null,
      options: [
        { text: 'A', isCorrect: true },
        { text: 'B', isCorrect: false },
      ],
    };
    const promise = service.createQuestion('s1', body);
    const req = http.expectOne((r) => r.url.endsWith('/api/sessions/s1/questions'));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush(question());
    await promise;
  });

  it('addVariation() POSTs to the variations endpoint and returns the variation', async () => {
    const body = { bodyLatex: 'V?', options: [{ text: 'A', isCorrect: true }, { text: 'B', isCorrect: false }] };
    const promise = service.addVariation('s1', 'q1', body);
    const req = http.expectOne((r) => r.url.endsWith('/api/sessions/s1/questions/q1/variations'));
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({
      id: 'var1',
      bodyLatex: 'V?',
      imageUrl: null,
      options: [{ id: 'oa', text: 'A', isCorrect: true }, { id: 'ob', text: 'B', isCorrect: false }],
    });
    expect((await promise).id).toBe('var1');
  });

  it('clearQuestionImage() DELETEs the question image', async () => {
    const promise = service.clearQuestionImage('s1', 'q1');
    const req = http.expectOne((r) => r.url.endsWith('/api/sessions/s1/questions/q1/image'));
    expect(req.request.method).toBe('DELETE');
    req.flush(question({ imageUrl: null }));
    await promise;
  });

  it('loadGrades() GETs taxonomy grades once (cached on second call)', async () => {
    const promise = service.loadGrades();
    const req = http.expectOne((r) => r.url.endsWith('/api/taxonomy/grades'));
    expect(req.request.method).toBe('GET');
    req.flush([{ id: 'g1', name: 'Grade 12' }]);
    await promise;
    expect(service.grades().length).toBe(1);

    await service.loadGrades(); // cached → no second request (verified by afterEach)
  });
});
