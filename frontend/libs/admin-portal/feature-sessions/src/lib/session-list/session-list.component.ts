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
import { debounceTime } from 'rxjs';
import { AuthStore } from '@sb/shared/data-access';
import {
  ButtonComponent,
  PaginationComponent,
  SbTableColumn,
  SelectComponent,
  SelectOption,
  StatusPillComponent,
  TableCellDirective,
  TableComponent,
  TagComponent,
  ToastService,
} from '@sb/shared/ui';
import { SessionListDto, SessionStatus } from '../data-access/session.models';
import { SessionService } from '../data-access/session.service';
import { statusPill, subjectAccent } from '../session.presentation';

/**
 * Sessions catalogue (FR-ADM-SES-001, mockup `scrSessions`). Header with a primary "Create session"
 * action; filter-bar card (search + grade + subject + status); paged table
 * (Session accent-tile+title · Grade · Specialization · State · Qs · Videos · Enrolled) with Edit/Manage
 * row actions. The server enforces the granular `Sessions*` permissions on every mutation.
 */
@Component({
  selector: 'sb-session-list',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    ButtonComponent,
    SelectComponent,
    StatusPillComponent,
    TagComponent,
    TableComponent,
    TableCellDirective,
    PaginationComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!canRead()) {
      <div class="ses__gate">
        <span class="ses__gate-icon" aria-hidden="true">
          <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
            <path d="M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2zM7 11V7a5 5 0 0 1 10 0v4"/>
          </svg>
        </span>
        <h3 class="ses__gate-title">Access required</h3>
        <p class="ses__gate-text">You don’t have permission to view sessions.</p>
      </div>
    } @else {
      <div class="ses__head">
        <div>
          <h1 class="ses__title">Sessions</h1>
          <p class="ses__subtitle">{{ total() }} in catalogue · author content, videos &amp; gating</p>
        </div>
        <sb-button variant="primary" (clicked)="create()">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="M12 5v14M5 12h14"/>
          </svg>
          Create session
        </sb-button>
      </div>

      <!-- Filter bar -->
      <div class="ses__filterbar" [formGroup]="filters">
        <div class="ses__search">
          <span class="ses__search-icon" aria-hidden="true">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 21l-4.35-4.35M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16z"/>
            </svg>
          </span>
          <input
            type="search"
            class="ses__search-input"
            formControlName="search"
            placeholder="Search sessions…"
            autocomplete="off"
            aria-label="Search sessions"
          />
        </div>
        <sb-select class="ses__filter" formControlName="gradeId" [options]="gradeOptions()" placeholder="All grades" />
        <sb-select class="ses__filter" formControlName="subjectId" [options]="subjectOptions()" placeholder="All subjects" />
        <sb-select class="ses__filter" formControlName="status" [options]="statusOptions" placeholder="All states" />
      </div>

      @if (rows().length === 0 && !isLoading()) {
        <div class="ses__empty">
          {{ hasFilters() ? 'No sessions match these filters.' : 'No sessions yet. Create your first session to start building the catalogue.' }}
        </div>
      } @else {
        <sb-table [columns]="columns" [rows]="rows()" [rowKey]="byId">
          <ng-template sbTableCell="title" let-row>
            <div class="ses__cell" (click)="manage(row)">
              <span class="ses__thumb" [style.background]="tileBg(row)" [style.color]="tileFg(row)">
                <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                     stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>
                </svg>
              </span>
              <div>
                <div class="ses__name">{{ row.title }}</div>
                <div class="ses__spec">{{ row.specializationName ?? '—' }}</div>
              </div>
            </div>
          </ng-template>

          <ng-template sbTableCell="grade" let-row><span class="ses__strong">{{ row.gradeName ?? '—' }}</span></ng-template>

          <ng-template sbTableCell="specialization" let-row>
            @if (row.specializationName) {
              <sb-tag [label]="row.specializationName" [subject]="accent(row)" />
            } @else { — }
          </ng-template>

          <ng-template sbTableCell="status" let-row>
            <sb-status-pill [variant]="pillFor(row.status)">{{ row.status }}</sb-status-pill>
          </ng-template>

          <ng-template sbTableCell="questions" let-row><span class="ses__num">{{ row.questionCount }}</span></ng-template>
          <ng-template sbTableCell="videos" let-row><span class="ses__num">{{ row.videoCount }}</span></ng-template>
          <ng-template sbTableCell="enrolled" let-row><strong class="ses__num">{{ row.enrolledCount }}</strong></ng-template>

          <ng-template sbTableCell="actions" let-row>
            <div class="ses__actions">
              <sb-button variant="secondary" size="sm" (clicked)="edit(row)">Edit</sb-button>
              <sb-button variant="ghost" size="sm" (clicked)="manage(row)">Manage</sb-button>
            </div>
          </ng-template>
        </sb-table>

        @if (total() > 0) {
          <div class="ses__pager">
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

    .ses__head { display: flex; align-items: flex-end; justify-content: space-between; gap: var(--sb-space-4); flex-wrap: wrap; }
    .ses__title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-xl-size); font-weight: 800; letter-spacing: -0.01em; color: var(--sb-text); }
    .ses__subtitle { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    .ses__filterbar {
      display: flex; gap: var(--sb-space-3); flex-wrap: wrap; align-items: center;
      background: var(--sb-surface); border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-lg); padding: var(--sb-space-3);
    }
    .ses__search {
      flex: 1 1 220px; display: flex; align-items: center; gap: var(--sb-space-2);
      height: 40px; padding: 0 var(--sb-space-3);
      background: var(--sb-surface-sunken); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-md);
    }
    .ses__search-icon { display: inline-flex; color: var(--sb-text-subtle); flex-shrink: 0; }
    .ses__search-input { flex: 1; min-width: 0; border: none; outline: none; background: transparent; font-family: var(--sb-font-sans); font-size: var(--sb-body-md-size); color: var(--sb-text); }
    .ses__filter { width: 170px; }

    .ses__cell { display: flex; align-items: center; gap: var(--sb-space-3); cursor: pointer; }
    .ses__thumb {
      width: 46px; height: 34px; border-radius: var(--sb-radius-sm); flex-shrink: 0;
      display: inline-flex; align-items: center; justify-content: center; overflow: hidden;
    }
    .ses__thumb-img { width: 100%; height: 100%; object-fit: cover; }
    .ses__name { font-weight: 700; font-size: var(--sb-body-md-size); color: var(--sb-text); }
    .ses__spec { font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); }
    .ses__strong { font-weight: 600; }
    .ses__num { font-variant-numeric: tabular-nums; }
    .ses__actions { display: inline-flex; gap: var(--sb-space-2); justify-content: flex-end; align-items: center; }
    .ses__pager { margin-top: var(--sb-space-2); }

    .ses__empty {
      background: var(--sb-surface); border: 1px dashed var(--sb-border-strong);
      border-radius: var(--sb-radius-lg); padding: var(--sb-space-12); text-align: center;
      color: var(--sb-text-muted); font-size: var(--sb-body-md-size);
    }

    .ses__gate { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-lg); padding: var(--sb-space-10); text-align: center; }
    .ses__gate-icon { display: inline-flex; align-items: center; justify-content: center; width: 56px; height: 56px; margin: 0 auto var(--sb-space-3); border-radius: var(--sb-radius-circle); background: var(--sb-warning-bg); color: var(--sb-warning-fg); }
    .ses__gate-title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-sm-size); font-weight: 700; color: var(--sb-text); }
    .ses__gate-text { margin: 0 auto; max-width: 380px; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
  `],
})
export class SessionListComponent implements OnInit {
  readonly #service = inject(SessionService);
  readonly #auth = inject(AuthStore);
  readonly #router = inject(Router);
  readonly #fb = inject(FormBuilder);
  readonly #toast = inject(ToastService);

  readonly rows = this.#service.sessions;
  readonly total = this.#service.total;
  readonly isLoading = this.#service.isLoading;

  readonly canRead = computed(() => this.#auth.hasPermission('SessionsRead'));

  readonly pageSize = 10;
  readonly page = signal(1);
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));

  readonly filters = this.#fb.group({
    search: [''],
    gradeId: [''],
    subjectId: [''],
    status: [''],
  });

  readonly statusOptions: SelectOption[] = [
    { value: '', label: 'All states' },
    { value: 'Draft', label: 'Draft' },
    { value: 'Published', label: 'Published' },
    { value: 'Archived', label: 'Archived' },
  ];

  readonly gradeOptions = computed<SelectOption[]>(() => [
    { value: '', label: 'All grades' },
    ...this.#service.grades().map((g) => ({ value: g.id, label: g.name })),
  ]);

  readonly subjectOptions = computed<SelectOption[]>(() => [
    { value: '', label: 'All subjects' },
    ...this.#service.subjects().map((s) => ({ value: s.id, label: s.name })),
  ]);

  readonly columns: readonly SbTableColumn[] = [
    { key: 'title', header: 'Session' },
    { key: 'grade', header: 'Grade' },
    { key: 'specialization', header: 'Specialization' },
    { key: 'status', header: 'State' },
    { key: 'questions', header: 'Qs', align: 'right' },
    { key: 'videos', header: 'Videos', align: 'right' },
    { key: 'enrolled', header: 'Enrolled', align: 'right' },
    { key: 'actions', header: '', align: 'right', width: '1%' },
  ];

  readonly byId = (row: SessionListDto): string => row.id;

  constructor() {
    this.filters.valueChanges.pipe(debounceTime(250), takeUntilDestroyed()).subscribe(() => {
      this.page.set(1);
      void this.reload();
    });
  }

  ngOnInit(): void {
    if (!this.canRead()) return;
    void this.#service.loadGrades();
    void this.#service.loadSubjects();
    void this.reload();
  }

  hasFilters(): boolean {
    const f = this.filters.getRawValue();
    return !!(f.search?.trim() || f.gradeId || f.subjectId || f.status);
  }

  #query(pageSize = this.pageSize) {
    const f = this.filters.getRawValue();
    return {
      search: f.search?.trim() || undefined,
      gradeId: f.gradeId || null,
      subjectId: f.subjectId || null,
      status: (f.status || null) as SessionStatus | null,
      pageSize,
    };
  }

  async reload(): Promise<void> {
    try {
      await this.#service.list({ ...this.#query(), page: this.page() });
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not load sessions.');
    }
  }

  onPageChange(page: number): void {
    this.page.set(page);
    void this.reload();
  }

  create(): void {
    void this.#router.navigate(['/sessions/new']);
  }

  edit(session: SessionListDto): void {
    void this.#router.navigate(['/sessions', session.id, 'edit']);
  }

  manage(session: SessionListDto): void {
    void this.#router.navigate(['/sessions', session.id]);
  }

  // Presentation helpers
  pillFor = statusPill;
  accent = (row: SessionListDto): string => subjectAccent(row.specializationName);
  tileBg = (row: SessionListDto): string => `var(--sb-subject-${this.accent(row)}-bg)`;
  tileFg = (row: SessionListDto): string => `var(--sb-subject-${this.accent(row)}-deep)`;
}
