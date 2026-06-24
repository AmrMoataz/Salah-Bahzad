import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

// The data-access barrel imports @angular/fire (ESM, unparsable by jest) — replace it with
// token-only doubles (the ESM-fire gotcha from S2/S3). The model types are erased at compile time,
// so only the injectable tokens need a runtime double.
jest.mock('@sb/student-portal/data-access', () => ({
  PlanService: class PlanService {},
  StudentAuthStore: class StudentAuthStore {},
}));

import { HomeComponent } from './home.component';
import {
  MyPlanDto,
  MyPlanKpis,
  MyPlanRecent,
  MyPlanStep,
  PlanService,
  StudentAuthStore,
} from '@sb/student-portal/data-access';

const ZERO_KPIS: MyPlanKpis = {
  activeSessions: 0,
  videosWatched: 0,
  videosTotal: 0,
  overallProgressPercent: 0,
  completedSessions: 0,
};

const ACTIVE_KPIS: MyPlanKpis = {
  activeSessions: 2,
  videosWatched: 12,
  videosTotal: 24,
  overallProgressPercent: 51,
  completedSessions: 1,
};

function makeStep(over: Partial<MyPlanStep> = {}): MyPlanStep {
  return {
    key: 'videos:s1',
    kind: 'Videos',
    title: 'Watch your lessons',
    subtitle: '2 of 6 watched',
    sessionId: 's1',
    sessionTitle: 'Algebra Basics',
    specializationName: 'Pure Maths',
    status: 'Pending',
    blocked: false,
    blockedReason: null,
    dueState: 'None',
    expiresAtUtc: null,
    progress: { done: 2, total: 6 },
    action: { type: 'Navigate', route: '/sessions/s1', label: 'Continue' },
    ...over,
  };
}

function makeFocus(): MyPlanDto['focus'] {
  return {
    sessionId: 's1',
    title: 'Algebra Basics',
    specializationName: 'Pure Maths',
    thumbnailUrl: null,
    progressPercent: 33,
    expiresAtUtc: null,
    isExpired: false,
    expiresInDays: null,
    dueState: 'None',
  };
}

function makePlan(over: Partial<MyPlanDto> = {}): MyPlanDto {
  return {
    isoWeek: '2026-W25',
    weekStartUtc: '2026-06-15T00:00:00Z',
    weekEndUtc: '2026-06-21T23:59:59Z',
    generatedAtUtc: '2026-06-22T08:00:00Z',
    totalSteps: 0,
    completedSteps: 0,
    overdueSteps: 0,
    kpis: { ...ZERO_KPIS },
    focus: null,
    steps: [],
    recentlyEnrolled: [],
    ...over,
  };
}

const daysOut = (n: number): string => new Date(Date.now() + n * 86_400_000).toISOString();
const daysAgo = (n: number): string => new Date(Date.now() - n * 86_400_000).toISOString();

describe('HomeComponent (FR-STU-SES-001, FR-STU-CAT-003, NFR-A11Y-001)', () => {
  let fixture: ComponentFixture<HomeComponent>;
  let plan: { plan: jest.Mock };

  function setup(dto: MyPlanDto, firstName = 'Lina') {
    plan = { plan: jest.fn().mockReturnValue(of(dto)) };
    TestBed.configureTestingModule({
      imports: [HomeComponent],
      providers: [
        provideRouter([]),
        { provide: PlanService, useValue: plan },
        { provide: StudentAuthStore, useValue: { firstName: () => firstName } },
      ],
    });
    fixture = TestBed.createComponent(HomeComponent);
    fixture.detectChanges();
    return fixture;
  }

  const root = () => fixture.nativeElement as HTMLElement;
  const text = () => root().textContent ?? '';
  const kpis = (): HTMLElement[] => Array.from(root().querySelectorAll<HTMLElement>('sb-home-kpi'));
  const rows = (): HTMLElement[] => Array.from(root().querySelectorAll<HTMLElement>('sb-home-plan-row'));
  const tiles = (): HTMLElement[] => Array.from(root().querySelectorAll<HTMLElement>('sb-home-recent-tile'));
  const anchors = (): HTMLAnchorElement[] => Array.from(root().querySelectorAll<HTMLAnchorElement>('a'));
  const ctaByText = (t: string): HTMLAnchorElement | undefined =>
    Array.from(root().querySelectorAll<HTMLAnchorElement>('sb-home-plan-row a.row__cta')).find((a) =>
      (a.textContent ?? '').includes(t),
    );
  const anchorByText = (t: string): HTMLAnchorElement | undefined =>
    anchors().find((a) => (a.textContent ?? '').trim() === t);

  // ── Hero ───────────────────────────────────────────────────────────────────────
  it('greets the student by first name and summarises the pending tasks (FR-STU-SES-001)', async () => {
    setup(
      makePlan({
        focus: makeFocus(),
        kpis: { ...ACTIVE_KPIS },
        totalSteps: 6,
        completedSteps: 1,
        overdueSteps: 1,
        steps: [
          makeStep({ key: 'a' }),
          makeStep({ key: 'b' }),
          makeStep({ key: 'c' }),
          makeStep({ key: 'd' }),
          makeStep({ key: 'e' }),
        ],
      }),
      'Lina',
    );
    await fixture.whenStable();

    expect(text()).toContain('Lina');
    // Hero count is the number of *pending* tasks on the list (not totalSteps).
    expect(text()).toContain('You have 5 tasks on your list');
    expect(text()).toContain('1 overdue');
    expect(text()).toContain('knock them out');
  });

  it('omits the "overdue" clause when overdueSteps is 0', async () => {
    setup(
      makePlan({
        focus: makeFocus(),
        kpis: { ...ACTIVE_KPIS, completedSessions: 0 },
        totalSteps: 3,
        overdueSteps: 0,
        steps: [makeStep()],
      }),
    );
    await fixture.whenStable();
    expect(text()).toContain('You have 1 task on your list');
    expect(text()).not.toContain('overdue');
  });

  // ── KPI widgets ───────────────────────────────────────────────────────────────
  it('renders the four KPI widgets with values and captions in order', async () => {
    setup(makePlan({ focus: makeFocus(), totalSteps: 1, steps: [makeStep()], kpis: { ...ACTIVE_KPIS } }));
    await fixture.whenStable();

    const cards = kpis();
    expect(cards).toHaveLength(4);
    expect(cards[0].textContent).toContain('Active sessions');
    expect(cards[0].textContent).toContain('2');
    expect(cards[0].textContent).toContain('In progress now');
    expect(cards[1].textContent).toContain('Videos watched');
    expect(cards[1].textContent).toContain('12 / 24');
    expect(cards[1].textContent).toContain('Across all sessions');
    expect(cards[2].textContent).toContain('Overall progress');
    expect(cards[2].textContent).toContain('51%');
    expect(cards[2].textContent).toContain('Average completion');
    expect(cards[3].textContent).toContain('Completed');
    expect(cards[3].textContent).toContain('1');
    expect(cards[3].textContent).toContain('Finished sessions');
  });

  // ── Tasks list ────────────────────────────────────────────────────────────────
  it('renders pending tasks in the list and keeps a visible "Completed · N" sub-list', async () => {
    setup(
      makePlan({
        focus: makeFocus(),
        kpis: { ...ACTIVE_KPIS },
        totalSteps: 2,
        completedSteps: 1,
        steps: [
          makeStep({ key: 'videos:s1', status: 'Pending' }),
          makeStep({ key: 'assignment:a1', kind: 'Assignment', status: 'Completed', title: 'Finish your assignment' }),
        ],
      }),
    );
    await fixture.whenStable();

    // One pending + one completed row, both rendered (no disclosure collapse).
    expect(rows()).toHaveLength(2);
    expect(text()).toContain('Completed · 1');
  });

  it('renders a read-only completion tick — NEVER a togglable checkbox (contract §0)', async () => {
    setup(
      makePlan({
        focus: makeFocus(),
        kpis: { ...ACTIVE_KPIS },
        totalSteps: 1,
        completedSteps: 1,
        steps: [makeStep({ status: 'Completed' })],
      }),
    );
    await fixture.whenStable();

    expect(root().querySelector('[role="img"][aria-label="Completed"]')).toBeTruthy();
    expect(root().querySelectorAll('input[type="checkbox"]')).toHaveLength(0);
  });

  it('shows a per-kind type pill + specialization for a task row', async () => {
    setup(makePlan({ focus: makeFocus(), kpis: { ...ACTIVE_KPIS }, totalSteps: 1, steps: [makeStep()] }));
    await fixture.whenStable();

    const row = rows()[0];
    expect(row.querySelector('.row__type')?.textContent?.trim()).toBe('Video');
    expect(row.querySelector('.row__spec')?.textContent).toContain('Pure Maths');
  });

  it('renders a blocked step disabled with its blockedReason and an inert CTA', async () => {
    setup(
      makePlan({
        focus: makeFocus(),
        kpis: { ...ACTIVE_KPIS },
        totalSteps: 1,
        steps: [
          makeStep({
            key: 'videos:s1',
            kind: 'Videos',
            blocked: true,
            blockedReason: 'Pass the quiz to unlock the videos',
            action: { type: 'Navigate', route: '/sessions/s1', label: 'Watch' },
          }),
        ],
      }),
    );
    await fixture.whenStable();

    expect(text()).toContain('Pass the quiz to unlock the videos');
    const cta = root().querySelector<HTMLButtonElement>('button[aria-disabled="true"]');
    expect(cta).toBeTruthy();
    expect(cta?.disabled).toBe(true);
    expect(cta?.textContent?.trim()).toBe('Watch');
    const describedBy = cta?.getAttribute('aria-describedby');
    expect(describedBy).toBeTruthy();
    expect(root().querySelector(`#${describedBy}`)?.textContent).toContain('Pass the quiz');
  });

  it('shows the expiry badge from real enrollment expiry — "Due in Nd" / "Overdue" (contract §E.3)', async () => {
    setup(
      makePlan({
        focus: makeFocus(),
        kpis: { ...ACTIVE_KPIS },
        totalSteps: 2,
        overdueSteps: 1,
        steps: [
          makeStep({ key: 'videos:s1', dueState: 'ExpiringSoon', expiresAtUtc: daysOut(3) }),
          makeStep({ key: 'assignment:a1', kind: 'Assignment', dueState: 'Expired', expiresAtUtc: daysAgo(2) }),
        ],
      }),
    );
    await fixture.whenStable();

    expect(text()).toContain('Due in 3d');
    expect(text()).toContain('Overdue');
  });

  it('renders the per-step {done}/{total} progress label + bar, and none for Quiz/Redeem (FR-STU-SES-001)', async () => {
    setup(
      makePlan({
        focus: makeFocus(),
        kpis: { ...ACTIVE_KPIS },
        totalSteps: 2,
        steps: [
          makeStep({ key: 'videos:s1', progress: { done: 2, total: 6 } }),
          makeStep({
            key: 'quiz:q2',
            kind: 'Quiz',
            title: 'Pass the gating quiz',
            progress: null,
            action: { type: 'Navigate', route: '/sessions/s1', label: 'Start' },
          }),
        ],
      }),
    );
    await fixture.whenStable();

    const videoRow = rows()[0];
    expect(videoRow.querySelector('.row__progress-label')?.textContent?.trim()).toBe('2/6');
    expect((videoRow.querySelector('.row__progress-fill') as HTMLElement).style.width).toBe('33%');
    expect(rows()[1].querySelector('.row__progress')).toBeNull();
  });

  // ── Per-kind CTA ─────────────────────────────────────────────────────────────────
  it('routes a Navigate step to action.route with its server-supplied label', async () => {
    setup(
      makePlan({
        focus: makeFocus(),
        kpis: { ...ACTIVE_KPIS },
        totalSteps: 1,
        steps: [
          makeStep({
            key: 'videos:abc',
            action: { type: 'Navigate', route: '/sessions/abc', label: 'Continue' },
          }),
        ],
      }),
    );
    await fixture.whenStable();

    const cta = ctaByText('Continue');
    expect(cta).toBeTruthy();
    expect(cta?.getAttribute('href')).toContain('/sessions/abc');
  });

  it('routes a Redeem task (e.g. expired "Renew access") to /redeem (code-only — FR-STU-CAT-003)', async () => {
    setup(
      makePlan({
        focus: makeFocus(),
        kpis: { ...ACTIVE_KPIS },
        totalSteps: 1,
        overdueSteps: 1,
        steps: [
          makeStep({
            key: 'redeem:s2',
            kind: 'Redeem',
            title: 'Renew access',
            subtitle: 'Get a new code from your teacher',
            dueState: 'Expired',
            expiresAtUtc: daysAgo(2),
            progress: null,
            action: { type: 'Redeem', route: null, label: 'Renew' },
          }),
        ],
      }),
    );
    await fixture.whenStable();

    const cta = ctaByText('Renew');
    expect(cta).toBeTruthy();
    expect(cta?.getAttribute('href')).toContain('/redeem');
  });

  // ── This week ──────────────────────────────────────────────────────────────────
  it('renders the "This week" bar at round(100 × completedSteps / totalSteps)', async () => {
    setup(
      makePlan({
        focus: makeFocus(),
        kpis: { ...ACTIVE_KPIS },
        totalSteps: 4,
        completedSteps: 1,
        steps: [makeStep()],
      }),
    );
    await fixture.whenStable();

    const bar = root().querySelector('[role="progressbar"]');
    expect(bar?.getAttribute('aria-valuenow')).toBe('25');
    expect(text()).toContain('1 of 4 done tasks');
    expect(text()).toContain('keep it rolling');
  });

  // ── Recently enrolled ─────────────────────────────────────────────────────────────
  it('renders the recently-enrolled list with "Added N days ago" and an "All" link', async () => {
    const recent: MyPlanRecent[] = [
      { sessionId: 'r1', title: 'Geometry', specializationName: 'Pure Maths', enrolledAtUtc: daysAgo(2) },
      { sessionId: 'r2', title: 'Trigonometry', specializationName: 'Pure Maths', enrolledAtUtc: daysAgo(5) },
    ];
    setup(makePlan({ focus: makeFocus(), kpis: { ...ACTIVE_KPIS }, totalSteps: 1, steps: [makeStep()], recentlyEnrolled: recent }));
    await fixture.whenStable();

    expect(tiles()).toHaveLength(2);
    expect(text()).toContain('Recently enrolled');
    expect(text()).toContain('Added 2 days ago');
    expect(text()).toContain('Added 5 days ago');
    expect(text()).toContain('Geometry');

    const all = anchorByText('All →') ?? anchors().find((a) => (a.textContent ?? '').includes('All'));
    expect(all?.getAttribute('href')).toContain('/sessions');
  });

  it('hides the recently-enrolled card when the array is empty', async () => {
    setup(makePlan({ focus: makeFocus(), kpis: { ...ACTIVE_KPIS }, totalSteps: 1, steps: [makeStep()], recentlyEnrolled: [] }));
    await fixture.whenStable();

    expect(tiles()).toHaveLength(0);
    expect(text()).not.toContain('Recently enrolled');
  });

  // ── All caught up ───────────────────────────────────────────────────────────────
  it('shows the all-caught-up state (message + completed list) when there are no pending tasks but history exists', async () => {
    setup(
      makePlan({
        focus: null,
        kpis: { ...ZERO_KPIS, completedSessions: 1 },
        totalSteps: 2,
        completedSteps: 2,
        steps: [
          makeStep({ key: 'v', status: 'Completed' }),
          makeStep({ key: 'a', kind: 'Assignment', status: 'Completed', title: 'Finish your assignment' }),
        ],
      }),
    );
    await fixture.whenStable();

    expect(text().toLowerCase()).toContain('caught up');
    expect(text()).toContain('Completed · 2');
    // Full dashboard still renders (KPIs + This week), and the relaxing mascot is used.
    expect(kpis()).toHaveLength(4);
    expect(root().querySelector('img')?.getAttribute('src')).toContain('salah-relaxing');
  });

  // ── Onboarding / empty plan ────────────────────────────────────────────────────────
  it('shows the bare onboarding hero (mascot + Browse catalogue, no widgets/rows) when there is no history', async () => {
    setup(makePlan({ focus: null, steps: [] }));
    await fixture.whenStable();

    const browse = anchorByText('Browse catalogue');
    expect(browse).toBeTruthy();
    expect(browse?.getAttribute('href')).toContain('/catalogue');
    expect(root().querySelector('img')?.getAttribute('src')).toContain('salah-mascot');
    // No dashboard chrome in the onboarding state.
    expect(rows()).toHaveLength(0);
    expect(kpis()).toHaveLength(0);
  });

  it('treats a single onboarding Redeem step as the bare onboarding state (contract §E.4)', async () => {
    setup(
      makePlan({
        focus: null,
        totalSteps: 1,
        steps: [
          makeStep({
            key: 'redeem:onboard',
            kind: 'Redeem',
            title: 'Redeem a code',
            subtitle: 'Unlock your first session',
            dueState: 'None',
            progress: null,
            action: { type: 'Redeem', route: null, label: 'Redeem' },
          }),
        ],
      }),
    );
    await fixture.whenStable();

    // The no-deadline Redeem is folded into the hero button, not rendered as a task row.
    expect(rows()).toHaveLength(0);
    expect(kpis()).toHaveLength(0);
    expect(anchorByText('Browse catalogue')).toBeTruthy();
  });
});
