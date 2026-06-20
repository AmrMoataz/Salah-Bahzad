import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';
import { CodeListDto } from '../data-access/code.models';
import { CodeService } from '../data-access/code.service';
import { CodeListComponent } from './code-list.component';

// Replace the real AuthStore module so jest doesn't pull in @angular/fire (ESM) at runtime.
jest.mock('@sb/shared/data-access', () => ({ AuthStore: class AuthStore {} }));

const code = (over: Partial<CodeListDto> = {}): CodeListDto => ({
  id: 'c1',
  serial: 'SB-2ABCD-3EFGH',
  value: 150,
  status: 'Active',
  batchId: 'b1',
  batchLabel: 'NEW-20260620-01',
  sessionId: 's1',
  sessionTitle: "Newton's Laws",
  redeemedByStudentId: null,
  redeemedByStudentName: null,
  redeemedAtUtc: null,
  createdByName: 'Salah Bahzad',
  createdAtUtc: '2026-06-20T09:00:00Z',
  ...over,
});

function makeServiceMock(rows: CodeListDto[]) {
  const codes = signal<CodeListDto[]>([]);
  const total = signal(0);
  return {
    codes,
    total,
    isLoading: signal(false),
    error: signal<string | null>(null),
    sessions: signal([{ id: 's1', title: "Newton's Laws" }]),
    loadSessions: jest.fn().mockResolvedValue(undefined),
    list: jest.fn().mockImplementation(async (q: unknown) => {
      codes.set(rows);
      total.set(rows.length);
      return { items: rows, total: rows.length, page: 1, pageSize: 10, totalPages: 1, query: q };
    }),
    disable: jest.fn().mockResolvedValue(code({ status: 'Inactive' })),
    enable: jest.fn().mockResolvedValue(code({ status: 'Active' })),
    remove: jest.fn().mockResolvedValue(undefined),
    export: jest.fn().mockResolvedValue(undefined),
    exportRows: jest.fn(),
  };
}

const TEACHER = ['CodesRead', 'CodesGenerate', 'CodesDisable', 'CodesDelete'];

describe('CodeListComponent', () => {
  function setup(perms: string[] = TEACHER, rows: CodeListDto[] = [code()]) {
    const service = makeServiceMock(rows);
    TestBed.configureTestingModule({
      imports: [CodeListComponent],
      providers: [
        provideRouter([]),
        { provide: CodeService, useValue: service },
        { provide: AuthStore, useValue: { hasPermission: (p: string) => perms.includes(p) } },
      ],
    });
    const fixture = TestBed.createComponent(CodeListComponent);
    return { fixture, service };
  }

  const text = (fixture: { nativeElement: HTMLElement }) =>
    fixture.nativeElement.textContent ?? '';

  it('shows the access gate and loads nothing without CodesRead', () => {
    const { fixture, service } = setup([]);
    fixture.detectChanges();
    expect(text(fixture)).toContain('Access required');
    expect(service.list).not.toHaveBeenCalled();
  });

  it('loads the register on init and renders a code row (Teacher)', fakeAsync(() => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    tick();
    fixture.detectChanges();
    expect(service.loadSessions).toHaveBeenCalled();
    expect(service.list).toHaveBeenCalled();
    expect(text(fixture)).toContain('SB-2ABCD-3EFGH');
  }));

  it('applies the status filter to the list query (debounced)', fakeAsync(() => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    tick();
    service.list.mockClear();

    fixture.componentInstance.filters.controls.status.setValue('Used');
    tick(300); // flush debounceTime(250)

    expect(service.list).toHaveBeenCalled();
    const arg = service.list.mock.calls.at(-1)?.[0] as { status?: string };
    expect(arg.status).toBe('Used');
  }));

  it('shows the Teacher select-column checkbox', fakeAsync(() => {
    const { fixture } = setup(['CodesRead', 'CodesDisable']);
    fixture.detectChanges();
    tick();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.cd__check')).toBeTruthy();
  }));

  it('gives Assistants a read-only alert and no checkboxes', fakeAsync(() => {
    const { fixture } = setup(['CodesRead']);
    fixture.detectChanges();
    tick();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.cd__check')).toBeFalsy();
    expect(text(fixture)).toContain('Read-only');
  }));

  it('disables the checkbox for Used codes and refuses to select them', fakeAsync(() => {
    const used = code({ id: 'cu', serial: 'SB-USED', status: 'Used' });
    const { fixture } = setup(['CodesRead', 'CodesDisable'], [used]);
    fixture.detectChanges();
    tick();
    fixture.detectChanges();

    const checkbox = fixture.nativeElement.querySelector('.cd__check') as HTMLInputElement;
    expect(checkbox.disabled).toBe(true);

    fixture.componentInstance.toggle(used);
    expect(fixture.componentInstance.selectedCount()).toBe(0);
  }));

  it('builds the selection CSV from the selected rows', fakeAsync(() => {
    const rows = [code({ id: 'c1' }), code({ id: 'c2', serial: 'SB-2' })];
    const { fixture, service } = setup(['CodesRead', 'CodesDisable'], rows);
    fixture.detectChanges();
    tick();
    fixture.detectChanges();

    fixture.componentInstance.toggle(rows[0]);
    fixture.componentInstance.exportSelection();

    expect(service.exportRows).toHaveBeenCalled();
    const selected = service.exportRows.mock.calls[0][0] as CodeListDto[];
    expect(selected.map((c) => c.id)).toEqual(['c1']);
  }));

  it('navigates to the generate screen', fakeAsync(() => {
    const { fixture } = setup();
    const router = TestBed.inject(Router);
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.detectChanges();
    tick();

    fixture.componentInstance.generate();
    expect(nav).toHaveBeenCalledWith(['/codes/generate']);
  }));
});
