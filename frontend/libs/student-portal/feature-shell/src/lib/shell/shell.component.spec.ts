import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { StudentAuthStore } from '@sb/student-portal/data-access';
import { ShellComponent } from './shell.component';

// The data-access barrel pulls in @angular/fire (ESM) — replace it with a token-only double.
jest.mock('@sb/student-portal/data-access', () => ({ StudentAuthStore: class StudentAuthStore {} }));

function setWidth(px: number): void {
  Object.defineProperty(window, 'innerWidth', { configurable: true, writable: true, value: px });
}

function makeStore() {
  return {
    fullName: signal('Lina Hassan'),
    student: signal({ id: 'stu-1', fullName: 'Lina Hassan', status: 'Active', boundDevice: null }),
    signOut: jest.fn().mockResolvedValue(undefined),
  };
}

describe('ShellComponent', () => {
  let store: ReturnType<typeof makeStore>;

  function setup(width: number) {
    setWidth(width);
    store = makeStore();
    TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [provideRouter([]), { provide: StudentAuthStore, useValue: store }],
    });
    const fixture = TestBed.createComponent(ShellComponent);
    fixture.detectChanges();
    return fixture;
  }

  const html = (f: { nativeElement: HTMLElement }) => f.nativeElement;

  afterEach(() => jest.clearAllMocks());

  it('renders the sidebar + Redeem footer and no bottom-nav at desktop width', () => {
    const fixture = setup(1280);
    const el = html(fixture);

    expect(el.querySelector('sb-student-sidebar')).toBeTruthy();
    expect(el.textContent).toContain('Redeem a code');
    expect(el.querySelector('sb-student-bottom-nav')).toBeNull();
    expect(el.querySelector('.shell__scrim')).toBeNull();
  });

  it('collapses the sidebar to an icon rail at tablet width', () => {
    const fixture = setup(900);
    expect(html(fixture).querySelector('.sidebar--rail')).toBeTruthy();
    expect(html(fixture).querySelector('sb-student-bottom-nav')).toBeNull();
  });

  it('shows the bottom-nav with the centre Redeem FAB on mobile', () => {
    const fixture = setup(500);
    const el = html(fixture);

    expect(el.querySelector('sb-student-bottom-nav')).toBeTruthy();
    expect(el.querySelector('[aria-label="Redeem a code"]')).toBeTruthy();
    // The sidebar is now a slide-in drawer.
    expect(el.querySelector('.sidebar--drawer')).toBeTruthy();
  });

  it('toggles the drawer + scrim on mobile and closes on scrim tap', () => {
    const fixture = setup(500);
    const el = html(fixture);

    expect(el.querySelector('.shell__scrim')).toBeNull();
    expect(el.querySelector('.sidebar--open')).toBeNull();

    fixture.componentInstance.toggleDrawer();
    fixture.detectChanges();
    expect(el.querySelector('.shell__scrim')).toBeTruthy();
    expect(el.querySelector('.sidebar--open')).toBeTruthy();

    (el.querySelector('.shell__scrim') as HTMLElement).click();
    fixture.detectChanges();
    expect(el.querySelector('.shell__scrim')).toBeNull();
    expect(el.querySelector('.sidebar--open')).toBeNull();
  });

  it('signs out from the user-chip menu via the store', () => {
    const fixture = setup(1280);
    const el = html(fixture);

    (el.querySelector('.topbar__user-btn') as HTMLElement).click();
    fixture.detectChanges();

    const signOutBtn = el.querySelector('.topbar__menu-item--danger') as HTMLElement;
    expect(signOutBtn).toBeTruthy();
    signOutBtn.click();

    expect(store.signOut).toHaveBeenCalled();
  });

  it('shows the signed-in student name in the header chip', () => {
    const fixture = setup(1280);
    expect(html(fixture).textContent).toContain('Lina Hassan');
  });

  it('derives crumb/title from the route map', () => {
    const fixture = setup(1280);
    const shell = fixture.componentInstance;

    expect(shell.metaFor('/')).toEqual(['Welcome', 'Home']);
    expect(shell.metaFor('/profile')).toEqual(['Account', 'Profile']);
    expect(shell.metaFor('/sessions')).toEqual(['Learn', 'My Sessions']);
    expect(shell.metaFor('/redeem')).toEqual(['Enroll', 'Redeem a code']);
    // initial URL ('/') drives the rendered title
    expect(html(fixture).textContent).toContain('Home');
  });
});
