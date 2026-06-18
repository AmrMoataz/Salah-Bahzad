import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type AvatarSize = 'xs' | 'sm' | 'md' | 'lg' | 'xl';
export type AvatarStatus = 'none' | 'active' | 'inactive' | 'pending';

/** Initials avatar with an optional status dot (matches the design-system Avatar). */
@Component({
  selector: 'sb-avatar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="sb-avatar sb-avatar--{{ size() }}">
      <span class="sb-avatar__circle" [style.background]="bg()" [style.color]="fg()">{{ initials() }}</span>
      @if (status() !== 'none') {
        <span class="sb-avatar__status sb-avatar__status--{{ status() }}" aria-hidden="true"></span>
      }
    </span>
  `,
  styles: [`
    .sb-avatar { position: relative; display: inline-flex; flex-shrink: 0; }

    .sb-avatar__circle {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      border-radius: var(--sb-radius-circle);
      border: 1px solid var(--sb-border);
      font-family: var(--sb-font-ui);
      font-weight: var(--sb-weight-extrabold);
      user-select: none;
      text-transform: uppercase;
    }

    .sb-avatar--xs .sb-avatar__circle { width: 24px; height: 24px; font-size: 10px; }
    .sb-avatar--sm .sb-avatar__circle { width: 32px; height: 32px; font-size: 13px; }
    .sb-avatar--md .sb-avatar__circle { width: 40px; height: 40px; font-size: 16px; }
    .sb-avatar--lg .sb-avatar__circle { width: 48px; height: 48px; font-size: 19px; }
    .sb-avatar--xl .sb-avatar__circle { width: 64px; height: 64px; font-size: 26px; }

    .sb-avatar__status {
      position: absolute;
      right: -1px;
      bottom: -1px;
      width: 28%;
      height: 28%;
      min-width: 8px;
      min-height: 8px;
      border-radius: var(--sb-radius-circle);
      border: 2px solid var(--sb-surface);
    }
    .sb-avatar__status--active { background: var(--sb-success); }
    .sb-avatar__status--inactive { background: var(--sb-neutral-300); }
    .sb-avatar__status--pending { background: var(--sb-warning); }
  `],
})
export class AvatarComponent {
  readonly initials = input<string>('');
  readonly size = input<AvatarSize>('md');
  /** Subject-accent token key, e.g. 'blue' | 'green' | 'purple' | 'pink' (see tokens.css). */
  readonly subject = input<string>('blue');
  readonly status = input<AvatarStatus>('none');

  readonly bg = computed(() => `var(--sb-subject-${this.subject()}-bg)`);
  readonly fg = computed(() => `var(--sb-subject-${this.subject()}-deep)`);
}
