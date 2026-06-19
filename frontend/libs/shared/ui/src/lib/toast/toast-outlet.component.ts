import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastComponent } from './toast.component';
import { ToastService } from './toast.service';

/** Fixed bottom-right toast stack. Mount once near the app root (the shell). */
@Component({
  selector: 'sb-toast-outlet',
  standalone: true,
  imports: [ToastComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sb-toast-outlet" aria-live="polite" aria-atomic="false">
      @for (toast of toasts(); track toast.id) {
        <div class="sb-toast-outlet__item">
          <sb-toast
            [variant]="toast.variant"
            [title]="toast.title"
            [message]="toast.message"
            (close)="dismiss(toast.id)"
          />
        </div>
      }
    </div>
  `,
  styles: [`
    .sb-toast-outlet {
      position: fixed;
      bottom: var(--sb-space-6);
      right: var(--sb-space-6);
      z-index: var(--sb-z-toast, 1400);
      display: flex;
      flex-direction: column;
      gap: var(--sb-space-2);
      pointer-events: none;
    }
    .sb-toast-outlet__item {
      pointer-events: auto;
      animation: sb-toast-in var(--sb-timing) var(--sb-easing-out);
    }
    @keyframes sb-toast-in {
      from { opacity: 0; transform: translateY(12px); }
      to { opacity: 1; transform: none; }
    }
  `],
})
export class ToastOutletComponent {
  readonly #service = inject(ToastService);
  readonly toasts = this.#service.toasts;

  dismiss(id: number): void {
    this.#service.dismiss(id);
  }
}
