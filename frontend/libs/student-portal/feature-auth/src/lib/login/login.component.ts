import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { StudentAuthStore } from '@sb/student-portal/data-access';
import { ButtonComponent, FormFieldComponent } from '@sb/shared/ui';

/**
 * Student sign-in (`AUTH: LOGIN`): Continue with Google + email/password, Forgot password, and a
 * Create-account link (the wizard is S1). Errors and friendly Firebase copy come from the store; a
 * blocked `403` doesn't surface here — the store routes to the status screen instead.
 */
@Component({
  selector: 'sb-student-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, ButtonComponent, FormFieldComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  readonly #fb = inject(FormBuilder);
  readonly #authStore = inject(StudentAuthStore);

  readonly isLoading = this.#authStore.isLoading;
  readonly serverError = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  readonly form = this.#fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]],
    remember: [true],
  });

  get emailError(): string {
    const ctrl = this.form.controls.email;
    if (!ctrl.touched) return '';
    if (ctrl.hasError('required')) return 'Email is required.';
    if (ctrl.hasError('email')) return 'Enter a valid email address.';
    return '';
  }

  get passwordError(): string {
    const ctrl = this.form.controls.password;
    if (!ctrl.touched) return '';
    if (ctrl.hasError('required')) return 'Password is required.';
    if (ctrl.hasError('minlength')) return 'Password must be at least 6 characters.';
    return '';
  }

  async onSubmit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.serverError.set(null);
    this.notice.set(null);

    const { email, password } = this.form.getRawValue();
    try {
      await this.#authStore.signIn(email!, password!);
    } catch {
      this.serverError.set(this.#authStore.error());
    }
  }

  async onGoogle(): Promise<void> {
    this.serverError.set(null);
    this.notice.set(null);
    try {
      await this.#authStore.signInWithGoogle();
    } catch {
      this.serverError.set(this.#authStore.error());
    }
  }

  /** Sends a Firebase reset email to the address in the form (FR-PLAT-AUTH-009). */
  async onForgotPassword(event: Event): Promise<void> {
    event.preventDefault();
    this.serverError.set(null);
    this.notice.set(null);

    const emailCtrl = this.form.controls.email;
    if (emailCtrl.invalid) {
      emailCtrl.markAsTouched();
      this.serverError.set('Enter your email address above, then tap “Forgot password?”.');
      return;
    }

    try {
      await this.#authStore.requestPasswordReset(emailCtrl.value!);
      this.notice.set('Password reset email sent — check your inbox.');
    } catch {
      this.serverError.set('Couldn’t send the reset email. Check the address and try again.');
    }
  }
}
