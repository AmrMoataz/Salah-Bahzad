import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SessionAttendanceRow, StudentAttendanceRow } from './attendance.models';
import { AttendanceService } from './attendance.service';

const sessionRow = (over: Partial<SessionAttendanceRow> = {}): SessionAttendanceRow => ({
  enrollmentId: 'en-1',
  studentId: 'st-1',
  studentName: 'Youssef Ibrahim',
  videosWatched: 0,
  videosTotal: 3,
  assignmentPercent: 80,
  bestQuizPercent: null,
  quizAttemptCount: 0,
  ...over,
});

const studentRow = (over: Partial<StudentAttendanceRow> = {}): StudentAttendanceRow => ({
  enrollmentId: 'en-1',
  sessionId: 'se-1',
  sessionTitle: 'Kinematics — Motion in 1D',
  videosWatched: 0,
  videosTotal: 3,
  assignmentPercent: 80,
  bestQuizPercent: null,
  quizAttemptCount: 0,
  ...over,
});

describe('AttendanceService', () => {
  let service: AttendanceService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AttendanceService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AttendanceService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('listBySession() GETs /api/attendance/sessions/{id} with paging and fills sessionRows', async () => {
    const promise = service.listBySession('se-1');

    const req = http.expectOne((r) => r.url.endsWith('/api/attendance/sessions/se-1'));
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('50');
    req.flush({ items: [sessionRow()], total: 1, page: 1, pageSize: 50, totalPages: 1 });

    await promise;
    expect(service.sessionRows().length).toBe(1);
    expect(service.sessionRows()[0].assignmentPercent).toBe(80);
    expect(service.total()).toBe(1);
    expect(service.isLoading()).toBe(false);
  });

  it('listByStudent() GETs /api/attendance/students/{id} and fills studentRows', async () => {
    const promise = service.listByStudent('st-1', 2);

    const req = http.expectOne((r) => r.url.endsWith('/api/attendance/students/st-1'));
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('2');
    req.flush({ items: [studentRow()], total: 1, page: 2, pageSize: 50, totalPages: 1 });

    await promise;
    expect(service.studentRows().length).toBe(1);
    expect(service.studentRows()[0].sessionTitle).toBe('Kinematics — Motion in 1D');
  });

  it('exportSession() GETs the session export as a blob (streamed CSV, audited server-side)', async () => {
    const promise = service.exportSession('se-1');

    const req = http.expectOne((r) => r.url.endsWith('/api/attendance/sessions/se-1/export'));
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['Student,Assignment\n']), {
      headers: { 'content-disposition': 'attachment; filename="attendance.csv"' },
    });

    await expect(promise).resolves.toBeUndefined();
  });

  it('exportStudent() GETs the student export as a blob', async () => {
    const promise = service.exportStudent('st-1');

    const req = http.expectOne((r) => r.url.endsWith('/api/attendance/students/st-1/export'));
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['Session,Quiz best\n']));

    await expect(promise).resolves.toBeUndefined();
  });

  it('loadSessions() reads /api/sessions directly (page=1,pageSize=200) and caches', async () => {
    const promise = service.loadSessions();

    const req = http.expectOne((r) => r.url.endsWith('/api/sessions'));
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('200');
    req.flush({ items: [{ id: 'se-1', title: 'Kinematics' }], total: 1, page: 1, pageSize: 200, totalPages: 1 });

    await promise;
    expect(service.sessions()).toEqual([{ id: 'se-1', title: 'Kinematics' }]);

    // Cached: a second call issues no request.
    await service.loadSessions();
    http.expectNone((r) => r.url.endsWith('/api/sessions'));
  });

  it('loadStudents() reads active students and maps fullName → name', async () => {
    const promise = service.loadStudents();

    const req = http.expectOne((r) => r.url.endsWith('/api/students'));
    expect(req.request.params.get('status')).toBe('Active');
    expect(req.request.params.get('pageSize')).toBe('200');
    req.flush({
      items: [{ id: 'st-1', fullName: 'Youssef Ibrahim' }],
      total: 1,
      page: 1,
      pageSize: 200,
      totalPages: 1,
    });

    await promise;
    expect(service.students()).toEqual([{ id: 'st-1', name: 'Youssef Ibrahim' }]);
  });

  it('surfaces the ProblemDetails detail on a failed list', async () => {
    const promise = service.listBySession('se-1');

    const req = http.expectOne((r) => r.url.endsWith('/api/attendance/sessions/se-1'));
    req.flush({ detail: 'You cannot read attendance.' }, { status: 403, statusText: 'Forbidden' });

    await expect(promise).rejects.toBeTruthy();
    expect(service.error()).toBe('You cannot read attendance.');
    expect(service.isLoading()).toBe(false);
  });
});
