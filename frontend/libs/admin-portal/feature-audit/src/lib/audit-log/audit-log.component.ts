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
import { ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { AuthStore } from '@sb/shared/data-access';
import {
  AlertComponent,
  ButtonComponent,
  PaginationComponent,
  SbTableColumn,
  SelectComponent,
  SelectOption,
  TableCellDirective,
  TableComponent,
  ToastService,
} from '@sb/shared/ui';
import { AuditCategory, AuditFeedItem, AuditListQuery, AuditPeriod } from '../data-access/audit.models';
import { AuditService } from '../data-access/audit.service';
import {
  AuditIconName,
  FeedAccent,
  accentBg,
  accentFg,
  actionPhrase,
  actorLabel,
  auditIconSvg,
  feedVisual,
  humanizeAction,
  relativeTime,
  targetRoute,
} from '../audit.presentation';

/**
 * Activity-log browser (FR-ADM-AUD-001..003, mockup `scrActivity`). The audit feed: a filter bar
 * (Actor · Action-category · Period), a paged table where each row is
 * `«icon» **actor** action **target** · when`, and a per-row **"View"** that *navigates to the
 * affected entity* (contract §1 — there is no before/after detail drawer in 5A). Assistants see a
 * "Scoped view" alert; the server still enforces `AuditRead`/`AuditReadSensitive` (default-deny), so
 * the UI only reflects role.
 *
 * Actor filtering is **client-side over the loaded page** (no facet endpoint in 5A, and `AuditFeedItem`
 * carries no `actorId`) — exactly like the prototype. Category + Period reload from the server.
 */
@Component({
  selector: 'sb-audit-log',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    AlertComponent,
    ButtonComponent,
    SelectComponent,
    TableComponent,
    TableCellDirective,
    PaginationComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!canRead()) {
      <div class="al__gate">
        <span class="al__gate-icon" aria-hidden="true">
          <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
            <path d="M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2zM7 11V7a5 5 0 0 1 10 0v4"/>
          </svg>
        </span>
        <h3 class="al__gate-title">Access required</h3>
        <p class="al__gate-text">You don’t have permission to view the activity log.</p>
      </div>
    } @else {
      <div class="al__head">
        <h1 class="al__title">Activity log</h1>
        <p class="al__subtitle">{{ subtitle() }}</p>
      </div>

      <!-- Filter bar -->
      <div class="al__filterbar" [formGroup]="filters">
        <sb-select class="al__filter al__filter--actor" formControlName="actor" [options]="actorOptions()" placeholder="All actors" />
        <sb-select class="al__filter" formControlName="category" [options]="categoryOptions" placeholder="All actions" />
        <sb-select class="al__filter" formControlName="period" [options]="periodOptions" placeholder="Period" />
      </div>

      @if (!isTeacher()) {
        <sb-alert variant="info" title="Scoped view">
          Assistants see a subset of the audit log. Sensitive entries (e.g. who-read-what) are visible to Admins only.
        </sb-alert>
      }

      @if (displayedRows().length === 0 && !isLoading()) {
        <div class="al__empty">No activity matches these filters.</div>
      } @else {
        <sb-table [columns]="columns" [rows]="displayedRows()" [rowKey]="byId">
          <ng-template sbTableCell="actor" let-row>
            <span class="al__actor">{{ actor(row) }}</span>
          </ng-template>

          <ng-template sbTableCell="action" let-row>
            @let v = visual(row);
            <span class="al__action">
              <span
                class="al__icon"
                aria-hidden="true"
                [style.background]="bg(v.accent)"
                [style.color]="fg(v.accent)"
                [innerHTML]="icon(v.icon)"
              ></span>
              @if (phrase(row); as p) {
                <span class="al__action-text">{{ p }} <strong>{{ row.targetLabel }}</strong></span>
              } @else {
                <span class="al__action-text">{{ fallback(row) }}</span>
              }
            </span>
          </ng-template>

          <ng-template sbTableCell="when" let-row>
            <span class="al__when">{{ when(row.occurredAtUtc) }}</span>
          </ng-template>

          <ng-template sbTableCell="view" let-row>
            <sb-button variant="ghost" size="sm" (clicked)="view(row)">View</sb-button>
          </ng-template>
        </sb-table>

        @if (total() > 0) {
          <div class="al__pager">
            <sb-pagination
              [page]="page()"
              [pageCount]="pageCount()"
              [total]="total()"
              [pageSize]="pageSize"
              (pageChange)="onPageChange($event)"
            />
          </div>
        }
      }
    }
  `,
  styles: [`
    :host { display: flex; flex-direction: column; gap: var(--sb-space-4); }

    .al__head { }
    .al__title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-xl-size); font-weight: 800; letter-spacing: -0.01em; color: var(--sb-text); }
    .al__subtitle { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    sb-alert { display: block; }

    .al__filterbar {
      display: flex; gap: var(--sb-space-3); flex-wrap: wrap; align-items: center;
      background: var(--sb-surface); border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-lg); padding: var(--sb-space-3);
    }
    .al__filter { width: 170px; }
    .al__filter--actor { width: 190px; }

    .al__actor { font-weight: 700; }

    .al__action { display: inline-flex; align-items: center; gap: var(--sb-space-2); }
    .al__icon {
      display: inline-flex; align-items: center; justify-content: center; flex-shrink: 0;
      width: 24px; height: 24px; border-radius: var(--sb-radius-circle);
    }
    .al__action-text { min-width: 0; }

    .al__when { color: var(--sb-text-muted); white-space: nowrap; }

    .al__pager { margin-top: var(--sb-space-2); }

    .al__empty {
      background: var(--sb-surface); border: 1px dashed var(--sb-border-strong);
      border-radius: var(--sb-radius-lg); padding: var(--sb-space-12); text-align: center;
      color: var(--sb-text-muted); font-size: var(--sb-body-md-size);
    }

    .al__gate { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-lg); padding: var(--sb-space-10); text-align: center; }
    .al__gate-icon { display: inline-flex; align-items: center; justify-content: center; width: 56px; height: 56px; margin: 0 auto var(--sb-space-3); border-radius: var(--sb-radius-circle); background: var(--sb-warning-bg); color: var(--sb-warning-fg); }
    .al__gate-title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-sm-size); font-weight: 700; color: var(--sb-text); }
    .al__gate-text { margin: 0 auto; max-width: 380px; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
  `],
})
export class AuditLogComponent implements OnInit {
  readonly #service = inject(AuditService);
  readonly #auth = inject(AuthStore);
  readonly #router = inject(Router);
  readonly #route = inject(ActivatedRoute);
  readonly #fb = inject(FormBuilder);
  readonly #toast = inject(ToastService);
  readonly #sanitizer = inject(DomSanitizer);
  readonly #iconCache = new Map<string, SafeHtml>();

  readonly rows = this.#service.items;
  readonly total = this.#service.total;
  readonly isLoading = this.#service.isLoading;

  readonly canRead = computed(() => this.#auth.hasPermission('AuditRead'));
  readonly isTeacher = computed(() => this.#auth.role() === 'Teacher');
  readonly subtitle = computed(() =>
    this.isTeacher()
      ? 'Full audit feed — who did what, when & where'
      : 'Scoped audit feed for your actions and assigned areas',
  );

  readonly pageSize = 20;
  readonly page = signal(1);
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));

  /** Client-side Actor filter (the feed exposes no `actorId`; we filter the loaded page by name). */
  readonly #selectedActor = signal('');
  readonly displayedRows = computed(() => {
    const actor = this.#selectedActor();
    const all = this.rows();
    return actor ? all.filter((r) => actorLabel(r) === actor) : all;
  });

  /** Actor options are distinct actor names from the loaded rows (no facet endpoint in 5A). */
  readonly actorOptions = computed<SelectOption[]>(() => {
    const names = [...new Set(this.rows().map((r) => actorLabel(r)))];
    return [{ value: '', label: 'All actors' }, ...names.map((n) => ({ value: n, label: n }))];
  });

  readonly categoryOptions: SelectOption[] = [
    { value: '', label: 'All actions' },
    { value: 'approval', label: 'Approvals' },
    { value: 'code', label: 'Codes' },
    { value: 'session', label: 'Sessions' },
    { value: 'device', label: 'Devices' },
  ];

  readonly periodOptions: SelectOption[] = [
    { value: '7d', label: 'Last 7 days' },
    { value: '30d', label: 'Last 30 days' },
    { value: '90d', label: 'Last 90 days' },
  ];

  readonly columns: readonly SbTableColumn[] = [
    { key: 'actor', header: 'Actor' },
    { key: 'action', header: 'Action' },
    { key: 'when', header: 'When' },
    { key: 'view', header: '', align: 'right', width: '1%' },
  ];

  readonly filters = this.#fb.group({
    actor: [''],
    category: [''],
    period: ['7d'],
  });

  readonly byId = (row: AuditFeedItem): string => row.id;

  /** Optional entity-tab scoping read from the URL (so `student-detail`/`session-detail` can reuse this list). */
  #studentId: string | null = null;
  #sessionId: string | null = null;
  #entityType: string | null = null;
  #entityId: string | null = null;

  constructor() {
    // Actor changes filter the loaded rows client-side (no reload). Category/Period reload from server.
    this.filters.controls.actor.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((v) => this.#selectedActor.set(v ?? ''));
    this.filters.controls.category.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.#reloadFromFilters());
    this.filters.controls.period.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.#reloadFromFilters());
  }

  ngOnInit(): void {
    if (!this.canRead()) return;
    const qp = this.#route.snapshot.queryParamMap;
    this.#studentId = qp.get('studentId');
    this.#sessionId = qp.get('sessionId');
    this.#entityType = qp.get('entityType');
    this.#entityId = qp.get('entityId');
    void this.reload();
  }

  #reloadFromFilters(): void {
    this.page.set(1);
    void this.reload();
  }

  #query(): AuditListQuery {
    const f = this.filters.getRawValue();
    return {
      category: (f.category || null) as AuditCategory | null,
      period: (f.period || '7d') as AuditPeriod,
      studentId: this.#studentId,
      sessionId: this.#sessionId,
      entityType: this.#entityType,
      entityId: this.#entityId,
      page: this.page(),
      pageSize: this.pageSize,
    };
  }

  async reload(): Promise<void> {
    try {
      await this.#service.list(this.#query());
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not load the activity log.');
    }
  }

  onPageChange(page: number): void {
    this.page.set(page);
    void this.reload();
  }

  /** Drill-in: navigate to the affected entity, or toast when the row has no linked entity. */
  view(row: AuditFeedItem): void {
    const route = targetRoute(row.targetType, row.targetId);
    if (route) void this.#router.navigate(route);
    else this.#toast.info('No linked entity for this entry');
  }

  // ── Presentation helpers (template-facing) ───────────────────────────────────────
  actor(row: AuditFeedItem): string {
    return actorLabel(row);
  }
  visual(row: AuditFeedItem): { icon: AuditIconName; accent: FeedAccent } {
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
  icon(name: AuditIconName): SafeHtml {
    const svg = auditIconSvg(name, 13);
    let trusted = this.#iconCache.get(svg);
    if (!trusted) {
      trusted = this.#sanitizer.bypassSecurityTrustHtml(svg);
      this.#iconCache.set(svg, trusted);
    }
    return trusted;
  }
}
