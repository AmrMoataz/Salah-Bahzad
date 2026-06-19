import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type ProgressVariant = 'primary' | 'success' | 'danger' | 'warning' | 'info';

/** Linear progress bar (design-system `Progress`). `success` uses the accent colour, per the DS. */
@Component({
  selector: 'sb-progress',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sb-progress">
      @if (label() || showValue()) {
        <div class="sb-progress__meta">
          @if (label()) { <span>{{ label() }}</span> }
          @if (showValue()) { <span class="sb-progress__value">{{ clamped() }}%</span> }
        </div>
      }
      <div
        class="sb-progress__track"
        [style.height.px]="height()"
        role="progressbar"
        [attr.aria-valuenow]="clamped()"
        aria-valuemin="0"
        aria-valuemax="100"
      >
        <div class="sb-progress__fill" [style.width.%]="clamped()" [style.background]="color()"></div>
      </div>
    </div>
  `,
  styles: [`
    .sb-progress { width: 100%; }
    .sb-progress__meta {
      display: flex;
      justify-content: space-between;
      margin-bottom: var(--sb-space-2);
      font-size: var(--sb-body-sm-size);
      color: var(--sb-text-muted);
    }
    .sb-progress__value { font-weight: 700; color: var(--sb-text); }

    .sb-progress__track {
      width: 100%;
      background: var(--sb-neutral-100);
      border-radius: var(--sb-radius-pill);
      overflow: hidden;
    }
    .sb-progress__fill {
      height: 100%;
      border-radius: var(--sb-radius-pill);
      transition: width var(--sb-timing) var(--sb-easing-standard);
    }
  `],
})
export class ProgressComponent {
  readonly value = input<number>(0);
  readonly variant = input<ProgressVariant>('primary');
  readonly label = input<string>('');
  readonly showValue = input<boolean>(false);
  readonly height = input<number>(8);

  readonly clamped = computed(() => Math.min(Math.max(this.value(), 0), 100));
  readonly color = computed(() => {
    const map: Record<ProgressVariant, string> = {
      primary: 'var(--sb-primary)',
      success: 'var(--sb-accent)',
      danger: 'var(--sb-danger)',
      warning: 'var(--sb-warning)',
      info: 'var(--sb-info)',
    };
    return map[this.variant()];
  });
}
