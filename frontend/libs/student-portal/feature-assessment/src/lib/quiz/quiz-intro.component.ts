import { DOCUMENT } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { AlertComponent, ButtonComponent, StatusPillComponent } from '@sb/shared/ui';
import { QuizAttempt, QuizService, StudentQuiz } from '@sb/student-portal/data-access';
import { QuizRunnerComponent } from './quiz-runner.component';
import { mmss, quizFlagVariant } from '../assessment.util';

/**
 * The informed **Quiz intro** + the runner host (`FR-STU-QZ-001/002`, contract §C, the prototype's
 * `screen === 'quizIntro'`). The **routed** component at `/sessions/:id/quiz`: it loads the gating quiz
 * **by session** (#1), renders the rules (time limit, question count, attempts remaining, best score, the
 * **one-sitting** warning), and offers **Start** (`attemptsRemaining > 0 && activeAttemptId == null`) or
 * **Resume** (an active attempt). **Start** mints the attempt via `start()` (#2) and flips to the
 * {@link QuizRunnerComponent} **in the same page** — the live questions are carried in memory (there is no
 * "GET live attempt" route, §A/§A.1). Each **terminal** attempt row deep-links the §B answer-key review.
 *
 * Hosting the runner here (a single route, no mid-attempt route change) means this component's
 * **CanDeactivate** ({@link canLeave}) delegates to the runner's leave modal so an in-app nav away during a
 * sitting raises the **"Leave the quiz?"** confirm (the hub teardown = forfeit, §A.1).
 */
@Component({
  selector: 'sb-quiz-intro',
  standalone: true,
  imports: [AlertComponent, ButtonComponent, StatusPillComponent, RouterLink, QuizRunnerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <div class="qi__skeleton" aria-hidden="true"></div>
    } @else if (phase() === 'run' && attempt(); as a) {
      <sb-quiz-runner [attempt]="a" [sessionId]="id()" [sessionTitle]="titleLine" />
    } @else if (quiz(); as q) {
      <button type="button" class="qi__back" (click)="back()">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <line x1="19" y1="12" x2="5" y2="12" /><polyline points="12 19 5 12 12 5" />
        </svg>
        Back to session
      </button>

      <div class="qi__card">
        <div class="qi__head">
          <span class="qi__head-icon" aria-hidden="true">
            <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="1.8"
                 stroke-linecap="round" stroke-linejoin="round">
              <path d="M9 11l3 3L22 4" /><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11" />
            </svg>
          </span>
          <h1 class="qi__title">{{ titleLine }}</h1>
          <p class="qi__sub">Pass to unlock this session’s videos</p>
        </div>

        <div class="qi__body">
          <div class="qi__tiles">
            <div class="qi__tile">
              <div class="qi__tile-val">{{ timeLimitLabel() }}</div>
              <div class="qi__tile-lbl">Time limit</div>
            </div>
            <div class="qi__tile">
              <div class="qi__tile-val">{{ q.attemptsRemaining }}</div>
              <div class="qi__tile-lbl">Attempts left</div>
            </div>
            <div class="qi__tile">
              <div class="qi__tile-val">{{ bestLabel() }}</div>
              <div class="qi__tile-lbl">Best score</div>
            </div>
          </div>

          <ul class="qi__rules">
            <li>
              <span class="qi__rule-tick" aria-hidden="true">✓</span>
              <span><strong>{{ q.settings.questionCount }}</strong> questions, randomly drawn and shuffled for every attempt.</span>
            </li>
            <li>
              <span class="qi__rule-tick" aria-hidden="true">✓</span>
              <span>Pass mark is <strong>{{ q.settings.minPassPercent }}%</strong>. Your best-of score counts.</span>
            </li>
          </ul>

          <div class="qi__alert">
            <sb-alert variant="danger" title="One sitting only">
              Closing the tab, navigating away, or losing connection <strong>forfeits</strong> the attempt
              with a zero and uses one of your limited attempts. Switching windows is logged for staff review.
            </sb-alert>
          </div>

          @if (startError(); as e) { <p class="qi__err">{{ e }}</p> }
          @if (couldNotResume()) {
            <p class="qi__err">
              We couldn’t resume your attempt here — reopen the session to continue.
            </p>
          }

          @switch (ctaMode()) {
            @case ('start') {
              <sb-button variant="primary" size="lg" [loading]="starting()" (clicked)="start()">
                Start attempt
              </sb-button>
            }
            @case ('resume') {
              <sb-button variant="primary" size="lg" (clicked)="resume()">Resume attempt</sb-button>
            }
            @case ('review') {
              <sb-button variant="primary" size="lg" (clicked)="reviewBest()">Review quiz</sb-button>
            }
            @default {
              <sb-button variant="secondary" size="lg" [disabled]="true">No attempts left</sb-button>
            }
          }
        </div>
      </div>

      @if (q.attempts.length) {
        <div class="qi__history">
          <h2 class="qi__history-title">Your attempts</h2>
          <ul class="qi__rows">
            @for (a of q.attempts; track a.id) {
              <li class="qi__row">
                <span class="qi__row-num">Attempt {{ a.number }}</span>
                <span class="qi__row-score">{{ a.scorePercent != null ? a.scorePercent + '%' : '—' }}</span>
                <sb-status-pill [variant]="flagVariant(a.flag)">{{ a.flag }}</sb-status-pill>
                @if (a.status !== 'InProgress') {
                  <a
                    class="qi__row-review"
                    [routerLink]="['/sessions', id(), 'quiz', 'attempts', a.id, 'review']"
                  >Review</a>
                } @else {
                  <span class="qi__row-active">In progress</span>
                }
              </li>
            }
          </ul>
        </div>
      }
    }
  `,
  styles: [`
    .qi__skeleton { height: 320px; border-radius: 20px; background: var(--sb-surface-sunken); animation: qi-pulse 1.3s var(--sb-easing-standard) infinite; }
    @keyframes qi-pulse { 0%, 100% { opacity: 1; } 50% { opacity: .55; } }

    .qi__back { display: inline-flex; align-items: center; gap: 6px; background: none; border: none; color: var(--sb-primary-600); font-family: inherit; font-weight: 700; font-size: 14px; cursor: pointer; padding: 4px 0; margin-bottom: 16px; }
    .qi__back:hover { color: var(--sb-primary-700); }
    .qi__back:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); border-radius: var(--sb-radius-sm); }

    .qi__card { max-width: 560px; margin: 0 auto; background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: 20px; overflow: hidden; box-shadow: var(--sb-shadow-md); }
    .qi__head { background: linear-gradient(135deg, var(--sb-subject-purple-deep), #4A2A72); padding: 28px; color: #fff; text-align: center; }
    .qi__head-icon { display: inline-flex; margin-bottom: 8px; }
    .qi__title { margin: 0; font-weight: 800; font-size: 23px; letter-spacing: -.3px; }
    .qi__sub { margin: 4px 0 0; opacity: .85; font-size: 14px; }

    .qi__body { padding: 24px; }
    .qi__tiles { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; margin-bottom: 20px; }
    .qi__tile { text-align: center; background: var(--sb-surface-sunken); border: 1px solid var(--sb-border); border-radius: 13px; padding: 14px 8px; }
    .qi__tile-val { font-family: var(--sb-font-mono); font-weight: 700; font-size: 22px; color: var(--sb-subject-purple-deep); }
    .qi__tile-lbl { font-size: 11px; color: var(--sb-text-muted); font-weight: 700; text-transform: uppercase; letter-spacing: .3px; margin-top: 2px; }

    .qi__rules { list-style: none; margin: 0 0 20px; padding: 0; display: flex; flex-direction: column; gap: 10px; }
    .qi__rules li { display: flex; gap: 10px; align-items: flex-start; font-size: 14px; color: var(--sb-text-muted); line-height: 1.5; }
    .qi__rule-tick { color: var(--sb-subject-green-deep); font-weight: 800; }

    .qi__alert { margin-bottom: 20px; }
    .qi__err { color: var(--sb-danger-fg); font-size: 13px; font-weight: 600; margin: 0 0 14px; }
    .qi__body sb-button { display: block; }
    .qi__body sb-button ::ng-deep .sb-btn { width: 100%; }

    .qi__history { max-width: 560px; margin: 20px auto 0; }
    .qi__history-title { font-weight: 800; font-size: 15px; margin: 0 0 10px; }
    .qi__rows { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 8px; }
    .qi__row { display: flex; align-items: center; gap: 12px; background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: 12px; padding: 12px 14px; }
    .qi__row-num { font-weight: 700; font-size: 14px; }
    .qi__row-score { font-family: var(--sb-font-mono); font-weight: 700; font-size: 14px; color: var(--sb-text); margin-left: auto; }
    .qi__row-review { color: var(--sb-primary-600); font-weight: 700; font-size: 13px; text-decoration: none; }
    .qi__row-review:hover { text-decoration: underline; }
    .qi__row-active { color: var(--sb-text-subtle); font-size: 12px; font-weight: 700; }

    @media (max-width: 560px) {
      .qi__tiles { gap: 8px; }
    }
  `],
})
export class QuizIntroComponent {
  readonly #service = inject(QuizService);
  readonly #router = inject(Router);
  readonly #document = inject(DOCUMENT);

  /** The session id, bound from `/sessions/:id/quiz` (`withComponentInputBinding`). */
  readonly id = input.required<string>();

  readonly quiz = signal<StudentQuiz | null>(null);
  readonly loading = signal(true);
  /** `'intro'` (the rules card) or `'run'` (the runner takes over, same route — no mid-attempt nav). */
  readonly phase = signal<'intro' | 'run'>('intro');
  readonly attempt = signal<QuizAttempt | null>(null);
  readonly starting = signal(false);
  /** The server `detail` for a `409` Start race (exhausted / already active). */
  readonly startError = signal<string | null>(null);
  /** A reload lost the in-memory attempt — show "reopen the session", never silently re-`start` (§A.1). */
  readonly couldNotResume = signal(false);

  /** Session title from S3 nav state — carried to the runner/results header when the DTO lacks it. */
  readonly titleLine: string;

  /** The live runner instance (when `phase === 'run'`) — the CanDeactivate guard delegates to it. */
  private readonly runner = viewChild(QuizRunnerComponent);

  readonly timeLimitLabel = computed(() => mmss((this.quiz()?.settings.timeLimitMinutes ?? 0) * 60));
  readonly bestLabel = computed(() => {
    const b = this.quiz()?.bestPercent;
    return b != null ? `${b}%` : '—';
  });

  /** The primary CTA: resume an active sitting > start a fresh one > review (passed, no attempts) > none. */
  readonly ctaMode = computed<'start' | 'resume' | 'review' | 'none'>(() => {
    const q = this.quiz();
    if (!q) return 'none';
    if (q.activeAttemptId != null) return 'resume';
    if (q.attemptsRemaining > 0) return 'start';
    if (q.passed) return 'review';
    return 'none';
  });

  constructor() {
    const state = this.#document.defaultView?.history.state as { sessionTitle?: string } | null;
    this.titleLine = state?.sessionTitle ? `${state.sessionTitle} — Quiz` : 'Prerequisite quiz';

    // Reload whenever the bound route id changes (router input binding sets it before first CD).
    effect(() => {
      const id = this.id();
      if (id) this.#load(id);
    });
  }

  flagVariant(flag: 'Clean' | 'Timeout' | 'Forfeit') {
    return quizFlagVariant(flag);
  }

  back(): void {
    void this.#router.navigate(['/sessions', this.id()]);
  }

  /** Mint a fresh attempt (#2), then flip to the runner with the live questions in memory (§A/§A.1). */
  start(): void {
    const q = this.quiz();
    if (!q || this.starting() || q.attemptsRemaining <= 0 || q.activeAttemptId != null) return;
    this.starting.set(true);
    this.startError.set(null);
    this.couldNotResume.set(false);
    this.#service.start(q.id).subscribe({
      next: (a) => {
        this.starting.set(false);
        this.attempt.set(a);
        this.phase.set('run');
      },
      error: (err: unknown) => {
        this.starting.set(false);
        if (err instanceof HttpErrorResponse && err.status === 409) {
          // Raced into an active/exhausted state — re-read so the intro offers Resume / the right CTA.
          this.startError.set(
            this.#detail(err) ?? 'You already have an attempt in progress.',
          );
          this.#load(this.id());
          return;
        }
        this.startError.set('We couldn’t start your attempt. Please try again.');
      },
    });
  }

  /**
   * Re-enter the runner on an active attempt. Resume is **best-effort within the page session** — if the
   * live questions were lost (a full reload), we **never** silently re-`start` (that `409`s); surface
   * "couldn't resume" instead (§A.1).
   */
  resume(): void {
    if (this.attempt()) {
      this.phase.set('run');
      return;
    }
    this.couldNotResume.set(true);
  }

  /** "Review quiz" (passed, no attempts left) → the best terminal attempt's §B review. */
  reviewBest(): void {
    const id = this.#bestTerminalAttemptId();
    if (id) void this.#router.navigate(['/sessions', this.id(), 'quiz', 'attempts', id, 'review']);
  }

  /** CanDeactivate hook (the route guard): block an in-app leave mid-sitting behind the runner's modal. */
  canLeave(): boolean | Observable<boolean> {
    if (this.phase() !== 'run') return true;
    const runner = this.runner();
    return runner ? runner.attemptLeave() : true;
  }

  #bestTerminalAttemptId(): string | null {
    const terminal = (this.quiz()?.attempts ?? []).filter((a) => a.status !== 'InProgress');
    if (!terminal.length) return null;
    return [...terminal].sort((a, b) => (b.scorePercent ?? 0) - (a.scorePercent ?? 0))[0].id;
  }

  #load(sessionId: string): void {
    this.loading.set(true);
    this.#service.quiz(sessionId).subscribe({
      next: (q) => {
        this.quiz.set(q);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        if (err instanceof HttpErrorResponse && err.status === 404) {
          // The session has no quiz (no prerequisite / no settings) — route back, not a hard error (§A #1).
          void this.#router.navigate(['/sessions', sessionId]);
          return;
        }
        // A transient load failure leaves the skeleton off with no quiz — re-entering the route retries.
        this.startError.set('We couldn’t load the quiz. Please try again.');
      },
    });
  }

  #detail(err: HttpErrorResponse): string | null {
    return (err.error as { detail?: string } | null)?.detail ?? null;
  }
}
