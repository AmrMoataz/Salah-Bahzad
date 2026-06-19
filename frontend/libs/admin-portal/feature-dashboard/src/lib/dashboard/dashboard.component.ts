import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { AuthStore } from '@sb/shared/data-access';

/** Inline outline icon (24×24 grid, ~1.8px stroke) matching the design-system iconography. */
const icon = (d: string): string =>
  `<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="${d}"/></svg>`;

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
            <div
              class="kpi-card__icon"
              [style.background]="'var(--sb-subject-' + card.accent + '-bg)'"
              [style.color]="'var(--sb-subject-' + card.accent + '-deep)'"
              aria-hidden="true"
            >
              <span [innerHTML]="iconHtml(card.icon)"></span>
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
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M9 18l6-6-6-6"/>
            </svg>
          </a>
          <a href="/codes" class="action-card">
            <span class="action-card__label">Generate codes</span>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M9 18l6-6-6-6"/>
            </svg>
          </a>
          <a href="/sessions/new" class="action-card">
            <span class="action-card__label">Create session</span>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M9 18l6-6-6-6"/>
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
      font-size: var(--sb-heading-xl-size);
      font-weight: 800;
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
    }

    .kpi-card__value {
      font-size: var(--sb-heading-xl-size);
      font-weight: 800;
      color: var(--sb-text);
      line-height: 1;
      font-variant-numeric: tabular-nums;
    }

    .kpi-card__label {
      font-size: var(--sb-body-md-size);
      color: var(--sb-text-muted);
      margin-top: var(--sb-space-1);
    }

    .dashboard__section-title {
      font-size: var(--sb-body-lg-size);
      font-weight: 700;
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
      font-size: var(--sb-body-md-size);
      font-weight: 600;
      transition: all var(--sb-timing) var(--sb-easing-standard);

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
  readonly #sanitizer = inject(DomSanitizer);
  readonly #iconCache = new Map<string, SafeHtml>();

  readonly staff = this.#authStore.staff;

  // Accent + outline icon mirror the prototype's StatCard treatment (tinted chip + deep icon),
  // using the canonical design-system icon paths (inbox, users, ticket, clipboard).
  readonly placeholderCards = [
    { label: 'Pending approvals', accent: 'mustard', icon: icon('M3 12h5l2 3h4l2-3h5M5 5h14l3 7v6a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2v-6z') },
    { label: 'Active students', accent: 'blue', icon: icon('M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75') },
    { label: 'Active codes', accent: 'green', icon: icon('M3 9a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2 2 2 0 0 0 0 4 2 2 0 0 1-2 2H5a2 2 0 0 1-2-2 2 2 0 0 0 0-4zM9 7v10') },
    { label: 'Enrollments', accent: 'purple', icon: icon('M9 3h6v2H9zM8 4H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2h-2M9 12l2 2 4-4') },
  ];

  /** Bypass the HTML sanitizer for developer-authored constant SVG icon markup (see sidebar). */
  iconHtml(svg: string): SafeHtml {
    let trusted = this.#iconCache.get(svg);
    if (!trusted) {
      trusted = this.#sanitizer.bypassSecurityTrustHtml(svg);
      this.#iconCache.set(svg, trusted);
    }
    return trusted;
  }
}
