import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { StaffRole } from '@sb/shared/data-access';
import {
  ButtonComponent,
  FormFieldComponent,
  ModalComponent,
  SelectComponent,
  SelectOption,
} from '@sb/shared/ui';
import { CreateStaffRequest, StaffListItem } from '../data-access/staff.models';

/** Create/edit staff modal form (FR-ADM-STAFF-001/002). Emits the payload; the parent persists it. */
@Component({
  selector: 'sb-staff-form',
  standalone: true,
  imports: [ReactiveFormsModule, ModalComponent, ButtonComponent, FormFieldComponent, SelectComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <sb-modal [open]="open()" [title]="title()" size="form" (close)="cancel.emit()">
      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="staff-form" novalidate>
        <p class="staff-form__intro">Assign a role no higher than your own.</p>

        @if (error()) {
          <div class="staff-form__error" role="alert">{{ error() }}</div>
        }

        <sb-form-field label="Full name" fieldId="staff-name" [error]="displayNameError" [required]="true">
          <input
            id="staff-name"
            type="text"
            formControlName="displayName"
            class="sb-input"
            autocomplete="off"
            placeholder="e.g. Hossam Fathy"
          />
        </sb-form-field>

        <sb-form-field label="Email" fieldId="staff-email" [error]="emailError" [required]="true">
          <input
            id="staff-email"
            type="email"
            formControlName="email"
            class="sb-input"
            autocomplete="off"
            placeholder="name@bahzad.edu.eg"
          />
        </sb-form-field>

        <sb-form-field label="Role" fieldId="staff-role" [error]="roleError" [required]="true">
          <sb-select
            inputId="staff-role"
            formControlName="role"
            [options]="roleOptions"
            [invalid]="!!roleError"
            placeholder="Select a role"
          />
        </sb-form-field>
      </form>

      <div modalFooter class="staff-form__actions">
        <sb-button variant="ghost" (clicked)="cancel.emit()">Cancel</sb-button>
        <sb-button variant="primary" [loading]="submitting()" (clicked)="onSubmit()">
          {{ isEdit() ? 'Save changes' : 'Create account' }}
        </sb-button>
      </div>
    </sb-modal>
  `,
  styles: [`
    .staff-form { display: flex; flex-direction: column; gap: var(--sb-space-4); }

    .staff-form__intro { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    .staff-form__error {
      background: var(--sb-danger-bg);
      color: var(--sb-danger-fg);
      border: 1px solid var(--sb-danger-border);
      border-radius: var(--sb-radius-md);
      padding: var(--sb-space-2) var(--sb-space-3);
      font-size: var(--sb-body-md-size);
      font-weight: 600;
    }

    .staff-form__actions { display: flex; gap: var(--sb-space-2); justify-content: flex-end; }

    .sb-input {
      width: 100%;
      height: 40px;
      padding: 0 var(--sb-space-3);
      border: 1px solid var(--sb-border-strong);
      border-radius: var(--sb-radius-md);
      font-size: var(--sb-body-md-size);
      font-family: var(--sb-font-sans);
      color: var(--sb-text);
      background: var(--sb-surface);
      outline: none;
      transition: border-color var(--sb-timing) var(--sb-easing-standard),
                  box-shadow var(--sb-timing) var(--sb-easing-standard);
    }
    .sb-input:focus { border-color: var(--sb-primary); box-shadow: var(--sb-shadow-focus); }
  `],
})
export class StaffFormComponent {
  readonly #fb = inject(FormBuilder);

  readonly open = input<boolean>(false);
  readonly staff = input<StaffListItem | null>(null);
  readonly submitting = input<boolean>(false);
  readonly error = input<string | null>(null);

  readonly save = output<CreateStaffRequest>();
  readonly cancel = output<void>();

  readonly isEdit = computed(() => this.staff() !== null);
  readonly title = computed(() =>
    this.isEdit() ? `Edit ${this.staff()?.displayName ?? ''}` : 'Add staff account',
  );

  readonly roleOptions: SelectOption[] = [
    { value: 'Assistant', label: 'Assistant' },
    { value: 'Teacher', label: 'Teacher' },
  ];

  readonly form = this.#fb.group({
    displayName: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email]],
    role: ['Assistant' as StaffRole, [Validators.required]],
  });

  constructor() {
    // Re-seed the form each time the modal opens (create = blank, edit = current values).
    effect(() => {
      if (this.open()) {
        const s = this.staff();
        this.form.reset({
          displayName: s?.displayName ?? '',
          email: s?.email ?? '',
          role: s?.role ?? 'Assistant',
        });
      }
    });
  }

  get displayNameError(): string {
    const c = this.form.controls.displayName;
    if (!c.touched) return '';
    if (c.hasError('required')) return 'Full name is required.';
    if (c.hasError('maxlength')) return 'Name is too long.';
    return '';
  }

  get emailError(): string {
    const c = this.form.controls.email;
    if (!c.touched) return '';
    if (c.hasError('required')) return 'Email is required.';
    if (c.hasError('email')) return 'Enter a valid email address.';
    return '';
  }

  get roleError(): string {
    const c = this.form.controls.role;
    if (!c.touched) return '';
    if (c.hasError('required')) return 'A role is required.';
    return '';
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.save.emit({ displayName: v.displayName!, email: v.email!, role: v.role! });
  }
}
