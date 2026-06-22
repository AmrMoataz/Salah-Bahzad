import { DOCUMENT } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  NgZone,
  OnDestroy,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { Observable, Subject, of } from 'rxjs';
import { AlertComponent, ButtonComponent, LatexPreviewComponent, ModalComponent } from '@sb/shared/ui';
import { QuizAttempt, QuizAttemptQuestion, QuizService } from '@sb/student-portal/data-access';
import { QuizHubClient } from './quiz-hub.client';
import { mmss, optionLetter } from '../assessment.util';

/** The dwell threshold under which the runner paints the countdown chrome red (the prototype's warnAt). */
const WARN_AT_SECONDS = 60;

/**
 * The proctored **Quiz runner** (`FR-STU-QZ-003..007`, contract §C, the prototype's `screen === 'quiz'`).
 * Renders the drawn `questions` from `start()` (#2) **one card at a time** with question dots + prev/next,
 * a **local countdown** (server-authoritative — the grade is the server's), **save-as-you-go** answers,
 * and a guarded exit.
 *
 * - **Hub-on-start (`FR-STU-QZ-004`):** opens the `QuizHub` (forfeit-on-disconnect) when the attempt
 *   arrives and tears it down on a clean submit / leave / destroy. The disconnect **is** the forfeit (§A.1).
 * - **Local countdown, server-authoritative (`FR-STU-QZ-003/006`):** seeded from `deadlineUtc − serverNowUtc`,
 *   ticked down via a `setInterval` started under **`NgZone.runOutsideAngular`** (the S4 gotcha — an in-zone
 *   recurring macrotask hangs `whenStable()`). On local-zero it calls `submit()`; a `409` (the Hangfire job
 *   already `TimedOut` it) re-reads `quiz(sessionId)` for the `TimedOut` summary → results, **never** an error.
 * - **Forfeit-on-leave (`FR-STU-QZ-004`):** a `beforeunload` guard arms the native prompt; an in-app nav
 *   away raises the **"Leave the quiz?"** modal via {@link attemptLeave} (the route's CanDeactivate guard) —
 *   **"Leave & forfeit"** tears down the hub → the server forfeits (score 0, consumed). Leaving **is** the forfeit.
 * - **Focus-loss recorded, not forfeited (`FR-STU-QZ-005`):** tab/window switches `POST …/focus` (#5) — telemetry
 *   only; **never** ends the attempt.
 *
 * This is a presentational child of {@link QuizIntroComponent} (fed the live `attempt` in-memory across the
 * intro→runner flip, §A/§A.1) — there is no "GET live attempt" route, so a full page reload mid-attempt is an
 * accepted forfeit by design (the hub drop on `unload` forfeits).
 */
@Component({
  selector: 'sb-quiz-runner',
  standalone: true,
  imports: [AlertComponent, ButtonComponent, LatexPreviewComponent, ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="qr">
      <header class="qr__bar">
        <span class="qr__pos">Question {{ posLabel() }}</span>
        <div
          class="qr__timer"
          [class.qr__timer--warn]="warn()"
          aria-live="off"
          [attr.aria-label]="'Time remaining ' + timeLabel()"
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <circle cx="12" cy="12" r="9" /><polyline points="12 7 12 12 15 14" />
          </svg>
          {{ timeLabel() }}
        </div>
      </header>

      @if (warn()) {
        <div class="qr__warn">
          <sb-alert variant="danger" title="Don’t leave this screen">
            Less than a minute left — finish and submit. Closing or leaving now forfeits the attempt.
          </sb-alert>
        </div>
      }
      @if (focusNoticeOpen()) {
        <div class="qr__warn">
          <sb-alert variant="warning" title="Tab switch logged">
            Leaving this tab is recorded for staff review. Stay on this screen to keep your attempt clean.
          </sb-alert>
        </div>
      }

      <div class="qr__dots" role="tablist" aria-label="Questions">
        @for (d of dots(); track d.index) {
          <button
            type="button"
            class="qr-dot"
            [attr.data-state]="d.state"
            role="tab"
            [attr.aria-selected]="d.current"
            [attr.aria-label]="'Question ' + (d.index + 1) + (d.answered ? ' (answered)' : '')"
            (click)="goTo(d.index)"
          ></button>
        }
      </div>

      @if (current(); as q) {
        <div class="qr__card">
          <div class="qr__qlabel">Question {{ q.order }}</div>
          @if (q.bodyLatex) { <sb-latex-preview class="qr__body" [latex]="q.bodyLatex" /> }
          @if (q.imageUrl) {
            <img class="qr__img" [src]="q.imageUrl" alt="Figure for question {{ q.order }}" />
          }

          <div class="qr__opts" role="radiogroup" [attr.aria-label]="'Question ' + q.order + ' options'">
            @for (o of q.options; track o.id; let oi = $index) {
              <button
                type="button"
                class="qr-opt"
                role="radio"
                [class.qr-opt--picked]="o.id === selectedFor(q.id)"
                [attr.aria-checked]="o.id === selectedFor(q.id)"
                [disabled]="submitting()"
                (click)="pick(q, o.id)"
              >
                <span class="qr-opt__key">{{ letter(oi) }}</span>
                <sb-latex-preview class="qr-opt__text" [latex]="o.text" />
                @if (o.id === selectedFor(q.id)) {
                  <svg class="qr-opt__check" width="20" height="20" viewBox="0 0 24 24" fill="none"
                       stroke="currentColor" stroke-width="2.5" stroke-linecap="round"
                       stroke-linejoin="round" aria-hidden="true">
                    <polyline points="20 6 9 17 4 12" />
                  </svg>
                }
              </button>
            }
          </div>
        </div>

        @if (submitError()) { <p class="qr__err">{{ submitError() }}</p> }

        <div class="qr__nav">
          <sb-button variant="secondary" size="lg" [disabled]="isFirst()" (clicked)="goPrev()">
            ← Previous
          </sb-button>
          <sb-button
            class="qr__primary"
            [variant]="isLast() ? 'primary' : 'secondary'"
            size="lg"
            [loading]="submitting()"
            (clicked)="onPrimary()"
          >
            {{ primaryLabel() }}
          </sb-button>
        </div>
      }
    </section>

    <sb-modal [open]="leaveModalOpen()" title="Leave the quiz?" size="confirm" (close)="stayInQuiz()">
      <div class="qr__leave">
        <img src="/assets/salah-prerequisite.png" alt="" aria-hidden="true" />
        <p>
          Leaving now <strong>forfeits this attempt</strong> and records a <strong>zero</strong>. It also
          counts as one of your limited attempts. There’s no way to resume.
        </p>
      </div>
      <div modalFooter class="qr__leave-actions">
        <sb-button variant="primary" (clicked)="stayInQuiz()">Stay in quiz</sb-button>
        <sb-button variant="danger" (clicked)="confirmLeave()">Leave &amp; forfeit</sb-button>
      </div>
    </sb-modal>
  `,
  styles: [`
    .qr { max-width: 760px; margin: 0 auto; display: flex; flex-direction: column; }

    .qr__bar { position: sticky; top: 0; z-index: 5; display: flex; align-items: center; justify-content: space-between; gap: 18px; background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: 14px; padding: 14px 18px; margin-bottom: 16px; box-shadow: var(--sb-shadow-sm); }
    .qr__pos { font-weight: 800; font-size: 15px; white-space: nowrap; }
    .qr__timer { display: inline-flex; align-items: center; gap: 8px; font-family: var(--sb-font-mono); font-weight: 700; font-size: 16px; color: var(--sb-text-muted); background: var(--sb-surface-sunken); border: 1px solid var(--sb-border); padding: 8px 14px; border-radius: var(--sb-radius-pill); font-variant-numeric: tabular-nums; }
    .qr__timer--warn { color: var(--sb-danger-fg); background: var(--sb-danger-bg); border-color: var(--sb-danger-border); }

    .qr__warn { margin-bottom: 14px; }

    .qr__dots { display: flex; gap: 7px; margin-bottom: 16px; flex-wrap: wrap; }
    .qr-dot { width: 26px; height: 8px; border-radius: var(--sb-radius-pill); border: none; background: var(--sb-neutral-100); cursor: pointer; padding: 0; transition: background var(--sb-timing-fast) var(--sb-easing-standard); }
    .qr-dot[data-state='answered'] { background: var(--sb-accent); }
    .qr-dot[data-state='current'] { background: var(--sb-primary); }
    .qr-dot:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }

    .qr__card { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: 18px; padding: 26px; box-shadow: var(--sb-shadow-sm); }
    .qr__qlabel { font-size: 12px; font-weight: 800; text-transform: uppercase; letter-spacing: .6px; color: var(--sb-subject-purple-deep); margin-bottom: 10px; }
    .qr__body { font-size: 18px; font-weight: 600; line-height: 1.5; margin-bottom: 6px; }
    .qr__img { display: block; max-width: 100%; height: auto; border-radius: var(--sb-radius-md); border: 1px solid var(--sb-border); margin: 12px 0; }

    .qr__opts { display: flex; flex-direction: column; gap: 10px; margin-top: 16px; }
    .qr-opt { display: flex; align-items: center; gap: 12px; width: 100%; text-align: left; padding: 12px 14px; border: 2px solid var(--sb-border); border-radius: var(--sb-radius-lg); background: var(--sb-surface); color: var(--sb-text); font-family: inherit; cursor: pointer; transition: border-color var(--sb-timing-fast) var(--sb-easing-standard), background var(--sb-timing-fast) var(--sb-easing-standard); }
    .qr-opt:hover:not(:disabled) { border-color: var(--sb-border-strong); }
    .qr-opt:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }
    .qr-opt:disabled { cursor: default; opacity: .8; }
    .qr-opt--picked { border-color: var(--sb-subject-purple-deep); background: var(--sb-subject-purple-bg); }
    .qr-opt__key { flex-shrink: 0; width: 30px; height: 30px; border-radius: 8px; display: inline-flex; align-items: center; justify-content: center; font-weight: 800; font-size: 14px; background: var(--sb-neutral-100); color: var(--sb-text-muted); }
    .qr-opt--picked .qr-opt__key { background: var(--sb-subject-purple-deep); color: #fff; }
    .qr-opt__text { flex: 1; font-size: 15px; font-weight: 600; }
    .qr-opt__check { flex-shrink: 0; color: var(--sb-subject-purple-deep); }

    .qr__err { color: var(--sb-danger-fg); font-size: 13px; font-weight: 600; margin: 12px 0 0; }

    .qr__nav { display: flex; gap: 12px; margin-top: 18px; }
    .qr__primary { flex: 1; }
    .qr__primary ::ng-deep .sb-btn { width: 100%; }

    .qr__leave { display: flex; gap: 16px; align-items: center; }
    .qr__leave img { width: 96px; flex-shrink: 0; }
    .qr__leave p { margin: 0; font-size: 14px; line-height: 1.55; color: var(--sb-text); }
    .qr__leave-actions { display: flex; gap: 12px; }
    .qr__leave-actions sb-button { flex: 1; }
    .qr__leave-actions sb-button ::ng-deep .sb-btn { width: 100%; }

    @media (max-width: 560px) {
      .qr__card { padding: 18px; }
      .qr__leave { flex-direction: column; text-align: center; }
    }
  `],
})
export class QuizRunnerComponent implements OnDestroy {
  readonly #service = inject(QuizService);
  readonly #hub = inject(QuizHubClient);
  readonly #router = inject(Router);
  readonly #document = inject(DOCUMENT);
  readonly #zone = inject(NgZone);

  /** The live attempt (questions, `deadlineUtc`, `serverNowUtc`) handed in from the intro flip (§A #2). */
  readonly attempt = input.required<QuizAttempt>();
  /** The session id (route context) — for answers, the 409-race re-read, and navigation. */
  readonly sessionId = input.required<string>();
  /** Session B's title (from S3 nav state) — carried to the results header. */
  readonly sessionTitle = input<string | null>(null);

  readonly index = signal(0);
  /** Local mirror of the saved pick per question (`aqId → optionId`) for the green highlight. */
  readonly selections = signal<Record<string, string>>({});
  /** Seconds left on the **local** countdown (server-authoritative; the Hangfire job is the backstop). */
  readonly remaining = signal(0);
  readonly submitting = signal(false);
  readonly submitError = signal<string | null>(null);
  readonly leaveModalOpen = signal(false);
  readonly focusNoticeOpen = signal(false);

  /** Set once the attempt is terminal (clean submit / forfeit) — guards double submit + the leave modal. */
  #terminal = false;
  /** Set the instant a clean navigation begins so the CanDeactivate guard waves us through. */
  #leaving = false;
  #initialized = false;
  #clock: ReturnType<Window['setInterval']> | undefined;
  #focusLostAt: string | null = null;
  #leaveSubject: Subject<boolean> | null = null;

  readonly questions = computed<QuizAttemptQuestion[]>(() => this.attempt().questions);
  readonly current = computed<QuizAttemptQuestion | null>(() => this.questions()[this.index()] ?? null);
  readonly total = computed(() => this.questions().length);
  readonly isFirst = computed(() => this.index() === 0);
  readonly isLast = computed(() => this.index() >= this.total() - 1);
  readonly posLabel = computed(() => `${this.index() + 1} of ${this.total()}`);
  readonly primaryLabel = computed(() => (this.isLast() ? 'Submit quiz' : 'Next'));
  readonly timeLabel = computed(() => mmss(this.remaining()));
  readonly warn = computed(() => this.remaining() > 0 && this.remaining() <= WARN_AT_SECONDS);

  readonly dots = computed(() =>
    this.questions().map((q, i) => ({
      index: i,
      current: i === this.index(),
      answered: this.selections()[q.id] != null,
      state: i === this.index() ? 'current' : this.selections()[q.id] != null ? 'answered' : 'todo',
    })),
  );

  constructor() {
    // Arm the sitting once the attempt is bound (a single navigation; the input is set before first CD).
    effect(() => {
      const a = this.attempt();
      if (a && !this.#initialized) {
        this.#initialized = true;
        this.#startSitting(a);
      }
    });
  }

  ngOnDestroy(): void {
    // Any teardown (clean submit OR an un-confirmed navigation) drops the hub — a drop IS the forfeit
    // server-side (§A.1). Idempotent: a clean submit already closed it.
    this.#teardown();
  }

  letter(index: number): string {
    return optionLetter(index);
  }

  selectedFor(questionId: string): string | null {
    return this.selections()[questionId] ?? null;
  }

  goTo(i: number): void {
    if (i < 0 || i >= this.total()) return;
    this.index.set(i);
  }

  goPrev(): void {
    if (!this.isFirst()) this.index.update((i) => i - 1);
  }

  goNext(): void {
    if (!this.isLast()) this.index.update((i) => i + 1);
  }

  onPrimary(): void {
    if (this.isLast()) this.submit();
    else this.goNext();
  }

  /**
   * Persist a pick immediately (§C — save-as-you-go, no client draft). Re-picking a different option
   * re-`PUT`s. A `409` (attempt no longer `InProgress` / past `deadlineUtc`) is swallowed — the submit /
   * timeout flow resolves the terminal state; the answer race never blocks the runner.
   */
  pick(question: QuizAttemptQuestion, optionId: string): void {
    if (this.submitting() || this.#terminal) return;
    if (this.selections()[question.id] === optionId) return;
    this.selections.update((s) => ({ ...s, [question.id]: optionId })); // optimistic highlight
    this.#service.answer(this.attempt().attemptId, question.id, optionId).subscribe({ error: () => {} });
  }

  /**
   * Manual **or** local-zero submit (#4). On success → results. On a `409` (the Hangfire job already
   * `TimedOut` the attempt) → re-read `quiz(sessionId)`, find the now-terminal summary, and route to
   * results — **never** an error (the server's clock wins, §C). A transient failure re-enables submit.
   */
  submit(): void {
    if (this.#terminal || this.submitting()) return;
    this.submitting.set(true);
    this.submitError.set(null);
    this.#stopClock();
    this.#service.submit(this.attempt().attemptId).subscribe({
      next: (r) =>
        this.#finishToResults({
          attemptId: this.attempt().attemptId,
          scorePercent: r.scorePercent,
          bestPercent: r.bestPercent,
          passed: r.passed,
          status: r.status,
        }),
      error: (err: unknown) => this.#onSubmitError(err),
    });
  }

  // ── leave (CanDeactivate guard + the modal) ──────────────────────────────────
  /**
   * The route's CanDeactivate guard calls this on an in-app navigation away. If the attempt is already
   * terminal / we're leaving cleanly → allow immediately. Otherwise raise the **"Leave the quiz?"**
   * modal and resolve with the student's choice (`true` = Leave & forfeit, `false` = Stay).
   */
  attemptLeave(): Observable<boolean> {
    if (this.#terminal || this.#leaving) return of(true);
    this.#leaveSubject = new Subject<boolean>();
    this.leaveModalOpen.set(true);
    return this.#leaveSubject.asObservable();
  }

  /** "Leave & forfeit" — tearing down the hub IS the forfeit (§A.1); allow the pending navigation. */
  confirmLeave(): void {
    this.leaveModalOpen.set(false);
    this.#leaving = true;
    this.#teardown();
    this.#resolveLeave(true);
  }

  /** "Stay in quiz" — dismiss; the sitting continues, the hub stays open. */
  stayInQuiz(): void {
    this.leaveModalOpen.set(false);
    this.#resolveLeave(false);
  }

  // ── HostListeners: native unload guard + focus telemetry ─────────────────────
  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    // Arm the browser's native "leave?" prompt while a live sitting is mounted (the prototype's guard).
    if (!this.#terminal) {
      event.preventDefault();
      event.returnValue = '';
    }
  }

  @HostListener('document:visibilitychange')
  onVisibilityChange(): void {
    if (this.#terminal) return;
    if (this.#document.hidden) this.#onFocusLost();
    else this.#onFocusReturned();
  }

  @HostListener('window:blur')
  onWindowBlur(): void {
    if (!this.#terminal) this.#onFocusLost();
  }

  @HostListener('window:focus')
  onWindowFocus(): void {
    if (!this.#terminal) this.#onFocusReturned();
  }

  // ── seams (tests drive these, not the wall clock) ────────────────────────────
  /** The 1 s countdown tick (server-authoritative; on local-zero → submit). Overridable in tests. */
  protected tick(): void {
    if (this.#terminal || this.submitting()) return;
    const next = Math.max(0, this.remaining() - 1);
    this.remaining.set(next);
    if (next === 0) this.submit(); // local-zero auto-submit (§C — the Hangfire 409 race is handled)
  }

  /** The wall-clock timestamp for focus telemetry. Overridable in tests. */
  protected now(): string {
    return new Date().toISOString();
  }

  // ── internals ────────────────────────────────────────────────────────────────
  #startSitting(a: QuizAttempt): void {
    // Seed the countdown PURELY from the server's two timestamps — no local-clock dependency, so the
    // display is deterministic and the user's wrong clock can't skew it (the server is authoritative, §C).
    const seed = Math.round((Date.parse(a.deadlineUtc) - Date.parse(a.serverNowUtc)) / 1000);
    this.remaining.set(Number.isFinite(seed) && seed > 0 ? seed : 0);
    this.#hub.open(); // forfeit-on-disconnect, armed AFTER start() — the attempt already exists (§A.1)
    this.#startClock();
  }

  #onSubmitError(err: unknown): void {
    if (err instanceof HttpErrorResponse && err.status === 409) {
      // The Hangfire timer already TimedOut the attempt — re-read by-session for the terminal summary (§C).
      this.#service.quiz(this.sessionId()).subscribe({
        next: (q) => {
          const s = q.attempts.find((x) => x.id === this.attempt().attemptId);
          if (!s) {
            this.#leaveTo(['/sessions', this.sessionId()]);
            return;
          }
          this.#finishToResults({
            attemptId: s.id,
            scorePercent: s.scorePercent ?? 0,
            bestPercent: q.bestPercent ?? s.scorePercent ?? 0,
            passed: q.passed,
            status: s.status,
          });
        },
        error: () => this.#leaveTo(['/sessions', this.sessionId()]),
      });
      return;
    }
    // Transient (network) — re-enable submit; resume the countdown if there is time left.
    this.submitting.set(false);
    this.submitError.set('Couldn’t submit your attempt. Check your connection and try again.');
    if (this.remaining() > 0) this.#startClock();
  }

  #finishToResults(state: {
    attemptId: string;
    scorePercent: number;
    bestPercent: number;
    passed: boolean;
    status: string;
  }): void {
    this.#terminal = true;
    this.#teardown();
    this.#leaveTo(['/sessions', this.sessionId(), 'quiz', 'results'], {
      ...state,
      sessionTitle: this.sessionTitle(),
    });
  }

  #leaveTo(commands: unknown[], state?: Record<string, unknown>): void {
    this.#leaving = true;
    void this.#router.navigate(commands as string[], state ? { state } : undefined);
  }

  #onFocusLost(): void {
    if (this.#focusLostAt) return; // dedupe overlapping blur + visibilitychange
    this.#focusLostAt = this.now();
    this.focusNoticeOpen.set(true);
    this.#service
      .focus(this.attempt().attemptId, { type: 'FocusLost', occurredAtUtc: this.#focusLostAt })
      .subscribe({ error: () => {} });
  }

  #onFocusReturned(): void {
    const lostAt = this.#focusLostAt;
    if (!lostAt) return;
    this.#focusLostAt = null;
    const occurredAtUtc = this.now();
    const durationMs = Math.max(0, Date.parse(occurredAtUtc) - Date.parse(lostAt));
    this.#service
      .focus(this.attempt().attemptId, { type: 'FocusReturned', occurredAtUtc, durationMs })
      .subscribe({ error: () => {} });
  }

  #resolveLeave(value: boolean): void {
    this.#leaveSubject?.next(value);
    this.#leaveSubject?.complete();
    this.#leaveSubject = null;
  }

  #teardown(): void {
    this.#stopClock();
    this.#hub.close();
  }

  #startClock(): void {
    this.#stopClock();
    // Run the recurring tick OUTSIDE the Angular zone — a zone-patched `setInterval` is a perpetual
    // macrotask that keeps the zone unstable forever (breaking `whenStable()`, the S4 gotcha); each
    // tick hops back in so the `remaining` signal still drives change detection.
    this.#zone.runOutsideAngular(() => {
      this.#clock = this.#document.defaultView?.setInterval(() => this.#zone.run(() => this.tick()), 1000);
    });
  }

  #stopClock(): void {
    if (this.#clock !== undefined) {
      this.#document.defaultView?.clearInterval(this.#clock);
      this.#clock = undefined;
    }
  }
}
