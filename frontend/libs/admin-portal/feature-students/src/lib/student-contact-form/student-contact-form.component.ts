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
import {
  ButtonComponent,
  FormFieldComponent,
  ModalComponent,
  SelectComponent,
  SelectOption,
} from '@sb/shared/ui';
import {
  GradeRef,
  StudentDetail,
  UpdateStudentContactRequest,
} from '../data-access/student.models';

/**
 * Edit a student's grade and parent contact numbers (FR-ADM-STU-005). Staff correct enrolment data;
 * identity fields (name, ID image, city/region) are not editable here. Emits the payload; the parent
 * persists it. The edit itself is audited server-side.
 */
@Component({
  selector: 'sb-student-contact-form',
  standalone: true,
  imports: [ReactiveFormsModule, ModalComponent, ButtonComponent, FormFieldComponent, SelectComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <sb-modal [open]="open()" [title]="title()" size="form" (close)="cancel.emit()">
      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="cf" novalidate>
        @if (error()) {
          <div class="cf__error" role="alert">{{ error() }}</div>
        }

        <sb-form-field label="Grade" fieldId="cf-grade" [error]="gradeError" [required]="true">
          <sb-select
            inputId="cf-grade"
            formControlName="gradeId"
            [options]="gradeOptions()"
            [invalid]="!!gradeError"
            placeholder="Select a grade"
          />
        </sb-form-field>

        <sb-form-field label="Phone (student)" fieldId="cf-phone0" [error]="phoneError" [required]="true">
          <input
            id="cf-phone0"
            type="tel"
            formControlName="phoneNumber"
            class="sb-input"
            autocomplete="off"
            inputmode="tel"
            placeholder="e.g. 01012345678"
          />
        </sb-form-field>

        <sb-form-field label="Parent phone (primary)" fieldId="cf-phone1" [error]="phone1Error" [required]="true">
          <input
            id="cf-phone1"
            type="tel"
            formControlName="parentPhonePrimary"
            class="sb-input"
            autocomplete="off"
            inputmode="tel"
            placeholder="e.g. 01012345678"
          />
        </sb-form-field>

        <sb-form-field label="Parent phone (secondary)" fieldId="cf-phone2" [error]="phone2Error" hint="Optional">
          <input
            id="cf-phone2"
            type="tel"
            formControlName="parentPhoneSecondary"
            class="sb-input"
            autocomplete="off"
            inputmode="tel"
            placeholder="Optional second number"
          />
        </sb-form-field>
      </form>

      <div modalFooter class="cf__actions">
        <sb-button variant="ghost" (clicked)="cancel.emit()">Cancel</sb-button>
        <sb-button variant="primary" [loading]="submitting()" (clicked)="onSubmit()">Save changes</sb-button>
      </div>
    </sb-modal>
  `,
  styles: [`
    .cf { display: flex; flex-direction: column; gap: var(--sb-space-4); }

    .cf__error {
      background: var(--sb-danger-bg);
      color: var(--sb-danger-fg);
      border: 1px solid var(--sb-danger-border);
      border-radius: var(--sb-radius-md);
      padding: var(--sb-space-2) var(--sb-space-3);
      font-size: var(--sb-body-md-size);
      font-weight: 600;
    }

    .cf__actions { display: flex; gap: var(--sb-space-2); justify-content: flex-end; }

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
export class StudentContactFormComponent {
  readonly #fb = inject(FormBuilder);

  readonly open = input<boolean>(false);
  readonly student = input<StudentDetail | null>(null);
  readonly grades = input<readonly GradeRef[]>([]);
  readonly submitting = input<boolean>(false);
  readonly error = input<string | null>(null);

  readonly save = output<UpdateStudentContactRequest>();
  readonly cancel = output<void>();

  readonly title = computed(() => `Edit ${this.student()?.fullName ?? 'student'}`);
  readonly gradeOptions = computed<SelectOption[]>(() =>
    this.grades().map((g) => ({ value: g.id, label: g.name })),
  );

  private static readonly PHONE = /^[0-9+()\s-]{6,30}$/;

  readonly form = this.#fb.group({
    gradeId: ['', [Validators.required]],
    phoneNumber: ['', [Validators.required, Validators.pattern(StudentContactFormComponent.PHONE)]],
    parentPhonePrimary: ['', [Validators.required, Validators.pattern(StudentContactFormComponent.PHONE)]],
    parentPhoneSecondary: ['', [Validators.pattern(StudentContactFormComponent.PHONE)]],
  });

  constructor() {
    // Re-seed from the student each time the modal opens.
    effect(() => {
      if (this.open()) {
        const s = this.student();
        this.form.reset({
          gradeId: s?.gradeId ?? '',
          phoneNumber: s?.phoneNumber ?? '',
          parentPhonePrimary: s?.parentPhonePrimary ?? '',
          parentPhoneSecondary: s?.parentPhoneSecondary ?? '',
        });
      }
    });
  }

  get gradeError(): string {
    const c = this.form.controls.gradeId;
    if (!c.touched) return '';
    if (c.hasError('required')) return 'A grade is required.';
    return '';
  }

  get phoneError(): string {
    const c = this.form.controls.phoneNumber;
    if (!c.touched) return '';
    if (c.hasError('required')) return 'A student phone is required.';
    if (c.hasError('pattern')) return 'Enter a valid phone number.';
    return '';
  }

  get phone1Error(): string {
    const c = this.form.controls.parentPhonePrimary;
    if (!c.touched) return '';
    if (c.hasError('required')) return 'A primary parent phone is required.';
    if (c.hasError('pattern')) return 'Enter a valid phone number.';
    return '';
  }

  get phone2Error(): string {
    const c = this.form.controls.parentPhoneSecondary;
    if (!c.touched) return '';
    if (c.hasError('pattern')) return 'Enter a valid phone number.';
    return '';
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const secondary = v.parentPhoneSecondary?.trim();
    this.save.emit({
      gradeId: v.gradeId!,
      phoneNumber: v.phoneNumber!.trim(),
      parentPhonePrimary: v.parentPhonePrimary!.trim(),
      parentPhoneSecondary: secondary ? secondary : null,
    });
  }
}
