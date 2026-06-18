import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  output,
} from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { NavGroup } from '../nav-item.model';

@Component({
  selector: 'sb-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside class="sidebar" [class.sidebar--open]="open()" aria-label="Main navigation">
      <!-- Brand -->
      <div class="sidebar__brand">
        <img class="sidebar__logo" src="/assets/logo-small.png" alt="" aria-hidden="true"
             width="34" height="34" />
        <span class="sidebar__brand-text">
          <span class="sidebar__brand-name">Salah Bahzad</span>
          <span class="sidebar__brand-sub">Teacher Portal</span>
        </span>
      </div>

      <!-- Grouped navigation -->
      <nav class="sidebar__nav" aria-label="Primary">
        @for (group of navGroups(); track group.title) {
          <p class="sidebar__group-title">{{ group.title }}</p>
          <ul class="sidebar__list" role="list">
            @for (item of group.items; track item.route) {
              <li>
                <a
                  [routerLink]="item.route"
                  routerLinkActive="is-active"
                  [routerLinkActiveOptions]="{ exact: false }"
                  class="sidebar__item"
                  (click)="navigate.emit()"
                >
                  <span class="sidebar__item-icon" aria-hidden="true" [innerHTML]="iconHtml(item.icon)"></span>
                  <span class="sidebar__item-label">{{ item.label }}</span>
                  @if (item.badgeSignal && item.badgeSignal()) {
                    <span class="sidebar__item-badge">{{ item.badgeSignal!() }}</span>
                  }
                </a>
              </li>
            }
          </ul>
        }
      </nav>
    </aside>
  `,
  styles: [`
    .sidebar {
      display: flex;
      flex-direction: column;
      width: 248px;
      flex-shrink: 0;
      height: 100%;
      background: var(--sb-surface);
      border-right: 1px solid var(--sb-border);
      padding: 0 var(--sb-space-3) var(--sb-space-3);
      overflow: hidden;
    }

    /* ── Brand ─────────────────────────────────────────────── */
    /* Fixed 64px height + border-bottom makes the brand's divider line
       up exactly with the topbar's bottom border (also 64px tall). */
    .sidebar__brand {
      display: flex;
      align-items: center;
      gap: var(--sb-space-3);
      height: 64px;
      flex-shrink: 0;
      padding: 0 6px;
      border-bottom: 1px solid var(--sb-border);
      margin-bottom: var(--sb-space-3);
    }

    .sidebar__logo { width: 34px; height: 34px; flex-shrink: 0; object-fit: contain; }

    .sidebar__brand-text { display: flex; flex-direction: column; line-height: 1.1; min-width: 0; }

    .sidebar__brand-name {
      font-weight: var(--sb-weight-extrabold);
      font-size: 15px;
      color: var(--sb-text);
    }

    .sidebar__brand-sub {
      font-size: 11px;
      font-weight: var(--sb-weight-bold);
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--sb-text-muted);
    }

    /* ── Nav ───────────────────────────────────────────────── */
    .sidebar__nav {
      flex: 1;
      display: flex;
      flex-direction: column;
      overflow-y: auto;
      overflow-x: hidden;
    }

    .sidebar__group-title {
      margin: 0;
      padding: 14px 12px 6px;
      font-size: 11px;
      font-weight: var(--sb-weight-bold);
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--sb-text-subtle);
    }

    .sidebar__list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .sidebar__item {
      display: flex;
      align-items: center;
      gap: 11px;
      padding: 10px 12px;
      border-radius: var(--sb-radius-md);
      text-decoration: none;
      color: var(--sb-text);
      font-size: var(--sb-text-sm);
      font-weight: var(--sb-weight-semibold);
      transition: background var(--sb-dur-fast) var(--sb-ease-standard),
                  color var(--sb-dur-fast) var(--sb-ease-standard);
    }

    .sidebar__item:hover { background: var(--sb-primary-50); color: var(--sb-text); text-decoration: none; }
    .sidebar__item:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }

    .sidebar__item.is-active,
    .sidebar__item.is-active:hover {
      background: var(--sb-primary);
      color: var(--sb-on-primary);
      font-weight: var(--sb-weight-bold);
      box-shadow: var(--sb-shadow-sm);
    }

    .sidebar__item-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 20px;
      height: 20px;
      flex-shrink: 0;
    }

    .sidebar__item-label { flex: 1; min-width: 0; }

    .sidebar__item-badge {
      margin-left: auto;
      min-width: 20px;
      height: 20px;
      padding: 0 6px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      border-radius: var(--sb-radius-pill);
      background: var(--sb-danger);
      color: #fff;
      font-size: 11px;
      font-weight: var(--sb-weight-extrabold);
    }

    .sidebar__item.is-active .sidebar__item-badge { background: rgba(255, 255, 255, 0.25); }

    /* ── Mobile drawer (matches the design's <=900px behaviour) ─ */
    @media (max-width: 900px) {
      .sidebar {
        position: fixed;
        top: 0;
        left: 0;
        z-index: var(--sb-z-overlay);
        height: 100dvh;
        transform: translateX(-100%);
        transition: transform var(--sb-dur) var(--sb-ease-standard);
      }

      .sidebar--open {
        transform: translateX(0);
        box-shadow: var(--sb-shadow-lg);
      }
    }
  `],
})
export class SidebarComponent {
  readonly #sanitizer = inject(DomSanitizer);
  readonly #iconCache = new Map<string, SafeHtml>();

  readonly navGroups = input.required<NavGroup[]>();
  /** Mobile drawer open state. */
  readonly open = input<boolean>(false);
  /** Emitted when a nav item is clicked (lets the shell close the mobile drawer). */
  readonly navigate = output<void>();

  /**
   * Icons are developer-authored constant SVG markup, so we bypass the HTML
   * sanitizer (which otherwise strips `<svg>`/stroke attributes from innerHTML).
   */
  iconHtml(svg: string): SafeHtml {
    let trusted = this.#iconCache.get(svg);
    if (!trusted) {
      trusted = this.#sanitizer.bypassSecurityTrustHtml(svg);
      this.#iconCache.set(svg, trusted);
    }
    return trusted;
  }
}
