import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { GateState } from '@sb/student-portal/data-access';

/**
 * The mascot-forward **gate banner** (the prototype's amber `#FEF6DD`/`#F5E2A0` band), driven by
 * `gateState` (§E.4). `QuizRequired` → "Pass the prerequisite quiz ({minPassPercent}%) to unlock the
 * remaining locked videos. Your assignment is available now."; `Expired` → videos & the quiz are
 * locked but the assignment & materials stay open (`FR-STU-SES-001`); `Open` → renders nothing.
 */
@Component({
  selector: 'sb-gate-banner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (gateState() !== 'Open') {
      <div class="gate" role="status" [attr.data-state]="gateState()">
        <img class="gate__mascot" [src]="mascot()" alt="" aria-hidden="true" />
        <div class="gate__text">
          <p class="gate__title">{{ title() }}</p>
          <p class="gate__body">{{ body() }}</p>
        </div>
      </div>
    }
  `,
  styles: [`
    .gate {
      display: flex;
      gap: 14px;
      align-items: center;
      background: #FEF6DD;
      border: 1px solid #F5E2A0;
      border-radius: 14px;
      padding: 12px 16px;
    }
    .gate__mascot { width: 62px; flex-shrink: 0; height: auto; }
    .gate__text { flex: 1; min-width: 0; }
    .gate__title { margin: 0 0 2px; font-weight: 800; color: #8A6A00; font-size: 15px; }
    .gate__body { margin: 0; font-size: 13px; color: #8A6A00; line-height: 1.5; }

    @media (max-width: 480px) {
      .gate__mascot { width: 48px; }
    }
  `],
})
export class GateBannerComponent {
  readonly gateState = input.required<GateState>();
  readonly minPassPercent = input<number>(0);

  readonly mascot = computed(() =>
    this.gateState() === 'QuizRequired'
      ? '/assets/salah-prerequisite.png'
      : '/assets/salah-relaxing.png',
  );

  readonly title = computed(() =>
    this.gateState() === 'QuizRequired'
      ? 'Pass the quiz to unlock videos'
      : 'This session’s access has expired',
  );

  readonly body = computed(() =>
    this.gateState() === 'QuizRequired'
      ? `Pass the prerequisite quiz (${this.minPassPercent()}%) to unlock the remaining locked videos. Your assignment is available now.`
      : 'Videos and the quiz are locked, but your assignment and materials stay open.',
  );
}
