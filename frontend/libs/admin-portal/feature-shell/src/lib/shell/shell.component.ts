import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { AuthStore } from '@sb/shared/data-access';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { TopbarComponent } from '../topbar/topbar.component';
import { NavGroup } from '../nav-item.model';

/** Inline stroke-style icon (24×24 grid) matching the Salah Bahzad design prototype. */
const icon = (path: string): string =>
  `<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="${path}"/></svg>`;

const NAV_GROUPS: NavGroup[] = [
  {
    title: 'Operations',
    items: [
      { label: 'Dashboard', route: '/dashboard', icon: icon('M4 4h6v8H4zM14 4h6v5h-6zM14 13h6v7h-6zM4 16h6v4H4z') },
      // Badge wires up to the live pending-approvals count once the approvals/students data source lands.
      { label: 'Approvals', route: '/approvals', icon: icon('M3 12h5l2 3h4l2-3h5M5 5h14l3 7v6a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2v-6z') },
      { label: 'Students', route: '/students', icon: icon('M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75') },
      { label: 'Sessions', route: '/sessions', icon: icon('M4 19.5A2.5 2.5 0 0 1 6.5 17H20M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z') },
      { label: 'Codes', route: '/codes', icon: icon('M3 9a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2 2 2 0 0 0 0 4 2 2 0 0 1-2 2H5a2 2 0 0 1-2-2 2 2 0 0 0 0-4zM9 7v10') },
      { label: 'Attendance', route: '/attendance', icon: icon('M9 3h6v2H9zM8 4H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2h-2M9 12l2 2 4-4') },
    ],
  },
  {
    title: 'Manage',
    items: [
      { label: 'Taxonomy', route: '/taxonomy', icon: icon('M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82zM7 7h.01') },
      { label: 'Staff', route: '/staff', icon: icon('M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z'), teacherOnly: true },
      { label: 'Activity Log', route: '/activity', icon: icon('M22 12h-4l-3 9L9 3l-3 9H2') },
      { label: 'Settings', route: '/settings', icon: icon('M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z') },
    ],
  },
];

/** Route segment → [title, subtitle] shown in the topbar. */
const ROUTE_META: Record<string, readonly [string, string]> = {
  dashboard: ['Dashboard', 'Operational overview'],
  approvals: ['Approvals queue', 'Pending registrations'],
  students: ['Students', 'Learner accounts'],
  sessions: ['Sessions', 'Content catalogue'],
  codes: ['Codes', 'Enrollment code register'],
  attendance: ['Attendance', 'Cross-student progress'],
  taxonomy: ['Taxonomy', 'Reference data'],
  staff: ['Staff', 'Teacher & assistant accounts'],
  activity: ['Activity Log', 'Audit investigation'],
  settings: ['Settings', 'Profile & preferences'],
};

@Component({
  selector: 'sb-shell',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, TopbarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="shell">
      <sb-sidebar
        [navGroups]="navGroups()"
        [open]="mobileNavOpen()"
        (navigate)="mobileNavOpen.set(false)"
      />

      @if (mobileNavOpen()) {
        <div class="shell__scrim" (click)="mobileNavOpen.set(false)"></div>
      }

      <div class="shell__main">
        <sb-topbar
          [title]="pageTitle()"
          [subtitle]="pageSubtitle()"
          (menuToggle)="mobileNavOpen.set(!mobileNavOpen())"
        />
        <main class="shell__content" id="main-content" tabindex="-1">
          <div class="shell__content-inner">
            <router-outlet />
          </div>
        </main>
      </div>
    </div>
  `,
  styles: [`
    .shell {
      display: flex;
      height: 100dvh;
      overflow: hidden;
    }

    .shell__main {
      flex: 1;
      display: flex;
      flex-direction: column;
      min-width: 0;
      overflow: hidden;
    }

    .shell__content {
      flex: 1;
      overflow-y: auto;
      background: var(--sb-bg);
    }

    /* Centered content column (matches the prototype's 1240px max-width). */
    .shell__content-inner {
      max-width: 1240px;
      margin: 0 auto;
      padding: var(--sb-space-7) var(--sb-space-7) var(--sb-space-16);
    }

    .shell__scrim {
      position: fixed;
      inset: 0;
      background: var(--sb-scrim);
      z-index: calc(var(--sb-z-overlay) - 1);
    }

    @media (min-width: 901px) {
      .shell__scrim { display: none; }
    }
  `],
})
export class ShellComponent {
  readonly #auth = inject(AuthStore);
  readonly #router = inject(Router);

  readonly mobileNavOpen = signal(false);

  /** Nav groups with permission- and role-gated items removed (empty groups dropped). */
  readonly navGroups = computed<NavGroup[]>(() =>
    NAV_GROUPS.map((group) => ({
      ...group,
      items: group.items.filter((item) => {
        if (item.permission && !this.#auth.hasPermission(item.permission)) return false;
        if (item.teacherOnly && this.#auth.role() !== 'Teacher') return false;
        return true;
      }),
    })).filter((group) => group.items.length > 0),
  );

  readonly #currentUrl = toSignal(
    this.#router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.#router.url },
  );

  readonly #segment = computed(
    () => this.#currentUrl().split('?')[0].split('/').filter(Boolean)[0] ?? 'dashboard',
  );

  readonly pageTitle = computed(() => ROUTE_META[this.#segment()]?.[0] ?? 'Salah Bahzad');
  readonly pageSubtitle = computed(() => ROUTE_META[this.#segment()]?.[1] ?? '');
}
