import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  computed,
  forwardRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

export interface SelectOption {
  value: string;
  label: string;
  description?: string;
  disabled?: boolean;
}

/**
 * Custom popover single-select (the design's `Dropdown`, richer than a native `<select>`):
 * per-option description, keyboard nav, click-outside, and a selected check. Implements
 * ControlValueAccessor so it drops into reactive forms via `formControlName`.
 */
@Component({
  selector: 'sb-select',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => SelectComponent), multi: true },
  ],
  template: `
    <button
      type="button"
      class="sb-select__trigger"
      [id]="inputId() || null"
      [class.sb-select__trigger--open]="open()"
      [class.sb-select__trigger--invalid]="invalid()"
      [disabled]="isDisabled()"
      (click)="toggle()"
      (keydown)="onKeydown($event)"
      aria-haspopup="listbox"
      [attr.aria-expanded]="open()"
    >
      <span class="sb-select__value" [class.sb-select__value--placeholder]="!selected()">
        {{ selected()?.label ?? placeholder() }}
      </span>
      <span class="sb-select__chevron" [class.sb-select__chevron--open]="open()" aria-hidden="true">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
          <path d="M6 9l6 6 6-6"/>
        </svg>
      </span>
    </button>

    @if (open()) {
      <ul class="sb-select__menu" role="listbox">
        @for (option of options(); track option.value; let i = $index) {
          <li
            role="option"
            class="sb-select__option"
            [class.sb-select__option--active]="i === active() && !option.disabled"
            [class.sb-select__option--disabled]="option.disabled"
            [attr.aria-selected]="option.value === value()"
            (mouseenter)="active.set(i)"
            (mousedown)="$event.preventDefault()"
            (click)="choose(option)"
          >
            <span class="sb-select__option-body">
              <span
                class="sb-select__option-label"
                [class.sb-select__option-label--selected]="option.value === value()"
              >{{ option.label }}</span>
              @if (option.description) {
                <span class="sb-select__option-desc">{{ option.description }}</span>
              }
            </span>
            @if (option.value === value()) {
              <span class="sb-select__check" aria-hidden="true">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                     stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M20 6L9 17l-5-5"/>
                </svg>
              </span>
            }
          </li>
        }
      </ul>
    }
  `,
  styles: [`
    :host { position: relative; display: block; }

    .sb-select__trigger {
      display: flex;
      align-items: center;
      gap: var(--sb-space-2);
      width: 100%;
      height: 40px;
      padding: 0 12px;
      text-align: left;
      font-family: var(--sb-font-sans);
      font-size: var(--sb-body-md-size);
      color: var(--sb-text);
      border: 1px solid var(--sb-border-strong);
      border-radius: var(--sb-radius-md);
      background: var(--sb-surface);
      cursor: pointer;
      outline: none;
      transition: border-color var(--sb-timing-fast) var(--sb-easing-standard),
                  box-shadow var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .sb-select__trigger--open { border-color: var(--sb-primary); box-shadow: var(--sb-shadow-focus); }
    .sb-select__trigger--invalid { border-color: var(--sb-danger); }
    .sb-select__trigger:focus-visible { border-color: var(--sb-primary); box-shadow: var(--sb-shadow-focus); }
    .sb-select__trigger:disabled {
      background: var(--sb-surface-sunken);
      cursor: not-allowed;
      opacity: 0.55;
    }

    .sb-select__value {
      flex: 1;
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .sb-select__value--placeholder { color: var(--sb-text-subtle); }

    .sb-select__chevron {
      display: inline-flex;
      color: var(--sb-text-muted);
      transition: transform var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .sb-select__chevron--open { transform: rotate(180deg); }

    .sb-select__menu {
      position: absolute;
      top: calc(100% + 6px);
      left: 0;
      right: 0;
      z-index: var(--sb-z-dropdown);
      margin: 0;
      padding: var(--sb-space-1);
      list-style: none;
      max-height: 240px;
      overflow-y: auto;
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-md);
      box-shadow: var(--sb-shadow-lg);
    }

    .sb-select__option {
      display: flex;
      align-items: center;
      gap: var(--sb-space-2);
      padding: 8px 10px;
      border-radius: var(--sb-radius-sm);
      cursor: pointer;
      color: var(--sb-text);
    }
    .sb-select__option--active { background: var(--sb-primary-50); }
    .sb-select__option--disabled { color: var(--sb-text-subtle); opacity: 0.6; cursor: not-allowed; }

    .sb-select__option-body { flex: 1; min-width: 0; }
    .sb-select__option-label {
      display: block;
      font-size: var(--sb-body-md-size);
      font-weight: 400;
      line-height: 1.3;
    }
    .sb-select__option-label--selected { font-weight: 700; }
    .sb-select__option-desc {
      display: block;
      font-size: var(--sb-body-sm-size);
      color: var(--sb-text-muted);
      line-height: 1.3;
    }

    .sb-select__check { display: inline-flex; flex-shrink: 0; color: var(--sb-primary); }
  `],
})
export class SelectComponent implements ControlValueAccessor {
  readonly #host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly options = input<SelectOption[]>([]);
  readonly placeholder = input('Select an option');
  readonly invalid = input(false);
  readonly inputId = input<string>('');
  readonly valueChange = output<string>();

  protected readonly open = signal(false);
  protected readonly value = signal<string | null>(null);
  protected readonly active = signal(-1);
  protected readonly isDisabled = signal(false);

  protected readonly selected = computed(() => {
    const current = this.value();
    return this.options().find((o) => o.value === current) ?? null;
  });

  #onChange: (value: string) => void = () => {};
  #onTouched: () => void = () => {};

  // ── ControlValueAccessor ────────────────────────────────────────
  writeValue(value: string | null): void {
    this.value.set(value ?? null);
  }
  registerOnChange(fn: (value: string) => void): void {
    this.#onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.#onTouched = fn;
  }
  setDisabledState(isDisabled: boolean): void {
    this.isDisabled.set(isDisabled);
  }

  // ── Interaction ─────────────────────────────────────────────────
  toggle(): void {
    if (this.isDisabled()) return;
    const next = !this.open();
    this.open.set(next);
    if (next) this.active.set(this.options().findIndex((o) => o.value === this.value()));
  }

  choose(option: SelectOption): void {
    if (option.disabled) return;
    this.value.set(option.value);
    this.#onChange(option.value);
    this.valueChange.emit(option.value);
    this.close();
  }

  close(): void {
    if (!this.open()) return;
    this.open.set(false);
    this.#onTouched();
  }

  onKeydown(event: KeyboardEvent): void {
    if (this.isDisabled()) return;
    const options = this.options();
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        if (!this.open()) {
          this.toggle();
          return;
        }
        this.active.update((i) => Math.min(options.length - 1, i + 1));
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.active.update((i) => Math.max(0, i - 1));
        break;
      case 'Enter':
        event.preventDefault();
        if (!this.open()) {
          this.toggle();
          return;
        }
        {
          const option = options[this.active()];
          if (option) this.choose(option);
        }
        break;
      case 'Escape':
        if (this.open()) {
          event.preventDefault();
          this.close();
        }
        break;
      default:
        break;
    }
  }

  @HostListener('document:mousedown', ['$event'])
  onDocumentMouseDown(event: MouseEvent): void {
    if (this.open() && !this.#host.nativeElement.contains(event.target as Node)) {
      this.close();
    }
  }
}
