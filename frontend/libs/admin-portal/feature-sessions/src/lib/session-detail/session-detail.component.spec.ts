import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';
import { ToastService } from '@sb/shared/ui';
import { EnrollmentListDto, SessionDetailDto } from '../data-access/session.models';
import { SessionService } from '../data-access/session.service';
import { SessionDetailComponent } from './session-detail.component';

// Replace the real AuthStore module so jest doesn't pull in @angular/fire (ESM) at runtime.
jest.mock('@sb/shared/data-access', () => ({ AuthStore: class AuthStore {} }));

const detail = (over: Partial<SessionDetailDto> = {}): SessionDetailDto => ({
  id: 's1',
  title: 'Kinematics',
  description: null,
  price: 150,
  validityDays: 90,
  thumbnailUrl: null,
  gradeId: 'g1',
  gradeName: 'Grade 12',
  subjectId: 'sub1',
  subjectName: 'Physics',
  specializationId: 'sp1',
  specializationName: 'Mechanics',
  status: 'Published',
  prerequisiteSessionId: null,
  prerequisiteTitle: null,
  quizSetting: null,
  videos: [],
  materials: [],
  questionCount: 0,
  quizEligibleQuestionCount: 0,
  enrolledCount: 3,
  createdAtUtc: '2026-06-01T00:00:00Z',
  updatedAtUtc: null,
  ...over,
});

const enrollment = (over: Partial<EnrollmentListDto> = {}): EnrollmentListDto => ({
  enrollmentId: 'e1',
  studentId: 'st1',
  studentName: 'Mariam Adel',
  studentInitials: 'MA',
  method: 'Code',
  status: 'Active',
  enrolledAtUtc: '2026-06-20T09:00:00Z',
  quizBestPercent: 0,
  videosWatched: 0,
  videosTotal: 0,
  ...over,
});

function makeService() {
  return {
    getById: jest.fn().mockResolvedValue(detail()),
    listQuestions: jest.fn().mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 10, totalPages: 0 }),
    listActivity: jest.fn().mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 15, totalPages: 0 }),
    listEnrollments: jest
      .fn()
      .mockResolvedValue({ items: [enrollment()], total: 1, page: 1, pageSize: 10, totalPages: 1 }),
    unlock: jest.fn().mockResolvedValue({ id: 'e1', method: 'Unlock' }),
    refund: jest.fn().mockResolvedValue({ id: 'e1', status: 'Refunded' }),
    searchActiveStudents: jest
      .fn()
      .mockResolvedValue([{ id: 'st1', name: 'Mariam Adel', phone: '01000000000' }]),
    error: () => null,
  };
}

const toast = { success: jest.fn(), error: jest.fn(), info: jest.fn(), show: jest.fn(), warning: jest.fn() };

const UNLOCK_REFUND = ['EnrollmentsUnlock', 'EnrollmentsRefund', 'SessionsEdit'];

describe('SessionDetailComponent (Phase 4 enrollment)', () => {
  function setup(perms: string[] = UNLOCK_REFUND, service = makeService()) {
    TestBed.configureTestingModule({
      imports: [SessionDetailComponent],
      providers: [
        provideRouter([]),
        { provide: SessionService, useValue: service },
        { provide: AuthStore, useValue: { hasPermission: (p: string) => perms.includes(p) } },
        { provide: ToastService, useValue: toast },
      ],
    });
    const fixture = TestBed.createComponent(SessionDetailComponent);
    fixture.componentRef.setInput('id', 's1');
    return { fixture, service };
  }

  const text = (fixture: ComponentFixture<SessionDetailComponent>) =>
    (fixture.nativeElement as HTMLElement).textContent ?? '';

  async function load(fixture: ComponentFixture<SessionDetailComponent>) {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(() => jest.clearAllMocks());

  it('loads the Enrolled tab from #8 and renders a student row', async () => {
    const { fixture, service } = setup();
    await load(fixture);

    fixture.componentInstance.onTab('enrolled');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(service.listEnrollments).toHaveBeenCalledWith('s1', 1, 10);
    expect(fixture.componentInstance.enrollments().length).toBe(1);
    expect(text(fixture)).toContain('Mariam Adel');
  });

  it('hides the "Unlock for student" button without EnrollmentsUnlock', async () => {
    const { fixture } = setup(['SessionsEdit']);
    await load(fixture);
    expect(text(fixture)).not.toContain('Unlock for student');
  });

  it('opens the unlock picker (loads active students) and unlocks (#9)', async () => {
    const { fixture, service } = setup();
    await load(fixture);

    await fixture.componentInstance.openUnlock();
    expect(service.searchActiveStudents).toHaveBeenCalled();
    expect(fixture.componentInstance.unlockOpen()).toBe(true);
    expect(fixture.componentInstance.studentOptions()).toEqual([
      { value: 'st1', label: 'Mariam Adel', description: '01000000000' },
    ]);

    fixture.componentInstance.unlockStudent.setValue('st1');
    await fixture.componentInstance.confirmUnlock();

    expect(service.unlock).toHaveBeenCalledWith('s1', 'st1');
    expect(toast.success).toHaveBeenCalledWith('Session unlocked for student');
    expect(fixture.componentInstance.unlockOpen()).toBe(false);
  });

  it('refunds an enrollment after the danger confirm (#10)', async () => {
    const { fixture, service } = setup();
    await load(fixture);
    fixture.componentInstance.onTab('enrolled');
    await fixture.whenStable();
    fixture.detectChanges();

    const row = fixture.componentInstance.enrollments()[0];
    fixture.componentInstance.askRefund(row);
    expect(fixture.componentInstance.refundOpen()).toBe(true);

    await fixture.componentInstance.confirmRefund();

    expect(service.refund).toHaveBeenCalledWith('e1');
    expect(toast.info).toHaveBeenCalledWith('Enrollment refunded');
  });
});
