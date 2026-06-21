import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  forwardRef,
  input,
  signal,
  viewChildren,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/** Matches everything *outside* the serial alphabet (alnum; Crockford base32 is a subset) — stripped on input. */
const NON_SERIAL = /[^A-Z0-9]/g;

/** Module-unique id so the visible label can name the box group (`aria-labelledby`). */
let nextLabelId = 0;

/**
 * Segmented access-code input for the `SB-XXXXX-XXXXX` serial (`FR-STU-CAT-003`, the prototype's
 * Redeem modal). A fixed **`SB`** prefix chip is followed by `groups` wide boxes (default `2`), each
 * holding one `groupLength`-char group (default `5`, matching `CodeSerialGenerator`) and separated by
 * a dash — the clean, full-width box look from the design. Filling a group auto-advances, **Backspace**
 * on an empty box retreats to the previous group, **arrows** cross box boundaries, and **paste**
 * distributes a pasted serial across the boxes (dashes/spaces stripped, an `SB` prefix dropped,
 * upper-cased). It implements `ControlValueAccessor`, so the enroll form binds it like any input; the
 * emitted value is the assembled `SB-AAAAA-BBBBB` serial (empty string until at least one box is filled).
 *
 * a11y (`FR-STU-A11Y-001`): a visible label names the box group (`aria-labelledby`), each box carries
 * its own position label, and paste is reachable from the keyboard (native Ctrl/Cmd+V).
 */
@Component({
  selector: 'sb-code-input',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CodeInputComponent),
      multi: true,
    },
  ],
  template: `
    <div class="ci-field">
      <span class="ci__label" [id]="labelId" (click)="focusFirst()">{{ label() }}</span>

      <div
        class="ci"
        role="group"
        [attr.aria-labelledby]="labelId"
        [class.ci--error]="!!error()"
      >
        <span class="ci__prefix" aria-hidden="true">{{ prefix() }} - </span>

        @for (group of groupIndexes(); track group; let gi = $index) {
          @if (gi > 0) {
            <span class="ci__dash" aria-hidden="true">–</span>
          }
          <input
            #box
            class="ci__box"
            type="text"
            inputmode="text"
            autocapitalize="characters"
            autocomplete="off"
            spellcheck="false"
            [attr.maxlength]="groupLength()"
            [disabled]="disabled()"
            [value]="groupValues()[gi]"
            [attr.aria-label]="'Code group ' + (gi + 1) + ' of ' + groups()"
            [attr.aria-invalid]="!!error() || null"
            (input)="onInput(gi, $event)"
            (keydown)="onKeydown(gi, $event)"
            (paste)="onPaste($event)"
            (focus)="onFocus()"
          />
        }
      </div>

      @if (error()) {
        <p class="ci__msg" role="alert">{{ error() }}</p>
      }
    </div>
  `,
  styles: [`
    .ci-field { display: flex; flex-direction: column; gap: var(--sb-space-2); }
    .ci__label { font-size: var(--sb-body-md-size); font-weight: 600; color: var(--sb-text); }

    /* One line that flex-fills the row — boxes shrink to fit so the SB-XXXXX-XXXXX field never wraps. */
    .ci {
      display: flex;
      align-items: center;
      flex-wrap: nowrap;
      gap: 10px;
      width: 100%;
    }
    .ci__prefix {
      flex-shrink: 0;
      font-family: var(--sb-font-mono);
      font-weight: 800;
      font-size: 16px;
      color: var(--sb-text-muted);
      letter-spacing: 1px;
    }
    .ci__dash { flex-shrink: 0; color: var(--sb-text-subtle); font-weight: 700; }
    .ci__box {
      flex: 1 1 0;
      min-width: 0;
      width: auto;
      height: 52px;
      padding: 0 10px;
      text-align: center;
      font-family: var(--sb-font-mono);
      font-size: 18px;
      font-weight: 700;
      letter-spacing: 2px;
      text-transform: uppercase;
      color: var(--sb-text);
      background: var(--sb-surface);
      border: 1.5px solid var(--sb-border-strong);
      border-radius: var(--sb-radius-md);
      transition: border-color var(--sb-timing-fast) var(--sb-easing-standard),
                  box-shadow var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .ci__box:focus { outline: none; border-color: var(--sb-primary); box-shadow: var(--sb-shadow-focus); }
    .ci__box:disabled { opacity: 0.5; cursor: not-allowed; }
    .ci--error .ci__box { border-color: var(--sb-danger); }
    .ci__msg { margin: 8px 0 0; color: var(--sb-danger-fg); font-size: var(--sb-body-md-size); font-weight: 600; }
  `],
})
export class CodeInputComponent implements ControlValueAccessor {
  /** Fixed serial prefix shown before the boxes and prepended to the emitted value. */
  readonly prefix = input('SB');
  /** Number of dash-separated groups (one box each). */
  readonly groups = input(2);
  /** Characters per group. */
  readonly groupLength = input(5);
  /** Inline error message; also tints the boxes (rendered verbatim — the server's problem.detail). */
  readonly error = input<string | null>(null);
  /** Visible label naming the box group. */
  readonly label = input('Enter your access code');

  readonly disabled = signal(false);
  /** One entry per group, each holding up to `groupLength` normalised characters. */
  readonly groupValues = signal<string[]>([]);

  protected readonly labelId = `sb-code-input-label-${nextLabelId++}`;

  readonly total = () => this.groups() * this.groupLength();
  readonly groupIndexes = () => Array.from({ length: this.groups() }, (_, i) => i);

  private readonly boxes = viewChildren<ElementRef<HTMLInputElement>>('box');

  #onChange: (value: string) => void = () => undefined;
  #onTouched: () => void = () => undefined;

  constructor() {
    // Seed the empty group buffer once the group/length inputs are known.
    this.groupValues.set(Array(this.groups()).fill(''));
  }

  // ── ControlValueAccessor ────────────────────────────────────────────────────
  writeValue(value: string | null): void {
    this.#fill(value ?? '');
  }
  registerOnChange(fn: (value: string) => void): void {
    this.#onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.#onTouched = fn;
  }
  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  // ── Interaction ─────────────────────────────────────────────────────────────
  onInput(index: number, event: Event): void {
    const target = event.target as HTMLInputElement;
    const normalised = this.#normalise(target.value).slice(0, this.groupLength());

    const next = [...this.groupValues()];
    next[index] = normalised;
    this.groupValues.set(next);
    target.value = normalised; // reflect the normalised text back into the box

    this.#emit();
    // A full group hands off to the next box (the auto-advance the design implies).
    if (normalised.length === this.groupLength()) this.#focusBox(index + 1, 'start');
  }

  onKeydown(index: number, event: KeyboardEvent): void {
    const el = event.target as HTMLInputElement;

    if (event.key === 'Backspace') {
      // Empty box → step back to the end of the previous group (then native delete continues there).
      if (el.value === '' && index > 0) {
        event.preventDefault();
        this.#focusBox(index - 1, 'end');
      }
      return;
    }
    if (event.key === 'ArrowLeft' && el.selectionStart === 0 && el.selectionEnd === 0 && index > 0) {
      event.preventDefault();
      this.#focusBox(index - 1, 'end');
    } else if (
      event.key === 'ArrowRight' &&
      el.selectionStart === el.value.length &&
      el.selectionEnd === el.value.length &&
      index < this.groups() - 1
    ) {
      event.preventDefault();
      this.#focusBox(index + 1, 'start');
    }
  }

  onPaste(event: ClipboardEvent): void {
    event.preventDefault();
    const text = event.clipboardData?.getData('text') ?? '';
    this.#fill(text);
    this.#emit();
    // Land focus on the first not-yet-full group (or the last) after a paste.
    const firstIncomplete = this.groupValues().findIndex((g) => g.length < this.groupLength());
    this.#focusBox(firstIncomplete === -1 ? this.groups() - 1 : firstIncomplete, 'end');
  }

  onFocus(): void {
    this.#onTouched();
  }

  /** Focuses the first box (the label's click target). */
  focusFirst(): void {
    this.#focusBox(0, 'end');
  }

  // ── Internals ───────────────────────────────────────────────────────────────
  /** Upper-cases and drops anything outside the alnum serial alphabet. */
  #normalise(value: string): string {
    return value.toUpperCase().replace(NON_SERIAL, '');
  }

  /** Normalises an incoming serial (strip prefix/dashes/spaces, upper-case) into the group boxes. */
  #fill(value: string): void {
    let normalised = this.#normalise(value);
    const prefix = this.prefix().toUpperCase();
    if (prefix && normalised.startsWith(prefix)) {
      normalised = normalised.slice(prefix.length);
    }
    normalised = normalised.slice(0, this.total());

    const groups: string[] = [];
    for (let g = 0; g < this.groups(); g++) {
      groups.push(normalised.slice(g * this.groupLength(), (g + 1) * this.groupLength()));
    }
    this.groupValues.set(groups);
  }

  /** Assembles the dashed serial and notifies the form (empty string until a box is filled). */
  #emit(): void {
    this.#onChange(this.serial());
  }

  /** The assembled `SB-AAAAA-BBBBB` serial, or `''` when no box is filled. */
  serial(): string {
    const groups = this.groupValues();
    if (!groups.some((g) => g.length > 0)) return '';
    return [this.prefix(), ...groups].join('-');
  }

  /** True once every group is full (the enroll button's enable gate). */
  complete(): boolean {
    const groups = this.groupValues();
    return groups.length === this.groups() && groups.every((g) => g.length === this.groupLength());
  }

  #focusBox(index: number, caret: 'start' | 'end' = 'end'): void {
    const boxes = this.boxes();
    const clamped = Math.max(0, Math.min(index, boxes.length - 1));
    const el = boxes[clamped]?.nativeElement;
    if (!el) return;
    el.focus();
    const pos = caret === 'start' ? 0 : el.value.length;
    try {
      el.setSelectionRange(pos, pos);
    } catch {
      // Some environments reject setSelectionRange on a freshly-focused input — focus alone is enough.
    }
  }
}
