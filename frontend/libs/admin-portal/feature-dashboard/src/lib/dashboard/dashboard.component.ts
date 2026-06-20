import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { AuthStore } from '@sb/shared/data-access';
import { CardComponent, SelectComponent, SelectOption, ToastService } from '@sb/shared/ui';
import { AuditFeedItem, DashboardPeriod } from '../data-access/dashboard.models';
import { DashboardService } from '../data-access/dashboard.service';
import {
  DashIconName,
  FeedAccent,
  accentBg,
  accentFg,
  actionPhrase,
  actorLabel,
  bucketEnrollments,
  dashIconSvg,
  feedVisual,
  humanizeAction,
  relativeTime,
} from '../dashboard.presentation';

interface StatCardVm {
  label: string;
  value: string;
  icon: DashIconName;
  accent: FeedAccent;
  route: string[] | null;
}
interface QuickActionVm {
  label: string;
  icon: DashIconName;
  accent: FeedAccent;
  route: string[];
}

/**
 * Dashboard (FR-ADM-DASH-001..003, mockup `scrDashboard`). The academy's operational pulse: a period
 * selector, 4 KPI StatCards (Pending approvals · Active students · Codes used/active · Revenue by code),
 * an enrollments bar chart (daily for 7d, bucketed weekly for 30/90d), 4 role-gated quick actions, and
 * a Recent-activity feed of the latest 7 audit rows. All data from `GET /api/dashboard` (frozen
 * contract §2). StatCard trend deltas are demo-only in the prototype → omitted (contract §2).
 */
@Component({
  selector: 'sb-dashboard',
  standalone: true,
  imports: [ReactiveFormsModule, CardComponent, SelectComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!canRead()) {
      <div class="db-gate">
        <h3 class="db-gate__title">Access required</h3>
        <p class="db-gate__text">You don’t have permission to view the dashboard.</p>
      </div>
    } @else {
      <div class="db-head">
        <div>
          <h1 class="db-title">Dashboard</h1>
          <p class="db-subtitle">Operational pulse across your academy</p>
        </div>
        <div class="db-head__actions">
          <sb-select [formControl]="periodControl" [options]="periodOptions" placeholder="Period" />
        </div>
      </div>

      <!-- KPI stat cards -->
      <div class="db-stats" aria-label="Key metrics">
        @for (card of statCards(); track card.label) {
          <div
            class="db-stat"
            [class.db-stat--clickable]="card.route"
            [attr.aria-label]="card.label"
            (click)="onStatClick(card)"
          >
            <div class="db-stat__top">
              <span class="db-stat__label">{{ card.label }}</span>
              <span
                class="db-stat__icon"
                aria-hidden="true"
                [style.background]="bg(card.accent)"
                [style.color]="fg(card.accent)"
                [innerHTML]="icon(card.icon, 18)"
              ></span>
            </div>
            <div class="db-stat__value">{{ card.value }}</div>
          </div>
        }
      </div>

      <div class="db-grid">
        <div class="db-col">
          <!-- Enrollments chart -->
          <sb-card [title]="chartTitle()">
            <span cardActions class="db-cap">{{ enrollmentsTotal() }} total · {{ chart().granularity }}</span>
            <div class="db-chart">
              @for (b of chart().bars; track $index) {
                <div class="db-chart__col">
                  <div
                    class="db-chart__bar"
                    [style.height.px]="barHeight(b.value)"
                    [style.background]="$index === chart().bars.length - 1 ? 'var(--sb-primary)' : 'var(--sb-primary-200)'"
                    [title]="b.value + ' enrollments'"
                  ></div>
                  <div class="db-chart__label">{{ b.label }}</div>
                </div>
              }
            </div>
          </sb-card>

          <!-- Quick actions -->
          <sb-card title="Quick actions">
            <div class="db-actions">
              @for (q of quickActions(); track q.label) {
                <button type="button" class="db-action" (click)="go(q.route)">
                  <span
                    class="db-action__icon"
                    aria-hidden="true"
                    [style.background]="bg(q.accent)"
                    [style.color]="fg(q.accent)"
                    [innerHTML]="icon(q.icon, 19)"
                  ></span>
                  <span class="db-action__label">{{ q.label }}</span>
                </button>
              }
            </div>
          </sb-card>
        </div>

        <!-- Recent activity -->
        <sb-card title="Recent activity" [padding]="false">
          <button cardActions type="button" class="db-viewall" (click)="go(['/activity'])">View all</button>
          @if (recent().length === 0) {
            <div class="db-empty">No recent activity yet.</div>
          } @else {
            @for (a of recent(); track a.id; let i = $index) {
              <div class="db-feed__row" [class.db-feed__row--divided]="i < recent().length - 1">
                @let v = visual(a);
                <span
                  class="db-feed__icon"
                  aria-hidden="true"
                  [style.background]="bg(v.accent)"
                  [style.color]="fg(v.accent)"
                  [innerHTML]="icon(v.icon, 15)"
                ></span>
                <div class="db-feed__body">
                  <div class="db-feed__line">
                    <strong>{{ actor(a) }}</strong>
                    @if (phrase(a); as p) {
                      <span class="db-feed__rest"> {{ p }} <strong>{{ a.targetLabel }}</strong></span>
                    } @else {
                      <span class="db-feed__rest"> {{ fallback(a) }}</span>
                    }
                  </div>
                  <div class="db-feed__when">{{ when(a.occurredAtUtc) }}</div>
                </div>
              </div>
            }
          }
        </sb-card>
      </div>
    }
  `,
  styles: [`
    :host { display: flex; flex-direction: column; gap: var(--sb-space-5); }

    .db-head { display: flex; align-items: flex-end; justify-content: space-between; gap: var(--sb-space-4); flex-wrap: wrap; }
    .db-title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-xl-size); font-weight: 800; letter-spacing: -0.01em; color: var(--sb-text); }
    .db-subtitle { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
    .db-head__actions { width: 180px; }

    /* StatCards */
    .db-stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: var(--sb-space-4); }
    .db-stat {
      background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-lg);
      box-shadow: var(--sb-shadow-sm); padding: var(--sb-space-5);
      display: flex; flex-direction: column; gap: var(--sb-space-3);
    }
    .db-stat--clickable { cursor: pointer; transition: box-shadow var(--sb-timing), transform var(--sb-timing); }
    .db-stat--clickable:hover { box-shadow: var(--sb-shadow-md); transform: translateY(-2px); }
    .db-stat__top { display: flex; align-items: center; justify-content: space-between; }
    .db-stat__label { font-size: var(--sb-label-lg-size); font-weight: 600; color: var(--sb-text-muted); }
    .db-stat__icon { width: 36px; height: 36px; border-radius: var(--sb-radius-circle); display: inline-flex; align-items: center; justify-content: center; flex-shrink: 0; }
    .db-stat__value { font-size: var(--sb-heading-lg-size); font-weight: 800; color: var(--sb-text); line-height: 1; font-variant-numeric: tabular-nums; }

    /* Two-column body */
    .db-grid { display: grid; grid-template-columns: minmax(0, 1.6fr) minmax(0, 1fr); gap: var(--sb-space-4); align-items: start; }
    .db-col { display: flex; flex-direction: column; gap: var(--sb-space-4); min-width: 0; }
    @media (max-width: 900px) { .db-grid { grid-template-columns: 1fr; } }

    .db-cap { font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); font-weight: 600; }

    /* Chart */
    .db-chart { display: flex; align-items: flex-end; gap: 6px; height: 180px; padding-top: var(--sb-space-2); }
    .db-chart__col { flex: 1; display: flex; flex-direction: column; align-items: center; gap: 4px; min-width: 0; }
    .db-chart__bar { width: 100%; border-radius: 6px 6px 0 0; transition: height var(--sb-timing-slow) var(--sb-easing-standard); }
    .db-chart__label { font-size: 9px; color: var(--sb-text-subtle); white-space: nowrap; }

    /* Quick actions */
    .db-actions { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: var(--sb-space-3); }
    .db-action {
      display: flex; align-items: center; gap: var(--sb-space-3); padding: 14px 16px;
      border: 1px solid var(--sb-border); border-radius: var(--sb-radius-md); background: var(--sb-surface);
      cursor: pointer; text-align: left; font-family: var(--sb-font-sans); font-weight: 700; font-size: var(--sb-body-md-size); color: var(--sb-text);
      transition: background var(--sb-timing-fast), border-color var(--sb-timing-fast);
    }
    .db-action:hover { background: var(--sb-primary-50); border-color: var(--sb-primary); }
    .db-action:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }
    .db-action__icon { width: 38px; height: 38px; border-radius: 10px; display: inline-flex; align-items: center; justify-content: center; flex-shrink: 0; }

    /* Recent activity */
    .db-viewall { border: none; background: none; color: var(--sb-link); font-family: var(--sb-font-sans); font-weight: 700; font-size: var(--sb-body-sm-size); cursor: pointer; }
    .db-feed__row { display: flex; gap: var(--sb-space-3); padding: 13px 18px; }
    .db-feed__row--divided { border-bottom: 1px solid var(--sb-border); }
    .db-feed__icon { width: 32px; height: 32px; flex-shrink: 0; border-radius: var(--sb-radius-circle); display: inline-flex; align-items: center; justify-content: center; }
    .db-feed__body { min-width: 0; }
    .db-feed__line { font-size: var(--sb-body-md-size); line-height: 1.4; }
    .db-feed__when { font-size: var(--sb-body-sm-size); color: var(--sb-text-subtle); margin-top: 2px; }
    .db-empty { padding: var(--sb-space-8); text-align: center; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    .db-gate { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-lg); padding: var(--sb-space-10); text-align: center; }
    .db-gate__title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-sm-size); font-weight: 700; color: var(--sb-text); }
    .db-gate__text { margin: 0 auto; max-width: 380px; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
  `],
})
export class DashboardComponent implements OnInit {
  readonly #service = inject(DashboardService);
  readonly #auth = inject(AuthStore);
  readonly #router = inject(Router);
  readonly #fb = inject(FormBuilder);
  readonly #toast = inject(ToastService);
  readonly #sanitizer = inject(DomSanitizer);
  readonly #iconCache = new Map<string, SafeHtml>();

  readonly #summary = this.#service.summary;

  readonly canRead = computed(() => this.#auth.hasPermission('DashboardRead'));
  readonly isTeacher = computed(() => this.#auth.role() === 'Teacher');

  readonly period = signal<DashboardPeriod>('30d');
  readonly periodControl = this.#fb.control<DashboardPeriod>('30d', { nonNullable: true });
  readonly periodOptions: SelectOption[] = [
    { value: '7d', label: 'Last 7 days' },
    { value: '30d', label: 'Last 30 days' },
    { value: '90d', label: 'Last 90 days' },
  ];

  // ── KPIs ─────────────────────────────────────────────────────────────────────
  readonly statCards = computed<StatCardVm[]>(() => {
    const s = this.#summary();
    const revenue = (s?.revenueFromCodes ?? 0).toLocaleString();
    return [
      { label: 'Pending approvals', value: String(s?.pendingApprovals ?? 0), icon: 'inbox', accent: 'mustard', route: ['/approvals'] },
      { label: 'Active students', value: String(s?.activeStudents ?? 0), icon: 'users', accent: 'blue', route: null },
      { label: 'Codes used / active', value: `${s?.codesUsed ?? 0} / ${s?.codesActive ?? 0}`, icon: 'ticket', accent: 'green', route: null },
      { label: 'Revenue (by code)', value: `EGP ${revenue}`, icon: 'money', accent: 'purple', route: null },
    ];
  });

  // ── Enrollments chart ──────────────────────────────────────────────────────────
  readonly chart = computed(() => {
    const s = this.#summary();
    return s
      ? bucketEnrollments(s.enrollmentsByDay, this.period(), s.periodTo)
      : { bars: [], granularity: 'daily' as const };
  });
  readonly #maxBar = computed(() => Math.max(1, ...this.chart().bars.map((b) => b.value)));
  readonly enrollmentsTotal = computed(() => this.#summary()?.enrollmentsTotal ?? 0);
  readonly chartTitle = computed(
    () => `Enrollments — last ${({ '7d': '7', '30d': '30', '90d': '90' } as const)[this.period()]} days`,
  );

  // ── Quick actions (role-gated) ──────────────────────────────────────────────────
  readonly quickActions = computed<QuickActionVm[]>(() =>
    [
      { label: 'Review approvals', icon: 'inbox' as const, accent: 'mustard' as const, route: ['/approvals'], show: true },
      { label: 'Generate codes', icon: 'ticket' as const, accent: 'green' as const, route: ['/codes/generate'], show: this.isTeacher() },
      { label: 'Create session', icon: 'plus' as const, accent: 'blue' as const, route: ['/sessions/new'], show: true },
      { label: 'Open attendance', icon: 'clipboard' as const, accent: 'purple' as const, route: ['/attendance'], show: true },
    ]
      .filter((q) => q.show)
      .map(({ label, icon, accent, route }) => ({ label, icon, accent, route })),
  );

  // ── Recent activity ──────────────────────────────────────────────────────────────
  readonly recent = computed<AuditFeedItem[]>(() => (this.#summary()?.recentActivity ?? []).slice(0, 7));

  constructor() {
    this.periodControl.valueChanges.pipe(takeUntilDestroyed()).subscribe((value) => {
      this.period.set(value);
      void this.reload();
    });
  }

  ngOnInit(): void {
    if (!this.canRead()) return;
    void this.reload();
  }

  async reload(): Promise<void> {
    try {
      await this.#service.load({ period: this.period() });
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not load the dashboard.');
    }
  }

  barHeight(value: number): number {
    return (value / this.#maxBar()) * 150;
  }

  onStatClick(card: StatCardVm): void {
    if (card.route) this.go(card.route);
  }

  go(route: string[]): void {
    void this.#router.navigate(route);
  }

  // ── Presentation helpers (template-facing) ───────────────────────────────────────
  actor(row: AuditFeedItem): string {
    return actorLabel(row);
  }
  visual(row: AuditFeedItem): { icon: DashIconName; accent: FeedAccent } {
    return feedVisual(row);
  }
  phrase(row: AuditFeedItem): string | null {
    return actionPhrase(row.action);
  }
  fallback(row: AuditFeedItem): string {
    return row.summary ?? humanizeAction(row.action);
  }
  when(iso: string | null): string {
    return relativeTime(iso);
  }
  bg = accentBg;
  fg = accentFg;

  /** Bypass the HTML sanitizer for developer-authored constant SVG icon markup (see sidebar). */
  icon(name: DashIconName, size = 18): SafeHtml {
    const svg = dashIconSvg(name, size);
    let trusted = this.#iconCache.get(svg);
    if (!trusted) {
      trusted = this.#sanitizer.bypassSecurityTrustHtml(svg);
      this.#iconCache.set(svg, trusted);
    }
    return trusted;
  }
}
