import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import {
  ButtonComponent,
  ButtonVariant,
  FormFieldComponent,
  ModalComponent,
} from '@sb/shared/ui';

/**
 * Reusable "confirm with a mandatory reason" modal — the reason is required by the server for both
 * rejecting a registration (FR-ADM-STU-004) and clearing a device (FR-PLAT-DEV-004), so the reason is
 * captured into the audit trail. Emits the trimmed reason; the parent persists and closes it.
 */
@Component({
  selector: 'sb-reason-dialog',
  standalone: true,
  imports: [ModalComponent, ButtonComponent, FormFieldComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <sb-modal [open]="open()" [title]="title()" size="form" (close)="cancel.emit()">
      @if (intro()) {
        <p class="reason__intro">{{ intro() }}</p>
      }

      @if (error()) {
        <div class="reason__error" role="alert">{{ error() }}</div>
      }

      <sb-form-field [label]="label()" fieldId="reason-input" [error]="reasonError()" [required]="true">
        <textarea
          id="reason-input"
          class="sb-textarea"
          rows="3"
          autocomplete="off"
          [placeholder]="placeholder()"
          [value]="reason()"
          (input)="onInput($event)"
          (blur)="touched.set(true)"
        ></textarea>
      </sb-form-field>

      <div modalFooter class="reason__actions">
        <sb-button variant="ghost" (clicked)="cancel.emit()">Cancel</sb-button>
        <sb-button [variant]="confirmVariant()" [loading]="busy()" (clicked)="onConfirm()">
          {{ confirmLabel() }}
        </sb-button>
      </div>
    </sb-modal>
  `,
  styles: [`
    .reason__intro { margin: 0 0 var(--sb-space-4); color: var(--sb-text-muted); line-height: 1.5; }

    .reason__error {
      margin-bottom: var(--sb-space-3);
      background: var(--sb-danger-bg);
      color: var(--sb-danger-fg);
      border: 1px solid var(--sb-danger-border);
      border-radius: var(--sb-radius-md);
      padding: var(--sb-space-2) var(--sb-space-3);
      font-size: var(--sb-body-md-size);
      font-weight: 600;
    }

    .reason__actions { display: flex; gap: var(--sb-space-2); justify-content: flex-end; }

    .sb-textarea {
      width: 100%;
      padding: var(--sb-space-2) var(--sb-space-3);
      border: 1px solid var(--sb-border-strong);
      border-radius: var(--sb-radius-md);
      font-size: var(--sb-body-md-size);
      font-family: var(--sb-font-sans);
      color: var(--sb-text);
      background: var(--sb-surface);
      resize: vertical;
      min-height: 76px;
      outline: none;
      transition: border-color var(--sb-timing) var(--sb-easing-standard),
                  box-shadow var(--sb-timing) var(--sb-easing-standard);
    }
    .sb-textarea:focus { border-color: var(--sb-primary); box-shadow: var(--sb-shadow-focus); }
  `],
})
export class ReasonDialogComponent {
  readonly open = input<boolean>(false);
  readonly title = input<string>('Provide a reason');
  readonly intro = input<string>('');
  readonly label = input<string>('Reason');
  readonly placeholder = input<string>('Explain why — this is recorded in the audit log.');
  readonly confirmLabel = input<string>('Confirm');
  readonly confirmVariant = input<ButtonVariant>('danger');
  readonly busy = input<boolean>(false);
  readonly error = input<string | null>(null);

  readonly confirm = output<string>();
  readonly cancel = output<void>();

  readonly reason = signal('');
  readonly touched = signal(false);

  readonly reasonError = computed(() =>
    this.touched() && !this.reason().trim() ? 'A reason is required.' : '',
  );

  constructor() {
    // Reset the field each time the dialog opens.
    effect(() => {
      if (this.open()) {
        this.reason.set('');
        this.touched.set(false);
      }
    });
  }

  onInput(event: Event): void {
    this.reason.set((event.target as HTMLTextAreaElement).value);
  }

  onConfirm(): void {
    const value = this.reason().trim();
    if (!value) {
      this.touched.set(true);
      return;
    }
    this.confirm.emit(value);
  }
}
