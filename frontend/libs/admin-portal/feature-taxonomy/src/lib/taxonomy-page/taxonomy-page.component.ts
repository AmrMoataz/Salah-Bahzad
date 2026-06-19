import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { AuthStore } from '@sb/shared/data-access';
import {
  AlertComponent,
  ButtonComponent,
  ConfirmDialogComponent,
  EmptyStateComponent,
  ModalComponent,
  SbTab,
  SbTableColumn,
  StatusPillComponent,
  TableCellDirective,
  TableComponent,
  TabsComponent,
} from '@sb/shared/ui';
import {
  Grade,
  Specialization,
  Subject,
  TaxonomyFormValue,
  TaxonomyKind,
} from '../data-access/taxonomy.models';
import { TaxonomyService } from '../data-access/taxonomy.service';
import { TaxonomyEditing, TaxonomyFormComponent } from '../taxonomy-form/taxonomy-form.component';

type TaxonomyTab = 'grades' | 'subjects' | 'specializations' | 'cities';

interface DeleteTarget {
  kind: TaxonomyKind;
  id: string;
  name: string;
}

/**
 * Taxonomy management screen (FR-PLAT-TAX-001/002/004, FR-ADM-TAX-*, mockup `scrTaxonomy`).
 * Tabbed Grades/Subjects/Specializations CRUD plus a read-only Cities & Regions reference tab.
 * Writes are gated on granular permissions (Teacher-only) — the server still enforces them.
 */
@Component({
  selector: 'sb-taxonomy-page',
  standalone: true,
  imports: [
    ButtonComponent,
    AlertComponent,
    EmptyStateComponent,
    StatusPillComponent,
    ConfirmDialogComponent,
    ModalComponent,
    TabsComponent,
    TableComponent,
    TableCellDirective,
    TaxonomyFormComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tax">
      <!-- Header -->
      <header class="tax__head">
        <div>
          <h1 class="tax__title">Taxonomy</h1>
          <p class="tax__subtitle">Reference data driving sessions &amp; student profiles</p>
        </div>
        @if (canCreate() && activeTab() !== 'cities') {
          <sb-button variant="primary" (clicked)="openCreate()">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M12 5v14M5 12h14"/>
            </svg>
            Add {{ kindLabel() }}
          </sb-button>
        }
      </header>

      @if (!canCreate()) {
        <sb-alert variant="info" title="Read-only">
          Editing taxonomy is restricted to Teachers. You can browse all reference data here.
        </sb-alert>
      }

      @if (error()) {
        <sb-alert variant="danger" title="Something went wrong">{{ error() }}</sb-alert>
      }

      <sb-tabs [tabs]="tabs()" [active]="activeTab()" (tabChange)="onTabChange($event)" />

      <!-- Tab content -->
      @switch (activeTab()) {
        @case ('grades') {
          @if (grades().length === 0 && !isLoading()) {
            <sb-empty-state
              image="/assets/salah-mascot.png"
              headline="No grades yet"
              description="Add your first grade level to start classifying sessions and students."
            >
              @if (canCreate()) { <sb-button variant="primary" (clicked)="openCreate()">Add grade</sb-button> }
            </sb-empty-state>
          } @else {
            <sb-table [columns]="gradeColumns" [rows]="grades()" [rowKey]="byId">
              <ng-template sbTableCell="name" let-row><strong>{{ row.name }}</strong></ng-template>
              <ng-template sbTableCell="actions" let-row>
                <div class="tax__actions">
                  @if (canEdit()) {
                    <sb-button variant="secondary" size="sm" (clicked)="openEdit('grade', row)">Edit</sb-button>
                  }
                  @if (canDelete()) {
                    <button type="button" class="tax__icon-btn" [attr.aria-label]="'Delete ' + row.name"
                            (click)="askDeleteGrade(row)">
                      <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                           stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                        <path d="M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2m3 0v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6"/>
                      </svg>
                    </button>
                  }
                </div>
              </ng-template>
            </sb-table>
          }
        }

        @case ('subjects') {
          @if (subjects().length === 0 && !isLoading()) {
            <sb-empty-state
              image="/assets/salah-mascot.png"
              headline="No subjects yet"
              description="Add a subject (e.g. Math), then give it specializations."
            >
              @if (canCreate()) { <sb-button variant="primary" (clicked)="openCreate()">Add subject</sb-button> }
            </sb-empty-state>
          } @else {
            <sb-table [columns]="subjectColumns" [rows]="subjects()" [rowKey]="byId">
              <ng-template sbTableCell="name" let-row><strong>{{ row.name }}</strong></ng-template>
              <ng-template sbTableCell="specializationCount" let-row>{{ row.specializationCount }}</ng-template>
              <ng-template sbTableCell="actions" let-row>
                <div class="tax__actions">
                  @if (canEdit()) {
                    <sb-button variant="secondary" size="sm" (clicked)="openEdit('subject', row)">Edit</sb-button>
                  }
                  @if (canDelete()) {
                    <button type="button" class="tax__icon-btn" [attr.aria-label]="'Delete ' + row.name"
                            (click)="askDeleteSubject(row)">
                      <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                           stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                        <path d="M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2m3 0v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6"/>
                      </svg>
                    </button>
                  }
                </div>
              </ng-template>
            </sb-table>
          }
        }

        @case ('specializations') {
          @if (subjects().length === 0 && !isLoading()) {
            <sb-alert variant="info" title="Add a subject first">
              Specializations belong to a subject. Create a subject before adding specializations.
            </sb-alert>
          } @else if (specializations().length === 0 && !isLoading()) {
            <sb-empty-state
              image="/assets/salah-mascot.png"
              headline="No specializations yet"
              description="Add a specialization (e.g. Mechanics) under one of your subjects."
            >
              @if (canCreate()) {
                <sb-button variant="primary" (clicked)="openCreate()">Add specialization</sb-button>
              }
            </sb-empty-state>
          } @else {
            <sb-table [columns]="specColumns" [rows]="specializations()" [rowKey]="byId">
              <ng-template sbTableCell="name" let-row><strong>{{ row.name }}</strong></ng-template>
              <ng-template sbTableCell="subjectName" let-row>
                <sb-status-pill variant="info">{{ row.subjectName }}</sb-status-pill>
              </ng-template>
              <ng-template sbTableCell="actions" let-row>
                <div class="tax__actions">
                  @if (canEdit()) {
                    <sb-button variant="secondary" size="sm" (clicked)="openEdit('specialization', row)">Edit</sb-button>
                  }
                  @if (canDelete()) {
                    <button type="button" class="tax__icon-btn" [attr.aria-label]="'Delete ' + row.name"
                            (click)="askDeleteSpecialization(row)">
                      <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                           stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                        <path d="M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2m3 0v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6"/>
                      </svg>
                    </button>
                  }
                </div>
              </ng-template>
            </sb-table>
          }
        }

        @case ('cities') {
          <div class="tax__cities">
            <sb-alert variant="info" title="Seeded reference data">
              Cities and Regions are a seeded, Egypt-wide dataset and are not staff-editable. They are
              shown here for reference and are available to the public sign-up form.
            </sb-alert>
            @if (cities().length > 0) {
              <div class="tax__pills">
                @for (city of cities(); track city.id) {
                  <span class="tax__pill">{{ city.nameEn }}</span>
                }
              </div>
            } @else if (!isLoading()) {
              <p class="tax__muted">No cities loaded.</p>
            }
          </div>
        }
      }
    </div>

    <!-- Create / edit modal -->
    <sb-taxonomy-form
      [open]="formOpen()"
      [kind]="formKind()"
      [editing]="editingSeed()"
      [subjects]="subjects()"
      [submitting]="saving()"
      [error]="formError()"
      (save)="onSave($event)"
      (cancel)="closeForm()"
    />

    <!-- Delete confirmation -->
    @if (deleteTarget(); as t) {
      <sb-confirm-dialog
        [open]="true"
        [title]="'Delete ' + t.name + '?'"
        [message]="'This ' + t.kind + ' will be removed. Historical references are preserved.'"
        confirmLabel="Delete"
        confirmVariant="danger"
        [busy]="confirmBusy()"
        (confirm)="onConfirmDelete()"
        (cancel)="deleteTarget.set(null)"
      />
    }

    <!-- Delete-in-use block (FR-PLAT-TAX-004) -->
    <sb-modal
      [open]="blockedMessage() !== null"
      title="Cannot delete — in use"
      size="confirm"
      (close)="blockedMessage.set(null)"
    >
      <sb-alert variant="warning" title="In use">{{ blockedMessage() }}</sb-alert>
      <div modalFooter class="tax__actions">
        <sb-button variant="primary" (clicked)="blockedMessage.set(null)">Got it</sb-button>
      </div>
    </sb-modal>
  `,
  styles: [`
    .tax { display: flex; flex-direction: column; gap: var(--sb-space-4); }

    .tax__head {
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      gap: var(--sb-space-4);
      flex-wrap: wrap;
    }
    .tax__title {
      margin: 0 0 var(--sb-space-1);
      font-size: var(--sb-heading-xl-size);
      font-weight: 800;
      letter-spacing: -0.01em;
      color: var(--sb-text);
    }
    .tax__subtitle { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    .tax__actions { display: inline-flex; gap: var(--sb-space-2); justify-content: flex-end; align-items: center; }

    .tax__icon-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 34px;
      height: 34px;
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-md);
      background: var(--sb-surface);
      color: var(--sb-danger);
      cursor: pointer;
      transition: background var(--sb-timing) var(--sb-easing-standard);
    }
    .tax__icon-btn:hover { background: var(--sb-primary-50); }
    .tax__icon-btn:focus-visible { box-shadow: var(--sb-shadow-focus); outline: none; }

    .tax__cities { display: flex; flex-direction: column; gap: var(--sb-space-4); }
    .tax__pills { display: flex; flex-wrap: wrap; gap: var(--sb-space-2); }
    .tax__pill {
      padding: 7px 14px;
      border-radius: var(--sb-radius-pill);
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      font-size: var(--sb-body-sm-size);
      font-weight: 600;
      color: var(--sb-text);
    }
    .tax__muted { color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
  `],
})
export class TaxonomyPageComponent implements OnInit {
  readonly #service = inject(TaxonomyService);
  readonly #auth = inject(AuthStore);

  readonly grades = this.#service.grades;
  readonly subjects = this.#service.subjects;
  readonly specializations = this.#service.specializations;
  readonly cities = this.#service.cities;
  readonly isLoading = this.#service.isLoading;
  readonly error = this.#service.error;

  readonly canCreate = computed(() => this.#auth.hasPermission('TaxonomyCreate'));
  readonly canEdit = computed(() => this.#auth.hasPermission('TaxonomyEdit'));
  readonly canDelete = computed(() => this.#auth.hasPermission('TaxonomyDelete'));

  readonly activeTab = signal<TaxonomyTab>('grades');

  readonly tabs = computed<SbTab[]>(() => [
    { id: 'grades', label: 'Grades', badge: this.grades().length },
    { id: 'subjects', label: 'Subjects', badge: this.subjects().length },
    { id: 'specializations', label: 'Specializations', badge: this.specializations().length },
    { id: 'cities', label: 'Cities & Regions' },
  ]);

  readonly kindLabel = computed<TaxonomyKind>(() => this.formKind());
  readonly formKind = computed<TaxonomyKind>(() => {
    switch (this.activeTab()) {
      case 'subjects':
        return 'subject';
      case 'specializations':
        return 'specialization';
      default:
        return 'grade';
    }
  });

  readonly gradeColumns: readonly SbTableColumn[] = [
    { key: 'name', header: 'Grade' },
    { key: 'actions', header: '', align: 'right', width: '1%' },
  ];
  readonly subjectColumns: readonly SbTableColumn[] = [
    { key: 'name', header: 'Subject' },
    { key: 'specializationCount', header: 'Specializations', align: 'right' },
    { key: 'actions', header: '', align: 'right', width: '1%' },
  ];
  readonly specColumns: readonly SbTableColumn[] = [
    { key: 'name', header: 'Specialization' },
    { key: 'subjectName', header: 'Subject' },
    { key: 'actions', header: '', align: 'right', width: '1%' },
  ];

  readonly byId = (row: Grade | Subject | Specialization): string => row.id;

  // Form state
  readonly formOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly editingSeed = signal<TaxonomyEditing | null>(null);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);

  // Delete state
  readonly deleteTarget = signal<DeleteTarget | null>(null);
  readonly confirmBusy = signal(false);
  readonly blockedMessage = signal<string | null>(null);

  ngOnInit(): void {
    // Load the editable taxonomy up front so tab badges are accurate; cities load lazily.
    void this.#service.loadGrades();
    void this.#service.loadSubjects();
    void this.#service.loadSpecializations();
  }

  onTabChange(id: string): void {
    this.activeTab.set(id as TaxonomyTab);
    if (id === 'cities' && this.cities().length === 0) void this.#service.loadCities();
  }

  // ── Create / edit ──────────────────────────────────────────────────────────
  openCreate(): void {
    this.editingId.set(null);
    this.editingSeed.set(null);
    this.formError.set(null);
    this.formOpen.set(true);
  }

  openEdit(kind: TaxonomyKind, row: Grade | Subject | Specialization): void {
    this.editingId.set(row.id);
    this.editingSeed.set({
      name: row.name,
      subjectId: kind === 'specialization' ? (row as Specialization).subjectId : undefined,
    });
    this.formError.set(null);
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  async onSave(value: TaxonomyFormValue): Promise<void> {
    this.saving.set(true);
    this.formError.set(null);
    const id = this.editingId();
    try {
      switch (this.formKind()) {
        case 'grade':
          await (id ? this.#service.updateGrade(id, value.name) : this.#service.createGrade(value.name));
          break;
        case 'subject':
          await (id ? this.#service.updateSubject(id, value.name) : this.#service.createSubject(value.name));
          break;
        case 'specialization':
          await (id
            ? this.#service.updateSpecialization(id, value.subjectId!, value.name)
            : this.#service.createSpecialization(value.subjectId!, value.name));
          break;
      }
      this.formOpen.set(false);
    } catch {
      this.formError.set(this.error() ?? 'Could not save. Please try again.');
    } finally {
      this.saving.set(false);
    }
  }

  // ── Delete ───────────────────────────────────────────────────────────────────
  askDeleteGrade(row: Grade): void {
    this.deleteTarget.set({ kind: 'grade', id: row.id, name: row.name });
  }

  askDeleteSubject(row: Subject): void {
    // Delete-in-use guard (FR-PLAT-TAX-004): a subject with live specializations can't be deleted.
    if (row.specializationCount > 0) {
      this.blockedMessage.set(
        `“${row.name}” has ${row.specializationCount} specialization(s). Remove them first, or archive the subject instead.`,
      );
      return;
    }
    this.deleteTarget.set({ kind: 'subject', id: row.id, name: row.name });
  }

  askDeleteSpecialization(row: Specialization): void {
    this.deleteTarget.set({ kind: 'specialization', id: row.id, name: row.name });
  }

  async onConfirmDelete(): Promise<void> {
    const target = this.deleteTarget();
    if (!target) return;
    this.confirmBusy.set(true);
    try {
      switch (target.kind) {
        case 'grade':
          await this.#service.deleteGrade(target.id);
          break;
        case 'subject':
          await this.#service.deleteSubject(target.id);
          break;
        case 'specialization':
          await this.#service.deleteSpecialization(target.id);
          break;
      }
      this.deleteTarget.set(null);
    } catch {
      // Server-side delete-in-use backstop (409) or any failure → show the reason, close the confirm.
      this.deleteTarget.set(null);
      this.blockedMessage.set(this.error() ?? 'Could not delete this item.');
    } finally {
      this.confirmBusy.set(false);
    }
  }
}
