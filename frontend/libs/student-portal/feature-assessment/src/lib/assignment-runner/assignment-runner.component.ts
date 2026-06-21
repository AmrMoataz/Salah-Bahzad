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
import { ButtonComponent, LatexPreviewComponent, ProgressComponent } from '@sb/shared/ui';
import {
  AssignmentEventBody,
  AssignmentProgress,
  AssignmentService,
  StudentAssignment,
  StudentAssignmentQuestion,
} from '@sb/student-portal/data-access';
import { mmss, optionLetter } from '../assessment.util';

/**
 * The open-book **Assignment runner** (`FR-STU-ASG-001..006`, contract §A/§C, the prototype's
 * `screen === 'assignment'`). One MCQ at a time inside a `max-width:620px` card: a success
 * **Progress** bar + "X of Y answered", an **accumulated up-timer** that resumes from
 * `timeSpentSeconds`, a picked-option-turns-green radio group, a per-question hint toggle, and
 * `← Previous` / `Next question` / **Submit assignment** on the last question.
 *
 * Each pick **persists immediately** via the answer `PUT` (no client draft, §C); answering the last
 * unanswered question **auto-grades server-side** (`Status → Completed`) — there is **no separate
 * submit call**, so "Submit assignment" simply returns to the session detail (the score lives in the
 * §B review + the S3 detail card, **no inline results**). The behaviour trail (`Entered` on open,
 * `Navigated` on prev/next, `Left` on exit) + the elapsed delta land in the engine via `POST /events`.
 * The runner is **reachable when the session is expired** (`FR-STU-SES-001`) — it is not gated by it.
 */
@Component({
  selector: 'sb-assignment-runner',
  standalone: true,
  imports: [ButtonComponent, LatexPreviewComponent, ProgressComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="run">
      <header class="run__top">
        <button type="button" class="run__exit" (click)="saveAndExit()">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <line x1="19" y1="12" x2="5" y2="12" /><polyline points="12 19 5 12 12 5" />
          </svg>
          Save &amp; exit
        </button>
        <div class="run__timer" aria-live="off" [attr.aria-label]="'Time spent ' + timeLabel()">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="2" aria-hidden="true">
            <circle cx="12" cy="12" r="9" /><polyline points="12 7 12 12 15 14" />
          </svg>
          {{ timeLabel() }}
        </div>
      </header>

      <div class="run__crumb">Assignment</div>
      <h1 class="run__title">{{ titleLine }}</h1>

      @if (loading()) {
        <div class="run__skeleton" aria-hidden="true"></div>
      } @else if (error()) {
        <div class="run__error">
          <p>{{ error() }}</p>
          <sb-button variant="primary" (clicked)="reload()">Try again</sb-button>
        </div>
      } @else if (current(); as q) {
        <div class="run__progress">
          <sb-progress class="run__bar" [value]="progressPercent()" variant="success" />
          <span class="run__count">{{ posLabel() }}</span>
        </div>

        <div class="run__card">
          <div class="run__qlabel">Question {{ q.order }}</div>
          @if (q.bodyLatex) {
            <sb-latex-preview class="run__body" [latex]="q.bodyLatex" />
          }
          @if (q.imageUrl) {
            <img class="run__img" [src]="q.imageUrl" alt="Figure for question {{ q.order }}" />
          }

          <div class="run__opts" role="radiogroup" [attr.aria-label]="'Question ' + q.order + ' options'">
            @for (o of q.options; track o.id; let oi = $index) {
              <button
                type="button"
                class="run-opt"
                role="radio"
                [class.run-opt--picked]="o.id === q.selectedOptionId"
                [attr.aria-checked]="o.id === q.selectedOptionId"
                [disabled]="completed()"
                (click)="pick(q, o.id)"
              >
                <span class="run-opt__key">{{ letter(oi) }}</span>
                <sb-latex-preview class="run-opt__text" [latex]="o.text" />
                @if (o.id === q.selectedOptionId) {
                  <svg class="run-opt__check" width="20" height="20" viewBox="0 0 24 24" fill="none"
                       stroke="currentColor" stroke-width="2.5" stroke-linecap="round"
                       stroke-linejoin="round" aria-hidden="true">
                    <polyline points="20 6 9 17 4 12" />
                  </svg>
                }
              </button>
            }
          </div>

          @if (q.hintUrl) {
            <div class="run__hintwrap">
              <button type="button" class="run__hinttoggle" (click)="toggleHint()"
                      [attr.aria-expanded]="hintOpen()">
                <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                     stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <path d="M9 18h6M10 22h4M12 2a7 7 0 0 0-4 12.7c.6.5 1 1.3 1 2.1h6c0-.8.4-1.6 1-2.1A7 7 0 0 0 12 2z" />
                </svg>
                {{ hintOpen() ? 'Hide hint' : 'Show hint' }}
              </button>
              @if (hintOpen()) {
                <div class="run__hint">
                  <span class="run__hint-icon" aria-hidden="true">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor" stroke="none">
                      <polygon points="9 7 9 17 17 12" />
                    </svg>
                  </span>
                  <p class="run__hint-text">
                    <strong>Video hint:</strong>
                    <a class="run__hint-link" [href]="q.hintUrl" target="_blank" rel="noopener">
                      Watch the explainer
                    </a>
                  </p>
                </div>
              }
            </div>
          }
        </div>

        <div class="run__nav">
          <sb-button variant="secondary" size="lg" [disabled]="isFirst()" (clicked)="goPrev()">
            ← Previous
          </sb-button>
          <sb-button class="run__primary" variant="primary" size="lg" (clicked)="onPrimary()">
            {{ primaryLabel() }}
          </sb-button>
        </div>
      }
    </section>
  `,
  styles: [`
    .run { max-width: 760px; margin: 0 auto; display: flex; flex-direction: column; }

    .run__top { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 12px; margin-bottom: 14px; }
    .run__exit { display: inline-flex; align-items: center; gap: 6px; background: none; border: none; color: var(--sb-primary-600); font-family: inherit; font-weight: 700; font-size: 14px; cursor: pointer; padding: 4px 0; }
    .run__exit:hover { color: var(--sb-primary-700); }
    .run__exit:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); border-radius: var(--sb-radius-sm); }
    .run__timer { display: inline-flex; align-items: center; gap: 8px; font-family: var(--sb-font-mono); font-weight: 700; font-size: 15px; color: var(--sb-text-muted); background: var(--sb-surface); border: 1px solid var(--sb-border); padding: 8px 14px; border-radius: var(--sb-radius-pill); }

    .run__crumb { font-size: 12px; font-weight: 800; text-transform: uppercase; letter-spacing: .6px; color: var(--sb-subject-green-deep); }
    .run__title { margin: 2px 0 16px; font-weight: 800; font-size: 24px; letter-spacing: -0.3px; }

    .run__skeleton { height: 280px; border-radius: 18px; background: var(--sb-surface-sunken); animation: run-pulse 1.3s var(--sb-easing-standard) infinite; }
    @keyframes run-pulse { 0%, 100% { opacity: 1; } 50% { opacity: .55; } }
    .run__error { text-align: center; padding: var(--sb-space-8); background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-xl); }
    .run__error p { margin: 0 0 12px; color: var(--sb-text-muted); }

    .run__progress { display: flex; align-items: center; gap: 12px; margin-bottom: 18px; }
    .run__bar { flex: 1; }
    .run__count { font-size: 13px; font-weight: 700; color: var(--sb-text-muted); white-space: nowrap; }

    .run__card { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: 18px; padding: 26px; box-shadow: var(--sb-shadow-sm); }
    .run__qlabel { font-size: 12px; font-weight: 800; text-transform: uppercase; letter-spacing: .6px; color: var(--sb-subject-green-deep); margin-bottom: 10px; }
    .run__body { font-size: 18px; font-weight: 600; line-height: 1.5; margin-bottom: 6px; }
    .run__img { display: block; max-width: 100%; height: auto; border-radius: var(--sb-radius-md); border: 1px solid var(--sb-border); margin: 12px 0; }

    .run__opts { display: flex; flex-direction: column; gap: 10px; margin-top: 16px; }
    .run-opt { display: flex; align-items: center; gap: 12px; width: 100%; text-align: left; padding: 12px 14px; border: 2px solid var(--sb-border); border-radius: var(--sb-radius-lg); background: var(--sb-surface); color: var(--sb-text); font-family: inherit; cursor: pointer; transition: border-color var(--sb-timing-fast) var(--sb-easing-standard), background var(--sb-timing-fast) var(--sb-easing-standard); }
    .run-opt:hover:not(:disabled) { border-color: var(--sb-border-strong); }
    .run-opt:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }
    .run-opt:disabled { cursor: default; }
    .run-opt--picked { border-color: var(--sb-success-fg); background: var(--sb-success-bg); }
    .run-opt__key { flex-shrink: 0; width: 30px; height: 30px; border-radius: 8px; display: inline-flex; align-items: center; justify-content: center; font-weight: 800; font-size: 14px; background: var(--sb-neutral-100); color: var(--sb-text-muted); }
    .run-opt--picked .run-opt__key { background: var(--sb-success-fg); color: #fff; }
    .run-opt__text { flex: 1; font-size: 15px; font-weight: 600; }
    .run-opt__check { flex-shrink: 0; color: var(--sb-success-fg); }

    .run__hintwrap { margin-top: 18px; }
    .run__hinttoggle { display: inline-flex; align-items: center; gap: 7px; background: none; border: none; color: var(--sb-warning-fg); font-family: inherit; font-weight: 700; font-size: 13px; cursor: pointer; padding: 6px 0; }
    .run__hinttoggle:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); border-radius: var(--sb-radius-sm); }
    .run__hint { margin-top: 10px; display: flex; gap: 12px; align-items: center; background: var(--sb-warning-bg); border: 1px solid var(--sb-warning-border); border-radius: var(--sb-radius-md); padding: 12px; }
    .run__hint-icon { width: 54px; height: 40px; border-radius: 8px; background: var(--sb-danger-fg); color: #fff; display: inline-flex; align-items: center; justify-content: center; flex-shrink: 0; }
    .run__hint-text { margin: 0; font-size: 13px; color: var(--sb-warning-fg); line-height: 1.45; }
    .run__hint-link { color: var(--sb-warning-fg); font-weight: 700; }

    .run__nav { display: flex; gap: 12px; margin-top: 18px; }
    .run__primary { flex: 1; }
    .run__primary ::ng-deep .sb-btn { width: 100%; }

    @media (max-width: 560px) {
      .run__title { font-size: 21px; }
      .run__card { padding: 18px; }
    }
  `],
})
export class AssignmentRunnerComponent implements OnDestroy {
  readonly #service = inject(AssignmentService);
  readonly #router = inject(Router);
  readonly #document = inject(DOCUMENT);
  readonly #zone = inject(NgZone);

  /** The session id, bound from `/sessions/:id/assignment` (`withComponentInputBinding`). */
  readonly id = input.required<string>();

  readonly assignment = signal<StudentAssignment | null>(null);
  readonly progress = signal<AssignmentProgress | null>(null);
  readonly index = signal(0);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly hintOpen = signal(false);

  /** The base accumulated seconds from the load (`timeSpentSeconds`) — the up-timer resumes from this. */
  readonly #baseSeconds = signal(0);
  /** Locally ticked seconds since the load — added to the base for display. */
  readonly ticked = signal(0);

  #unflushedMs = 0;
  #left = false;
  #clock: ReturnType<Window['setInterval']> | undefined;

  /** Cosmetic title from the navigation state (S3 passes the session title); falls back gracefully. */
  readonly titleLine: string;

  readonly assignmentId = computed(() => this.assignment()?.id ?? '');
  readonly questions = computed(() => this.assignment()?.questions ?? []);
  readonly current = computed<StudentAssignmentQuestion | null>(
    () => this.questions()[this.index()] ?? null,
  );
  readonly status = computed(
    () => this.progress()?.status ?? this.assignment()?.status ?? 'InProgress',
  );
  readonly completed = computed(() => this.status() === 'Completed');
  readonly isFirst = computed(() => this.index() === 0);
  readonly isLast = computed(() => this.index() >= this.questions().length - 1);
  readonly questionCount = computed(() => this.progress()?.questionCount ?? this.questions().length);
  readonly answeredCount = computed(
    () => this.progress()?.answeredCount ?? this.questions().filter((q) => q.selectedOptionId != null).length,
  );
  readonly progressPercent = computed(() => {
    const n = this.questionCount();
    return n ? Math.round((this.answeredCount() / n) * 100) : 0;
  });
  readonly posLabel = computed(() => `${this.answeredCount()} of ${this.questionCount()} answered`);
  readonly primaryLabel = computed(() => (this.isLast() ? 'Submit assignment' : 'Next question'));
  readonly timeLabel = computed(() => mmss(this.#baseSeconds() + this.ticked()));

  constructor() {
    const state = this.#document.defaultView?.history.state as { sessionTitle?: string } | null;
    const title = state?.sessionTitle;
    this.titleLine = title ? `${title} — Homework` : 'Homework';

    // Reload whenever the bound route id changes (router input binding sets it before first CD).
    effect(() => {
      const id = this.id();
      if (id) this.#load(id);
    });
  }

  ngOnDestroy(): void {
    this.#leave();
  }

  letter(index: number): string {
    return optionLetter(index);
  }

  reload(): void {
    this.#load(this.id());
  }

  /** Save & exit (the prototype's `openActiveDetail`) — answers already persisted; flush & leave. */
  saveAndExit(): void {
    this.#leave();
    void this.#router.navigate(['/sessions', this.id()]);
  }

  toggleHint(): void {
    this.hintOpen.update((open) => !open);
  }

  /**
   * Persist a pick immediately (§C — no client draft). Answering the last unanswered question
   * auto-grades server-side; the `AssignmentProgressDto` updates the count + status. Blocked once
   * `Completed` (re-answer would `409`).
   */
  pick(question: StudentAssignmentQuestion, optionId: string): void {
    if (this.completed()) return;
    if (question.selectedOptionId === optionId) return;
    this.#setSelected(question.id, optionId); // optimistic green highlight
    this.#service.answer(this.assignmentId(), question.id, optionId).subscribe({
      next: (p) => this.progress.set(p), // authoritative count + status (auto-grade on the last)
      error: () => this.#resync(), // a race (e.g. already Completed → 409) → re-sync from the engine
    });
  }

  goPrev(): void {
    if (this.isFirst()) return;
    const target = this.questions()[this.index() - 1];
    this.index.update((i) => i - 1);
    this.hintOpen.set(false);
    this.#emit({ type: 'Navigated', questionOrder: target.order, occurredAtUtc: this.now(), elapsedMs: this.#flush() });
  }

  goNext(): void {
    if (this.isLast()) return;
    const target = this.questions()[this.index() + 1];
    this.index.update((i) => i + 1);
    this.hintOpen.set(false);
    this.#emit({ type: 'Navigated', questionOrder: target.order, occurredAtUtc: this.now(), elapsedMs: this.#flush() });
  }

  /** The primary button: `Next question` (non-last) or `Submit assignment` (last → back to the detail). */
  onPrimary(): void {
    if (this.isLast()) {
      this.#leave();
      void this.#router.navigate(['/sessions', this.id()]);
    } else {
      this.goNext();
    }
  }

  @HostListener('window:beforeunload')
  onBeforeUnload(): void {
    this.#leave();
  }

  @HostListener('document:visibilitychange')
  onVisibilityChange(): void {
    if (this.#document.hidden) this.#leave();
    else this.#resume();
  }

  // ── Behaviour events + the accumulated timer (§C / F4) ───────────────────────
  /** Seam for tests — the wall-clock tick (1 s) accruing display seconds + the unflushed delta. */
  protected tick(): void {
    this.ticked.update((t) => t + 1);
    this.#unflushedMs += 1000;
  }

  /** Seam for tests — the event timestamp. */
  protected now(): string {
    return new Date().toISOString();
  }

  #enter(): void {
    this.#left = false;
    this.#emit({ type: 'Entered', occurredAtUtc: this.now(), elapsedMs: 0 });
    this.#startClock();
  }

  #leave(): void {
    if (this.#left) return;
    this.#left = true;
    this.#stopClock();
    this.#emit({ type: 'Left', occurredAtUtc: this.now(), elapsedMs: this.#flush() });
  }

  #resume(): void {
    if (!this.assignment() || this.completed()) return;
    this.#left = false;
    this.#startClock();
  }

  #emit(body: AssignmentEventBody): void {
    const id = this.assignmentId();
    if (!id) return;
    // Behaviour telemetry is best-effort — a failed event never blocks the runner.
    this.#service.event(id, body).subscribe({ error: () => {} });
  }

  #flush(): number {
    const ms = this.#unflushedMs;
    this.#unflushedMs = 0;
    return ms;
  }

  #startClock(): void {
    this.#stopClock();
    // Run the recurring tick OUTSIDE the Angular zone — a zone-patched `setInterval` is a perpetual
    // macrotask that would keep the zone unstable forever (breaking `whenStable()`); each tick hops
    // back in so the timer signal still drives change detection.
    this.#zone.runOutsideAngular(() => {
      this.#clock = this.#document.defaultView?.setInterval(
        () => this.#zone.run(() => this.tick()),
        1000,
      );
    });
  }

  #stopClock(): void {
    if (this.#clock !== undefined) {
      this.#document.defaultView?.clearInterval(this.#clock);
      this.#clock = undefined;
    }
  }

  #setSelected(questionId: string, optionId: string): void {
    this.assignment.update((a) =>
      a
        ? {
            ...a,
            questions: a.questions.map((q) =>
              q.id === questionId ? { ...q, selectedOptionId: optionId } : q,
            ),
          }
        : a,
    );
  }

  #resync(): void {
    this.#service.assignment(this.id()).subscribe({
      next: (a) => {
        this.assignment.set(a);
        this.progress.set(this.#seedProgress(a));
      },
      error: () => {},
    });
  }

  #seedProgress(a: StudentAssignment): AssignmentProgress {
    return {
      answeredCount: a.questions.filter((q) => q.selectedOptionId != null).length,
      questionCount: a.questions.length,
      status: a.status,
    };
  }

  #load(sessionId: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.#service.assignment(sessionId).subscribe({
      next: (a) => {
        this.assignment.set(a);
        this.progress.set(this.#seedProgress(a));
        const firstUnanswered = a.questions.findIndex((q) => q.selectedOptionId == null);
        this.index.set(firstUnanswered >= 0 ? firstUnanswered : 0);
        this.#baseSeconds.set(a.timeSpentSeconds);
        this.ticked.set(0);
        this.#unflushedMs = 0;
        this.loading.set(false);
        this.#enter();
      },
      error: (err: unknown) => {
        this.loading.set(false);
        if (err instanceof HttpErrorResponse && err.status === 404) {
          // No enrollment for the session → route back, not a hard error (§A #1).
          void this.#router.navigate(['/sessions', sessionId]);
          return;
        }
        this.error.set('Please check your connection and try again.');
      },
    });
  }
}
