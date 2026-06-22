import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

const RADIUS = 15.5;
const CIRC = 2 * Math.PI * RADIUS; // the prototype's `circ = 2π·15.5`

/**
 * A small **score ring** for the quiz results screen (contract §D, the prototype's `quizResults` score
 * dial). An SVG arc (the same `circ = 2π·15.5` stroke-dash maths the S3 hero ring uses) coloured by
 * **pass/fail**, with the score `%` centred in mono.
 *
 * Re-implemented **inside** `feature-assessment` on purpose — the shared `Progress` is linear-only and
 * S3's `CircularProgressComponent` lives in another `scope:student-portal` feature; cross-importing it
 * would breach the module boundary (master plan §3.2). `role="img"` + `aria-label` keep it legible.
 */
@Component({
  selector: 'sb-score-ring',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="ring"
      [class.ring--pass]="passed()"
      [class.ring--fail]="!passed()"
      [style.width.px]="size()"
      [style.height.px]="size()"
      role="img"
      [attr.aria-label]="clamped() + '% — ' + (passed() ? 'passed' : 'not passed')"
    >
      <svg viewBox="0 0 36 36" class="ring__svg" aria-hidden="true">
        <circle class="ring__track" cx="18" cy="18" [attr.r]="radius" />
        <circle
          class="ring__value"
          cx="18"
          cy="18"
          [attr.r]="radius"
          [attr.stroke-dasharray]="circ"
          [attr.stroke-dashoffset]="offset()"
        />
      </svg>
      <div class="ring__label">
        <span class="ring__pct">{{ clamped() }}<span class="ring__pct-sign">%</span></span>
      </div>
    </div>
  `,
  styles: [`
    .ring { position: relative; flex-shrink: 0; margin: 0 auto; }
    .ring__svg { width: 100%; height: 100%; transform: rotate(-90deg); }
    .ring__track { fill: none; stroke: var(--sb-neutral-100); stroke-width: 3.2; }
    .ring__value {
      fill: none;
      stroke-width: 3.2;
      stroke-linecap: round;
      transition: stroke-dashoffset var(--sb-timing-slow) var(--sb-easing-standard);
    }
    .ring--pass .ring__value { stroke: var(--sb-accent); }
    .ring--fail .ring__value { stroke: var(--sb-danger); }
    .ring--pass .ring__pct { color: var(--sb-subject-green-deep); }
    .ring--fail .ring__pct { color: var(--sb-danger-fg); }

    .ring__label {
      position: absolute;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      line-height: 1;
    }
    .ring__pct { font-family: var(--sb-font-mono); font-weight: 700; font-size: 30px; }
    .ring__pct-sign { font-size: 16px; }
  `],
})
export class ScoreRingComponent {
  /** The attempt's `scorePercent`. */
  readonly value = input<number>(0);
  /** Drives the arc + text colour (`scorePercent >= minPassPercent`, derived by the caller). */
  readonly passed = input<boolean>(false);
  readonly size = input<number>(120);

  readonly radius = RADIUS;
  readonly circ = CIRC;
  readonly clamped = computed(() => Math.min(Math.max(Math.round(this.value()), 0), 100));
  readonly offset = computed(() => CIRC * (1 - this.clamped() / 100));
}
