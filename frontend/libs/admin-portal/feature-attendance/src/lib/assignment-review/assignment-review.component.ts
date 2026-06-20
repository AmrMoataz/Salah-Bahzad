import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { AuthStore } from '@sb/shared/data-access';
import {
  AvatarComponent,
  CardComponent,
  EmptyStateComponent,
  LatexPreviewComponent,
  SbTableColumn,
  StatusPillComponent,
  TableCellDirective,
  TableComponent,
  TabsComponent,
  ToastService,
} from '@sb/shared/ui';
import { BehaviourEventType } from '../data-access/attendance.models';
import { ReviewService } from '../data-access/review.service';
import {
  accentBg,
  accentFg,
  avatarSubject,
  behaviourIconSvg,
  behaviourVisual,
  clockTime,
  initialsOf,
  mmss,
  optionLetter,
  optionState,
  optionStyle,
  quizFlagPill,
  relativeTime,
} from '../attendance.presentation';

type ReviewTab = 'assignment' | 'quiz' | 'behaviour';

/**
 * Assignment / Behaviour review (`FR-ADM-REV-001`/`-003`, mockup `scrReview`). A staff-only deep-dive
 * for one enrollment: a back-link, a student header with **Score** (`correctCount/questionCount`) and
 * **Time spent** (`mm:ss`), then three tabs — **Assignment** (a card per question: the correct option
 * is green+check, the student's wrong pick is red+×, with a `+mark`/`0` pill), **Quiz attempts**
 * (the gating quiz's attempts table — 5B-2 §B, lazy-loaded on tab activation), and **Behaviour log**
 * (the in-assessment icon timeline, now including the quiz's focus-loss rows). Unlike the student `§A`
 * shape, this view exposes `isCorrect`.
 */
@Component({
  selector: 'sb-assignment-review',
  standalone: true,
  imports: [
    AvatarComponent,
    CardComponent,
    EmptyStateComponent,
    LatexPreviewComponent,
    StatusPillComponent,
    TableComponent,
    TableCellDirective,
    TabsComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!canRead()) {
      <div class="rv__gate">
        <h3 class="rv__gate-title">Access required</h3>
        <p class="rv__gate-text">You don’t have permission to view assignment reviews.</p>
      </div>
    } @else {
      <button type="button" class="rv__back" (click)="back()">
        <span class="rv__back-icon" aria-hidden="true" [innerHTML]="backIcon"></span>
        Back
      </button>

      @if (review(); as r) {
        <div class="rv__head">
          <div class="rv__who">
            <sb-avatar size="lg" [initials]="initials(r.studentName)" [subject]="subjectFor()" />
            <div>
              <h1 class="rv__name">{{ r.studentName }}</h1>
              <div class="rv__sub">{{ r.sessionTitle }} · Assignment review</div>
            </div>
          </div>
          <div class="rv__stats">
            <div class="rv__stat">
              <div class="rv__stat-value rv__stat-value--score">{{ r.correctCount }}/{{ r.questionCount }}</div>
              <div class="rv__stat-label">Score</div>
            </div>
            <div class="rv__stat">
              <div class="rv__stat-value">{{ time(r.timeSpentSeconds) }}</div>
              <div class="rv__stat-label">Time spent</div>
            </div>
          </div>
        </div>

        <div class="rv__tabs">
          <sb-tabs [tabs]="tabs" [active]="activeTab()" (tabChange)="onTab($event)" />
        </div>

        @switch (activeTab()) {
          @case ('assignment') {
            <div class="rv__qs">
              @for (q of r.questions; track q.order) {
                <sb-card>
                  <div class="rv-q__head">
                    <div class="rv-q__title">
                      <span class="rv-q__num">Q{{ q.order }}.</span>
                      <sb-latex-preview class="rv-q__body" [latex]="q.bodyLatex" />
                    </div>
                    <span class="rv-q__pill">
                      <sb-status-pill [variant]="q.isCorrect ? 'success' : 'danger'">
                        {{ q.isCorrect ? '+' + q.mark : '0' }}
                      </sb-status-pill>
                    </span>
                  </div>

                  @if (q.imageUrl) {
                    <img class="rv-q__img" [src]="q.imageUrl" alt="" />
                  }

                  <div class="rv-q__opts">
                    @for (o of q.options; track o.id; let oi = $index) {
                      @let st = optState(o, q.selectedOptionId);
                      <div
                        class="rv-opt"
                        [attr.data-state]="st"
                        [style.background]="optBg(st)"
                        [style.border-color]="optBorder(st)"
                      >
                        <span class="rv-opt__text">
                          <strong class="rv-opt__letter">{{ letter(oi) }}.</strong> {{ o.text }}
                        </span>
                        @if (st === 'correct') {
                          <span class="rv-opt__mark rv-opt__mark--ok" aria-hidden="true" [innerHTML]="checkIcon"></span>
                        } @else if (st === 'picked-wrong') {
                          <span class="rv-opt__mark rv-opt__mark--bad" aria-hidden="true" [innerHTML]="xIcon"></span>
                        }
                      </div>
                    }
                  </div>
                </sb-card>
              }
            </div>
          }

          @case ('quiz') {
            @if (quizReview(); as qz) {
              <div class="rv-qz__summary">
                Best <strong>{{ qz.bestPercent }}%</strong> ·
                <span [class.rv-qz__pass]="qz.passed" [class.rv-qz__fail]="!qz.passed">
                  {{ qz.passed ? 'Passed' : 'Not passed' }}
                </span>
                (min {{ qz.minPassPercent }}%) ·
                {{ qz.attemptsUsed }}/{{ qz.attemptsAllowed }} attempts
              </div>
              <sb-card [padding]="false" title="Quiz attempts">
                <sb-table [columns]="quizColumns" [rows]="qz.attempts" [rowKey]="byAttempt">
                  <ng-template sbTableCell="attempt" let-row>
                    Attempt {{ row.number }}
                    @if (row.isBest) {
                      <span class="rv-qz__best">(best)</span>
                    }
                  </ng-template>
                  <ng-template sbTableCell="score" let-row><strong>{{ row.scorePercent }}%</strong></ng-template>
                  <ng-template sbTableCell="time" let-row>{{ time(row.timeSpentSeconds) }}</ng-template>
                  <ng-template sbTableCell="flag" let-row>
                    @let pill = flagPill(row.flag);
                    <sb-status-pill [variant]="pill.variant">{{ pill.label }}</sb-status-pill>
                  </ng-template>
                  <ng-template sbTableCell="when" let-row>{{ when(row.startedAtUtc) }}</ng-template>
                </sb-table>
              </sb-card>
            } @else if (quizMissing()) {
              <sb-card [padding]="false" title="Quiz attempts">
                <sb-empty-state
                  headline="No gating quiz for this session"
                  description="This session isn’t gated by a prerequisite quiz, so there are no attempts to review."
                />
              </sb-card>
            } @else {
              <div class="rv__loading">{{ quizLoading() ? 'Loading…' : 'Could not load quiz attempts.' }}</div>
            }
          }

          @case ('behaviour') {
            <sb-card [padding]="false" title="In-assessment behaviour">
              @if (behaviour().length === 0) {
                <div class="rv-tl__empty">No behaviour recorded for this assignment.</div>
              } @else {
                @for (e of behaviour(); track $index; let i = $index) {
                  <div class="rv-tl__row" [class.rv-tl__row--divided]="i < behaviour().length - 1">
                    <span
                      class="rv-tl__icon"
                      aria-hidden="true"
                      [style.background]="bg(e.type)"
                      [style.color]="fg(e.type)"
                      [innerHTML]="icon(e.type)"
                    ></span>
                    <div class="rv-tl__label">{{ e.label }}</div>
                    <span class="rv-tl__time">{{ clock(e.occurredAtUtc) }}</span>
                  </div>
                }
              }
            </sb-card>
          }
        }
      } @else {
        <div class="rv__loading">{{ isLoading() ? 'Loading…' : 'Could not load this review.' }}</div>
      }
    }
  `,
  styles: [`
    :host { display: block; }

    .rv__back {
      display: inline-flex; align-items: center; gap: 6px;
      border: none; background: transparent; color: var(--sb-text-muted);
      font-family: var(--sb-font-sans); font-weight: 700; font-size: var(--sb-body-sm-size);
      cursor: pointer; padding: 6px 10px 6px 6px; border-radius: var(--sb-radius-md); margin-bottom: var(--sb-space-3);
    }
    .rv__back:hover { background: var(--sb-surface-sunken); color: var(--sb-text); }
    .rv__back:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }
    .rv__back-icon { display: inline-flex; }

    .rv__head { display: flex; align-items: center; justify-content: space-between; gap: var(--sb-space-4); flex-wrap: wrap; margin-bottom: var(--sb-space-4); }
    .rv__who { display: flex; gap: var(--sb-space-3); align-items: center; }
    .rv__name { margin: 0; font-size: var(--sb-heading-lg-size); font-weight: 800; letter-spacing: -0.01em; color: var(--sb-text); }
    .rv__sub { color: var(--sb-text-muted); font-size: var(--sb-body-md-size); margin-top: 2px; }

    .rv__stats { display: flex; gap: var(--sb-space-6); }
    .rv__stat { text-align: center; }
    .rv__stat-value { font-size: var(--sb-heading-lg-size); font-weight: 800; color: var(--sb-text); line-height: 1; font-variant-numeric: tabular-nums; }
    .rv__stat-value--score { color: var(--sb-primary); }
    .rv__stat-label { font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); margin-top: var(--sb-space-1); }

    .rv__tabs { margin-bottom: var(--sb-space-4); }

    /* Quiz attempts */
    .rv-qz__summary { margin-bottom: var(--sb-space-3); color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
    .rv-qz__summary strong { color: var(--sb-text); }
    .rv-qz__pass { color: var(--sb-success-fg); font-weight: 700; }
    .rv-qz__fail { color: var(--sb-danger-fg); font-weight: 700; }
    .rv-qz__best { margin-left: 4px; color: var(--sb-primary); font-weight: 700; }

    /* Question cards */
    .rv__qs { display: flex; flex-direction: column; gap: var(--sb-space-3); }
    .rv-q__head { display: flex; justify-content: space-between; gap: var(--sb-space-3); margin-bottom: var(--sb-space-3); }
    .rv-q__title { display: flex; align-items: baseline; gap: 6px; flex: 1; min-width: 0; font-weight: 700; }
    .rv-q__num { flex-shrink: 0; }
    .rv-q__body { min-width: 0; }
    .rv-q__pill { flex-shrink: 0; }
    .rv-q__img { display: block; max-width: 320px; width: 100%; height: auto; margin-bottom: var(--sb-space-3); border-radius: var(--sb-radius-md); border: 1px solid var(--sb-border); }

    .rv-q__opts { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: var(--sb-space-2); }
    .rv-opt {
      display: flex; justify-content: space-between; align-items: center; gap: var(--sb-space-2);
      padding: 9px 12px; border-radius: var(--sb-radius-md); border: 1px solid var(--sb-border);
      font-size: var(--sb-body-sm-size); color: var(--sb-text);
    }
    .rv-opt__text { min-width: 0; }
    .rv-opt__letter { color: var(--sb-text-muted); }
    .rv-opt__mark { display: inline-flex; flex-shrink: 0; }
    .rv-opt__mark--ok { color: var(--sb-success-fg); }
    .rv-opt__mark--bad { color: var(--sb-danger-fg); }

    /* Behaviour timeline */
    .rv-tl__row { display: flex; gap: var(--sb-space-3); align-items: center; padding: 12px 18px; }
    .rv-tl__row--divided { border-bottom: 1px solid var(--sb-border); }
    .rv-tl__icon { width: 28px; height: 28px; flex-shrink: 0; border-radius: var(--sb-radius-circle); display: inline-flex; align-items: center; justify-content: center; }
    .rv-tl__label { flex: 1; min-width: 0; font-size: var(--sb-body-sm-size); font-weight: 600; color: var(--sb-text); }
    .rv-tl__time { font-family: var(--sb-font-mono); font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); white-space: nowrap; }
    .rv-tl__empty { padding: var(--sb-space-8); text-align: center; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    .rv__loading { padding: var(--sb-space-10); text-align: center; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    .rv__gate { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-lg); padding: var(--sb-space-10); text-align: center; }
    .rv__gate-title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-sm-size); font-weight: 700; color: var(--sb-text); }
    .rv__gate-text { margin: 0 auto; max-width: 380px; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
  `],
})
export class AssignmentReviewComponent implements OnInit {
  readonly #service = inject(ReviewService);
  readonly #auth = inject(AuthStore);
  readonly #router = inject(Router);
  readonly #route = inject(ActivatedRoute);
  readonly #toast = inject(ToastService);
  readonly #sanitizer = inject(DomSanitizer);
  readonly #iconCache = new Map<BehaviourEventType, SafeHtml>();

  readonly review = this.#service.review;
  readonly behaviour = this.#service.behaviour;
  readonly isLoading = this.#service.isLoading;
  readonly quizReview = this.#service.quizReview;
  readonly quizMissing = this.#service.quizMissing;
  readonly quizLoading = this.#service.quizLoading;

  readonly canRead = computed(() => this.#auth.hasPermission('AttendanceRead'));

  readonly activeTab = signal<ReviewTab>('assignment');
  readonly tabs = [
    { id: 'assignment', label: 'Assignment' },
    { id: 'quiz', label: 'Quiz attempts' },
    { id: 'behaviour', label: 'Behaviour log' },
  ];

  /** `scrReview` "Quiz attempts" columns (lines 1128-1130): Attempt / Score / Time spent / Flags / When. */
  readonly quizColumns: readonly SbTableColumn[] = [
    { key: 'attempt', header: 'Attempt' },
    { key: 'score', header: 'Score', align: 'right' },
    { key: 'time', header: 'Time spent', align: 'right' },
    { key: 'flag', header: 'Flags' },
    { key: 'when', header: 'When', align: 'right' },
  ];

  #enrollmentId = '';
  /** The quiz tab is lazy-loaded once on first activation (idempotent; reset to retry after an error). */
  #quizLoadStarted = false;

  readonly backIcon: SafeHtml = this.#sanitizer.bypassSecurityTrustHtml(
    '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" ' +
      'stroke-linecap="round" stroke-linejoin="round"><path d="M19 12H5M12 19l-7-7 7-7"/></svg>',
  );
  readonly checkIcon: SafeHtml = this.#sanitizer.bypassSecurityTrustHtml(behaviourIconSvg('check', 15));
  readonly xIcon: SafeHtml = this.#sanitizer.bypassSecurityTrustHtml(behaviourIconSvg('x', 15));

  ngOnInit(): void {
    if (!this.canRead()) return;
    const id = this.#route.snapshot.paramMap.get('enrollmentId');
    if (!id) return;
    this.#enrollmentId = id;
    void this.#load(id);
  }

  async #load(id: string): Promise<void> {
    try {
      await Promise.all([this.#service.getReview(id), this.#service.getBehaviour(id)]);
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not load the review.');
    }
  }

  onTab(id: string): void {
    this.activeTab.set(id as ReviewTab);
    if (id === 'quiz') void this.#loadQuiz();
  }

  /** Lazy-load the quiz attempts the first time the tab is opened (a 404 is a silent empty state). */
  async #loadQuiz(): Promise<void> {
    if (this.#quizLoadStarted || !this.#enrollmentId) return;
    this.#quizLoadStarted = true;
    try {
      await this.#service.getQuizReview(this.#enrollmentId);
    } catch {
      this.#quizLoadStarted = false; // allow a retry on the next activation
      this.#toast.error(this.#service.error() ?? 'Could not load quiz attempts.');
    }
  }

  back(): void {
    void this.#router.navigate(['/attendance']);
  }

  // ── Presentation helpers (template-facing) ───────────────────────────────────────
  initials = initialsOf;
  time = mmss;
  clock = clockTime;
  when = relativeTime;
  flagPill = quizFlagPill;
  letter = optionLetter;
  optState = optionState;

  /** Quiz-attempts table keys on the attempt number (one row per attempt). */
  readonly byAttempt = (row: { number: number }): number => row.number;

  subjectFor(): string {
    return avatarSubject(this.#enrollmentId);
  }
  optBg(state: ReturnType<typeof optionState>): string {
    return optionStyle(state).bg;
  }
  optBorder(state: ReturnType<typeof optionState>): string {
    return optionStyle(state).border;
  }
  bg(type: BehaviourEventType): string {
    return accentBg(behaviourVisual(type).accent);
  }
  fg(type: BehaviourEventType): string {
    return accentFg(behaviourVisual(type).accent);
  }

  /** Bypass the HTML sanitizer for developer-authored constant SVG icon markup (see sidebar). */
  icon(type: BehaviourEventType): SafeHtml {
    let trusted = this.#iconCache.get(type);
    if (!trusted) {
      trusted = this.#sanitizer.bypassSecurityTrustHtml(behaviourIconSvg(behaviourVisual(type).icon, 14));
      this.#iconCache.set(type, trusted);
    }
    return trusted;
  }
}
