import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * S0 placeholder for the Redeem flow (the enroll modal + code input ship in S2). The shell's centre
 * FAB and the sidebar Redeem button both route here for now.
 */
@Component({
  selector: 'sb-redeem-placeholder',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="redeem">
      <div class="redeem__icon" aria-hidden="true">
        <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
          <path d="M3 9a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2 2 2 0 0 0 0 4 2 2 0 0 1-2 2H5a2 2 0 0 1-2-2 2 2 0 0 0 0-4z" />
          <path d="M9 7v10" />
        </svg>
      </div>
      <h1 class="redeem__title">Redeem an access code</h1>
      <p class="redeem__body">
        Enter a code from your teacher to unlock a session. The redeem flow opens here soon — for now,
        keep your code handy.
      </p>
      <a routerLink="/" class="redeem__btn">Back to home</a>
    </section>
  `,
  styles: [`
    .redeem {
      max-width: 460px;
      margin: 0 auto;
      text-align: center;
      padding: var(--sb-space-12) 0;
    }
    .redeem__icon {
      width: 64px;
      height: 64px;
      margin: 0 auto var(--sb-space-4);
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: var(--sb-radius-xl);
      background: var(--sb-primary-50);
      color: var(--sb-primary-600);
    }
    .redeem__title { font-size: var(--sb-heading-lg-size); font-weight: 800; margin-bottom: var(--sb-space-3); }
    .redeem__body { color: var(--sb-neutral-600); line-height: 1.55; margin-bottom: var(--sb-space-6); }
    .redeem__btn {
      display: inline-block;
      min-height: 44px;
      line-height: 44px;
      padding: 0 22px;
      border-radius: var(--sb-radius-md);
      background: var(--sb-primary);
      color: var(--sb-on-primary);
      font-weight: 700;
      text-decoration: none;
    }
    .redeem__btn:hover { background: var(--sb-primary-hover); color: var(--sb-on-primary); text-decoration: none; }
  `],
})
export class RedeemPlaceholderComponent {}
