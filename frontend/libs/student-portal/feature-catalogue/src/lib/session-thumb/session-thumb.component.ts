import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from '@angular/core';
import { ButtonComponent, TagComponent } from '@sb/shared/ui';
import { CatalogueSession } from '@sb/student-portal/data-access';

/**
 * Subject-accent palette keys (`--sb-subject-{key}-*`). **Order + the running-sum hash below mirror
 * the admin portal's `subjectAccent` exactly** (ported, not imported — student-portal can't depend on
 * an admin lib) so a given specialization tints the same colour in both portals.
 */
const SUBJECT_ACCENTS = ['blue', 'green', 'purple', 'orange', 'mint', 'pink', 'mustard', 'red'] as const;

/**
 * Presentational catalogue card (`FR-STU-CAT-001/002`, the prototype's `SessionThumb` + meta block).
 * Renders a session's thumbnail (or a tinted placeholder), specialization tag (subject-accent
 * colour, like the admin portal), an enrollment-state chip, title/description, a meta row
 * (video-count · material-count · validity), Quiz / Assignment content badges, an optional
 * **prerequisite** badge, the price (`EGP n` / **Free**), and a CTA driven by `enrollmentState` +
 * `prerequisiteSatisfied`:
 *
 * - `Enrolled` → **Open** (emits `open`).
 * - has an **unmet** prerequisite → an amber "Requires: *{title}*" badge + a dimmed **Locked**
 *   button (disabled). The server stays authoritative — this is UX (`FR-STU-CAT-002`).
 * - prerequisite **met** (or none) → a green "Prerequisite met: *{title}*" badge when there is one,
 *   and an active **Enroll** button (emits `enroll`).
 */
@Component({
  selector: 'sb-session-thumb',
  standalone: true,
  imports: [ButtonComponent, TagComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article class="thumb">
      <div class="thumb__media" [class.thumb__media--placeholder]="!session().thumbnailUrl">
        @if (session().thumbnailUrl) {
          <img class="thumb__img" [src]="session().thumbnailUrl" [alt]="session().title" loading="lazy" />
        } @else {
          <span class="thumb__placeholder-title" aria-hidden="true">{{ session().title }}</span>
        }
      </div>

      <div class="thumb__body">
        <div class="thumb__chips">
          @if (session().specializationName) {
            <sb-tag [label]="session().specializationName!" [subject]="accent()" />
          }
          @if (statusLabel()) {
            <span class="thumb__status" [attr.data-state]="session().enrollmentState">{{ statusLabel() }}</span>
          }
        </div>

        <h3 class="thumb__title">{{ session().title }}</h3>

        @if (session().description) {
          <p class="thumb__desc">{{ session().description }}</p>
        }

        <p class="thumb__meta">
          <span class="thumb__meta-item">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <circle cx="12" cy="12" r="10" /><polygon points="10 8 16 12 10 16 10 8" />
            </svg>
            <span><strong>{{ session().videoCount }}</strong> {{ session().videoCount === 1 ? 'video' : 'videos' }}</span>
          </span>
          <span class="thumb__meta-item">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" /><polyline points="14 2 14 8 20 8" />
            </svg>
            <span><strong>{{ session().materialCount }}</strong> {{ session().materialCount === 1 ? 'material' : 'materials' }}</span>
          </span>
          <span class="thumb__meta-item">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <circle cx="12" cy="12" r="10" /><polyline points="12 6 12 12 16 14" />
            </svg>
            <span>{{ validityLabel() }}</span>
          </span>
        </p>

        @if (session().hasQuiz || session().hasAssignment) {
          <div class="thumb__content">
            @if (session().hasQuiz) {
              <span class="thumb__content-tag thumb__content-tag--quiz">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                     stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <rect x="3" y="3" width="18" height="18" rx="2" /><path d="m9 12 2 2 4-4" />
                </svg>
                Quiz
              </span>
            }
            @if (session().hasAssignment) {
              <span class="thumb__content-tag thumb__content-tag--assignment">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                     stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <path d="M12 20h9" /><path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4Z" />
                </svg>
                Assignment
              </span>
            }
          </div>
        }

        @if (session().prerequisiteSessionId) {
          @if (session().prerequisiteSatisfied) {
            <span class="thumb__prereq thumb__prereq--met">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                   stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <polyline points="20 6 9 17 4 12" />
              </svg>
              Prerequisite met: {{ session().prerequisiteTitle }}
            </span>
          } @else {
            <span class="thumb__prereq">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                   stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <rect x="3" y="11" width="18" height="11" rx="2" /><path d="M7 11V7a5 5 0 0 1 10 0v4" />
              </svg>
              Requires: {{ session().prerequisiteTitle }}
            </span>
          }
        }

        <div class="thumb__footer">
          <span class="thumb__price">{{ priceLabel() }}</span>
          @if (session().enrollmentState === 'Enrolled') {
            <sb-button variant="accent" (clicked)="open.emit(session())">Open</sb-button>
          } @else if (!session().prerequisiteSatisfied) {
            <sb-button variant="primary" [disabled]="true">Locked</sb-button>
          } @else {
            <sb-button variant="primary" (clicked)="enroll.emit(session())">Enroll</sb-button>
          }
        </div>
      </div>
    </article>
  `,
  styles: [`
    .thumb {
      display: flex;
      flex-direction: column;
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-lg);
      overflow: hidden;
      box-shadow: var(--sb-shadow-sm);
      height: 100%;
    }
    .thumb__media {
      position: relative;
      aspect-ratio: 16 / 9;
      background: var(--sb-surface-sunken);
      overflow: hidden;
    }
    .thumb__media--placeholder {
      display: flex;
      align-items: center;
      justify-content: center;
      padding: var(--sb-space-4);
      background: linear-gradient(135deg, var(--sb-primary-50), var(--sb-accent-50));
    }
    .thumb__img { width: 100%; height: 100%; object-fit: cover; display: block; }
    .thumb__placeholder-title {
      font-family: var(--sb-font-display);
      font-size: 26px;
      font-weight: 700;
      color: var(--sb-primary-700);
      text-align: center;
      line-height: 1.05;
    }

    .thumb__body { display: flex; flex-direction: column; gap: 8px; padding: var(--sb-space-4); flex: 1; }
    .thumb__chips { display: flex; gap: 6px; align-items: center; flex-wrap: wrap; }
    .thumb__status {
      font-size: var(--sb-label-md-size);
      font-weight: 700;
      line-height: 1.4;
      padding: 3px 9px;
      border-radius: var(--sb-radius-pill);
      border: 1px solid transparent;
    }
    .thumb__status[data-state='Enrolled'] {
      color: var(--sb-info-fg); background: var(--sb-info-bg); border-color: var(--sb-info-border);
    }
    .thumb__status[data-state='Expired'],
    .thumb__status[data-state='Refunded'] {
      color: var(--sb-warning-fg); background: var(--sb-warning-bg); border-color: var(--sb-warning-border);
    }

    .thumb__title { font-size: var(--sb-heading-xs-size); font-weight: 800; letter-spacing: -0.2px; margin: 0; }
    .thumb__desc {
      font-size: var(--sb-body-md-size);
      color: var(--sb-text-muted);
      line-height: 1.5;
      margin: 0;
      flex: 1;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .thumb__meta {
      display: flex;
      gap: 14px;
      align-items: center;
      flex-wrap: wrap;
      font-size: var(--sb-body-sm-size);
      color: var(--sb-text-subtle);
      font-weight: 600;
      margin: 0;
    }
    .thumb__meta-item { display: inline-flex; align-items: center; gap: 5px; }
    .thumb__meta-item strong { color: var(--sb-text-muted); font-weight: 800; }

    .thumb__content { display: flex; gap: 6px; flex-wrap: wrap; }
    .thumb__content-tag {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      font-size: var(--sb-label-md-size);
      font-weight: 700;
      padding: 3px 9px;
      border-radius: var(--sb-radius-pill);
      border: 1px solid transparent;
    }
    .thumb__content-tag--quiz {
      color: var(--sb-info-fg); background: var(--sb-info-bg); border-color: var(--sb-info-border);
    }
    .thumb__content-tag--assignment {
      color: var(--sb-success-fg); background: var(--sb-success-bg); border-color: var(--sb-success-border);
    }
    .thumb__prereq {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: var(--sb-label-md-size);
      font-weight: 700;
      color: var(--sb-warning-fg);
      background: var(--sb-warning-bg);
      border: 1px solid var(--sb-warning-border);
      padding: 4px 9px;
      border-radius: var(--sb-radius-sm);
    }
    .thumb__prereq--met { color: var(--sb-success-fg); background: var(--sb-success-bg); border-color: var(--sb-success-border); }

    .thumb__footer {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--sb-space-3);
      margin-top: 6px;
      padding-top: var(--sb-space-3);
      border-top: 1px solid var(--sb-border);
    }
    .thumb__price { font-family: var(--sb-font-mono); font-weight: 700; font-size: 18px; color: var(--sb-text); }
  `],
})
export class SessionThumbComponent {
  readonly session = input.required<CatalogueSession>();

  /** Enroll a not-yet-enrolled session (opens the code modal). */
  readonly enroll = output<CatalogueSession>();
  /** Open an already-enrolled session (the session detail — S3). */
  readonly open = output<CatalogueSession>();

  /**
   * Stable subject accent per **specialization** (keyed on the name, like the admin portal) so each
   * specialization gets its own colour and stays consistent across cards and across both portals.
   */
  readonly accent = computed(() => {
    const key = this.session().specializationName ?? '';
    let hash = 0;
    for (let i = 0; i < key.length; i++) hash = (hash + key.charCodeAt(i)) % SUBJECT_ACCENTS.length;
    return SUBJECT_ACCENTS[hash];
  });

  readonly priceLabel = computed(() => (this.session().price === 0 ? 'Free' : `EGP ${this.session().price}`));

  readonly validityLabel = computed(() => {
    const days = this.session().validityDays;
    return days === 0 ? 'No expiry' : `${days}-day access`;
  });

  readonly statusLabel = computed(() => {
    switch (this.session().enrollmentState) {
      case 'Enrolled': return 'Enrolled';
      case 'Expired': return 'Expired';
      case 'Refunded': return 'Refunded';
      default: return '';
    }
  });
}
