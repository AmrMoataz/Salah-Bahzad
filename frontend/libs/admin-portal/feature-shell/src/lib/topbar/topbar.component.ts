import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '@sb/shared/data-access';

@Component({
  selector: 'sb-topbar',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="topbar">
      <div class="topbar__left">
        <h1 class="topbar__title">{{ pageTitle() }}</h1>
      </div>
      <div class="topbar__right">
        <!-- Avatar menu (simplified — expand with dropdown in Phase 1) -->
        @if (staff()) {
          <div class="topbar__user">
            <div class="topbar__avatar" [title]="staff()!.displayName" aria-label="User menu">
              {{ initials() }}
            </div>
            <div class="topbar__user-info">
              <span class="topbar__user-name">{{ staff()!.displayName }}</span>
              <span class="topbar__user-role">{{ staff()!.role }}</span>
            </div>
            <button
              class="topbar__signout"
              (click)="signOut()"
              type="button"
              aria-label="Sign out"
            >
              <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
                <path d="M3 3h4.5a.5.5 0 0 1 0 1H3.5v8h4a.5.5 0 0 1 0 1H3a.5.5 0 0 1-.5-.5v-9A.5.5 0 0 1 3 3zm7.354 2.646a.5.5 0 0 0-.708.708L11.293 7.5H6.5a.5.5 0 0 0 0 1h4.793l-1.647 1.646a.5.5 0 0 0 .708.708l2.5-2.5a.5.5 0 0 0 0-.708l-2.5-2.5z"/>
              </svg>
            </button>
          </div>
        }
      </div>
    </header>
  `,
  styles: [`
    .topbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      height: 60px;
      padding: 0 var(--sb-space-6);
      background: var(--sb-surface);
      border-bottom: 1px solid var(--sb-border);
      box-shadow: var(--sb-shadow-xs);
      flex-shrink: 0;
    }

    .topbar__title {
      font-size: var(--sb-text-lg);
      font-weight: var(--sb-weight-bold);
      color: var(--sb-text);
    }

    .topbar__right { display: flex; align-items: center; gap: var(--sb-space-4); }

    .topbar__user {
      display: flex;
      align-items: center;
      gap: var(--sb-space-3);
    }

    .topbar__avatar {
      width: 36px;
      height: 36px;
      border-radius: var(--sb-radius-circle);
      background: var(--sb-primary);
      color: white;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: var(--sb-text-sm);
      font-weight: var(--sb-weight-bold);
      cursor: pointer;
      flex-shrink: 0;
    }

    .topbar__user-info {
      display: flex;
      flex-direction: column;
      gap: 1px;
    }

    .topbar__user-name {
      font-size: var(--sb-text-sm);
      font-weight: var(--sb-weight-semibold);
      color: var(--sb-text);
      line-height: 1.2;
    }

    .topbar__user-role {
      font-size: var(--sb-text-xs);
      color: var(--sb-text-muted);
      line-height: 1.2;
    }

    .topbar__signout {
      background: transparent;
      border: none;
      cursor: pointer;
      color: var(--sb-text-muted);
      padding: var(--sb-space-2);
      border-radius: var(--sb-radius-sm);
      display: flex;
      align-items: center;
      transition: all var(--sb-dur) var(--sb-ease-standard);

      &:hover { background: var(--sb-surface-sunken); color: var(--sb-danger-fg); }
      &:focus-visible { box-shadow: var(--sb-shadow-focus); outline: none; }
    }

    @media (max-width: 640px) {
      .topbar__user-info { display: none; }
    }
  `],
})
export class TopbarComponent {
  readonly #authStore = inject(AuthStore);
  readonly #router = inject(Router);

  readonly pageTitle = input<string>('Dashboard');
  readonly staff = this.#authStore.staff;

  get initials(): () => string {
    return () => {
      const name = this.staff()?.displayName ?? '';
      return name
        .split(' ')
        .map((n) => n[0])
        .slice(0, 2)
        .join('')
        .toUpperCase();
    };
  }

  async signOut(): Promise<void> {
    await this.#authStore.signOut();
  }
}
