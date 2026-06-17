import {
  ChangeDetectionStrategy,
  Component,
  signal,
} from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { TopbarComponent } from '../topbar/topbar.component';
import { NavItem } from '../nav-item.model';

const NAV_ITEMS: NavItem[] = [
  {
    label: 'Dashboard',
    route: '/dashboard',
    icon: `<svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor">
      <path d="M2 11a1 1 0 0 1 1-1h3a1 1 0 0 1 1 1v6a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1v-6zm6-6a1 1 0 0 1 1-1h3a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H9a1 1 0 0 1-1-1V5zm6 3a1 1 0 0 1 1-1h3a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1h-3a1 1 0 0 1-1-1V8z"/>
    </svg>`,
  },
  {
    label: 'Students',
    route: '/students',
    icon: `<svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor">
      <path d="M9 6a3 3 0 1 1-6 0 3 3 0 0 1 6 0zM17 6a3 3 0 1 1-6 0 3 3 0 0 1 6 0zM12.93 17c.046-.327.07-.66.07-1a6.97 6.97 0 0 0-1.5-4.33A5 5 0 0 1 19 16v1h-6.07zM6 11a5 5 0 0 1 5 5v1H1v-1a5 5 0 0 1 5-5z"/>
    </svg>`,
  },
  {
    label: 'Sessions',
    route: '/sessions',
    icon: `<svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor">
      <path d="M2 6a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v2a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6zm0 6a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v2a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2v-2z"/>
    </svg>`,
  },
  {
    label: 'Codes',
    route: '/codes',
    icon: `<svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor">
      <path fill-rule="evenodd" d="M17.707 9.293a1 1 0 0 1 0 1.414l-7 7a1 1 0 0 1-1.414 0l-7-7A.997.997 0 0 1 2 10V5a3 3 0 0 1 3-3h5c.256 0 .512.098.707.293l7 7z" clip-rule="evenodd"/>
    </svg>`,
  },
  {
    label: 'Taxonomy',
    route: '/taxonomy',
    icon: `<svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor">
      <path d="M5 3a2 2 0 0 0-2 2v2a2 2 0 0 0 2 2h2a2 2 0 0 0 2-2V5a2 2 0 0 0-2-2H5zm0 8a2 2 0 0 0-2 2v2a2 2 0 0 0 2 2h2a2 2 0 0 0 2-2v-2a2 2 0 0 0-2-2H5zm6-8a2 2 0 0 0-2 2v2a2 2 0 0 0 2 2h2a2 2 0 0 0 2-2V5a2 2 0 0 0-2-2h-2zm0 8a2 2 0 0 0-2 2v2a2 2 0 0 0 2 2h2a2 2 0 0 0 2-2v-2a2 2 0 0 0-2-2h-2z"/>
    </svg>`,
  },
  {
    label: 'Staff',
    route: '/staff',
    icon: `<svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor">
      <path d="M13 6a3 3 0 1 1-6 0 3 3 0 0 1 6 0zM18 8a2 2 0 1 1-4 0 2 2 0 0 1 4 0zM14 15a4 4 0 0 0-8 0v3h8v-3zM6 8a2 2 0 1 1-4 0 2 2 0 0 1 4 0zM16 18v-3a5.972 5.972 0 0 0-.75-2.906A3.005 3.005 0 0 1 19 15v3h-3zM4.75 12.094A5.973 5.973 0 0 0 4 15v3H1v-3a3 3 0 0 1 3.75-2.906z"/>
    </svg>`,
  },
  {
    label: 'Attendance',
    route: '/attendance',
    icon: `<svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor">
      <path fill-rule="evenodd" d="M6 2a1 1 0 0 0-1 1v1H4a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2h-1V3a1 1 0 1 0-2 0v1H7V3a1 1 0 0 0-1-1zm0 5a1 1 0 0 0 0 2h8a1 1 0 1 0 0-2H6z" clip-rule="evenodd"/>
    </svg>`,
  },
  {
    label: 'Activity Log',
    route: '/activity',
    icon: `<svg width="20" height="20" viewBox="0 0 20 20" fill="currentColor">
      <path fill-rule="evenodd" d="M3 4a1 1 0 0 1 1-1h12a1 1 0 1 1 0 2H4a1 1 0 0 1-1-1zm0 4a1 1 0 0 1 1-1h12a1 1 0 1 1 0 2H4a1 1 0 0 1-1-1zm0 4a1 1 0 0 1 1-1h12a1 1 0 1 1 0 2H4a1 1 0 0 1-1-1z" clip-rule="evenodd"/>
    </svg>`,
  },
];

@Component({
  selector: 'sb-shell',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, TopbarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="shell">
      <sb-sidebar
        [navItems]="navItems"
        [collapsed]="sidebarCollapsed()"
        (collapsedChange)="sidebarCollapsed.set($event)"
      />
      <div class="shell__main">
        <sb-topbar />
        <main class="shell__content" id="main-content" tabindex="-1">
          <router-outlet />
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
      overflow: hidden;
    }

    .shell__content {
      flex: 1;
      overflow-y: auto;
      padding: var(--sb-space-6);
      background: var(--sb-bg);
    }

    @media (max-width: 1024px) {
      :host ::ng-deep .sidebar {
        position: fixed;
        left: 0;
        top: 0;
        z-index: var(--sb-z-overlay);
        height: 100%;
        transform: translateX(-100%);
        transition: transform var(--sb-dur) var(--sb-ease-standard);

        &.sidebar--open {
          transform: translateX(0);
        }
      }
    }
  `],
})
export class ShellComponent {
  readonly navItems = NAV_ITEMS;
  readonly sidebarCollapsed = signal(false);
}
