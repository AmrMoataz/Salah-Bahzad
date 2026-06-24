import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { MyPlanRecent } from '@sb/student-portal/data-access';
import { accentBg, accentFg, addedAgo, homeIconSvg, subjectAccent } from '../home-presentation';

/**
 * One row of the "Recently enrolled" list — a leading subject-tinted book icon, the session title +
 * "Added N days ago" (computed client-side from `enrolledAtUtc` — the DTO carries no relative time),
 * and a trailing chevron. Tapping the row routes to the S3 session detail (`/sessions/{id}`) — a route
 * string, never an import of `feature-sessions` (module boundary).
 */
@Component({
  selector: 'sb-home-recent-tile',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="tile" [routerLink]="['/sessions', recent().sessionId]">
      <span class="tile__icon" aria-hidden="true" [style.background]="iconBg()" [style.color]="iconFg()"
            [innerHTML]="iconHtml()"></span>
      <span class="tile__body">
        <span class="tile__title">{{ recent().title }}</span>
        <span class="tile__ago">{{ addedText() }}</span>
      </span>
      <span class="tile__chev" aria-hidden="true" [innerHTML]="chevronHtml()"></span>
    </a>
  `,
  styles: [`
    :host { display: block; }
    .tile {
      display: flex;
      align-items: center;
      gap: var(--sb-space-3);
      padding: var(--sb-space-3);
      border-radius: var(--sb-radius-md);
      text-decoration: none;
      color: inherit;
      transition: background var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .tile:hover { background: var(--sb-surface-sunken); text-decoration: none; color: inherit; }
    .tile:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }
    .tile__icon {
      flex-shrink: 0;
      width: 40px; height: 40px; border-radius: var(--sb-radius-md);
      display: inline-flex; align-items: center; justify-content: center;
    }
    .tile__body { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 1px; }
    .tile__title {
      font-size: var(--sb-body-md-size); font-weight: 700; color: var(--sb-text); line-height: 1.3;
      white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
    }
    .tile__ago { font-size: var(--sb-body-sm-size); color: var(--sb-text-subtle); font-weight: 600; }
    .tile__chev { flex-shrink: 0; color: var(--sb-text-subtle); display: inline-flex; }
  `],
})
export class HomeRecentTileComponent {
  readonly recent = input.required<MyPlanRecent>();

  readonly #sanitizer = inject(DomSanitizer);
  readonly #accent = computed(() => subjectAccent(this.recent().specializationName));

  readonly addedText = computed(() => addedAgo(this.recent().enrolledAtUtc));
  readonly iconBg = computed(() => accentBg(this.#accent()));
  readonly iconFg = computed(() => accentFg(this.#accent()));
  readonly iconHtml = computed<SafeHtml>(() =>
    this.#sanitizer.bypassSecurityTrustHtml(homeIconSvg('book', 20)),
  );
  readonly chevronHtml = computed<SafeHtml>(() =>
    this.#sanitizer.bypassSecurityTrustHtml(homeIconSvg('chevron', 18)),
  );
}
