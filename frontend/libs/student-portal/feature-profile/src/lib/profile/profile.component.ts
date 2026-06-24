import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import {
  AlertComponent,
  AvatarComponent,
  ButtonComponent,
  CardComponent,
  FormFieldComponent,
  ModalComponent,
  SelectComponent,
  SelectOption,
  StatusPillComponent,
  ToastService,
} from '@sb/shared/ui';
import {
  CityRef,
  ProfileService,
  RegionRef,
  RegistrationService,
  StudentAuthStore,
  StudentProfile,
  UpdateMyStudentProfile,
} from '@sb/student-portal/data-access';

/**
 * The student self-service **Profile** screen (S6, the prototype's `PROFILE` section + its three
 * modals — `FR-STU-PRO-001/002/003`). It is the **final** student-portal slice: a gradient header
 * band (initials-only `xl` Avatar + name + grade/city sub-line + an "Active" success Chip), a typed
 * reactive **Personal information** form (Full name / School / City→Region cascade / parent phones
 * editable; **Email** and **Grade** disabled), a **Bound device** card, and a **Security** card.
 *
 * **Four binding decisions (contract §D):** (1) avatar = initials only — no upload; (2) device reset
 * is **contact-support only**, calling **no API**; (3) password change = Firebase
 * `sendPasswordResetEmail` (no form, no backend); (4) email = read-only Firebase identity (not stored,
 * not in either DTO). Email + password + sign-out all go through the {@link StudentAuthStore} seam so
 * Firebase stays out of the feature lib.
 *
 * `phoneNumber` is **preserved unchanged** in the PUT body: per §C.1 the student edits
 * {full name, parent phones, school, city, region}; their own phone is not an editable field here, but
 * the §A.2 body requires it `NotEmpty`, so it is carried through from the loaded profile.
 */
@Component({
  selector: 'sb-student-profile',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    DatePipe,
    AvatarComponent,
    StatusPillComponent,
    CardComponent,
    ButtonComponent,
    SelectComponent,
    FormFieldComponent,
    AlertComponent,
    ModalComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <div class="pf__loading" aria-busy="true" aria-label="Loading your profile">
        <div class="pf__loading-band"></div>
        <div class="pf__loading-grid">
          <div class="pf__loading-card"></div>
          <div class="pf__loading-card"></div>
        </div>
      </div>
    } @else if (profile(); as p) {
      <section class="pf" [formGroup]="form">
        <!-- Header band -->
        <header class="pf__band">
          <sb-avatar size="xl" status="active" [initials]="initials()" />
          <div class="pf__band-id">
            <h1 class="pf__name">{{ p.fullName }}</h1>
            @if (subLine()) {
              <p class="pf__sub">{{ subLine() }}</p>
            }
          </div>
          <sb-status-pill variant="success">{{ p.status }}</sb-status-pill>
        </header>

        <div class="pf__grid">
          <!-- Personal information -->
          <sb-card title="Personal information">
            <div class="pf__fields">
              <sb-form-field label="Full name" fieldId="fullName" [error]="fieldError('fullName')" [required]="true">
                <input
                  id="fullName"
                  type="text"
                  class="sb-input"
                  formControlName="fullName"
                  autocomplete="name"
                  [attr.aria-invalid]="isInvalid('fullName') ? 'true' : null"
                />
              </sb-form-field>

              <sb-form-field label="Email" fieldId="email" hint="Managed by your sign-in provider.">
                <input
                  id="email"
                  type="email"
                  class="sb-input"
                  [value]="email() ?? ''"
                  disabled
                  aria-disabled="true"
                />
              </sb-form-field>

              <sb-form-field label="School" fieldId="schoolName" [error]="fieldError('schoolName')" [required]="true">
                <input
                  id="schoolName"
                  type="text"
                  class="sb-input"
                  formControlName="schoolName"
                  [attr.aria-invalid]="isInvalid('schoolName') ? 'true' : null"
                />
              </sb-form-field>

              <sb-form-field label="Grade" fieldId="grade" hint="Set by your teacher.">
                <input
                  id="grade"
                  type="text"
                  class="sb-input"
                  [value]="p.gradeName ?? ''"
                  disabled
                  aria-disabled="true"
                />
              </sb-form-field>

              <sb-form-field label="City" fieldId="cityId" [error]="fieldError('cityId')" [required]="true">
                <sb-select
                  inputId="cityId"
                  formControlName="cityId"
                  placeholder="Select city"
                  [options]="cityOptions()"
                  [invalid]="isInvalid('cityId')"
                  (valueChange)="onCityChange($event)"
                />
              </sb-form-field>

              <sb-form-field
                label="Region"
                fieldId="regionId"
                [error]="fieldError('regionId')"
                [required]="true"
              >
                <sb-select
                  inputId="regionId"
                  formControlName="regionId"
                  placeholder="Select region"
                  [options]="regionOptions()"
                  [invalid]="isInvalid('regionId')"
                />
              </sb-form-field>
            </div>

            <h3 class="pf__subhead">Parent / guardian numbers</h3>
            <div class="pf__fields">
              <sb-form-field
                label="Primary"
                fieldId="parentPhonePrimary"
                [error]="fieldError('parentPhonePrimary')"
                [required]="true"
              >
                <input
                  id="parentPhonePrimary"
                  type="tel"
                  class="sb-input"
                  formControlName="parentPhonePrimary"
                  [attr.aria-invalid]="isInvalid('parentPhonePrimary') ? 'true' : null"
                />
              </sb-form-field>

              <sb-form-field label="Secondary" fieldId="parentPhoneSecondary" [error]="fieldError('parentPhoneSecondary')">
                <input
                  id="parentPhoneSecondary"
                  type="tel"
                  class="sb-input"
                  formControlName="parentPhoneSecondary"
                  [attr.aria-invalid]="isInvalid('parentPhoneSecondary') ? 'true' : null"
                />
              </sb-form-field>
            </div>

            @if (formError()) {
              <p class="pf__form-error" role="alert">{{ formError() }}</p>
            }

            <div class="pf__save">
              <sb-button
                variant="primary"
                [loading]="saving()"
                [disabled]="form.invalid || form.pristine"
                (clicked)="save()"
              >Save changes</sb-button>
            </div>
          </sb-card>

          <!-- Right column: Bound device + Security -->
          <div class="pf__side">
            <sb-card title="Bound device">
              @if (p.boundDevice; as device) {
                <div class="pf__device">
                  <span class="pf__device-icon" aria-hidden="true">
                    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                         stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                      <rect x="5" y="2" width="14" height="20" rx="2" />
                      <line x1="12" y1="18" x2="12.01" y2="18" />
                    </svg>
                  </span>
                  <div class="pf__device-body">
                    <p class="pf__device-name">{{ device.summary ?? 'Your current device' }}</p>
                    <p class="pf__device-meta">Bound {{ device.boundAtUtc | date: 'mediumDate' }}</p>
                  </div>
                </div>
              } @else {
                <p class="pf__device-empty">No device bound yet.</p>
              }
              <p class="pf__device-help">
                Only one device can access content. To switch devices, contact support to reset the binding.
              </p>
              <sb-button variant="secondary" size="sm" (clicked)="openDeviceModal()">Reset device</sb-button>
            </sb-card>

            <sb-card title="Security">
              <div class="pf__security">
                <sb-button variant="secondary" (clicked)="openPasswordModal()">Change password</sb-button>
                <sb-button variant="danger-ghost" (clicked)="openSignOutModal()">Sign out</sb-button>
              </div>
            </sb-card>
          </div>
        </div>
      </section>
    }

    <!-- D.2 — Device-reset modal: INFO only, calls NO API (decision 2) -->
    <sb-modal [open]="deviceModalOpen()" title="Reset bound device?" size="confirm" (close)="closeDeviceModal()">
      <sb-alert variant="warning" title="One device only">
        Resetting unbinds your current phone. The next device you sign in on becomes your bound device.
        This can only be done a limited number of times — contact support to reset it.
      </sb-alert>
      <div modalFooter class="pf__modal-actions">
        <sb-button variant="secondary" (clicked)="closeDeviceModal()">Cancel</sb-button>
        <sb-button variant="primary" (clicked)="requestDeviceReset()">Request reset</sb-button>
      </div>
    </sb-modal>

    <!-- D.3 — Change-password modal: Firebase sendPasswordResetEmail (decision 3) -->
    <sb-modal [open]="passwordModalOpen()" title="Change password" size="confirm" (close)="closePasswordModal()">
      @if (pwSent()) {
        <div class="pf__pw-done">
          <span class="pf__pw-check" aria-hidden="true">
            <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="20 6 9 17 4 12" />
            </svg>
          </span>
          <h3 class="pf__pw-title">Check your inbox</h3>
          <p class="pf__pw-text">
            We sent a password reset link to {{ email() }}. Follow it to set a new password.
          </p>
        </div>
      } @else {
        <p class="pf__pw-prompt">
          Send a password reset link to <strong>{{ email() }}</strong>? You'll set a new password from your inbox.
        </p>
      }
      <div modalFooter class="pf__modal-actions">
        @if (pwSent()) {
          <sb-button variant="primary" (clicked)="closePasswordModal()">Done</sb-button>
        } @else {
          <sb-button variant="secondary" (clicked)="closePasswordModal()">Cancel</sb-button>
          <sb-button variant="primary" [loading]="pwSending()" (clicked)="confirmPasswordReset()">Send reset link</sb-button>
        }
      </div>
    </sb-modal>

    <!-- D.4 — Sign-out confirm modal -->
    <sb-modal [open]="signOutModalOpen()" title="Sign out?" size="confirm" (close)="closeSignOutModal()">
      <div class="pf__signout">
        <img class="pf__signout-art" src="/assets/salah-relaxing.png" alt="" aria-hidden="true" />
        <p class="pf__signout-text">
          You'll need to sign in again on this device. Your progress is saved — see you soon!
        </p>
      </div>
      <div modalFooter class="pf__modal-actions">
        <sb-button variant="secondary" (clicked)="closeSignOutModal()">Stay signed in</sb-button>
        <sb-button variant="danger" [loading]="signingOut()" (clicked)="confirmSignOut()">Sign out</sb-button>
      </div>
    </sb-modal>
  `,
  styles: [`
    .pf { display: flex; flex-direction: column; gap: var(--sb-space-5); }

    .pf__band {
      display: flex;
      align-items: center;
      gap: var(--sb-space-4);
      flex-wrap: wrap;
      padding: var(--sb-space-5);
      border: 1px solid #D2E4F6;
      border-radius: var(--sb-radius-xl);
      background: linear-gradient(135deg, #EAF2FB, #EBF5E9);
    }
    .pf__band-id { flex: 1; min-width: 160px; }
    .pf__name { margin: 0; font-size: 24px; font-weight: 800; letter-spacing: -0.3px; color: var(--sb-text); }
    .pf__sub { margin: 2px 0 0; font-size: var(--sb-body-md-size); color: var(--sb-text-muted); }

    .pf__grid { display: grid; grid-template-columns: 1.6fr 1fr; gap: var(--sb-space-5); align-items: start; }
    .pf__side { display: flex; flex-direction: column; gap: var(--sb-space-5); }

    .pf__fields { display: grid; grid-template-columns: 1fr 1fr; gap: var(--sb-space-4); }
    .pf__subhead { margin: var(--sb-space-5) 0 var(--sb-space-3); font-size: var(--sb-body-md-size); font-weight: 800; color: var(--sb-text); }

    :host ::ng-deep .sb-input {
      width: 100%;
      height: 44px;
      padding: 0 14px;
      border: 1px solid var(--sb-border-strong);
      border-radius: var(--sb-radius-md);
      background: var(--sb-surface);
      color: var(--sb-text);
      font: var(--sb-body-md-size) var(--sb-font-sans);
      outline: none;
      box-sizing: border-box;
    }
    :host ::ng-deep .sb-input::placeholder { color: var(--sb-text-subtle); }
    :host ::ng-deep .sb-input:focus { border-color: var(--sb-primary); box-shadow: var(--sb-shadow-focus); }
    :host ::ng-deep .sb-input[aria-invalid='true'] { border-color: var(--sb-danger); }
    :host ::ng-deep .sb-input:disabled { background: var(--sb-surface-sunken); color: var(--sb-text-muted); cursor: not-allowed; }

    .pf__form-error { margin: var(--sb-space-4) 0 0; color: var(--sb-danger-fg); font-size: var(--sb-body-sm-size); font-weight: 600; }
    .pf__save { display: flex; margin-top: var(--sb-space-5); }

    .pf__device {
      display: flex;
      gap: var(--sb-space-3);
      align-items: center;
      padding: var(--sb-space-3);
      background: var(--sb-surface-sunken);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-md);
    }
    .pf__device-icon {
      width: 42px;
      height: 42px;
      flex-shrink: 0;
      border-radius: var(--sb-radius-md);
      background: var(--sb-info-bg);
      color: var(--sb-info-fg);
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .pf__device-name { margin: 0; font-size: var(--sb-body-md-size); font-weight: 700; color: var(--sb-text); }
    .pf__device-meta { margin: 2px 0 0; font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); }
    .pf__device-empty { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }
    .pf__device-help { margin: var(--sb-space-3) 0; font-size: var(--sb-body-sm-size); color: var(--sb-text-muted); line-height: 1.5; }

    .pf__security { display: flex; flex-direction: column; gap: var(--sb-space-3); }
    .pf__security sb-button { display: block; }
    .pf__security ::ng-deep .sb-btn { width: 100%; }

    .pf__modal-actions { display: flex; gap: var(--sb-space-3); }
    .pf__modal-actions sb-button { flex: 1; }
    .pf__modal-actions ::ng-deep .sb-btn { width: 100%; }

    .pf__pw-prompt { margin: 0; color: var(--sb-text); font-size: var(--sb-body-md-size); line-height: 1.5; }
    .pf__pw-done { text-align: center; }
    .pf__pw-check {
      width: 64px;
      height: 64px;
      margin: 0 auto var(--sb-space-3);
      border-radius: var(--sb-radius-circle);
      background: var(--sb-success-bg);
      color: var(--sb-success);
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .pf__pw-title { margin: 0; font-size: var(--sb-heading-sm-size); font-weight: 800; }
    .pf__pw-text { margin: var(--sb-space-2) 0 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); line-height: 1.5; }

    .pf__signout { display: flex; gap: var(--sb-space-4); align-items: center; }
    .pf__signout-art { width: 96px; flex-shrink: 0; }
    .pf__signout-text { margin: 0; color: var(--sb-text); font-size: var(--sb-body-md-size); line-height: 1.5; }

    .pf__loading { display: flex; flex-direction: column; gap: var(--sb-space-5); }
    .pf__loading-band { height: 104px; border-radius: var(--sb-radius-xl); background: var(--sb-surface-sunken); animation: pf-pulse 1.3s var(--sb-easing-standard) infinite; }
    .pf__loading-grid { display: grid; grid-template-columns: 1.6fr 1fr; gap: var(--sb-space-5); }
    .pf__loading-card { height: 320px; border-radius: var(--sb-radius-lg); background: var(--sb-surface-sunken); animation: pf-pulse 1.3s var(--sb-easing-standard) infinite; }
    @keyframes pf-pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.55; } }

    @media (max-width: 860px) {
      .pf__grid, .pf__loading-grid { grid-template-columns: 1fr; }
    }
    @media (max-width: 560px) {
      .pf__fields { grid-template-columns: 1fr; }
    }
  `],
})
export class ProfileComponent {
  readonly #profileSvc = inject(ProfileService);
  readonly #registration = inject(RegistrationService);
  readonly #store = inject(StudentAuthStore);
  readonly #toast = inject(ToastService);
  readonly #fb = inject(NonNullableFormBuilder);
  readonly #destroyRef = inject(DestroyRef);

  // ── Loaded state ─────────────────────────────────────────────────
  readonly profile = signal<StudentProfile | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  /** Read-only Firebase identity email (decision 4 / §C.2) — never a form control, never sent on PUT. */
  readonly email = signal<string | null>(null);

  // ── Reference data (reused anonymous /api/reference reads, §C.4) ──
  readonly #cities = signal<CityRef[]>([]);
  readonly #regions = signal<RegionRef[]>([]);
  readonly cityOptions = computed<SelectOption[]>(() =>
    this.#cities().map((c) => ({ value: c.id, label: c.nameEn })),
  );
  readonly regionOptions = computed<SelectOption[]>(() =>
    this.#regions().map((r) => ({ value: r.id, label: r.nameEn })),
  );

  // ── Modals (each driven by an open flag) ─────────────────────────
  readonly deviceModalOpen = signal(false);
  readonly passwordModalOpen = signal(false);
  readonly pwSending = signal(false);
  readonly pwSent = signal(false);
  readonly signOutModalOpen = signal(false);
  readonly signingOut = signal(false);

  /** Only the writable, editable fields (§C.1). `phoneNumber` is preserved from the loaded profile. */
  readonly form = this.#fb.group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    schoolName: ['', [Validators.required, Validators.maxLength(200)]],
    cityId: ['', [Validators.required]],
    regionId: ['', [Validators.required]],
    parentPhonePrimary: ['', [Validators.required, Validators.maxLength(32)]],
    parentPhoneSecondary: ['', [Validators.maxLength(32)]],
  });

  // ── Header derivations ───────────────────────────────────────────
  readonly initials = computed(() => {
    const name = this.profile()?.fullName?.trim() ?? '';
    if (!name) return '';
    return name
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('');
  });

  /** "{gradeName} · {cityName}" — the prototype's "track" segment has no DTO field, so it is dropped. */
  readonly subLine = computed(() => {
    const p = this.profile();
    if (!p) return '';
    return [p.gradeName, p.cityName].filter((segment): segment is string => !!segment).join(' · ');
  });

  constructor() {
    this.email.set(this.#store.getCurrentEmail());

    this.#registration
      .cities()
      .pipe(takeUntilDestroyed())
      .subscribe({ next: (c) => this.#cities.set(c), error: () => this.#cities.set([]) });

    this.#load();
  }

  // ── Load + seed ──────────────────────────────────────────────────
  #load(): void {
    this.loading.set(true);
    this.#profileSvc
      .getProfile()
      .pipe(takeUntilDestroyed(this.#destroyRef))
      .subscribe({
        next: (p) => {
          this.#seed(p);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.#toast.error('Couldn’t load your profile. Please try again.');
        },
      });
  }

  #seed(p: StudentProfile): void {
    this.profile.set(p);
    this.form.reset({
      fullName: p.fullName,
      schoolName: p.schoolName,
      cityId: p.cityId,
      regionId: p.regionId,
      parentPhonePrimary: p.parentPhonePrimary,
      parentPhoneSecondary: p.parentPhoneSecondary ?? '',
    });
    // Seed the region options for the current city so the selected region renders.
    this.#loadRegions(p.cityId);
  }

  #loadRegions(cityId: string): void {
    if (!cityId) {
      this.#regions.set([]);
      return;
    }
    this.#registration
      .regions(cityId)
      .pipe(takeUntilDestroyed(this.#destroyRef))
      .subscribe({ next: (r) => this.#regions.set(r), error: () => this.#regions.set([]) });
  }

  /** City→region cascade (§C.3): load the picked city's regions and clear the stale region. */
  onCityChange(cityId: string): void {
    this.form.controls.regionId.reset('');
    this.#loadRegions(cityId);
  }

  // ── Save ─────────────────────────────────────────────────────────
  save(): void {
    if (this.saving()) return;
    const p = this.profile();
    if (!p || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const body: UpdateMyStudentProfile = {
      fullName: v.fullName.trim(),
      // Preserved unchanged — not student-editable here (§C.1), but the §A.2 body requires it.
      phoneNumber: p.phoneNumber,
      schoolName: v.schoolName.trim(),
      cityId: v.cityId,
      regionId: v.regionId,
      parentPhonePrimary: v.parentPhonePrimary.trim(),
      parentPhoneSecondary: v.parentPhoneSecondary.trim() || null,
    };

    this.saving.set(true);
    this.formError.set(null);
    this.#profileSvc
      .updateProfile(body)
      .pipe(takeUntilDestroyed(this.#destroyRef))
      .subscribe({
        next: (updated) => {
          this.saving.set(false);
          this.email.set(this.#store.getCurrentEmail());
          this.#seed(updated);
          this.form.markAsPristine();
          this.#toast.success('Profile updated.');
        },
        error: (err: unknown) => {
          this.saving.set(false);
          this.formError.set(this.#messageFor(err));
        },
      });
  }

  /** A `400` is FluentValidation (incl. unknown/mismatched city/region) — render the server's detail. */
  #messageFor(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      const body = err.error as { detail?: string; title?: string } | null;
      if (body?.detail) return body.detail;
      if (body?.title) return body.title;
      if (err.status === 400) return 'Please check the highlighted fields and try again.';
    }
    return 'Something went wrong. Please try again.';
  }

  // ── Device-reset modal — INFO only, NO API (decision 2 / §D.2) ───
  openDeviceModal(): void {
    this.deviceModalOpen.set(true);
  }
  closeDeviceModal(): void {
    this.deviceModalOpen.set(false);
  }
  /** Pure contact-support action — closes the modal and toasts; it calls **no** API (§D.2). */
  requestDeviceReset(): void {
    this.deviceModalOpen.set(false);
    this.#toast.info('Contact support to reset your device.');
  }

  // ── Change-password modal — Firebase reset email (decision 3 / §D.3) ─
  openPasswordModal(): void {
    this.pwSent.set(false);
    this.passwordModalOpen.set(true);
  }
  closePasswordModal(): void {
    this.passwordModalOpen.set(false);
  }
  async confirmPasswordReset(): Promise<void> {
    if (this.pwSending()) return;
    const email = this.email();
    if (!email) {
      this.#toast.error('No email on file for a password reset.');
      return;
    }
    this.pwSending.set(true);
    try {
      await this.#store.requestPasswordReset(email);
      this.pwSent.set(true);
    } catch {
      this.#toast.error('Couldn’t send the reset email. Please try again.');
    } finally {
      this.pwSending.set(false);
    }
  }

  // ── Sign-out modal (§D.4) ────────────────────────────────────────
  openSignOutModal(): void {
    this.signOutModalOpen.set(true);
  }
  closeSignOutModal(): void {
    this.signOutModalOpen.set(false);
  }
  async confirmSignOut(): Promise<void> {
    if (this.signingOut()) return;
    this.signingOut.set(true);
    try {
      await this.#store.signOut();
    } finally {
      this.signingOut.set(false);
    }
  }

  // ── Field error copy (touched-only) ──────────────────────────────
  isInvalid(name: keyof typeof this.form.controls): boolean {
    const c = this.form.controls[name];
    return c.touched && c.invalid;
  }

  fieldError(name: keyof typeof this.form.controls): string {
    const c = this.form.controls[name];
    if (!c.touched || c.valid) return '';
    if (c.hasError('required')) return 'This field is required.';
    if (c.hasError('maxlength')) return 'That value is a little too long.';
    return 'Please check this field.';
  }
}
