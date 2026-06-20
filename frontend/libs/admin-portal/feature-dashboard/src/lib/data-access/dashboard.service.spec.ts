import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { DashboardSummary } from './dashboard.models';
import { DashboardService } from './dashboard.service';

const summary: DashboardSummary = {
  pendingApprovals: 3,
  activeStudents: 120,
  codesUsed: 40,
  codesActive: 60,
  revenueFromCodes: 6000,
  periodFrom: '2026-05-21T00:00:00Z',
  periodTo: '2026-06-20T12:00:00Z',
  enrollmentsByDay: [{ date: '2026-06-20', count: 12 }],
  enrollmentsTotal: 12,
  recentActivity: [],
};

describe('DashboardService', () => {
  let service: DashboardService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [DashboardService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(DashboardService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('load() GETs /api/dashboard with the period param and maps the summary into the signal', async () => {
    const promise = service.load({ period: '30d' });

    const req = http.expectOne((r) => r.url.endsWith('/api/dashboard'));
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('period')).toBe('30d');
    req.flush(summary);

    const result = await promise;
    expect(result.pendingApprovals).toBe(3);
    expect(service.summary()?.enrollmentsTotal).toBe(12);
    expect(service.isLoading()).toBe(false);
  });

  it('load() sends a from/to range and omits period when given one', async () => {
    const promise = service.load({ from: '2026-06-01', to: '2026-06-20' });

    const req = http.expectOne((r) => r.url.endsWith('/api/dashboard'));
    expect(req.request.params.get('period')).toBeNull();
    expect(req.request.params.get('from')).toBe('2026-06-01');
    expect(req.request.params.get('to')).toBe('2026-06-20');
    req.flush(summary);

    await promise;
  });

  it('surfaces the ProblemDetails detail on error', async () => {
    const promise = service.load({ period: '7d' });

    const req = http.expectOne((r) => r.url.endsWith('/api/dashboard'));
    req.flush({ detail: 'No dashboard access.' }, { status: 403, statusText: 'Forbidden' });

    await expect(promise).rejects.toBeTruthy();
    expect(service.error()).toBe('No dashboard access.');
    expect(service.isLoading()).toBe(false);
  });
});
