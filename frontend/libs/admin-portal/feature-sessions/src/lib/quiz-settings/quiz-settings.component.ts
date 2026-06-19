import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';
import { AlertComponent, ButtonComponent, CardComponent, ToastService } from '@sb/shared/ui';
import { SessionDetailDto } from '../data-access/session.models';
import { SessionService } from '../data-access/session.service';

/**
 * Gating-quiz settings (FR-ADM-QZ-001/002, FR-PLAT-SES-006, mockup `scrQuizSettings`). Four sliders
 * (time, number of questions, attempts, minimum pass %) with a live "effective behaviour" summary and
 * a soft warning when the requested question count exceeds the session's quiz-eligible bank (the
 * server hard-blocks this on publish). Saves via the quiz-settings endpoint (time in minutes per the contract).
 */
@Component({
  selector: 'sb-quiz-settings',
  standalone: true,
  imports: [CardComponent, ButtonComponent, AlertComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button type="button" class="qz__back" (click)="back()">
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
           stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <path d="M19 12H5M12 19l-7-7 7-7"/>
      </svg>
      Back to session
    </button>

    @if (loadError()) {
      <sb-alert variant="danger" title="Couldn’t load session">{{ loadError() }}</sb-alert>
    } @else if (session(); as s) {
      <div class="qz__head">
        <div>
          <h1 class="qz__title">Quiz settings</h1>
          <p class="qz__subtitle">Gating quiz for “{{ s.title }}”</p>
        </div>
        <sb-button variant="primary" [loading]="saving()" (clicked)="save()">Save settings</sb-button>
      </div>

      @if (warn()) {
        <div class="qz__warn">
          <sb-alert variant="warning" title="Not enough eligible questions">
            You requested {{ count() }} questions but only {{ eligible() }} quiz-eligible
            question{{ eligible() === 1 ? '' : 's' }} exist in this session’s bank. Lower the count or mark
            more questions eligible — publishing is blocked until it fits.
          </sb-alert>
        </div>
      }

      <div class="qz__knobs">
        <div class="qz__knob">
          <div class="qz__knob-head"><span>Time limit</span><span class="qz__knob-value">{{ time() }} min</span></div>
          <input type="range" min="5" max="60" step="5" [value]="time()" (input)="set('time', $event)" />
          <div class="qz__knob-scale"><span>5 min</span><span>60 min</span></div>
        </div>
        <div class="qz__knob">
          <div class="qz__knob-head"><span>Number of questions</span><span class="qz__knob-value">{{ count() }}</span></div>
          <input type="range" min="5" max="30" step="1" [value]="count()" (input)="set('count', $event)" />
          <div class="qz__knob-scale"><span>5</span><span>30</span></div>
        </div>
        <div class="qz__knob">
          <div class="qz__knob-head"><span>Attempts allowed</span><span class="qz__knob-value">{{ attempts() }}</span></div>
          <input type="range" min="1" max="5" step="1" [value]="attempts()" (input)="set('attempts', $event)" />
          <div class="qz__knob-scale"><span>1</span><span>5</span></div>
        </div>
        <div class="qz__knob">
          <div class="qz__knob-head"><span>Minimum pass</span><span class="qz__knob-value">{{ pass() }}%</span></div>
          <input type="range" min="40" max="100" step="5" [value]="pass()" (input)="set('pass', $event)" />
          <div class="qz__knob-scale"><span>40%</span><span>100%</span></div>
        </div>
      </div>

      <sb-card title="Effective behaviour">
        <p class="qz__effect">
          Students get <strong>{{ attempts() }} attempt{{ attempts() === 1 ? '' : 's' }}</strong> at a
          <strong>{{ count() }}-question</strong> quiz with a <strong>{{ time() }}-minute</strong> limit. They must
          score at least <strong>{{ pass() }}%</strong> to unlock this session’s videos. Questions are drawn at
          random from <strong>{{ eligible() }} eligible</strong> question{{ eligible() === 1 ? '' : 's' }}
          (with their variations).
        </p>
      </sb-card>
    } @else {
      <p class="qz__loading">Loading…</p>
    }
  `,
  styles: [`
    :host { display: block; }

    .qz__back { display: inline-flex; align-items: center; gap: var(--sb-space-2); margin-bottom: var(--sb-space-4); border: none; background: transparent; cursor: pointer; color: var(--sb-text-muted); font-family: var(--sb-font-sans); font-size: var(--sb-body-md-size); font-weight: 700; padding: 0; }
    .qz__back:hover { color: var(--sb-primary); }
    .qz__loading { padding: var(--sb-space-8) 0; color: var(--sb-text-muted); }

    .qz__head { display: flex; align-items: flex-end; justify-content: space-between; gap: var(--sb-space-4); flex-wrap: wrap; margin-bottom: var(--sb-space-5); }
    .qz__title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-lg-size); font-weight: 800; letter-spacing: -0.01em; color: var(--sb-text); }
    .qz__subtitle { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    .qz__warn { margin-bottom: var(--sb-space-4); }

    .qz__knobs { display: grid; grid-template-columns: repeat(auto-fit, minmax(230px, 1fr)); gap: var(--sb-space-4); margin-bottom: var(--sb-space-4); }
    .qz__knob { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-lg); padding: var(--sb-space-5); box-shadow: var(--sb-shadow-sm); }
    .qz__knob-head { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: var(--sb-space-3); }
    .qz__knob-head > span:first-child { font-weight: 700; font-size: var(--sb-body-md-size); color: var(--sb-text); }
    .qz__knob-value { font-size: var(--sb-heading-md-size); font-weight: 800; color: var(--sb-primary); }
    .qz__knob input[type='range'] { width: 100%; accent-color: var(--sb-primary); cursor: pointer; }
    .qz__knob-scale { display: flex; justify-content: space-between; font-size: var(--sb-label-sm-size); color: var(--sb-text-subtle); margin-top: var(--sb-space-1); }

    .qz__effect { margin: 0; font-size: var(--sb-body-lg-size); line-height: 1.8; color: var(--sb-text-muted); }
    .qz__effect strong { color: var(--sb-text); }
  `],
})
export class QuizSettingsComponent {
  readonly #service = inject(SessionService);
  readonly #auth = inject(AuthStore);
  readonly #router = inject(Router);
  readonly #toast = inject(ToastService);

  /** Bound from the `:id` route segment (withComponentInputBinding). */
  readonly id = input.required<string>();

  readonly session = signal<SessionDetailDto | null>(null);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);

  readonly time = signal(15);
  readonly count = signal(10);
  readonly attempts = signal(2);
  readonly pass = signal(60);

  readonly eligible = computed(() => this.session()?.quizEligibleQuestionCount ?? 0);
  readonly warn = computed(() => this.count() > this.eligible());

  constructor() {
    if (!this.#auth.hasPermission('SessionsEdit')) {
      this.loadError.set('You don’t have permission to edit quiz settings.');
    }
    effect(() => {
      const id = this.id();
      queueMicrotask(() => void this.#load(id));
    });
  }

  async #load(id: string): Promise<void> {
    if (this.loadError()) return;
    try {
      const s = await this.#service.getById(id);
      this.session.set(s);
      const q = s.quizSetting;
      if (q) {
        this.time.set(q.timeLimitMinutes);
        this.count.set(q.questionCount);
        this.attempts.set(q.attemptCount);
        this.pass.set(q.minPassPercent);
      }
    } catch {
      this.loadError.set('Could not load this session. It may not exist or you may not have access.');
    }
  }

  set(knob: 'time' | 'count' | 'attempts' | 'pass', event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    ({ time: this.time, count: this.count, attempts: this.attempts, pass: this.pass })[knob].set(value);
  }

  async save(): Promise<void> {
    this.saving.set(true);
    try {
      await this.#service.updateQuizSettings(this.id(), {
        timeLimitMinutes: this.time(),
        questionCount: this.count(),
        attemptCount: this.attempts(),
        minPassPercent: this.pass(),
      });
      this.#toast.success('Quiz settings saved');
      void this.#router.navigate(['/sessions', this.id()]);
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not save the quiz settings.');
    } finally {
      this.saving.set(false);
    }
  }

  back(): void {
    void this.#router.navigate(['/sessions', this.id()]);
  }
}
