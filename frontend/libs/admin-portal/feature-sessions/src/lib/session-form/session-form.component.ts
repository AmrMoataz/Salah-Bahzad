import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';
import {
  AlertComponent,
  ButtonComponent,
  CardComponent,
  ComboboxComponent,
  FileUploadComponent,
  FormFieldComponent,
  SelectComponent,
  SelectOption,
  StatusPillComponent,
  ToastService,
  UploadedFile,
} from '@sb/shared/ui';
import {
  SessionDetailDto,
  SessionVideoDto,
} from '../data-access/session.models';
import { SessionService } from '../data-access/session.service';
import { fileSize, statusPill, videoStatusPill } from '../session.presentation';
import { AddVideoPayload, EditVideoPayload, VideoDialogComponent } from './video-dialog.component';

/**
 * Session create/edit (FR-ADM-SES-002..006, mockup `scrSessionEdit`). One screen for both modes:
 * Details (title, description, price, validity, grade/subject/specialization, thumbnail) on the left
 * with the Videos and Materials panels, and Publish / Gating / Question-bank cards on the right.
 * In **create** mode only the details are captured (POST) — videos, materials, prerequisite and
 * publish unlock once the session exists, so on save we land in edit mode. In **edit** mode media and
 * gating mutations hit their granular endpoints immediately (with upload progress); "Save session"
 * persists the detail fields.
 */
@Component({
  selector: 'sb-session-form',
  standalone: true,
  // RouterLink intentionally omitted — navigation is programmatic via Router.
  imports: [
    ReactiveFormsModule,
    CardComponent,
    ButtonComponent,
    SelectComponent,
    ComboboxComponent,
    StatusPillComponent,
    FormFieldComponent,
    FileUploadComponent,
    AlertComponent,
    VideoDialogComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button type="button" class="sf__back" (click)="cancel()">
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
           stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <path d="M19 12H5M12 19l-7-7 7-7"/>
      </svg>
      Back to sessions
    </button>

    @if (loadError()) {
      <sb-alert variant="danger" title="Couldn’t load session">{{ loadError() }}</sb-alert>
    } @else {
      <div class="sf__head">
        <div>
          <h1 class="sf__title">{{ isNew() ? 'Create session' : 'Edit · ' + (session()?.title ?? '') }}</h1>
          <p class="sf__subtitle">Author content, media, gating and publish state</p>
        </div>
        <div class="sf__head-actions">
          <sb-button variant="ghost" (clicked)="cancel()">Cancel</sb-button>
          <sb-button variant="primary" [loading]="saving()" (clicked)="save()">
            {{ isNew() ? 'Create session' : 'Save session' }}
          </sb-button>
        </div>
      </div>

      <div class="sf__grid">
        <!-- LEFT -->
        <div class="sf__col">
          <sb-card title="Details">
            <form [formGroup]="form" class="sf__form" novalidate>
              <sb-form-field label="Title" fieldId="sf-title" [error]="err('title')" [required]="true">
                <input id="sf-title" type="text" class="sb-input" formControlName="title"
                       placeholder="e.g. Kinematics — Motion in 1D" autocomplete="off" />
              </sb-form-field>

              <sb-form-field label="Description" fieldId="sf-desc" [error]="err('description')">
                <textarea id="sf-desc" class="sb-textarea" rows="3" formControlName="description"
                          placeholder="What this session covers…"></textarea>
              </sb-form-field>

              <div class="sf__row sf__row--3">
                <sb-form-field label="Grade" fieldId="sf-grade" [error]="err('gradeId')" [required]="true">
                  <sb-select inputId="sf-grade" formControlName="gradeId" [options]="gradeOptions()"
                             [invalid]="!!err('gradeId')" placeholder="Select grade" />
                </sb-form-field>
                <sb-form-field label="Subject" fieldId="sf-subject" [required]="true">
                  <sb-select inputId="sf-subject" formControlName="subjectId" [options]="subjectOptions()"
                             placeholder="Select subject" />
                </sb-form-field>
                <sb-form-field label="Specialization" fieldId="sf-spec" [error]="err('specializationId')" [required]="true">
                  <sb-select inputId="sf-spec" formControlName="specializationId" [options]="specializationOptions()"
                             [invalid]="!!err('specializationId')" placeholder="Select specialization" />
                </sb-form-field>
              </div>

              <div class="sf__row sf__row--2">
                <sb-form-field label="Price (EGP)" fieldId="sf-price" [error]="err('price')">
                  <input id="sf-price" type="number" min="0" class="sb-input" formControlName="price" />
                </sb-form-field>
                <sb-form-field label="Validity (days)" fieldId="sf-validity" hint="0–365 days from enrollment" [error]="err('validityDays')">
                  <input id="sf-validity" type="number" min="0" max="365" class="sb-input" formControlName="validityDays" />
                </sb-form-field>
              </div>

              <sb-file-upload
                label="Thumbnail"
                accept="image/jpeg,image/png,image/webp"
                hint="PNG, JPG or WebP · max 5 MB"
                [files]="thumbFiles()"
                [disabled]="thumbBusy()"
                (filesPicked)="onThumbPicked($event)"
                (remove)="clearThumb()"
              />
            </form>
          </sb-card>

          <!-- Videos -->
          <sb-card title="Videos" [padding]="false">
            @if (canEditContent()) {
              <sb-button cardActions variant="secondary" size="sm" (clicked)="openAddVideo()">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                     stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 5v14M5 12h14"/></svg>
                Add video
              </sb-button>
            }
            @if (!session()) {
              <p class="sf__panel-hint">Save the session first to add videos.</p>
            } @else if (videos().length === 0) {
              <p class="sf__panel-hint">No videos yet. Add one through the secure pipeline.</p>
            } @else {
              <ul class="sf__list">
                @for (v of videos(); track v.id; let i = $index) {
                  <li
                    class="sf__item"
                    [class.sf__item--over]="dragOver() === i"
                    [class.sf__item--dragging]="dragFrom() === i"
                    draggable="true"
                    (dragstart)="onDragStart(i, $event)"
                    (dragover)="onDragOver(i, $event)"
                    (drop)="onDrop(i, $event)"
                    (dragend)="onDragEnd()"
                  >
                    <span class="sf__grip" title="Drag to reorder" (mousedown)="grabAt(i)" (mouseup)="releaseGrip()">⠿</span>
                    <span class="sf__vicon" aria-hidden="true">
                      <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                           stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M23 7l-7 5 7 5V7zM14 5H3a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2z"/></svg>
                    </span>
                    <span class="sf__vbody">
                      <span class="sf__vtitle">{{ i + 1 }}. {{ v.title }}</span>
                      <span class="sf__vsub">
                        <span class="sf__vmeta">{{ v.lengthMinutes }} min · secure HLS</span>
                        <sb-status-pill [variant]="vPill(v.processingStatus)">{{ v.processingStatus }}</sb-status-pill>
                      </span>
                    </span>
                    <span class="sf__vaccess">Access <strong>{{ v.accessCount }}×</strong></span>
                    <span class="sf__vactions">
                      <button type="button" class="sf__iconbtn" title="Edit video" (click)="openEditVideo(v)" aria-label="Edit video">
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7M18.5 2.5a2.12 2.12 0 0 1 3 3L12 15l-4 1 1-4z"/></svg>
                      </button>
                      <button type="button" class="sf__iconbtn sf__iconbtn--danger" title="Remove" (click)="removeVideo(v)" aria-label="Remove video">
                        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2m3 0v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6"/></svg>
                      </button>
                    </span>
                  </li>
                }
              </ul>
            }
          </sb-card>

          <!-- Materials -->
          <sb-card title="Materials" [padding]="false">
            @if (canEditContent()) {
              <sb-file-upload
                cardActions
                variant="button"
                accept="application/pdf,text/csv,image/png,image/jpeg"
                [disabled]="materialBusy()"
                (filesPicked)="onMaterialPicked($event)"
              >
                <span fuLabel style="display: inline-flex; align-items: center; gap: 6px;">
                  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                       stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 5v14M5 12h14"/></svg>
                  Add
                </span>
              </sb-file-upload>
            }
            @if (!session()) {
              <p class="sf__panel-hint">Save the session first to add materials.</p>
            } @else if (materials().length === 0) {
              <p class="sf__panel-hint">No materials yet (PDF, CSV, PNG or JPG).</p>
            } @else {
              <ul class="sf__list">
                @for (m of materials(); track m.id) {
                  <li class="sf__item">
                    <span class="sf__micon" aria-hidden="true">
                      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                           stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6"/></svg>
                    </span>
                    <span class="sf__vbody">
                      <span class="sf__mtitle">{{ m.fileName }}</span>
                      <span class="sf__msub">{{ m.kind }} · {{ size(m.sizeBytes) }}</span>
                    </span>
                    <button type="button" class="sf__iconbtn sf__iconbtn--danger" title="Remove" (click)="removeMaterial(m.id)" aria-label="Remove material">
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2m3 0v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6"/></svg>
                    </button>
                  </li>
                }
              </ul>
            }
          </sb-card>
        </div>

        <!-- RIGHT (sticky) -->
        <div class="sf__col sf__col--side">
          <sb-card title="Publish">
            <div class="sf__publish">
              @if (session(); as s) {
                <div class="sf__publish-row">
                  <span>State</span>
                  <sb-status-pill [variant]="pillFor(s.status)">{{ s.status }}</sb-status-pill>
                </div>
                @if (canPublish()) {
                  <div class="sf__publish-actions">
                    @if (s.status !== 'Published') {
                      <sb-button variant="accent" size="sm" [loading]="publishBusy()" (clicked)="publish()">Publish</sb-button>
                    } @else {
                      <sb-button variant="secondary" size="sm" [loading]="publishBusy()" (clicked)="archive()">Archive</sb-button>
                    }
                  </div>
                }
                <p class="sf__muted">Draft &amp; archived sessions are hidden from the catalogue and cannot be enrolled.</p>
              } @else {
                <div class="sf__publish-row">
                  <span>State</span>
                  <sb-status-pill variant="warning">Draft</sb-status-pill>
                </div>
                <p class="sf__muted">New sessions are created as a draft, hidden from the catalogue. Publish once content is ready.</p>
              }
            </div>
          </sb-card>

          <sb-card title="Gating">
            <div class="sf__gating">
              <sb-form-field label="Prerequisite session" fieldId="sf-prereq" hint="Students must finish it before enrolling.">
                <sb-combobox inputId="sf-prereq" [options]="prerequisiteOptions()" [formControl]="prereqControl"
                             placeholder="Search sessions…" emptyText="No matching sessions" />
              </sb-form-field>

              <div class="sf__gating-quiz">
                <div>
                  <div class="sf__gating-title">Gating quiz</div>
                  <div class="sf__muted">{{ quizSummary() }}</div>
                </div>
                <sb-button variant="secondary" size="sm" [disabled]="!session()" (clicked)="configureQuiz()">Configure</sb-button>
              </div>
            </div>
          </sb-card>

          <sb-card title="Question bank">
            <div class="sf__bank">
              <p class="sf__muted"><strong class="sf__strong">{{ session()?.questionCount ?? 0 }}</strong> questions attached</p>
              <sb-button variant="secondary" size="sm" [disabled]="!session()" (clicked)="manageBank()">Manage question bank</sb-button>
            </div>
          </sb-card>
        </div>
      </div>
    }

    <sb-video-dialog
      [open]="videoDialogOpen()"
      [mode]="videoDialogMode()"
      [video]="editingVideo()"
      [submitting]="videoBusy()"
      [error]="videoError()"
      [progress]="uploadProgress()"
      (add)="addVideo($event)"
      (save)="saveEdit($event)"
      (cancel)="videoDialogOpen.set(false)"
    />
  `,
  styles: [`
    :host { display: block; }

    .sf__back { display: inline-flex; align-items: center; gap: var(--sb-space-2); margin-bottom: var(--sb-space-4); border: none; background: transparent; cursor: pointer; color: var(--sb-text-muted); font-family: var(--sb-font-sans); font-size: var(--sb-body-md-size); font-weight: 700; padding: 0; }
    .sf__back:hover { color: var(--sb-primary); }

    .sf__head { display: flex; align-items: flex-end; justify-content: space-between; gap: var(--sb-space-4); flex-wrap: wrap; margin-bottom: var(--sb-space-5); }
    .sf__head-actions { display: flex; gap: var(--sb-space-2); }
    .sf__title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-lg-size); font-weight: 800; letter-spacing: -0.01em; color: var(--sb-text); }
    .sf__subtitle { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    .sf__grid { display: grid; grid-template-columns: minmax(0, 2fr) minmax(0, 1fr); gap: var(--sb-space-4); align-items: start; }
    @media (max-width: 900px) { .sf__grid { grid-template-columns: 1fr; } }
    .sf__col { display: flex; flex-direction: column; gap: var(--sb-space-4); }
    .sf__col--side { position: sticky; top: var(--sb-space-2); }
    @media (max-width: 900px) { .sf__col--side { position: static; } }

    .sf__form { display: flex; flex-direction: column; gap: var(--sb-space-4); }
    .sf__row { display: grid; gap: var(--sb-space-3); }
    .sf__row--2 { grid-template-columns: 1fr 1fr; }
    .sf__row--3 { grid-template-columns: 1fr 1fr 1fr; }
    @media (max-width: 620px) { .sf__row--2, .sf__row--3 { grid-template-columns: 1fr; } }


    .sf__list { list-style: none; margin: 0; padding: 0; }
    .sf__item { display: flex; align-items: center; gap: var(--sb-space-3); padding: var(--sb-space-3) var(--sb-space-5); border-bottom: 1px solid var(--sb-border); transition: background var(--sb-timing-fast) var(--sb-easing-standard); }
    .sf__item:last-child { border-bottom: none; }
    .sf__item--dragging { opacity: 0.45; }
    .sf__item--over { background: var(--sb-primary-50); }
    .sf__grip { color: var(--sb-text-subtle); cursor: grab; font-size: 16px; user-select: none; padding: 0 var(--sb-space-1); }
    .sf__grip:active { cursor: grabbing; color: var(--sb-text-muted); }
    .sf__vicon { width: 34px; height: 34px; flex-shrink: 0; border-radius: var(--sb-radius-sm); background: var(--sb-info-bg); color: var(--sb-info-fg); display: inline-flex; align-items: center; justify-content: center; }
    .sf__micon { width: 32px; height: 32px; flex-shrink: 0; border-radius: var(--sb-radius-sm); background: var(--sb-surface-sunken); color: var(--sb-text-muted); display: inline-flex; align-items: center; justify-content: center; }
    .sf__vbody { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 3px; }
    .sf__vtitle, .sf__mtitle { font-weight: 700; font-size: var(--sb-body-md-size); color: var(--sb-text); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .sf__vsub { display: inline-flex; align-items: center; gap: var(--sb-space-2); flex-wrap: wrap; }
    .sf__vmeta { font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); }
    .sf__msub { font-size: var(--sb-body-sm-size); color: var(--sb-text-subtle); }
    .sf__vaccess { font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); white-space: nowrap; }
    .sf__vaccess strong { color: var(--sb-text); font-variant-numeric: tabular-nums; }
    .sf__vactions { display: inline-flex; gap: 4px; }
    .sf__iconbtn { width: 30px; height: 30px; border: 1px solid var(--sb-border); background: var(--sb-surface); border-radius: var(--sb-radius-md); cursor: pointer; display: inline-flex; align-items: center; justify-content: center; color: var(--sb-text-muted); font-size: 14px; }
    .sf__iconbtn:hover:not(:disabled) { background: var(--sb-surface-sunken); color: var(--sb-text); }
    .sf__iconbtn:disabled { opacity: 0.4; cursor: not-allowed; }
    .sf__iconbtn--danger { color: var(--sb-danger); }
    .sf__iconbtn--danger:hover:not(:disabled) { color: var(--sb-danger-fg); background: var(--sb-danger-bg); }

    .sf__panel-hint { margin: 0; padding: var(--sb-space-5); color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    .sf__publish, .sf__gating, .sf__bank { display: flex; flex-direction: column; gap: var(--sb-space-3); }
    .sf__publish-row { display: flex; justify-content: space-between; align-items: center; font-size: var(--sb-body-md-size); }
    .sf__publish-actions { display: flex; gap: var(--sb-space-2); }
    .sf__muted { margin: 0; font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); line-height: 1.5; }
    .sf__strong { color: var(--sb-text); }
    .sf__gating-quiz { border-top: 1px solid var(--sb-border); padding-top: var(--sb-space-3); display: flex; justify-content: space-between; align-items: center; gap: var(--sb-space-3); }
    .sf__gating-title { font-weight: 700; font-size: var(--sb-body-md-size); color: var(--sb-text); }

    .sb-input, .sb-textarea {
      width: 100%; padding: 0 var(--sb-space-3); border: 1px solid var(--sb-border-strong);
      border-radius: var(--sb-radius-md); font-size: var(--sb-body-md-size); font-family: var(--sb-font-sans);
      color: var(--sb-text); background: var(--sb-surface); outline: none;
      transition: border-color var(--sb-timing) var(--sb-easing-standard), box-shadow var(--sb-timing) var(--sb-easing-standard);
    }
    .sb-input { height: 40px; }
    .sb-textarea { padding: var(--sb-space-2) var(--sb-space-3); resize: vertical; min-height: 76px; line-height: 1.5; }
    .sb-input:focus, .sb-textarea:focus { border-color: var(--sb-primary); box-shadow: var(--sb-shadow-focus); }
  `],
})
export class SessionFormComponent {
  readonly #service = inject(SessionService);
  readonly #auth = inject(AuthStore);
  readonly #router = inject(Router);
  readonly #fb = inject(NonNullableFormBuilder);
  readonly #toast = inject(ToastService);

  /** Bound from the `:id` route segment in edit mode; undefined on `/sessions/new`. */
  readonly id = input<string>();

  readonly session = signal<SessionDetailDto | null>(null);
  readonly loadError = signal<string | null>(null);
  readonly isNew = computed(() => !this.id());

  readonly saving = signal(false);
  readonly thumbBusy = signal(false);
  readonly publishBusy = signal(false);
  readonly materialBusy = signal(false);
  readonly reordering = signal(false);

  // Drag-to-reorder (native HTML5 DnD, initiated from the grip handle only).
  readonly grabbing = signal<number | null>(null);
  readonly dragFrom = signal<number | null>(null);
  readonly dragOver = signal<number | null>(null);

  /** A just-picked thumbnail file — drives the dropzone list and (in create mode) the upload on save. */
  readonly #pickedThumb = signal<File | null>(null);
  /** Route id the loaded session belongs to — guards #init against duplicate fetches. */
  #loadedId: string | undefined;

  readonly canEditContent = computed(() => !!this.session() && this.#auth.hasPermission('SessionsEdit'));
  readonly canPublish = computed(() => this.#auth.hasPermission('SessionsPublish'));

  readonly videos = computed<SessionVideoDto[]>(() => this.session()?.videos ?? []);
  readonly materials = computed(() => this.session()?.materials ?? []);

  readonly form = this.#fb.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.maxLength(2000)]],
    gradeId: ['', [Validators.required]],
    subjectId: ['', [Validators.required]],
    specializationId: ['', [Validators.required]],
    price: [0, [Validators.required, Validators.min(0)]],
    validityDays: [90, [Validators.required, Validators.min(0), Validators.max(365)]],
  });

  // Prerequisite (managed outside the details form — persisted immediately in edit mode).
  // Disabled until the session exists; enabled by #applySession.
  readonly prereqControl = this.#fb.control<string>({ value: '', disabled: true });
  readonly #allSessions = signal<{ id: string; title: string }[]>([]);

  // Video dialog
  readonly videoDialogOpen = signal(false);
  readonly videoDialogMode = signal<'add' | 'edit'>('add');
  readonly editingVideo = signal<SessionVideoDto | null>(null);
  readonly videoBusy = signal(false);
  readonly videoError = signal<string | null>(null);
  readonly uploadProgress = signal<number | null>(null);

  readonly gradeOptions = computed<SelectOption[]>(() =>
    this.#service.grades().map((g) => ({ value: g.id, label: g.name })),
  );
  readonly subjectOptions = computed<SelectOption[]>(() =>
    this.#service.subjects().map((s) => ({ value: s.id, label: s.name })),
  );
  readonly #subjectId = signal('');
  readonly specializationOptions = computed<SelectOption[]>(() => {
    const subjectId = this.#subjectId();
    return this.#service
      .specializations()
      .filter((s) => !subjectId || s.subjectId === subjectId)
      .map((s) => ({ value: s.id, label: s.name }));
  });

  // No synthetic "none" row — the combobox's placeholder + clear (×) express the empty state.
  readonly prerequisiteOptions = computed<SelectOption[]>(() =>
    this.#allSessions()
      .filter((s) => s.id !== this.id())
      .map((s) => ({ value: s.id, label: s.title })),
  );

  readonly quizSummary = computed(() => {
    const q = this.session()?.quizSetting;
    if (!q) return 'Not configured';
    return `${q.timeLimitMinutes} min · ${q.questionCount} Qs · ${q.minPassPercent}% pass`;
  });

  /** The dropzone's file list — shows a freshly picked thumbnail (matches the prototype). */
  readonly thumbFiles = computed<UploadedFile[]>(() => {
    const picked = this.#pickedThumb();
    return picked ? [{ name: picked.name, sizeBytes: picked.size }] : [];
  });

  constructor() {
    // Keep specialization options scoped to the chosen subject; drop a now-invalid selection.
    this.form.controls.subjectId.valueChanges.pipe(takeUntilDestroyed()).subscribe((subjectId) => {
      this.#subjectId.set(subjectId);
      const specId = this.form.controls.specializationId.value;
      if (specId && this.#service.specializations().some((s) => s.id === specId && s.subjectId !== subjectId)) {
        this.form.controls.specializationId.setValue('');
      }
    });

    // Persist prerequisite changes immediately (edit mode). Programmatic sets use emitEvent:false.
    this.prereqControl.valueChanges.pipe(takeUntilDestroyed()).subscribe((value) => {
      void this.onPrerequisite(value ?? '');
    });

    // Load reference data + (edit) the session whenever the route id resolves. The async work runs
    // in a microtask so this effect only tracks id() — not the taxonomy signals those loaders read.
    effect(() => {
      const id = this.id();
      queueMicrotask(() => void this.#init(id));
    });
  }

  async #init(id: string | undefined): Promise<void> {
    if (id && this.#loadedId === id) return; // already loaded this session — don't refetch
    this.loadError.set(null);
    this.#pickedThumb.set(null);
    // Reference data is cached in the root service, so these are no-ops after the first load.
    const refs = Promise.all([
      this.#service.loadGrades(),
      this.#service.loadSubjects(),
      this.#service.loadSpecializations(),
    ]);
    if (!id) {
      this.#loadedId = undefined;
      this.session.set(null);
      this.prereqControl.disable({ emitEvent: false });
      this.form.reset({ title: '', description: '', gradeId: '', subjectId: '', specializationId: '', price: 0, validityDays: 90 });
      return;
    }
    try {
      await refs; // specializations power the subject derivation in #applySession
      const s = await this.#service.getById(id);
      this.#loadedId = id;
      this.#applySession(s);
      void this.#loadPrerequisiteCandidates();
    } catch {
      this.loadError.set('Could not load this session. It may not exist or you may not have access.');
    }
  }

  #applySession(s: SessionDetailDto): void {
    this.session.set(s);
    const subjectId = this.#service.specializations().find((sp) => sp.id === s.specializationId)?.subjectId ?? '';
    this.#subjectId.set(subjectId);
    this.form.reset({
      title: s.title,
      description: s.description ?? '',
      gradeId: s.gradeId,
      subjectId,
      specializationId: s.specializationId,
      price: s.price,
      validityDays: s.validityDays,
    });
    this.prereqControl.enable({ emitEvent: false });
    this.prereqControl.setValue(s.prerequisiteSessionId ?? '', { emitEvent: false });
  }

  /**
   * Update the loaded session + prerequisite picker from a side mutation (thumbnail, prerequisite,
   * publish/archive) WITHOUT resetting the details form — so unsaved edits in the form survive.
   */
  #mergeSession(s: SessionDetailDto): void {
    this.session.set(s);
    this.prereqControl.setValue(s.prerequisiteSessionId ?? '', { emitEvent: false });
  }

  async #loadPrerequisiteCandidates(): Promise<void> {
    try {
      // The API caps pageSize at 100, so page through to gather every candidate.
      const all: { id: string; title: string }[] = [];
      let page = 1;
      let totalPages = 1;
      do {
        const result = await this.#service.listRaw({ pageSize: 100, page });
        all.push(...result.items.map((x) => ({ id: x.id, title: x.title })));
        totalPages = result.totalPages;
        page++;
      } while (page <= totalPages);
      this.#allSessions.set(all);
    } catch {
      /* a missing candidate list just leaves the picker with the current value */
    }
  }

  err(
    control: 'title' | 'description' | 'gradeId' | 'subjectId' | 'specializationId' | 'price' | 'validityDays',
  ): string {
    const c = this.form.controls[control];
    if (!c.touched || c.valid) return '';
    if (c.hasError('required')) return 'This field is required.';
    if (c.hasError('min')) return 'Value is too low.';
    if (c.hasError('max')) return 'Maximum is 365 days.';
    if (c.hasError('maxlength')) return 'This value is too long.';
    return '';
  }

  // ── Primary save ─────────────────────────────────────────────────────────────
  async save(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.#toast.error('Please fix the highlighted fields.');
      return;
    }
    const v = this.form.getRawValue();
    const payload = {
      title: v.title.trim(),
      description: v.description.trim(),
      price: Number(v.price),
      validityDays: Number(v.validityDays),
      gradeId: v.gradeId,
      specializationId: v.specializationId,
    };
    this.saving.set(true);
    try {
      if (this.isNew()) {
        const created = await this.#service.create(payload);
        const picked = this.#pickedThumb();
        if (picked) {
          try {
            await this.#service.uploadThumbnail(created.id, picked);
          } catch {
            this.#toast.error('Session created, but the thumbnail upload failed — retry from the editor.');
          }
        }
        this.#toast.success('Session created');
        void this.#router.navigate(['/sessions', created.id, 'edit']);
      } else {
        this.#applySession(await this.#service.update(this.id()!, payload));
        this.#toast.success('Session saved');
      }
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not save the session.');
    } finally {
      this.saving.set(false);
    }
  }

  cancel(): void {
    void this.#router.navigate(['/sessions']);
  }

  // ── Thumbnail ─────────────────────────────────────────────────────────────────
  onThumbPicked(files: File[]): void {
    const file = files[0];
    if (!file) return;
    this.#pickedThumb.set(file);
    if (this.isNew()) {
      this.#toast.info('Thumbnail will be uploaded when you create the session.');
      return;
    }
    void this.#uploadThumb(file);
  }

  clearThumb(): void {
    this.#pickedThumb.set(null);
  }

  async #uploadThumb(file: File): Promise<void> {
    this.thumbBusy.set(true);
    try {
      this.#mergeSession(await this.#service.uploadThumbnail(this.id()!, file));
      this.#toast.success('Thumbnail updated');
    } catch {
      this.#pickedThumb.set(null);
      this.#toast.error(this.#service.error() ?? 'Could not upload the thumbnail.');
    } finally {
      this.thumbBusy.set(false);
    }
  }

  // ── Videos ──────────────────────────────────────────────────────────────────
  openAddVideo(): void {
    this.videoError.set(null);
    this.uploadProgress.set(null);
    this.editingVideo.set(null);
    this.videoDialogMode.set('add');
    this.videoDialogOpen.set(true);
  }

  openEditVideo(video: SessionVideoDto): void {
    this.videoError.set(null);
    this.editingVideo.set(video);
    this.videoDialogMode.set('edit');
    this.videoDialogOpen.set(true);
  }

  async addVideo(payload: AddVideoPayload): Promise<void> {
    const id = this.id();
    if (!id) return;
    this.videoBusy.set(true);
    this.videoError.set(null);
    this.uploadProgress.set(0);
    try {
      const created = await this.#service.addVideo(
        id,
        payload.file,
        payload.title,
        payload.lengthMinutes,
        payload.accessCount,
        (pct) => this.uploadProgress.set(pct),
      );
      this.#patchVideos((list) => [...list, created]);
      this.videoDialogOpen.set(false);
      this.#toast.success('Video added — processing started');
    } catch {
      this.videoError.set(this.#service.error() ?? 'Could not upload the video.');
    } finally {
      this.videoBusy.set(false);
      this.uploadProgress.set(null);
    }
  }

  async saveEdit(payload: EditVideoPayload): Promise<void> {
    const id = this.id();
    const video = this.editingVideo();
    if (!id || !video) return;
    this.videoBusy.set(true);
    this.videoError.set(null);
    try {
      const updated = await this.#service.updateVideo(
        id, video.id, payload.title, payload.lengthMinutes, payload.accessCount,
      );
      this.#patchVideos((list) => list.map((v) => (v.id === updated.id ? updated : v)));
      this.videoDialogOpen.set(false);
      this.#toast.success('Video updated');
    } catch {
      this.videoError.set(this.#service.error() ?? 'Could not update the video.');
    } finally {
      this.videoBusy.set(false);
    }
  }

  async removeVideo(video: SessionVideoDto): Promise<void> {
    const id = this.id();
    if (!id) return;
    try {
      await this.#service.removeVideo(id, video.id);
      this.#patchVideos((list) => list.filter((v) => v.id !== video.id));
      this.#toast.info('Video removed');
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not remove the video.');
    }
  }

  // ── Drag-to-reorder (grip handle → native HTML5 DnD) ───────────────────────────
  /** Arm dragging for this row — the `<li>` is only `draggable` while its grip is pressed. */
  grabAt(index: number): void {
    if (!this.reordering()) this.grabbing.set(index);
  }
  releaseGrip(): void {
    this.grabbing.set(null);
  }

  onDragStart(index: number, event: DragEvent): void {
    if (this.grabbing() !== index) {
      event.preventDefault(); // not started from the grip
      return;
    }
    this.dragFrom.set(index);
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData('text/plain', String(index));
    }
  }

  onDragOver(index: number, event: DragEvent): void {
    if (this.dragFrom() === null) return;
    event.preventDefault(); // allow drop
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
    if (this.dragOver() !== index) this.dragOver.set(index);
  }

  onDrop(index: number, event: DragEvent): void {
    event.preventDefault();
    const from = this.dragFrom();
    this.#resetDrag();
    if (from === null || from === index) return;
    void this.#reorder(from, index);
  }

  onDragEnd(): void {
    this.#resetDrag();
  }

  #resetDrag(): void {
    this.grabbing.set(null);
    this.dragFrom.set(null);
    this.dragOver.set(null);
  }

  async #reorder(from: number, to: number): Promise<void> {
    const id = this.id();
    const list = [...this.videos()];
    if (!id || from < 0 || to < 0 || from >= list.length || to >= list.length) return;
    const [moved] = list.splice(from, 1);
    list.splice(to, 0, moved);
    this.reordering.set(true);
    try {
      const reordered = await this.#service.reorderVideos(id, list.map((v) => v.id));
      this.#patchVideos(() => reordered);
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not reorder videos.');
    } finally {
      this.reordering.set(false);
    }
  }

  #patchVideos(fn: (list: SessionVideoDto[]) => SessionVideoDto[]): void {
    this.session.update((s) => (s ? { ...s, videos: fn(s.videos) } : s));
  }

  // ── Materials ─────────────────────────────────────────────────────────────────
  onMaterialPicked(files: File[]): void {
    const file = files[0];
    if (file) void this.#uploadMaterial(file);
  }

  async #uploadMaterial(file: File): Promise<void> {
    const id = this.id();
    if (!id) return;
    this.materialBusy.set(true);
    try {
      const created = await this.#service.addMaterial(id, file);
      this.session.update((s) => (s ? { ...s, materials: [...s.materials, created] } : s));
      this.#toast.success('Material added');
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not add the material.');
    } finally {
      this.materialBusy.set(false);
    }
  }

  async removeMaterial(materialId: string): Promise<void> {
    const id = this.id();
    if (!id) return;
    try {
      await this.#service.removeMaterial(id, materialId);
      this.session.update((s) => (s ? { ...s, materials: s.materials.filter((m) => m.id !== materialId) } : s));
      this.#toast.info('Material removed');
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not remove the material.');
    }
  }

  // ── Prerequisite / publish / navigation ────────────────────────────────────────
  async onPrerequisite(value: string): Promise<void> {
    const id = this.id();
    if (!id) return;
    try {
      this.#mergeSession(await this.#service.setPrerequisite(id, value || null));
      this.#toast.success(value ? 'Prerequisite set' : 'Prerequisite cleared');
    } catch {
      // Roll the picker back to the persisted value on conflict (self/cycle → 409).
      this.prereqControl.setValue(this.session()?.prerequisiteSessionId ?? '', { emitEvent: false });
      this.#toast.error(this.#service.error() ?? 'Could not set the prerequisite (self/cycle not allowed).');
    }
  }

  async publish(): Promise<void> {
    await this.#lifecycle(() => this.#service.publish(this.id()!), 'Session published');
  }

  async archive(): Promise<void> {
    await this.#lifecycle(() => this.#service.archive(this.id()!), 'Session archived');
  }

  async #lifecycle(op: () => Promise<SessionDetailDto>, message: string): Promise<void> {
    this.publishBusy.set(true);
    try {
      this.#mergeSession(await op());
      this.#toast.success(message);
    } catch {
      this.#toast.error(this.#service.error() ?? 'That state change isn’t allowed right now.');
    } finally {
      this.publishBusy.set(false);
    }
  }

  configureQuiz(): void {
    if (this.id()) void this.#router.navigate(['/sessions', this.id(), 'quiz-settings']);
  }
  manageBank(): void {
    if (this.id()) void this.#router.navigate(['/sessions', this.id()]);
  }

  // Presentation helpers
  pillFor = statusPill;
  vPill = videoStatusPill;
  size = fileSize;
}
