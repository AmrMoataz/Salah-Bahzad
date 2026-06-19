import { Injectable, signal } from '@angular/core';

export type ToastVariant = 'success' | 'danger' | 'info' | 'warning';

export interface ToastItem {
  id: number;
  variant: ToastVariant;
  title: string;
  message: string;
}

/**
 * App-wide toast queue (design-system `Toast`). Mount a single {@link ToastOutletComponent} once in
 * the shell; features inject this service and call `success()/error()/info()/warning()`.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  /** Auto-dismiss delay, matching the prototype. */
  static readonly TTL_MS = 3200;

  readonly #toasts = signal<ToastItem[]>([]);
  readonly toasts = this.#toasts.asReadonly();

  #seq = 0;
  readonly #timers = new Map<number, ReturnType<typeof setTimeout>>();

  show(message: string, variant: ToastVariant = 'success', title?: string): number {
    const id = ++this.#seq;
    this.#toasts.update((list) => [...list, { id, variant, message, title: title ?? this.#defaultTitle(variant) }]);
    this.#timers.set(id, setTimeout(() => this.dismiss(id), ToastService.TTL_MS));
    return id;
  }

  success(message: string, title?: string): number {
    return this.show(message, 'success', title);
  }
  error(message: string, title?: string): number {
    return this.show(message, 'danger', title);
  }
  info(message: string, title?: string): number {
    return this.show(message, 'info', title);
  }
  warning(message: string, title?: string): number {
    return this.show(message, 'warning', title);
  }

  dismiss(id: number): void {
    const timer = this.#timers.get(id);
    if (timer) {
      clearTimeout(timer);
      this.#timers.delete(id);
    }
    this.#toasts.update((list) => list.filter((t) => t.id !== id));
  }

  #defaultTitle(variant: ToastVariant): string {
    switch (variant) {
      case 'success':
        return 'Done';
      case 'danger':
        return 'Error';
      default:
        return 'Notice';
    }
  }
}
