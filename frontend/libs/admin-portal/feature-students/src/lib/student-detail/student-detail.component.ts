import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore, PendingApprovalsStore } from '@sb/shared/data-access';
import {
  AlertComponent,
  AvatarComponent,
  ButtonComponent,
  CardComponent,
  ConfirmDialogComponent,
  ProgressComponent,
  SbTab,
  SbTableColumn,
  StatusPillComponent,
  TableCellDirective,
  TableComponent,
  TabsComponent,
  ToastService,
} from '@sb/shared/ui';
import {
  StudentAttendanceProgress,
  StudentAuditEntry,
  StudentDetail,
  StudentEnrollmentDto,
  UpdateStudentContactRequest,
} from '../data-access/student.models';
import { StudentService } from '../data-access/student.service';
import { ReasonDialogComponent } from '../reason-dialog/reason-dialog.component';
import { StudentContactFormComponent } from '../student-contact-form/student-contact-form.component';
import {
  amount,
  avatarSubject,
  dateTime,
  methodPill,
  statusDot,
  statusPill,
  studentInitials,
} from '../student.presentation';

type DetailTab = 'logins' | 'enroll' | 'activity';

interface IdImageState {
  loading: boolean;
  url: string | null;
  error: boolean;
}

/**
 * Student 360° detail (FR-ADM-STU-002/005/006/008, FR-PLAT-DEV-006, mockup `scrStudentDetail`).
 * Titled cards: Profile (with the ID image loaded on demand via a signed URL, audited per view —
 * FR-PLAT-AST-003), Bound device, Enrollments & attendance (per-session video/quiz progress —
 * FR-ADM-ATT-002), and History tabs (login + activity from the audit log). Lifecycle actions are
 * permission-gated and toasted.
 */
@Component({
  selector: 'sb-student-detail',
  standalone: true,
  imports: [
    RouterLink,
    CardComponent,
    AvatarComponent,
    ButtonComponent,
    StatusPillComponent,
    AlertComponent,
    ProgressComponent,
    TabsComponent,
    TableComponent,
    TableCellDirective,
    ConfirmDialogComponent,
    ReasonDialogComponent,
    StudentContactFormComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="det__back" routerLink="/students">
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
           stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <path d="M19 12H5M12 19l-7-7 7-7"/>
      </svg>
      Back to students
    </a>

    @if (loadError()) {
      <sb-alert variant="danger" title="Couldn’t load student">{{ loadError() }}</sb-alert>
    } @else if (student(); as s) {
      <!-- Profile header -->
      <div class="det__header">
        <div class="det__identity">
          <sb-avatar size="xl" [initials]="initials(s.fullName)" [subject]="subjectFor(s.id)" [status]="dotFor(s.status)" />
          <div>
            <div class="det__name-row">
              <h1 class="det__name">{{ s.fullName }}</h1>
              <sb-status-pill [variant]="pillFor(s.status)">{{ s.status }}</sb-status-pill>
            </div>
            <p class="det__sub">{{ s.gradeName ?? 'No grade' }} · {{ s.schoolName }} · {{ s.cityName ?? '—' }}, {{ s.regionName ?? '—' }}</p>
          </div>
        </div>

        <div class="det__actions">
          @if (s.status === 'Pending' && canApprove()) {
            <sb-button variant="accent" (clicked)="approve()">Approve</sb-button>
          }
          @if (s.status === 'Pending' && canReject()) {
            <sb-button variant="danger-ghost" (clicked)="openReject()">Reject</sb-button>
          }
          @if (s.status === 'Active' && canDeactivate()) {
            <sb-button variant="secondary" (clicked)="deactivateOpen.set(true)">Deactivate</sb-button>
          }
          @if (s.status === 'Inactive' && canDeactivate()) {
            <sb-button variant="accent" (clicked)="reactivate()">Reactivate</sb-button>
          }
          @if (canDeviceClear()) {
            <sb-button variant="secondary" [disabled]="!s.activeDevice" (clicked)="openClear()">Clear device</sb-button>
          }
        </div>
      </div>

      @if (s.status === 'Rejected' && s.rejectionReason) {
        <sb-alert variant="danger" title="Registration rejected">{{ s.rejectionReason }}</sb-alert>
      }

      <div class="det__cols">
        <!-- Left column -->
        <div class="det__col">
          <sb-card title="Profile">
            @if (canEdit()) {
              <sb-button cardActions variant="ghost" size="sm" (clicked)="openContact()">Edit</sb-button>
            }

            <!-- ID image (loaded on demand, audited) -->
            <div class="det__id">
              @if (!s.hasIdImage) {
                <div class="det__id-msg">No ID image on file</div>
              } @else if (idImage(); as im) {
                @if (im.loading) {
                  <div class="det__id-msg">Loading secure preview…</div>
                } @else if (im.url) {
                  <img class="det__id-img" [src]="im.url" alt="National ID verification document" />
                  <span class="det__id-badge">
                    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                         stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6"/>
                    </svg>
                    National ID
                  </span>
                  @if (s.status !== 'Pending') {
                    <span class="det__id-verified"><sb-status-pill variant="success">Verified</sb-status-pill></span>
                  }
                } @else {
                  <button type="button" class="det__id-load" (click)="loadIdImage()">Retry preview</button>
                }
              } @else {
                <button type="button" class="det__id-load" (click)="loadIdImage()">
                  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                       stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                    <path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7z"/><circle cx="12" cy="12" r="3"/>
                  </svg>
                  <span>View ID image</span>
                  <span class="det__id-hint">Opening is audited</span>
                </button>
              }
            </div>

            <dl class="det__kv">
              <div><dt>Phone</dt><dd>{{ s.phoneNumber }}</dd></div>
              <div><dt>Parent phone 1</dt><dd>{{ s.parentPhonePrimary }}</dd></div>
              <div><dt>Parent phone 2</dt><dd>{{ s.parentPhoneSecondary ?? '—' }}</dd></div>
              <div><dt>Grade</dt><dd>{{ s.gradeName ?? '—' }}</dd></div>
              <div><dt>School</dt><dd>{{ s.schoolName }}</dd></div>
              <div><dt>City / Region</dt><dd>{{ s.cityName ?? '—' }}, {{ s.regionName ?? '—' }}</dd></div>
              <div><dt>Terms</dt><dd>{{ s.termsVersion ? s.termsVersion + ' · ' + at(s.termsAcceptedAtUtc) : '—' }}</dd></div>
            </dl>
          </sb-card>

          <sb-card title="Bound device">
            @if (s.activeDevice; as d) {
              <div class="det__device">
                <span class="det__device-icon" aria-hidden="true">
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                       stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M5 2h14a2 2 0 0 1 2 2v16a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2zM11 18h2"/>
                  </svg>
                </span>
                <div>
                  <div class="det__device-name">{{ d.fingerprintSummary ?? 'Bound device' }}</div>
                  <div class="det__device-sub">Bound {{ at(d.boundAtUtc) }} · anti-sharing active</div>
                </div>
              </div>
            } @else {
              <p class="det__muted">No device bound. The student may bind a device on next sign-in.</p>
            }
          </sb-card>
        </div>

        <!-- Right column -->
        <div class="det__col">
          <sb-card title="Enrollments & attendance">
            @if (canViewAttendance()) {
              <a cardActions class="det__report" routerLink="/attendance">Full report</a>
            }
            @if (!canViewAttendance()) {
              <p class="det__muted">Attendance reporting requires the Attendance permission.</p>
            } @else if (attendanceLoading() && attendance().length === 0) {
              <p class="det__muted">Loading progress…</p>
            } @else if (attendance().length === 0) {
              <p class="det__muted">No enrolments yet. Progress appears once the student enrols in a session.</p>
            } @else {
              <ul class="det__prog">
                @for (p of attendance(); track p.enrollmentId) {
                  <li>
                    <div class="det__prog-head">
                      <span class="det__strong">{{ p.sessionTitle ?? 'Session' }}</span>
                      <span class="det__prog-meta">{{ p.videosWatched }}/{{ p.videosTotal }} videos · quiz {{ quizLabel(p) }}</span>
                    </div>
                    <sb-progress [value]="watchPct(p)" [variant]="quizVariant(p)" />
                  </li>
                }
              </ul>
            }
          </sb-card>

          <sb-card title="History">
            <sb-tabs [tabs]="tabs" [active]="activeTab()" (tabChange)="onTabChange($event)" />
            <div class="det__hist">
              @if (activeTab() === 'enroll') {
                @if (enrollments().length === 0 && !enrollmentsLoading()) {
                  <p class="det__muted det__hist-empty">No enrolments or transactions recorded yet.</p>
                } @else {
                  <sb-table [columns]="enrollmentColumns" [rows]="enrollments()" [rowKey]="enrollmentKey">
                    <ng-template sbTableCell="session" let-r><span class="det__strong">{{ r.sessionTitle }}</span></ng-template>
                    <ng-template sbTableCell="method" let-r>
                      <sb-status-pill [variant]="methodPill(r.method)">{{ r.method }}</sb-status-pill>
                    </ng-template>
                    <ng-template sbTableCell="amount" let-r>{{ amount(r.amount) }}</ng-template>
                    <ng-template sbTableCell="when" let-r>{{ at(r.enrolledAtUtc) }}</ng-template>
                  </sb-table>
                  @if (enrollmentsHasMore()) {
                    <div class="det__more">
                      <sb-button variant="ghost" size="sm" [loading]="enrollmentsLoading()" (clicked)="loadMoreEnrollments()">Load more</sb-button>
                    </div>
                  }
                }
              } @else if (currentEntries().length === 0 && !currentLoading()) {
                <p class="det__muted det__hist-empty">No {{ activeTab() === 'activity' ? 'activity' : 'sign-ins' }} recorded yet.</p>
              } @else {
                <sb-table [columns]="historyColumns()" [rows]="currentEntries()" [rowKey]="auditKey">
                  <ng-template sbTableCell="when" let-row>{{ at(row.occurredAtUtc) }}</ng-template>
                  <ng-template sbTableCell="action" let-row><span class="det__strong">{{ action(row.action) }}</span></ng-template>
                  <ng-template sbTableCell="ip" let-row>{{ row.ipAddress ?? '—' }}</ng-template>
                  <ng-template sbTableCell="detail" let-row>{{ row.summary ?? '—' }}</ng-template>
                </sb-table>
                @if (currentHasMore()) {
                  <div class="det__more">
                    <sb-button variant="ghost" size="sm" [loading]="currentLoading()" (clicked)="loadMore()">Load more</sb-button>
                  </div>
                }
              }
            </div>
          </sb-card>
        </div>
      </div>
    } @else {
      <p class="det__muted det__loading">Loading…</p>
    }

    <!-- Deactivate confirmation -->
    @if (student(); as s) {
      <sb-confirm-dialog
        [open]="deactivateOpen()"
        [title]="'Deactivate ' + s.fullName + '?'"
        message="The student will be blocked from signing in until reactivated. This action is audited."
        confirmLabel="Deactivate"
        confirmVariant="danger"
        [busy]="actionBusy()"
        (confirm)="deactivate()"
        (cancel)="deactivateOpen.set(false)"
      />

      <sb-reason-dialog
        [open]="rejectOpen()"
        [title]="'Reject ' + s.fullName + '?'"
        intro="A reason is required. It is stored in history and shown to the student."
        label="Rejection reason"
        placeholder="e.g. ID image unclear — please re-upload a readable photo."
        confirmLabel="Reject registration"
        confirmVariant="danger"
        [busy]="actionBusy()"
        [error]="actionError()"
        (confirm)="reject($event)"
        (cancel)="rejectOpen.set(false)"
      />

      <sb-reason-dialog
        [open]="clearOpen()"
        [title]="'Clear ' + s.fullName + '’s device?'"
        intro="The student will be able to bind a new device on next sign-in. Provide a reason for the audit log."
        label="Reason for clearing"
        placeholder="e.g. Student replaced their phone."
        confirmLabel="Clear device"
        confirmVariant="danger"
        [busy]="actionBusy()"
        [error]="actionError()"
        (confirm)="clearDevice($event)"
        (cancel)="clearOpen.set(false)"
      />

      <sb-student-contact-form
        [open]="contactOpen()"
        [student]="s"
        [grades]="grades()"
        [submitting]="actionBusy()"
        [error]="actionError()"
        (save)="saveContact($event)"
        (cancel)="contactOpen.set(false)"
      />
    }
  `,
  styles: [`
    :host { display: block; }

    .det__back { display: inline-flex; align-items: center; gap: var(--sb-space-2); margin-bottom: var(--sb-space-4); color: var(--sb-text-muted); font-size: var(--sb-body-md-size); font-weight: 700; text-decoration: none; }
    .det__back:hover { color: var(--sb-primary); }
    .det__loading { padding: var(--sb-space-8) 0; }

    /* Header */
    .det__header { display: flex; align-items: flex-start; justify-content: space-between; gap: var(--sb-space-4); flex-wrap: wrap; margin-bottom: var(--sb-space-5); }
    .det__identity { display: flex; align-items: center; gap: var(--sb-space-4); min-width: 0; }
    .det__name-row { display: flex; align-items: center; gap: var(--sb-space-3); flex-wrap: wrap; }
    .det__name { margin: 0; font-size: var(--sb-heading-lg-size); font-weight: 800; letter-spacing: -0.01em; color: var(--sb-text); }
    .det__sub { margin: var(--sb-space-1) 0 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
    .det__actions { display: flex; gap: var(--sb-space-2); flex-wrap: wrap; align-items: center; }

    /* Rejected alert spacing */
    sb-alert { display: block; margin-bottom: var(--sb-space-4); }

    /* Columns */
    .det__cols { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 2fr); gap: var(--sb-space-4); align-items: start; }
    @media (max-width: 900px) { .det__cols { grid-template-columns: 1fr; } }
    .det__col { display: flex; flex-direction: column; gap: var(--sb-space-4); }

    /* ID image */
    .det__id {
      position: relative;
      aspect-ratio: 16 / 10;
      border-radius: var(--sb-radius-md);
      border: 1px solid var(--sb-border);
      background: var(--sb-surface-sunken);
      overflow: hidden;
      display: flex;
      align-items: center;
      justify-content: center;
      margin-bottom: var(--sb-space-3);
    }
    .det__id-img { width: 100%; height: 100%; object-fit: cover; }
    .det__id-msg { color: var(--sb-text-subtle); font-size: var(--sb-body-sm-size); }
    .det__id-load {
      display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 4px;
      width: 100%; height: 100%; border: none; background: transparent; cursor: pointer;
      color: var(--sb-text-muted); font-family: var(--sb-font-sans); font-size: var(--sb-body-md-size); font-weight: 600;
    }
    .det__id-load:hover { background: var(--sb-primary-50); color: var(--sb-primary-700); }
    .det__id-hint { font-size: var(--sb-label-sm-size); color: var(--sb-text-subtle); font-weight: 600; }
    .det__id-badge { position: absolute; bottom: 8px; left: 8px; display: inline-flex; align-items: center; gap: 5px; font-size: 11px; font-weight: 700; color: #fff; background: rgba(0,0,0,.55); padding: 3px 9px; border-radius: var(--sb-radius-pill); }
    .det__id-verified { position: absolute; top: 8px; right: 8px; }

    /* KV list */
    .det__kv { margin: 0; display: flex; flex-direction: column; }
    .det__kv > div { display: flex; justify-content: space-between; gap: var(--sb-space-3); padding: var(--sb-space-2) 0; border-top: 1px solid var(--sb-border); }
    .det__kv > div:first-child { border-top: none; }
    .det__kv dt { color: var(--sb-text-muted); font-size: var(--sb-body-sm-size); }
    .det__kv dd { margin: 0; font-weight: 600; font-size: var(--sb-body-sm-size); color: var(--sb-text); text-align: right; }

    /* Device */
    .det__device { display: flex; align-items: center; gap: var(--sb-space-3); }
    .det__device-icon { width: 42px; height: 42px; flex-shrink: 0; border-radius: var(--sb-radius-md); background: var(--sb-info-bg); color: var(--sb-info-fg); display: inline-flex; align-items: center; justify-content: center; }
    .det__device-name { font-weight: 700; color: var(--sb-text); }
    .det__device-sub { font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); }

    .det__muted { color: var(--sb-text-muted); font-size: var(--sb-body-md-size); margin: 0; line-height: 1.5; }
    .det__strong { font-weight: 700; }

    /* History */
    .det__hist { margin-top: var(--sb-space-3); }
    .det__hist-empty { padding: var(--sb-space-4) 0; }
    .det__more { margin-top: var(--sb-space-3); }

    /* Enrollments & attendance progress */
    .det__report { background: none; border: none; color: var(--sb-link); font-weight: 700; font-size: var(--sb-body-sm-size); text-decoration: none; cursor: pointer; }
    .det__report:hover { color: var(--sb-link-hover); text-decoration: underline; }
    .det__prog { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--sb-space-4); }
    .det__prog-head { display: flex; justify-content: space-between; gap: var(--sb-space-3); margin-bottom: var(--sb-space-2); font-size: var(--sb-body-sm-size); }
    .det__prog-meta { color: var(--sb-text-muted); white-space: nowrap; }
  `],
})
export class StudentDetailComponent {
  readonly #service = inject(StudentService);
  readonly #auth = inject(AuthStore);
  readonly #toast = inject(ToastService);
  readonly #pendingApprovals = inject(PendingApprovalsStore);

  /** Bound from the `:id` route segment (withComponentInputBinding). */
  readonly id = input.required<string>();

  readonly student = signal<StudentDetail | null>(null);
  readonly loadError = signal<string | null>(null);
  readonly grades = this.#service.grades;

  readonly canApprove = computed(() => this.#auth.hasPermission('StudentsApprove'));
  readonly canReject = computed(() => this.#auth.hasPermission('StudentsReject'));
  readonly canEdit = computed(() => this.#auth.hasPermission('StudentsEdit'));
  readonly canDeactivate = computed(() => this.#auth.hasPermission('StudentsDeactivate'));
  readonly canDeviceClear = computed(() => this.#auth.hasPermission('StudentsDeviceClear'));
  readonly canViewAttendance = computed(() => this.#auth.hasPermission('AttendanceRead'));

  // ID image (loaded on demand)
  readonly idImage = signal<IdImageState | null>(null);

  // History tabs
  readonly activeTab = signal<DetailTab>('logins');
  readonly logins = signal<StudentAuditEntry[]>([]);
  readonly loginsTotal = signal(0);
  readonly loginsPage = signal(0);
  readonly loginsLoading = signal(false);
  readonly activity = signal<StudentAuditEntry[]>([]);
  readonly activityTotal = signal(0);
  readonly activityPage = signal(0);
  readonly activityLoading = signal(false);
  readonly enrollments = signal<StudentEnrollmentDto[]>([]);
  readonly enrollmentsTotal = signal(0);
  readonly enrollmentsPage = signal(0);
  readonly enrollmentsLoading = signal(false);
  readonly enrollmentsHasMore = computed(() => this.enrollments().length < this.enrollmentsTotal());

  // Enrollments & attendance progress card (loaded eagerly with the student, gated by AttendanceRead)
  readonly attendance = signal<StudentAttendanceProgress[]>([]);
  readonly attendanceLoading = signal(false);

  readonly tabs: readonly SbTab[] = [
    { id: 'logins', label: 'Login history' },
    { id: 'enroll', label: 'Enrollments & transactions' },
    { id: 'activity', label: 'Activity' },
  ];

  readonly historyColumns = computed<readonly SbTableColumn[]>(() =>
    this.activeTab() === 'activity'
      ? [
          { key: 'action', header: 'Action' },
          { key: 'detail', header: 'Detail' },
          { key: 'when', header: 'When' },
        ]
      : [
          { key: 'when', header: 'Date & time' },
          { key: 'action', header: 'Action' },
          { key: 'ip', header: 'IP' },
        ],
  );

  readonly currentEntries = computed(() =>
    this.activeTab() === 'activity' ? this.activity() : this.logins(),
  );
  readonly currentLoading = computed(() =>
    this.activeTab() === 'activity' ? this.activityLoading() : this.loginsLoading(),
  );
  readonly currentHasMore = computed(() =>
    this.activeTab() === 'activity'
      ? this.activity().length < this.activityTotal()
      : this.logins().length < this.loginsTotal(),
  );

  // Action dialogs / busy
  readonly deactivateOpen = signal(false);
  readonly rejectOpen = signal(false);
  readonly clearOpen = signal(false);
  readonly contactOpen = signal(false);
  readonly actionBusy = signal(false);
  readonly actionError = signal<string | null>(null);

  readonly auditKey = (row: StudentAuditEntry): string => row.id;

  readonly enrollmentColumns: readonly SbTableColumn[] = [
    { key: 'session', header: 'Session' },
    { key: 'method', header: 'Method' },
    { key: 'amount', header: 'Amount', align: 'right' },
    { key: 'when', header: 'When' },
  ];
  readonly enrollmentKey = (row: StudentEnrollmentDto): string => row.enrollmentId;

  constructor() {
    effect(() => {
      const id = this.id();
      queueMicrotask(() => void this.#load(id));
    });
  }

  async #load(id: string): Promise<void> {
    this.loadError.set(null);
    this.student.set(null);
    this.idImage.set(null);
    this.#resetHistory();
    try {
      const s = await this.#service.getById(id);
      this.student.set(s);
      void this.#service.loadGrades();
      void this.#loadLogins(true);
      if (this.canViewAttendance()) void this.#loadAttendance();
    } catch {
      this.loadError.set('Could not load this student. It may not exist or you may not have access.');
    }
  }

  #resetHistory(): void {
    this.activeTab.set('logins');
    this.logins.set([]);
    this.loginsTotal.set(0);
    this.loginsPage.set(0);
    this.activity.set([]);
    this.activityTotal.set(0);
    this.activityPage.set(0);
    this.enrollments.set([]);
    this.enrollmentsTotal.set(0);
    this.enrollmentsPage.set(0);
    this.attendance.set([]);
  }

  onTabChange(id: string): void {
    const tab = id as DetailTab;
    this.activeTab.set(tab);
    if (tab === 'logins' && this.loginsPage() === 0) void this.#loadLogins(true);
    if (tab === 'activity' && this.activityPage() === 0) void this.#loadActivity(true);
    if (tab === 'enroll' && this.enrollmentsPage() === 0) void this.#loadEnrollments(true);
  }

  loadMore(): void {
    if (this.activeTab() === 'activity') void this.#loadActivity(false);
    else void this.#loadLogins(false);
  }

  loadMoreEnrollments(): void {
    void this.#loadEnrollments(false);
  }

  async #loadEnrollments(reset: boolean): Promise<void> {
    const page = reset ? 1 : this.enrollmentsPage() + 1;
    this.enrollmentsLoading.set(true);
    try {
      const res = await this.#service.listEnrollments(this.id(), page, 20);
      this.enrollments.update((cur) => (reset ? res.items : [...cur, ...res.items]));
      this.enrollmentsTotal.set(res.total);
      this.enrollmentsPage.set(page);
    } catch {
      /* leave what we have */
    } finally {
      this.enrollmentsLoading.set(false);
    }
  }

  async #loadAttendance(): Promise<void> {
    this.attendanceLoading.set(true);
    try {
      const res = await this.#service.listAttendance(this.id(), 1, 50);
      this.attendance.set(res.items);
    } catch {
      /* leave empty — the card shows its empty state */
    } finally {
      this.attendanceLoading.set(false);
    }
  }

  async #loadLogins(reset: boolean): Promise<void> {
    const page = reset ? 1 : this.loginsPage() + 1;
    this.loginsLoading.set(true);
    try {
      const res = await this.#service.listLoginHistory(this.id(), page, 20);
      this.logins.update((cur) => (reset ? res.items : [...cur, ...res.items]));
      this.loginsTotal.set(res.total);
      this.loginsPage.set(page);
    } catch {
      /* leave what we have */
    } finally {
      this.loginsLoading.set(false);
    }
  }

  async #loadActivity(reset: boolean): Promise<void> {
    const page = reset ? 1 : this.activityPage() + 1;
    this.activityLoading.set(true);
    try {
      const res = await this.#service.listActivity(this.id(), page, 20);
      this.activity.update((cur) => (reset ? res.items : [...cur, ...res.items]));
      this.activityTotal.set(res.total);
      this.activityPage.set(page);
    } catch {
      /* leave what we have */
    } finally {
      this.activityLoading.set(false);
    }
  }

  #refreshActivity(): void {
    if (this.activityPage() > 0) {
      this.activityPage.set(0);
      void this.#loadActivity(true);
    }
  }

  // ── ID image ─────────────────────────────────────────────────────────────────
  async loadIdImage(): Promise<void> {
    if (!this.student()?.hasIdImage) return;
    const current = this.idImage();
    if (current?.loading || current?.url) return;
    this.idImage.set({ loading: true, url: null, error: false });
    try {
      const result = await this.#service.getIdImageUrl(this.id());
      this.idImage.set({ loading: false, url: result.url, error: false });
    } catch {
      this.idImage.set({ loading: false, url: null, error: true });
      this.#toast.error('Could not load the ID image.');
    }
  }

  // ── Lifecycle actions ─────────────────────────────────────────────────────────
  async approve(): Promise<void> {
    await this.#mutate(
      () => this.#service.approve(this.id()),
      'Student approved & sign-in enabled',
      'success',
      () => this.#pendingApprovals.refresh(),
    );
  }

  async reactivate(): Promise<void> {
    await this.#mutate(() => this.#service.setActive(this.id(), true), 'Student reactivated');
  }

  deactivate(): void {
    void this.#mutate(
      () => this.#service.setActive(this.id(), false),
      'Student deactivated',
      'info',
      () => this.deactivateOpen.set(false),
    );
  }

  openReject(): void {
    this.actionError.set(null);
    this.rejectOpen.set(true);
  }

  reject(reason: string): void {
    void this.#mutate(
      () => this.#service.reject(this.id(), reason),
      'Student rejected',
      'info',
      () => {
        this.rejectOpen.set(false);
        void this.#pendingApprovals.refresh();
      },
    );
  }

  openClear(): void {
    this.actionError.set(null);
    this.clearOpen.set(true);
  }

  clearDevice(reason: string): void {
    void this.#mutate(
      () => this.#service.clearDevice(this.id(), reason),
      'Bound device cleared',
      'success',
      () => this.clearOpen.set(false),
    );
  }

  openContact(): void {
    this.actionError.set(null);
    this.contactOpen.set(true);
  }

  saveContact(payload: UpdateStudentContactRequest): void {
    void this.#mutate(
      () => this.#service.updateContact(this.id(), payload),
      'Contact details saved',
      'success',
      () => this.contactOpen.set(false),
    );
  }

  /** Runs a mutation that returns the refreshed detail, updates state, toasts, and refreshes activity. */
  async #mutate(
    op: () => Promise<StudentDetail>,
    successMsg: string,
    variant: 'success' | 'info' = 'success',
    onSuccess?: () => void,
  ): Promise<void> {
    this.actionBusy.set(true);
    this.actionError.set(null);
    try {
      this.student.set(await op());
      onSuccess?.();
      this.#toast.show(successMsg, variant);
      this.#refreshActivity();
    } catch {
      const message = this.#service.error() ?? 'Something went wrong. Please try again.';
      this.actionError.set(message);
      this.#toast.error(message);
    } finally {
      this.actionBusy.set(false);
    }
  }

  // Presentation helpers
  initials = studentInitials;
  pillFor = statusPill;
  dotFor = statusDot;
  subjectFor = avatarSubject;
  at = dateTime;
  methodPill = methodPill;
  amount = amount;

  watchPct(p: StudentAttendanceProgress): number {
    return p.videosTotal > 0 ? Math.round((p.videosWatched / p.videosTotal) * 100) : 0;
  }
  quizVariant(p: StudentAttendanceProgress): 'success' | 'warning' {
    return (p.bestQuizPercent ?? 0) >= 60 ? 'success' : 'warning';
  }
  quizLabel(p: StudentAttendanceProgress): string {
    return p.bestQuizPercent !== null ? `${p.bestQuizPercent}%` : '—';
  }

  action(value: string): string {
    const spaced = value.replace(/[._]/g, ' ').replace(/([a-z])([A-Z])/g, '$1 $2').trim();
    return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
  }
}
