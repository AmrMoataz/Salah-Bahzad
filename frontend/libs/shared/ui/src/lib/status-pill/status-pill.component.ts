import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type PillVariant = 'success' | 'danger' | 'warning' | 'info' | 'neutral';

@Component({
  selector: 'sb-status-pill',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="sb-pill" [class]="'sb-pill--' + variant()">
      <ng-content />
    </span>
  `,
  styles: [`
    .sb-pill {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 2px 10px;
      border-radius: var(--sb-radius-pill);
      font-size: var(--sb-text-xs);
      font-weight: var(--sb-weight-semibold);
      white-space: nowrap;
    }

    .sb-pill--success { background: var(--sb-success-bg); color: var(--sb-success-fg); }
    .sb-pill--danger  { background: var(--sb-danger-bg);  color: var(--sb-danger-fg); }
    .sb-pill--warning { background: var(--sb-warning-bg); color: var(--sb-warning-fg); }
    .sb-pill--info    { background: var(--sb-info-bg);    color: var(--sb-info-fg); }
    .sb-pill--neutral { background: var(--sb-neutral-100); color: var(--sb-neutral-600); }
  `],
})
export class StatusPillComponent {
  readonly variant = input<PillVariant>('neutral');
}
