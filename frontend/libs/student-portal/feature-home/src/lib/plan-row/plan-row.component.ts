import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { MyPlanStep } from '@sb/student-portal/data-access';
import {
  accentBase,
  accentBg,
  accentFg,
  dueLabel,
  overdueLabel,
  stepTypeAccent,
  stepTypeLabel,
} from '../home-presentation';

/**
 * One row of the "Your tasks" list (`FR-STU-SES-001`), styled to the Student Portal Home mock: a
 * read-only completion tick (square), the task title, a tag line (colored type pill + specialization
 * + the real expiry badge), and an outlined per-kind CTA. Pure presentational; renders a single
 * {@link MyPlanStep} exactly as composed server-side — it never invents a field, a date, or a label.
 *
 * Read-only by design (contract §0): the completion tick is a **rendered state** (`role="img"` with an
 * accessible label), **never** a togglable `<input type=checkbox>`. The only time pressure is the
 * `dueState` badge from enrollment expiry. A `blocked` row is dimmed with its `blockedReason` and an
 * inert CTA (`aria-disabled` + `aria-describedby`). The CTA label is server-supplied — rendered verbatim.
 */
@Component({
  selector: 'sb-home-plan-row',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="row" [class.row--done]="isDone()" [class.row--blocked]="step().blocked">
      <!-- read-only completion tick (NOT an interactive control) -->
      <span class="row__tick" [class.row__tick--done]="isDone()" role="img"
            [attr.aria-label]="isDone() ? 'Completed' : 'Not done'">
        @if (isDone()) {
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="3" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="M5 12.5l4.5 4.5L19 6.5" />
          </svg>
        }
      </span>

      <div class="row__body">
        <span class="row__title">{{ step().title }}</span>

        <div class="row__tags">
          <span class="row__type" [style.background]="typeBg()" [style.color]="typeFg()">{{ typeLabel() }}</span>
          @if (specName()) {
            <span class="row__spec">{{ specName() }}</span>
          }
          @if (!isDone() && step().dueState === 'Expired') {
            <span class="row__due row__due--over">{{ overdueText() }}</span>
          } @else if (!isDone() && step().dueState === 'ExpiringSoon') {
            <span class="row__due row__due--soon">{{ dueText() }}</span>
          }
        </div>

        @if (!isDone() && step().subtitle) {
          <p class="row__subtitle">{{ step().subtitle }}</p>
        }

        @if (!isDone() && step().progress; as p) {
          <div class="row__progress" aria-hidden="true">
            <span class="row__progress-track"><span class="row__progress-fill" [style.width.%]="progressPct()"></span></span>
            <span class="row__progress-label">{{ p.done }}/{{ p.total }}</span>
          </div>
        }

        @if (step().blocked && step().blockedReason) {
          <p class="row__reason" [id]="reasonId()">{{ step().blockedReason }}</p>
        }
      </div>

      @if (!isDone()) {
        <div class="row__meta">
          @if (step().blocked) {
            <button
              type="button"
              class="row__cta row__cta--disabled"
              disabled
              aria-disabled="true"
              [attr.aria-describedby]="step().blockedReason ? reasonId() : null"
            >{{ step().action.label }}</button>
          } @else if (step().action.type === 'Navigate' && step().action.route) {
            <a class="row__cta" [style.color]="typeFg()" [style.borderColor]="typeBorder()"
               [routerLink]="step().action.route">{{ step().action.label }} <span aria-hidden="true">→</span></a>
          } @else {
            <a class="row__cta" [style.color]="typeFg()" [style.borderColor]="typeBorder()"
               routerLink="/redeem">{{ step().action.label }} <span aria-hidden="true">→</span></a>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .row {
      display: flex;
      align-items: flex-start;
      gap: var(--sb-space-4);
      padding: var(--sb-space-4) 0;
    }
    .row--blocked { opacity: 0.62; }

    /* read-only completion tick rendered as a checkbox-style square */
    .row__tick {
      flex-shrink: 0; margin-top: 1px;
      width: 26px; height: 26px; border-radius: var(--sb-radius-sm);
      border: 2px solid var(--sb-border-strong); background: var(--sb-surface);
      display: inline-flex; align-items: center; justify-content: center;
      color: transparent;
    }
    .row__tick--done { background: var(--sb-accent); border-color: var(--sb-accent); color: #fff; }

    .row__body { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 6px; }
    .row__title { font-size: var(--sb-body-lg-size); font-weight: 700; color: var(--sb-text); line-height: 1.3; }
    .row--done .row__title { text-decoration: line-through; color: var(--sb-text-muted); }

    .row__tags { display: flex; align-items: center; gap: var(--sb-space-2); flex-wrap: wrap; }
    .row__type {
      padding: 2px 9px; border-radius: var(--sb-radius-pill);
      font-size: var(--sb-label-md-size); font-weight: 700; line-height: 1.4; white-space: nowrap;
    }
    .row__spec { font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); font-weight: 600; }
    .row__due {
      padding: 2px 9px; border-radius: var(--sb-radius-pill);
      font-size: var(--sb-label-md-size); font-weight: 700; line-height: 1.4; white-space: nowrap;
    }
    .row__due--soon { background: var(--sb-warning-bg); color: var(--sb-warning-fg); }
    .row__due--over { background: var(--sb-danger-bg); color: var(--sb-danger-fg); }

    .row__subtitle { margin: 0; font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); line-height: 1.4; }

    .row__progress { display: flex; align-items: center; gap: var(--sb-space-2); margin-top: 2px; }
    .row__progress-track {
      flex: 1; max-width: 160px; height: 6px;
      background: var(--sb-neutral-100); border-radius: var(--sb-radius-pill); overflow: hidden;
    }
    .row__progress-fill { display: block; height: 100%; background: var(--sb-accent); border-radius: var(--sb-radius-pill); }
    .row__progress-label { font-size: var(--sb-body-sm-size); font-weight: 700; color: var(--sb-text-muted); font-variant-numeric: tabular-nums; }

    .row__reason { margin: 2px 0 0; font-size: var(--sb-body-sm-size); color: var(--sb-warning-fg); font-weight: 600; }

    .row__meta { display: flex; align-items: center; flex-shrink: 0; align-self: center; }

    /* outlined per-kind CTA, accent set inline */
    .row__cta {
      display: inline-flex; align-items: center; gap: 6px;
      min-height: 38px; padding: 0 16px;
      border: 1.5px solid var(--sb-border-strong); border-radius: var(--sb-radius-md);
      background: var(--sb-surface);
      font-family: inherit; font-weight: 700; font-size: var(--sb-body-md-size);
      text-decoration: none; cursor: pointer; white-space: nowrap;
      transition: background var(--sb-timing-fast) var(--sb-easing-standard), box-shadow var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .row__cta:hover { background: var(--sb-surface-sunken); text-decoration: none; }
    .row__cta:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }
    .row__cta--disabled {
      background: var(--sb-neutral-100); color: var(--sb-text-subtle);
      border-color: var(--sb-border); cursor: not-allowed;
    }
    .row__cta--disabled:hover { background: var(--sb-neutral-100); }

    @media (max-width: 560px) {
      .row { flex-wrap: wrap; gap: var(--sb-space-3); }
      .row__meta { width: 100%; padding-left: calc(26px + var(--sb-space-4)); }
      .row__cta { width: 100%; justify-content: center; }
    }
  `],
})
export class HomePlanRowComponent {
  readonly step = input.required<MyPlanStep>();

  readonly isDone = computed(() => this.step().status === 'Completed');
  readonly specName = computed(() => this.step().specializationName ?? this.step().sessionTitle ?? '');

  readonly #accent = computed(() => stepTypeAccent(this.step().kind));
  readonly typeLabel = computed(() => stepTypeLabel(this.step().kind));
  readonly typeBg = computed(() => accentBg(this.#accent()));
  readonly typeFg = computed(() => accentFg(this.#accent()));
  readonly typeBorder = computed(() => accentBase(this.#accent()));

  readonly progressPct = computed(() => {
    const p = this.step().progress;
    return p && p.total > 0 ? Math.round((100 * p.done) / p.total) : 0;
  });
  readonly dueText = computed(() => dueLabel(this.step().expiresAtUtc));
  readonly overdueText = computed(() => overdueLabel(this.step().expiresAtUtc));
  /** Deterministic, unique id for the blocked reason (links the inert CTA via `aria-describedby`). */
  readonly reasonId = computed(() => `sb-plan-reason-${this.step().key.replace(/[^a-z0-9]+/gi, '-')}`);
}
