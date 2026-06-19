import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ModalComponent } from '../modal/modal.component';
import { ButtonComponent, ButtonVariant } from '../button/button.component';

/** Confirmation dialog built on {@link ModalComponent}; name the object in the message. */
@Component({
  selector: 'sb-confirm-dialog',
  standalone: true,
  imports: [ModalComponent, ButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <sb-modal [open]="open()" [title]="title()" size="confirm" (close)="cancel.emit()">
      <p class="sb-confirm__message">{{ message() }}</p>
      <div modalFooter class="sb-confirm__actions">
        <sb-button variant="ghost" (clicked)="cancel.emit()">{{ cancelLabel() }}</sb-button>
        <sb-button [variant]="confirmVariant()" [loading]="busy()" (clicked)="confirm.emit()">
          {{ confirmLabel() }}
        </sb-button>
      </div>
    </sb-modal>
  `,
  styles: [`
    .sb-confirm__message {
      margin: 0;
      color: var(--sb-text-muted);
      line-height: 1.5;
    }
    .sb-confirm__actions {
      display: flex;
      gap: var(--sb-space-2);
      justify-content: flex-end;
    }
  `],
})
export class ConfirmDialogComponent {
  readonly open = input<boolean>(false);
  readonly title = input<string>('');
  readonly message = input<string>('');
  readonly confirmLabel = input<string>('Confirm');
  readonly cancelLabel = input<string>('Cancel');
  readonly confirmVariant = input<ButtonVariant>('primary');
  readonly busy = input<boolean>(false);
  readonly confirm = output<void>();
  readonly cancel = output<void>();
}
