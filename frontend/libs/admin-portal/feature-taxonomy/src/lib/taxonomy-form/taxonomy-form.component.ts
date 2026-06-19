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
import { Subject, TaxonomyFormValue, TaxonomyKind } from '../data-access/taxonomy.models';

/** Seed values for the form; null means "create" (blank). */
export interface TaxonomyEditing {
  name: string;
  subjectId?: string;
}

/**
 * Create/edit modal for a grade, subject, or specialization (FR-ADM-TAX-001). Specializations get a
 * required parent-subject picker. Emits the payload; the parent persists it.
 */
@Component({
  selector: 'sb-taxonomy-form',
  standalone: true,
  imports: [ReactiveFormsModule, ModalComponent, ButtonComponent, FormFieldComponent, SelectComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <sb-modal [open]="open()" [title]="title()" size="form" (close)="cancel.emit()">
      <form [formGroup]="form" (ngSubmit)="onSubmit()" class="tax-form" novalidate>
        @if (error()) {
          <div class="tax-form__error" role="alert">{{ error() }}</div>
        }

        @if (kind() === 'specialization') {
          <sb-form-field label="Parent subject" fieldId="tax-subject" [error]="subjectError" [required]="true">
            <sb-select
              inputId="tax-subject"
              formControlName="subjectId"
              [options]="subjectOptions()"
              [invalid]="!!subjectError"
              placeholder="Select a subject"
            />
          </sb-form-field>
        }

        <sb-form-field [label]="nameLabel()" fieldId="tax-name" [error]="nameError" [required]="true">
          <input
            id="tax-name"
            type="text"
            formControlName="name"
            class="sb-input"
            autocomplete="off"
            [placeholder]="namePlaceholder()"
          />
        </sb-form-field>
      </form>

      <div modalFooter class="tax-form__actions">
        <sb-button variant="ghost" (clicked)="cancel.emit()">Cancel</sb-button>
        <sb-button variant="primary" [loading]="submitting()" (clicked)="onSubmit()">
          {{ isEdit() ? 'Save changes' : 'Add ' + kind() }}
        </sb-button>
      </div>
    </sb-modal>
  `,
  styles: [`
    .tax-form { display: flex; flex-direction: column; gap: var(--sb-space-4); }

    .tax-form__error {
      background: var(--sb-danger-bg);
      color: var(--sb-danger-fg);
      border: 1px solid var(--sb-danger-border);
      border-radius: var(--sb-radius-md);
      padding: var(--sb-space-2) var(--sb-space-3);
      font-size: var(--sb-body-md-size);
      font-weight: 600;
    }

    .tax-form__actions { display: flex; gap: var(--sb-space-2); justify-content: flex-end; }

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
export class TaxonomyFormComponent {
  readonly #fb = inject(FormBuilder);

  readonly open = input<boolean>(false);
  readonly kind = input.required<TaxonomyKind>();
  readonly editing = input<TaxonomyEditing | null>(null);
  readonly subjects = input<readonly Subject[]>([]);
  readonly submitting = input<boolean>(false);
  readonly error = input<string | null>(null);

  readonly save = output<TaxonomyFormValue>();
  readonly cancel = output<void>();

  readonly isEdit = computed(() => this.editing() !== null);
  readonly title = computed(() => `${this.isEdit() ? 'Edit' : 'Add'} ${this.kind()}`);
  readonly nameLabel = computed(() => `${this.titleCase(this.kind())} name`);
  readonly namePlaceholder = computed(
    () =>
      ({ grade: 'e.g. Grade 10', subject: 'e.g. Math', specialization: 'e.g. Mechanics' })[this.kind()],
  );
  readonly subjectOptions = computed<SelectOption[]>(() =>
    this.subjects().map((s) => ({ value: s.id, label: s.name })),
  );

  readonly form = this.#fb.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    subjectId: [''],
  });

  constructor() {
    // Apply the conditional subject requirement, and re-seed the form each time the modal opens.
    effect(() => {
      const subjectCtrl = this.form.controls.subjectId;
      if (this.kind() === 'specialization') {
        subjectCtrl.addValidators(Validators.required);
      } else {
        subjectCtrl.removeValidators(Validators.required);
      }
      subjectCtrl.updateValueAndValidity({ emitEvent: false });

      if (this.open()) {
        const e = this.editing();
        this.form.reset({
          name: e?.name ?? '',
          subjectId: e?.subjectId ?? this.subjects()[0]?.id ?? '',
        });
      }
    });
  }

  get nameError(): string {
    const c = this.form.controls.name;
    if (!c.touched) return '';
    if (c.hasError('required')) return `A ${this.kind()} name is required.`;
    if (c.hasError('maxlength')) return 'Name is too long.';
    return '';
  }

  get subjectError(): string {
    const c = this.form.controls.subjectId;
    if (!c.touched) return '';
    if (c.hasError('required')) return 'A parent subject is required.';
    return '';
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const payload: TaxonomyFormValue = { name: v.name!.trim() };
    if (this.kind() === 'specialization') payload.subjectId = v.subjectId!;
    this.save.emit(payload);
  }

  private titleCase(value: string): string {
    return value.charAt(0).toUpperCase() + value.slice(1);
  }
}
