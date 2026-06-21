import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { CatalogueService, CatalogueSession, Enrollment } from '@sb/student-portal/data-access';
import { SessionThumbComponent } from '../session-thumb/session-thumb.component';
import { EnrollModalComponent } from '../enroll-modal/enroll-modal.component';

interface SpecChip {
  id: string;
  name: string;
}

/**
 * The catalogue screen (`FR-STU-CAT-001/002`, the prototype's `CATALOGUE` banner). Loads the
 * tenant's published sessions, renders them as a responsive `SessionThumb` grid, and offers a
 * **client-side specialization chip-bar** (derived from the loaded set — the happy path fetches with
 * no params). Shows a loading skeleton, a **mascot empty state** when nothing matches, and opens the
 * {@link EnrollModalComponent} from a card's **Enroll** CTA or the shell's **Redeem FAB** (the FAB
 * routes here with `openRedeem` so the boundary stays intact). On a successful redeem the catalogue
 * refetches and the just-enrolled card flips to `Enrolled`/Open.
 */
@Component({
  selector: 'sb-catalogue',
  standalone: true,
  imports: [SessionThumbComponent, EnrollModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="cat">
      <header class="cat__head">
        <div>
          <h1 class="cat__title">Catalogue</h1>
          <p class="cat__lede">Browse published sessions and redeem a code to start learning.</p>
        </div>
        <button type="button" class="cat__redeem" (click)="openRedeemModal()">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="2.2" stroke-linecap="round" aria-hidden="true">
            <path d="M12 5v14M5 12h14" />
          </svg>
          Redeem a code
        </button>
      </header>

      @if (specChips().length > 1) {
        <div class="cat__filters" role="group" aria-label="Filter by specialization">
          <span class="cat__filters-label" aria-hidden="true">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3" />
            </svg>
            Filter
          </span>
          <button
            type="button"
            class="cat__chip"
            [class.is-active]="selectedSpec() === null"
            [attr.aria-pressed]="selectedSpec() === null"
            (click)="selectSpec(null)"
          >All</button>
          @for (chip of specChips(); track chip.id) {
            <button
              type="button"
              class="cat__chip"
              [class.is-active]="selectedSpec() === chip.id"
              [attr.aria-pressed]="selectedSpec() === chip.id"
              (click)="selectSpec(chip.id)"
            >{{ chip.name }}</button>
          }
        </div>
      }

      @if (loading()) {
        <div class="cat__grid" aria-hidden="true">
          @for (i of skeletons; track i) {
            <div class="cat__skeleton"></div>
          }
        </div>
      } @else if (error()) {
        <div class="cat__empty">
          <img class="cat__empty-art" src="/assets/salah-mascot.png" alt="" aria-hidden="true" />
          <h2 class="cat__empty-title">Couldn’t load the catalogue</h2>
          <p class="cat__empty-copy">{{ error() }}</p>
          <button type="button" class="cat__redeem" (click)="reload()">Try again</button>
        </div>
      } @else if (filtered().length === 0) {
        <div class="cat__empty">
          <img class="cat__empty-art" src="/assets/salah-mascot.png" alt="" aria-hidden="true" />
          <h2 class="cat__empty-title">Nothing here… yet</h2>
          <p class="cat__empty-copy">
            @if (sessions().length === 0) {
              No published sessions yet. Check back soon — there’s plenty on the way.
            } @else {
              No sessions match this filter. Try another specialization.
            }
          </p>
          @if (selectedSpec() !== null) {
            <button type="button" class="cat__redeem" (click)="selectSpec(null)">Clear filter</button>
          }
        </div>
      } @else {
        <ul class="cat__grid" aria-label="Published sessions">
          @for (session of filtered(); track session.id) {
            <li class="cat__cell">
              <sb-session-thumb
                [session]="session"
                (enroll)="openEnroll($event)"
                (open)="openSession($event)"
              />
            </li>
          }
        </ul>
      }
    </section>

    <sb-enroll-modal
      [open]="modalOpen()"
      [sessionId]="modalSessionId()"
      [sessionTitle]="modalTitle()"
      (close)="closeModal()"
      (enrolled)="onEnrolled($event)"
      (goToSession)="openSessionById($event.sessionId)"
    />
  `,
  styles: [`
    .cat { display: flex; flex-direction: column; gap: var(--sb-space-5); }

    .cat__head { display: flex; align-items: flex-end; justify-content: space-between; gap: var(--sb-space-4); flex-wrap: wrap; }
    .cat__title { font-family: var(--sb-font-display); font-size: 36px; font-weight: 700; color: var(--sb-primary-700); line-height: 0.9; margin: 0 0 4px; }
    .cat__lede { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); }

    .cat__redeem {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      min-height: 40px;
      padding: 0 16px;
      border: none;
      border-radius: var(--sb-radius-md);
      background: var(--sb-primary);
      color: var(--sb-on-primary);
      font-family: inherit;
      font-weight: 700;
      font-size: var(--sb-body-md-size);
      cursor: pointer;
      transition: background var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .cat__redeem:hover { background: var(--sb-primary-hover); }
    .cat__redeem:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }

    .cat__filters {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      flex-wrap: wrap;
      align-self: flex-start;
      width: fit-content;
      max-width: 100%;
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-pill);
      box-shadow: var(--sb-shadow-sm);
      padding: 5px 8px 5px 12px;
    }
    .cat__filters-label {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      color: var(--sb-text-subtle);
      font-weight: 700;
      font-size: var(--sb-body-sm-size);
      padding-right: 4px;
    }
    .cat__chip {
      min-height: 34px;
      padding: 0 16px;
      border: none;
      border-radius: var(--sb-radius-pill);
      background: transparent;
      color: var(--sb-text-muted);
      font-family: inherit;
      font-weight: 700;
      font-size: var(--sb-body-sm-size);
      cursor: pointer;
      transition: background var(--sb-timing-fast) var(--sb-easing-standard),
                  color var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .cat__chip:hover { background: var(--sb-primary-50); color: var(--sb-primary-600); }
    .cat__chip.is-active,
    .cat__chip.is-active:hover { background: var(--sb-primary); color: var(--sb-on-primary); }
    .cat__chip:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }

    .cat__grid {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(290px, 1fr));
      gap: var(--sb-space-4);
    }
    .cat__cell { display: flex; }
    .cat__cell sb-session-thumb { width: 100%; }
    .cat__skeleton { aspect-ratio: 16 / 11; border-radius: var(--sb-radius-lg); background: var(--sb-surface-sunken); animation: cat-pulse 1.3s var(--sb-easing-standard) infinite; }
    @keyframes cat-pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.55; } }

    .cat__empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      gap: var(--sb-space-2);
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-xl);
      padding: var(--sb-space-12) var(--sb-space-5);
    }
    .cat__empty-art { width: 150px; height: auto; margin-bottom: var(--sb-space-2); }
    .cat__empty-title { margin: 0; font-size: var(--sb-heading-sm-size); font-weight: 800; }
    .cat__empty-copy { margin: 0 0 var(--sb-space-2); max-width: 360px; color: var(--sb-text-muted); line-height: 1.5; }

    @media (max-width: 560px) {
      .cat__grid { grid-template-columns: 1fr; }
      .cat__title { font-size: 30px; }
    }
  `],
})
export class CatalogueComponent {
  readonly #catalogue = inject(CatalogueService);
  readonly #router = inject(Router);

  /** Set via route data on `/redeem` (the shell's Redeem FAB target) — auto-opens a blank modal. */
  readonly openRedeem = input(false);

  readonly skeletons = Array.from({ length: 6 }, (_, i) => i);

  readonly sessions = signal<CatalogueSession[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly selectedSpec = signal<string | null>(null);

  readonly modalOpen = signal(false);
  readonly modalSessionId = signal<string | null>(null);
  readonly modalTitle = signal<string | null>(null);

  /** Distinct specializations across the loaded set (the client-side chip-bar source). */
  readonly specChips = computed<SpecChip[]>(() => {
    const seen = new Map<string, string>();
    for (const s of this.sessions()) {
      if (s.specializationId && !seen.has(s.specializationId)) {
        seen.set(s.specializationId, s.specializationName ?? 'Other');
      }
    }
    return [...seen].map(([id, name]) => ({ id, name }));
  });

  readonly filtered = computed(() => {
    const spec = this.selectedSpec();
    return spec === null ? this.sessions() : this.sessions().filter((s) => s.specializationId === spec);
  });

  constructor() {
    this.reload();
    // The Redeem FAB routes to `/redeem` (data: openRedeem) — open a blank (un-scoped) modal.
    effect(() => {
      if (this.openRedeem()) this.openRedeemModal();
    });
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.#catalogue.catalogue().subscribe({
      next: (list) => {
        this.sessions.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Please check your connection and try again.');
        this.loading.set(false);
      },
    });
  }

  selectSpec(id: string | null): void {
    this.selectedSpec.set(id);
  }

  openEnroll(session: CatalogueSession): void {
    this.modalSessionId.set(session.id);
    this.modalTitle.set(session.title);
    this.modalOpen.set(true);
  }

  openRedeemModal(): void {
    this.modalSessionId.set(null);
    this.modalTitle.set(null);
    this.modalOpen.set(true);
  }

  closeModal(): void {
    this.modalOpen.set(false);
  }

  /** A successful redeem — refetch so the just-enrolled card flips to `Enrolled`/Open. */
  onEnrolled(_enrollment: Enrollment): void {
    this.reload();
  }

  openSession(session: CatalogueSession): void {
    this.openSessionById(session.id);
  }

  /**
   * Route to the session detail (S3). That route doesn't exist yet, so the app's wildcard lands the
   * student back on home until S3 ships — a soft placeholder, no broken build (plan F4/F5).
   */
  openSessionById(sessionId: string): void {
    this.closeModal();
    void this.#router.navigate(['/sessions', sessionId]);
  }
}
