import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ProgressVariant } from '@sb/shared/ui';

const RADIUS = 15.5;
const CIRC = 2 * Math.PI * RADIUS; // the prototype's `circ = 2π·15.5`

/**
 * A circular progress **ring** for the session-detail hero (the prototype's `Progress circular`). An
 * SVG arc using the `circ = 2π·15.5` stroke-dash maths, with the percent + a "done" caption centred.
 * `role="img"` + `aria-label` keep it screen-reader-legible. *(The shared `Progress` is linear — used
 * for the spotlight bar; this ring is detail-only.)*
 */
@Component({
  selector: 'sb-circular-progress',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="ring"
      [style.width.px]="size()"
      [style.height.px]="size()"
      role="img"
      [attr.aria-label]="clamped() + '% complete'"
    >
      <svg viewBox="0 0 36 36" class="ring__svg" aria-hidden="true">
        <circle class="ring__track" cx="18" cy="18" [attr.r]="radius" />
        <circle
          class="ring__value"
          cx="18"
          cy="18"
          [attr.r]="radius"
          [style.stroke]="color()"
          [attr.stroke-dasharray]="circ"
          [attr.stroke-dashoffset]="offset()"
        />
      </svg>
      <div class="ring__label">
        <span class="ring__pct">{{ clamped() }}<span class="ring__pct-sign">%</span></span>
        <span class="ring__caption">done</span>
      </div>
    </div>
  `,
  styles: [`
    .ring { position: relative; flex-shrink: 0; }
    .ring__svg { width: 100%; height: 100%; transform: rotate(-90deg); }
    .ring__track {
      fill: none;
      stroke: var(--sb-neutral-100);
      stroke-width: 3.2;
    }
    .ring__value {
      fill: none;
      stroke-width: 3.2;
      stroke-linecap: round;
      transition: stroke-dashoffset var(--sb-timing-slow) var(--sb-easing-standard);
    }
    .ring__label {
      position: absolute;
      inset: 0;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      line-height: 1;
    }
    .ring__pct {
      font-family: var(--sb-font-mono);
      font-weight: 700;
      font-size: 18px;
      color: var(--sb-text);
    }
    .ring__pct-sign { font-size: 11px; }
    .ring__caption {
      font-size: 10px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--sb-text-subtle);
      margin-top: 3px;
    }
  `],
})
export class CircularProgressComponent {
  readonly value = input<number>(0);
  readonly variant = input<ProgressVariant>('success');
  readonly size = input<number>(84);

  readonly radius = RADIUS;
  readonly circ = CIRC;
  readonly clamped = computed(() => Math.min(Math.max(Math.round(this.value()), 0), 100));
  readonly offset = computed(() => CIRC * (1 - this.clamped() / 100));
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
