import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type PillVariant = 'success' | 'danger' | 'warning' | 'info' | 'neutral';

/** Status pill with a leading colored dot and tinted border (matches the design-system pill). */
@Component({
  selector: 'sb-status-pill',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="sb-pill" [class]="'sb-pill--' + variant()">
      <span class="sb-pill__dot" aria-hidden="true"></span>
      <ng-content />
    </span>
  `,
  styles: [`
    .sb-pill {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 3px 10px;
      border-radius: var(--sb-radius-pill);
      border: 1px solid transparent;
      font-size: var(--sb-body-sm-size);
      font-weight: 700;
      text-transform: capitalize;
      white-space: nowrap;
    }

    .sb-pill__dot {
      width: 6px;
      height: 6px;
      border-radius: var(--sb-radius-circle);
      background: currentColor;
      flex-shrink: 0;
    }

    .sb-pill--success { background: var(--sb-success-bg); color: var(--sb-success-fg); border-color: var(--sb-success-border); }
    .sb-pill--danger  { background: var(--sb-danger-bg);  color: var(--sb-danger-fg);  border-color: var(--sb-danger-border); }
    .sb-pill--warning { background: var(--sb-warning-bg); color: var(--sb-warning-fg); border-color: var(--sb-warning-border); }
    .sb-pill--info    { background: var(--sb-info-bg);    color: var(--sb-info-fg);    border-color: var(--sb-info-border); }
    .sb-pill--neutral { background: var(--sb-neutral-100); color: var(--sb-text-muted); border-color: var(--sb-border); }
  `],
})
export class StatusPillComponent {
  readonly variant = input<PillVariant>('neutral');
}
