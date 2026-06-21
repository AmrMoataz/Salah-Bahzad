import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  computed,
  forwardRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { SelectOption } from '../select/select.component';

/**
 * Searchable single-select (the design's `Combobox`/autocomplete): a text field that filters the
 * options as you type, with keyboard navigation, a clear (×) affordance, click-outside close, and a
 * "no matches" row. Implements ControlValueAccessor so it drops into reactive forms via
 * `formControlName` / `[formControl]`, exactly like {@link SelectComponent}. The bound value is the
 * selected option's `value` (empty string when cleared).
 */
@Component({
  selector: 'sb-combobox',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => ComboboxComponent), multi: true },
  ],
  template: `
    <div
      #field
      class="sb-combo__field"
      [class.sb-combo__field--open]="open()"
      [class.sb-combo__field--invalid]="invalid()"
      [class.sb-combo__field--disabled]="isDisabled()"
    >
      <span class="sb-combo__icon" aria-hidden="true">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
          <path d="M21 21l-4.35-4.35M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16z"/>
        </svg>
      </span>
      <input
        #input
        type="text"
        class="sb-combo__input"
        role="combobox"
        autocomplete="off"
        aria-autocomplete="list"
        [id]="inputId() || null"
        [attr.aria-expanded]="open()"
        [value]="display()"
        [placeholder]="placeholder()"
        [disabled]="isDisabled()"
        (input)="onInput($event)"
        (focus)="onFocus()"
        (keydown)="onKeydown($event)"
      />
      @if (selected() && !isDisabled()) {
        <button
          type="button"
          class="sb-combo__clear"
          aria-label="Clear"
          (mousedown)="$event.preventDefault()"
          (click)="clear()"
        >×</button>
      }
      <span class="sb-combo__chevron" [class.sb-combo__chevron--open]="open()" aria-hidden="true">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
          <path d="M6 9l6 6 6-6"/>
        </svg>
      </span>
    </div>

    @if (open()) {
      <ul
        class="sb-combo__menu"
        role="listbox"
        [style.top.px]="menuPos()?.top"
        [style.left.px]="menuPos()?.left"
        [style.width.px]="menuPos()?.width"
      >
        @if (filtered().length === 0) {
          <li class="sb-combo__empty">{{ emptyText() }}</li>
        }
        @for (option of filtered(); track option.value; let i = $index) {
          <li
            role="option"
            class="sb-combo__option"
            [class.sb-combo__option--active]="i === active() && !option.disabled"
            [class.sb-combo__option--disabled]="option.disabled"
            [attr.aria-selected]="option.value === value()"
            (mouseenter)="active.set(i)"
            (mousedown)="$event.preventDefault()"
            (click)="choose(option)"
          >
            <span class="sb-combo__option-body">
              <span
                class="sb-combo__option-label"
                [class.sb-combo__option-label--selected]="option.value === value()"
              >{{ option.label }}</span>
              @if (option.description) {
                <span class="sb-combo__option-desc">{{ option.description }}</span>
              }
            </span>
            @if (option.value === value()) {
              <span class="sb-combo__check" aria-hidden="true">
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

    .sb-combo__field {
      display: flex;
      align-items: center;
      gap: var(--sb-space-2);
      height: 40px;
      padding: 0 12px;
      border: 1px solid var(--sb-border-strong);
      border-radius: var(--sb-radius-md);
      background: var(--sb-surface);
      transition: border-color var(--sb-timing-fast) var(--sb-easing-standard),
                  box-shadow var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .sb-combo__field--open { border-color: var(--sb-primary); box-shadow: var(--sb-shadow-focus); }
    .sb-combo__field--invalid { border-color: var(--sb-danger); }
    .sb-combo__field--disabled { background: var(--sb-surface-sunken); opacity: 0.55; }

    .sb-combo__icon { display: inline-flex; color: var(--sb-text-subtle); flex-shrink: 0; }

    .sb-combo__input {
      flex: 1;
      min-width: 0;
      border: none;
      outline: none;
      background: transparent;
      font-family: var(--sb-font-sans);
      font-size: var(--sb-body-md-size);
      color: var(--sb-text);
    }
    .sb-combo__input::placeholder { color: var(--sb-text-subtle); }
    .sb-combo__input:disabled { cursor: not-allowed; }

    .sb-combo__clear {
      flex-shrink: 0;
      width: 20px;
      height: 20px;
      border: none;
      border-radius: var(--sb-radius-circle);
      background: var(--sb-neutral-100);
      color: var(--sb-text-muted);
      cursor: pointer;
      font-size: 13px;
      line-height: 1;
      display: inline-flex;
      align-items: center;
      justify-content: center;
    }
    .sb-combo__clear:hover { color: var(--sb-text); }

    .sb-combo__chevron {
      display: inline-flex;
      flex-shrink: 0;
      color: var(--sb-text-muted);
      transition: transform var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .sb-combo__chevron--open { transform: rotate(180deg); }

    .sb-combo__menu {
      position: fixed;
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

    .sb-combo__empty { padding: 10px; text-align: center; font-size: var(--sb-body-md-size); color: var(--sb-text-subtle); }

    .sb-combo__option {
      display: flex;
      align-items: center;
      gap: var(--sb-space-2);
      padding: 8px 10px;
      border-radius: var(--sb-radius-sm);
      cursor: pointer;
      color: var(--sb-text);
    }
    .sb-combo__option--active { background: var(--sb-primary-50); }
    .sb-combo__option--disabled { color: var(--sb-text-subtle); opacity: 0.6; cursor: not-allowed; }

    .sb-combo__option-body { flex: 1; min-width: 0; }
    .sb-combo__option-label { display: block; font-size: var(--sb-body-md-size); font-weight: 400; line-height: 1.3; }
    .sb-combo__option-label--selected { font-weight: 700; }
    .sb-combo__option-desc { display: block; font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); line-height: 1.3; }

    .sb-combo__check { display: inline-flex; flex-shrink: 0; color: var(--sb-primary); }
  `],
})
export class ComboboxComponent implements ControlValueAccessor {
  readonly #host = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly fieldRef = viewChild<ElementRef<HTMLElement>>('field');

  readonly options = input<SelectOption[]>([]);
  readonly placeholder = input('Type to search…');
  readonly emptyText = input('No matches');
  readonly invalid = input(false);
  readonly inputId = input<string>('');
  readonly valueChange = output<string>();

  protected readonly open = signal(false);
  protected readonly query = signal('');
  protected readonly value = signal<string | null>(null);
  protected readonly active = signal(0);
  protected readonly isDisabled = signal(false);

  /**
   * Viewport coordinates for the (fixed-position) menu. Fixed positioning lets the menu escape any
   * ancestor `overflow: hidden` (e.g. cards) and sticky/transform stacking contexts that would
   * otherwise clip it or paint it behind sibling cards. Kept in sync while open (open/type/scroll/resize).
   */
  protected readonly menuPos = signal<{ top: number; left: number; width: number } | null>(null);

  constructor() {
    // Capture phase so scrolls inside any ancestor scroll container (not just the window) reposition.
    const sync = (): void => {
      if (this.open()) this.#reposition();
    };
    window.addEventListener('scroll', sync, true);
    window.addEventListener('resize', sync);
    inject(DestroyRef).onDestroy(() => {
      window.removeEventListener('scroll', sync, true);
      window.removeEventListener('resize', sync);
    });
  }

  #reposition(): void {
    const field = this.fieldRef()?.nativeElement;
    if (!field) return;
    const r = field.getBoundingClientRect();
    this.menuPos.set({ top: r.bottom + 6, left: r.left, width: r.width });
  }

  protected readonly selected = computed(() => {
    const current = this.value();
    return this.options().find((o) => o.value === current) ?? null;
  });

  /** Show the live query while open; otherwise the selected option's label. */
  protected readonly display = computed(() =>
    this.open() ? this.query() : (this.selected()?.label ?? ''),
  );

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLowerCase();
    const all = this.options();
    if (!this.open() || !q) return all;
    return all.filter((o) => o.label.toLowerCase().includes(q));
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
  onFocus(): void {
    if (this.isDisabled()) return;
    this.#reposition();
    this.open.set(true);
    this.active.set(0);
  }

  onInput(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
    this.#reposition();
    this.open.set(true);
    this.active.set(0);
  }

  choose(option: SelectOption): void {
    if (option.disabled) return;
    this.value.set(option.value);
    this.#onChange(option.value);
    this.valueChange.emit(option.value);
    this.#close();
  }

  clear(): void {
    this.value.set('');
    this.#onChange('');
    this.valueChange.emit('');
    this.#close();
  }

  onKeydown(event: KeyboardEvent): void {
    if (this.isDisabled()) return;
    const options = this.filtered();
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        if (!this.open()) this.#reposition();
        this.open.set(true);
        this.active.update((i) => Math.min(options.length - 1, i + 1));
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.active.update((i) => Math.max(0, i - 1));
        break;
      case 'Enter':
        event.preventDefault();
        if (this.open() && options[this.active()]) this.choose(options[this.active()]);
        break;
      case 'Escape':
        if (this.open()) {
          event.preventDefault();
          this.#close();
        }
        break;
      default:
        break;
    }
  }

  #close(): void {
    if (!this.open()) {
      this.query.set('');
      return;
    }
    this.open.set(false);
    this.query.set('');
    this.#onTouched();
  }

  @HostListener('document:mousedown', ['$event'])
  onDocumentMouseDown(event: MouseEvent): void {
    if (this.open() && !this.#host.nativeElement.contains(event.target as Node)) {
      this.#close();
    }
  }
}
