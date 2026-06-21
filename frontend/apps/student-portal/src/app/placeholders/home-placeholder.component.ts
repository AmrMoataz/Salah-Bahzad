import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { StudentAuthStore } from '@sb/student-portal/data-access';

/**
 * S0 placeholder home rendered inside the shell. A friendly welcome until the real catalogue
 * (S2), sessions (S3), and profile (S6) screens land. The Redeem CTA points at the S0 placeholder.
 */
@Component({
  selector: 'sb-home-placeholder',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="home">
      <div class="home__hero">
        <img class="home__mascot" src="/assets/salah-relaxing.png" alt="" aria-hidden="true" />
        <div>
          <p class="home__greeting">Welcome back{{ firstName() ? ', ' + firstName() : '' }}!</p>
          <p class="home__lede">
            You’re signed in. Your catalogue, sessions, and profile are on their way — for now, redeem
            an access code from your teacher to unlock a session.
          </p>
          <a routerLink="/redeem" class="home__cta">Redeem a code</a>
        </div>
      </div>

      <div class="home__grid">
        <article class="home__card">
          <h2 class="home__card-title">Browse the catalogue</h2>
          <p class="home__card-body">See every session for your grade and track. Coming soon.</p>
          <span class="home__soon">Soon</span>
        </article>
        <article class="home__card">
          <h2 class="home__card-title">My sessions</h2>
          <p class="home__card-body">Pick up where you left off — videos, assignments, quizzes. Coming soon.</p>
          <span class="home__soon">Soon</span>
        </article>
        <article class="home__card">
          <h2 class="home__card-title">Your profile</h2>
          <p class="home__card-body">Manage your details and your bound device. Coming soon.</p>
          <span class="home__soon">Soon</span>
        </article>
      </div>
    </section>
  `,
  styles: [`
    .home { display: flex; flex-direction: column; gap: var(--sb-space-6); }

    .home__hero {
      display: flex;
      align-items: center;
      gap: var(--sb-space-6);
      flex-wrap: wrap;
      background: linear-gradient(135deg, var(--sb-primary-50), var(--sb-accent-50));
      border: 1px solid var(--sb-primary-100);
      border-radius: var(--sb-radius-xl);
      padding: var(--sb-space-7);
    }
    .home__mascot { width: 120px; height: auto; flex-shrink: 0; }
    .home__greeting {
      font-family: var(--sb-font-display);
      font-size: 34px;
      font-weight: 700;
      color: var(--sb-primary-700);
      line-height: 1;
    }
    .home__lede { margin-top: 10px; max-width: 520px; color: var(--sb-neutral-600); line-height: 1.55; }
    .home__cta {
      display: inline-block;
      margin-top: 16px;
      min-height: 44px;
      line-height: 44px;
      padding: 0 22px;
      border-radius: var(--sb-radius-md);
      background: var(--sb-primary);
      color: var(--sb-on-primary);
      font-weight: 700;
      text-decoration: none;
    }
    .home__cta:hover { background: var(--sb-primary-hover); color: var(--sb-on-primary); text-decoration: none; }

    .home__grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: var(--sb-space-4);
    }
    .home__card {
      position: relative;
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-lg);
      padding: var(--sb-space-5);
    }
    .home__card-title { font-size: var(--sb-heading-xs-size); font-weight: 800; margin-bottom: 6px; }
    .home__card-body { color: var(--sb-text-muted); font-size: var(--sb-body-md-size); line-height: 1.5; }
    .home__soon {
      position: absolute;
      top: var(--sb-space-4);
      right: var(--sb-space-4);
      font-size: 10px;
      font-weight: 800;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--sb-text-subtle);
      background: var(--sb-surface-sunken);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-pill);
      padding: 2px 7px;
    }
  `],
})
export class HomePlaceholderComponent {
  readonly #authStore = inject(StudentAuthStore);
  readonly firstName = this.#authStore.firstName;
}
