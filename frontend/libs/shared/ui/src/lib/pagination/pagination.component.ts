import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

/** Page numbers with prev/next + an optional "X–Y of N" summary (design-system `Pagination`). */
@Component({
  selector: 'sb-pagination',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sb-pager">
      @if (total() != null && pageSize() != null) {
        <span class="sb-pager__summary">{{ rangeStart() }}–{{ rangeEnd() }} of {{ total() }}</span>
      }
      <div class="sb-pager__controls">
        <button
          type="button"
          class="sb-pager__btn"
          [disabled]="page() <= 1"
          aria-label="Previous"
          (click)="go(page() - 1)"
        >‹</button>
        @for (p of pages(); track $index) {
          @if (p === '…') {
            <span class="sb-pager__gap" aria-hidden="true">…</span>
          } @else {
            <button
              type="button"
              class="sb-pager__btn"
              [class.is-active]="p === page()"
              [attr.aria-current]="p === page() ? 'page' : null"
              (click)="go(p)"
            >{{ p }}</button>
          }
        }
        <button
          type="button"
          class="sb-pager__btn"
          [disabled]="page() >= pageCount()"
          aria-label="Next"
          (click)="go(page() + 1)"
        >›</button>
      </div>
    </div>
  `,
  styles: [`
    .sb-pager {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--sb-space-4);
      flex-wrap: wrap;
    }
    .sb-pager__summary { font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); }
    .sb-pager__controls { display: flex; align-items: center; gap: var(--sb-space-1); }

    .sb-pager__btn {
      min-width: 34px;
      height: 34px;
      padding: 0 8px;
      border-radius: var(--sb-radius-md);
      border: 1px solid transparent;
      background: transparent;
      color: var(--sb-text);
      font-family: var(--sb-font-sans);
      font-size: var(--sb-body-md-size);
      font-weight: 700;
      cursor: pointer;
      transition: background var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .sb-pager__btn:hover:not(:disabled):not(.is-active) { background: var(--sb-surface-sunken); }
    .sb-pager__btn:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }
    .sb-pager__btn:disabled { color: var(--sb-text-subtle); cursor: not-allowed; }
    .sb-pager__btn.is-active {
      border-color: var(--sb-primary);
      background: var(--sb-primary);
      color: var(--sb-on-primary);
    }
    .sb-pager__gap { min-width: 24px; text-align: center; color: var(--sb-text-subtle); }
  `],
})
export class PaginationComponent {
  readonly page = input<number>(1);
  readonly pageCount = input<number>(1);
  readonly total = input<number | null>(null);
  readonly pageSize = input<number | null>(null);
  readonly pageChange = output<number>();

  readonly rangeStart = computed(() => (this.page() - 1) * (this.pageSize() ?? 0) + 1);
  readonly rangeEnd = computed(() =>
    Math.min(this.page() * (this.pageSize() ?? 0), this.total() ?? 0),
  );

  /** 1 … window … last (collapses when ≤ 7 pages), matching the DS. */
  readonly pages = computed<(number | '…')[]>(() => {
    const count = this.pageCount();
    const current = this.page();
    const out: (number | '…')[] = [];
    if (count <= 7) {
      for (let i = 1; i <= count; i++) out.push(i);
      return out;
    }
    out.push(1);
    if (current > 3) out.push('…');
    for (let i = Math.max(2, current - 1); i <= Math.min(count - 1, current + 1); i++) out.push(i);
    if (current < count - 2) out.push('…');
    out.push(count);
    return out;
  });

  go(p: number): void {
    if (p >= 1 && p <= this.pageCount() && p !== this.page()) this.pageChange.emit(p);
  }
}
