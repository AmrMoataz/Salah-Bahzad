import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';
import { NavItem } from '../nav-item.model';

@Component({
  selector: 'sb-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="sidebar" [class.sidebar--collapsed]="collapsed()" aria-label="Main navigation">
      <!-- Logo -->
      <div class="sidebar__brand">
        <div class="sidebar__logo" aria-hidden="true">
          <svg width="32" height="32" viewBox="0 0 32 32" fill="none">
            <rect width="32" height="32" rx="8" fill="var(--sb-primary)"/>
            <text x="16" y="22" text-anchor="middle" font-size="18" font-weight="900"
                  font-family="var(--sb-font-ui)" fill="white">S</text>
          </svg>
        </div>
        @if (!collapsed()) {
          <span class="sidebar__brand-name">Salah Bahzad</span>
        }
      </div>

      <!-- Nav items -->
      <ul class="sidebar__nav" role="list">
        @for (item of navItems(); track item.route) {
          <li>
            <a
              [routerLink]="item.route"
              routerLinkActive="sidebar__nav-item--active"
              [routerLinkActiveOptions]="{ exact: item.route === '/' }"
              class="sidebar__nav-item"
              [attr.aria-label]="collapsed() ? item.label : null"
              [title]="collapsed() ? item.label : ''"
            >
              <span class="sidebar__nav-icon" aria-hidden="true" [innerHTML]="item.icon"></span>
              @if (!collapsed()) {
                <span class="sidebar__nav-label">{{ item.label }}</span>
              }
            </a>
          </li>
        }
      </ul>

      <!-- Collapse toggle -->
      <button
        class="sidebar__toggle"
        (click)="toggleCollapse()"
        [attr.aria-label]="collapsed() ? 'Expand sidebar' : 'Collapse sidebar'"
        type="button"
      >
        <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"
             [style.transform]="collapsed() ? 'rotate(180deg)' : ''">
          <path d="M9.78 12.78a.75.75 0 0 1-1.06 0L4.47 8.53a.75.75 0 0 1 0-1.06l4.25-4.25a.75.75 0 0 1 1.06 1.06L6.06 8l3.72 3.72a.75.75 0 0 1 0 1.06z"/>
        </svg>
      </button>
    </nav>
  `,
  styles: [`
    .sidebar {
      display: flex;
      flex-direction: column;
      width: 240px;
      height: 100%;
      background: var(--sb-surface);
      border-right: 1px solid var(--sb-border);
      transition: width var(--sb-dur) var(--sb-ease-standard);
      overflow: hidden;
      flex-shrink: 0;

      &--collapsed { width: 64px; }
    }

    .sidebar__brand {
      display: flex;
      align-items: center;
      gap: var(--sb-space-3);
      padding: var(--sb-space-4) var(--sb-space-4);
      height: 60px;
      border-bottom: 1px solid var(--sb-border);
      overflow: hidden;
    }

    .sidebar__brand-name {
      font-weight: var(--sb-weight-extrabold);
      font-size: var(--sb-text-base);
      color: var(--sb-text);
      white-space: nowrap;
    }

    .sidebar__nav {
      list-style: none;
      margin: 0;
      padding: var(--sb-space-3) var(--sb-space-2);
      display: flex;
      flex-direction: column;
      gap: 2px;
      flex: 1;
      overflow-y: auto;
    }

    .sidebar__nav-item {
      display: flex;
      align-items: center;
      gap: var(--sb-space-3);
      padding: 0 var(--sb-space-3);
      height: 40px;
      border-radius: var(--sb-radius-md);
      text-decoration: none;
      color: var(--sb-text-muted);
      font-size: var(--sb-text-sm);
      font-weight: var(--sb-weight-semibold);
      transition: background var(--sb-dur) var(--sb-ease-standard),
                  color var(--sb-dur) var(--sb-ease-standard);
      white-space: nowrap;
      overflow: hidden;
      position: relative;

      &:hover { background: var(--sb-surface-sunken); color: var(--sb-text); }

      &:focus-visible { box-shadow: var(--sb-shadow-focus); outline: none; }

      &--active {
        background: var(--sb-primary-50);
        color: var(--sb-primary);

        &::before {
          content: '';
          position: absolute;
          left: 0;
          top: 6px;
          bottom: 6px;
          width: 3px;
          background: var(--sb-primary);
          border-radius: 0 2px 2px 0;
        }
      }
    }

    .sidebar__nav-icon {
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      width: 20px;
      height: 20px;
    }

    .sidebar__nav-label { flex: 1; }

    .sidebar__toggle {
      margin: var(--sb-space-2);
      height: 36px;
      background: transparent;
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-md);
      cursor: pointer;
      color: var(--sb-text-muted);
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all var(--sb-dur) var(--sb-ease-standard);

      &:hover { background: var(--sb-surface-sunken); color: var(--sb-text); }
      &:focus-visible { box-shadow: var(--sb-shadow-focus); outline: none; }

      svg { transition: transform var(--sb-dur) var(--sb-ease-standard); }
    }
  `],
})
export class SidebarComponent {
  readonly #authStore = inject(AuthStore);
  readonly navItems = input.required<NavItem[]>();
  readonly collapsed = input<boolean>(false);
  readonly collapsedChange = output<boolean>();

  readonly #isCollapsed = signal(false);

  toggleCollapse(): void {
    const next = !this.collapsed();
    this.collapsedChange.emit(next);
  }
}
