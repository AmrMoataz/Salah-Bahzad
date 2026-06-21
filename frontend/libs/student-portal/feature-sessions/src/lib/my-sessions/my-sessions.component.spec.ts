import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

// The data-access barrel imports @angular/fire (ESM) — replace it with a token-only double.
jest.mock('@sb/student-portal/data-access', () => ({
  MySessionsService: class MySessionsService {},
}));

import { MySessionsComponent } from './my-sessions.component';
import { MySessionsService, MySession } from '@sb/student-portal/data-access';

const DAY = 86_400_000;

function makeSession(over: Partial<MySession> = {}): MySession {
  return {
    id: 's1',
    enrollmentId: 'e1',
    title: 'Algebra Basics',
    gradeName: 'Grade 1',
    subjectName: 'Maths',
    specializationName: 'Pure Maths',
    thumbnailUrl: null,
    videoCount: 4,
    videosWatched: 2,
    progressPercent: 50,
    enrolledAtUtc: '2026-06-01T00:00:00Z',
    expiresAtUtc: null,
    isExpired: false,
    state: 'InProgress',
    ...over,
  };
}

describe('MySessionsComponent (FR-STU-SES-001)', () => {
  let fixture: ComponentFixture<MySessionsComponent>;
  let component: MySessionsComponent;
  let service: { mySessions: jest.Mock };

  function setup(sessions: MySession[]) {
    service = { mySessions: jest.fn().mockReturnValue(of(sessions)) };
    TestBed.configureTestingModule({
      imports: [MySessionsComponent],
      providers: [provideRouter([]), { provide: MySessionsService, useValue: service }],
    });
    fixture = TestBed.createComponent(MySessionsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    return fixture;
  }

  const root = () => fixture.nativeElement as HTMLElement;
  const text = () => root().textContent ?? '';
  const listRows = (): HTMLElement[] =>
    Array.from(root().querySelectorAll<HTMLElement>('sb-session-list-row'));
  const hero = () => root().querySelector<HTMLElement>('.ms__hero');

  it('renders every enrolled session (spotlight hero + divided list)', async () => {
    setup([
      makeSession({ id: 's1', title: 'Algebra Basics', progressPercent: 80 }),
      makeSession({ id: 's2', title: 'Geometry', progressPercent: 20 }),
      makeSession({ id: 's3', title: 'Trigonometry', progressPercent: 10 }),
    ]);
    await fixture.whenStable();

    // The highest-progress active session is the hero; the other two are list rows.
    expect(hero()).toBeTruthy();
    expect(listRows()).toHaveLength(2);
    expect(text()).toContain('Algebra Basics');
    expect(text()).toContain('Geometry');
    expect(text()).toContain('Trigonometry');
  });

  it('shows the mascot empty state when there are no sessions', async () => {
    setup([]);
    await fixture.whenStable();

    expect(listRows()).toHaveLength(0);
    expect(hero()).toBeNull();
    const empty = root().querySelector('.ms__empty');
    expect(empty).toBeTruthy();
    expect(empty?.querySelector('img')?.getAttribute('src')).toContain('salah-relaxing');
    expect(text()).toContain('No sessions yet');
    // A "Browse the catalogue" link points back to /catalogue.
    expect(root().querySelector('a[href="/catalogue"]')).toBeTruthy();
  });

  it('spotlight picks the highest-progressPercent active session and shows "Continue session →"', async () => {
    setup([
      makeSession({ id: 'a', title: 'Low Progress', progressPercent: 30 }),
      makeSession({ id: 'b', title: 'High Progress', progressPercent: 85 }),
    ]);
    await fixture.whenStable();

    expect(component.spotlight()?.id).toBe('b');
    expect(hero()?.textContent).toContain('High Progress');
    expect(hero()?.textContent).toContain('Continue session →');
    // The other active session falls into the list.
    expect(listRows()).toHaveLength(1);
    expect(listRows()[0].textContent).toContain('Low Progress');
  });

  it('never features a Completed session in the spotlight (Jump back in is for unfinished sessions)', async () => {
    setup([
      // The completed session has the highest progress (100%) but must NOT be the hero.
      makeSession({ id: 'done', title: 'All Done', state: 'Completed', progressPercent: 100 }),
      makeSession({ id: 'wip', title: 'Halfway', state: 'InProgress', progressPercent: 40 }),
    ]);
    await fixture.whenStable();

    expect(component.spotlight()?.id).toBe('wip');
    expect(hero()?.textContent).toContain('Halfway');
    expect(hero()?.textContent).not.toContain('All Done');
    // The completed session drops into the divided list (with its Review CTA).
    expect(listRows().some((r) => r.textContent?.includes('All Done'))).toBe(true);
  });

  it('shows no spotlight hero when every active session is already completed', async () => {
    setup([
      makeSession({ id: 'c1', title: 'Done One', state: 'Completed', progressPercent: 100 }),
      makeSession({ id: 'c2', title: 'Done Two', state: 'Completed', progressPercent: 100 }),
    ]);
    await fixture.whenStable();

    expect(hero()).toBeNull();
    expect(listRows()).toHaveLength(2);
  });

  it('filter chip-bar — "Expiring soon" narrows to non-expired sessions in the expiring-soon window', async () => {
    setup([
      makeSession({ id: 'soon', title: 'Soon One', expiresAtUtc: new Date(Date.now() + 12 * 60 * 60 * 1000).toISOString() }),
      makeSession({ id: 'far', title: 'Far Two', expiresAtUtc: new Date(Date.now() + 60 * DAY).toISOString() }),
      makeSession({ id: 'exp', title: 'Expired Three', isExpired: true, expiresAtUtc: new Date(Date.now() - DAY).toISOString() }),
    ]);
    await fixture.whenStable();

    component.setFilter('ExpiringSoon');
    fixture.detectChanges();

    expect(text()).toContain('Soon One');
    expect(text()).not.toContain('Far Two');
    expect(text()).not.toContain('Expired Three');
  });

  it('filter chip-bar — "Expired" narrows to isExpired sessions, which show an Expired label', async () => {
    setup([
      makeSession({ id: 'active', title: 'Active One' }),
      makeSession({ id: 'exp', title: 'Expired Three', isExpired: true, expiresAtUtc: new Date(Date.now() - DAY).toISOString() }),
    ]);
    await fixture.whenStable();

    component.setFilter('Expired');
    fixture.detectChanges();

    expect(text()).toContain('Expired Three');
    expect(text()).not.toContain('Active One');
    // The expired row surfaces the "Expired" expiry label in its meta line.
    const row = listRows().find((r) => r.textContent?.includes('Expired Three'));
    expect(row?.querySelector('.lrow__sub')?.textContent).toContain('Expired');
  });

  it('list-row CTA label is Start / Continue / Review by completion state', async () => {
    // All expired → none becomes the spotlight, so all three render as list rows.
    const past = new Date(Date.now() - DAY).toISOString();
    setup([
      makeSession({ id: 'n', title: 'NotStarted S', state: 'NotStarted', isExpired: true, expiresAtUtc: past }),
      makeSession({ id: 'i', title: 'InProgress S', state: 'InProgress', isExpired: true, expiresAtUtc: past }),
      makeSession({ id: 'c', title: 'Completed S', state: 'Completed', isExpired: true, expiresAtUtc: past }),
    ]);
    await fixture.whenStable();

    expect(hero()).toBeNull();
    const labels = listRows().map((r) => r.querySelector('button')?.textContent?.trim());
    expect(labels).toEqual(expect.arrayContaining(['Start', 'Continue', 'Review']));
  });

  it('renders summary counts (Enrolled / Active / Completed) from the full set', async () => {
    setup([
      makeSession({ id: '1', state: 'Completed', progressPercent: 100 }),
      makeSession({ id: '2', state: 'InProgress', isExpired: true, expiresAtUtc: new Date(Date.now() - DAY).toISOString() }),
      makeSession({ id: '3', state: 'NotStarted', progressPercent: 0, videosWatched: 0 }),
    ]);
    await fixture.whenStable();

    expect(component.activeCount()).toBe(2); // two are not expired
    expect(component.completedCount()).toBe(1);
    const counts = root().querySelector('.ms__counts');
    expect(counts?.textContent).toContain('Enrolled');
    expect(counts?.textContent).toContain('Active');
    expect(counts?.textContent).toContain('Completed');
  });
});
