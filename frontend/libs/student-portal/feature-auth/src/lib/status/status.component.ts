import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { Router } from '@angular/router';
import { StudentAuthStore, StudentBlockReason } from '@sb/student-portal/data-access';

interface StatusMeta {
  mascot: string;
  title: string;
  /** Used when the server didn't send a readable `detail`. */
  fallbackBody: string;
}

// Copy is anchored to the frozen reasons (§1.4) and the prototype's AUTH: REGISTER pending state +
// PROFILE one-device wording. Mascots are the prototype's named poses.
const META: Record<StudentBlockReason, StatusMeta> = {
  account_pending: {
    mascot: 'salah-passed.png',
    title: 'Your account is pending approval',
    fallbackBody:
      'Salah will review and approve it — you’ll get an email when you can sign in. Approvals usually take less than a day.',
  },
  account_rejected: {
    mascot: 'salah-failed.png',
    title: 'Your registration wasn’t approved',
    fallbackBody: 'Your registration was not approved.',
  },
  account_inactive: {
    mascot: 'salah-failed.png',
    title: 'Your account is inactive',
    fallbackBody: 'Your account has been deactivated. Please contact support to restore access.',
  },
  device_not_recognized: {
    mascot: 'salah-prerequisite.png',
    title: 'This device isn’t recognised',
    fallbackBody:
      'Only one device can access content. To switch devices, contact support to reset the binding.',
  },
};

/**
 * Terminal sign-in states driven by `StudentAuthStore.status()` — pending / rejected (shows the
 * server's `RejectionReason`) / inactive / device_not_recognized. Reached only via `statusGuard`
 * (a parked reason); "Back to sign in" clears it and returns to `/login`.
 */
@Component({
  selector: 'sb-student-status',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (view(); as v) {
      <div class="status">
        <div class="status__card">
          <img class="status__mascot" [src]="'/assets/' + v.mascot" alt="" aria-hidden="true" />
          <h1 class="status__title">{{ v.title }}</h1>

          @if (reason() === 'account_rejected') {
            @if (detail()) {
              <div class="status__reason" role="alert">
                <span class="status__reason-label">Reason</span>
                <span>{{ detail() }}</span>
              </div>
            }
            <p class="status__body">If you think this is a mistake, contact support.</p>
          } @else {
            <p class="status__body">{{ detail() || v.fallbackBody }}</p>
          }

          <button type="button" class="status__btn" (click)="backToLogin()">Back to sign in</button>
        </div>
      </div>
    }
  `,
  styles: [`
    .status {
      min-height: 100dvh;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 32px 22px;
      background: var(--sb-neutral-25);
    }

    .status__card {
      width: 100%;
      max-width: 420px;
      text-align: center;
      animation: sb-pop var(--sb-timing-slow) var(--sb-easing-out);
    }

    @keyframes sb-pop {
      from { opacity: 0; transform: translateY(8px); }
      to { opacity: 1; transform: translateY(0); }
    }

    .status__mascot { width: 140px; height: auto; margin: 0 auto 10px; display: block; }

    .status__title {
      font-size: 24px;
      font-weight: 800;
      letter-spacing: -0.3px;
      margin: 0 0 12px;
    }

    .status__body {
      color: var(--sb-neutral-600);
      font-size: 15px;
      line-height: 1.55;
      margin: 0 0 20px;
    }

    .status__reason {
      display: flex;
      flex-direction: column;
      gap: 4px;
      text-align: left;
      background: var(--sb-danger-bg);
      border: 1px solid var(--sb-danger-border);
      color: var(--sb-danger-fg);
      border-radius: var(--sb-radius-md);
      padding: var(--sb-space-3) var(--sb-space-4);
      margin: 0 0 16px;
      font-size: var(--sb-body-md-size);
      font-weight: 600;
    }
    .status__reason-label {
      font-size: 11px;
      font-weight: 800;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      opacity: 0.8;
    }

    .status__btn {
      width: 100%;
      min-height: 48px;
      border: none;
      border-radius: var(--sb-radius-md);
      background: var(--sb-primary);
      color: var(--sb-on-primary);
      font-family: inherit;
      font-size: 15px;
      font-weight: 700;
      cursor: pointer;
      transition: background var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .status__btn:hover { background: var(--sb-primary-hover); }
    .status__btn:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }
  `],
})
export class StatusComponent {
  readonly #authStore = inject(StudentAuthStore);
  readonly #router = inject(Router);

  readonly reason = this.#authStore.status;
  readonly detail = this.#authStore.statusDetail;
  readonly view = computed(() => {
    const reason = this.reason();
    return reason ? META[reason] : null;
  });

  constructor() {
    // Defensive: a parked status is required to be here. If it's cleared, leave for /login.
    effect(() => {
      if (this.reason() === null) void this.#router.navigate(['/login']);
    });
  }

  backToLogin(): void {
    this.#authStore.clearStatus();
    void this.#router.navigate(['/login']);
  }
}
