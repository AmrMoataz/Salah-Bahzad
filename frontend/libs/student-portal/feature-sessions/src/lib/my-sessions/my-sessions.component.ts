import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ButtonComponent, ProgressComponent, TagComponent } from '@sb/shared/ui';
import { MySession, MySessionsService } from '@sb/student-portal/data-access';
import { SessionTileComponent } from '../ui/session-tile.component';
import { SessionListRowComponent } from '../ui/session-list-row.component';
import {
  accentFor,
  expiryInfo,
  isExpiringSoon,
  progressVariant,
  videosLabel,
} from '../ui/session-display';

type FilterKey = 'all' | 'InProgress' | 'ExpiringSoon' | 'Completed' | 'Expired';

const FILTERS: { key: FilterKey; label: string }[] = [
  { key: 'all', label: 'All' },
  { key: 'InProgress', label: 'In progress' },
  { key: 'ExpiringSoon', label: 'Expiring soon' },
  { key: 'Completed', label: 'Completed' },
  { key: 'Expired', label: 'Expired' },
];

/**
 * The **My Sessions** hub (`FR-STU-SES-001`, the prototype's `MY SESSIONS` / `spotlight` layout).
 * Loads the caller's enrolled sessions, shows summary counts + a client-side filter chip-bar, a
 * "Jump back in" **spotlight hero** for the most-advanced still-active session, and a **divided list**
 * of the rest with progress + expiry chips + a Start/Continue/Review CTA. Empty → the mascot empty
 * state. (Only the `spotlight` layout is built — the prototype's `cards`/`rail` enum is dropped.)
 */
@Component({
  selector: 'sb-my-sessions',
  standalone: true,
  imports: [
    RouterLink,
    ButtonComponent,
    ProgressComponent,
    TagComponent,
    SessionTileComponent,
    SessionListRowComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="ms">
      <header class="ms__head">
        <div>
          <h1 class="ms__title">My sessions</h1>
          <p class="ms__lede">Pick up where you left off. Assignments stay open even after a session expires.</p>
        </div>
        @if (!loading() && sessions().length > 0) {
          <div class="ms__counts" aria-label="Summary">
            <div class="ms__count"><span class="ms__count-n">{{ sessions().length }}</span><span class="ms__count-l">Enrolled</span></div>
            <span class="ms__count-div" aria-hidden="true"></span>
            <div class="ms__count"><span class="ms__count-n ms__count-n--blue">{{ activeCount() }}</span><span class="ms__count-l">Active</span></div>
            <span class="ms__count-div" aria-hidden="true"></span>
            <div class="ms__count"><span class="ms__count-n ms__count-n--green">{{ completedCount() }}</span><span class="ms__count-l">Completed</span></div>
          </div>
        }
      </header>

      @if (!loading() && !error() && sessions().length > 0) {
        <div class="ms__filters" role="group" aria-label="Filter sessions">
          <span class="ms__filters-label" aria-hidden="true">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                 stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3" />
            </svg>
            Filter
          </span>
          @for (f of filters; track f.key) {
            <button
              type="button"
              class="ms__chip"
              [class.is-active]="filter() === f.key"
              [attr.aria-pressed]="filter() === f.key"
              (click)="setFilter(f.key)"
            >{{ f.label }}</button>
          }
        </div>
      }

      @if (loading()) {
        <div class="ms__skeleton-list" aria-hidden="true">
          @for (i of skeletons; track i) { <div class="ms__skeleton"></div> }
        </div>
      } @else if (error()) {
        <div class="ms__empty">
          <img class="ms__empty-art" src="/assets/salah-mascot.png" alt="" aria-hidden="true" />
          <h2 class="ms__empty-title">Couldn’t load your sessions</h2>
          <p class="ms__empty-copy">{{ error() }}</p>
          <sb-button variant="primary" (clicked)="reload()">Try again</sb-button>
        </div>
      } @else if (sessions().length === 0) {
        <div class="ms__empty">
          <img class="ms__empty-art" src="/assets/salah-relaxing.png" alt="" aria-hidden="true" />
          <h2 class="ms__empty-title">No sessions yet</h2>
          <p class="ms__empty-copy">Once you redeem a code, your sessions show up here — ready to learn.</p>
          <a routerLink="/catalogue" class="ms__empty-link">Browse the catalogue</a>
        </div>
      } @else if (filtered().length === 0) {
        <div class="ms__empty ms__empty--filter">
          <img class="ms__empty-art" src="/assets/salah-relaxing.png" alt="" aria-hidden="true" />
          <h2 class="ms__empty-title">Nothing in this filter</h2>
          <p class="ms__empty-copy">Try a different status to see your other sessions.</p>
          <sb-button variant="secondary" (clicked)="setFilter('all')">Show all</sb-button>
        </div>
      } @else {
        @if (spotlight(); as hero) {
          <div class="ms__eyebrow">Jump back in</div>
          <article class="ms__hero" [attr.data-accent]="heroAccent()">
            <div class="ms__hero-media">
              <sb-session-tile
                [title]="hero.title"
                [grade]="hero.gradeName"
                [thumbnailUrl]="hero.thumbnailUrl"
                [subject]="hero.specializationName"
              />
            </div>
            <div class="ms__hero-body">
              <div class="ms__hero-chips">
                @if (hero.specializationName) {
                  <sb-tag [label]="hero.specializationName" [subject]="heroAccent()" />
                }
                <span class="ms__hero-expiry" [attr.data-variant]="heroExpiry().variant">{{ heroExpiry().label }}</span>
              </div>
              <h2 class="ms__hero-title">{{ hero.title }}</h2>
              <div class="ms__hero-prog">
                <div class="ms__hero-prog-meta">
                  <span>{{ heroVideos() }}</span>
                  <span class="ms__hero-pct">{{ hero.progressPercent }}%</span>
                </div>
                <sb-progress [value]="hero.progressPercent" [variant]="heroProgVariant()" />
              </div>
              <div>
                <sb-button variant="primary" size="lg" (clicked)="open(hero.id)">Continue session →</sb-button>
              </div>
            </div>
          </article>
        }

        <div class="ms__eyebrow">All sessions</div>
        <ul class="ms__list" aria-label="Your sessions">
          @for (s of listRows(); track s.id) {
            <li class="ms__list-item">
              <sb-session-list-row [session]="s" (open)="open($event)" />
            </li>
          }
        </ul>
      }
    </section>
  `,
  styles: [`
    .ms { display: flex; flex-direction: column; gap: var(--sb-space-4); }

    .ms__head { display: flex; align-items: flex-end; justify-content: space-between; gap: 16px; flex-wrap: wrap; }
    .ms__title { font-family: var(--sb-font-display); font-size: 34px; font-weight: 700; color: var(--sb-primary-700); line-height: 0.9; margin: 0 0 6px; }
    .ms__lede { margin: 0; color: var(--sb-text-muted); font-size: var(--sb-body-md-size); max-width: 540px; }

    .ms__counts { display: flex; align-items: center; gap: 18px; }
    .ms__count { text-align: center; }
    .ms__count-n { display: block; font-family: var(--sb-font-mono); font-size: 22px; font-weight: 800; line-height: 1; color: var(--sb-text); }
    .ms__count-n--blue { color: var(--sb-primary-600); }
    .ms__count-n--green { color: var(--sb-accent); }
    .ms__count-l { display: block; font-size: 11px; font-weight: 700; color: var(--sb-text-subtle); text-transform: uppercase; letter-spacing: 0.05em; margin-top: 4px; }
    .ms__count-div { width: 1px; height: 32px; background: var(--sb-border); }

    .ms__filters {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      flex-wrap: nowrap;
      overflow-x: auto;
      align-self: flex-start;
      width: fit-content;
      max-width: 100%;
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-pill);
      box-shadow: var(--sb-shadow-sm);
      padding: 5px 8px 5px 12px;
      scrollbar-width: none;            /* hide the scrollbar; the row scrolls on overflow (mobile/tablet) */
      -webkit-overflow-scrolling: touch;
    }
    .ms__filters::-webkit-scrollbar { display: none; }
    .ms__filters-label {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      flex-shrink: 0;
      color: var(--sb-text-subtle);
      font-weight: 700;
      font-size: var(--sb-body-sm-size);
      padding-right: 4px;
    }
    .ms__chip {
      min-height: 34px;
      padding: 0 16px;
      flex-shrink: 0;
      white-space: nowrap;
      border: none;
      border-radius: var(--sb-radius-pill);
      background: transparent;
      color: var(--sb-text-muted);
      font-family: inherit;
      font-weight: 700;
      font-size: var(--sb-body-sm-size);
      cursor: pointer;
      transition: background var(--sb-timing-fast) var(--sb-easing-standard), color var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .ms__chip:hover { background: var(--sb-primary-50); color: var(--sb-primary-600); }
    .ms__chip.is-active, .ms__chip.is-active:hover { background: var(--sb-primary); color: var(--sb-on-primary); }
    .ms__chip:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }

    .ms__eyebrow { font-size: 11px; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; color: var(--sb-text-subtle); margin-top: 8px; }

    /* ── Spotlight hero ── */
    .ms__hero {
      display: flex;
      flex-wrap: wrap;
      background: linear-gradient(135deg, var(--sb-primary-50), var(--sb-surface));
      border: 1px solid var(--sb-border);
      border-radius: 20px;
      overflow: hidden;
      box-shadow: var(--sb-shadow-sm);
    }
    .ms__hero[data-accent='green']  { background: linear-gradient(135deg, var(--sb-subject-green-bg), var(--sb-surface)); }
    .ms__hero[data-accent='purple'] { background: linear-gradient(135deg, var(--sb-subject-purple-bg), var(--sb-surface)); }
    .ms__hero[data-accent='orange'] { background: linear-gradient(135deg, var(--sb-subject-orange-bg), var(--sb-surface)); }
    .ms__hero[data-accent='pink']   { background: linear-gradient(135deg, var(--sb-subject-pink-bg), var(--sb-surface)); }
    .ms__hero[data-accent='mint']   { background: linear-gradient(135deg, var(--sb-subject-mint-bg), var(--sb-surface)); }
    .ms__hero[data-accent='mustard']{ background: linear-gradient(135deg, var(--sb-subject-mustard-bg), var(--sb-surface)); }
    .ms__hero[data-accent='red']    { background: linear-gradient(135deg, var(--sb-subject-red-bg), var(--sb-surface)); }

    .ms__hero-media { flex: 1 1 300px; min-width: 260px; max-width: 380px; align-self: center; padding: 20px; }
    .ms__hero-body { flex: 2 1 320px; min-width: 260px; padding: 26px 28px; display: flex; flex-direction: column; justify-content: center; gap: 14px; }
    .ms__hero-chips { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .ms__hero-expiry { font-size: var(--sb-label-md-size); font-weight: 700; padding: 3px 10px; border-radius: var(--sb-radius-pill); border: 1px solid transparent; }
    .ms__hero-expiry[data-variant='danger']  { color: var(--sb-danger-fg);  background: var(--sb-danger-bg);  border-color: var(--sb-danger-border); }
    .ms__hero-expiry[data-variant='warning'] { color: var(--sb-warning-fg); background: var(--sb-warning-bg); border-color: var(--sb-warning-border); }
    .ms__hero-expiry[data-variant='neutral'] { color: var(--sb-text-muted); background: var(--sb-neutral-100); border-color: var(--sb-border); }
    .ms__hero-title { margin: 0; font-weight: 800; font-size: 26px; letter-spacing: -0.4px; line-height: 1.1; color: var(--sb-text); }
    .ms__hero-prog-meta { display: flex; align-items: center; justify-content: space-between; margin-bottom: 7px; font-size: 13px; color: var(--sb-text-muted); font-weight: 600; }
    .ms__hero-pct { font-family: var(--sb-font-mono); font-weight: 800; color: var(--sb-text); }

    /* ── Divided list ── */
    .ms__list { list-style: none; margin: 0; padding: 0; background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: 16px; overflow: hidden; box-shadow: var(--sb-shadow-sm); }
    .ms__list-item + .ms__list-item { border-top: 1px solid var(--sb-border); }

    /* ── Skeleton + empty ── */
    .ms__skeleton-list { display: flex; flex-direction: column; gap: 10px; }
    .ms__skeleton { height: 68px; border-radius: 14px; background: var(--sb-surface-sunken); animation: ms-pulse 1.3s var(--sb-easing-standard) infinite; }
    @keyframes ms-pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.55; } }

    .ms__empty { display: flex; flex-direction: column; align-items: center; text-align: center; gap: var(--sb-space-2); background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-xl); padding: var(--sb-space-12) var(--sb-space-5); }
    .ms__empty-art { width: 120px; height: auto; margin-bottom: var(--sb-space-2); }
    .ms__empty-title { margin: 0; font-size: var(--sb-heading-sm-size); font-weight: 800; }
    .ms__empty-copy { margin: 0 0 var(--sb-space-2); max-width: 360px; color: var(--sb-text-muted); line-height: 1.5; }
    .ms__empty-link { color: var(--sb-primary-600); font-weight: 700; text-decoration: none; }
    .ms__empty-link:hover { text-decoration: underline; }

    @media (max-width: 640px) {
      .ms__title { font-size: 28px; }
      .ms__hero-body { padding: 20px; }
    }
  `],
})
export class MySessionsComponent {
  readonly #service = inject(MySessionsService);
  readonly #router = inject(Router);

  readonly filters = FILTERS;
  readonly skeletons = Array.from({ length: 4 }, (_, i) => i);

  readonly sessions = signal<MySession[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly filter = signal<FilterKey>('all');

  readonly activeCount = computed(() => this.sessions().filter((s) => !s.isExpired).length);
  readonly completedCount = computed(() => this.sessions().filter((s) => s.state === 'Completed').length);

  readonly filtered = computed(() => this.sessions().filter((s) => this.#matches(s, this.filter())));

  /**
   * The spotlight "Jump back in" session: the most-advanced **unfinished** still-active session in the
   * current filter — highest `progressPercent` among rows that are **not expired** and **not Completed**
   * (you can't "jump back into" something you've already finished). `null` (no hero) when nothing qualifies.
   */
  readonly spotlight = computed<MySession | null>(() => {
    const candidates = this.filtered().filter((s) => !s.isExpired && s.state !== 'Completed');
    if (candidates.length === 0) return null;
    return candidates.reduce((best, s) => (s.progressPercent > best.progressPercent ? s : best));
  });

  /** The divided list = the filtered set minus the spotlight session (when one is shown). */
  readonly listRows = computed(() => {
    const hero = this.spotlight();
    return hero ? this.filtered().filter((s) => s.id !== hero.id) : this.filtered();
  });

  readonly heroAccent = computed(() => accentFor(this.spotlight()?.specializationName));
  readonly heroVideos = computed(() => {
    const h = this.spotlight();
    return h ? videosLabel(h.videosWatched, h.videoCount) : '';
  });
  readonly heroExpiry = computed(() => {
    const h = this.spotlight();
    return h ? expiryInfo(h.expiresAtUtc, h.isExpired) : { label: '', variant: 'neutral' as const };
  });
  readonly heroProgVariant = computed(() => progressVariant(this.spotlight()?.state ?? 'NotStarted'));

  constructor() {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.#service.mySessions().subscribe({
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

  setFilter(key: FilterKey): void {
    this.filter.set(key);
  }

  open(sessionId: string): void {
    void this.#router.navigate(['/sessions', sessionId]);
  }

  #matches(s: MySession, filter: FilterKey): boolean {
    switch (filter) {
      case 'all':
        return true;
      case 'InProgress':
        return s.state === 'InProgress';
      case 'Completed':
        return s.state === 'Completed';
      case 'ExpiringSoon':
        return isExpiringSoon(s);
      case 'Expired':
        return s.isExpired;
    }
  }
}
