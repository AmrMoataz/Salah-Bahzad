import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';
import { ToastService } from '@sb/shared/ui';
import { DashboardSummary } from '../data-access/dashboard.models';
import { DashboardService } from '../data-access/dashboard.service';
import { DashboardComponent } from './dashboard.component';

// Replace the real data-access module so jest doesn't pull in @angular/fire (ESM) at runtime.
jest.mock('@sb/shared/data-access', () => ({ AuthStore: class AuthStore {} }));

/** A 30-day dense series (one enrollment per day) → 6 weekly buckets of 5, 30 total. */
function makeSummary(over: Partial<DashboardSummary> = {}): DashboardSummary {
  const end = new Date(Date.UTC(2026, 5, 20));
  const enrollmentsByDay = Array.from({ length: 30 }, (_, i) => {
    const d = new Date(end);
    d.setUTCDate(end.getUTCDate() - (29 - i));
    const key = `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}-${String(d.getUTCDate()).padStart(2, '0')}`;
    return { date: key, count: 1 };
  });
  return {
    pendingApprovals: 7,
    activeStudents: 123,
    codesUsed: 40,
    codesActive: 60,
    revenueFromCodes: 12500,
    periodFrom: '2026-05-22T00:00:00Z',
    periodTo: '2026-06-20T12:00:00Z',
    enrollmentsByDay,
    enrollmentsTotal: 30,
    recentActivity: [],
    ...over,
  };
}

function makeServiceMock(summary = makeSummary()) {
  const summarySig = signal<DashboardSummary | null>(null);
  return {
    summary: summarySig,
    isLoading: signal(false),
    error: signal<string | null>(null),
    load: jest.fn().mockImplementation(async () => {
      summarySig.set(summary);
      return summary;
    }),
  };
}

const toast = { success: jest.fn(), error: jest.fn(), info: jest.fn(), warning: jest.fn(), show: jest.fn() };

describe('DashboardComponent', () => {
  function setup(opts: { role?: string; summary?: DashboardSummary } = {}) {
    const role = opts.role ?? 'Teacher';
    const service = makeServiceMock(opts.summary ?? makeSummary());
    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        provideRouter([]),
        { provide: DashboardService, useValue: service },
        { provide: AuthStore, useValue: { hasPermission: () => true, role: () => role } },
        { provide: ToastService, useValue: toast },
      ],
    });
    const fixture = TestBed.createComponent(DashboardComponent);
    return { fixture, service };
  }

  const text = (fixture: { nativeElement: HTMLElement }) => fixture.nativeElement.textContent ?? '';

  beforeEach(() => jest.clearAllMocks());

  it('renders the 4 KPI values from the summary', async () => {
    const { fixture } = setup();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const values = Array.from(
      fixture.nativeElement.querySelectorAll('.db-stat__value'),
    ).map((el) => (el as HTMLElement).textContent?.trim());

    expect(values).toEqual(['7', '123', '40 / 60', `EGP ${(12500).toLocaleString()}`]);
  });

  it('buckets the 30d enrollments series into 6 weekly bars', async () => {
    const { fixture } = setup();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const bars = fixture.nativeElement.querySelectorAll('.db-chart__bar');
    expect(bars.length).toBe(6);
  });

  it('hides the "Generate codes" quick action for an Assistant', async () => {
    const { fixture } = setup({ role: 'Assistant' });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const t = text(fixture);
    expect(t).toContain('Review approvals'); // shown to everyone
    expect(t).not.toContain('Generate codes'); // Teacher-only
  });

  it('shows the "Generate codes" quick action for a Teacher', async () => {
    const { fixture } = setup({ role: 'Teacher' });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(text(fixture)).toContain('Generate codes');
  });
});
