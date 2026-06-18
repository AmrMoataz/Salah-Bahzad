import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Centered empty/zero-data state with an optional mascot image and a projected action slot. */
@Component({
  selector: 'sb-empty-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sb-empty">
      @if (image()) {
        <img class="sb-empty__img" [src]="image()" alt="" aria-hidden="true" />
      }
      <h3 class="sb-empty__headline">{{ headline() }}</h3>
      @if (description()) {
        <p class="sb-empty__desc">{{ description() }}</p>
      }
      <div class="sb-empty__action"><ng-content /></div>
    </div>
  `,
  styles: [`
    .sb-empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      gap: var(--sb-space-3);
      padding: var(--sb-space-10) var(--sb-space-6);
      max-width: 380px;
      margin: 0 auto;
    }
    .sb-empty__img { width: 140px; height: auto; margin-bottom: var(--sb-space-2); }
    .sb-empty__headline {
      margin: 0;
      font-size: var(--sb-text-lg);
      font-weight: var(--sb-weight-bold);
      color: var(--sb-text);
    }
    .sb-empty__desc {
      margin: 0;
      color: var(--sb-text-muted);
      line-height: var(--sb-leading-normal);
    }
    .sb-empty__action:empty { display: none; }
    .sb-empty__action { margin-top: var(--sb-space-2); }
  `],
})
export class EmptyStateComponent {
  readonly image = input<string>('');
  readonly headline = input<string>('');
  readonly description = input<string>('');
}
