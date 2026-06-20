import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuditFeedItem } from './audit.models';
import { AuditService } from './audit.service';

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

describe('AuditService', () => {
  let service: AuditService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AuditService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuditService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('list() GETs /api/audit with category/period/paging and omits empty filters', async () => {
    const promise = service.list({
      category: 'code',
      period: '30d',
      page: 2,
      pageSize: 20,
      actorId: null,
      from: undefined,
    });

    const req = http.expectOne((r) => r.url.endsWith('/api/audit'));
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('category')).toBe('code');
    expect(req.request.params.get('period')).toBe('30d');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('20');
    // empty filters are omitted entirely
    expect(req.request.params.get('actorId')).toBeNull();
    expect(req.request.params.get('from')).toBeNull();
    expect(req.request.params.get('studentId')).toBeNull();
    req.flush({ items: [item()], total: 1, page: 2, pageSize: 20, totalPages: 1 });

    await promise;
    expect(service.items().length).toBe(1);
    expect(service.total()).toBe(1);
    expect(service.isLoading()).toBe(false);
  });

  it('list() defaults page=1 & pageSize=20 and forwards the entity-tab filters', async () => {
    const promise = service.list({ studentId: 'stu-1', entityType: 'Student', entityId: 'stu-1' });

    const req = http.expectOne((r) => r.url.endsWith('/api/audit'));
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('20');
    expect(req.request.params.get('studentId')).toBe('stu-1');
    expect(req.request.params.get('entityType')).toBe('Student');
    expect(req.request.params.get('entityId')).toBe('stu-1');
    req.flush({ items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 });

    await promise;
    expect(service.items()).toEqual([]);
  });

  it('list() surfaces the ProblemDetails detail on error', async () => {
    const promise = service.list();

    const req = http.expectOne((r) => r.url.endsWith('/api/audit'));
    req.flush({ detail: 'You cannot read the audit log.' }, { status: 403, statusText: 'Forbidden' });

    await expect(promise).rejects.toBeTruthy();
    expect(service.error()).toBe('You cannot read the audit log.');
    expect(service.isLoading()).toBe(false);
  });

  it('has NO detail method — 5A exposes no before/after endpoint (drill-in navigates to the entity)', () => {
    expect((service as unknown as Record<string, unknown>)['getById']).toBeUndefined();
    expect((service as unknown as Record<string, unknown>)['detail']).toBeUndefined();
  });
});
