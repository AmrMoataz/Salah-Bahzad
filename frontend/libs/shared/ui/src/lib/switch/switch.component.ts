import {
  ChangeDetectionStrategy,
  Component,
  forwardRef,
  input,
  output,
  signal,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/**
 * Toggle switch (design-system `Switch`): a track + knob with an optional trailing label.
 * Implements ControlValueAccessor so it drops into reactive forms via `formControlName`,
 * and also emits <code>(checkedChange)</code> for standalone (non-form) use.
 * Accessible: a real <code>role="switch"</code> button with <code>aria-checked</code> and Space/Enter toggle.
 */
@Component({
  selector: 'sb-switch',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => SwitchComponent), multi: true },
  ],
  template: `
    <button
      type="button"
      role="switch"
      class="sb-switch"
      [class.sb-switch--on]="checked()"
      [attr.aria-checked]="checked()"
      [attr.aria-label]="label() || null"
      [disabled]="isDisabled()"
      (click)="toggle()"
    >
      <span class="sb-switch__track"><span class="sb-switch__knob"></span></span>
      @if (label()) {
        <span class="sb-switch__label">{{ label() }}</span>
      }
    </button>
  `,
  styles: [`
    .sb-switch {
      display: inline-flex;
      align-items: center;
      gap: var(--sb-space-3);
      border: none;
      background: none;
      padding: 0;
      cursor: pointer;
      font-family: var(--sb-font-sans);
      color: var(--sb-text);
      outline: none;
    }
    .sb-switch:disabled { cursor: not-allowed; opacity: 0.55; }

    .sb-switch__track {
      position: relative;
      flex-shrink: 0;
      width: 42px;
      height: 24px;
      border-radius: var(--sb-radius-pill);
      background: var(--sb-neutral-300);
      transition: background var(--sb-timing) var(--sb-easing-standard);
    }
    .sb-switch--on .sb-switch__track { background: var(--sb-primary); }
    .sb-switch:focus-visible .sb-switch__track { box-shadow: var(--sb-shadow-focus); }

    .sb-switch__knob {
      position: absolute;
      top: 3px;
      left: 3px;
      width: 18px;
      height: 18px;
      border-radius: var(--sb-radius-circle);
      background: #fff;
      box-shadow: var(--sb-shadow-sm);
      transition: transform var(--sb-timing) var(--sb-easing-standard);
    }
    .sb-switch--on .sb-switch__knob { transform: translateX(18px); }

    .sb-switch__label {
      font-size: var(--sb-body-md-size);
      font-weight: 600;
      line-height: 1.3;
      text-align: left;
    }
  `],
})
export class SwitchComponent implements ControlValueAccessor {
  readonly label = input<string>('');
  readonly checkedChange = output<boolean>();

  protected readonly checked = signal(false);
  protected readonly isDisabled = signal(false);

  #onChange: (value: boolean) => void = () => {};
  #onTouched: () => void = () => {};

  // ── ControlValueAccessor ────────────────────────────────────────
  writeValue(value: boolean): void {
    this.checked.set(!!value);
  }
  registerOnChange(fn: (value: boolean) => void): void {
    this.#onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.#onTouched = fn;
  }
  setDisabledState(isDisabled: boolean): void {
    this.isDisabled.set(isDisabled);
  }

  toggle(): void {
    if (this.isDisabled()) return;
    const next = !this.checked();
    this.checked.set(next);
    this.#onChange(next);
    this.#onTouched();
    this.checkedChange.emit(next);
  }
}
