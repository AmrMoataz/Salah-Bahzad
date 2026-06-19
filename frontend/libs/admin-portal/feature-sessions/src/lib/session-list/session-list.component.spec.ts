import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';
import { SessionListDto } from '../data-access/session.models';
import { SessionService } from '../data-access/session.service';
import { SessionListComponent } from './session-list.component';

// Replace the real AuthStore module so jest doesn't pull in @angular/fire (ESM) at runtime.
// We only need AuthStore as a DI token; the test provides a fake implementation.
jest.mock('@sb/shared/data-access', () => ({ AuthStore: class AuthStore {} }));

const row = (over: Partial<SessionListDto> = {}): SessionListDto => ({
  id: 's1',
  title: 'Kinematics',
  gradeName: 'Grade 12',
  subjectName: 'Physics',
  specializationName: 'Mechanics',
  status: 'Published',
  questionCount: 12,
  videoCount: 4,
  enrolledCount: 0,
  ...over,
});

function makeServiceMock() {
  const sessions = signal<SessionListDto[]>([]);
  const total = signal(0);
  return {
    sessions,
    total,
    isLoading: signal(false),
    error: signal<string | null>(null),
    grades: signal([{ id: 'g1', name: 'Grade 12' }]),
    subjects: signal([{ id: 'sub1', name: 'Physics' }]),
    specializations: signal([{ id: 'sp1', name: 'Mechanics', subjectId: 'sub1', subjectName: 'Physics' }]),
    loadGrades: jest.fn().mockResolvedValue(undefined),
    loadSubjects: jest.fn().mockResolvedValue(undefined),
    loadSpecializations: jest.fn().mockResolvedValue(undefined),
    list: jest.fn().mockImplementation(async (q: unknown) => {
      sessions.set([row()]);
      total.set(1);
      return { items: [row()], total: 1, page: 1, pageSize: 10, totalPages: 1, query: q };
    }),
    listRaw: jest.fn().mockResolvedValue({ items: [row()], total: 1, page: 1, pageSize: 1000, totalPages: 1 }),
  };
}

describe('SessionListComponent', () => {
  function setup(canRead = true) {
    const service = makeServiceMock();
    TestBed.configureTestingModule({
      imports: [SessionListComponent],
      providers: [
        provideRouter([]),
        { provide: SessionService, useValue: service },
        { provide: AuthStore, useValue: { hasPermission: () => canRead } },
      ],
    });
    const fixture = TestBed.createComponent(SessionListComponent);
    return { fixture, service };
  }

  it('shows the access gate and loads nothing without SessionsRead', () => {
    const { fixture, service } = setup(false);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Access required');
    expect(service.list).not.toHaveBeenCalled();
  });

  it('loads the catalogue on init and renders a row', fakeAsync(() => {
    const { fixture, service } = setup(true);
    fixture.detectChanges();
    tick();
    fixture.detectChanges();
    expect(service.loadGrades).toHaveBeenCalled();
    expect(service.list).toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Kinematics');
  }));

  it('applies the status filter to the list query (debounced)', fakeAsync(() => {
    const { fixture, service } = setup(true);
    fixture.detectChanges();
    tick();
    service.list.mockClear();

    fixture.componentInstance.filters.controls.status.setValue('Published');
    tick(300); // flush debounceTime(250)

    expect(service.list).toHaveBeenCalled();
    const arg = service.list.mock.calls.at(-1)?.[0] as { status?: string };
    expect(arg.status).toBe('Published');
  }));

  it('navigates to the editor on Create', fakeAsync(() => {
    const { fixture } = setup(true);
    const router = TestBed.inject(Router);
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.detectChanges();
    tick();

    fixture.componentInstance.create();
    expect(nav).toHaveBeenCalledWith(['/sessions/new']);
  }));
});
