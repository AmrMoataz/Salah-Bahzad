import { DOCUMENT } from '@angular/common';
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
import { ButtonComponent } from '@sb/shared/ui';
import { QuizService } from '@sb/student-portal/data-access';
import { ScoreRingComponent } from './score-ring.component';

/** The score summary the runner hands the results screen via navigation `state` (§D / F6). */
interface QuizResultState {
  attemptId: string;
  scorePercent: number;
  bestPercent: number;
  passed: boolean;
  status: string;
  sessionTitle: string | null;
}

/**
 * The **score-only** results screen (`FR-STU-QZ-008`, contract §D, the prototype's `quizResults`). A
 * centred card: a **pass/fail mascot**, a re-implemented **score ring** (this attempt's `scorePercent`),
 * a headline + sub, two stat tiles — **"This attempt"** + **"Best of"** — a primary **"Back to session"**,
 * and a **"Review answers"** link to the §B answer-key review for the just-finished attempt. **No answer
 * key here** (the prototype's results is score-only — §D/§G).
 *
 * The result + `attemptId` arrive via navigation `state` from the runner; on a refresh/deep-link (no
 * state) it re-derives from `quiz(sessionId)`'s latest **terminal** attempt so the screen still renders.
 */
@Component({
  selector: 'sb-quiz-results',
  standalone: true,
  imports: [ButtonComponent, ScoreRingComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="qres">
      @if (loading()) {
        <div class="qres__skeleton" aria-hidden="true"></div>
      } @else if (result(); as r) {
        <div class="qres__card">
          <img
            class="qres__mascot"
            [src]="r.passed ? '/assets/salah-passed.png' : '/assets/salah-failed.png'"
            alt=""
            aria-hidden="true"
          />
          <sb-score-ring class="qres__ring" [value]="r.scorePercent" [passed]="r.passed" />

          <h1 class="qres__headline" [class.qres__headline--pass]="r.passed" [class.qres__headline--fail]="!r.passed">
            {{ r.passed ? 'You passed!' : 'Not quite yet' }}
          </h1>
          <p class="qres__sub">
            {{ r.passed
                ? 'Your best-of score cleared the bar — this session’s videos are unlocked.'
                : 'Your best-of score is below the pass mark. Review your answers, then try again.' }}
          </p>

          <div class="qres__tiles">
            <div class="qres__tile">
              <div class="qres__tile-val">{{ r.scorePercent }}%</div>
              <div class="qres__tile-lbl">This attempt</div>
            </div>
            <div class="qres__tile">
              <div class="qres__tile-val">{{ r.bestPercent }}%</div>
              <div class="qres__tile-lbl">Best of</div>
            </div>
          </div>

          <sb-button class="qres__primary" variant="primary" size="lg" (clicked)="backToSession()">
            Back to session
          </sb-button>
          <button type="button" class="qres__review" (click)="reviewAnswers()">Review answers</button>
        </div>
      } @else {
        <div class="qres__card">
          <p class="qres__sub">We couldn’t load your result.</p>
          <sb-button variant="primary" (clicked)="backToSession()">Back to session</sb-button>
        </div>
      }
    </section>
  `,
  styles: [`
    .qres { display: flex; justify-content: center; }
    .qres__skeleton { width: 100%; max-width: 560px; height: 380px; border-radius: 20px; background: var(--sb-surface-sunken); animation: qres-pulse 1.3s var(--sb-easing-standard) infinite; }
    @keyframes qres-pulse { 0%, 100% { opacity: 1; } 50% { opacity: .55; } }

    .qres__card { width: 100%; max-width: 560px; text-align: center; background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: 20px; padding: 32px 26px; box-shadow: var(--sb-shadow-md); }
    .qres__mascot { width: 138px; margin: 0 auto 8px; display: block; }
    .qres__ring { display: block; margin: 0 auto 16px; }
    .qres__headline { margin: 0; font-weight: 800; font-size: 24px; }
    .qres__headline--pass { color: var(--sb-subject-green-deep); }
    .qres__headline--fail { color: var(--sb-danger-fg); }
    .qres__sub { color: var(--sb-text-muted); font-size: 15px; margin: 6px auto 0; max-width: 420px; line-height: 1.5; }

    .qres__tiles { display: flex; gap: 12px; margin: 22px 0; justify-content: center; }
    .qres__tile { flex: 1; max-width: 160px; background: var(--sb-surface-sunken); border: 1px solid var(--sb-border); border-radius: 13px; padding: 14px; }
    .qres__tile-val { font-family: var(--sb-font-mono); font-weight: 700; font-size: 20px; }
    .qres__tile-lbl { font-size: 11px; color: var(--sb-text-muted); font-weight: 700; text-transform: uppercase; margin-top: 2px; }

    .qres__primary { display: block; }
    .qres__primary ::ng-deep .sb-btn { width: 100%; }
    .qres__review { display: inline-block; margin-top: 14px; background: none; border: none; color: var(--sb-primary-600); font-family: inherit; font-weight: 700; font-size: 14px; cursor: pointer; padding: 4px 8px; }
    .qres__review:hover { color: var(--sb-primary-700); text-decoration: underline; }
    .qres__review:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); border-radius: var(--sb-radius-sm); }
  `],
})
export class QuizResultsComponent {
  readonly #service = inject(QuizService);
  readonly #router = inject(Router);
  readonly #document = inject(DOCUMENT);

  /** The session id, bound from `/sessions/:id/quiz/results` (`withComponentInputBinding`). */
  readonly id = input.required<string>();

  readonly result = signal<QuizResultState | null>(null);
  readonly loading = signal(true);

  readonly attemptId = computed(() => this.result()?.attemptId ?? null);

  constructor() {
    effect(() => {
      const id = this.id();
      if (!id) return;
      const fromNav = this.#navState();
      if (fromNav) {
        this.result.set(fromNav);
        this.loading.set(false);
      } else {
        this.#deriveFromServer(id);
      }
    });
  }

  backToSession(): void {
    void this.#router.navigate(['/sessions', this.id()]);
  }

  reviewAnswers(): void {
    const attemptId = this.attemptId();
    if (attemptId) {
      void this.#router.navigate(['/sessions', this.id(), 'quiz', 'attempts', attemptId, 'review']);
    } else {
      void this.#router.navigate(['/sessions', this.id(), 'quiz']);
    }
  }

  #navState(): QuizResultState | null {
    const s = this.#document.defaultView?.history.state as Partial<QuizResultState> | null;
    if (s && typeof s.attemptId === 'string' && typeof s.scorePercent === 'number') {
      return {
        attemptId: s.attemptId,
        scorePercent: s.scorePercent,
        bestPercent: s.bestPercent ?? s.scorePercent,
        passed: !!s.passed,
        status: s.status ?? 'Submitted',
        sessionTitle: s.sessionTitle ?? null,
      };
    }
    return null;
  }

  /** Refresh/deep-link fallback: re-derive from the latest terminal attempt of `quiz(sessionId)` (§D/F6). */
  #deriveFromServer(sessionId: string): void {
    this.loading.set(true);
    this.#service.quiz(sessionId).subscribe({
      next: (q) => {
        const terminal = q.attempts
          .filter((a) => a.status !== 'InProgress')
          .sort((a, b) => Date.parse(b.startedAtUtc) - Date.parse(a.startedAtUtc));
        const latest = terminal[0];
        if (latest) {
          this.result.set({
            attemptId: latest.id,
            scorePercent: latest.scorePercent ?? 0,
            bestPercent: q.bestPercent ?? latest.scorePercent ?? 0,
            passed: q.passed,
            status: latest.status,
            sessionTitle: null,
          });
        }
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        void this.#router.navigate(['/sessions', sessionId]);
      },
    });
  }
}
