import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** A single tab. <code>badge</code> shows a count chip (hidden when null/undefined). */
export interface SbTab {
  id: string;
  label: string;
  badge?: number | null;
}

/**
 * Underline tab bar (design-system `Tabs`). Stateless: the parent owns the active id and reacts to
 * <code>(tabChange)</code>. Keyboard/ARIA: a real <code>role="tablist"</code> of <code>role="tab"</code> buttons.
 */
@Component({
  selector: 'sb-tabs',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sb-tabs" role="tablist">
      @for (tab of tabs(); track tab.id) {
        <button
          type="button"
          role="tab"
          class="sb-tabs__tab"
          [class.is-active]="tab.id === active()"
          [attr.aria-selected]="tab.id === active()"
          (click)="tabChange.emit(tab.id)"
        >
          {{ tab.label }}
          @if (tab.badge != null) {
            <span class="sb-tabs__badge">{{ tab.badge }}</span>
          }
        </button>
      }
    </div>
  `,
  styles: [`
    .sb-tabs {
      display: flex;
      gap: var(--sb-space-1);
      border-bottom: 1px solid var(--sb-border);
      overflow-x: auto;
    }

    .sb-tabs__tab {
      display: inline-flex;
      align-items: center;
      gap: var(--sb-space-2);
      padding: 10px 14px;
      border: none;
      background: none;
      cursor: pointer;
      font-family: var(--sb-font-sans);
      font-size: var(--sb-body-md-size);
      font-weight: 600;
      color: var(--sb-text-muted);
      border-bottom: 2px solid transparent;
      margin-bottom: -1px;
      white-space: nowrap;
      transition: color var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .sb-tabs__tab:hover { color: var(--sb-text); }
    .sb-tabs__tab:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); border-radius: var(--sb-radius-sm); }

    .sb-tabs__tab.is-active {
      color: var(--sb-primary);
      font-weight: 700;
      border-bottom-color: var(--sb-primary);
    }

    .sb-tabs__badge {
      font-size: var(--sb-label-sm-size);
      font-weight: 700;
      padding: 1px 7px;
      border-radius: var(--sb-radius-pill);
      background: var(--sb-neutral-100);
      color: var(--sb-text-muted);
    }
    .sb-tabs__tab.is-active .sb-tabs__badge {
      background: var(--sb-primary-100);
      color: var(--sb-primary-800);
    }
  `],
})
export class TabsComponent {
  readonly tabs = input.required<readonly SbTab[]>();
  readonly active = input.required<string>();
  readonly tabChange = output<string>();
}
