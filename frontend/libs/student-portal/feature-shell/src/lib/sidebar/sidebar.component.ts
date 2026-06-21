import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { NavItem } from '../nav-item.model';

/**
 * Student navigation rail. Three responsive modes driven by the shell's breakpoint:
 * full (desktop), icon-`rail` (tablet, labels/brand/footer hidden), and `drawer` (mobile, fixed +
 * slide-in with a focus trap). Mirrors the prototype's `APP` → Sidebar block.
 */
@Component({
  selector: 'sb-student-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside
      #drawerEl
      class="sidebar"
      [class.sidebar--rail]="collapsed()"
      [class.sidebar--drawer]="drawer()"
      [class.sidebar--open]="open()"
      aria-label="Main navigation"
      (keydown)="onKeydown($event)"
    >
      <!-- Brand -->
      <div class="sidebar__brand">
        <img class="sidebar__logo" src="/assets/logo-small.png" alt="" aria-hidden="true"
             width="42" height="42" />
        <span class="sidebar__brand-text">
          <span class="sidebar__brand-name">Salah Bahzad</span>
          <span class="sidebar__brand-sub">Student Portal</span>
        </span>
      </div>

      <!-- Navigation -->
      <nav class="sidebar__nav" aria-label="Primary">
        <ul class="sidebar__list" role="list">
          @for (item of navItems(); track item.route) {
            <li>
              @if (item.disabled) {
                <span class="sidebar__item sidebar__item--disabled"
                      aria-disabled="true" [title]="item.label + ' — coming soon'">
                  <span class="sidebar__item-icon" aria-hidden="true" [innerHTML]="iconHtml(item.icon)"></span>
                  <span class="sidebar__item-label">{{ item.label }}</span>
                  <span class="sidebar__item-soon">Soon</span>
                </span>
              } @else {
                <a
                  [routerLink]="item.route"
                  routerLinkActive="is-active"
                  [routerLinkActiveOptions]="{ exact: item.exact ?? false }"
                  class="sidebar__item"
                  [title]="item.label"
                  (click)="navigate.emit()"
                >
                  <span class="sidebar__item-icon" aria-hidden="true" [innerHTML]="iconHtml(item.icon)"></span>
                  <span class="sidebar__item-label">{{ item.label }}</span>
                </a>
              }
            </li>
          }
        </ul>
      </nav>

      <!-- Redeem footer (hidden in the icon rail) -->
      <div class="sidebar__footer">
        <div class="sidebar__redeem">
          <p class="sidebar__redeem-title">Need a code?</p>
          <p class="sidebar__redeem-copy">Get an access code from your teacher to unlock new sessions.</p>
          <button type="button" class="sidebar__redeem-btn" (click)="redeem.emit()">Redeem a code</button>
        </div>
      </div>
    </aside>
  `,
  styles: [`
    .sidebar {
      display: flex;
      flex-direction: column;
      width: 248px;
      flex-shrink: 0;
      position: sticky;
      top: 0;
      height: 100dvh;
      align-self: flex-start;
      background: var(--sb-surface);
      border-right: 1px solid var(--sb-border);
      padding: var(--sb-space-4) var(--sb-space-4) var(--sb-space-3);
      overflow: hidden;
    }

    /* ── Brand ─────────────────────────────────────────────── */
    .sidebar__brand {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 4px 8px 22px;
      flex-shrink: 0;
    }

    .sidebar__logo { width: 42px; height: 42px; flex-shrink: 0; object-fit: contain; }

    .sidebar__brand-text { display: flex; flex-direction: column; line-height: 1.1; min-width: 0; }
    .sidebar__brand-name { font-weight: 800; font-size: 14px; letter-spacing: -0.2px; color: var(--sb-text); }
    .sidebar__brand-sub  { font-size: 11px; font-weight: 600; color: var(--sb-text-muted); margin-top: 2px; }

    /* ── Nav ───────────────────────────────────────────────── */
    .sidebar__nav { flex: 1; overflow-y: auto; overflow-x: hidden; }
    .sidebar__list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 4px; }

    .sidebar__item {
      display: flex;
      align-items: center;
      gap: 12px;
      width: 100%;
      padding: 11px 12px;
      min-height: 44px;
      border-radius: 11px;
      text-decoration: none;
      color: var(--sb-neutral-600);
      font-size: 14.5px;
      font-weight: 700;
      cursor: pointer;
      transition: background var(--sb-timing-fast) var(--sb-easing-standard),
                  color var(--sb-timing-fast) var(--sb-easing-standard);
    }

    .sidebar__item:hover { background: var(--sb-primary-50); color: var(--sb-primary-600); text-decoration: none; }
    .sidebar__item:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }

    .sidebar__item.is-active,
    .sidebar__item.is-active:hover {
      background: var(--sb-primary-50);
      color: var(--sb-primary-600);
    }

    .sidebar__item--disabled,
    .sidebar__item--disabled:hover {
      color: var(--sb-text-subtle);
      background: transparent;
      cursor: default;
    }

    .sidebar__item-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 22px;
      height: 22px;
      flex-shrink: 0;
    }

    .sidebar__item-label { flex: 1; text-align: left; min-width: 0; }

    .sidebar__item-soon {
      font-size: 10px;
      font-weight: 800;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--sb-text-subtle);
      background: var(--sb-surface-sunken);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-pill);
      padding: 2px 7px;
    }

    /* ── Redeem footer ─────────────────────────────────────── */
    .sidebar__footer { margin-top: auto; padding-top: 16px; }

    .sidebar__redeem {
      background: linear-gradient(135deg, var(--sb-primary-50), var(--sb-accent-50));
      border: 1px solid var(--sb-primary-100);
      border-radius: 14px;
      padding: 14px;
    }

    .sidebar__redeem-title {
      font-family: var(--sb-font-display);
      font-size: 22px;
      font-weight: 700;
      color: var(--sb-primary-700);
      line-height: 1;
    }

    .sidebar__redeem-copy {
      font-size: 12px;
      color: var(--sb-neutral-600);
      margin: 6px 0 10px;
      line-height: 1.45;
    }

    .sidebar__redeem-btn {
      width: 100%;
      min-height: 32px;
      border: none;
      border-radius: var(--sb-radius-md);
      background: var(--sb-primary);
      color: var(--sb-on-primary);
      font-family: inherit;
      font-size: 13px;
      font-weight: 700;
      cursor: pointer;
      transition: background var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .sidebar__redeem-btn:hover { background: var(--sb-primary-hover); }
    .sidebar__redeem-btn:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }

    /* ── Tablet: icon rail ─────────────────────────────────── */
    .sidebar--rail { width: 74px; padding-left: 12px; padding-right: 12px; }
    .sidebar--rail .sidebar__brand-text,
    .sidebar--rail .sidebar__item-label,
    .sidebar--rail .sidebar__item-soon,
    .sidebar--rail .sidebar__footer { display: none; }
    .sidebar--rail .sidebar__item { justify-content: center; }
    .sidebar--rail .sidebar__brand { justify-content: center; padding-left: 0; padding-right: 0; }

    /* ── Mobile: drawer ────────────────────────────────────── */
    .sidebar--drawer {
      position: fixed;
      top: 0;
      left: 0;
      bottom: 0;
      width: 268px;
      z-index: var(--sb-z-overlay);
      transform: translateX(-110%);
      transition: transform var(--sb-timing) var(--sb-easing-standard);
    }
    .sidebar--drawer.sidebar--open {
      transform: translateX(0);
      box-shadow: var(--sb-shadow-lg);
    }
  `],
})
export class SidebarComponent {
  readonly #sanitizer = inject(DomSanitizer);
  readonly #iconCache = new Map<string, SafeHtml>();

  readonly navItems = input.required<NavItem[]>();
  /** Mobile drawer open state. */
  readonly open = input<boolean>(false);
  /** Tablet icon-rail (labels/brand/footer hidden). */
  readonly collapsed = input<boolean>(false);
  /** Mobile drawer mode (fixed + slide). */
  readonly drawer = input<boolean>(false);

  /** Emitted when a nav item is activated (lets the shell close the mobile drawer). */
  readonly navigate = output<void>();
  /** Emitted by the Redeem footer button. */
  readonly redeem = output<void>();

  // Angular signal queries can't live on an ES-private (#) field — use a TS-private one.
  private readonly drawerRef = viewChild<ElementRef<HTMLElement>>('drawerEl');

  constructor() {
    // Move focus into the drawer when it opens (a11y, FR-STU-A11Y-001).
    effect(() => {
      if (this.drawer() && this.open()) {
        queueMicrotask(() => this.#focusables()[0]?.focus());
      }
    });
  }

  /** Escape closes the drawer; Tab is trapped within it. */
  onKeydown(event: KeyboardEvent): void {
    if (!this.drawer() || !this.open()) return;

    if (event.key === 'Escape') {
      this.navigate.emit();
      return;
    }
    if (event.key !== 'Tab') return;

    const focusables = this.#focusables();
    if (focusables.length === 0) return;
    const first = focusables[0];
    const last = focusables[focusables.length - 1];
    const active = document.activeElement;

    if (event.shiftKey && active === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }

  #focusables(): HTMLElement[] {
    const root = this.drawerRef()?.nativeElement;
    if (!root) return [];
    return Array.from(
      root.querySelectorAll<HTMLElement>('a[href], button:not([disabled]), [tabindex]:not([tabindex="-1"])'),
    );
  }

  iconHtml(svg: string): SafeHtml {
    let trusted = this.#iconCache.get(svg);
    if (!trusted) {
      trusted = this.#sanitizer.bypassSecurityTrustHtml(svg);
      this.#iconCache.set(svg, trusted);
    }
    return trusted;
  }
}
