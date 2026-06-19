import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  effect,
  input,
  output,
  viewChild,
} from '@angular/core';

/**
 * Accessible modal dialog (scrim + focus + Esc-to-close). Project the body as default content and
 * the action buttons into the footer slot:
 *
 * ```html
 * <sb-modal [open]="open()" title="…" size="form" (close)="open.set(false)">
 *   <p>Body…</p>
 *   <div modalFooter> <sb-button>…</sb-button> </div>
 * </sb-modal>
 * ```
 */
@Component({
  selector: 'sb-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div class="sb-modal__scrim" (click)="onScrimClick()">
        <div
          #dialog
          class="sb-modal sb-modal--{{ size() }}"
          role="dialog"
          aria-modal="true"
          [attr.aria-label]="title()"
          tabindex="-1"
          (click)="$event.stopPropagation()"
        >
          <header class="sb-modal__header">
            <h2 class="sb-modal__title">{{ title() }}</h2>
            <button class="sb-modal__close" type="button" aria-label="Close" (click)="close.emit()">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                   stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M18 6L6 18M6 6l12 12"/>
              </svg>
            </button>
          </header>

          <div class="sb-modal__body"><ng-content /></div>

          <footer class="sb-modal__footer"><ng-content select="[modalFooter]" /></footer>
        </div>
      </div>
    }
  `,
  styles: [`
    .sb-modal__scrim {
      position: fixed;
      inset: 0;
      z-index: var(--sb-z-modal);
      background: var(--sb-scrim);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: var(--sb-space-4);
      animation: sb-modal-fade var(--sb-timing-fast) var(--sb-easing-out);
    }

    .sb-modal {
      background: var(--sb-surface);
      border-radius: var(--sb-radius-lg);
      box-shadow: var(--sb-shadow-lg);
      width: 92%;
      max-height: calc(100dvh - var(--sb-space-12));
      display: flex;
      flex-direction: column;
      outline: none;
      animation: sb-modal-pop var(--sb-timing) var(--sb-easing-standard);
    }

    .sb-modal--confirm { max-width: 480px; }
    .sb-modal--form { max-width: 640px; }

    .sb-modal__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--sb-space-4);
      padding: var(--sb-space-5) var(--sb-space-6);
      border-bottom: 1px solid var(--sb-border);
    }

    .sb-modal__title {
      margin: 0;
      font-size: var(--sb-heading-sm-size);
      font-weight: 700;
      color: var(--sb-text);
    }

    .sb-modal__close {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      border: none;
      background: none;
      padding: 0;
      color: var(--sb-text-muted);
      cursor: pointer;
      flex-shrink: 0;
      border-radius: var(--sb-radius-sm);
      transition: color var(--sb-timing) var(--sb-easing-standard);

      &:hover { color: var(--sb-text); }
      &:focus-visible { box-shadow: var(--sb-shadow-focus); outline: none; }
    }

    .sb-modal__body {
      padding: var(--sb-space-6);
      /* visible (not auto) so the Select popover can overflow the modal without being clipped */
      overflow: visible;
    }

    .sb-modal__footer {
      padding: var(--sb-space-4) var(--sb-space-6);
      border-top: 1px solid var(--sb-border);
    }
    .sb-modal__footer:empty { display: none; }

    @keyframes sb-modal-fade { from { opacity: 0; } to { opacity: 1; } }
    @keyframes sb-modal-pop {
      from { opacity: 0; transform: translateY(8px) scale(0.98); }
      to { opacity: 1; transform: translateY(0) scale(1); }
    }
  `],
})
export class ModalComponent {
  readonly open = input<boolean>(false);
  readonly title = input<string>('');
  readonly size = input<'form' | 'confirm'>('form');
  readonly closeOnScrim = input<boolean>(true);
  readonly close = output<void>();

  private readonly dialog = viewChild<ElementRef<HTMLElement>>('dialog');

  constructor() {
    // Move focus into the dialog when it opens (basic focus management).
    effect(() => {
      if (this.open()) {
        queueMicrotask(() => this.dialog()?.nativeElement.focus());
      }
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open()) this.close.emit();
  }

  onScrimClick(): void {
    if (this.closeOnScrim()) this.close.emit();
  }
}
