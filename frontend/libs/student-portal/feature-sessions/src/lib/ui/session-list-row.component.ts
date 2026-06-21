import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ButtonComponent, PillVariant, TagComponent } from '@sb/shared/ui';
import { MySession } from '@sb/student-portal/data-access';
import { accentFor, ctaLabel, expiryInfo, videosLabel } from './session-display';

/**
 * One row of the My-Sessions **divided list** (the prototype's `spotlightRest` row): a compact
 * {@link SessionTileComponent} + title + specialization tag, a "{watched} of {n} videos" line with an
 * **expiry chip**, the completion-state pill + progress percent, and a Start/Continue/Review CTA
 * (§E.2). The CTA + the row navigate to the session detail.
 */
@Component({
  selector: 'sb-session-list-row',
  standalone: true,
  imports: [ButtonComponent, TagComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="lrow">
      <span class="lrow__accent" [attr.data-accent]="subjectAccent()" aria-hidden="true">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z" /><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z" />
        </svg>
      </span>

      <div class="lrow__meta">
        <div class="lrow__title-line">
          <span class="lrow__title">{{ session().title }}</span>
          @if (session().specializationName) {
            <sb-tag [label]="session().specializationName!" [subject]="subjectAccent()" />
          }
        </div>
        <div class="lrow__sub">
          <span>{{ videos() }} · {{expiry().label}}</span>
        </div>
      </div>

      <div class="lrow__controls">
        <span class="lrow__pill" [attr.data-variant]="statePill().variant">
          <span class="lrow__dot" aria-hidden="true"></span>{{ statePill().label }}
        </span>
        <span class="lrow__pct">{{ session().progressPercent }}%</span>
        <sb-button variant="secondary" size="sm" (clicked)="open.emit(session().id)">{{ cta() }}</sb-button>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; }
    .lrow {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 10px 14px;
      padding: 14px 16px;
      container-type: inline-size;
    }

    .lrow__accent {
      width: 38px;
      height: 38px;
      border-radius: 10px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      background: var(--sb-subject-blue-bg);
      color: var(--sb-subject-blue-deep);
    }
    .lrow__accent[data-accent='green']  { background: var(--sb-subject-green-bg);  color: var(--sb-subject-green-deep); }
    .lrow__accent[data-accent='purple'] { background: var(--sb-subject-purple-bg); color: var(--sb-subject-purple-deep); }
    .lrow__accent[data-accent='orange'] { background: var(--sb-subject-orange-bg); color: var(--sb-subject-orange-deep); }
    .lrow__accent[data-accent='mint']   { background: var(--sb-subject-mint-bg);   color: var(--sb-subject-mint-deep); }
    .lrow__accent[data-accent='pink']   { background: var(--sb-subject-pink-bg);   color: var(--sb-subject-pink-deep); }
    .lrow__accent[data-accent='mustard']{ background: var(--sb-subject-mustard-bg);color: var(--sb-subject-mustard-deep); }
    .lrow__accent[data-accent='red']    { background: var(--sb-subject-red-bg);    color: var(--sb-subject-red-deep); }

    .lrow__meta { flex: 1 1 180px; min-width: 0; }
    .lrow__title-line { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .lrow__title { font-weight: 800; font-size: 15px; letter-spacing: -0.15px; color: var(--sb-text); }
    .lrow__sub { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; margin-top: 4px; font-size: 12px; color: var(--sb-text-subtle); }

    .lrow__chip {
      font-size: var(--sb-label-md-size);
      font-weight: 700;
      padding: 2px 8px;
      border-radius: var(--sb-radius-pill);
      border: 1px solid transparent;
    }
    .lrow__chip[data-variant='danger']  { color: var(--sb-danger-fg);  background: var(--sb-danger-bg);  border-color: var(--sb-danger-border); }
    .lrow__chip[data-variant='warning'] { color: var(--sb-warning-fg); background: var(--sb-warning-bg); border-color: var(--sb-warning-border); }
    .lrow__chip[data-variant='neutral'] { color: var(--sb-text-muted); background: var(--sb-neutral-100); border-color: var(--sb-border); }

    .lrow__pill {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 3px 10px;
      border-radius: var(--sb-radius-pill);
      border: 1px solid transparent;
      font-size: var(--sb-body-sm-size);
      font-weight: 700;
      white-space: nowrap;
      flex-shrink: 0;
    }
    .lrow__dot { width: 6px; height: 6px; border-radius: 50%; background: currentColor; flex-shrink: 0; }
    .lrow__pill[data-variant='success'] { color: var(--sb-success-fg); background: var(--sb-success-bg); border-color: var(--sb-success-border); }
    .lrow__pill[data-variant='info']    { color: var(--sb-info-fg);    background: var(--sb-info-bg);    border-color: var(--sb-info-border); }
    .lrow__pill[data-variant='neutral'] { color: var(--sb-text-muted); background: var(--sb-neutral-100); border-color: var(--sb-border); }

    .lrow__pct {
      font-family: var(--sb-font-mono);
      font-weight: 800;
      font-size: 13px;
      color: var(--sb-text-muted);
      width: 42px;
      text-align: right;
      flex-shrink: 0;
    }

    .lrow__controls {
      display: flex;
      align-items: center;
      gap: 10px;
      flex-shrink: 0;
      margin-left: auto;        /* right-aligned on the wide single-row layout */
    }

    /* Narrow rows (phone/tablet): the pill + percent + CTA drop to their own full-width line, aligned
       under the title with the CTA pushed right — keyed off the ROW's width, not a guessed viewport. */
    @container (max-width: 440px) {
      .lrow__controls {
        flex-basis: 100%;
        margin-left: 0;
        padding-left: 52px;
      }
      .lrow__controls sb-button { margin-left: auto; }
    }
  `],
})
export class SessionListRowComponent {
  readonly session = input.required<MySession>();

  /** Mirrors the catalogue's subject-accent so the tag + accent box tint consistently. */
  readonly subjectAccent = computed(() => accentFor(this.session().specializationName));
  readonly videos = computed(() => videosLabel(this.session().videosWatched, this.session().videoCount));
  readonly expiry = computed(() => expiryInfo(this.session().expiresAtUtc, this.session().isExpired));
  readonly cta = computed(() => ctaLabel(this.session().state));
  readonly statePill = computed<{ label: string; variant: PillVariant }>(() => {
    switch (this.session().state) {
      case 'Completed':
        return { label: 'Completed', variant: 'success' };
      case 'InProgress':
        return { label: 'In progress', variant: 'info' };
      default:
        return { label: 'Not started', variant: 'neutral' };
    }
  });

  /** Emits the session id to open the detail. */
  readonly open = output<string>();
}
