import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type AlertVariant = 'info' | 'success' | 'warning' | 'danger';

/** Inline status banner with a filled-circle glyph icon (matches the design-system Alert). */
@Component({
  selector: 'sb-alert',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sb-alert sb-alert--{{ variant() }}" role="alert">
      <span class="sb-alert__icon" aria-hidden="true">{{ glyph() }}</span>
      <div class="sb-alert__content">
        @if (title()) { <p class="sb-alert__title">{{ title() }}</p> }
        <div class="sb-alert__body"><ng-content /></div>
      </div>
    </div>
  `,
  styles: [`
    .sb-alert {
      display: flex;
      gap: var(--sb-space-3);
      align-items: flex-start;
      padding: var(--sb-space-3) var(--sb-space-4);
      border-radius: var(--sb-radius-md);
      border: 1px solid transparent;
      font-size: var(--sb-body-md-size);
    }

    .sb-alert__icon {
      flex-shrink: 0;
      width: 20px;
      height: 20px;
      border-radius: var(--sb-radius-circle);
      display: inline-flex;
      align-items: center;
      justify-content: center;
      font-size: var(--sb-body-sm-size);
      font-weight: 800;
      line-height: 1;
      margin-top: 1px;
    }

    .sb-alert__content { flex: 1; line-height: 1.5; }
    .sb-alert__title { margin: 0 0 2px; font-weight: 700; }
    .sb-alert__body { line-height: 1.3; }

    .sb-alert--info {
      background: var(--sb-info-bg); border-color: var(--sb-info-border); color: var(--sb-info-fg);
    }
    .sb-alert--info .sb-alert__icon { background: var(--sb-info-fg); color: var(--sb-info-bg); }

    .sb-alert--success {
      background: var(--sb-success-bg); border-color: var(--sb-success-border); color: var(--sb-success-fg);
    }
    .sb-alert--success .sb-alert__icon { background: var(--sb-success-fg); color: var(--sb-success-bg); }

    .sb-alert--warning {
      background: var(--sb-warning-bg); border-color: var(--sb-warning-border); color: var(--sb-warning-fg);
    }
    .sb-alert--warning .sb-alert__icon { background: var(--sb-warning-fg); color: var(--sb-warning-bg); }

    .sb-alert--danger {
      background: var(--sb-danger-bg); border-color: var(--sb-danger-border); color: var(--sb-danger-fg);
    }
    .sb-alert--danger .sb-alert__icon { background: var(--sb-danger-fg); color: var(--sb-danger-bg); }
  `],
})
export class AlertComponent {
  readonly variant = input<AlertVariant>('info');
  readonly title = input<string>('');

  protected readonly glyph = computed(
    () => ({ info: 'i', success: '✓', warning: '!', danger: '!' })[this.variant()],
  );
}
