import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  TemplateRef,
  computed,
  contentChildren,
  inject,
  input,
} from '@angular/core';

/** A column header definition. Cell rendering is supplied per-column via <code>*sbTableCell</code>. */
export interface SbTableColumn {
  /** Matches the key used on the <code>[sbTableCell]</code> template; also the default text value source. */
  key: string;
  header: string;
  align?: 'left' | 'right' | 'center';
  /** Optional fixed width (e.g. <code>'1%'</code> to shrink an actions column). */
  width?: string;
}

/* eslint-disable-next-line @typescript-eslint/no-explicit-any */
export interface SbTableCellContext { $implicit: any }

/**
 * Per-column cell template, keyed to a column's <code>key</code>:
 * <pre>&lt;ng-template sbTableCell="name" let-row&gt;&lt;strong&gt;{{ row.name }}&lt;/strong&gt;&lt;/ng-template&gt;</pre>
 * Columns with no matching template fall back to the row's value at <code>key</code>.
 */
@Directive({ selector: 'ng-template[sbTableCell]', standalone: true })
export class TableCellDirective {
  readonly key = input.required<string>({ alias: 'sbTableCell' });
  readonly template = inject<TemplateRef<SbTableCellContext>>(TemplateRef);

  static ngTemplateContextGuard(
    _dir: TableCellDirective,
    _ctx: unknown,
  ): _ctx is SbTableCellContext {
    return true;
  }
}

/**
 * Reusable, column-driven data table with the design-system chrome (bordered card, sticky header,
 * row hover). Generic over the row shape; cells are projected via <code>[sbTableCell]</code> templates.
 */
@Component({
  selector: 'sb-table',
  standalone: true,
  imports: [NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sb-table-wrap">
      <table class="sb-table">
        <thead>
          <tr>
            @for (col of columns(); track col.key) {
              <th
                scope="col"
                [style.text-align]="col.align ?? 'left'"
                [style.width]="col.width ?? null"
              >
                {{ col.header }}
              </th>
            }
          </tr>
        </thead>
        <tbody>
          @for (row of rows(); track keyOf(row, $index)) {
            <tr>
              @for (col of columns(); track col.key) {
                <td [style.text-align]="col.align ?? 'left'">
                  @if (templateFor(col.key); as tpl) {
                    <ng-container [ngTemplateOutlet]="tpl" [ngTemplateOutletContext]="{ $implicit: row }" />
                  } @else {
                    {{ valueFor(row, col.key) }}
                  }
                </td>
              }
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .sb-table-wrap {
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-lg);
      overflow-x: auto;
      background: var(--sb-surface);
    }
    .sb-table { width: 100%; border-collapse: collapse; }

    .sb-table thead th {
      position: sticky;
      top: 0;
      background: var(--sb-surface-sunken);
      text-align: left;
      font-size: var(--sb-body-sm-size);
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.02em;
      color: var(--sb-text-muted);
      padding: var(--sb-space-3) var(--sb-space-4);
      white-space: nowrap;
    }

    .sb-table tbody td {
      padding: var(--sb-space-3) var(--sb-space-4);
      border-top: 1px solid var(--sb-border);
      vertical-align: middle;
      font-size: var(--sb-body-md-size);
      color: var(--sb-text);
    }
    .sb-table tbody tr:hover { background: var(--sb-primary-50); }
  `],
})
export class TableComponent {
  readonly columns = input.required<readonly SbTableColumn[]>();
  readonly rows = input.required<readonly unknown[]>();
  /** Stable row key for change tracking; falls back to the index when not provided. */
  readonly rowKey = input<((row: never, index: number) => string | number) | null>(null);

  // Angular signal queries cannot be declared on ES #private members (NG1053), so use a TS-private field.
  private readonly cells = contentChildren(TableCellDirective);
  readonly #cellMap = computed(() => {
    const map = new Map<string, TemplateRef<SbTableCellContext>>();
    for (const cell of this.cells()) map.set(cell.key(), cell.template);
    return map;
  });

  keyOf(row: unknown, index: number): string | number {
    const fn = this.rowKey();
    return fn ? fn(row as never, index) : index;
  }

  templateFor(key: string): TemplateRef<SbTableCellContext> | null {
    return this.#cellMap().get(key) ?? null;
  }

  valueFor(row: unknown, key: string): unknown {
    return (row as Record<string, unknown>)[key];
  }
}
