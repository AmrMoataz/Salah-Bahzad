import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  output,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { NavItem } from '../nav-item.model';

/**
 * Mobile bottom navigation with the raised centre **Redeem** FAB. Mirrors the prototype's
 * `Bottom nav (mobile)` block: two items, the FAB, then one item. Disabled (not-yet-built) items
 * render greyed and inert; later phases flip them live.
 */
@Component({
  selector: 'sb-student-bottom-nav',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="bnav" aria-label="Primary">
      @for (item of leftItems(); track item.route) {
        <ng-container [ngTemplateOutlet]="navBtn" [ngTemplateOutletContext]="{ $implicit: item }" />
      }

      <button type="button" class="bnav__fab-btn" (click)="redeem.emit()" aria-label="Redeem a code">
        <span class="bnav__fab">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="2.4" stroke-linecap="round" aria-hidden="true">
            <path d="M12 5v14M5 12h14" />
          </svg>
        </span>
        <span class="bnav__label">Redeem</span>
      </button>

      @for (item of rightItems(); track item.route) {
        <ng-container [ngTemplateOutlet]="navBtn" [ngTemplateOutletContext]="{ $implicit: item }" />
      }
    </nav>

    <ng-template #navBtn let-item>
      @if (item.disabled) {
        <span class="bnav__item bnav__item--disabled" aria-disabled="true">
          <span class="bnav__icon" aria-hidden="true" [innerHTML]="iconHtml(item.icon)"></span>
          <span class="bnav__label">{{ item.label }}</span>
        </span>
      } @else {
        <a
          class="bnav__item"
          [routerLink]="item.route"
          routerLinkActive="is-active"
          [routerLinkActiveOptions]="{ exact: item.exact ?? false }"
          (click)="navigate.emit()"
        >
          <span class="bnav__icon" aria-hidden="true" [innerHTML]="iconHtml(item.icon)"></span>
          <span class="bnav__label">{{ item.label }}</span>
        </a>
      }
    </ng-template>
  `,
  styles: [`
    .bnav {
      position: fixed;
      bottom: 0;
      left: 0;
      right: 0;
      z-index: var(--sb-z-sticky);
      display: flex;
      align-items: flex-end;
      justify-content: space-around;
      background: var(--sb-surface);
      border-top: 1px solid var(--sb-border);
      padding: 8px 8px 10px;
      box-shadow: 0 -2px 12px rgba(26, 26, 22, 0.06);
    }

    .bnav__item {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 3px;
      flex: 1;
      padding: 6px 0;
      background: none;
      border: none;
      cursor: pointer;
      text-decoration: none;
      color: var(--sb-text-subtle);
    }
    .bnav__item.is-active { color: var(--sb-primary-600); }
    .bnav__item:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); border-radius: var(--sb-radius-sm); }
    .bnav__item--disabled { color: var(--sb-text-subtle); opacity: 0.65; cursor: default; }

    .bnav__icon { display: inline-flex; align-items: center; justify-content: center; width: 22px; height: 22px; }
    .bnav__label { font-size: 11px; font-weight: 700; }

    .bnav__fab-btn {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 3px;
      flex: 1;
      padding: 6px 0;
      background: none;
      border: none;
      cursor: pointer;
      color: var(--sb-primary-600);
    }
    .bnav__fab-btn:focus-visible { outline: none; }
    .bnav__fab-btn:focus-visible .bnav__fab { box-shadow: var(--sb-shadow-focus); }

    .bnav__fab {
      width: 40px;
      height: 40px;
      margin-top: -18px;
      border-radius: 13px;
      background: var(--sb-primary);
      color: var(--sb-on-primary);
      display: flex;
      align-items: center;
      justify-content: center;
      border: 3px solid var(--sb-surface);
      box-shadow: 0 4px 12px rgba(44, 111, 179, 0.32);
    }
  `],
  host: { '[style.display]': '"contents"' },
})
export class BottomNavComponent {
  readonly #sanitizer = inject(DomSanitizer);
  readonly #iconCache = new Map<string, SafeHtml>();

  /** The full nav set; the FAB splits it (items before/after the centre). */
  readonly items = input.required<NavItem[]>();

  readonly navigate = output<void>();
  readonly redeem = output<void>();

  readonly leftItems = () => this.items().slice(0, 2);
  readonly rightItems = () => this.items().slice(2);

  iconHtml(svg: string): SafeHtml {
    let trusted = this.#iconCache.get(svg);
    if (!trusted) {
      trusted = this.#sanitizer.bypassSecurityTrustHtml(svg);
      this.#iconCache.set(svg, trusted);
    }
    return trusted;
  }
}
