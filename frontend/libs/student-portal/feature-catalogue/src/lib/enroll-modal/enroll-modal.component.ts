import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonComponent, ModalComponent } from '@sb/shared/ui';
import { CatalogueService, Enrollment } from '@sb/student-portal/data-access';
import { CodeInputComponent } from '../code-input/code-input.component';

/**
 * The guided enroll-by-code modal (`FR-STU-CAT-003/004/005`, the prototype's Enroll modal). A
 * `confirm`-size DS `Modal` wrapping the segmented {@link CodeInputComponent} + a redeem button.
 * Opened two ways (the parent supplies the inputs): from a card's **Enroll** CTA (pre-scoped to that
 * session) or from the shell's **Redeem FAB** (blank — redeem any code).
 *
 * On submit it calls `redeem(serial)`. A **`201`** flips to the success state and emits `enrolled`
 * (the parent refreshes the catalogue so the card flips to `Enrolled`/Open). On error it renders the
 * message inline under the input and **keeps the entered serial**: a **`400`** → "Enter a valid
 * code."; a **`409`** → the server's `problem.detail` **verbatim** (the six §B.3 strings are already
 * specific + user-safe). The button shows a spinner + is disabled while in flight.
 */
@Component({
  selector: 'sb-enroll-modal',
  standalone: true,
  imports: [ReactiveFormsModule, ModalComponent, CodeInputComponent, ButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <sb-modal [open]="open()" [title]="title()" size="confirm" (close)="onClose()">
      @if (success()) {
        <div class="enroll enroll--success">
          <div class="enroll__check" aria-hidden="true">
            <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="20 6 9 17 4 12" />
            </svg>
          </div>
          <h3 class="enroll__success-title">You’re enrolled!</h3>
          <p class="enroll__success-text">{{ successText() }}</p>
          <sb-button variant="accent" size="lg" (clicked)="onGoToSession()">Go to session</sb-button>
        </div>
      } @else {
        <div class="enroll">
          <p class="enroll__sub">{{ subText() }}</p>
          <sb-code-input
            [formControl]="serialControl"
            [error]="error()"
            label="Enter your access code"
          />
          <div class="enroll__paste">
            <button type="button" class="enroll__paste-btn" (click)="pasteFromClipboard()">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                   stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <rect x="8" y="2" width="8" height="4" rx="1" />
                <path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2" />
              </svg>
              Paste from clipboard
            </button>
          </div>
          <sb-button
            variant="primary"
            size="lg"
            [loading]="loading()"
            [disabled]="!codeInput()?.complete()"
            (clicked)="submit()"
          >Redeem code</sb-button>
        </div>
      }
    </sb-modal>
  `,
  styles: [`
    .enroll { display: flex; flex-direction: column; gap: var(--sb-space-4); }
    .enroll sb-button { display: block; }
    .enroll ::ng-deep .sb-btn { width: 100%; }
    .enroll__sub { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); line-height: 1.5; }

    .enroll__paste { margin-top: calc(var(--sb-space-2) * -1 + 2px); }
    .enroll__paste-btn {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 4px 8px;
      margin-left: -8px;
      border: none;
      background: transparent;
      color: var(--sb-primary-600);
      font-family: inherit;
      font-size: var(--sb-body-sm-size);
      font-weight: 700;
      cursor: pointer;
      border-radius: var(--sb-radius-sm);
    }
    .enroll__paste-btn:hover { background: var(--sb-primary-50); }
    .enroll__paste-btn:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }

    .enroll--success { text-align: center; gap: var(--sb-space-3); }
    .enroll__check {
      width: 72px;
      height: 72px;
      margin: 0 auto var(--sb-space-2);
      border-radius: 50%;
      background: var(--sb-success-bg);
      color: var(--sb-success);
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .enroll__success-title { margin: 0; font-size: var(--sb-heading-sm-size); font-weight: 800; }
    .enroll__success-text { margin: 0 0 var(--sb-space-2); color: var(--sb-text-muted); font-size: var(--sb-body-md-size); line-height: 1.5; }
  `],
})
export class EnrollModalComponent {
  readonly #catalogue = inject(CatalogueService);

  readonly open = input(false);
  /** Optional session scoping — tailors the copy and the success nav target. */
  readonly sessionId = input<string | null>(null);
  readonly sessionTitle = input<string | null>(null);

  readonly close = output<void>();
  /** Emitted on a successful redeem so the parent refreshes the catalogue (card → Enrolled). */
  readonly enrolled = output<Enrollment>();
  /** "Go to session" — the parent routes to the session detail (S3) for the just-enrolled session. */
  readonly goToSession = output<Enrollment>();

  readonly serialControl = new FormControl('', { nonNullable: true });
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal(false);
  readonly #result = signal<Enrollment | null>(null);

  private readonly codeInput = viewChild(CodeInputComponent);

  readonly title = computed(() => 'Redeem access code');
  readonly subText = computed(() => {
    const t = this.sessionTitle();
    const where = t ? t : 'a new session';
    return `Enrolling in ${where}. The code value must match this session’s price.`;
  });
  readonly successText = computed(() => {
    const t = this.#result()?.sessionTitle ?? this.sessionTitle();
    const where = t ? t : 'a new session';
    return `You’re now enrolled in ${where}. It’s been added to My Sessions with the assignment ready.`;
  });

  constructor() {
    // Reset the form each time the modal (re)opens so a fresh attempt starts clean.
    effect(() => {
      if (this.open()) this.#reset();
    });
  }

  submit(): void {
    if (this.loading()) return;
    const serial = this.serialControl.value.trim();
    this.loading.set(true);
    this.error.set(null);

    this.#catalogue.redeem(serial).subscribe({
      next: (enrollment) => {
        this.loading.set(false);
        this.#result.set(enrollment);
        this.success.set(true);
        this.enrolled.emit(enrollment);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(this.#messageFor(err));
        // Keep the entered serial in the boxes so the student can fix a typo (don't reset).
      },
    });
  }

  onClose(): void {
    this.close.emit();
  }

  /**
   * Reads the serial from the clipboard and distributes it into the boxes (the design's "Paste from
   * clipboard" affordance). Setting the control value runs the CodeInput's `writeValue`, which strips
   * the prefix/dashes and fills the boxes. Silently no-ops if clipboard access is denied/unavailable.
   */
  async pasteFromClipboard(): Promise<void> {
    try {
      const text = await navigator.clipboard?.readText();
      if (text) this.serialControl.setValue(text);
    } catch {
      // Clipboard permission denied or unsupported — the student can still type/paste into the boxes.
    }
  }

  onGoToSession(): void {
    const result = this.#result();
    if (result) this.goToSession.emit(result);
  }

  /** A `400` is a malformed/empty serial (mirror the validator); everything else is the server's. */
  #messageFor(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 400) return 'Enter a valid code.';
      const detail = (err.error as { detail?: string } | null)?.detail;
      if (detail) return detail; // the six §B.3 strings — rendered verbatim
    }
    return 'Something went wrong. Please try again.';
  }

  #reset(): void {
    this.success.set(false);
    this.loading.set(false);
    this.error.set(null);
    this.#result.set(null);
    this.serialControl.reset('');
  }
}
