import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { AuthStore, StaffRole } from '@sb/shared/data-access';
import {
  AlertComponent,
  AvatarComponent,
  ButtonComponent,
  ButtonVariant,
  ConfirmDialogComponent,
  EmptyStateComponent,
  StatusPillComponent,
} from '@sb/shared/ui';
import { CreateStaffRequest, StaffListItem } from '../data-access/staff.models';
import { StaffService } from '../data-access/staff.service';
import { StaffFormComponent } from '../staff-form/staff-form.component';

interface ConfirmState {
  title: string;
  message: string;
  confirmLabel: string;
  variant: ButtonVariant;
  action: () => Promise<void>;
}

/**
 * Staff & role management screen (FR-ADM-STAFF-001..004, mockup `scrStaff`).
 * Teacher-only; non-teachers see a role-gate. Every mutating control is gated on a granular
 * permission — the server still enforces it (UI hiding is never the only control).
 */
@Component({
  selector: 'sb-staff-list',
  standalone: true,
  imports: [
    ButtonComponent,
    StatusPillComponent,
    AvatarComponent,
    AlertComponent,
    EmptyStateComponent,
    ConfirmDialogComponent,
    StaffFormComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!isTeacher()) {
      <div class="staff__rolegate">
        <span class="staff__rolegate-icon" aria-hidden="true">
          <svg width="26" height="26" viewBox="0 0 20 20" fill="currentColor">
            <path fill-rule="evenodd" d="M10 1a4 4 0 0 0-4 4v2H5a2 2 0 0 0-2 2v7a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-1V5a4 4 0 0 0-4-4zm2 6V5a2 2 0 1 0-4 0v2h4zm-2 4a1.25 1.25 0 0 1 .75 2.25V15a.75.75 0 0 1-1.5 0v-1.75A1.25 1.25 0 0 1 10 11z" clip-rule="evenodd"/>
          </svg>
        </span>
        <h3 class="staff__rolegate-title">Teacher access required</h3>
        <p class="staff__rolegate-text">Staff management is a Teacher-only area.</p>
      </div>
    } @else {
      <div class="staff">
        <!-- Header -->
        <header class="staff__head">
          <div>
            <h1 class="staff__title">Staff</h1>
            <p class="staff__subtitle">{{ rows().length }} accounts · teachers &amp; assistants</p>
          </div>
          @if (canCreate()) {
            <sb-button variant="primary" (clicked)="openCreate()">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                   stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M12 5v14M5 12h14"/>
              </svg>
              Add staff
            </sb-button>
          }
        </header>

        <sb-alert variant="info" title="No escalation">
          You can assign a role no higher than your own. Password reset is delegated to Firebase self-service.
        </sb-alert>

        @if (error()) {
          <sb-alert variant="danger" title="Couldn’t load staff">{{ error() }}</sb-alert>
        }

        @if (rows().length === 0 && !isLoading()) {
          <sb-empty-state
            image="/assets/salah-mascot.png"
            headline="No staff yet"
            description="Add your first teacher or assistant to get started."
          >
            @if (canCreate()) {
              <sb-button variant="primary" (clicked)="openCreate()">Add staff</sb-button>
            }
          </sb-empty-state>
        } @else {
          <div class="staff__table-wrap">
            <table class="staff__table">
              <thead>
                <tr>
                  <th scope="col">Member</th>
                  <th scope="col">Role</th>
                  <th scope="col">Status</th>
                  <th scope="col">Last active</th>
                  <th scope="col" class="staff__col-actions"><span class="sr-only">Actions</span></th>
                </tr>
              </thead>
              <tbody>
                @for (s of rows(); track s.id) {
                  <tr>
                    <td>
                      <div class="staff__member">
                        <sb-avatar
                          [initials]="initials(s.displayName)"
                          [subject]="subjectFor(s.role)"
                          [status]="s.isActive ? 'active' : 'none'"
                        />
                        <div>
                          <div class="staff__name">{{ s.displayName }}</div>
                          <div class="staff__email">{{ s.email }}</div>
                        </div>
                      </div>
                    </td>
                    <td>
                      <sb-status-pill [variant]="s.role === 'Teacher' ? 'info' : 'success'">
                        {{ s.role }}
                      </sb-status-pill>
                    </td>
                    <td>
                      <sb-status-pill [variant]="s.isActive ? 'success' : 'neutral'">
                        {{ s.isActive ? 'Active' : 'Inactive' }}
                      </sb-status-pill>
                    </td>
                    <td class="staff__muted">{{ lastActive(s.lastSeenAtUtc) }}</td>
                    <td>
                      <div class="staff__actions">
                        @if (canEdit()) {
                          <sb-button variant="ghost" size="sm" (clicked)="askResetPassword(s)">
                            Reset password
                          </sb-button>
                          <sb-button variant="secondary" size="sm" (clicked)="openEdit(s)">Edit</sb-button>
                        }
                        @if (canToggle(s)) {
                          <sb-button variant="ghost" size="sm" (clicked)="askToggleActive(s)">
                            {{ s.isActive ? 'Deactivate' : 'Activate' }}
                          </sb-button>
                        }
                        @if (canRemove(s)) {
                          <button
                            type="button"
                            class="staff__icon-btn"
                            [attr.aria-label]="'Remove ' + s.displayName"
                            (click)="askRemove(s)"
                          >
                            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                                 stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                              <path d="M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2m3 0v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6"/>
                            </svg>
                          </button>
                        }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>

      <!-- Create / edit modal -->
      <sb-staff-form
        [open]="formOpen()"
        [staff]="editing()"
        [submitting]="saving()"
        [error]="formError()"
        (save)="onSave($event)"
        (cancel)="closeForm()"
      />

      <!-- Confirmations (reset / deactivate / remove) -->
      @if (confirmState(); as c) {
        <sb-confirm-dialog
          [open]="true"
          [title]="c.title"
          [message]="c.message"
          [confirmLabel]="c.confirmLabel"
          [confirmVariant]="c.variant"
          [busy]="confirmBusy()"
          (confirm)="onConfirm()"
          (cancel)="cancelConfirm()"
        />
      }
    }
  `,
  styles: [`
    .staff { display: flex; flex-direction: column; gap: var(--sb-space-4); }

    .staff__head {
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      gap: var(--sb-space-4);
      flex-wrap: wrap;
    }
    .staff__title {
      margin: 0 0 var(--sb-space-1);
      font-size: var(--sb-heading-xl-size);
      font-weight: 800;
      letter-spacing: -0.01em;
      color: var(--sb-text);
    }
    .staff__subtitle { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    .staff__table-wrap {
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-lg);
      overflow-x: auto;
      background: var(--sb-surface);
    }
    .staff__table { width: 100%; border-collapse: collapse; }
    .staff__table thead th {
      position: sticky;
      top: 0;
      background: var(--sb-surface-sunken);
      text-align: left;
      font-size: var(--sb-body-sm-size);
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.02em;
      color: var(--sb-text-muted);
      padding: var(--sb-space-3) var(--sb-space-4);
      white-space: nowrap;
    }
    .staff__table tbody td {
      padding: var(--sb-space-3) var(--sb-space-4);
      border-top: 1px solid var(--sb-border);
      vertical-align: middle;
    }
    .staff__table tbody tr:hover { background: var(--sb-primary-50); }

    .staff__member { display: flex; align-items: center; gap: var(--sb-space-3); }
    .staff__name { font-weight: 700; font-size: var(--sb-body-md-size); color: var(--sb-text); }
    .staff__email { font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); }
    .staff__muted { color: var(--sb-text-muted); font-size: var(--sb-body-md-size); white-space: nowrap; }

    .staff__col-actions { width: 1%; }
    .staff__actions { display: inline-flex; gap: var(--sb-space-2); justify-content: flex-end; align-items: center; }

    .staff__icon-btn {
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
    .staff__icon-btn:hover { background: var(--sb-primary-50); }
    .staff__icon-btn:focus-visible { box-shadow: var(--sb-shadow-focus); outline: none; }

    /* Role-gate (non-teacher) */
    .staff__rolegate {
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-lg);
      padding: var(--sb-space-10);
      text-align: center;
    }
    .staff__rolegate-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 56px;
      height: 56px;
      margin: 0 auto var(--sb-space-3);
      border-radius: var(--sb-radius-circle);
      background: var(--sb-warning-bg);
      color: var(--sb-warning-fg);
    }
    .staff__rolegate-title {
      margin: 0 0 var(--sb-space-1);
      font-size: var(--sb-heading-sm-size);
      font-weight: 700;
      color: var(--sb-text);
    }
    .staff__rolegate-text {
      margin: 0 auto;
      max-width: 380px;
      color: var(--sb-text-muted);
      font-size: var(--sb-body-md-size);
    }

    .sr-only {
      position: absolute;
      width: 1px; height: 1px;
      padding: 0; margin: -1px;
      overflow: hidden; clip: rect(0,0,0,0);
      white-space: nowrap; border: 0;
    }
  `],
})
export class StaffListComponent implements OnInit {
  readonly #service = inject(StaffService);
  readonly #auth = inject(AuthStore);

  readonly rows = this.#service.staff;
  readonly isLoading = this.#service.isLoading;
  readonly error = this.#service.error;

  readonly isTeacher = computed(() => this.#auth.role() === 'Teacher');
  readonly canCreate = computed(() => this.#auth.hasPermission('StaffCreate'));
  readonly canEdit = computed(() => this.#auth.hasPermission('StaffEdit'));
  readonly canDeactivate = computed(() => this.#auth.hasPermission('StaffDeactivate'));
  readonly canDelete = computed(() => this.#auth.hasPermission('StaffDelete'));

  // Modal / dialog state
  readonly formOpen = signal(false);
  readonly editing = signal<StaffListItem | null>(null);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);

  readonly confirmState = signal<ConfirmState | null>(null);
  readonly confirmBusy = signal(false);

  ngOnInit(): void {
    if (this.isTeacher()) void this.reload();
  }

  async reload(): Promise<void> {
    await this.#service.list({ page: 1, pageSize: 50 });
  }

  // ── Create / edit ────────────────────────────────────────────────
  openCreate(): void {
    this.editing.set(null);
    this.formError.set(null);
    this.formOpen.set(true);
  }

  openEdit(staff: StaffListItem): void {
    this.editing.set(staff);
    this.formError.set(null);
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
  }

  async onSave(value: CreateStaffRequest): Promise<void> {
    this.saving.set(true);
    this.formError.set(null);
    try {
      const editing = this.editing();
      if (editing) {
        await this.#service.update(editing.id, value);
      } else {
        await this.#service.create(value);
      }
      this.formOpen.set(false);
      await this.reload();
    } catch {
      this.formError.set(this.#service.error() ?? 'Could not save the staff member.');
    } finally {
      this.saving.set(false);
    }
  }

  // ── Row actions ──────────────────────────────────────────────────
  askResetPassword(staff: StaffListItem): void {
    this.confirmState.set({
      title: `Reset password for ${staff.displayName}?`,
      message: `A password-reset email will be sent to ${staff.email}.`,
      confirmLabel: 'Send reset email',
      variant: 'primary',
      action: async () => {
        await this.#service.sendPasswordReset(staff.id);
      },
    });
  }

  askToggleActive(staff: StaffListItem): void {
    const deactivating = staff.isActive;
    this.confirmState.set({
      title: deactivating ? `Deactivate ${staff.displayName}?` : `Activate ${staff.displayName}?`,
      message: deactivating
        ? 'They will be blocked from signing in until reactivated. This action is audited.'
        : 'They will be able to sign in again.',
      confirmLabel: deactivating ? 'Deactivate' : 'Activate',
      variant: deactivating ? 'danger' : 'primary',
      action: async () => {
        await this.#service.setActive(staff.id, !staff.isActive);
      },
    });
  }

  askRemove(staff: StaffListItem): void {
    this.confirmState.set({
      title: `Remove ${staff.displayName}?`,
      message: 'The account will be soft-deleted; audit attribution is preserved.',
      confirmLabel: 'Remove',
      variant: 'danger',
      action: async () => {
        await this.#service.remove(staff.id);
      },
    });
  }

  async onConfirm(): Promise<void> {
    const current = this.confirmState();
    if (!current) return;
    this.confirmBusy.set(true);
    try {
      await current.action();
      this.confirmState.set(null);
      await this.reload();
    } finally {
      this.confirmBusy.set(false);
    }
  }

  cancelConfirm(): void {
    this.confirmState.set(null);
  }

  // ── Gating helpers ───────────────────────────────────────────────
  canToggle(staff: StaffListItem): boolean {
    return this.canDeactivate() && !this.isSelf(staff);
  }

  /** Mirrors the mockup: teachers can't be removed, and you can't remove yourself. */
  canRemove(staff: StaffListItem): boolean {
    return this.canDelete() && staff.role !== 'Teacher' && !this.isSelf(staff);
  }

  isSelf(staff: StaffListItem): boolean {
    return this.#auth.staff()?.id === staff.id;
  }

  // ── Presentation ─────────────────────────────────────────────────
  initials(name: string): string {
    return name
      .split(' ')
      .filter(Boolean)
      .map((w) => w[0])
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }

  subjectFor(role: StaffRole): string {
    return role === 'Teacher' ? 'blue' : 'pink';
  }

  /** Relative "last active" label from the last-seen timestamp ('Never' if they've never signed in). */
  lastActive(iso: string | null): string {
    if (!iso) return 'Never';
    const then = new Date(iso).getTime();
    if (Number.isNaN(then)) return 'Never';
    const minutes = Math.floor((Date.now() - then) / 60000);
    if (minutes < 1) return 'Just now';
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    if (days < 7) return `${days}d ago`;
    return new Date(iso).toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  }
}
