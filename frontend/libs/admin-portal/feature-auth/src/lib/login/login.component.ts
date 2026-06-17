import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthStore } from '@sb/shared/data-access';
import { ButtonComponent, FormFieldComponent } from '@sb/shared/ui';

@Component({
  selector: 'sb-login',
  standalone: true,
  imports: [ReactiveFormsModule, ButtonComponent, FormFieldComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  readonly #fb = inject(FormBuilder);
  readonly #authStore = inject(AuthStore);

  readonly isLoading = this.#authStore.isLoading;
  readonly serverError = signal<string | null>(null);

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

    const { email, password } = this.form.getRawValue();
    try {
      await this.#authStore.signIn(email!, password!);
    } catch {
      this.serverError.set(this.#authStore.error());
    }
  }
}
