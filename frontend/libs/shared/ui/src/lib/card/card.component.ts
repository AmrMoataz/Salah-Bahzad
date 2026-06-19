import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type CardShadow = 'sm' | 'md' | 'lg';

/**
 * Titled surface card (design-system `Card`). Optional header (title + right-aligned actions slot),
 * a padded or flush body, and an interactive hover lift. Project header actions into `[cardActions]`:
 *
 * ```html
 * <sb-card title="History">
 *   <a cardActions href="…">Full report</a>
 *   <p>Body…</p>
 * </sb-card>
 * ```
 */
@Component({
  selector: 'sb-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="sb-card sb-card--{{ shadow() }}"
      [class.sb-card--interactive]="interactive()"
    >
      @if (title()) {
        <div class="sb-card__head">
          <h3 class="sb-card__title">{{ title() }}</h3>
          <div class="sb-card__actions"><ng-content select="[cardActions]" /></div>
        </div>
      }
      <div class="sb-card__body" [class.sb-card__body--flush]="!padding()">
        <ng-content />
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }

    .sb-card {
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-lg);
      overflow: hidden;
      transition: box-shadow var(--sb-timing) var(--sb-easing-standard),
                  transform var(--sb-timing) var(--sb-easing-standard);
    }
    .sb-card--sm { box-shadow: var(--sb-shadow-sm); }
    .sb-card--md { box-shadow: var(--sb-shadow-md); }
    .sb-card--lg { box-shadow: var(--sb-shadow-lg); }

    .sb-card--interactive { cursor: pointer; }
    .sb-card--interactive:hover { box-shadow: var(--sb-shadow-md); transform: translateY(-2px); }

    .sb-card__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--sb-space-2);
      padding: var(--sb-space-4) var(--sb-space-6);
      border-bottom: 1px solid var(--sb-border);
    }
    .sb-card__title {
      margin: 0;
      font-size: var(--sb-heading-sm-size);
      font-weight: 700;
      color: var(--sb-text);
    }
    .sb-card__actions { display: flex; gap: var(--sb-space-2); align-items: center; }
    .sb-card__actions:empty { display: none; }

    .sb-card__body {
      padding: var(--sb-space-6);
      color: var(--sb-text);
      font-size: var(--sb-body-md-size);
    }
    .sb-card__body--flush { padding: 0; }
  `],
})
export class CardComponent {
  readonly title = input<string>('');
  readonly padding = input<boolean>(true);
  readonly shadow = input<CardShadow>('sm');
  readonly interactive = input<boolean>(false);
}
