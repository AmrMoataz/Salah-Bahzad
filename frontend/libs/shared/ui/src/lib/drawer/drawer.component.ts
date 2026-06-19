import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  computed,
  effect,
  input,
  output,
  viewChild,
} from '@angular/core';

/**
 * Edge sheet / side drawer (design-system `Drawer`). Scrim + focus + Esc-to-close, slides in from the
 * right (default) or left. Project the body as default content and any action buttons into the footer:
 *
 * ```html
 * <sb-drawer [open]="open()" title="LaTeX reference" [width]="660" (close)="open.set(false)">
 *   <p>Body…</p>
 *   <div drawerFooter> <sb-button>…</sb-button> </div>
 * </sb-drawer>
 * ```
 */
@Component({
  selector: 'sb-drawer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <div class="sb-drawer__root" [style.--sb-drawer-width.px]="width()">
        <div class="sb-drawer__scrim" (click)="onScrimClick()"></div>
        <div
          #panel
          class="sb-drawer__panel"
          [class.sb-drawer__panel--left]="side() === 'left'"
          role="dialog"
          aria-modal="true"
          [attr.aria-label]="title()"
          tabindex="-1"
        >
          <header class="sb-drawer__header">
            <h3 class="sb-drawer__title">{{ title() }}</h3>
            <button class="sb-drawer__close" type="button" aria-label="Close" (click)="close.emit()">
              <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                   stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M18 6L6 18M6 6l12 12"/>
              </svg>
            </button>
          </header>

          <div class="sb-drawer__body"><ng-content /></div>

          <footer class="sb-drawer__footer"><ng-content select="[drawerFooter]" /></footer>
        </div>
      </div>
    }
  `,
  styles: [`
    .sb-drawer__root {
      position: fixed;
      inset: 0;
      z-index: var(--sb-z-modal);
      font-family: var(--sb-font-sans);
    }

    .sb-drawer__scrim {
      position: absolute;
      inset: 0;
      background: var(--sb-scrim);
      animation: sb-drawer-fade var(--sb-timing-fast) var(--sb-easing-out);
    }

    .sb-drawer__panel {
      position: absolute;
      top: 0;
      bottom: 0;
      right: 0;
      width: 92%;
      max-width: var(--sb-drawer-width, 400px);
      background: var(--sb-surface);
      box-shadow: var(--sb-shadow-lg);
      display: flex;
      flex-direction: column;
      outline: none;
      animation: sb-drawer-in-right var(--sb-timing) var(--sb-easing-out);
    }
    .sb-drawer__panel--left {
      right: auto;
      left: 0;
      animation-name: sb-drawer-in-left;
    }

    .sb-drawer__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--sb-space-4);
      padding: var(--sb-space-4) var(--sb-space-5);
      border-bottom: 1px solid var(--sb-border);
      flex-shrink: 0;
    }
    .sb-drawer__title {
      margin: 0;
      font-size: var(--sb-heading-sm-size);
      font-weight: 700;
      color: var(--sb-text);
    }
    .sb-drawer__close {
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

    .sb-drawer__body {
      flex: 1;
      overflow: auto;
      padding: var(--sb-space-5);
      font-size: var(--sb-body-md-size);
      color: var(--sb-text);
    }

    .sb-drawer__footer {
      display: flex;
      justify-content: flex-end;
      gap: var(--sb-space-3);
      padding: var(--sb-space-4) var(--sb-space-5);
      border-top: 1px solid var(--sb-border);
      flex-shrink: 0;
    }
    .sb-drawer__footer:empty { display: none; }

    @keyframes sb-drawer-fade { from { opacity: 0; } to { opacity: 1; } }
    @keyframes sb-drawer-in-right {
      from { opacity: 0.4; transform: translateX(24px); }
      to { opacity: 1; transform: translateX(0); }
    }
    @keyframes sb-drawer-in-left {
      from { opacity: 0.4; transform: translateX(-24px); }
      to { opacity: 1; transform: translateX(0); }
    }
  `],
})
export class DrawerComponent {
  readonly open = input<boolean>(false);
  readonly title = input<string>('');
  readonly side = input<'left' | 'right'>('right');
  readonly width = input<number>(400);
  readonly closeOnScrim = input<boolean>(true);
  readonly close = output<void>();

  /** Exposed for templates/tests that want the resolved panel width. */
  readonly resolvedWidth = computed(() => this.width());

  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');

  constructor() {
    // Move focus into the panel when it opens (basic focus management, mirrors ModalComponent).
    effect(() => {
      if (this.open()) {
        queueMicrotask(() => this.panel()?.nativeElement.focus());
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
