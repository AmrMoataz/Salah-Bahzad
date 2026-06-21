import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * S0 placeholder for the registration wizard (the full Step-1/Step-2 flow ships in S1). Keeps the
 * "Create an account" link from the login screen resolvable and on-brand.
 */
@Component({
  selector: 'sb-student-register-placeholder',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="reg">
      <div class="reg__card">
        <img class="reg__mascot" src="/assets/salah-mascot.png" alt="" aria-hidden="true" />
        <h1 class="reg__title">Create your account</h1>
        <p class="reg__body">
          Sign-up is coming soon. Create your account, then your teacher approves it — once you’re in,
          redeem a code and start learning.
        </p>
        <a routerLink="/login" class="reg__btn">Back to sign in</a>
      </div>
    </div>
  `,
  styles: [`
    .reg {
      min-height: 100dvh;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 32px 22px;
      background: var(--sb-neutral-25);
    }
    .reg__card { width: 100%; max-width: 420px; text-align: center; }
    .reg__mascot { width: 140px; height: auto; margin: 0 auto 10px; display: block; }
    .reg__title { font-size: 24px; font-weight: 800; letter-spacing: -0.3px; margin: 0 0 12px; }
    .reg__body { color: var(--sb-neutral-600); font-size: 15px; line-height: 1.55; margin: 0 0 20px; }
    .reg__btn {
      display: inline-block;
      min-height: 48px;
      line-height: 48px;
      padding: 0 28px;
      border-radius: var(--sb-radius-md);
      background: var(--sb-primary);
      color: var(--sb-on-primary);
      font-weight: 700;
      text-decoration: none;
    }
    .reg__btn:hover { background: var(--sb-primary-hover); text-decoration: none; color: var(--sb-on-primary); }
    .reg__btn:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }
  `],
})
export class RegisterPlaceholderComponent {}
