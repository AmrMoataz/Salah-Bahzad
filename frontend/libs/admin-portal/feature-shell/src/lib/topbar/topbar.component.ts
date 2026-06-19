import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';

@Component({
  selector: 'sb-topbar',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:click)': 'closeMenu()' },
  template: `
    <header class="topbar">
      <button
        class="topbar__burger"
        type="button"
        (click)="menuToggle.emit()"
        aria-label="Open navigation"
      >
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <path d="M3 6h18M3 12h18M3 18h18" />
        </svg>
      </button>

      <div class="topbar__heading">
        <h1 class="topbar__title">{{ title() }}</h1>
        @if (subtitle()) {
          <p class="topbar__subtitle">{{ subtitle() }}</p>
        }
      </div>

      <button class="topbar__bell" type="button" aria-label="Notifications">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <path d="M18 8a6 6 0 1 0-12 0c0 7-3 9-3 9h18s-3-2-3-9M13.73 21a2 2 0 0 1-3.46 0" />
        </svg>
        <span class="topbar__bell-dot" aria-hidden="true"></span>
      </button>

      @if (staff(); as user) {
        <div class="topbar__user">
          <button
            class="topbar__user-btn"
            type="button"
            (click)="toggleMenu($event)"
            [attr.aria-expanded]="menuOpen()"
            aria-haspopup="menu"
          >
            <span class="topbar__avatar">{{ initials() }}</span>
            <span class="topbar__user-info">
              <span class="topbar__user-name">{{ user.displayName }}</span>
              <span class="topbar__user-role">{{ user.role }}</span>
            </span>
            <svg class="topbar__chevron" [class.is-open]="menuOpen()" width="14" height="14"
                 viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                 stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M6 9l6 6 6-6" />
            </svg>
          </button>

          @if (menuOpen()) {
            <div class="topbar__menu" role="menu">
              <a class="topbar__menu-item" role="menuitem" routerLink="/settings" (click)="closeMenu()">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                     stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <path d="M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z" />
                  <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
                </svg>
                <span>Settings</span>
              </a>
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
      }
    </header>
  `,
  styles: [`
    .topbar {
      display: flex;
      align-items: center;
      gap: var(--sb-space-4);
      height: 64px;
      flex-shrink: 0;
      padding: 0 var(--sb-space-5);
      background: var(--sb-surface);
      border-bottom: 1px solid var(--sb-border);
    }

    .topbar__burger {
      display: none;
      width: 40px;
      height: 40px;
      flex-shrink: 0;
      align-items: center;
      justify-content: center;
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-md);
      background: var(--sb-surface);
      color: var(--sb-text);
      cursor: pointer;
    }

    .topbar__burger:hover { background: var(--sb-surface-sunken); }
    .topbar__burger:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }

    .topbar__heading { min-width: 0; flex-shrink: 1; }

    .topbar__title {
      font-size: var(--sb-heading-sm-size);
      font-weight: 800;
      line-height: 1.1;
      color: var(--sb-text);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .topbar__subtitle {
      font-size: 12.5px;
      color: var(--sb-text-muted);
      line-height: 1.3;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .topbar__bell {
      position: relative;
      margin-left: auto;
      width: 40px;
      height: 40px;
      flex-shrink: 0;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-md);
      background: var(--sb-surface);
      color: var(--sb-text);
      cursor: pointer;
    }

    .topbar__bell:hover { background: var(--sb-surface-sunken); }
    .topbar__bell:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }

    .topbar__bell-dot {
      position: absolute;
      top: 8px;
      right: 9px;
      width: 7px;
      height: 7px;
      border-radius: var(--sb-radius-circle);
      background: var(--sb-danger);
      border: 1.5px solid var(--sb-surface);
    }

    .topbar__user { position: relative; flex-shrink: 0; }

    .topbar__user-btn {
      display: flex;
      align-items: center;
      gap: var(--sb-space-3);
      padding: 4px 6px 4px 4px;
      border: none;
      background: transparent;
      border-radius: var(--sb-radius-pill);
      cursor: pointer;
    }

    .topbar__user-btn:hover { background: var(--sb-surface-sunken); }
    .topbar__user-btn:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }

    .topbar__avatar {
      width: 38px;
      height: 38px;
      flex-shrink: 0;
      border-radius: var(--sb-radius-circle);
      background: var(--sb-primary-600);
      color: #fff;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      font-weight: 800;
      font-size: var(--sb-body-md-size);
      text-transform: uppercase;
    }

    .topbar__user-info {
      display: flex;
      flex-direction: column;
      line-height: 1.15;
      text-align: left;
    }

    .topbar__user-name {
      font-size: 13.5px;
      font-weight: 700;
      color: var(--sb-text);
    }

    .topbar__user-role {
      font-size: 11.5px;
      color: var(--sb-text-muted);
    }

    .topbar__chevron {
      color: var(--sb-text-muted);
      transition: transform var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .topbar__chevron.is-open { transform: rotate(180deg); }

    .topbar__menu {
      position: absolute;
      top: calc(100% + 8px);
      right: 0;
      min-width: 200px;
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-md);
      box-shadow: var(--sb-shadow-lg);
      padding: 6px;
      z-index: var(--sb-z-dropdown);
    }

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
      text-decoration: none;
      text-align: left;
    }

    .topbar__menu-item:hover { background: var(--sb-surface-sunken); color: var(--sb-text); text-decoration: none; }
    .topbar__menu-item:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }
    .topbar__menu-item--danger { color: var(--sb-danger-fg); }

    .topbar__menu-divider { height: 1px; background: var(--sb-border); margin: 4px 0; }

    @media (max-width: 900px) {
      .topbar__burger { display: inline-flex; }
      .topbar__user-info { display: none; }
    }
  `],
})
export class TopbarComponent {
  readonly #authStore = inject(AuthStore);

  readonly title = input<string>('');
  readonly subtitle = input<string>('');
  /** Emitted when the mobile burger is pressed. */
  readonly menuToggle = output<void>();

  readonly staff = this.#authStore.staff;
  readonly menuOpen = signal(false);

  readonly initials = computed(() => {
    const name = this.staff()?.displayName ?? '';
    return name
      .split(' ')
      .map((part) => part[0])
      .filter(Boolean)
      .slice(0, 2)
      .join('')
      .toUpperCase();
  });

  toggleMenu(event: MouseEvent): void {
    event.stopPropagation();
    this.menuOpen.update((open) => !open);
  }

  closeMenu(): void {
    if (this.menuOpen()) {
      this.menuOpen.set(false);
    }
  }

  async signOut(): Promise<void> {
    this.menuOpen.set(false);
    await this.#authStore.signOut();
  }
}
