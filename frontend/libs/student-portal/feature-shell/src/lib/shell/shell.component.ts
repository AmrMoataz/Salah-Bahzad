import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { TopbarComponent } from '../topbar/topbar.component';
import { BottomNavComponent } from '../bottom-nav/bottom-nav.component';
import { NavItem } from '../nav-item.model';

/** Inline stroke-style icon (24×24 grid) matching the Student Portal prototype. */
const svg = (inner: string): string =>
  `<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">${inner}</svg>`;

const ICON = {
  home: svg('<path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/>'),
  catalogue: svg('<circle cx="12" cy="12" r="10"/><polygon points="16.24 7.76 14.12 14.12 7.76 16.24 9.88 9.88 16.24 7.76"/>'),
  sessions: svg('<rect x="2" y="7" width="20" height="14" rx="2"/><polyline points="17 2 12 7 7 2"/>'),
  profile: svg('<circle cx="12" cy="8" r="4"/><path d="M4 21v-1a6 6 0 0 1 6-6h4a6 6 0 0 1 6 6v1"/>'),
};

// S0 gates everything but Home off (the placeholder home). Catalogue + My Sessions are live (S2/S3);
// Profile stays disabled ("Soon") until S6. Phases flip each item's `disabled` off as they land.
const NAV_ITEMS: NavItem[] = [
  { label: 'Home', route: '/', exact: true, icon: ICON.home },
  { label: 'Catalogue', route: '/catalogue', icon: ICON.catalogue },
  { label: 'My Sessions', route: '/sessions', icon: ICON.sessions },
  { label: 'Profile', route: '/profile', icon: ICON.profile, disabled: true },
];

// The mobile bottom-nav: two items, the centre Redeem FAB, then two items.
const BOTTOM_ITEMS: NavItem[] = [
  { label: 'Home', route: '/', exact: true, icon: ICON.home },
  { label: 'Catalogue', route: '/catalogue', icon: ICON.catalogue },
  { label: 'Sessions', route: '/sessions', icon: ICON.sessions },
  { label: 'Profile', route: '/profile', icon: ICON.profile, disabled: true },
];

/** Route segment → [crumb, pageTitle] shown in the header (prototype's `titles` map). */
const ROUTE_META: Record<string, readonly [string, string]> = {
  '': ['Welcome', 'Home'],
  redeem: ['Enroll', 'Redeem a code'],
  catalogue: ['Discover', 'Catalogue'],
  sessions: ['Learn', 'My Sessions'],
  profile: ['Account', 'Profile'],
};

@Component({
  selector: 'sb-student-shell',
  standalone: true,
  imports: [RouterOutlet, SidebarComponent, TopbarComponent, BottomNavComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(window:resize)': 'onResize()' },
  template: `
    <div class="shell">
      <sb-student-sidebar
        [navItems]="navItems"
        [open]="drawerOpen()"
        [collapsed]="isTablet()"
        [drawer]="isMobile()"
        (navigate)="closeDrawer()"
        (redeem)="goRedeem()"
      />

      @if (isMobile() && drawerOpen()) {
        <div class="shell__scrim" (click)="closeDrawer()"></div>
      }

      <div class="shell__main">
        <sb-student-topbar
          [crumb]="crumb()"
          [pageTitle]="pageTitle()"
          [showBurger]="isMobile()"
          (menuToggle)="toggleDrawer()"
        />

        <main class="shell__content" [class.shell__content--mobile]="isMobile()" id="main-content" tabindex="-1">
          <div class="shell__content-inner">
            <router-outlet />
          </div>
        </main>

        @if (isMobile()) {
          <sb-student-bottom-nav [items]="bottomItems" (navigate)="closeDrawer()" (redeem)="goRedeem()" />
        }
      </div>
    </div>
  `,
  styles: [`
    .shell {
      display: flex;
      min-height: 100dvh;
      background: var(--sb-bg);
    }

    .shell__main {
      flex: 1;
      min-width: 0;
      display: flex;
      flex-direction: column;
    }

    .shell__content { flex: 1; }

    .shell__content-inner {
      max-width: 1080px;
      margin: 0 auto;
      padding: var(--sb-space-7) var(--sb-space-7) var(--sb-space-16);
    }

    .shell__content--mobile .shell__content-inner {
      padding: var(--sb-space-5) var(--sb-space-4) 100px;
    }

    .shell__scrim {
      position: fixed;
      inset: 0;
      background: var(--sb-scrim);
      z-index: calc(var(--sb-z-overlay) - 1);
      animation: sb-scrim-in var(--sb-timing-fast) var(--sb-easing-out);
    }

    @keyframes sb-scrim-in { from { opacity: 0; } to { opacity: 1; } }
  `],
})
export class ShellComponent {
  readonly #router = inject(Router);

  readonly navItems = NAV_ITEMS;
  readonly bottomItems = BOTTOM_ITEMS;

  readonly drawerOpen = signal(false);

  readonly #width = signal(typeof window !== 'undefined' ? window.innerWidth : 1280);
  /** Breakpoints mirror the prototype: mobile < 760, tablet 760–1080 (icon rail), desktop ≥ 1080. */
  readonly isMobile = computed(() => this.#width() < 760);
  readonly isTablet = computed(() => this.#width() >= 760 && this.#width() < 1080);
  readonly isDesktop = computed(() => this.#width() >= 1080);

  readonly #currentUrl = toSignal(
    this.#router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects),
    ),
    { initialValue: this.#router.url },
  );

  readonly crumb = computed(() => this.metaFor(this.#currentUrl())[0]);
  readonly pageTitle = computed(() => this.metaFor(this.#currentUrl())[1]);

  constructor() {
    // The drawer is a mobile-only affordance — never leave it "open" after growing past mobile.
    effect(() => {
      if (!this.isMobile() && this.drawerOpen()) this.drawerOpen.set(false);
    });
  }

  /** Pure crumb/title lookup from the route map (segment 0 of the URL). */
  metaFor(url: string): readonly [string, string] {
    const segment = url.split('?')[0].split('/').filter(Boolean)[0] ?? '';
    return ROUTE_META[segment] ?? ['', 'Salah Bahzad'];
  }

  onResize(): void {
    this.#width.set(window.innerWidth);
  }

  toggleDrawer(): void {
    this.drawerOpen.update((open) => !open);
  }

  closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  goRedeem(): void {
    this.closeDrawer();
    void this.#router.navigate(['/redeem']);
  }
}
