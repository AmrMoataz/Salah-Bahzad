import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProgressComponent } from '@sb/shared/ui';
import {
  MyPlanDto,
  MyPlanKpis,
  MyPlanStep,
  PlanService,
  StudentAuthStore,
} from '@sb/student-portal/data-access';
import { HomeAccent, HomeIconName, greetingPrefix } from '../home-presentation';
import { HomeKpiCardComponent } from '../kpi-card/kpi-card.component';
import { HomePlanRowComponent } from '../plan-row/plan-row.component';
import { HomeRecentTileComponent } from '../recent-tile/recent-tile.component';

interface HomeKpi {
  label: string;
  value: string;
  caption: string;
  icon: HomeIconName;
  accent: HomeAccent;
}

const ZERO_KPIS: MyPlanKpis = {
  activeSessions: 0,
  videosWatched: 0,
  videosTotal: 0,
  overallProgressPercent: 0,
  completedSessions: 0,
};

const MASCOT_STANDING = '/assets/salah-mascot.png';
const MASCOT_RELAXING = '/assets/salah-relaxing.png';

/**
 * The personalized **Home** — a server-composed weekly study plan rendered read-only (`FR-STU-SES-001`,
 * the Student Portal Home mock reconciled to the platform's real capabilities). Lands on the shell's
 * `''` route. One read (`GET /api/me/plan`); everything below is `computed()` from it. The plan is
 * **derived state**: no editable checkboxes, no fabricated due dates — the only deadline rendered is
 * enrollment expiry (`dueState`). Enrollment is **code-only** (`FR-STU-CAT-003`) → "Redeem", never
 * "Enroll/Renew".
 */
@Component({
  selector: 'sb-home',
  standalone: true,
  imports: [
    RouterLink,
    ProgressComponent,
    HomeKpiCardComponent,
    HomePlanRowComponent,
    HomeRecentTileComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <div class="home__skeleton" aria-hidden="true">
        <div class="home__sk home__sk--hero"></div>
        <div class="home__sk-row">
          <div class="home__sk home__sk--kpi"></div>
          <div class="home__sk home__sk--kpi"></div>
          <div class="home__sk home__sk--kpi"></div>
          <div class="home__sk home__sk--kpi"></div>
        </div>
        <div class="home__sk home__sk--list"></div>
      </div>
    } @else if (loadError()) {
      <div class="home__error">
        <img class="home__error-art" src="/assets/salah-relaxing.png" alt="" aria-hidden="true" />
        <h2 class="home__error-title">We couldn’t load your plan</h2>
        <p class="home__error-copy">Please check your connection and try again.</p>
        <button type="button" class="home__btn home__btn--primary" (click)="reload()">Try again</button>
      </div>
    } @else {
      <section class="home">
        <!-- 1) Hero -->
        <div class="home__hero">
          <div class="home__hero-body">
            <p class="home__greeting">{{ greeting() }}</p>
            <p class="home__lede">{{ heroSubtitle() }}</p>
            <div class="home__cta-row">
              <a routerLink="/redeem" class="home__btn home__btn--primary">Redeem a code</a>
              <a routerLink="/catalogue" class="home__btn home__btn--ghost">Browse catalogue</a>
            </div>
          </div>
          <img class="home__mascot" [src]="heroMascot()" alt="" aria-hidden="true" />
        </div>

        @if (showFullLayout()) {
          <!-- 2) KPI widgets -->
          <div class="home__kpis" role="group" aria-label="Your learning at a glance">
            @for (k of kpiCards(); track k.label) {
              <sb-home-kpi [label]="k.label" [value]="k.value" [caption]="k.caption" [icon]="k.icon" [accent]="k.accent" />
            }
          </div>

          <!-- 3) Main grid -->
          <div class="home__grid">
            <!-- Your tasks -->
            <section class="home__card home__tasks" aria-labelledby="home-tasks-heading">
              <header class="home__card-head">
                <h2 class="home__card-title" id="home-tasks-heading">Your tasks</h2>
                @if (overdueSteps() > 0) {
                  <span class="home__overdue">
                    <span class="home__overdue-dot" aria-hidden="true"></span>{{ overdueSteps() }} overdue
                  </span>
                }
              </header>
              <div class="home__divider" aria-hidden="true"></div>

              @if (allCaughtUp()) {
                <div class="home__caughtup">
                  <span class="home__caughtup-tick" aria-hidden="true">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                         stroke-width="3" stroke-linecap="round" stroke-linejoin="round">
                      <path d="M5 12.5l4.5 4.5L19 6.5" />
                    </svg>
                  </span>
                  <div>
                    <p class="home__caughtup-title">You’re all caught up!</p>
                    <p class="home__caughtup-copy">No tasks left on your list — nice work.</p>
                  </div>
                </div>
              } @else {
                <ul class="home__list" role="list" aria-label="Tasks to do">
                  @for (step of pendingTasks(); track step.key) {
                    <li class="home__list-item"><sb-home-plan-row [step]="step" /></li>
                  }
                </ul>
              }

              @if (completed().length > 0) {
                <p class="home__done-head">Completed · {{ completed().length }}</p>
                <ul class="home__list home__list--done" role="list" aria-label="Completed tasks">
                  @for (step of completed(); track step.key) {
                    <li class="home__list-item"><sb-home-plan-row [step]="step" /></li>
                  }
                </ul>
              }
            </section>

            <!-- Sidebar -->
            <div class="home__side">
              <!-- This week -->
              <section class="home__card home__week" aria-labelledby="home-week-heading">
                <div class="home__week-top">
                  <div>
                    <h2 class="home__card-title" id="home-week-heading">This week</h2>
                    <p class="home__week-sub">Task completion</p>
                  </div>
                  <span class="home__week-pct">{{ weekPercent() }}%</span>
                </div>
                <sb-progress [value]="weekPercent()" variant="primary" [height]="10" ariaLabel="This week’s task completion" />
                <p class="home__week-caption">{{ completedStepsCount() }} of {{ totalSteps() }} done tasks — keep it rolling!</p>
              </section>

              <!-- Recently enrolled -->
              @if (recent().length > 0) {
                <section class="home__card home__recent" aria-labelledby="home-recent-heading">
                  <header class="home__card-head">
                    <h2 class="home__card-title" id="home-recent-heading">Recently enrolled</h2>
                    <a class="home__all" routerLink="/sessions">All <span aria-hidden="true">→</span></a>
                  </header>
                  <ul class="home__recent-list" role="list" aria-label="Recently enrolled sessions">
                    @for (r of recent(); track r.sessionId) {
                      <li><sb-home-recent-tile [recent]="r" /></li>
                    }
                  </ul>
                </section>
              }
            </div>
          </div>
        }
      </section>
    }
  `,
  styles: [`
    :host { display: block; }
    .home { display: flex; flex-direction: column; gap: var(--sb-space-6); }

    /* ── Hero ───────────────────────────────────────────────────────────────── */
    .home__hero {
      position: relative;
      overflow: hidden;
      background: linear-gradient(120deg, var(--sb-primary-50) 0%, var(--sb-accent-50) 100%);
      border: 1px solid var(--sb-primary-100);
      border-radius: var(--sb-radius-xl);
      padding: var(--sb-space-8) var(--sb-space-7);
      display: flex;
      align-items: center;
      gap: var(--sb-space-6);
    }
    .home__hero::before, .home__hero::after {
      content: ''; position: absolute; border-radius: var(--sb-radius-circle); pointer-events: none;
    }
    .home__hero::before { width: 280px; height: 280px; top: -90px; right: 70px; background: var(--sb-accent-100); opacity: 0.45; }
    .home__hero::after  { width: 160px; height: 160px; bottom: -60px; right: 210px; background: var(--sb-primary-100); opacity: 0.5; }

    .home__hero-body { position: relative; z-index: 1; flex: 1; min-width: 0; }
    .home__mascot { position: relative; z-index: 1; width: 150px; height: auto; flex-shrink: 0; align-self: flex-end; }

    .home__greeting {
      font-family: var(--sb-font-display);
      font-size: var(--sb-display-lg-size); font-weight: 700; color: var(--sb-primary); line-height: 1; margin: 0;
    }
    .home__lede { margin: var(--sb-space-3) 0 0; max-width: 520px; color: var(--sb-neutral-600); font-size: var(--sb-body-lg-size); line-height: 1.55; }

    .home__cta-row { display: flex; gap: var(--sb-space-3); flex-wrap: wrap; margin-top: var(--sb-space-5); }
    .home__btn {
      display: inline-flex; align-items: center; justify-content: center;
      min-height: 44px; padding: 0 22px;
      border-radius: var(--sb-radius-md);
      font-family: inherit; font-weight: 700; font-size: var(--sb-body-md-size);
      text-decoration: none; cursor: pointer; border: 1px solid transparent;
      transition: background var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .home__btn--primary { background: var(--sb-primary); color: var(--sb-on-primary); }
    .home__btn--primary:hover { background: var(--sb-primary-hover); color: var(--sb-on-primary); text-decoration: none; }
    .home__btn--ghost { background: var(--sb-surface); color: var(--sb-primary-700); border-color: var(--sb-primary-200); }
    .home__btn--ghost:hover { background: var(--sb-primary-50); color: var(--sb-primary-700); text-decoration: none; }
    .home__btn:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }

    /* ── KPI grid ───────────────────────────────────────────────────────────── */
    .home__kpis {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: var(--sb-space-4);
    }

    /* ── Main grid ──────────────────────────────────────────────────────────── */
    .home__grid {
      display: grid;
      grid-template-columns: minmax(0, 1.7fr) minmax(0, 1fr);
      gap: var(--sb-space-6);
      align-items: start;
    }
    .home__side { display: flex; flex-direction: column; gap: var(--sb-space-6); }

    /* ── Card shell ─────────────────────────────────────────────────────────── */
    .home__card {
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-lg);
      box-shadow: var(--sb-shadow-sm);
      padding: var(--sb-space-6);
    }
    .home__card-head { display: flex; align-items: center; justify-content: space-between; gap: var(--sb-space-3); }
    .home__card-title { margin: 0; font-size: var(--sb-heading-sm-size); font-weight: 800; color: var(--sb-text); }
    .home__divider { height: 1px; background: var(--sb-border); margin: var(--sb-space-4) 0; }

    .home__overdue {
      display: inline-flex; align-items: center; gap: 6px;
      padding: 3px 10px; border-radius: var(--sb-radius-pill);
      background: var(--sb-danger-bg); color: var(--sb-danger-fg);
      font-size: var(--sb-label-md-size); font-weight: 700; white-space: nowrap;
    }
    .home__overdue-dot { width: 6px; height: 6px; border-radius: var(--sb-radius-circle); background: currentColor; }

    /* ── Tasks list ─────────────────────────────────────────────────────────── */
    .home__list { list-style: none; margin: 0; padding: 0; }
    .home__list-item { display: block; }
    .home__list-item + .home__list-item { border-top: 1px solid var(--sb-border); }

    .home__done-head {
      margin: 0; padding-top: var(--sb-space-3); border-top: 1px solid var(--sb-border);
      font-size: var(--sb-label-sm-size); font-weight: 700; letter-spacing: 0.06em;
      text-transform: uppercase; color: var(--sb-text-subtle);
    }
    .home__list--done { margin-top: var(--sb-space-1); }

    .home__caughtup { display: flex; align-items: center; gap: var(--sb-space-4); padding: var(--sb-space-1) 0 var(--sb-space-4); }
    .home__caughtup-tick {
      flex-shrink: 0; width: 40px; height: 40px; border-radius: var(--sb-radius-md);
      background: var(--sb-success-bg); color: var(--sb-success-fg);
      display: inline-flex; align-items: center; justify-content: center;
    }
    .home__caughtup-title { margin: 0; font-weight: 800; color: var(--sb-text); font-size: var(--sb-body-lg-size); }
    .home__caughtup-copy { margin: 2px 0 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    /* ── This week ──────────────────────────────────────────────────────────── */
    .home__week {
      background: linear-gradient(120deg, var(--sb-primary-50), var(--sb-accent-50));
      border-color: var(--sb-primary-100);
      display: flex; flex-direction: column; gap: var(--sb-space-3);
    }
    .home__week-top { display: flex; align-items: flex-start; justify-content: space-between; gap: var(--sb-space-3); }
    .home__week-sub { margin: 2px 0 0; font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); font-weight: 600; }
    .home__week-pct { font-size: var(--sb-display-md-size); font-weight: 800; color: var(--sb-primary); line-height: 1; font-variant-numeric: tabular-nums; }
    .home__week-caption { margin: 0; font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); font-weight: 600; }

    /* ── Recently enrolled ──────────────────────────────────────────────────── */
    .home__recent-list { list-style: none; margin: var(--sb-space-2) 0 0; padding: 0; display: flex; flex-direction: column; gap: 2px; }
    .home__all {
      font-size: var(--sb-body-md-size); font-weight: 700; color: var(--sb-primary);
      text-decoration: none; display: inline-flex; align-items: center; gap: 4px; white-space: nowrap;
    }
    .home__all:hover { text-decoration: underline; color: var(--sb-primary-hover); }
    .home__all:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); border-radius: var(--sb-radius-sm); }

    /* ── Error ──────────────────────────────────────────────────────────────── */
    .home__error {
      display: flex; flex-direction: column; align-items: center; text-align: center; gap: var(--sb-space-2);
      background: var(--sb-surface); border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-xl); padding: var(--sb-space-12) var(--sb-space-5);
    }
    .home__error-art { width: 140px; height: auto; margin-bottom: var(--sb-space-2); }
    .home__error-title { margin: 0; font-size: var(--sb-heading-sm-size); font-weight: 800; }
    .home__error-copy { margin: 0 0 var(--sb-space-2); max-width: 360px; color: var(--sb-text-muted); line-height: 1.5; }

    /* ── Skeleton ───────────────────────────────────────────────────────────── */
    .home__skeleton { display: flex; flex-direction: column; gap: var(--sb-space-6); }
    .home__sk-row { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: var(--sb-space-4); }
    .home__sk { border-radius: var(--sb-radius-lg); background: var(--sb-surface-sunken); animation: home-pulse 1.3s var(--sb-easing-standard) infinite; }
    .home__sk--hero { height: 196px; }
    .home__sk--kpi { height: 116px; }
    .home__sk--list { height: 320px; }
    @keyframes home-pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.55; } }

    /* ── Responsive ─────────────────────────────────────────────────────────── */
    @media (max-width: 980px) {
      .home__grid { grid-template-columns: 1fr; }
    }
    @media (max-width: 640px) {
      .home__hero { flex-wrap: wrap; padding: var(--sb-space-6); }
      .home__greeting { font-size: var(--sb-display-md-size); }
      .home__mascot { width: 104px; align-self: center; }
      .home__cta-row .home__btn { flex: 1; }
    }
  `],
})
export class HomeComponent {
  readonly #plan = inject(PlanService);
  readonly #auth = inject(StudentAuthStore);

  readonly #firstName = this.#auth.firstName;

  readonly plan = signal<MyPlanDto | null>(null);
  readonly loading = signal(true);
  readonly loadError = signal(false);

  // ── Hero ─────────────────────────────────────────────────────────────────────
  readonly greeting = computed(() => {
    const name = this.#firstName();
    return `${greetingPrefix()}${name ? ', ' + name : ''}!`;
  });

  // ── Steps ────────────────────────────────────────────────────────────────────
  readonly #steps = computed<MyPlanStep[]>(() => this.plan()?.steps ?? []);
  readonly pending = computed(() => this.#steps().filter((s) => s.status === 'Pending'));
  readonly completed = computed(() => this.#steps().filter((s) => s.status === 'Completed'));
  /**
   * The pending steps actually rendered as task rows. A generic, no-deadline `Redeem` step (the
   * onboarding "Redeem a code" / the all-done roll-forward) is **not** a task — it's surfaced by the
   * hero's "Redeem a code" button — so it's filtered out of the list.
   */
  readonly pendingTasks = computed(() =>
    this.pending().filter((s) => !(s.kind === 'Redeem' && s.dueState === 'None')),
  );

  readonly totalSteps = computed(() => this.plan()?.totalSteps ?? 0);
  readonly completedStepsCount = computed(() => this.plan()?.completedSteps ?? 0);
  readonly overdueSteps = computed(() => this.plan()?.overdueSteps ?? 0);

  // ── KPIs ─────────────────────────────────────────────────────────────────────
  readonly #kpis = computed<MyPlanKpis>(() => this.plan()?.kpis ?? ZERO_KPIS);
  readonly kpiCards = computed<HomeKpi[]>(() => {
    const k = this.#kpis();
    return [
      { label: 'Active sessions', value: String(k.activeSessions), caption: 'In progress now', icon: 'tv', accent: 'blue' },
      { label: 'Videos watched', value: `${k.videosWatched} / ${k.videosTotal}`, caption: 'Across all sessions', icon: 'play', accent: 'green' },
      { label: 'Overall progress', value: `${k.overallProgressPercent}%`, caption: 'Average completion', icon: 'chart', accent: 'purple' },
      { label: 'Completed', value: String(k.completedSessions), caption: 'Finished sessions', icon: 'check-square', accent: 'green' },
    ];
  });

  readonly focus = computed(() => this.plan()?.focus ?? null);
  readonly recent = computed(() => this.plan()?.recentlyEnrolled ?? []);

  // ── This-week bar ──────────────────────────────────────────────────────────────
  readonly weekPercent = computed(() => {
    const t = this.totalSteps();
    return t === 0 ? 0 : Math.round((100 * this.completedStepsCount()) / t);
  });

  // ── State machine: onboarding (no history) / all-caught-up / active ──────────────
  /** Any real enrollment history → render the full dashboard; otherwise the bare onboarding hero. */
  readonly #hasActivity = computed(() => {
    const k = this.#kpis();
    return (
      this.focus() !== null ||
      k.activeSessions > 0 ||
      k.completedSessions > 0 ||
      k.videosTotal > 0 ||
      this.recent().length > 0 ||
      this.completed().length > 0 ||
      this.pendingTasks().length > 0
    );
  });
  readonly showFullLayout = computed(() => this.#hasActivity());
  /** History but nothing left to do (everything complete / only a roll-forward redeem). */
  readonly allCaughtUp = computed(() => this.#hasActivity() && this.pendingTasks().length === 0);

  readonly heroMascot = computed(() => (this.allCaughtUp() ? MASCOT_RELAXING : MASCOT_STANDING));
  readonly heroSubtitle = computed(() => {
    if (!this.#hasActivity()) {
      return 'Redeem an access code from your teacher to unlock your first session — or browse the catalogue to see what’s available.';
    }
    if (this.allCaughtUp()) {
      return 'You’re all caught up — nice work! Redeem a code when you’re ready for your next session.';
    }
    const n = this.pendingTasks().length;
    const o = this.overdueSteps();
    const overdue = o > 0 ? ` — ${o} overdue` : '';
    return `You have ${n} ${n === 1 ? 'task' : 'tasks'} on your list${overdue}. Let’s knock them out.`;
  });

  constructor() {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.#plan.plan().subscribe({
      next: (dto) => {
        this.plan.set(dto);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
  }
}
