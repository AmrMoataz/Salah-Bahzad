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
  AssignmentService,
  StudentAssignmentReview,
  StudentReviewOption,
} from '@sb/student-portal/data-access';
import { mmss, optionLetter } from '../assessment.util';

type OptionState = 'correct' | 'picked-wrong' | 'neutral';

/**
 * The **answer-key review** screen (`FR-STU-ASG-007`, contract §B/§D) — the **only** student surface
 * that reveals correctness, and only for the caller's own **`Completed`** assignment. The prototype
 * has no review screen, so this is a **new** student screen that mirrors the admin
 * `AssignmentReviewComponent`'s option treatment (re-implemented, never imported): the **correct**
 * option marked with a green check, the student's **wrong** pick marked red, a per-question
 * right/wrong pill (`+{mark}` / `0`), and the overall score + time.
 *
 * It is keyed by the **userAssignment id**, which it derives from the session: it loads
 * `assignment(sessionId)` (deep-link-safe) and then `review(assignment.id)`. A **`403`
 * `assignment_in_progress`** (the deep-link edge — the S3 "Review assignment" CTA only shows when
 * `Completed`) renders a friendly "finish first" panel; a **`404`** routes back to `/sessions/{id}`.
 * Read-only — the review never re-`PUT`s an answer (the assignment is `Completed` + immutable).
 */
@Component({
  selector: 'sb-assignment-review',
  standalone: true,
  imports: [ButtonComponent, LatexPreviewComponent, StatusPillComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="rev">
      <button type="button" class="rev__back" (click)="back()">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <line x1="19" y1="12" x2="5" y2="12" /><polyline points="12 19 5 12 12 5" />
        </svg>
        Back to session
      </button>

      @if (loading()) {
        <div class="rev__skeleton" aria-hidden="true"></div>
      } @else if (finishFirst(); as msg) {
        <div class="rev__gate">
          <img src="/assets/salah-mascot.png" alt="" aria-hidden="true" />
          <h2 class="rev__gate-title">Almost there</h2>
          <p class="rev__gate-text">{{ msg }}</p>
          <sb-button variant="primary" (clicked)="continueAssignment()">Continue assignment</sb-button>
        </div>
      } @else if (error()) {
        <div class="rev__gate">
          <h2 class="rev__gate-title">We couldn’t open your review</h2>
          <p class="rev__gate-text">{{ error() }}</p>
          <sb-button variant="primary" (clicked)="reload()">Try again</sb-button>
        </div>
      } @else if (review(); as r) {
        <header class="rev__head">
          <div class="rev__titles">
            <div class="rev__crumb">Assignment review</div>
            <h1 class="rev__title">{{ headerTitle() }}</h1>
          </div>
          <div class="rev__stats">
            <div class="rev__stat">
              <div class="rev__stat-val rev__stat-val--score">{{ r.percent }}%</div>
              <div class="rev__stat-lbl">Score</div>
            </div>
            <div class="rev__stat">
              <div class="rev__stat-val">{{ r.scoreMarks }}/{{ r.maxMarks }}</div>
              <div class="rev__stat-lbl">Marks</div>
            </div>
            <div class="rev__stat">
              <div class="rev__stat-val">{{ r.correctCount }}/{{ r.questionCount }}</div>
              <div class="rev__stat-lbl">Correct</div>
            </div>
            <div class="rev__stat">
              <div class="rev__stat-val">{{ time(r.timeSpentSeconds) }}</div>
              <div class="rev__stat-lbl">Time</div>
            </div>
          </div>
        </header>

        <div class="rev__qs">
          @for (q of r.questions; track q.id) {
            <article class="rev-q">
              <div class="rev-q__head">
                <div class="rev-q__title">
                  <span class="rev-q__num">Question {{ q.order }}</span>
                  @if (q.bodyLatex) { <sb-latex-preview class="rev-q__body" [latex]="q.bodyLatex" /> }
                </div>
                <span class="rev-q__pill">
                  <sb-status-pill [variant]="q.isCorrect ? 'success' : 'danger'">
                    {{ q.isCorrect ? '+' + q.mark : '0' }}
                  </sb-status-pill>
                </span>
              </div>

              @if (q.imageUrl) {
                <img class="rev-q__img" [src]="q.imageUrl" alt="Figure for question {{ q.order }}" />
              }

              <div class="rev-q__opts">
                @for (o of q.options; track o.id; let oi = $index) {
                  @let st = optState(o, q.selectedOptionId);
                  <div class="rev-opt" [attr.data-state]="st">
                    <span class="rev-opt__key">{{ letter(oi) }}</span>
                    <sb-latex-preview class="rev-opt__text" [latex]="o.text" />
                    @if (st === 'correct') {
                      <span class="rev-opt__mark rev-opt__mark--ok">
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                             stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                          <polyline points="20 6 9 17 4 12" />
                        </svg>
                        <span class="rev-sr">Correct answer</span>
                      </span>
                    } @else if (st === 'picked-wrong') {
                      <span class="rev-opt__mark rev-opt__mark--bad">
                        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                             stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                          <path d="M18 6 6 18M6 6l12 12" />
                        </svg>
                        <span class="rev-sr">Your answer — incorrect</span>
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
    .rev { display: flex; flex-direction: column; gap: var(--sb-space-4); }

    .rev__back { display: inline-flex; align-items: center; gap: 6px; align-self: flex-start; background: none; border: none; color: var(--sb-primary-600); font-family: inherit; font-weight: 700; font-size: 14px; cursor: pointer; padding: 4px 0; }
    .rev__back:hover { color: var(--sb-primary-700); }
    .rev__back:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); border-radius: var(--sb-radius-sm); }

    .rev__skeleton { height: 220px; border-radius: 18px; background: var(--sb-surface-sunken); animation: rev-pulse 1.3s var(--sb-easing-standard) infinite; }
    @keyframes rev-pulse { 0%, 100% { opacity: 1; } 50% { opacity: .55; } }

    .rev__gate { text-align: center; padding: var(--sb-space-10) var(--sb-space-5); background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-xl); }
    .rev__gate img { width: 120px; }
    .rev__gate-title { margin: 8px 0 4px; font-weight: 800; }
    .rev__gate-text { margin: 0 auto 14px; max-width: 380px; color: var(--sb-text-muted); }

    .rev__head { display: flex; align-items: center; justify-content: space-between; gap: var(--sb-space-4); flex-wrap: wrap; background: linear-gradient(135deg, var(--sb-primary-50), var(--sb-surface)); border: 1px solid var(--sb-border); border-radius: 18px; padding: 18px 20px; }
    .rev__crumb { font-size: 12px; font-weight: 800; text-transform: uppercase; letter-spacing: .6px; color: var(--sb-subject-green-deep); }
    .rev__title { margin: 2px 0 0; font-weight: 800; font-size: 22px; letter-spacing: -0.3px; }
    .rev__stats { display: flex; gap: var(--sb-space-5); }
    .rev__stat { text-align: center; }
    .rev__stat-val { font-size: 22px; font-weight: 800; color: var(--sb-text); line-height: 1; font-variant-numeric: tabular-nums; }
    .rev__stat-val--score { color: var(--sb-primary); }
    .rev__stat-lbl { font-size: 12px; color: var(--sb-text-muted); margin-top: 4px; font-weight: 600; }

    .rev__qs { display: flex; flex-direction: column; gap: var(--sb-space-3); }
    .rev-q { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: 16px; padding: 18px; }
    .rev-q__head { display: flex; justify-content: space-between; gap: var(--sb-space-3); margin-bottom: var(--sb-space-3); }
    .rev-q__title { display: flex; flex-direction: column; gap: 4px; flex: 1; min-width: 0; }
    .rev-q__num { font-size: 12px; font-weight: 800; text-transform: uppercase; letter-spacing: .6px; color: var(--sb-text-muted); }
    .rev-q__body { font-weight: 600; }
    .rev-q__pill { flex-shrink: 0; }
    .rev-q__img { display: block; max-width: 320px; width: 100%; height: auto; margin-bottom: var(--sb-space-3); border-radius: var(--sb-radius-md); border: 1px solid var(--sb-border); }

    .rev-q__opts { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: var(--sb-space-2); }
    .rev-opt { display: flex; align-items: center; gap: 10px; padding: 9px 12px; border-radius: var(--sb-radius-md); border: 1px solid var(--sb-border); background: var(--sb-surface); }
    .rev-opt[data-state='correct'] { background: var(--sb-success-bg); border-color: var(--sb-success-border); }
    .rev-opt[data-state='picked-wrong'] { background: var(--sb-danger-bg); border-color: var(--sb-danger-border); }
    .rev-opt__key { flex-shrink: 0; width: 24px; height: 24px; border-radius: 6px; display: inline-flex; align-items: center; justify-content: center; font-weight: 800; font-size: 12px; background: var(--sb-neutral-100); color: var(--sb-text-muted); }
    .rev-opt[data-state='correct'] .rev-opt__key { background: var(--sb-success-fg); color: #fff; }
    .rev-opt[data-state='picked-wrong'] .rev-opt__key { background: var(--sb-danger-fg); color: #fff; }
    .rev-opt__text { flex: 1; min-width: 0; font-size: 14px; }
    .rev-opt__mark { display: inline-flex; align-items: center; flex-shrink: 0; }
    .rev-opt__mark--ok { color: var(--sb-success-fg); }
    .rev-opt__mark--bad { color: var(--sb-danger-fg); }

    .rev-sr { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0,0,0,0); white-space: nowrap; border: 0; }

    @media (max-width: 560px) {
      .rev__head { flex-direction: column; align-items: flex-start; }
      .rev__stats { gap: var(--sb-space-4); }
    }
  `],
})
export class AssignmentReviewComponent {
  readonly #service = inject(AssignmentService);
  readonly #router = inject(Router);

  /** The session id, bound from `/sessions/:id/assignment/review` (`withComponentInputBinding`). */
  readonly id = input.required<string>();

  readonly review = signal<StudentAssignmentReview | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  /** The server `detail` for a `403 assignment_in_progress` (the deep-link "finish first" panel). */
  readonly finishFirst = signal<string | null>(null);

  readonly headerTitle = computed(() => {
    const t = this.review()?.sessionTitle;
    return t ? `${t} · Assignment review` : 'Assignment review';
  });

  constructor() {
    effect(() => {
      const id = this.id();
      if (id) this.#load(id);
    });
  }

  letter(index: number): string {
    return optionLetter(index);
  }

  time(seconds: number): string {
    return mmss(seconds);
  }

  optState(option: StudentReviewOption, selectedOptionId: string | null): OptionState {
    if (option.isCorrect) return 'correct';
    if (selectedOptionId != null && option.id === selectedOptionId) return 'picked-wrong';
    return 'neutral';
  }

  reload(): void {
    this.#load(this.id());
  }

  back(): void {
    void this.#router.navigate(['/sessions', this.id()]);
  }

  continueAssignment(): void {
    void this.#router.navigate(['/sessions', this.id(), 'assignment']);
  }

  #load(sessionId: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.finishFirst.set(null);
    this.review.set(null);

    // The review read is keyed by the userAssignment id — derive it from the session (deep-link-safe).
    this.#service.assignment(sessionId).subscribe({
      next: (a) => {
        this.#service.review(a.id).subscribe({
          next: (r) => {
            this.review.set(r);
            this.loading.set(false);
          },
          error: (err: unknown) => this.#onReviewError(err, sessionId),
        });
      },
      error: (err: unknown) => {
        this.loading.set(false);
        if (err instanceof HttpErrorResponse && err.status === 404) {
          void this.#router.navigate(['/sessions', sessionId]);
          return;
        }
        this.error.set('Please check your connection and try again.');
      },
    });
  }

  #onReviewError(err: unknown, sessionId: string): void {
    this.loading.set(false);
    if (err instanceof HttpErrorResponse) {
      const body = err.error as { reason?: string; detail?: string } | null;
      if (err.status === 403 && body?.reason === 'assignment_in_progress') {
        // The deep-link edge — surface the server's friendly message, not an error (§B.2).
        this.finishFirst.set(body.detail ?? 'Finish the assignment to see your answers and score.');
        return;
      }
      if (err.status === 404) {
        // Unknown / another student's / another tenant's → route back (§B.2).
        void this.#router.navigate(['/sessions', sessionId]);
        return;
      }
    }
    this.error.set('We couldn’t load your review. Please try again.');
  }
}
