import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';
import { SessionDetailDto } from '../data-access/session.models';
import { SessionService } from '../data-access/session.service';
import { SessionFormComponent } from './session-form.component';

// Replace the real AuthStore module so jest doesn't pull in @angular/fire (ESM) at runtime.
jest.mock('@sb/shared/data-access', () => ({ AuthStore: class AuthStore {} }));

/** Flush the constructor effect + its queued #init microtask. */
const settle = (): Promise<void> => new Promise((resolve) => setTimeout(resolve));

const detail = (over: Partial<SessionDetailDto> = {}): SessionDetailDto => ({
  id: 's9',
  title: 'New',
  description: '',
  price: 100,
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

function makeServiceMock() {
  return {
    error: signal<string | null>(null),
    grades: signal([{ id: 'g1', name: 'Grade 12' }]),
    subjects: signal([{ id: 'sub1', name: 'Physics' }]),
    specializations: signal([{ id: 'sp1', name: 'Mechanics', subjectId: 'sub1', subjectName: 'Physics' }]),
    loadGrades: jest.fn().mockResolvedValue(undefined),
    loadSubjects: jest.fn().mockResolvedValue(undefined),
    loadSpecializations: jest.fn().mockResolvedValue(undefined),
    create: jest.fn().mockResolvedValue(detail()),
    update: jest.fn().mockResolvedValue(detail()),
    listRaw: jest.fn().mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 1000, totalPages: 0 }),
  };
}

describe('SessionFormComponent (create)', () => {
  function setup() {
    const service = makeServiceMock();
    TestBed.configureTestingModule({
      imports: [SessionFormComponent],
      providers: [
        provideRouter([]),
        { provide: SessionService, useValue: service },
        { provide: AuthStore, useValue: { hasPermission: () => true } },
      ],
    });
    const fixture = TestBed.createComponent(SessionFormComponent);
    const router = TestBed.inject(Router);
    jest.spyOn(router, 'navigate').mockResolvedValue(true);
    // No `id` input set → create mode.
    fixture.detectChanges();
    return { fixture, service, router };
  }

  it('does not create when required fields are empty', async () => {
    const { fixture, service } = setup();
    await settle();
    await fixture.componentInstance.save();
    expect(service.create).not.toHaveBeenCalled();
    expect(fixture.componentInstance.form.controls.title.touched).toBe(true);
  });

  it('POSTs the create payload once the details are valid', async () => {
    const { fixture, service, router } = setup();
    await settle();
    const form = fixture.componentInstance.form;
    form.controls.title.setValue('Kinematics');
    form.controls.description.setValue('Motion basics');
    form.controls.subjectId.setValue('sub1');
    form.controls.specializationId.setValue('sp1');
    form.controls.gradeId.setValue('g1');
    form.controls.price.setValue(150);
    form.controls.validityDays.setValue(60);

    await fixture.componentInstance.save();

    expect(service.create).toHaveBeenCalledTimes(1);
    expect(service.create).toHaveBeenCalledWith({
      title: 'Kinematics',
      description: 'Motion basics',
      price: 150,
      validityDays: 60,
      gradeId: 'g1',
      specializationId: 'sp1',
    });
    expect(router.navigate).toHaveBeenCalledWith(['/sessions', 's9', 'edit']);
  });
});
