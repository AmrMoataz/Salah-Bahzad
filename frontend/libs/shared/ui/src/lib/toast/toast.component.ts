import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ToastVariant } from './toast.service';

/** Presentational toast (design-system `Toast`): surface card with a left accent bar + dismiss. */
@Component({
  selector: 'sb-toast',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sb-toast" [style.border-left-color]="accent()" role="status">
      <div class="sb-toast__body">
        @if (title()) { <div class="sb-toast__title">{{ title() }}</div> }
        @if (message()) { <div class="sb-toast__message">{{ message() }}</div> }
      </div>
      <button type="button" class="sb-toast__close" aria-label="Dismiss" (click)="close.emit()">×</button>
    </div>
  `,
  styles: [`
    .sb-toast {
      display: flex;
      align-items: flex-start;
      gap: var(--sb-space-3);
      min-width: 280px;
      max-width: 360px;
      padding: var(--sb-space-3) var(--sb-space-4);
      background: var(--sb-surface);
      border-radius: var(--sb-radius-md);
      border-left: 4px solid var(--sb-info);
      box-shadow: var(--sb-shadow-md);
    }
    .sb-toast__body { flex: 1; min-width: 0; }
    .sb-toast__title { font-size: var(--sb-body-md-size); font-weight: 700; color: var(--sb-text); }
    .sb-toast__message { font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); margin-top: 2px; }
    .sb-toast__close {
      border: none;
      background: none;
      cursor: pointer;
      color: var(--sb-text-subtle);
      font-size: 18px;
      line-height: 1;
      padding: 0;
    }
  `],
})
export class ToastComponent {
  readonly variant = input<ToastVariant>('info');
  readonly title = input<string>('');
  readonly message = input<string>('');
  readonly close = output<void>();

  readonly accent = computed(() => {
    const map: Record<ToastVariant, string> = {
      success: 'var(--sb-success)',
      danger: 'var(--sb-danger)',
      warning: 'var(--sb-warning)',
      info: 'var(--sb-info)',
    };
    return map[this.variant()];
  });
}
