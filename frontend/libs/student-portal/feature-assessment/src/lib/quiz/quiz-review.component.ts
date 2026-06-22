import { HttpErrorResponse } from '@angular/common/http';
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
import { ButtonComponent, LatexPreviewComponent, StatusPillComponent } from '@sb/shared/ui';
import {
  QuizService,
  StudentQuizAttemptReview,
  StudentQuizReviewOption,
} from '@sb/student-portal/data-access';
import { mmss, optionLetter, quizFlagFromStatus, quizFlagVariant } from '../assessment.util';

type OptionState = 'correct' | 'picked-wrong' | 'neutral';

/**
 * The **NEW** per-attempt **answer-key review** (`FR-STU-QZ-009`, contract §B/§D) — the **only** student
 * surface that reveals quiz correctness, and only for the caller's own **terminal** attempt. The
 * prototype has no review screen, so this is a new student screen mirroring the admin/S4 option treatment
 * (re-implemented, never imported): the **correct** option green-checked, the student's **wrong** pick
 * red, a per-question right/wrong pill, plus the score + time + the attempt flag.
 *
 * Keyed directly by `attemptId` (the §B read). A **`403 quiz_attempt_in_progress`** (the deep-link edge —
 * the intro only links terminal rows) renders a friendly "finish first" panel + a "Continue quiz" button;
 * a **`404`** routes back to `/sessions/{id}`. Read-only — the attempt is terminal + immutable.
 */
@Component({
  selector: 'sb-quiz-review',
  standalone: true,
  imports: [ButtonComponent, LatexPreviewComponent, StatusPillComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="qrev">
      <button type="button" class="qrev__back" (click)="back()">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <line x1="19" y1="12" x2="5" y2="12" /><polyline points="12 19 5 12 12 5" />
        </svg>
        Back to session
      </button>

      @if (loading()) {
        <div class="qrev__skeleton" aria-hidden="true"></div>
      } @else if (finishFirst(); as msg) {
        <div class="qrev__gate">
          <img src="/assets/salah-prerequisite.png" alt="" aria-hidden="true" />
          <h2 class="qrev__gate-title">Almost there</h2>
          <p class="qrev__gate-text">{{ msg }}</p>
          <sb-button variant="primary" (clicked)="continueQuiz()">Continue quiz</sb-button>
        </div>
      } @else if (error()) {
        <div class="qrev__gate">
          <h2 class="qrev__gate-title">We couldn’t open your review</h2>
          <p class="qrev__gate-text">{{ error() }}</p>
          <sb-button variant="primary" (clicked)="reload()">Try again</sb-button>
        </div>
      } @else if (review(); as r) {
        <header class="qrev__head">
          <div class="qrev__titles">
            <div class="qrev__crumb">Quiz review</div>
            <h1 class="qrev__title">{{ headerTitle() }}</h1>
            <div class="qrev__attempt">Attempt {{ r.number }}</div>
          </div>
          <div class="qrev__stats">
            <div class="qrev__stat">
              <div class="qrev__stat-val qrev__stat-val--score">{{ r.scorePercent }}%</div>
              <div class="qrev__stat-lbl">Score</div>
            </div>
            <div class="qrev__stat">
              <div class="qrev__stat-val">{{ time(r.timeSpentSeconds) }}</div>
              <div class="qrev__stat-lbl">Time</div>
            </div>
            <div class="qrev__stat qrev__stat--pills">
              <sb-status-pill [variant]="passed() ? 'success' : 'danger'">
                {{ passed() ? 'Passed' : 'Below pass' }}
              </sb-status-pill>
              <sb-status-pill [variant]="flagVariant()">{{ flag() }}</sb-status-pill>
            </div>
          </div>
        </header>

        <div class="qrev__qs">
          @for (q of r.questions; track q.id) {
            <article class="qrev-q">
              <div class="qrev-q__head">
                <div class="qrev-q__title">
                  <span class="qrev-q__num">Question {{ q.order }}</span>
                  @if (q.bodyLatex) { <sb-latex-preview class="qrev-q__body" [latex]="q.bodyLatex" /> }
                </div>
                <span class="qrev-q__pill">
                  <sb-status-pill [variant]="q.isCorrect ? 'success' : 'danger'">
                    {{ q.isCorrect ? '+' + q.mark : '0' }}
                  </sb-status-pill>
                </span>
              </div>

              @if (q.imageUrl) {
                <img class="qrev-q__img" [src]="q.imageUrl" alt="Figure for question {{ q.order }}" />
              }

              <div class="qrev-q__opts">
                @for (o of q.options; track o.id; let oi = $index) {
                  @let st = optState(o, q.selectedOptionId);
                  <div class="qrev-opt" [attr.data-state]="st">
                    <span class="qrev-opt__key">{{ letter(oi) }}</span>
                    <sb-latex-preview class="qrev-opt__text" [latex]="o.text" />
                    @if (st === 'correct') {
                      <span class="qrev-opt__mark qrev-opt__mark--ok">
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                             stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                          <polyline points="20 6 9 17 4 12" />
                        </svg>
                        <span class="qrev-sr">Correct answer</span>
                      </span>
                    } @else if (st === 'picked-wrong') {
                      <span class="qrev-opt__mark qrev-opt__mark--bad">
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                             stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                          <path d="M18 6 6 18M6 6l12 12" />
                        </svg>
                        <span class="qrev-sr">Your answer — incorrect</span>
                      </span>
                    }
                  </div>
                }
              </div>
            </article>
          }
        </div>
      }
    </section>
  `,
  styles: [`
    .qrev { display: flex; flex-direction: column; gap: var(--sb-space-4); }

    .qrev__back { display: inline-flex; align-items: center; gap: 6px; align-self: flex-start; background: none; border: none; color: var(--sb-primary-600); font-family: inherit; font-weight: 700; font-size: 14px; cursor: pointer; padding: 4px 0; }
    .qrev__back:hover { color: var(--sb-primary-700); }
    .qrev__back:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); border-radius: var(--sb-radius-sm); }

    .qrev__skeleton { height: 220px; border-radius: 18px; background: var(--sb-surface-sunken); animation: qrev-pulse 1.3s var(--sb-easing-standard) infinite; }
    @keyframes qrev-pulse { 0%, 100% { opacity: 1; } 50% { opacity: .55; } }

    .qrev__gate { text-align: center; padding: var(--sb-space-10) var(--sb-space-5); background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-xl); }
    .qrev__gate img { width: 120px; }
    .qrev__gate-title { margin: 8px 0 4px; font-weight: 800; }
    .qrev__gate-text { margin: 0 auto 14px; max-width: 380px; color: var(--sb-text-muted); }

    .qrev__head { display: flex; align-items: center; justify-content: space-between; gap: var(--sb-space-4); flex-wrap: wrap; background: linear-gradient(135deg, var(--sb-subject-purple-bg), var(--sb-surface)); border: 1px solid var(--sb-border); border-radius: 18px; padding: 18px 20px; }
    .qrev__crumb { font-size: 12px; font-weight: 800; text-transform: uppercase; letter-spacing: .6px; color: var(--sb-subject-purple-deep); }
    .qrev__title { margin: 2px 0 0; font-weight: 800; font-size: 22px; letter-spacing: -.3px; }
    .qrev__attempt { font-size: 13px; color: var(--sb-text-muted); font-weight: 700; margin-top: 2px; }
    .qrev__stats { display: flex; gap: var(--sb-space-5); align-items: center; flex-wrap: wrap; }
    .qrev__stat { text-align: center; }
    .qrev__stat--pills { display: flex; flex-direction: column; gap: 6px; }
    .qrev__stat-val { font-size: 22px; font-weight: 800; color: var(--sb-text); line-height: 1; font-variant-numeric: tabular-nums; }
    .qrev__stat-val--score { color: var(--sb-subject-purple-deep); }
    .qrev__stat-lbl { font-size: 12px; color: var(--sb-text-muted); margin-top: 4px; font-weight: 600; }

    .qrev__qs { display: flex; flex-direction: column; gap: var(--sb-space-3); }
    .qrev-q { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: 16px; padding: 18px; }
    .qrev-q__head { display: flex; justify-content: space-between; gap: var(--sb-space-3); margin-bottom: var(--sb-space-3); }
    .qrev-q__title { display: flex; flex-direction: column; gap: 4px; flex: 1; min-width: 0; }
    .qrev-q__num { font-size: 12px; font-weight: 800; text-transform: uppercase; letter-spacing: .6px; color: var(--sb-text-muted); }
    .qrev-q__body { font-weight: 600; }
    .qrev-q__pill { flex-shrink: 0; }
    .qrev-q__img { display: block; max-width: 320px; width: 100%; height: auto; margin-bottom: var(--sb-space-3); border-radius: var(--sb-radius-md); border: 1px solid var(--sb-border); }

    .qrev-q__opts { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: var(--sb-space-2); }
    .qrev-opt { display: flex; align-items: center; gap: 10px; padding: 9px 12px; border-radius: var(--sb-radius-md); border: 1px solid var(--sb-border); background: var(--sb-surface); }
    .qrev-opt[data-state='correct'] { background: var(--sb-success-bg); border-color: var(--sb-success-border); }
    .qrev-opt[data-state='picked-wrong'] { background: var(--sb-danger-bg); border-color: var(--sb-danger-border); }
    .qrev-opt__key { flex-shrink: 0; width: 24px; height: 24px; border-radius: 6px; display: inline-flex; align-items: center; justify-content: center; font-weight: 800; font-size: 12px; background: var(--sb-neutral-100); color: var(--sb-text-muted); }
    .qrev-opt[data-state='correct'] .qrev-opt__key { background: var(--sb-success-fg); color: #fff; }
    .qrev-opt[data-state='picked-wrong'] .qrev-opt__key { background: var(--sb-danger-fg); color: #fff; }
    .qrev-opt__text { flex: 1; min-width: 0; font-size: 14px; }
    .qrev-opt__mark { display: inline-flex; align-items: center; flex-shrink: 0; }
    .qrev-opt__mark--ok { color: var(--sb-success-fg); }
    .qrev-opt__mark--bad { color: var(--sb-danger-fg); }

    .qrev-sr { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0,0,0,0); white-space: nowrap; border: 0; }

    @media (max-width: 560px) {
      .qrev__head { flex-direction: column; align-items: flex-start; }
      .qrev__stats { gap: var(--sb-space-4); }
    }
  `],
})
export class QuizReviewComponent {
  readonly #service = inject(QuizService);
  readonly #router = inject(Router);

  /** The session id, bound from `/sessions/:id/quiz/attempts/:attemptId/review` (input binding). */
  readonly id = input.required<string>();
  /** The attempt id (the §B key) — bound from the same route. */
  readonly attemptId = input.required<string>();

  readonly review = signal<StudentQuizAttemptReview | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  /** The server `detail` for a `403 quiz_attempt_in_progress` (the deep-link "finish first" panel). */
  readonly finishFirst = signal<string | null>(null);

  readonly headerTitle = computed(() => {
    const t = this.review()?.sessionTitle;
    return t ? `${t} · Quiz review` : 'Quiz review';
  });
  /** Per-attempt pass/fail, derived client-side (`scorePercent >= minPassPercent`, §B.1). */
  readonly passed = computed(() => {
    const r = this.review();
    return r ? r.scorePercent >= r.minPassPercent : false;
  });
  readonly flag = computed(() => quizFlagFromStatus(this.review()?.status ?? 'Submitted'));
  readonly flagVariant = computed(() => quizFlagVariant(this.flag()));

  constructor() {
    effect(() => {
      const attemptId = this.attemptId();
      if (attemptId) this.#load(attemptId);
    });
  }

  letter(index: number): string {
    return optionLetter(index);
  }

  time(seconds: number): string {
    return mmss(seconds);
  }

  optState(option: StudentQuizReviewOption, selectedOptionId: string | null): OptionState {
    if (option.isCorrect) return 'correct';
    if (selectedOptionId != null && option.id === selectedOptionId) return 'picked-wrong';
    return 'neutral';
  }

  reload(): void {
    this.#load(this.attemptId());
  }

  back(): void {
    void this.#router.navigate(['/sessions', this.id()]);
  }

  continueQuiz(): void {
    void this.#router.navigate(['/sessions', this.id(), 'quiz']);
  }

  #load(attemptId: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.finishFirst.set(null);
    this.review.set(null);

    // Keyed directly by attemptId (the §B read) — no session→id derivation (unlike the assignment review).
    this.#service.review(attemptId).subscribe({
      next: (r) => {
        this.review.set(r);
        this.loading.set(false);
      },
      error: (err: unknown) => this.#onError(err),
    });
  }

  #onError(err: unknown): void {
    this.loading.set(false);
    if (err instanceof HttpErrorResponse) {
      const body = err.error as { reason?: string; detail?: string } | null;
      if (err.status === 403 && body?.reason === 'quiz_attempt_in_progress') {
        // The deep-link edge — surface the server's friendly message, not an error (§B.2).
        this.finishFirst.set(body.detail ?? 'Finish the quiz to see your answers and score.');
        return;
      }
      if (err.status === 404) {
        // Unknown / another student's / another tenant's → route back (§B.2).
        void this.#router.navigate(['/sessions', this.id()]);
        return;
      }
    }
    this.error.set('We couldn’t load your review. Please try again.');
  }
}
