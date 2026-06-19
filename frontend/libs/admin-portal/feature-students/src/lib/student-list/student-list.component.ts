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
import { AuthStore, PendingApprovalsStore } from '@sb/shared/data-access';
import {
  AvatarComponent,
  ButtonComponent,
  PaginationComponent,
  SbTableColumn,
  SelectComponent,
  SelectOption,
  StatusPillComponent,
  TableCellDirective,
  TableComponent,
  ToastService,
} from '@sb/shared/ui';
import { StudentListItem, StudentStatus } from '../data-access/student.models';
import { StudentService } from '../data-access/student.service';
import { avatarSubject, relativeTime, statusDot, statusPill, studentInitials } from '../student.presentation';

/**
 * Students triage list (FR-ADM-STU-001, mockup `scrStudents`). Server-side filter by status + grade,
 * debounced search, a filter-bar card, paged table (Student · Grade · City · Status · Device · Joined),
 * inline approve + open-detail, and CSV export. Controls are permission-gated; the server enforces them.
 */
@Component({
  selector: 'sb-student-list',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    ButtonComponent,
    SelectComponent,
    StatusPillComponent,
    AvatarComponent,
    TableComponent,
    TableCellDirective,
    PaginationComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!canRead()) {
      <div class="stu__gate">
        <span class="stu__gate-icon" aria-hidden="true">
          <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
            <path d="M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2zM7 11V7a5 5 0 0 1 10 0v4"/>
          </svg>
        </span>
        <h3 class="stu__gate-title">Access required</h3>
        <p class="stu__gate-text">You don’t have permission to view students.</p>
      </div>
    } @else {
      <!-- Header -->
      <div class="stu__head">
        <div>
          <h1 class="stu__title">Students</h1>
          <p class="stu__subtitle">{{ total() }} total · triage, review &amp; manage learner accounts</p>
        </div>
        <sb-button variant="secondary" [loading]="exporting()" (clicked)="exportCsv()">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"/>
          </svg>
          Export
        </sb-button>
      </div>

      <!-- Filter bar -->
      <div class="stu__filterbar" [formGroup]="filters">
        <div class="stu__search">
          <span class="stu__search-icon" aria-hidden="true">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 21l-4.35-4.35M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16z"/>
            </svg>
          </span>
          <input
            type="search"
            class="stu__search-input"
            formControlName="search"
            placeholder="Search by name, phone, city…"
            autocomplete="off"
            aria-label="Search students"
          />
        </div>
        <sb-select class="stu__filter" formControlName="status" [options]="statusOptions" placeholder="All statuses" />
        <sb-select class="stu__filter" formControlName="gradeId" [options]="gradeOptions()" placeholder="All grades" />
      </div>

      @if (rows().length === 0 && !isLoading()) {
        <div class="stu__empty">
          {{ hasFilters() ? 'No students match these filters.' : 'No students yet. They appear here after self-registration.' }}
        </div>
      } @else {
        <sb-table [columns]="columns" [rows]="rows()" [rowKey]="byId">
          <ng-template sbTableCell="student" let-row>
            <div class="stu__member" (click)="view(row)">
              <sb-avatar [initials]="initials(row.fullName)" [subject]="subjectFor(row.id)" [status]="dotFor(row.status)" />
              <div>
                <div class="stu__name">{{ row.fullName }}</div>
                <div class="stu__phone">{{ row.phoneNumber }}</div>
              </div>
            </div>
          </ng-template>

          <ng-template sbTableCell="grade" let-row><span class="stu__strong">{{ row.gradeName ?? '—' }}</span></ng-template>
          <ng-template sbTableCell="city" let-row>{{ row.cityName ?? '—' }}</ng-template>

          <ng-template sbTableCell="status" let-row>
            <sb-status-pill [variant]="pillFor(row.status)">{{ row.status }}</sb-status-pill>
          </ng-template>

          <ng-template sbTableCell="device" let-row>
            @if (row.activeDeviceSummary) {
              <span class="stu__device">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                     stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <path d="M5 2h14a2 2 0 0 1 2 2v16a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2zM11 18h2"/>
                </svg>
                {{ row.activeDeviceSummary }}
              </span>
            } @else {
              <span class="stu__notbound">Not bound</span>
            }
          </ng-template>

          <ng-template sbTableCell="joined" let-row><span class="stu__muted">{{ joined(row.createdAtUtc) }}</span></ng-template>

          <ng-template sbTableCell="actions" let-row>
            <div class="stu__actions">
              @if (row.status === 'Pending' && canApprove()) {
                <sb-button variant="accent" size="sm" (clicked)="approve(row)">Approve</sb-button>
              }
              <sb-button variant="secondary" size="sm" (clicked)="view(row)">View</sb-button>
            </div>
          </ng-template>
        </sb-table>

        @if (total() > 0) {
          <div class="stu__pager">
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

    .stu__head { display: flex; align-items: flex-end; justify-content: space-between; gap: var(--sb-space-4); flex-wrap: wrap; }
    .stu__title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-xl-size); font-weight: 800; letter-spacing: -0.01em; color: var(--sb-text); }
    .stu__subtitle { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    /* Filter bar card */
    .stu__filterbar {
      display: flex;
      gap: var(--sb-space-3);
      flex-wrap: wrap;
      align-items: center;
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-lg);
      padding: var(--sb-space-3);
    }
    .stu__search {
      flex: 1 1 240px;
      display: flex;
      align-items: center;
      gap: var(--sb-space-2);
      height: 40px;
      padding: 0 var(--sb-space-3);
      background: var(--sb-surface-sunken);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-md);
    }
    .stu__search-icon { display: inline-flex; color: var(--sb-text-subtle); flex-shrink: 0; }
    .stu__search-input {
      flex: 1; min-width: 0; border: none; outline: none; background: transparent;
      font-family: var(--sb-font-sans); font-size: var(--sb-body-md-size); color: var(--sb-text);
    }
    .stu__filter { width: 170px; }

    /* Table cells */
    .stu__member { display: flex; align-items: center; gap: var(--sb-space-3); cursor: pointer; }
    .stu__name { font-weight: 700; font-size: var(--sb-body-md-size); color: var(--sb-text); }
    .stu__phone { font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); }
    .stu__strong { font-weight: 600; }
    .stu__muted { color: var(--sb-text-muted); white-space: nowrap; }
    .stu__device { display: inline-flex; align-items: center; gap: 6px; font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); }
    .stu__notbound { color: var(--sb-text-subtle); font-size: var(--sb-body-sm-size); }
    .stu__actions { display: inline-flex; gap: var(--sb-space-2); justify-content: flex-end; align-items: center; }

    .stu__pager { margin-top: var(--sb-space-2); }

    /* Inline empty (dashed) */
    .stu__empty {
      background: var(--sb-surface);
      border: 1px dashed var(--sb-border-strong);
      border-radius: var(--sb-radius-lg);
      padding: var(--sb-space-12);
      text-align: center;
      color: var(--sb-text-muted);
      font-size: var(--sb-body-md-size);
    }

    /* Access gate */
    .stu__gate { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-lg); padding: var(--sb-space-10); text-align: center; }
    .stu__gate-icon { display: inline-flex; align-items: center; justify-content: center; width: 56px; height: 56px; margin: 0 auto var(--sb-space-3); border-radius: var(--sb-radius-circle); background: var(--sb-warning-bg); color: var(--sb-warning-fg); }
    .stu__gate-title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-sm-size); font-weight: 700; color: var(--sb-text); }
    .stu__gate-text { margin: 0 auto; max-width: 380px; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
  `],
})
export class StudentListComponent implements OnInit {
  readonly #service = inject(StudentService);
  readonly #auth = inject(AuthStore);
  readonly #router = inject(Router);
  readonly #fb = inject(FormBuilder);
  readonly #toast = inject(ToastService);
  readonly #pendingApprovals = inject(PendingApprovalsStore);

  readonly rows = this.#service.students;
  readonly total = this.#service.total;
  readonly isLoading = this.#service.isLoading;

  readonly canRead = computed(() => this.#auth.hasPermission('StudentsRead'));
  readonly canApprove = computed(() => this.#auth.hasPermission('StudentsApprove'));

  readonly pageSize = 10;
  readonly page = signal(1);
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));
  readonly exporting = signal(false);

  readonly filters = this.#fb.group({ search: [''], status: [''], gradeId: [''] });

  readonly statusOptions: SelectOption[] = [
    { value: '', label: 'All statuses' },
    { value: 'Pending', label: 'Pending' },
    { value: 'Active', label: 'Active' },
    { value: 'Rejected', label: 'Rejected' },
    { value: 'Inactive', label: 'Inactive' },
  ];

  readonly gradeOptions = computed<SelectOption[]>(() => [
    { value: '', label: 'All grades' },
    ...this.#service.grades().map((g) => ({ value: g.id, label: g.name })),
  ]);

  readonly columns: readonly SbTableColumn[] = [
    { key: 'student', header: 'Student' },
    { key: 'grade', header: 'Grade' },
    { key: 'city', header: 'City' },
    { key: 'status', header: 'Status' },
    { key: 'device', header: 'Device' },
    { key: 'joined', header: 'Joined' },
    { key: 'actions', header: '', align: 'right', width: '1%' },
  ];

  readonly byId = (row: StudentListItem): string => row.id;

  constructor() {
    this.filters.valueChanges.pipe(debounceTime(250), takeUntilDestroyed()).subscribe(() => {
      this.page.set(1);
      void this.reload();
    });
  }

  ngOnInit(): void {
    if (!this.canRead()) return;
    void this.#service.loadGrades();
    void this.reload();
  }

  hasFilters(): boolean {
    const f = this.filters.getRawValue();
    return !!(f.search?.trim() || f.status || f.gradeId);
  }

  #query(pageSize = this.pageSize) {
    const f = this.filters.getRawValue();
    return {
      search: f.search?.trim() || undefined,
      status: (f.status || null) as StudentStatus | null,
      gradeId: f.gradeId || null,
      pageSize,
    };
  }

  async reload(): Promise<void> {
    try {
      await this.#service.list({ ...this.#query(), page: this.page() });
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not load students.');
    }
  }

  onPageChange(page: number): void {
    this.page.set(page);
    void this.reload();
  }

  view(student: StudentListItem): void {
    void this.#router.navigate(['/students', student.id]);
  }

  async approve(student: StudentListItem): Promise<void> {
    try {
      await this.#service.approve(student.id);
      this.#toast.success('Student approved & sign-in enabled');
      await this.reload();
      void this.#pendingApprovals.refresh();
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not approve this student.');
    }
  }

  async exportCsv(): Promise<void> {
    this.exporting.set(true);
    try {
      const result = await this.#service.listRaw({ ...this.#query(1000), page: 1 });
      this.#downloadCsv(result.items);
      this.#toast.success(`Exported ${result.items.length} students`);
    } catch {
      this.#toast.error('Export failed. Please try again.');
    } finally {
      this.exporting.set(false);
    }
  }

  #downloadCsv(items: StudentListItem[]): void {
    const headers = ['Name', 'Phone', 'Grade', 'City', 'Status', 'Parent phone', 'Device', 'Joined'];
    const esc = (v: string) => `"${v.replace(/"/g, '""')}"`;
    const lines = [headers.map(esc).join(',')];
    for (const s of items) {
      lines.push(
        [
          s.fullName,
          s.phoneNumber,
          s.gradeName ?? '',
          s.cityName ?? '',
          s.status,
          s.parentPhonePrimary,
          s.activeDeviceSummary ?? 'Not bound',
          new Date(s.createdAtUtc).toLocaleDateString(),
        ]
          .map((v) => esc(String(v)))
          .join(','),
      );
    }
    const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'students.csv';
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1500);
  }

  // Presentation helpers
  initials = studentInitials;
  pillFor = statusPill;
  dotFor = statusDot;
  subjectFor = avatarSubject;
  joined = relativeTime;
}
