import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { AuthStore, PendingApprovalsStore } from '@sb/shared/data-access';
import {
  AvatarComponent,
  ButtonComponent,
  EmptyStateComponent,
  StatusPillComponent,
  ToastService,
} from '@sb/shared/ui';
import { StudentListItem } from '../data-access/student.models';
import { StudentService } from '../data-access/student.service';
import { ReasonDialogComponent } from '../reason-dialog/reason-dialog.component';
import { avatarSubject, relativeTime, studentInitials } from '../student.presentation';

interface IdImageState {
  loading: boolean;
  url: string | null;
  error: boolean;
}

/**
 * Approvals queue (FR-ADM-STU-003/004, mockup `scrApprovals`). Pending-only triage cards, each led by
 * the student's ID image — loaded on demand via a short-lived signed URL, and audited per view
 * (FR-PLAT-AST-003): one audit row per actual look, never a bulk auto-load. Inline approve + reject.
 */
@Component({
  selector: 'sb-approvals-queue',
  standalone: true,
  imports: [ButtonComponent, AvatarComponent, EmptyStateComponent, StatusPillComponent, ReasonDialogComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!canRead()) {
      <div class="aq__gate">
        <h3 class="aq__gate-title">Access required</h3>
        <p class="aq__gate-text">You don’t have permission to review registrations.</p>
      </div>
    } @else {
      <div class="aq__head">
        <h1 class="aq__title">Approvals queue</h1>
        <p class="aq__subtitle">
          {{ pending().length }} registration{{ pending().length === 1 ? '' : 's' }} awaiting review
        </p>
      </div>

      @if (pending().length === 0 && !isLoading()) {
        <div class="aq__empty-card">
          <sb-empty-state
            image="/assets/salah-relaxing.png"
            headline="All caught up!"
            description="There are no pending registrations to review right now."
          />
        </div>
      } @else {
        <div class="aq__grid">
          @for (st of pending(); track st.id) {
            <article class="aq__card">
              <!-- ID image header (loaded on demand, audited) -->
              <div class="aq__id">
                @if (img(st.id); as state) {
                  @if (state.loading) {
                    <div class="aq__id-msg">Loading secure preview…</div>
                  } @else if (state.url) {
                    <img class="aq__id-img" [src]="state.url" alt="National ID document" />
                    <span class="aq__id-badge">
                      <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                           stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                        <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6"/>
                      </svg>
                      National ID
                    </span>
                  } @else {
                    <button type="button" class="aq__id-btn" (click)="loadId(st)">Retry preview</button>
                  }
                } @else {
                  <button type="button" class="aq__id-btn" (click)="loadId(st)">
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                         stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6"/>
                    </svg>
                    <span>View National ID</span>
                    <span class="aq__id-hint">Opening is audited</span>
                  </button>
                }
                <span class="aq__id-status"><sb-status-pill variant="warning">Pending</sb-status-pill></span>
              </div>

              <!-- Body -->
              <div class="aq__body">
                <div class="aq__person">
                  <sb-avatar size="lg" [initials]="initials(st.fullName)" [subject]="subjectFor(st.id)" status="pending" />
                  <div class="aq__person-id">
                    <h2 class="aq__name">{{ st.fullName }}</h2>
                    <p class="aq__meta">{{ st.gradeName ?? 'No grade' }} · {{ st.cityName ?? '—' }}</p>
                  </div>
                </div>

                <dl class="aq__kv">
                  <div><dt>Phone</dt><dd>{{ st.phoneNumber }}</dd></div>
                  <div><dt>Parent</dt><dd>{{ st.parentPhonePrimary }}</dd></div>
                  <div><dt>School</dt><dd>{{ st.schoolName }}</dd></div>
                  <div><dt>Applied</dt><dd>{{ applied(st.createdAtUtc) }}</dd></div>
                </dl>

                <div class="aq__actions">
                  @if (canApprove()) {
                    <sb-button variant="accent" size="sm" class="aq__approve" (clicked)="approve(st)">Approve</sb-button>
                  }
                  @if (canReject()) {
                    <sb-button variant="danger-ghost" size="sm" (clicked)="askReject(st)">Reject</sb-button>
                  }
                  <button type="button" class="aq__open" [attr.aria-label]="'Open ' + st.fullName" (click)="view(st)">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                         stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                      <path d="M9 18l6-6-6-6"/>
                    </svg>
                  </button>
                </div>
              </div>
            </article>
          }
        </div>
      }

      <sb-reason-dialog
        [open]="rejectTarget() !== null"
        [title]="'Reject ' + (rejectTarget()?.fullName ?? '') + '?'"
        intro="A reason is required. It is stored in history and shown to the student."
        label="Rejection reason"
        placeholder="e.g. ID image unclear — please re-upload a readable photo."
        confirmLabel="Reject registration"
        confirmVariant="danger"
        [busy]="rejectBusy()"
        [error]="rejectError()"
        (confirm)="onReject($event)"
        (cancel)="rejectTarget.set(null)"
      />
    }
  `,
  styles: [`
    :host { display: flex; flex-direction: column; gap: var(--sb-space-4); }

    .aq__head { display: flex; flex-direction: column; gap: var(--sb-space-1); }
    .aq__title { margin: 0; font-size: var(--sb-heading-xl-size); font-weight: 800; letter-spacing: -0.01em; color: var(--sb-text); }
    .aq__subtitle { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    .aq__empty-card { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-lg); padding: var(--sb-space-5); }

    .aq__grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(330px, 1fr)); gap: var(--sb-space-4); }

    .aq__card {
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-lg);
      overflow: hidden;
      box-shadow: var(--sb-shadow-sm);
    }

    /* ID image header */
    .aq__id {
      position: relative;
      height: 120px;
      background: var(--sb-surface-sunken);
      border-bottom: 1px solid var(--sb-border);
      display: flex;
      align-items: center;
      justify-content: center;
      overflow: hidden;
    }
    .aq__id-img { width: 100%; height: 100%; object-fit: cover; }
    .aq__id-msg { color: var(--sb-text-subtle); font-size: var(--sb-body-sm-size); }
    .aq__id-btn {
      display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 4px;
      width: 100%; height: 100%; border: none; background: transparent; cursor: pointer;
      color: var(--sb-text-muted); font-family: var(--sb-font-sans); font-size: var(--sb-body-sm-size); font-weight: 600;
    }
    .aq__id-btn:hover { background: var(--sb-primary-50); color: var(--sb-primary-700); }
    .aq__id-hint { font-size: var(--sb-label-sm-size); color: var(--sb-text-subtle); font-weight: 600; }
    .aq__id-badge {
      position: absolute; bottom: 8px; left: 10px;
      display: inline-flex; align-items: center; gap: 5px;
      font-size: 10.5px; font-weight: 700; color: #fff;
      background: rgba(0,0,0,.55); padding: 3px 8px; border-radius: var(--sb-radius-pill);
    }
    .aq__id-status { position: absolute; top: 10px; right: 10px; }

    /* Body */
    .aq__body { padding: var(--sb-space-4); display: flex; flex-direction: column; gap: var(--sb-space-3); }
    .aq__person { display: flex; align-items: center; gap: var(--sb-space-3); }
    .aq__person-id { min-width: 0; }
    .aq__name { margin: 0; font-size: 15px; font-weight: 800; color: var(--sb-text); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .aq__meta { margin: 2px 0 0; font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); }

    .aq__kv { margin: 0; display: grid; grid-template-columns: 1fr 1fr; gap: 8px 12px; }
    .aq__kv dt { color: var(--sb-text-subtle); font-weight: 600; font-size: 11px; text-transform: uppercase; letter-spacing: 0.04em; }
    .aq__kv dd { margin: 1px 0 0; font-weight: 600; font-size: var(--sb-body-sm-size); color: var(--sb-text); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

    .aq__actions { display: flex; gap: var(--sb-space-2); align-items: center; }
    .aq__approve { flex: 1; }
    .aq__approve ::ng-deep .sb-btn { width: 100%; }
    .aq__open {
      display: inline-flex; align-items: center; justify-content: center;
      width: 32px; height: 32px; flex-shrink: 0;
      border: 1px solid var(--sb-border-strong); border-radius: var(--sb-radius-md);
      background: transparent; color: var(--sb-text); cursor: pointer;
    }
    .aq__open:hover { background: var(--sb-surface-sunken); }
    .aq__open:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }

    .aq__gate { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-lg); padding: var(--sb-space-10); text-align: center; }
    .aq__gate-title { margin: 0 0 var(--sb-space-1); font-size: var(--sb-heading-sm-size); font-weight: 700; color: var(--sb-text); }
    .aq__gate-text { margin: 0 auto; max-width: 380px; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
  `],
})
export class ApprovalsQueueComponent implements OnInit {
  readonly #service = inject(StudentService);
  readonly #auth = inject(AuthStore);
  readonly #router = inject(Router);
  readonly #toast = inject(ToastService);
  readonly #pendingApprovals = inject(PendingApprovalsStore);

  readonly pending = this.#service.students;
  readonly isLoading = this.#service.isLoading;

  readonly canRead = computed(() => this.#auth.hasPermission('StudentsRead'));
  readonly canApprove = computed(() => this.#auth.hasPermission('StudentsApprove'));
  readonly canReject = computed(() => this.#auth.hasPermission('StudentsReject'));

  readonly #idImages = signal<Record<string, IdImageState>>({});

  readonly rejectTarget = signal<StudentListItem | null>(null);
  readonly rejectBusy = signal(false);
  readonly rejectError = signal<string | null>(null);

  ngOnInit(): void {
    if (this.canRead()) void this.reload();
  }

  async reload(): Promise<void> {
    try {
      await this.#service.list({ status: 'Pending', page: 1, pageSize: 100 });
    } catch {
      this.#toast.error(this.#service.error() ?? 'Could not load the queue.');
    }
  }

  img(id: string): IdImageState | undefined {
    return this.#idImages()[id];
  }

  async loadId(student: StudentListItem): Promise<void> {
    const current = this.#idImages()[student.id];
    if (current?.loading || current?.url) return;
    this.#patchImg(student.id, { loading: true, url: null, error: false });
    try {
      const result = await this.#service.getIdImageUrl(student.id);
      this.#patchImg(student.id, { loading: false, url: result.url, error: false });
    } catch {
      this.#patchImg(student.id, { loading: false, url: null, error: true });
      this.#toast.error('Could not load the ID image.');
    }
  }

  #patchImg(id: string, state: IdImageState): void {
    this.#idImages.update((map) => ({ ...map, [id]: state }));
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

  askReject(student: StudentListItem): void {
    this.rejectError.set(null);
    this.rejectTarget.set(student);
  }

  async onReject(reason: string): Promise<void> {
    const target = this.rejectTarget();
    if (!target) return;
    this.rejectBusy.set(true);
    this.rejectError.set(null);
    try {
      await this.#service.reject(target.id, reason);
      this.rejectTarget.set(null);
      this.#toast.info('Student rejected');
      await this.reload();
      void this.#pendingApprovals.refresh();
    } catch {
      this.rejectError.set(this.#service.error() ?? 'Could not reject this student.');
    } finally {
      this.rejectBusy.set(false);
    }
  }

  initials = studentInitials;
  subjectFor = avatarSubject;
  applied = relativeTime;
}
