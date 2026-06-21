import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { accentFor, mascotFor } from './session-display';

/**
 * The **session thumbnail tile** — used in the spotlight hero, the My-Sessions list rows
 * (`compact`), and the session-detail hero.
 *
 * - When {@link thumbnailUrl} is present (a signed R2 image) it renders that, cover-cropped.
 * - Otherwise it renders the **same no-image placeholder as the catalogue `SessionThumb`** (a soft
 *   `primary → accent` gradient with the title centered in the brand display font) so an
 *   image-less session looks identical across the catalogue, My Sessions, and the session detail.
 *
 * `compact` is the small square used in list rows (image or a single subject-accent block + mascot).
 * The `num` / `grade` inputs are retained for the callers' convenience but no longer surface on the
 * placeholder (the catalogue placeholder shows the title only).
 */
@Component({
  selector: 'sb-session-tile',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tile" [class.tile--compact]="compact()" [attr.data-accent]="accent()">
      @if (thumbnailUrl()) {
        <img class="tile__img" [src]="thumbnailUrl()" [alt]="title()" loading="lazy" />
      } @else if (compact()) {
        <div class="tile__mini">
          <img class="tile__mini-mascot" [src]="mascot()" alt="" aria-hidden="true" />
        </div>
      } @else {
        <div class="tile__placeholder">
          <span class="tile__placeholder-title" aria-hidden="true">{{ title() }}</span>
        </div>
      }
    </div>
  `,
  styles: [`
    .tile { width: 100%; border-radius: 14px; overflow: hidden; }
    .tile__img { width: 100%; height: 100%; object-fit: cover; display: block; aspect-ratio: 16 / 9; }

    /* ── No-image placeholder (matches the catalogue SessionThumb exactly) ── */
    .tile__placeholder {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 100%;
      aspect-ratio: 16 / 9;
      padding: var(--sb-space-4);
      container-type: inline-size;
      background: linear-gradient(135deg, var(--sb-primary-50), var(--sb-accent-50));
    }
    .tile__placeholder-title {
      font-family: var(--sb-font-display);
      font-weight: 700;
      /* Tracks the catalogue's 26px at ~card width; scales with the tile (hero vs detail). */
      font-size: clamp(18px, 8cqw, 30px);
      line-height: 1.05;
      text-align: center;
      color: var(--sb-primary-700);
    }

    /* ── Compact list-row square ── */
    .tile--compact { width: 52px; height: 52px; border-radius: 12px; flex-shrink: 0; }
    .tile--compact .tile__img { aspect-ratio: 1; height: 52px; }
    .tile__mini {
      width: 52px;
      height: 52px;
      display: flex;
      align-items: flex-end;
      justify-content: center;
      overflow: hidden;
      background:
        radial-gradient(120% 120% at 50% 120%,
          var(--sb-subject-blue-bg), var(--sb-surface-sunken));
    }
    .tile[data-accent='green'] .tile__mini  { background: var(--sb-subject-green-bg); }
    .tile[data-accent='purple'] .tile__mini { background: var(--sb-subject-purple-bg); }
    .tile[data-accent='orange'] .tile__mini { background: var(--sb-subject-orange-bg); }
    .tile[data-accent='mint'] .tile__mini   { background: var(--sb-subject-mint-bg); }
    .tile[data-accent='pink'] .tile__mini   { background: var(--sb-subject-pink-bg); }
    .tile[data-accent='mustard'] .tile__mini{ background: var(--sb-subject-mustard-bg); }
    .tile[data-accent='red'] .tile__mini    { background: var(--sb-subject-red-bg); }
    .tile[data-accent='blue'] .tile__mini   { background: var(--sb-subject-blue-bg); }
    .tile__mini-mascot { height: 46px; width: auto; object-fit: contain; object-position: bottom; }
  `],
})
export class SessionTileComponent {
  readonly title = input.required<string>();
  readonly grade = input<string | null>(null);
  readonly thumbnailUrl = input<string | null>(null);
  /** Specialization name — drives the accent tint + the mascot pose. */
  readonly subject = input<string | null>(null);
  /** Optional sequence number for the "Session {num}" eyebrow (omit to show just the title). */
  readonly num = input<number | null>(null);
  /** Small square mode for the list rows. */
  readonly compact = input<boolean>(false);

  readonly accent = computed(() => accentFor(this.subject()));
  readonly mascot = computed(() => mascotFor(this.subject()));
}
