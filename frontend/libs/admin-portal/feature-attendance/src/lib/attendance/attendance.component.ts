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
import {
  AvatarComponent,
  ButtonComponent,
  CardComponent,
  ComboboxComponent,
  ProgressComponent,
  SbTableColumn,
  SelectOption,
  TableCellDirective,
  TableComponent,
  TabsComponent,
  ToastService,
} from '@sb/shared/ui';
import { SessionAttendanceRow, StudentAttendanceRow } from '../data-access/attendance.models';
import { AttendanceService } from '../data-access/attendance.service';
import { avatarSubject, initialsOf, percentOrDash, videoPercent } from '../attendance.presentation';

type AttendanceTab = 'session' | 'student';

/**
 * Attendance & reporting (`FR-ADM-ATT-001..004`, mockup `scrAttendance`). Cross-student progress in two
 * views: **By session** (a session combo → cohort matrix of every enrolled student) and **By student**
 * (a student combo → their per-session breakdown). Each row drills into the assignment review
 * (`/review/{enrollmentId}`), and the active view's selection exports to CSV (audited server-side).
 *
 * The **Videos** column still renders `0/total` (fed by the 5C video gate) — a caption flags that it
 * populates once that slice ships; **Quiz best/Attempts** are now live (5B-2). The whole screen is
 * gated on `AttendanceRead`; the server still enforces it (default-deny), so the UI only reflects role.
 */
@Component({
  selector: 'sb-attendance',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    AvatarComponent,
    ButtonComponent,
    CardComponent,
    ComboboxComponent,
    ProgressComponent,
    TableComponent,
    TableCellDirective,
    TabsComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!canRead()) {
      <div class="att__gate">
        <span class="att__gate-icon" aria-hidden="true">
          <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
            <path d="M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2zM7 11V7a5 5 0 0 1 10 0v4"/>
          </svg>
        </span>
        <h3 class="att__gate-title">Access required</h3>
        <p class="att__gate-text">You don’t have permission to view attendance &amp; reporting.</p>
      </div>
    } @else {
      <div class="att__head">
        <div>
          <h1 class="att__title">Attendance &amp; reporting</h1>
          <p class="att__subtitle">Cross-student progress across videos, assignments &amp; quizzes</p>
        </div>
        <div class="att__head-actions">
          <sb-button variant="secondary" [disabled]="!hasSelection()" (clicked)="export()">
            <span class="att__btn-icon" aria-hidden="true" [innerHTML]="downloadIcon"></span>
            Export
          </sb-button>
        </div>
      </div>

      <sb-tabs [tabs]="tabs" [active]="activeTab()" (tabChange)="onTab($event)" />

      @if (activeTab() === 'session') {
        <div class="att__combo">
          <sb-combobox
            [formControl]="sessionControl"
            [options]="sessionOptions()"
            placeholder="Select a session"
            emptyText="No sessions"
          />
        </div>

        <sb-card [title]="sessionTitle()" [padding]="false">
          @if (sessionRows().length === 0) {
            <div class="att__empty">{{ emptyMsg() }}</div>
          } @else {
            <sb-table [columns]="sessionColumns" [rows]="sessionRows()" [rowKey]="byEnrollment">
              <ng-template sbTableCell="student" let-row>
                <span class="att__student">
                  <sb-avatar size="sm" [initials]="initials(row.studentName)" [subject]="subjectFor(row.studentId)" />
                  <span class="att__student-name">{{ row.studentName }}</span>
                </span>
              </ng-template>

              <ng-template sbTableCell="videos" let-row>
                <span class="att__videos">
                  <span class="att__videos-bar"><sb-progress [value]="videoPct(row)" [height]="6" /></span>
                  <span class="att__videos-count">{{ row.videosWatched }}/{{ row.videosTotal }}</span>
                </span>
              </ng-template>

              <ng-template sbTableCell="assignment" let-row>{{ percent(row.assignmentPercent) }}</ng-template>
              <ng-template sbTableCell="quiz" let-row><strong>{{ percent(row.bestQuizPercent) }}</strong></ng-template>
              <ng-template sbTableCell="attempts" let-row>{{ row.quizAttemptCount }}</ng-template>

              <ng-template sbTableCell="act" let-row>
                <sb-button variant="ghost" size="sm" (clicked)="drill(row.enrollmentId)">Drill in</sb-button>
              </ng-template>
            </sb-table>
          }
        </sb-card>
      } @else {
        <div class="att__combo">
          <sb-combobox
            [formControl]="studentControl"
            [options]="studentOptions()"
            placeholder="Select a student"
            emptyText="No students"
          />
        </div>

        <sb-card [title]="studentTitle()" [padding]="false">
          @if (studentRows().length === 0) {
            <div class="att__empty">{{ emptyMsg() }}</div>
          } @else {
            <sb-table [columns]="studentColumns" [rows]="studentRows()" [rowKey]="byEnrollment">
              <ng-template sbTableCell="session" let-row><strong>{{ row.sessionTitle }}</strong></ng-template>
              <ng-template sbTableCell="videos" let-row>{{ row.videosWatched }}/{{ row.videosTotal }}</ng-template>
              <ng-template sbTableCell="quiz" let-row>{{ percent(row.bestQuizPercent) }}</ng-template>

              <ng-template sbTableCell="act" let-row>
                <sb-button variant="ghost" size="sm" (clicked)="drill(row.enrollmentId)">Review</sb-button>
              </ng-template>
            </sb-table>
          }
        </sb-card>
      }

      <p class="att__note">
        Videos watched populates when video tracking ships (5C).
      </p>
    }
  `,
  styles: [`
    :host { display: flex; flex-direction: column; gap: var(--sb-space-4); }

    .att__head { display: flex; align-items: flex-end; justify-content: space-between; gap: var(--sb-space-4); flex-wrap: wrap; }
    .att__title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-xl-size); font-weight: 800; letter-spacing: -0.01em; color: var(--sb-text); }
    .att__subtitle { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
    .att__btn-icon { display: inline-flex; }

    .att__combo { width: 320px; max-width: 100%; }

    .att__student { display: inline-flex; align-items: center; gap: var(--sb-space-3); cursor: default; }
    .att__student-name { font-weight: 700; }

    .att__videos { display: flex; align-items: center; gap: var(--sb-space-2); width: 150px; }
    .att__videos-bar { flex: 1; min-width: 0; }
    .att__videos-count { font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); white-space: nowrap; }

    .att__empty {
      padding: var(--sb-space-10); text-align: center;
      color: var(--sb-text-muted); font-size: var(--sb-body-md-size);
    }

    .att__note { margin: 0; color: var(--sb-text-subtle); font-size: var(--sb-body-sm-size); }

    .att__gate { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-lg); padding: var(--sb-space-10); text-align: center; }
    .att__gate-icon { display: inline-flex; align-items: center; justify-content: center; width: 56px; height: 56px; margin: 0 auto var(--sb-space-3); border-radius: var(--sb-radius-circle); background: var(--sb-warning-bg); color: var(--sb-warning-fg); }
    .att__gate-title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-sm-size); font-weight: 700; color: var(--sb-text); }
    .att__gate-text { margin: 0 auto; max-width: 380px; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
  `],
})
export class AttendanceComponent implements OnInit {
  readonly #service = inject(AttendanceService);
  readonly #auth = inject(AuthStore);
  readonly #router = inject(Router);
  readonly #toast = inject(ToastService);
  readonly #fb = inject(FormBuilder);
  readonly #sanitizer = inject(DomSanitizer);

  readonly canRead = computed(() => this.#auth.hasPermission('AttendanceRead'));

  readonly sessionRows = this.#service.sessionRows;
  readonly studentRows = this.#service.studentRows;
  readonly isLoading = this.#service.isLoading;

  readonly activeTab = signal<AttendanceTab>('session');

  readonly sessionControl = this.#fb.control('', { nonNullable: true });
  readonly studentControl = this.#fb.control('', { nonNullable: true });

  /** Selected ids tracked as signals so the card titles + export button react to combo changes. */
  readonly #sessionId = signal('');
  readonly #studentId = signal('');

  readonly tabs = [
    { id: 'session', label: 'By session' },
    { id: 'student', label: 'By student' },
  ];

  readonly sessionOptions = computed<SelectOption[]>(() =>
    this.#service.sessions().map((s) => ({ value: s.id, label: s.title })),
  );
  readonly studentOptions = computed<SelectOption[]>(() =>
    this.#service.students().map((s) => ({ value: s.id, label: s.name })),
  );

  readonly sessionTitle = computed(() => {
    const s = this.#service.sessions().find((x) => x.id === this.#sessionId());
    return s ? `${s.title} · cohort matrix` : 'Cohort matrix';
  });
  readonly studentTitle = computed(() => {
    const s = this.#service.students().find((x) => x.id === this.#studentId());
    return s ? `${s.name} · per-session breakdown` : 'Per-session breakdown';
  });

  readonly hasSelection = computed(() =>
    this.activeTab() === 'session' ? !!this.#sessionId() : !!this.#studentId(),
  );

  readonly emptyMsg = computed(() => {
    if (this.isLoading()) return 'Loading…';
    if (this.activeTab() === 'session') {
      return this.#sessionId() ? 'No enrolled students for this session yet.' : 'Select a session to view its cohort.';
    }
    return this.#studentId() ? 'No sessions for this student yet.' : 'Select a student to view their breakdown.';
  });

  readonly sessionColumns: readonly SbTableColumn[] = [
    { key: 'student', header: 'Student' },
    { key: 'videos', header: 'Videos watched' },
    { key: 'assignment', header: 'Assignment', align: 'right' },
    { key: 'quiz', header: 'Quiz best', align: 'right' },
    { key: 'attempts', header: 'Attempts', align: 'right' },
    { key: 'act', header: '', align: 'right', width: '1%' },
  ];

  readonly studentColumns: readonly SbTableColumn[] = [
    { key: 'session', header: 'Session' },
    { key: 'videos', header: 'Videos' },
    { key: 'quiz', header: 'Quiz best', align: 'right' },
    { key: 'act', header: '', align: 'right', width: '1%' },
  ];

  /** Both matrices key on the enrollment id (one row per enrollment). */
  readonly byEnrollment = (row: SessionAttendanceRow | StudentAttendanceRow): string => row.enrollmentId;

  readonly downloadIcon: SafeHtml = this.#sanitizer.bypassSecurityTrustHtml(
    '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" ' +
      'stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"/></svg>',
  );

  constructor() {
    this.sessionControl.valueChanges.pipe(takeUntilDestroyed()).subscribe((v) => {
      this.#sessionId.set(v ?? '');
      if (v) void this.#loadSession(v);
    });
    this.studentControl.valueChanges.pipe(takeUntilDestroyed()).subscribe((v) => {
      this.#studentId.set(v ?? '');
      if (v) void this.#loadStudent(v);
    });
  }

  ngOnInit(): void {
    if (!this.canRead()) return;
    void this.#initSessionTab();
  }

  onTab(id: string): void {
    this.activeTab.set(id as AttendanceTab);
    if (id === 'student') void this.#initStudentTab();
  }

  /** Drill-in / Review: navigate to the assignment review for the row's enrollment. */
  drill(enrollmentId: string): void {
    void this.#router.navigate(['/review', enrollmentId]);
  }

  async export(): Promise<void> {
    try {
      if (this.activeTab() === 'session') {
        const id = this.#sessionId();
        if (id) await this.#service.exportSession(id);
      } else {
        const id = this.#studentId();
        if (id) await this.#service.exportStudent(id);
      }
    } catch {
      this.#toast.error('Could not export attendance.');
    }
  }

  // ── Loaders ──────────────────────────────────────────────────────────────────────
  async #initSessionTab(): Promise<void> {
    try {
      await this.#service.loadSessions();
    } catch {
      this.#toast.error('Could not load sessions.');
      return;
    }
    const first = this.#service.sessions()[0];
    if (first && !this.sessionControl.value) this.sessionControl.setValue(first.id);
  }

  async #initStudentTab(): Promise<void> {
    try {
      await this.#service.loadStudents();
    } catch {
      this.#toast.error('Could not load students.');
      return;
    }
    const first = this.#service.students()[0];
    if (first && !this.studentControl.value) this.studentControl.setValue(first.id);
  }

  async #loadSession(sessionId: string): Promise<void> {
    try {
      await this.#service.listBySession(sessionId);
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not load attendance.');
    }
  }

  async #loadStudent(studentId: string): Promise<void> {
    try {
      await this.#service.listByStudent(studentId);
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not load attendance.');
    }
  }

  // ── Presentation helpers (template-facing) ───────────────────────────────────────
  initials = initialsOf;
  subjectFor = avatarSubject;
  percent = percentOrDash;
  videoPct(row: SessionAttendanceRow): number {
    return videoPercent(row.videosWatched, row.videosTotal);
  }
}
