import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Subject-tinted chip (design-system `Tag`). Used for subject/specialization labels on the
 * sessions catalogue and detail. Colour comes from the design's subject accent palette
 * (`--sb-subject-{key}-bg` / `-deep`); pass the accent key via <code>subject</code>.
 */
@Component({
  selector: 'sb-tag',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="sb-tag" [style.background]="bg()" [style.color]="fg()">
      <ng-content>{{ label() }}</ng-content>
    </span>
  `,
  styles: [`
    .sb-tag {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      padding: 3px 9px;
      border-radius: var(--sb-radius-pill);
      font-size: var(--sb-label-md-size);
      font-weight: 700;
      line-height: 1.4;
      white-space: nowrap;
      max-width: 100%;
      overflow: hidden;
      text-overflow: ellipsis;
    }
  `],
})
export class TagComponent {
  readonly label = input<string>('');
  /** Subject-accent token key, e.g. 'blue' | 'green' | 'purple' | 'pink' (see _design-tokens.scss). */
  readonly subject = input<string>('blue');

  readonly bg = computed(() => `var(--sb-subject-${this.subject()}-bg)`);
  readonly fg = computed(() => `var(--sb-subject-${this.subject()}-deep)`);
}
