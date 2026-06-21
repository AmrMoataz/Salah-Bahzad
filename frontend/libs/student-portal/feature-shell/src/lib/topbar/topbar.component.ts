import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { StudentAuthStore } from '@sb/student-portal/data-access';

/**
 * Sticky header: mobile burger + route crumb/title, a (visual-only) notifications bell, and a user
 * chip whose menu signs out. Mirrors the prototype's `APP` → Main column header.
 */
@Component({
  selector: 'sb-student-topbar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:click)': 'closeMenu()' },
  template: `
    <header class="topbar">
      @if (showBurger()) {
        <button class="topbar__burger" type="button" (click)="menuToggle.emit()" aria-label="Open navigation">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="2.2" stroke-linecap="round" aria-hidden="true">
            <path d="M3 6h18M3 12h18M3 18h18" />
          </svg>
        </button>
      }

      <div class="topbar__heading">
        <p class="topbar__crumb">{{ crumb() }}</p>
        <h1 class="topbar__title">{{ pageTitle() }}</h1>
      </div>

      <button class="topbar__bell" type="button" aria-label="Notifications">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <path d="M18 8a6 6 0 0 0-12 0c0 7-3 9-3 9h18s-3-2-3-9" />
          <path d="M13.7 21a2 2 0 0 1-3.4 0" />
        </svg>
        <span class="topbar__bell-dot" aria-hidden="true"></span>
      </button>

      <div class="topbar__user">
        <button
          class="topbar__user-btn"
          type="button"
          (click)="toggleMenu($event)"
          [attr.aria-expanded]="menuOpen()"
          aria-haspopup="menu"
        >
          <span class="topbar__user-info">
            <span class="topbar__user-name">{{ fullName() }}</span>
          </span>
          <span class="topbar__avatar" aria-hidden="true">
            <span class="topbar__avatar-circle">{{ initials() }}</span>
            <span class="topbar__avatar-dot"></span>
          </span>
        </button>

        @if (menuOpen()) {
          <div class="topbar__menu" role="menu">
            <div class="topbar__menu-head">
              <span class="topbar__menu-name">{{ fullName() }}</span>
            </div>
            <div class="topbar__menu-divider"></div>
            <button
              class="topbar__menu-item topbar__menu-item--danger"
              role="menuitem"
              type="button"
              (click)="signOut()"
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                   stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9" />
              </svg>
              <span>Sign out</span>
            </button>
          </div>
        }
      </div>
    </header>
  `,
  styles: [`
    .topbar {
      position: sticky;
      top: 0;
      z-index: var(--sb-z-sticky);
      display: flex;
      align-items: center;
      gap: var(--sb-space-3);
      padding: 14px 26px;
      background: rgba(251, 251, 247, 0.88);
      backdrop-filter: blur(8px);
      border-bottom: 1px solid var(--sb-border);
    }

    .topbar__burger {
      width: 42px;
      height: 42px;
      flex-shrink: 0;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      border: 1px solid var(--sb-border);
      border-radius: 11px;
      background: var(--sb-surface);
      color: var(--sb-text);
      cursor: pointer;
    }
    .topbar__burger:hover { background: var(--sb-surface-sunken); }
    .topbar__burger:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }

    .topbar__heading { min-width: 0; flex: 1; }
    .topbar__crumb {
      font-size: 12px;
      color: var(--sb-text-muted);
      font-weight: 700;
      letter-spacing: 0.3px;
      text-transform: uppercase;
    }
    .topbar__title {
      font-weight: 800;
      font-size: 18px;
      letter-spacing: -0.3px;
      color: var(--sb-text);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .topbar__bell {
      position: relative;
      width: 42px;
      height: 42px;
      flex-shrink: 0;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      border: 1px solid var(--sb-border);
      border-radius: 11px;
      background: var(--sb-surface);
      color: var(--sb-neutral-600);
      cursor: pointer;
    }
    .topbar__bell:hover { background: var(--sb-surface-sunken); }
    .topbar__bell:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }
    .topbar__bell-dot {
      position: absolute;
      top: 8px;
      right: 9px;
      width: 8px;
      height: 8px;
      border-radius: var(--sb-radius-circle);
      background: var(--sb-danger);
      border: 2px solid var(--sb-surface);
    }

    .topbar__user { position: relative; flex-shrink: 0; }
    .topbar__user-btn {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 4px 5px 4px 14px;
      border: 1px solid var(--sb-border);
      background: var(--sb-surface);
      border-radius: 30px;
      cursor: pointer;
    }
    .topbar__user-btn:hover { background: var(--sb-surface-sunken); }
    .topbar__user-btn:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }

    .topbar__user-info { display: flex; flex-direction: column; line-height: 1.1; text-align: right; }
    .topbar__user-name { font-size: 14px; font-weight: 700; color: var(--sb-text); }

    /* Avatar matches the design-system Avatar: circle, subject-blue tint + deep initials, 1px
       border, and a green "approved" status dot (the chip only renders for an Active student). */
    .topbar__avatar { position: relative; display: inline-flex; flex-shrink: 0; }
    .topbar__avatar-circle {
      width: 40px;
      height: 40px;
      border-radius: var(--sb-radius-circle);
      border: 1px solid var(--sb-border);
      background: var(--sb-subject-blue-bg);
      color: var(--sb-subject-blue-deep);
      display: inline-flex;
      align-items: center;
      justify-content: center;
      font-weight: 800;
      font-size: 16px;
      text-transform: uppercase;
    }
    .topbar__avatar-dot {
      position: absolute;
      right: -1px;
      bottom: -1px;
      width: 11px;
      height: 11px;
      border-radius: var(--sb-radius-circle);
      background: var(--sb-success);
      border: 2px solid var(--sb-surface);
    }

    .topbar__menu {
      position: absolute;
      top: calc(100% + 8px);
      right: 0;
      min-width: 210px;
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-md);
      box-shadow: var(--sb-shadow-lg);
      padding: 6px;
      z-index: var(--sb-z-dropdown);
    }
    .topbar__menu-head { padding: 8px 12px; }
    .topbar__menu-name { font-size: 13.5px; font-weight: 700; color: var(--sb-text); }
    .topbar__menu-divider { height: 1px; background: var(--sb-border); margin: 4px 0; }

    .topbar__menu-item {
      display: flex;
      align-items: center;
      gap: var(--sb-space-3);
      width: 100%;
      padding: 9px 12px;
      border: none;
      background: none;
      border-radius: var(--sb-radius-sm);
      cursor: pointer;
      font-size: var(--sb-body-md-size);
      font-weight: 600;
      color: var(--sb-text);
      text-align: left;
    }
    .topbar__menu-item:hover { background: var(--sb-surface-sunken); }
    .topbar__menu-item:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }
    .topbar__menu-item--danger { color: var(--sb-danger-fg); }

    @media (max-width: 759px) {
      .topbar { padding: 12px 16px; }
      .topbar__user-info { display: none; }
    }
  `],
})
export class TopbarComponent {
  readonly #authStore = inject(StudentAuthStore);

  readonly crumb = input<string>('');
  readonly pageTitle = input<string>('');
  /** Show the mobile burger (the shell passes `isMobile`). */
  readonly showBurger = input<boolean>(false);
  /** Emitted when the mobile burger is pressed. */
  readonly menuToggle = output<void>();

  readonly fullName = this.#authStore.fullName;
  readonly menuOpen = signal(false);

  readonly initials = computed(() =>
    this.fullName()
      .split(' ')
      .map((part) => part[0])
      .filter(Boolean)
      .slice(0, 2)
      .join('')
      .toUpperCase(),
  );

  toggleMenu(event: MouseEvent): void {
    event.stopPropagation();
    this.menuOpen.update((open) => !open);
  }

  closeMenu(): void {
    if (this.menuOpen()) this.menuOpen.set(false);
  }

  async signOut(): Promise<void> {
    this.menuOpen.set(false);
    await this.#authStore.signOut();
  }
}
