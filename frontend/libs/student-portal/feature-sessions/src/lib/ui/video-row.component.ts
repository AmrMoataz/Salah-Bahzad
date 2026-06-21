import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ButtonComponent } from '@sb/shared/ui';
import { MySessionVideo } from '@sb/student-portal/data-access';
import { formatDuration, isPlayable, lockBadge } from './session-display';

/**
 * One **video playlist row** (the prototype's playlist item). Shows a lock/play glyph, "{n}. {title}",
 * the `MM:SS` duration, the per-video access badge (§E.3 → "0 views left" red / "Locked" grey /
 * "{n} of {m} views" green), and a **Play** button — disabled with a 🔒 when not `Playable`. A gate
 * failure for this row renders the server's `reason` `detail` inline (§D.2 / §E.5).
 */
@Component({
  selector: 'sb-video-row',
  standalone: true,
  imports: [ButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="row" [class.row--locked]="!playable()">
      <div class="row__main">
        <span class="row__icon" [attr.data-locked]="!playable()" aria-hidden="true">
          @if (playable()) {
            <svg width="18" height="18" viewBox="0 0 24 24" fill="currentColor" stroke="none">
              <polygon points="8 5 19 12 8 19 8 5" />
            </svg>
          } @else {
            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <rect x="3" y="11" width="18" height="11" rx="2" /><path d="M7 11V7a5 5 0 0 1 10 0v4" />
            </svg>
          }
        </span>

        <div class="row__meta">
          <p class="row__title">{{ num() }}. {{ video().title }}</p>
          <p class="row__duration">{{ duration() }}</p>
        </div>

        <span class="row__badge" [attr.data-variant]="badge().variant">{{ badge().label }}</span>

        @if (playable()) {
          <sb-button variant="primary" size="sm" [loading]="loading()" (clicked)="play.emit(video().id)">
            Play
          </sb-button>
        } @else {
          <sb-button variant="primary" size="sm" [disabled]="true" ariaLabel="Locked">🔒</sb-button>
        }
      </div>

      @if (error()) {
        <p class="row__error" role="alert">{{ error() }}</p>
      }
    </div>
  `,
  styles: [`
    .row {
      border: 1px solid var(--sb-border);
      border-radius: 12px;
      padding: 12px;
      background: var(--sb-surface);
    }
    .row--locked { background: var(--sb-surface-sunken); }
    .row__main { display: flex; align-items: center; gap: 12px; }

    .row__icon {
      width: 34px;
      height: 34px;
      flex-shrink: 0;
      border-radius: 9px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: var(--sb-primary-50);
      color: var(--sb-primary-600);
    }
    .row__icon[data-locked='true'] { background: var(--sb-neutral-100); color: var(--sb-text-subtle); }

    .row__meta { flex: 1; min-width: 0; }
    .row__title {
      margin: 0;
      font-weight: 700;
      font-size: 14px;
      color: var(--sb-text);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .row--locked .row__title { color: var(--sb-text-muted); }
    .row__duration { margin: 2px 0 0; font-size: 12px; color: var(--sb-text-muted); }

    .row__badge {
      font-size: var(--sb-label-md-size);
      font-weight: 700;
      padding: 3px 9px;
      border-radius: var(--sb-radius-pill);
      border: 1px solid transparent;
      white-space: nowrap;
      flex-shrink: 0;
    }
    .row__badge[data-variant='success'] { color: var(--sb-success-fg); background: var(--sb-success-bg); border-color: var(--sb-success-border); }
    .row__badge[data-variant='danger']  { color: var(--sb-danger-fg);  background: var(--sb-danger-bg);  border-color: var(--sb-danger-border); }
    .row__badge[data-variant='neutral'] { color: var(--sb-text-muted); background: var(--sb-neutral-100); border-color: var(--sb-border); }

    .row__error {
      margin: 10px 0 0;
      font-size: 13px;
      font-weight: 600;
      color: var(--sb-danger-fg);
      line-height: 1.4;
    }

    @media (max-width: 480px) {
      .row__badge { display: none; }
    }
  `],
})
export class VideoRowComponent {
  readonly video = input.required<MySessionVideo>();
  /** 1-based playlist position. */
  readonly num = input.required<number>();
  /** In-flight Play (the gate POST) for this row. */
  readonly loading = input<boolean>(false);
  /** The gate `reason` detail to render inline (null = no error). */
  readonly error = input<string | null>(null);

  /** Emits the videoId on a Play click (only fired for `Playable` rows). */
  readonly play = output<string>();

  readonly playable = computed(() => isPlayable(this.video().lockState));
  readonly duration = computed(() => formatDuration(this.video().lengthSeconds));
  readonly badge = computed(() => lockBadge(this.video()));
}
