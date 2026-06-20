import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { AuthStore, PendingApprovalsStore } from '@sb/shared/data-access';
import { ToastService } from '@sb/shared/ui';
import { StudentDetail } from '../data-access/student.models';
import { StudentService } from '../data-access/student.service';
import { StudentDetailComponent } from './student-detail.component';

// Replace the real data-access module so jest doesn't pull in @angular/fire (ESM) at runtime.
jest.mock('@sb/shared/data-access', () => ({
  AuthStore: class AuthStore {},
  PendingApprovalsStore: class PendingApprovalsStore {},
}));

const detail = (over: Partial<StudentDetail> = {}): StudentDetail => ({
  id: '1',
  fullName: 'Yousef Adel',
  phoneNumber: '+201090000000',
  status: 'Active',
  rejectionReason: null,
  gradeId: 'g1',
  gradeName: 'Grade 10',
  cityId: 'c1',
  cityName: 'Cairo',
  regionId: 'r1',
  regionName: 'Nasr City',
  schoolName: 'Cairo STEM',
  parentPhonePrimary: '+201000000000',
  parentPhoneSecondary: null,
  hasIdImage: false,
  termsVersion: 'v1',
  termsAcceptedAtUtc: '2026-06-01T00:00:00Z',
  lastSeenAtUtc: null,
  createdAtUtc: '2026-06-01T00:00:00Z',
  updatedAtUtc: null,
  activeDevice: null,
  ...over,
});

const emptyPage = { items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 };

function makeService() {
  return {
    getById: jest.fn().mockResolvedValue(detail()),
    grades: signal([]),
    loadGrades: jest.fn().mockResolvedValue(undefined),
    listLoginHistory: jest.fn().mockResolvedValue(emptyPage),
    listActivity: jest.fn().mockResolvedValue(emptyPage),
    listEnrollments: jest.fn().mockResolvedValue({
      items: [
        {
          enrollmentId: 'e1',
          sessionId: 's1',
          sessionTitle: "Newton's Laws",
          method: 'Code',
          status: 'Active',
          amount: 150,
          enrolledAtUtc: '2026-06-20T09:00:00Z',
          codeSerial: 'SB-1',
        },
      ],
      total: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    }),
    getIdImageUrl: jest.fn(),
    error: () => null,
  };
}

const toast = { success: jest.fn(), error: jest.fn(), info: jest.fn(), show: jest.fn(), warning: jest.fn() };

describe('StudentDetailComponent (Phase 4 enrollments tab)', () => {
  function setup(service = makeService()) {
    TestBed.configureTestingModule({
      imports: [StudentDetailComponent],
      providers: [
        provideRouter([]),
        { provide: StudentService, useValue: service },
        { provide: AuthStore, useValue: { hasPermission: () => true } },
        { provide: PendingApprovalsStore, useValue: { refresh: jest.fn(), count: () => 0 } },
        { provide: ToastService, useValue: toast },
      ],
    });
    const fixture = TestBed.createComponent(StudentDetailComponent);
    fixture.componentRef.setInput('id', '1');
    return { fixture, service };
  }

  it('loads enrolments from #11 when the tab is opened and renders Session/Amount', async () => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    fixture.componentInstance.onTabChange('enroll');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(service.listEnrollments).toHaveBeenCalledWith('1', 1, 20);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain("Newton's Laws");
    expect(text).toContain('EGP 150');
  });
});
