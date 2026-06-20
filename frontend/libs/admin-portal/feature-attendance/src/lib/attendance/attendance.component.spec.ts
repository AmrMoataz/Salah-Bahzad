import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';
import { ToastService } from '@sb/shared/ui';
import { SessionAttendanceRow, StudentAttendanceRow } from '../data-access/attendance.models';
import { AttendanceService } from '../data-access/attendance.service';
import { AttendanceComponent } from './attendance.component';

// Replace the real data-access module so jest doesn't pull in @angular/fire (ESM) at runtime.
jest.mock('@sb/shared/data-access', () => ({ AuthStore: class AuthStore {} }));

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
  enrollmentId: 'en-9',
  sessionId: 'se-1',
  sessionTitle: 'Kinematics — Motion in 1D',
  videosWatched: 0,
  videosTotal: 3,
  assignmentPercent: 80,
  bestQuizPercent: null,
  quizAttemptCount: 0,
  ...over,
});

function makeServiceMock(opts: { sRows?: SessionAttendanceRow[]; stRows?: StudentAttendanceRow[] } = {}) {
  const sessionRows = signal<SessionAttendanceRow[]>([]);
  const studentRows = signal<StudentAttendanceRow[]>([]);
  const sessions = signal([{ id: 'se-1', title: 'Kinematics — Motion in 1D' }]);
  const students = signal([{ id: 'st-1', name: 'Youssef Ibrahim' }]);
  return {
    sessionRows,
    studentRows,
    sessions,
    students,
    total: signal(0),
    isLoading: signal(false),
    error: signal<string | null>(null),
    loadSessions: jest.fn().mockResolvedValue(undefined),
    loadStudents: jest.fn().mockResolvedValue(undefined),
    listBySession: jest.fn().mockImplementation(async () => sessionRows.set(opts.sRows ?? [sessionRow()])),
    listByStudent: jest.fn().mockImplementation(async () => studentRows.set(opts.stRows ?? [studentRow()])),
    exportSession: jest.fn().mockResolvedValue(undefined),
    exportStudent: jest.fn().mockResolvedValue(undefined),
  };
}

const toast = { success: jest.fn(), error: jest.fn(), info: jest.fn(), warning: jest.fn(), show: jest.fn() };

describe('AttendanceComponent', () => {
  function setup(opts: { perms?: string[]; service?: ReturnType<typeof makeServiceMock> } = {}) {
    const perms = opts.perms ?? ['AttendanceRead', 'AttendanceExport'];
    const service = opts.service ?? makeServiceMock();
    TestBed.configureTestingModule({
      imports: [AttendanceComponent],
      providers: [
        provideRouter([]),
        { provide: AttendanceService, useValue: service },
        { provide: AuthStore, useValue: { hasPermission: (p: string) => perms.includes(p), role: () => 'Teacher' } },
        { provide: ToastService, useValue: toast },
      ],
    });
    const fixture = TestBed.createComponent(AttendanceComponent);
    return { fixture, service };
  }

  const text = (fixture: { nativeElement: HTMLElement }) => fixture.nativeElement.textContent ?? '';

  beforeEach(() => jest.clearAllMocks());

  it('shows the access gate and loads nothing without AttendanceRead', () => {
    const { fixture, service } = setup({ perms: [] });
    fixture.detectChanges();
    expect(text(fixture)).toContain('Access required');
    expect(service.loadSessions).not.toHaveBeenCalled();
  });

  it('loads sessions, default-selects the first, and renders the cohort matrix', async () => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(service.loadSessions).toHaveBeenCalled();
    expect(service.listBySession).toHaveBeenCalledWith('se-1');
    const t = text(fixture);
    expect(t).toContain('By session');
    expect(t).toContain('Youssef Ibrahim');
    expect(t).toContain('cohort matrix');
    // Assignment percent renders; pending quiz columns render as the em-dash.
    expect(t).toContain('80%');
    expect(t).toContain('—');
  });

  it('renders real quiz columns (Quiz best % + Attempts) once the 5B-2 engine populates them', async () => {
    const service = makeServiceMock({
      sRows: [sessionRow({ bestQuizPercent: 78, quizAttemptCount: 2 })],
    });
    const { fixture } = setup({ service });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const t = text(fixture);
    expect(t).toContain('78%'); // Quiz best now a real percent, not the em-dash
    expect(t).toContain('2'); // Attempts count
    // The pending caption now flags only Videos (5C), not quizzes.
    expect(t).toContain('Videos watched populates when video tracking ships (5C)');
    expect(t).not.toContain('5B-2');
  });

  it('switches to "By student" and loads that student\'s per-session breakdown', async () => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.componentInstance.onTab('student');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(service.loadStudents).toHaveBeenCalled();
    expect(service.listByStudent).toHaveBeenCalledWith('st-1');
    expect(text(fixture)).toContain('per-session breakdown');
  });

  it('"Drill in" navigates to /review/{enrollmentId}', async () => {
    const { fixture } = setup();
    const router = TestBed.inject(Router);
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.componentInstance.drill('en-1');
    expect(nav).toHaveBeenCalledWith(['/review', 'en-1']);
  });

  it('Export streams the active session-tab selection as CSV', async () => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    await fixture.whenStable();

    await fixture.componentInstance.export();
    expect(service.exportSession).toHaveBeenCalledWith('se-1');
    expect(service.exportStudent).not.toHaveBeenCalled();
  });
});
