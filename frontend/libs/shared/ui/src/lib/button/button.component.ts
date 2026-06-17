import {
  ChangeDetectionStrategy,
  Component,
  input,
  output,
} from '@angular/core';
import { CommonModule } from '@angular/common';

export type ButtonVariant = 'primary' | 'accent' | 'secondary' | 'ghost' | 'danger' | 'danger-ghost';
export type ButtonSize = 'sm' | 'md' | 'lg';

@Component({
  selector: 'sb-button',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      [type]="type()"
      [disabled]="disabled() || loading()"
      [attr.aria-label]="ariaLabel() || null"
      [attr.aria-busy]="loading() || null"
      (click)="clicked.emit($event)"
      class="sb-btn"
      [class]="'sb-btn--' + variant() + ' sb-btn--' + size()"
      [class.sb-btn--loading]="loading()"
    >
      @if (loading()) {
        <span class="sb-btn__spinner" aria-hidden="true"></span>
      }
      <ng-content />
    </button>
  `,
  styles: [`
    :host { display: inline-flex; }

    .sb-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: var(--sb-space-2);
      border: none;
      border-radius: var(--sb-radius-md);
      font-family: var(--sb-font-ui);
      font-weight: var(--sb-weight-bold);
      cursor: pointer;
      transition: background-color var(--sb-dur) var(--sb-ease-standard),
                  box-shadow var(--sb-dur) var(--sb-ease-standard),
                  transform var(--sb-dur-fast) var(--sb-ease-standard);
      white-space: nowrap;

      &:active:not(:disabled) { transform: translateY(1px); }
      &:focus-visible { box-shadow: var(--sb-shadow-focus); outline: none; }
      &:disabled { opacity: 0.45; cursor: not-allowed; }
    }

    /* Sizes */
    .sb-btn--sm { height: 32px; padding: 0 12px; font-size: var(--sb-text-sm); }
    .sb-btn--md { height: 40px; padding: 0 16px; font-size: var(--sb-text-sm); }
    .sb-btn--lg { height: 48px; padding: 0 20px; font-size: var(--sb-text-base); }

    /* Variants */
    .sb-btn--primary {
      background: var(--sb-primary);
      color: var(--sb-on-primary);
      &:hover:not(:disabled) { background: var(--sb-primary-hover); }
      &:active:not(:disabled) { background: var(--sb-primary-active); }
    }
    .sb-btn--accent {
      background: var(--sb-accent);
      color: #fff;
      &:hover:not(:disabled) { background: var(--sb-accent-700); }
    }
    .sb-btn--secondary {
      background: transparent;
      color: var(--sb-text);
      border: 1px solid var(--sb-border-strong);
      &:hover:not(:disabled) { background: var(--sb-surface-sunken); }
    }
    .sb-btn--ghost {
      background: transparent;
      color: var(--sb-text);
      &:hover:not(:disabled) { background: var(--sb-surface-sunken); }
    }
    .sb-btn--danger {
      background: var(--sb-danger);
      color: #fff;
      &:hover:not(:disabled) { background: #C7322B; }
    }
    .sb-btn--danger-ghost {
      background: transparent;
      color: var(--sb-danger-fg);
      &:hover:not(:disabled) { background: var(--sb-danger-bg); }
    }

    /* Spinner */
    .sb-btn__spinner {
      width: 16px;
      height: 16px;
      border: 2px solid rgba(255,255,255,0.4);
      border-top-color: currentColor;
      border-radius: 50%;
      animation: sb-spin 0.7s linear infinite;
    }
    @keyframes sb-spin { to { transform: rotate(360deg); } }
  `],
})
export class ButtonComponent {
  readonly variant = input<ButtonVariant>('primary');
  readonly size = input<ButtonSize>('md');
  readonly type = input<'button' | 'submit' | 'reset'>('button');
  readonly disabled = input<boolean>(false);
  readonly loading = input<boolean>(false);
  readonly ariaLabel = input<string>('');

  readonly clicked = output<MouseEvent>();
}
