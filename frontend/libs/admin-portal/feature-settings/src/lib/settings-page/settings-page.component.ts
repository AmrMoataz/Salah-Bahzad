import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthStore } from '@sb/shared/data-access';
import {
  AlertComponent,
  AvatarComponent,
  ButtonComponent,
  FormFieldComponent,
  StatusPillComponent,
} from '@sb/shared/ui';

/**
 * Settings screen (FR-ADM-SET-001, mockup `scrSettings`): editable display name, Firebase-delegated
 * password reset, a tenant-settings placeholder, and sign-out. Email and password are owned by the
 * authentication provider (Firebase) — the platform stores neither (FR-PLAT-AUTH-004/009).
 */
@Component({
  selector: 'sb-settings-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    ButtonComponent,
    AlertComponent,
    AvatarComponent,
    StatusPillComponent,
    FormFieldComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="settings">
      <header class="settings__head">
        <h1 class="settings__title">Settings</h1>
        <p class="settings__subtitle">Your profile, security &amp; tenant preferences</p>
      </header>

      <div class="settings__grid">
        <!-- Left column -->
        <div class="settings__col">
          <!-- Profile -->
          <section class="card">
            <h2 class="card__title">Profile</h2>
            <div class="card__body">
              <div class="settings__identity">
                <sb-avatar size="xl" [initials]="initials()" [subject]="accent()" />
                <div>
                  <div class="settings__name">{{ displayName() }}</div>
                  <div class="settings__email">{{ email() }}</div>
                  <sb-status-pill [variant]="role() === 'Teacher' ? 'info' : 'success'">
                    {{ role() ?? '—' }}
                  </sb-status-pill>
                </div>
              </div>

              <form [formGroup]="profileForm" (ngSubmit)="saveProfile()" class="settings__form" novalidate>
                <sb-form-field label="Display name" fieldId="profile-name" [error]="nameError" [required]="true">
                  <input
                    id="profile-name"
                    type="text"
                    formControlName="displayName"
                    class="sb-input"
                    autocomplete="name"
                    placeholder="Your name"
                  />
                </sb-form-field>

                @if (saveState() === 'saved') {
                  <sb-alert variant="success" title="Saved">Your display name has been updated.</sb-alert>
                } @else if (saveState() === 'error') {
                  <sb-alert variant="danger" title="Couldn’t save">Please try again in a moment.</sb-alert>
                }

                <div>
                  <sb-button variant="primary" type="submit" [loading]="saveState() === 'saving'">
                    Save profile
                  </sb-button>
                </div>
              </form>

              <sb-alert variant="info" title="Managed by your sign-in provider">
                Your email address and password are managed by your authentication provider (Firebase) and
                can’t be changed here. Use “Send password reset email” below to change your password.
              </sb-alert>
            </div>
          </section>

          <!-- Security -->
          <section class="card">
            <h2 class="card__title">Security</h2>
            <div class="card__body">
              <p class="settings__text">
                Passwords are managed by Firebase — the platform never stores them. We’ll email
                <strong>{{ email() }}</strong> a secure link to set a new password.
              </p>

              @if (resetState() === 'sent') {
                <sb-alert variant="success" title="Email sent">
                  Check your inbox for the password-reset link.
                </sb-alert>
              } @else if (resetState() === 'error') {
                <sb-alert variant="danger" title="Couldn’t send the email">
                  Please try again in a moment.
                </sb-alert>
              }

              <div>
                <sb-button
                  variant="secondary"
                  [loading]="resetState() === 'sending'"
                  (clicked)="sendReset()"
                >
                  Send password reset email
                </sb-button>
              </div>
            </div>
          </section>
        </div>

        <!-- Right column -->
        <div class="settings__col">
          <!-- Tenant settings (placeholder) -->
          <section class="card">
            <h2 class="card__title">Tenant settings</h2>
            <div class="card__placeholder">
              <img src="/assets/salah-mascot.png" alt="" class="settings__mascot" />
              <div class="settings__coming">Coming soon</div>
              <p class="settings__text settings__text--center">
                Tenant-wide configuration (branding, billing, integrations) will live here in a future release.
              </p>
            </div>
          </section>

          <!-- Session -->
          <section class="card">
            <h2 class="card__title">Session</h2>
            <div class="card__body">
              <p class="settings__text">
                Signed in as <strong>{{ displayName() }}</strong> · {{ role() === 'Teacher' ? 'Owner' : 'Assistant' }}
              </p>
              <div>
                <sb-button variant="danger-ghost" [loading]="signingOut()" (clicked)="signOut()">
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                       stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                    <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9"/>
                  </svg>
                  Sign out
                </sb-button>
              </div>
            </div>
          </section>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .settings { display: flex; flex-direction: column; gap: var(--sb-space-4); }

    .settings__head { margin-bottom: var(--sb-space-1); }
    .settings__title {
      margin: 0 0 var(--sb-space-1);
      font-size: var(--sb-heading-xl-size);
      font-weight: 800;
      letter-spacing: -0.01em;
      color: var(--sb-text);
    }
    .settings__subtitle { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    .settings__grid {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
      gap: var(--sb-space-4);
      align-items: start;
    }
    @media (max-width: 760px) {
      .settings__grid { grid-template-columns: minmax(0, 1fr); }
    }

    .settings__col { display: flex; flex-direction: column; gap: var(--sb-space-4); }

    .card {
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-lg);
      padding: var(--sb-space-5);
    }
    .card__title {
      margin: 0 0 var(--sb-space-4);
      font-size: var(--sb-heading-sm-size);
      font-weight: 700;
      color: var(--sb-text);
    }
    .card__body { display: flex; flex-direction: column; gap: var(--sb-space-4); }
    .card__placeholder {
      text-align: center;
      padding: var(--sb-space-4) 0;
      color: var(--sb-text-muted);
    }

    .settings__identity { display: flex; align-items: center; gap: var(--sb-space-4); }
    .settings__name { font-weight: 800; font-size: var(--sb-heading-xs-size); color: var(--sb-text); }
    .settings__email {
      font-size: var(--sb-body-sm-size);
      color: var(--sb-text-muted);
      margin-bottom: var(--sb-space-2);
    }

    .settings__form { display: flex; flex-direction: column; gap: var(--sb-space-3); }

    .settings__text { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); line-height: 1.5; }
    .settings__text--center { max-width: 280px; margin: 0 auto; }

    .settings__mascot { width: 84px; height: auto; margin-bottom: var(--sb-space-2); opacity: 0.9; }
    .settings__coming { font-weight: 700; color: var(--sb-text); margin-bottom: var(--sb-space-1); }

    .sb-input {
      width: 100%;
      height: 40px;
      padding: 0 var(--sb-space-3);
      border: 1px solid var(--sb-border-strong);
      border-radius: var(--sb-radius-md);
      font-size: var(--sb-body-md-size);
      font-family: var(--sb-font-sans);
      color: var(--sb-text);
      background: var(--sb-surface);
      outline: none;
      transition: border-color var(--sb-timing) var(--sb-easing-standard),
                  box-shadow var(--sb-timing) var(--sb-easing-standard);
    }
    .sb-input:focus { border-color: var(--sb-primary); box-shadow: var(--sb-shadow-focus); }
  `],
})
export class SettingsPageComponent {
  readonly #auth = inject(AuthStore);
  readonly #fb = inject(FormBuilder);

  readonly #staff = this.#auth.staff;
  readonly role = this.#auth.role;

  readonly displayName = computed(() => this.#staff()?.displayName ?? '—');
  readonly email = computed(() => this.#staff()?.email ?? '');
  readonly accent = computed(() => (this.role() === 'Teacher' ? 'blue' : 'pink'));
  readonly initials = computed(() => {
    const name = this.#staff()?.displayName ?? '';
    const letters = name
      .split(' ')
      .filter(Boolean)
      .map((w) => w[0])
      .slice(0, 2)
      .join('');
    return (letters || 'SB').toUpperCase();
  });

  readonly profileForm = this.#fb.group({
    displayName: ['', [Validators.required, Validators.maxLength(200)]],
  });

  readonly saveState = signal<'idle' | 'saving' | 'saved' | 'error'>('idle');
  readonly resetState = signal<'idle' | 'sending' | 'sent' | 'error'>('idle');
  readonly signingOut = signal(false);

  constructor() {
    // Seed the field from the signed-in identity once it's available, without clobbering edits.
    effect(() => {
      const name = this.#staff()?.displayName;
      const control = this.profileForm.controls.displayName;
      if (name && control.pristine) {
        control.setValue(name, { emitEvent: false });
      }
    });
  }

  get nameError(): string {
    const c = this.profileForm.controls.displayName;
    if (!c.touched) return '';
    if (c.hasError('required')) return 'Your name is required.';
    if (c.hasError('maxlength')) return 'Name is too long.';
    return '';
  }

  async saveProfile(): Promise<void> {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }
    this.saveState.set('saving');
    try {
      await this.#auth.updateDisplayName(this.profileForm.getRawValue().displayName!.trim());
      this.profileForm.markAsPristine();
      this.saveState.set('saved');
    } catch {
      this.saveState.set('error');
    }
  }

  async sendReset(): Promise<void> {
    this.resetState.set('sending');
    try {
      await this.#auth.requestPasswordReset();
      this.resetState.set('sent');
    } catch {
      this.resetState.set('error');
    }
  }

  async signOut(): Promise<void> {
    this.signingOut.set(true);
    try {
      await this.#auth.signOut();
    } finally {
      this.signingOut.set(false);
    }
  }
}
