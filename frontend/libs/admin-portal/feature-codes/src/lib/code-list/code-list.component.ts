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
  AlertComponent,
  ButtonComponent,
  ComboboxComponent,
  ConfirmDialogComponent,
  PaginationComponent,
  SbTableColumn,
  SelectComponent,
  SelectOption,
  StatusPillComponent,
  TableCellDirective,
  TableComponent,
  ToastService,
} from '@sb/shared/ui';
import { CodeListDto, CodeStatus } from '../data-access/code.models';
import { CodeService } from '../data-access/code.service';
import { codeStatusLabel, codeStatusPill, dateOnly, dateTime, egp } from '../code.presentation';

/**
 * Codes register (FR-ADM-COD-002..005, mockup `scrCodes`). The enrollment-code register: a filter bar
 * (search serial|student + status + session), a paged table, and Teacher-only controls — a select
 * column (disabled for `Used` codes) driving a bulk Disable / Export-selection bar, per-row
 * Enable↔Disable + Delete, and the header Generate action. Assistants see a read-only alert; the
 * server still enforces every `Codes*` permission (default-deny). Export (#3) streams from the server;
 * "Export selection" builds the CSV client-side from the loaded rows.
 */
@Component({
  selector: 'sb-code-list',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    AlertComponent,
    ButtonComponent,
    ComboboxComponent,
    SelectComponent,
    StatusPillComponent,
    TableComponent,
    TableCellDirective,
    PaginationComponent,
    ConfirmDialogComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!canRead()) {
      <div class="cd__gate">
        <span class="cd__gate-icon" aria-hidden="true">
          <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
            <path d="M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2zM7 11V7a5 5 0 0 1 10 0v4"/>
          </svg>
        </span>
        <h3 class="cd__gate-title">Access required</h3>
        <p class="cd__gate-text">You don’t have permission to view the codes register.</p>
      </div>
    } @else {
      <div class="cd__head">
        <div>
          <h1 class="cd__title">Codes</h1>
          <p class="cd__subtitle">{{ total() }} codes · {{ activeOnPage() }} active · enrollment register</p>
        </div>
        <div class="cd__head-actions">
          <sb-button variant="secondary" [loading]="exporting()" (clicked)="exportAll()">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"/>
            </svg>
            Export
          </sb-button>
          @if (canGenerate()) {
            <sb-button variant="primary" (clicked)="generate()">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                   stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M12 5v14M5 12h14"/>
              </svg>
              Generate codes
            </sb-button>
          }
        </div>
      </div>

      @if (!canGenerate()) {
        <sb-alert variant="info" title="Read-only">
          Assistants can view the register. Generating, disabling and deleting codes is restricted to Teachers.
        </sb-alert>
      }

      @if (canDisable() && selectedCount() > 0) {
        <div class="cd__bulk">
          <span class="cd__bulk-count">{{ selectedCount() }} selected</span>
          <sb-button variant="secondary" size="sm" [loading]="bulkBusy()" (clicked)="bulkDisable()">Disable</sb-button>
          <sb-button variant="secondary" size="sm" (clicked)="exportSelection()">Export selection</sb-button>
          <button type="button" class="cd__bulk-clear" (click)="clearSelection()">Clear</button>
        </div>
      }

      <!-- Filter bar -->
      <div class="cd__filterbar" [formGroup]="filters">
        <div class="cd__search">
          <span class="cd__search-icon" aria-hidden="true">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 21l-4.35-4.35M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16z"/>
            </svg>
          </span>
          <input
            type="search"
            class="cd__search-input"
            formControlName="search"
            placeholder="Search serial or student…"
            autocomplete="off"
            aria-label="Search codes"
          />
        </div>
        <sb-select class="cd__filter" formControlName="status" [options]="statusOptions" placeholder="All statuses" />
        <sb-combobox class="cd__filter cd__filter--wide" formControlName="sessionId" [options]="sessionOptions()" placeholder="All sessions" emptyText="No sessions" />
      </div>

      @if (rows().length === 0 && !isLoading()) {
        <div class="cd__empty">
          {{ hasFilters() ? 'No codes match these filters.' : 'No codes yet. Generate a batch to start the register.' }}
        </div>
      } @else {
        <sb-table [columns]="columns()" [rows]="rows()" [rowKey]="byId">
          @if (canDisable()) {
            <ng-template sbTableCell="select" let-row>
              <input
                type="checkbox"
                class="cd__check"
                [checked]="isSelected(row)"
                [disabled]="row.status === 'Used'"
                (change)="toggle(row)"
                [attr.aria-label]="'Select ' + row.serial"
              />
            </ng-template>
          }

          <ng-template sbTableCell="serial" let-row><span class="cd__serial">{{ row.serial }}</span></ng-template>
          <ng-template sbTableCell="value" let-row>{{ value(row.value) }}</ng-template>

          <ng-template sbTableCell="status" let-row>
            <sb-status-pill [variant]="pillFor(row.status)">{{ statusLabel(row.status) }}</sb-status-pill>
          </ng-template>

          <ng-template sbTableCell="batch" let-row>{{ row.batchLabel }}</ng-template>
          <ng-template sbTableCell="session" let-row><span class="cd__session">{{ row.sessionTitle }}</span></ng-template>

          <ng-template sbTableCell="redeemedBy" let-row>
            @if (row.redeemedByStudentName) {
              <div>
                <div class="cd__strong">{{ row.redeemedByStudentName }}</div>
                <div class="cd__muted-sm">{{ at(row.redeemedAtUtc) }}</div>
              </div>
            } @else {
              <span class="cd__muted">—</span>
            }
          </ng-template>

          <ng-template sbTableCell="created" let-row>{{ day(row.createdAtUtc) }}</ng-template>

          @if (canDisable() || canDelete()) {
            <ng-template sbTableCell="actions" let-row>
              @if (row.status !== 'Used') {
                <div class="cd__actions">
                  @if (canDisable()) {
                    <sb-button variant="ghost" size="sm" (clicked)="toggleStatus(row)">
                      {{ row.status === 'Inactive' ? 'Enable' : 'Disable' }}
                    </sb-button>
                  }
                  @if (canDelete()) {
                    <button type="button" class="cd__icon-danger" [attr.aria-label]="'Delete ' + row.serial" (click)="askDelete(row)">
                      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                           stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                        <path d="M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2m3 0v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6"/>
                      </svg>
                    </button>
                  }
                </div>
              }
            </ng-template>
          }
        </sb-table>

        @if (total() > 0) {
          <div class="cd__pager">
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

      <sb-confirm-dialog
        [open]="deleteOpen()"
        [title]="pendingDelete() ? 'Delete code ' + pendingDelete()!.serial + '?' : 'Delete code?'"
        message="The code will be soft-deleted and can no longer be redeemed. This action is audited."
        confirmLabel="Delete code"
        confirmVariant="danger"
        [busy]="actionBusy()"
        (confirm)="confirmDelete()"
        (cancel)="deleteOpen.set(false)"
      />
    }
  `,
  styles: [`
    :host { display: flex; flex-direction: column; gap: var(--sb-space-4); }

    .cd__head { display: flex; align-items: flex-end; justify-content: space-between; gap: var(--sb-space-4); flex-wrap: wrap; }
    .cd__title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-xl-size); font-weight: 800; letter-spacing: -0.01em; color: var(--sb-text); }
    .cd__subtitle { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
    .cd__head-actions { display: flex; gap: var(--sb-space-2); flex-wrap: wrap; }

    sb-alert { display: block; }

    .cd__bulk {
      display: flex; align-items: center; gap: var(--sb-space-3);
      padding: var(--sb-space-2) var(--sb-space-4);
      background: var(--sb-primary-50); border: 1px solid var(--sb-primary-200);
      border-radius: var(--sb-radius-md);
    }
    .cd__bulk-count { font-weight: 700; font-size: var(--sb-body-sm-size); color: var(--sb-primary-800); }
    .cd__bulk-clear {
      margin-left: auto; border: none; background: none; cursor: pointer;
      color: var(--sb-text-muted); font-family: var(--sb-font-sans); font-weight: 700; font-size: var(--sb-body-sm-size);
    }
    .cd__bulk-clear:hover { color: var(--sb-text); }

    .cd__filterbar {
      display: flex; gap: var(--sb-space-3); flex-wrap: wrap; align-items: center;
      background: var(--sb-surface); border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-lg); padding: var(--sb-space-3);
    }
    .cd__search {
      flex: 1 1 220px; display: flex; align-items: center; gap: var(--sb-space-2);
      height: 40px; padding: 0 var(--sb-space-3);
      background: var(--sb-surface-sunken); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-md);
    }
    .cd__search-icon { display: inline-flex; color: var(--sb-text-subtle); flex-shrink: 0; }
    .cd__search-input { flex: 1; min-width: 0; border: none; outline: none; background: transparent; font-family: var(--sb-font-sans); font-size: var(--sb-body-md-size); color: var(--sb-text); }
    .cd__filter { width: 160px; }
    .cd__filter--wide { width: 220px; }

    .cd__check { width: 16px; height: 16px; accent-color: var(--sb-primary); cursor: pointer; }
    .cd__check:disabled { cursor: not-allowed; opacity: 0.5; }

    .cd__serial { font-family: var(--sb-font-mono); font-weight: 700; font-size: var(--sb-body-sm-size); }
    .cd__session { font-size: var(--sb-body-sm-size); }
    .cd__strong { font-weight: 600; }
    .cd__muted { color: var(--sb-text-subtle); }
    .cd__muted-sm { font-size: var(--sb-body-sm-size); color: var(--sb-text-subtle); }

    .cd__actions { display: inline-flex; gap: var(--sb-space-2); justify-content: flex-end; align-items: center; }
    .cd__icon-danger {
      width: 32px; height: 32px; flex-shrink: 0; border: 1px solid var(--sb-border); background: var(--sb-surface);
      border-radius: var(--sb-radius-md); cursor: pointer; display: inline-flex; align-items: center; justify-content: center;
      color: var(--sb-danger); transition: background var(--sb-timing-fast) var(--sb-easing-standard), border-color var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .cd__icon-danger:hover { background: var(--sb-danger-bg); border-color: var(--sb-danger-border); }

    .cd__pager { margin-top: var(--sb-space-2); }

    .cd__empty {
      background: var(--sb-surface); border: 1px dashed var(--sb-border-strong);
      border-radius: var(--sb-radius-lg); padding: var(--sb-space-12); text-align: center;
      color: var(--sb-text-muted); font-size: var(--sb-body-md-size);
    }

    .cd__gate { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-lg); padding: var(--sb-space-10); text-align: center; }
    .cd__gate-icon { display: inline-flex; align-items: center; justify-content: center; width: 56px; height: 56px; margin: 0 auto var(--sb-space-3); border-radius: var(--sb-radius-circle); background: var(--sb-warning-bg); color: var(--sb-warning-fg); }
    .cd__gate-title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-sm-size); font-weight: 700; color: var(--sb-text); }
    .cd__gate-text { margin: 0 auto; max-width: 380px; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
  `],
})
export class CodeListComponent implements OnInit {
  readonly #service = inject(CodeService);
  readonly #auth = inject(AuthStore);
  readonly #router = inject(Router);
  readonly #fb = inject(FormBuilder);
  readonly #toast = inject(ToastService);

  readonly rows = this.#service.codes;
  readonly total = this.#service.total;
  readonly isLoading = this.#service.isLoading;

  readonly canRead = computed(() => this.#auth.hasPermission('CodesRead'));
  readonly canGenerate = computed(() => this.#auth.hasPermission('CodesGenerate'));
  readonly canDisable = computed(() => this.#auth.hasPermission('CodesDisable'));
  readonly canDelete = computed(() => this.#auth.hasPermission('CodesDelete'));

  /** Active-code count derived from the loaded page (the list endpoint exposes no global aggregate). */
  readonly activeOnPage = computed(() => this.rows().filter((c) => c.status === 'Active').length);

  readonly pageSize = 10;
  readonly page = signal(1);
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));

  readonly #selected = signal<ReadonlySet<string>>(new Set());
  readonly selectedCount = computed(() => this.#selected().size);

  readonly exporting = signal(false);
  readonly bulkBusy = signal(false);
  readonly deleteOpen = signal(false);
  readonly pendingDelete = signal<CodeListDto | null>(null);
  readonly actionBusy = signal(false);

  readonly filters = this.#fb.group({
    search: [''],
    status: [''],
    sessionId: [''],
  });

  readonly statusOptions: SelectOption[] = [
    { value: '', label: 'All statuses' },
    { value: 'Active', label: 'Active' },
    { value: 'Used', label: 'Used' },
    { value: 'Inactive', label: 'Disabled' },
  ];

  readonly sessionOptions = computed<SelectOption[]>(() => [
    { value: '', label: 'All sessions' },
    ...this.#service.sessions().map((s) => ({ value: s.id, label: s.title })),
  ]);

  /** Columns are permission-shaped: the select + actions columns appear only for Teachers. */
  readonly columns = computed<readonly SbTableColumn[]>(() => {
    const cols: SbTableColumn[] = [];
    if (this.canDisable()) cols.push({ key: 'select', header: '', width: '1%' });
    cols.push(
      { key: 'serial', header: 'Serial' },
      { key: 'value', header: 'Value', align: 'right' },
      { key: 'status', header: 'Status' },
      { key: 'batch', header: 'Batch' },
      { key: 'session', header: 'Session' },
      { key: 'redeemedBy', header: 'Redeemed by' },
      { key: 'created', header: 'Created' },
    );
    if (this.canDisable() || this.canDelete()) {
      cols.push({ key: 'actions', header: '', align: 'right', width: '1%' });
    }
    return cols;
  });

  readonly byId = (row: CodeListDto): string => row.id;

  constructor() {
    this.filters.valueChanges.pipe(debounceTime(250), takeUntilDestroyed()).subscribe(() => {
      this.page.set(1);
      this.clearSelection();
      void this.reload();
    });
  }

  ngOnInit(): void {
    if (!this.canRead()) return;
    void this.#service.loadSessions();
    void this.reload();
  }

  hasFilters(): boolean {
    const f = this.filters.getRawValue();
    return !!(f.search?.trim() || f.status || f.sessionId);
  }

  #query(pageSize = this.pageSize) {
    const f = this.filters.getRawValue();
    return {
      search: f.search?.trim() || undefined,
      status: (f.status || null) as CodeStatus | null,
      sessionId: f.sessionId || null,
      pageSize,
    };
  }

  async reload(): Promise<void> {
    try {
      await this.#service.list({ ...this.#query(), page: this.page() });
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not load the codes register.');
    }
  }

  onPageChange(page: number): void {
    this.page.set(page);
    void this.reload();
  }

  // ── Selection ──────────────────────────────────────────────────────────────────
  isSelected(row: CodeListDto): boolean {
    return this.#selected().has(row.id);
  }

  toggle(row: CodeListDto): void {
    if (row.status === 'Used') return;
    const next = new Set(this.#selected());
    if (next.has(row.id)) next.delete(row.id);
    else next.add(row.id);
    this.#selected.set(next);
  }

  clearSelection(): void {
    if (this.#selected().size > 0) this.#selected.set(new Set());
  }

  #selectedRows(): CodeListDto[] {
    const ids = this.#selected();
    return this.rows().filter((c) => ids.has(c.id));
  }

  // ── Mutations (#5 / #6 / #7) ─────────────────────────────────────────────────────
  async toggleStatus(row: CodeListDto): Promise<void> {
    const willEnable = row.status === 'Inactive';
    try {
      if (willEnable) await this.#service.enable(row.id);
      else await this.#service.disable(row.id);
      this.#toast.info(`Code ${row.serial} ${willEnable ? 'enabled' : 'disabled'}`);
      await this.reload();
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not update the code.');
    }
  }

  async bulkDisable(): Promise<void> {
    const targets = this.#selectedRows().filter((c) => c.status !== 'Used');
    if (targets.length === 0) return;
    this.bulkBusy.set(true);
    let failed = 0;
    for (const code of targets) {
      try {
        await this.#service.disable(code.id);
      } catch {
        failed++;
      }
    }
    this.bulkBusy.set(false);
    this.clearSelection();
    if (failed === 0) this.#toast.info(`${targets.length} codes disabled`);
    else this.#toast.error(`${failed} of ${targets.length} codes could not be disabled`);
    await this.reload();
  }

  askDelete(row: CodeListDto): void {
    this.pendingDelete.set(row);
    this.deleteOpen.set(true);
  }

  async confirmDelete(): Promise<void> {
    const code = this.pendingDelete();
    if (!code) return;
    this.actionBusy.set(true);
    try {
      await this.#service.remove(code.id);
      this.deleteOpen.set(false);
      this.#toast.info('Code deleted');
      await this.reload();
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not delete the code.');
    } finally {
      this.actionBusy.set(false);
    }
  }

  // ── Exports ──────────────────────────────────────────────────────────────────────
  async exportAll(): Promise<void> {
    this.exporting.set(true);
    try {
      await this.#service.export(this.#query());
    } catch {
      this.#toast.error('Could not export the register.');
    } finally {
      this.exporting.set(false);
    }
  }

  exportSelection(): void {
    const rows = this.#selectedRows();
    if (rows.length === 0) return;
    this.#service.exportRows(rows);
    this.#toast.success(`Exported ${rows.length} codes`);
  }

  // ── Navigation ─────────────────────────────────────────────────────────────────
  generate(): void {
    void this.#router.navigate(['/codes/generate']);
  }

  // Presentation helpers
  pillFor = codeStatusPill;
  statusLabel = codeStatusLabel;
  value = egp;
  at = dateTime;
  day = dateOnly;
}
