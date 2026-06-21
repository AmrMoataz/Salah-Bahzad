import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  AlertComponent,
  ButtonComponent,
  FormFieldComponent,
  SelectComponent,
  SelectOption,
  FileUploadComponent,
  UploadedFile,
} from '@sb/shared/ui';
import {
  CityRef,
  GradeRef,
  ID_IMAGE_ACCEPTED_TYPES,
  ID_IMAGE_ACCEPT_ATTR,
  ID_IMAGE_MAX_BYTES,
  RegionRef,
  RegistrationMethod,
  RegistrationService,
} from '@sb/student-portal/data-access';

/** Cross-field check: manual sign-up's two password fields must match (skipped when disabled). */
function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const pw = group.get('password');
  const confirm = group.get('confirmPassword');
  if (!pw || !confirm || pw.disabled || confirm.disabled) return null;
  if (!confirm.value) return null;
  return pw.value === confirm.value ? null : { passwordMismatch: true };
}

/** Per-field copy for a missing required value (falls back to a generic message). */
const REQUIRED_MESSAGE: Record<string, string> = {
  fullName: 'Your full name is required.',
  email: 'Email is required.',
  password: 'Choose a password.',
  confirmPassword: 'Please confirm your password.',
  phoneNumber: 'Your phone number is required.',
  schoolName: 'Your school name is required.',
  gradeId: 'Select your grade.',
  cityId: 'Select your city.',
  regionId: 'Select your region.',
  parentPhonePrimary: 'A parent/guardian phone is required.',
};

/**
 * The two-step self-registration wizard (prototype `AUTH: REGISTER`), replacing S0's `/register`
 * placeholder under `guestGuard`. One typed reactive form split across **Step 1 — account**
 * (Google popup *or* email/password, plus the student's phone) and **Step 2 — details** (school,
 * grade, city→region cascade, ≥ 1 parent phone, ID image, terms + one-device consent). The Firebase
 * identity is minted by `RegistrationService` (never the student exchange — there is no student
 * yet); on `201` the inline pending state renders (`FR-STU-REG-009`). Server validation is
 * authoritative: `400` field errors map back onto the controls, `404`/`409`/`429` surface readable
 * copy, and entered values survive every error.
 *
 * Satisfies `FR-STU-REG-001..009`, `FR-PLAT-AUTH-003`, `NFR-PRIV-001/003`, `FR-STU-RWD-001/002`,
 * `FR-STU-A11Y-001`.
 */
@Component({
  selector: 'sb-student-register',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonComponent,
    FormFieldComponent,
    SelectComponent,
    FileUploadComponent,
    AlertComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
})
export class RegisterComponent {
  readonly #fb = inject(FormBuilder);
  readonly #registration = inject(RegistrationService);
  readonly #destroyRef = inject(DestroyRef);
  readonly #host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly idImageAccept = ID_IMAGE_ACCEPT_ATTR;

  // ── Wizard state ─────────────────────────────────────────────────
  readonly step = signal<1 | 2>(1);
  readonly method = signal<RegistrationMethod>('manual');
  readonly googleProfile = signal<{ fullName: string; email: string } | null>(null);
  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly topError = signal<string | null>(null);
  /** A post-create `409` (or Firebase email-already-in-use) → surface a sign-in affordance. */
  readonly alreadyRegistered = signal(false);

  // ── Reference data ───────────────────────────────────────────────
  readonly #grades = signal<GradeRef[]>([]);
  readonly #cities = signal<CityRef[]>([]);
  readonly #regions = signal<RegionRef[]>([]);
  readonly regionsLoading = signal(false);

  readonly gradeOptions = computed<SelectOption[]>(() =>
    this.#grades().map((g) => ({ value: g.id, label: g.name })),
  );
  readonly cityOptions = computed<SelectOption[]>(() =>
    this.#cities().map((c) => ({ value: c.id, label: c.nameEn })),
  );
  readonly regionOptions = computed<SelectOption[]>(() =>
    this.#regions().map((r) => ({ value: r.id, label: r.nameEn })),
  );

  // ── ID image (held outside the form; FileUpload emits raw File[]) ─
  readonly #idFile = signal<File | null>(null);
  readonly idError = signal<string | null>(null);
  readonly idFiles = computed<UploadedFile[]>(() => {
    const file = this.#idFile();
    return file ? [{ name: file.name, sizeBytes: file.size }] : [];
  });

  readonly form = this.#fb.group(
    {
      // Step 1
      fullName: ['', [Validators.required, Validators.maxLength(200)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]],
      phoneNumber: ['', [Validators.required, Validators.maxLength(32)]],
      // Step 2
      schoolName: ['', [Validators.required, Validators.maxLength(200)]],
      gradeId: ['', [Validators.required]],
      cityId: ['', [Validators.required]],
      regionId: [{ value: '', disabled: true }, [Validators.required]],
      parentPhonePrimary: ['', [Validators.required, Validators.maxLength(32)]],
      parentPhoneSecondary: ['', [Validators.maxLength(32)]],
      terms: [false, [Validators.requiredTrue]],
    },
    { validators: passwordsMatch },
  );

  constructor() {
    this.#registration
      .grades()
      .pipe(takeUntilDestroyed())
      .subscribe({ next: (g) => this.#grades.set(g), error: () => this.#grades.set([]) });
    this.#registration
      .cities()
      .pipe(takeUntilDestroyed())
      .subscribe({ next: (c) => this.#cities.set(c), error: () => this.#cities.set([]) });
  }

  // ── Step 1 ───────────────────────────────────────────────────────
  /** Runs the Google popup, prefills name + (read-only) email, and switches Step 1 to its Google view. */
  async onGoogle(): Promise<void> {
    this.topError.set(null);
    this.alreadyRegistered.set(false);
    try {
      const profile = await this.#registration.signUpWithGoogle();
      this.method.set('google');
      this.googleProfile.set({
        fullName: profile.fullName ?? '',
        email: profile.email ?? '',
      });
      this.form.patchValue({
        fullName: profile.fullName ?? '',
        email: profile.email ?? '',
      });
      // Email/password are owned by Google now — exclude them from validation.
      this.form.controls.password.disable();
      this.form.controls.confirmPassword.disable();
      this.form.controls.email.disable();
    } catch (err: unknown) {
      this.#handleFirebaseError(err);
    }
  }

  /** Switches back from the Google sub-view to the email/password form. */
  useEmailInstead(): void {
    this.method.set('manual');
    this.googleProfile.set(null);
    this.#registration.reset();
    this.form.controls.password.enable();
    this.form.controls.confirmPassword.enable();
    this.form.controls.email.enable();
    this.form.patchValue({ email: '', password: '', confirmPassword: '' });
  }

  /** "Continue →": validate Step 1's relevant controls, then advance. */
  goToStep2(): void {
    const controls = this.#step1Controls();
    controls.forEach((c) => c.markAsTouched());
    const valid = controls.every((c) => c.valid) && !this.form.hasError('passwordMismatch');
    if (!valid) {
      this.#focusFirstError();
      return;
    }
    this.step.set(2);
    this.#focusStepHeading();
  }

  goToStep1(): void {
    this.step.set(1);
    this.#focusStepHeading();
  }

  #step1Controls(): AbstractControl[] {
    const f = this.form.controls;
    return this.method() === 'google'
      ? [f.fullName, f.phoneNumber]
      : [f.fullName, f.email, f.password, f.confirmPassword, f.phoneNumber];
  }

  // ── Step 2 ───────────────────────────────────────────────────────
  /** City→region cascade: load the picked city's regions and reset any stale region. */
  onCityChange(cityId: string): void {
    const region = this.form.controls.regionId;
    region.reset('');
    region.disable();
    this.#regions.set([]);
    if (!cityId) return;

    this.regionsLoading.set(true);
    this.#registration
      .regions(cityId)
      .pipe(takeUntilDestroyed(this.#destroyRef))
      .subscribe({
        next: (regions) => {
          this.#regions.set(regions);
          this.form.controls.regionId.enable();
          this.regionsLoading.set(false);
        },
        error: () => {
          this.#regions.set([]);
          this.regionsLoading.set(false);
        },
      });
  }

  onIdPicked(files: File[]): void {
    const file = files[0];
    if (!file) return;

    if (!(ID_IMAGE_ACCEPTED_TYPES as readonly string[]).includes(file.type)) {
      this.#rejectId('Use a JPEG, PNG, or WebP image.');
      return;
    }
    if (file.size === 0) {
      this.#rejectId('That file looks empty — pick another.');
      return;
    }
    if (file.size > ID_IMAGE_MAX_BYTES) {
      this.#rejectId('Image must be 5 MB or smaller.');
      return;
    }

    this.idError.set(null);
    this.#setIdFile(file);
  }

  clearId(): void {
    this.#setIdFile(null);
  }

  #rejectId(message: string): void {
    this.idError.set(message);
    this.#setIdFile(null);
  }

  #setIdFile(file: File | null): void {
    this.#idFile.set(file);
  }

  // ── Submit ───────────────────────────────────────────────────────
  async submit(): Promise<void> {
    // The form spans both steps; an Enter press on Step 1 advances instead of submitting.
    if (this.step() !== 2) {
      this.goToStep2();
      return;
    }

    const f = this.form.controls;
    const required = [f.schoolName, f.gradeId, f.cityId, f.regionId, f.parentPhonePrimary];
    [...required, f.parentPhoneSecondary, f.terms].forEach((c) => c.markAsTouched());

    let invalid = required.some((c) => c.invalid) || f.terms.invalid || f.parentPhoneSecondary.invalid;
    if (!this.#idFile()) {
      this.idError.set('Upload a photo of your ID to continue.');
      invalid = true;
    }
    if (invalid) {
      this.#focusFirstError();
      return;
    }

    this.submitting.set(true);
    this.topError.set(null);
    this.alreadyRegistered.set(false);

    const raw = this.form.getRawValue();
    try {
      if (this.method() === 'manual') {
        await this.#registration.createEmailAccount(raw.email!.trim(), raw.password!);
      }
      await firstValueFrom(
        this.#registration.register({
          fullName: raw.fullName!.trim(),
          phoneNumber: raw.phoneNumber!.trim(),
          parentPhonePrimary: raw.parentPhonePrimary!.trim(),
          parentPhoneSecondary: raw.parentPhoneSecondary?.trim() || undefined,
          gradeId: raw.gradeId!,
          cityId: raw.cityId!,
          regionId: raw.regionId!,
          schoolName: raw.schoolName!.trim(),
          idImage: this.#idFile()!,
        }),
      );
      this.submitted.set(true);
    } catch (err: unknown) {
      this.#handleSubmitError(err);
    } finally {
      this.submitting.set(false);
    }
  }

  // ── Error mapping ────────────────────────────────────────────────
  #handleSubmitError(err: unknown): void {
    if (err instanceof HttpErrorResponse) {
      switch (err.status) {
        case 400:
          this.#applyValidationErrors(err);
          break;
        case 404:
          this.topError.set(
            this.#detail(err) ??
              "We couldn't find your grade, city, or region. Please re-check and try again.",
          );
          break;
        case 409:
          this.alreadyRegistered.set(true);
          this.topError.set('An account already exists for this sign-in. Please sign in instead.');
          break;
        case 429:
          this.topError.set('Too many attempts. Please wait a moment and try again.');
          break;
        case 0:
          this.topError.set('Network error. Please check your connection.');
          break;
        default:
          this.topError.set(this.#detail(err) ?? 'Something went wrong. Please try again.');
      }
      return;
    }
    this.#handleFirebaseError(err);
  }

  /** Maps FluentValidation field errors (ASP.NET `ValidationProblemDetails.errors`) onto controls. */
  #applyValidationErrors(err: HttpErrorResponse): void {
    const errors = (err.error?.errors ?? null) as Record<string, string[]> | null;
    if (!errors) {
      this.topError.set(this.#detail(err) ?? 'Please check the highlighted fields and try again.');
      return;
    }

    let touchedStep1 = false;
    for (const [key, messages] of Object.entries(errors)) {
      const message = messages?.[0] ?? 'Invalid value.';
      const control = this.#controlForServerKey(key);
      if (control) {
        control.setErrors({ server: message });
        control.markAsTouched();
        if (this.#step1Controls().includes(control)) touchedStep1 = true;
      } else if (key.toLowerCase() === 'idimage') {
        this.idError.set(message);
      } else if (key.toLowerCase() === 'firebaseidtoken') {
        this.topError.set(message);
      }
    }
    if (!this.topError()) {
      this.topError.set('Please check the highlighted fields and try again.');
    }
    // A Step-1 field failed server-side — take the student back to it so the error is visible.
    if (touchedStep1) this.step.set(1);
  }

  #handleFirebaseError(err: unknown): void {
    const code = (err as { code?: string })?.code;
    if (!code?.startsWith('auth/')) {
      this.topError.set('Something went wrong. Please try again.');
      return;
    }
    switch (code) {
      case 'auth/email-already-in-use':
        this.alreadyRegistered.set(true);
        this.topError.set('You already have an account — sign in instead.');
        this.step.set(1);
        break;
      case 'auth/weak-password':
        this.form.controls.password.setErrors({ server: 'Choose a stronger password (8+ characters).' });
        this.step.set(1);
        break;
      case 'auth/invalid-email':
        this.form.controls.email.setErrors({ server: 'Enter a valid email address.' });
        this.step.set(1);
        break;
      case 'auth/popup-closed-by-user':
      case 'auth/cancelled-popup-request':
        this.topError.set('Google sign-up was cancelled.');
        break;
      case 'auth/popup-blocked':
        this.topError.set('Your browser blocked the Google popup. Allow popups and try again.');
        break;
      case 'auth/network-request-failed':
        this.topError.set('Network error. Please check your connection.');
        break;
      default:
        this.topError.set('Couldn’t create your account. Please try again.');
    }
  }

  #controlForServerKey(key: string): AbstractControl | null {
    const map: Record<string, keyof typeof this.form.controls> = {
      fullname: 'fullName',
      email: 'email',
      password: 'password',
      phonenumber: 'phoneNumber',
      parentphoneprimary: 'parentPhonePrimary',
      parentphonesecondary: 'parentPhoneSecondary',
      gradeid: 'gradeId',
      cityid: 'cityId',
      regionid: 'regionId',
      schoolname: 'schoolName',
    };
    const name = map[key.toLowerCase()];
    return name ? this.form.controls[name] : null;
  }

  #detail(err: HttpErrorResponse): string | null {
    const body = err.error as { detail?: string; title?: string } | undefined;
    return body?.detail ?? body?.title ?? null;
  }

  // ── Field error copy (touched-only; server errors win) ───────────
  fieldError(name: keyof typeof this.form.controls): string {
    const c = this.form.controls[name];
    if (!c.touched || c.valid) return '';
    if (c.hasError('server')) return c.getError('server') as string;
    if (c.hasError('required')) return REQUIRED_MESSAGE[name as string] ?? 'This field is required.';
    if (c.hasError('email')) return 'Enter a valid email address.';
    if (c.hasError('minlength')) return 'Password must be at least 8 characters.';
    if (c.hasError('maxlength')) return 'That value is a little too long.';
    return 'Please check this field.';
  }

  get confirmError(): string {
    const c = this.form.controls.confirmPassword;
    if (!c.touched) return '';
    if (c.hasError('server')) return c.getError('server') as string;
    if (c.hasError('required')) return 'Please confirm your password.';
    if (this.form.hasError('passwordMismatch')) return 'Passwords do not match.';
    return '';
  }

  isInvalid(name: keyof typeof this.form.controls): boolean {
    const c = this.form.controls[name];
    return c.touched && c.invalid;
  }

  /** Initials for the Google prefill chip (e.g. "Lina Hassan" → "LH"). */
  readonly googleInitials = computed(() => {
    const name = this.googleProfile()?.fullName?.trim() ?? '';
    if (!name) return '🙂';
    return name
      .split(/\s+/)
      .slice(0, 2)
      .map((p) => p[0]?.toUpperCase() ?? '')
      .join('');
  });

  // ── Focus management (a11y) ──────────────────────────────────────
  #focusStepHeading(): void {
    setTimeout(() => {
      this.#host.nativeElement.querySelector<HTMLElement>('[data-step-heading]')?.focus();
    });
  }

  #focusFirstError(): void {
    setTimeout(() => {
      const target = this.#host.nativeElement.querySelector<HTMLElement>(
        '[aria-invalid="true"], .reg__field--error input, .reg__field--error .sb-select__trigger',
      );
      target?.focus();
    });
  }
}
