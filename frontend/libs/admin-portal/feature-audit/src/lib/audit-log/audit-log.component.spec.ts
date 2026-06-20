import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';
import { ToastService } from '@sb/shared/ui';
import { AuditFeedItem } from '../data-access/audit.models';
import { AuditService } from '../data-access/audit.service';
import { AuditLogComponent } from './audit-log.component';

// Replace the real data-access module so jest doesn't pull in @angular/fire (ESM) at runtime.
jest.mock('@sb/shared/data-access', () => ({ AuthStore: class AuthStore {} }));

const item = (over: Partial<AuditFeedItem> = {}): AuditFeedItem => ({
  id: 'a1',
  occurredAtUtc: '2026-06-20T12:00:00Z',
  actorType: 'Staff',
  actorRole: 'Teacher',
  actorName: 'Mariam Adel',
  action: 'StudentApproved',
  category: 'approval',
  summary: 'Mariam Adel approved Youssef Ibrahim',
  targetType: 'Student',
  targetId: 's-1',
  targetLabel: 'Youssef Ibrahim',
  portal: 'admin',
  ipAddress: '41.0.0.1',
  ...over,
});

function makeServiceMock(rows: AuditFeedItem[]) {
  const items = signal<AuditFeedItem[]>([]);
  const total = signal(0);
  return {
    items,
    total,
    isLoading: signal(false),
    error: signal<string | null>(null),
    list: jest.fn().mockImplementation(async (q: unknown) => {
      items.set(rows);
      total.set(rows.length);
      return { items: rows, total: rows.length, page: 1, pageSize: 20, totalPages: 1, query: q };
    }),
  };
}

const toast = { success: jest.fn(), error: jest.fn(), info: jest.fn(), warning: jest.fn(), show: jest.fn() };

describe('AuditLogComponent', () => {
  function setup(opts: { perms?: string[]; role?: string; rows?: AuditFeedItem[] } = {}) {
    const perms = opts.perms ?? ['AuditRead', 'AuditReadSensitive'];
    const role = opts.role ?? 'Teacher';
    const rows = opts.rows ?? [item()];
    const service = makeServiceMock(rows);
    TestBed.configureTestingModule({
      imports: [AuditLogComponent],
      providers: [
        provideRouter([]),
        { provide: AuditService, useValue: service },
        { provide: AuthStore, useValue: { hasPermission: (p: string) => perms.includes(p), role: () => role } },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
      ],
    });
    const fixture = TestBed.createComponent(AuditLogComponent);
    return { fixture, service };
  }

  const text = (fixture: { nativeElement: HTMLElement }) => fixture.nativeElement.textContent ?? '';

  beforeEach(() => jest.clearAllMocks());

  it('shows the access gate and loads nothing without AuditRead', () => {
    const { fixture, service } = setup({ perms: [] });
    fixture.detectChanges();
    expect(text(fixture)).toContain('Access required');
    expect(service.list).not.toHaveBeenCalled();
  });

  it('loads the feed on init and renders actor / phrase / target (Teacher)', async () => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(service.list).toHaveBeenCalled();
    const t = text(fixture);
    expect(t).toContain('Mariam Adel');
    expect(t).toContain('approved');
    expect(t).toContain('Youssef Ibrahim');
  });

  it('hides the "Scoped view" alert for Teachers', async () => {
    const { fixture } = setup({ role: 'Teacher' });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(text(fixture)).not.toContain('Scoped view');
  });

  it('shows the "Scoped view" alert for Assistants', async () => {
    const { fixture } = setup({ role: 'Assistant' });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(text(fixture)).toContain('Scoped view');
  });

  it('applies the category filter to the list query and resets to page 1', async () => {
    const { fixture, service } = setup();
    fixture.detectChanges();
    await fixture.whenStable();
    service.list.mockClear();

    fixture.componentInstance.filters.controls.category.setValue('code');
    await fixture.whenStable();

    expect(service.list).toHaveBeenCalled();
    const arg = service.list.mock.calls.at(-1)?.[0] as { category?: string; page?: number };
    expect(arg.category).toBe('code');
    expect(arg.page).toBe(1);
  });

  it('"View" navigates to the affected entity via targetType + targetId', async () => {
    const { fixture } = setup();
    const router = TestBed.inject(Router);
    const nav = jest.spyOn(router, 'navigate').mockResolvedValue(true);
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.componentInstance.view(item({ targetType: 'Session', targetId: 'sess-9' }));
    expect(nav).toHaveBeenCalledWith(['/sessions', 'sess-9']);
  });

  it('toasts when a row has no linked entity', async () => {
    const { fixture } = setup();
    fixture.detectChanges();
    await fixture.whenStable();

    fixture.componentInstance.view(item({ targetType: null, targetId: null }));
    expect(toast.info).toHaveBeenCalledWith('No linked entity for this entry');
  });

  it('renders the empty-state when there are no rows', async () => {
    const { fixture } = setup({ rows: [] });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(text(fixture)).toContain('No activity matches these filters');
  });
});
