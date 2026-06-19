import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';
import {
  AlertComponent,
  ButtonComponent,
  CardComponent,
  ConfirmDialogComponent,
  PaginationComponent,
  SbTab,
  SbTableColumn,
  StatusPillComponent,
  TableCellDirective,
  TableComponent,
  TabsComponent,
  TagComponent,
  ToastService,
} from '@sb/shared/ui';
import {
  QuestionDto,
  SessionDetailDto,
  SessionMaterialDto,
  SessionVideoDto,
} from '../data-access/session.models';
import { SessionService } from '../data-access/session.service';
import {
  dateTime,
  fileSize,
  money,
  statusPill,
  subjectAccent,
  videoStatusPill,
} from '../session.presentation';

type DetailTab = 'overview' | 'videos' | 'materials' | 'bank' | 'enrolled' | 'activity';

/**
 * Session 360° detail (FR-ADM-SES-007/008, mockup `scrSessionDetail`). Header with the subject-tinted
 * icon tile, title + status pill, and Edit / Delete actions; tabbed body for Overview, Videos,
 * Materials (on-demand signed-URL download), the Question bank (paged, with edit/delete + links to the
 * editor and quiz settings), and Phase-4 placeholders (Enrolled, Activity, manual unlock).
 */
@Component({
  selector: 'sb-session-detail',
  standalone: true,
  imports: [
    CardComponent,
    ButtonComponent,
    StatusPillComponent,
    TagComponent,
    TabsComponent,
    TableComponent,
    TableCellDirective,
    PaginationComponent,
    ConfirmDialogComponent,
    AlertComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button type="button" class="sd__back" (click)="back()">
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
           stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <path d="M19 12H5M12 19l-7-7 7-7"/>
      </svg>
      Back to sessions
    </button>

    @if (loadError()) {
      <sb-alert variant="danger" title="Couldn’t load session">{{ loadError() }}</sb-alert>
    } @else if (session(); as s) {
      <!-- Header -->
      <div class="sd__header">
        <div class="sd__identity">
          <span class="sd__tile" [style.background]="tileBg()" [style.color]="tileFg()">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>
            </svg>
          </span>
          <div>
            <div class="sd__name-row">
              <h1 class="sd__name">{{ s.title }}</h1>
              <sb-status-pill [variant]="pillFor(s.status)">{{ s.status }}</sb-status-pill>
            </div>
            <div class="sd__sub">
              @if (s.specializationName) { <sb-tag [label]="s.specializationName" [subject]="accent()" /> }
              <span>{{ s.gradeName ?? '—' }} · {{ price(s.price) }} · {{ s.validityDays }}-day access</span>
            </div>
          </div>
        </div>

        <div class="sd__actions">
          <sb-button variant="secondary" (clicked)="unlock()">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2zM7 11V7a5 5 0 0 1 9.9-1"/></svg>
            Unlock for student
          </sb-button>
          @if (canEdit()) {
            <sb-button variant="primary" (clicked)="edit()">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                   stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7M18.5 2.5a2.12 2.12 0 0 1 3 3L12 15l-4 1 1-4z"/></svg>
              Edit
            </sb-button>
          }
          @if (canDelete()) {
            <sb-button variant="danger-ghost" (clicked)="deleteOpen.set(true)">Delete</sb-button>
          }
        </div>
      </div>

      <div class="sd__tabs">
        <sb-tabs [tabs]="tabs()" [active]="activeTab()" (tabChange)="onTab($event)" />
      </div>

      <!-- OVERVIEW -->
      @if (activeTab() === 'overview') {
        <div class="sd__overview">
          <div class="sd__tiles">
            <div class="sd__stat"><span class="sd__stat-label">Enrolled</span><span class="sd__stat-value">{{ s.enrolledCount }}</span><span class="sd__stat-sub">students (Phase 4)</span></div>
            <div class="sd__stat"><span class="sd__stat-label">Videos</span><span class="sd__stat-value">{{ s.videos.length }}</span><span class="sd__stat-sub">in pipeline</span></div>
            <div class="sd__stat"><span class="sd__stat-label">Questions</span><span class="sd__stat-value">{{ s.questionCount }}</span><span class="sd__stat-sub">{{ s.quizEligibleQuestionCount }} quiz-eligible</span></div>
            <div class="sd__stat"><span class="sd__stat-label">Materials</span><span class="sd__stat-value">{{ s.materials.length }}</span><span class="sd__stat-sub">attachments</span></div>
          </div>
          <sb-card title="Details">
            <dl class="sd__kv">
              <div><dt>Grade</dt><dd>{{ s.gradeName ?? '—' }}</dd></div>
              <div><dt>Specialization</dt><dd>{{ s.specializationName ?? '—' }}</dd></div>
              <div><dt>Price</dt><dd>{{ price(s.price) }}</dd></div>
              <div><dt>Validity</dt><dd>{{ s.validityDays }} days</dd></div>
              <div><dt>Prerequisite</dt><dd>{{ s.prerequisiteTitle ?? 'None' }}</dd></div>
              <div><dt>Gating quiz</dt><dd>{{ quizSummary() }}</dd></div>
            </dl>
            @if (s.description) {
              <p class="sd__desc">{{ s.description }}</p>
            }
          </sb-card>
        </div>
      }

      <!-- VIDEOS -->
      @if (activeTab() === 'videos') {
        <sb-card title="Videos & per-video access" [padding]="false">
          @if (canEdit()) {
            <sb-button cardActions variant="secondary" size="sm" (clicked)="edit()">Edit</sb-button>
          }
          @if (s.videos.length === 0) {
            <p class="sd__empty-inline">No videos uploaded yet.</p>
          } @else {
            <sb-table [columns]="videoColumns" [rows]="s.videos" [rowKey]="videoKey">
              <ng-template sbTableCell="order" let-v>{{ v.order + 1 }}</ng-template>
              <ng-template sbTableCell="title" let-v><span class="sd__strong">{{ v.title }}</span></ng-template>
              <ng-template sbTableCell="status" let-v>
                <sb-status-pill [variant]="vPill(v.processingStatus)">{{ v.processingStatus }}</sb-status-pill>
              </ng-template>
              <ng-template sbTableCell="access" let-v><strong>{{ v.accessCount }}×</strong></ng-template>
            </sb-table>
            <p class="sd__note">Playback &amp; watch analytics arrive with the Phase 5 secure video pipeline.</p>
          }
        </sb-card>
      }

      <!-- MATERIALS -->
      @if (activeTab() === 'materials') {
        @if (s.materials.length === 0) {
          <div class="sd__empty">No materials attached. Add PDFs, sheets or images from the editor.</div>
        } @else {
          <div class="sd__materials">
            @for (m of s.materials; track m.id) {
              <div class="sd__material">
                <span class="sd__material-icon" aria-hidden="true">
                  <svg width="19" height="19" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                       stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6"/></svg>
                </span>
                <div class="sd__material-body">
                  <div class="sd__material-name">{{ m.fileName }}</div>
                  <div class="sd__material-sub">{{ m.kind }} · {{ size(m.sizeBytes) }}</div>
                </div>
                <sb-button variant="ghost" size="sm" [loading]="downloadingId() === m.id" (clicked)="download(m)">Download</sb-button>
              </div>
            }
          </div>
        }
      }

      <!-- QUESTION BANK -->
      @if (activeTab() === 'bank') {
        <sb-card title="Attached questions" [padding]="false">
          <div cardActions class="sd__bank-actions">
            @if (canEdit()) {
              <sb-button variant="secondary" size="sm" (clicked)="quizSettings()">Quiz settings</sb-button>
            }
            @if (canQuestionsCreate()) {
              <sb-button variant="primary" size="sm" (clicked)="newQuestion()">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                     stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 5v14M5 12h14"/></svg>
                New question
              </sb-button>
            }
          </div>

          @if (questions().length === 0 && !questionsLoading()) {
            <p class="sd__empty-inline">No questions in this session’s bank yet.</p>
          } @else {
            <sb-table [columns]="questionColumns" [rows]="questions()" [rowKey]="questionKey">
              <ng-template sbTableCell="text" let-q>
                <button type="button" class="sd__qtext" (click)="editQuestion(q)">{{ questionLabel(q) }}</button>
              </ng-template>
              <ng-template sbTableCell="mark" let-q><strong>{{ q.mark }}</strong></ng-template>
              <ng-template sbTableCell="variations" let-q>{{ q.variations.length + 1 }}</ng-template>
              <ng-template sbTableCell="quiz" let-q>
                @if (q.isValidForQuiz) {
                  <sb-status-pill variant="success">Eligible</sb-status-pill>
                } @else {
                  <sb-status-pill variant="neutral">Excluded</sb-status-pill>
                }
              </ng-template>
              <ng-template sbTableCell="actions" let-q>
                <div class="sd__qactions">
                  @if (canQuestionsEdit()) {
                    <sb-button variant="ghost" size="sm" (clicked)="editQuestion(q)">Edit</sb-button>
                  }
                  @if (canQuestionsDelete()) {
                    <sb-button variant="secondary" size="sm" (clicked)="askDeleteQuestion(q)">Delete</sb-button>
                  }
                </div>
              </ng-template>
            </sb-table>
            @if (questionsTotal() > questionsPageSize) {
              <div class="sd__bank-pager">
                <sb-pagination
                  [page]="questionsPage()"
                  [pageCount]="questionsPageCount()"
                  [total]="questionsTotal()"
                  [pageSize]="questionsPageSize"
                  (pageChange)="onQuestionsPage($event)"
                />
              </div>
            }
          }
        </sb-card>
      }

      <!-- ENROLLED (Phase 4) -->
      @if (activeTab() === 'enrolled') {
        <div class="sd__empty">Enrolled students, progress and refunds arrive in Phase 4 (enrollment &amp; codes).</div>
      }

      <!-- ACTIVITY (placeholder until audit wiring) -->
      @if (activeTab() === 'activity') {
        <div class="sd__empty">Per-session activity will appear here once the audit feed is wired in.</div>
      }
    } @else {
      <p class="sd__loading">Loading…</p>
    }

    @if (session(); as s) {
      <sb-confirm-dialog
        [open]="deleteOpen()"
        [title]="'Delete ' + s.title + '?'"
        message="If this session has enrollments or history it is soft-deleted (hidden, history preserved). This action is audited."
        confirmLabel="Delete session"
        confirmVariant="danger"
        [busy]="actionBusy()"
        (confirm)="remove()"
        (cancel)="deleteOpen.set(false)"
      />

      <sb-confirm-dialog
        [open]="deleteQuestionOpen()"
        title="Delete question?"
        message="The question and its variations are removed from this session’s bank. This is a soft-delete and is audited."
        confirmLabel="Delete question"
        confirmVariant="danger"
        [busy]="actionBusy()"
        (confirm)="confirmDeleteQuestion()"
        (cancel)="deleteQuestionOpen.set(false)"
      />
    }
  `,
  styles: [`
    :host { display: block; }

    .sd__back { display: inline-flex; align-items: center; gap: var(--sb-space-2); margin-bottom: var(--sb-space-4); border: none; background: transparent; cursor: pointer; color: var(--sb-text-muted); font-family: var(--sb-font-sans); font-size: var(--sb-body-md-size); font-weight: 700; padding: 0; }
    .sd__back:hover { color: var(--sb-primary); }
    .sd__loading { padding: var(--sb-space-8) 0; color: var(--sb-text-muted); }

    .sd__header { display: flex; align-items: flex-start; justify-content: space-between; gap: var(--sb-space-4); flex-wrap: wrap; margin-bottom: var(--sb-space-5); }
    .sd__identity { display: flex; align-items: center; gap: var(--sb-space-4); min-width: 0; }
    .sd__tile { width: 52px; height: 52px; flex-shrink: 0; border-radius: var(--sb-radius-md); overflow: hidden; display: inline-flex; align-items: center; justify-content: center; }
    .sd__tile-img { width: 100%; height: 100%; object-fit: cover; }
    .sd__name-row { display: flex; align-items: center; gap: var(--sb-space-3); flex-wrap: wrap; }
    .sd__name { margin: 0; font-size: var(--sb-heading-lg-size); font-weight: 800; letter-spacing: -0.01em; color: var(--sb-text); }
    .sd__sub { margin-top: var(--sb-space-1); display: flex; align-items: center; gap: var(--sb-space-2); flex-wrap: wrap; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
    .sd__actions { display: flex; gap: var(--sb-space-2); flex-wrap: wrap; align-items: center; }

    .sd__tabs { margin-bottom: var(--sb-space-4); }

    .sd__overview { display: flex; flex-direction: column; gap: var(--sb-space-4); }
    .sd__tiles { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: var(--sb-space-3); }
    .sd__stat { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-lg); padding: var(--sb-space-4); display: flex; flex-direction: column; gap: var(--sb-space-1); box-shadow: var(--sb-shadow-sm); }
    .sd__stat-label { font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); font-weight: 600; }
    .sd__stat-value { font-size: var(--sb-heading-lg-size); font-weight: 800; line-height: 1; color: var(--sb-text); }
    .sd__stat-sub { font-size: var(--sb-body-sm-size); color: var(--sb-text-subtle); }

    .sd__kv { margin: 0; display: grid; grid-template-columns: 1fr 1fr; gap: var(--sb-space-2) var(--sb-space-6); }
    @media (max-width: 620px) { .sd__kv { grid-template-columns: 1fr; } }
    .sd__kv > div { display: flex; justify-content: space-between; gap: var(--sb-space-3); padding: var(--sb-space-1) 0; }
    .sd__kv dt { color: var(--sb-text-muted); font-size: var(--sb-body-sm-size); }
    .sd__kv dd { margin: 0; font-weight: 600; font-size: var(--sb-body-sm-size); color: var(--sb-text); text-align: right; }
    .sd__desc { margin: var(--sb-space-4) 0 0; padding-top: var(--sb-space-4); border-top: 1px solid var(--sb-border); color: var(--sb-text-muted); line-height: 1.6; }

    .sd__strong { font-weight: 700; }
    .sd__note { margin: 0; padding: var(--sb-space-3) var(--sb-space-5); color: var(--sb-text-subtle); font-size: var(--sb-body-sm-size); border-top: 1px solid var(--sb-border); }

    .sd__materials { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: var(--sb-space-3); }
    .sd__material { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-lg); padding: var(--sb-space-4); display: flex; gap: var(--sb-space-3); align-items: center; }
    .sd__material-icon { width: 40px; height: 40px; flex-shrink: 0; border-radius: var(--sb-radius-md); background: var(--sb-info-bg); color: var(--sb-info-fg); display: inline-flex; align-items: center; justify-content: center; }
    .sd__material-body { flex: 1; min-width: 0; }
    .sd__material-name { font-weight: 700; font-size: var(--sb-body-sm-size); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .sd__material-sub { font-size: var(--sb-body-sm-size); color: var(--sb-text-subtle); }

    .sd__bank-actions { display: flex; gap: var(--sb-space-2); }
    .sd__qtext { border: none; background: none; padding: 0; cursor: pointer; text-align: left; font-family: var(--sb-font-sans); font-weight: 600; font-size: var(--sb-body-md-size); color: var(--sb-text); max-width: 460px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .sd__qtext:hover { color: var(--sb-primary); }
    .sd__qactions { display: inline-flex; gap: var(--sb-space-2); justify-content: flex-end; }
    .sd__bank-pager { padding: var(--sb-space-3) var(--sb-space-5); }

    .sd__empty { background: var(--sb-surface); border: 1px dashed var(--sb-border-strong); border-radius: var(--sb-radius-lg); padding: var(--sb-space-10); text-align: center; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
    .sd__empty-inline { margin: 0; padding: var(--sb-space-8); text-align: center; color: var(--sb-text-muted); }
  `],
})
export class SessionDetailComponent {
  readonly #service = inject(SessionService);
  readonly #auth = inject(AuthStore);
  readonly #router = inject(Router);
  readonly #toast = inject(ToastService);

  /** Bound from the `:id` route segment (withComponentInputBinding). */
  readonly id = input.required<string>();

  readonly session = signal<SessionDetailDto | null>(null);
  readonly loadError = signal<string | null>(null);
  readonly activeTab = signal<DetailTab>('overview');

  readonly questions = signal<QuestionDto[]>([]);
  readonly questionsTotal = signal(0);
  readonly questionsPage = signal(1);
  readonly questionsLoading = signal(false);
  readonly questionsPageSize = 10;
  readonly questionsPageCount = computed(() =>
    Math.max(1, Math.ceil(this.questionsTotal() / this.questionsPageSize)),
  );

  readonly downloadingId = signal<string | null>(null);
  readonly deleteOpen = signal(false);
  readonly deleteQuestionOpen = signal(false);
  readonly #pendingDeleteQuestion = signal<QuestionDto | null>(null);
  readonly actionBusy = signal(false);

  readonly canEdit = computed(() => this.#auth.hasPermission('SessionsEdit'));
  readonly canDelete = computed(() => this.#auth.hasPermission('SessionsDelete'));
  readonly canQuestionsCreate = computed(() => this.#auth.hasPermission('QuestionsCreate'));
  readonly canQuestionsEdit = computed(() => this.#auth.hasPermission('QuestionsEdit'));
  readonly canQuestionsDelete = computed(() => this.#auth.hasPermission('QuestionsDelete'));

  readonly tabs = computed<SbTab[]>(() => {
    const s = this.session();
    return [
      { id: 'overview', label: 'Overview' },
      { id: 'videos', label: 'Videos', badge: s?.videos.length ?? null },
      { id: 'materials', label: 'Materials', badge: s?.materials.length ?? null },
      { id: 'bank', label: 'Question bank', badge: s?.questionCount ?? null },
      { id: 'enrolled', label: 'Enrolled', badge: s?.enrolledCount ?? null },
      { id: 'activity', label: 'Activity' },
    ];
  });

  readonly videoColumns: readonly SbTableColumn[] = [
    { key: 'order', header: '#', width: '1%' },
    { key: 'title', header: 'Video' },
    { key: 'status', header: 'Status' },
    { key: 'access', header: 'Access count', align: 'right' },
  ];
  readonly questionColumns: readonly SbTableColumn[] = [
    { key: 'text', header: 'Question' },
    { key: 'mark', header: 'Mark', align: 'right' },
    { key: 'variations', header: 'Variations', align: 'right' },
    { key: 'quiz', header: 'Quiz' },
    { key: 'actions', header: '', align: 'right', width: '1%' },
  ];

  readonly videoKey = (v: SessionVideoDto): string => v.id;
  readonly questionKey = (q: QuestionDto): string => q.id;

  readonly quizSummary = computed(() => {
    const q = this.session()?.quizSetting;
    if (!q) return 'Not configured';
    return `${q.timeLimitMinutes} min · ${q.questionCount} Qs · ${q.minPassPercent}% pass`;
  });

  readonly accent = computed(() =>
    subjectAccent(this.session()?.specializationName ?? this.session()?.specializationId),
  );
  readonly tileBg = computed(() => `var(--sb-subject-${this.accent()}-bg)`);
  readonly tileFg = computed(() => `var(--sb-subject-${this.accent()}-deep)`);

  constructor() {
    effect(() => {
      const id = this.id();
      queueMicrotask(() => void this.#load(id));
    });
  }

  async #load(id: string): Promise<void> {
    this.loadError.set(null);
    this.session.set(null);
    this.activeTab.set('overview');
    this.questions.set([]);
    this.questionsTotal.set(0);
    this.questionsPage.set(1);
    try {
      this.session.set(await this.#service.getById(id));
    } catch {
      this.loadError.set('Could not load this session. It may not exist or you may not have access.');
    }
  }

  onTab(id: string): void {
    const tab = id as DetailTab;
    this.activeTab.set(tab);
    if (tab === 'bank' && this.questions().length === 0) void this.#loadQuestions(1);
  }

  async #loadQuestions(page: number): Promise<void> {
    this.questionsLoading.set(true);
    try {
      const res = await this.#service.listQuestions(this.id(), page, this.questionsPageSize);
      this.questions.set(res.items);
      this.questionsTotal.set(res.total);
      this.questionsPage.set(page);
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not load the question bank.');
    } finally {
      this.questionsLoading.set(false);
    }
  }

  onQuestionsPage(page: number): void {
    void this.#loadQuestions(page);
  }

  // ── Navigation ────────────────────────────────────────────────────────────────
  back(): void {
    void this.#router.navigate(['/sessions']);
  }
  edit(): void {
    void this.#router.navigate(['/sessions', this.id(), 'edit']);
  }
  quizSettings(): void {
    void this.#router.navigate(['/sessions', this.id(), 'quiz-settings']);
  }
  newQuestion(): void {
    void this.#router.navigate(['/sessions', this.id(), 'questions', 'new']);
  }
  editQuestion(q: QuestionDto): void {
    void this.#router.navigate(['/sessions', this.id(), 'questions', q.id, 'edit']);
  }

  unlock(): void {
    this.#toast.info('Manual unlock for a student arrives in Phase 4 (enrollment & codes).');
  }

  // ── Materials download (on-demand signed URL) ──────────────────────────────────
  async download(material: SessionMaterialDto): Promise<void> {
    this.downloadingId.set(material.id);
    try {
      const signed = await this.#service.getMaterialUrl(this.id(), material.id);
      window.open(signed.url, '_blank', 'noopener');
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not generate a download link.');
    } finally {
      this.downloadingId.set(null);
    }
  }

  // ── Delete session / question ───────────────────────────────────────────────────
  async remove(): Promise<void> {
    this.actionBusy.set(true);
    try {
      await this.#service.remove(this.id());
      this.deleteOpen.set(false);
      this.#toast.info('Session deleted');
      void this.#router.navigate(['/sessions']);
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not delete the session.');
    } finally {
      this.actionBusy.set(false);
    }
  }

  askDeleteQuestion(q: QuestionDto): void {
    this.#pendingDeleteQuestion.set(q);
    this.deleteQuestionOpen.set(true);
  }

  async confirmDeleteQuestion(): Promise<void> {
    const q = this.#pendingDeleteQuestion();
    if (!q) return;
    this.actionBusy.set(true);
    try {
      await this.#service.removeQuestion(this.id(), q.id);
      this.deleteQuestionOpen.set(false);
      this.questions.update((list) => list.filter((x) => x.id !== q.id));
      this.questionsTotal.update((t) => Math.max(0, t - 1));
      this.session.update((s) => (s ? { ...s, questionCount: Math.max(0, s.questionCount - 1) } : s));
      this.#toast.info('Question deleted');
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not delete the question.');
    } finally {
      this.actionBusy.set(false);
    }
  }

  // Presentation helpers
  pillFor = statusPill;
  vPill = videoStatusPill;
  price = money;
  size = fileSize;
  at = dateTime;

  questionLabel(q: QuestionDto): string {
    const body = (q.bodyLatex ?? '').trim();
    if (body) return body.length > 90 ? `${body.slice(0, 90)}…` : body;
    return q.imageUrl ? 'Image question' : 'Untitled question';
  }
}
