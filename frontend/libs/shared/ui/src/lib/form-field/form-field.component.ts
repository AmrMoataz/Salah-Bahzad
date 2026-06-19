import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'sb-form-field',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sb-field" [class.sb-field--error]="!!error()">
      @if (label()) {
        <label [for]="fieldId()" class="sb-field__label">
          {{ label() }}
          @if (required()) { <span class="sb-field__required" aria-hidden="true">*</span> }
        </label>
      }
      <ng-content />
      @if (hint() && !error()) {
        <span class="sb-field__hint">{{ hint() }}</span>
      }
      @if (error()) {
        <span class="sb-field__error" role="alert" [id]="fieldId() + '-error'">{{ error() }}</span>
      }
    </div>
  `,
  styles: [`
    .sb-field { display: flex; flex-direction: column; gap: 4px; }

    .sb-field__label {
      font-size: var(--sb-body-md-size);
      font-weight: 600;
      color: var(--sb-text);
      user-select: none;
    }

    .sb-field__required { color: var(--sb-danger); margin-left: 2px; }

    .sb-field__hint {
      font-size: var(--sb-body-sm-size);
      color: var(--sb-text-muted);
    }

    .sb-field__error {
      font-size: var(--sb-body-sm-size);
      color: var(--sb-danger-fg);
      font-weight: 600;
    }
  `],
})
export class FormFieldComponent {
  readonly label = input<string>('');
  readonly fieldId = input<string>('');
  readonly hint = input<string>('');
  readonly error = input<string>('');
  readonly required = input<boolean>(false);
}
