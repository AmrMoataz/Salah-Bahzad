import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthStore } from '@sb/shared/data-access';

/**
 * Phase 0 dashboard shell — KPI cards and activity feed added in Phase 5 (FR-ADM-DASH-001/002/003).
 */
@Component({
  selector: 'sb-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dashboard">
      <div class="dashboard__header">
        <h2 class="dashboard__title">
          Welcome back, {{ staff()?.displayName ?? 'Staff' }}
        </h2>
        <p class="dashboard__subtitle">
          Here's what's happening in your platform today.
        </p>
      </div>

      <!-- Placeholder KPI cards (data wired in Phase 5) -->
      <div class="dashboard__kpis" aria-label="Key metrics">
        @for (card of placeholderCards; track card.label) {
          <div class="kpi-card" [attr.aria-label]="card.label">
            <div class="kpi-card__icon" [style.background]="card.iconBg" aria-hidden="true">
              <span [innerHTML]="card.icon"></span>
            </div>
            <div class="kpi-card__body">
              <div class="kpi-card__value">—</div>
              <div class="kpi-card__label">{{ card.label }}</div>
            </div>
          </div>
        }
      </div>

      <!-- Quick actions (FR-ADM-DASH-003) -->
      <section class="dashboard__actions" aria-labelledby="quick-actions-title">
        <h3 id="quick-actions-title" class="dashboard__section-title">Quick actions</h3>
        <div class="dashboard__action-list">
          <a href="/students?status=pending" class="action-card">
            <span class="action-card__label">Review approvals</span>
            <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
              <path d="M6.22 8.72a.75.75 0 0 0 1.06 1.06l3.25-3.25a.75.75 0 0 0 0-1.06L7.28 2.22a.75.75 0 0 0-1.06 1.06L9.44 6.5 6.22 9.72v-1z"/>
            </svg>
          </a>
          <a href="/codes" class="action-card">
            <span class="action-card__label">Generate codes</span>
            <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
              <path d="M6.22 8.72a.75.75 0 0 0 1.06 1.06l3.25-3.25a.75.75 0 0 0 0-1.06L7.28 2.22a.75.75 0 0 0-1.06 1.06L9.44 6.5 6.22 9.72v-1z"/>
            </svg>
          </a>
          <a href="/sessions/new" class="action-card">
            <span class="action-card__label">Create session</span>
            <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
              <path d="M6.22 8.72a.75.75 0 0 0 1.06 1.06l3.25-3.25a.75.75 0 0 0 0-1.06L7.28 2.22a.75.75 0 0 0-1.06 1.06L9.44 6.5 6.22 9.72v-1z"/>
            </svg>
          </a>
        </div>
      </section>
    </div>
  `,
  styles: [`
    .dashboard {
      max-width: 1280px;
      display: flex;
      flex-direction: column;
      gap: var(--sb-space-8);
    }

    .dashboard__header {}
    .dashboard__title {
      font-size: var(--sb-text-2xl);
      font-weight: var(--sb-weight-extrabold);
      color: var(--sb-text);
      margin-bottom: var(--sb-space-1);
    }
    .dashboard__subtitle { color: var(--sb-text-muted); }

    .dashboard__kpis {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: var(--sb-space-4);
    }

    .kpi-card {
      background: var(--sb-surface);
      border-radius: var(--sb-radius-lg);
      box-shadow: var(--sb-shadow-sm);
      padding: var(--sb-space-6);
      display: flex;
      align-items: flex-start;
      gap: var(--sb-space-4);
    }

    .kpi-card__icon {
      width: 48px;
      height: 48px;
      border-radius: var(--sb-radius-lg);
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      color: white;
    }

    .kpi-card__value {
      font-size: var(--sb-text-2xl);
      font-weight: var(--sb-weight-extrabold);
      color: var(--sb-text);
      line-height: 1;
      font-variant-numeric: tabular-nums;
    }

    .kpi-card__label {
      font-size: var(--sb-text-sm);
      color: var(--sb-text-muted);
      margin-top: var(--sb-space-1);
    }

    .dashboard__section-title {
      font-size: var(--sb-text-base);
      font-weight: var(--sb-weight-bold);
      color: var(--sb-text);
      margin-bottom: var(--sb-space-4);
    }

    .dashboard__action-list {
      display: flex;
      flex-wrap: wrap;
      gap: var(--sb-space-3);
    }

    .action-card {
      display: flex;
      align-items: center;
      gap: var(--sb-space-2);
      padding: var(--sb-space-3) var(--sb-space-4);
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-md);
      text-decoration: none;
      color: var(--sb-text);
      font-size: var(--sb-text-sm);
      font-weight: var(--sb-weight-semibold);
      transition: all var(--sb-dur) var(--sb-ease-standard);

      &:hover {
        background: var(--sb-primary-50);
        border-color: var(--sb-primary);
        color: var(--sb-primary);
        text-decoration: none;
      }

      &:focus-visible { box-shadow: var(--sb-shadow-focus); outline: none; }
    }
  `],
})
export class DashboardComponent {
  readonly #authStore = inject(AuthStore);
  readonly staff = this.#authStore.staff;

  readonly placeholderCards = [
    {
      label: 'Pending approvals',
      iconBg: 'var(--sb-warning)',
      icon: `<svg width="24" height="24" viewBox="0 0 20 20" fill="currentColor">
        <path d="M10 9a3 3 0 1 0 0-6 3 3 0 0 0 0 6zm-7 9a7 7 0 1 1 14 0H3z"/>
      </svg>`,
    },
    {
      label: 'Active students',
      iconBg: 'var(--sb-accent)',
      icon: `<svg width="24" height="24" viewBox="0 0 20 20" fill="currentColor">
        <path d="M9 6a3 3 0 1 1-6 0 3 3 0 0 1 6 0zM17 6a3 3 0 1 1-6 0 3 3 0 0 1 6 0zM12.93 17c.046-.327.07-.66.07-1a6.97 6.97 0 0 0-1.5-4.33A5 5 0 0 1 19 16v1h-6.07zM6 11a5 5 0 0 1 5 5v1H1v-1a5 5 0 0 1 5-5z"/>
      </svg>`,
    },
    {
      label: 'Active codes',
      iconBg: 'var(--sb-primary)',
      icon: `<svg width="24" height="24" viewBox="0 0 20 20" fill="currentColor">
        <path fill-rule="evenodd" d="M17.707 9.293a1 1 0 0 1 0 1.414l-7 7a1 1 0 0 1-1.414 0l-7-7A.997.997 0 0 1 2 10V5a3 3 0 0 1 3-3h5c.256 0 .512.098.707.293l7 7zM5 6a1 1 0 1 0 0-2 1 1 0 0 0 0 2z" clip-rule="evenodd"/>
      </svg>`,
    },
    {
      label: 'Enrollments',
      iconBg: 'var(--sb-purple)',
      icon: `<svg width="24" height="24" viewBox="0 0 20 20" fill="currentColor">
        <path d="M9 2a1 1 0 0 0 0 2h2a1 1 0 0 0 0-2H9z"/>
        <path fill-rule="evenodd" d="M4 5a2 2 0 0 1 2-2 3 3 0 0 0 3 3h2a3 3 0 0 0 3-3 2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V5z" clip-rule="evenodd"/>
      </svg>`,
    },
  ];
}
