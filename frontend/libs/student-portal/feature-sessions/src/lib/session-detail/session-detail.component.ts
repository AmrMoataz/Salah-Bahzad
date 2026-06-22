import { DOCUMENT } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  OnDestroy,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { AlertComponent, ButtonComponent, ModalComponent, TagComponent } from '@sb/shared/ui';
import {
  MySessionDetail,
  MySessionMaterial,
  MySessionsService,
} from '@sb/student-portal/data-access';
import { SessionTileComponent } from '../ui/session-tile.component';
import { CircularProgressComponent } from '../ui/circular-progress.component';
import { GateBannerComponent } from '../ui/gate-banner.component';
import { VideoRowComponent } from '../ui/video-row.component';
import { accentFor, expiryInfo, humanizeBytes } from '../ui/session-display';

/** How long to wait for the native app to grab the deep link before offering the install prompt. */
const INSTALL_PROMPT_DELAY_MS = 1500;

/**
 * The **Session detail** study screen (`FR-STU-SES-001..004`, the prototype's `SESSION DETAIL`). A
 * hero band with a circular progress ring, the mascot gate banner (by `gateState`), the ordered video
 * playlist with lock/access badges + the **deep-link Play** flow (`FR-STU-VID-001/003/004`), and the
 * Assignment / Prerequisite-quiz / Materials entry cards. The **assignment stays reachable when the
 * session is expired** (`FR-STU-SES-001`). The browser fires **only** the 5C Play gate — no in-browser
 * player, manifest, or AES key (§G).
 */
@Component({
  selector: 'sb-session-detail',
  standalone: true,
  imports: [
    AlertComponent,
    ButtonComponent,
    ModalComponent,
    TagComponent,
    SessionTileComponent,
    CircularProgressComponent,
    GateBannerComponent,
    VideoRowComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="sd">
      <button type="button" class="sd__back" (click)="goBack()">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor"
             stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <line x1="19" y1="12" x2="5" y2="12" /><polyline points="12 19 5 12 12 5" />
        </svg>
        My sessions
      </button>

      @if (loading()) {
        <div class="sd__skeleton" aria-hidden="true"></div>
      } @else if (error()) {
        <div class="sd__error">
          <img src="/assets/salah-mascot.png" alt="" aria-hidden="true" />
          <h2>We couldn’t open this session</h2>
          <p>{{ error() }}</p>
          <sb-button variant="primary" (clicked)="reload()">Try again</sb-button>
        </div>
      } @else if (detail(); as d) {
        <!-- Hero band -->
        <div class="sd__hero" [attr.data-accent]="accent()">
          <div class="sd__hero-media">
            <sb-session-tile [title]="d.title" [grade]="d.gradeName" [thumbnailUrl]="d.thumbnailUrl" [subject]="d.specializationName" />
          </div>
          <div class="sd__hero-body">
            <div class="sd__hero-chips">
              @if (d.specializationName) { <sb-tag [label]="d.specializationName" [subject]="accent()" /> }
              <span class="sd__chip" [attr.data-variant]="expiry().variant">{{ expiry().label }}</span>
            </div>
            <h1 class="sd__title">{{ d.title }}</h1>
            @if (d.description) { <p class="sd__desc">{{ d.description }}</p> }
            <p class="sd__meta">{{ metaLine() }}</p>
          </div>
          <div class="sd__hero-ring">
            <sb-circular-progress [value]="d.progressPercent" [variant]="ringVariant()" />
          </div>
        </div>

        <sb-gate-banner [gateState]="d.gateState" [minPassPercent]="d.minPassPercent" />

        <div class="sd__grid">
          <!-- Video playlist -->
          <div class="sd__card">
            <div class="sd__card-head">
              <h2 class="sd__card-title">Video playlist</h2>
              <span class="sd__count">{{ d.videoCount }} {{ d.videoCount === 1 ? 'lesson' : 'lessons' }}</span>
            </div>
            @if (d.videos.length === 0) {
              <p class="sd__muted">No videos in this session yet.</p>
            } @else {
              <div class="sd__playlist">
                @for (v of d.videos; track v.id; let i = $index) {
                  <sb-video-row
                    [video]="v"
                    [num]="i + 1"
                    [loading]="playingId() === v.id"
                    [error]="errorVideoId() === v.id ? videoError() : null"
                    (play)="play($event)"
                  />
                }
              </div>
            }
            <div class="sd__note">
              <sb-alert variant="info">
                Each play opens the lesson in the <strong>Salah Bahzad app</strong> and consumes one view.
                Videos carry a visible watermark with your name.
              </sb-alert>
            </div>
          </div>

          <!-- Right column -->
          <div class="sd__side">
            @if (d.assignment; as a) {
              <div class="sd__card">
                <div class="sd__entry-head">
                  <span class="sd__entry-icon sd__entry-icon--asg" aria-hidden="true">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                         stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                      <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                      <polyline points="14 2 14 8 20 8" /><line x1="9" y1="15" x2="15" y2="15" />
                    </svg>
                  </span>
                  <div><p class="sd__entry-title">Assignment</p><p class="sd__entry-sub">Open-book · resumable</p></div>
                </div>
                <p class="sd__entry-status">{{ assignmentMeta()?.line }}</p>
                <sb-button variant="accent" (clicked)="openAssignment()">{{ assignmentMeta()?.cta }}</sb-button>
              </div>
            }

            @if (d.quiz; as q) {
              <div class="sd__card">
                <div class="sd__entry-head">
                  <span class="sd__entry-icon sd__entry-icon--quiz" aria-hidden="true">
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor"
                         stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                      <path d="M9 11l3 3L22 4" /><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11" />
                    </svg>
                  </span>
                  <div><p class="sd__entry-title">Prerequisite quiz</p><p class="sd__entry-sub">Timed · unlocks videos</p></div>
                </div>
                <p class="sd__entry-status">{{ quizMeta()?.line }}</p>
                <sb-button variant="secondary" (clicked)="openQuiz()">{{ quizMeta()?.cta }}</sb-button>
              </div>
            }

            <div class="sd__card">
              <h2 class="sd__card-title sd__card-title--mb">Materials</h2>
              @if (d.materials.length === 0) {
                <p class="sd__muted">No materials for this session.</p>
              } @else {
                <ul class="sd__materials">
                  @for (m of d.materials; track m.id) {
                    <li>
                      <button type="button" class="sd__material" [disabled]="materialLoadingId() === m.id" (click)="downloadMaterial(m)">
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="var(--sb-danger)"
                             stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" /><polyline points="14 2 14 8 20 8" />
                        </svg>
                        <span class="sd__material-name">{{ m.fileName }}</span>
                        <span class="sd__material-kind">{{ m.kind }}</span>
                        <span class="sd__material-size">{{ size(m) }}</span>
                      </button>
                    </li>
                  }
                </ul>
              }
              @if (materialError()) { <p class="sd__soon sd__soon--err">{{ materialError() }}</p> }
            </div>
          </div>
        </div>
      }
    </section>

    <sb-modal [open]="installPromptOpen()" title="Open in the Salah Bahzad app" size="confirm" (close)="installPromptOpen.set(false)">
      <div class="sd__install">
        <p>Lessons play in the <strong>Salah Bahzad app</strong> so your videos stay protected with your watermark.</p>
        <p class="sd__install-sub">Don’t have it yet? Install the app, then tap <strong>Play</strong> again.</p>
        <div class="sd__stores">
          <a class="sd__store" href="https://play.google.com/store" target="_blank" rel="noopener">Google Play</a>
          <a class="sd__store" href="https://www.apple.com/app-store/" target="_blank" rel="noopener">App Store</a>
        </div>
      </div>
      <div modalFooter class="sd__install-actions">
        <sb-button variant="secondary" (clicked)="installPromptOpen.set(false)">Close</sb-button>
        <sb-button variant="primary" (clicked)="retryDeepLink()">Try again</sb-button>
      </div>
    </sb-modal>
  `,
  styles: [`
    .sd { display: flex; flex-direction: column; gap: var(--sb-space-4); }

    .sd__back { display: inline-flex; align-items: center; gap: 6px; align-self: flex-start; background: none; border: none; color: var(--sb-primary-600); font-family: inherit; font-weight: 700; font-size: 14px; cursor: pointer; padding: 4px 0; }
    .sd__back:hover { color: var(--sb-primary-700); }
    .sd__back:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); border-radius: var(--sb-radius-sm); }

    .sd__skeleton { height: 200px; border-radius: 18px; background: var(--sb-surface-sunken); animation: sd-pulse 1.3s var(--sb-easing-standard) infinite; }
    @keyframes sd-pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.55; } }
    .sd__error { text-align: center; padding: var(--sb-space-10) var(--sb-space-5); background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: var(--sb-radius-xl); }
    .sd__error img { width: 110px; }
    .sd__error h2 { margin: 8px 0 4px; font-weight: 800; }
    .sd__error p { margin: 0 0 12px; color: var(--sb-text-muted); }

    /* ── Hero ── */
    .sd__hero { display: flex; gap: 20px; flex-wrap: wrap; align-items: center; background: linear-gradient(135deg, var(--sb-primary-50), var(--sb-surface)); border: 1px solid var(--sb-border); border-radius: 18px; padding: 18px; }
    .sd__hero[data-accent='green']  { background: linear-gradient(135deg, var(--sb-subject-green-bg), var(--sb-surface)); }
    .sd__hero[data-accent='purple'] { background: linear-gradient(135deg, var(--sb-subject-purple-bg), var(--sb-surface)); }
    .sd__hero[data-accent='orange'] { background: linear-gradient(135deg, var(--sb-subject-orange-bg), var(--sb-surface)); }
    .sd__hero[data-accent='pink']   { background: linear-gradient(135deg, var(--sb-subject-pink-bg), var(--sb-surface)); }
    .sd__hero[data-accent='mint']   { background: linear-gradient(135deg, var(--sb-subject-mint-bg), var(--sb-surface)); }
    .sd__hero[data-accent='mustard']{ background: linear-gradient(135deg, var(--sb-subject-mustard-bg), var(--sb-surface)); }
    .sd__hero[data-accent='red']    { background: linear-gradient(135deg, var(--sb-subject-red-bg), var(--sb-surface)); }
    .sd__hero-media { width: 100%; max-width: 300px; flex-shrink: 0; }
    .sd__hero-body { flex: 1; min-width: 220px; }
    .sd__hero-chips { display: flex; gap: 8px; align-items: center; margin-bottom: 8px; flex-wrap: wrap; }
    .sd__chip { font-size: var(--sb-label-md-size); font-weight: 700; padding: 3px 10px; border-radius: var(--sb-radius-pill); border: 1px solid transparent; }
    .sd__chip[data-variant='danger']  { color: var(--sb-danger-fg);  background: var(--sb-danger-bg);  border-color: var(--sb-danger-border); }
    .sd__chip[data-variant='warning'] { color: var(--sb-warning-fg); background: var(--sb-warning-bg); border-color: var(--sb-warning-border); }
    .sd__chip[data-variant='neutral'] { color: var(--sb-text-muted); background: var(--sb-neutral-100); border-color: var(--sb-border); }
    .sd__title { margin: 0; font-weight: 800; font-size: 26px; letter-spacing: -0.4px; }
    .sd__desc { margin: 4px 0 0; color: var(--sb-text-muted); font-size: 14px; max-width: 560px; line-height: 1.5; }
    .sd__meta { margin: 8px 0 0; font-size: 13px; color: var(--sb-text-subtle); font-weight: 600; }
    .sd__hero-ring { flex-shrink: 0; }

    /* ── Grid ── */
    .sd__grid { display: grid; grid-template-columns: minmax(0, 1.7fr) minmax(260px, 1fr); gap: 18px; align-items: start; }
    .sd__card { background: var(--sb-surface); border: 1px solid var(--sb-border); border-radius: 16px; padding: 18px; }
    .sd__side { display: flex; flex-direction: column; gap: 18px; }
    .sd__card-head { display: flex; align-items: center; justify-content: space-between; margin-bottom: 14px; }
    .sd__card-title { margin: 0; font-weight: 800; font-size: 17px; }
    .sd__card-title--mb { margin-bottom: 12px; }
    .sd__count { font-size: 12px; color: var(--sb-text-muted); font-weight: 700; }
    .sd__playlist { display: flex; flex-direction: column; gap: 8px; }
    .sd__note { margin-top: 14px; }
    .sd__muted { color: var(--sb-text-muted); font-size: 14px; margin: 0; }

    .sd__entry-head { display: flex; align-items: center; gap: 10px; margin-bottom: 6px; }
    .sd__entry-icon { width: 38px; height: 38px; border-radius: 10px; display: inline-flex; align-items: center; justify-content: center; flex-shrink: 0; }
    .sd__entry-icon--asg { background: var(--sb-subject-green-bg); color: var(--sb-subject-green-deep); }
    .sd__entry-icon--quiz { background: var(--sb-subject-purple-bg); color: var(--sb-subject-purple-deep); }
    .sd__entry-title { margin: 0; font-weight: 800; font-size: 16px; }
    .sd__entry-sub { margin: 0; font-size: 12px; color: var(--sb-text-muted); }
    .sd__entry-status { font-size: 13px; color: var(--sb-text-muted); margin: 8px 0 12px; }
    .sd__card sb-button { display: block; }
    .sd__card sb-button ::ng-deep .sb-btn { width: 100%; }
    .sd__soon { margin: 10px 0 0; font-size: 12px; font-weight: 600; color: var(--sb-text-subtle); }
    .sd__soon--err { color: var(--sb-danger-fg); }

    .sd__materials { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 8px; }
    .sd__material { display: flex; align-items: center; gap: 10px; width: 100%; padding: 10px; border: 1px solid var(--sb-border); border-radius: 10px; background: var(--sb-surface); color: var(--sb-text); font-family: inherit; cursor: pointer; text-align: left; }
    .sd__material:hover:not(:disabled) { background: var(--sb-surface-sunken); }
    .sd__material:disabled { opacity: 0.6; cursor: progress; }
    .sd__material:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }
    .sd__material-name { flex: 1; font-size: 13px; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .sd__material-kind { font-size: 10px; font-weight: 800; color: var(--sb-text-subtle); text-transform: uppercase; letter-spacing: 0.03em; }
    .sd__material-size { font-size: 11px; color: var(--sb-text-subtle); font-weight: 600; }

    .sd__install p { margin: 0 0 8px; line-height: 1.55; }
    .sd__install-sub { color: var(--sb-text-muted); font-size: 14px; }
    .sd__stores { display: flex; gap: 10px; margin-top: 12px; }
    .sd__store { flex: 1; text-align: center; padding: 10px; border: 1px solid var(--sb-border-strong); border-radius: var(--sb-radius-md); font-weight: 700; text-decoration: none; color: var(--sb-text); }
    .sd__store:hover { background: var(--sb-surface-sunken); }
    .sd__install-actions { display: flex; gap: 10px; justify-content: flex-end; }

    @media (max-width: 860px) {
      .sd__grid { grid-template-columns: 1fr; }
      .sd__hero-ring { order: -1; }
    }
  `],
})
export class SessionDetailComponent implements OnDestroy {
  readonly #service = inject(MySessionsService);
  readonly #router = inject(Router);
  readonly #document = inject(DOCUMENT);

  /** The session id, bound from the `/sessions/:id` route param (`withComponentInputBinding`). */
  readonly id = input.required<string>();

  readonly detail = signal<MySessionDetail | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  // Deep-link Play state
  readonly playingId = signal<string | null>(null);
  readonly errorVideoId = signal<string | null>(null);
  readonly videoError = signal<string | null>(null);
  readonly installPromptOpen = signal(false);
  readonly lastDeepLink = signal<string | null>(null);

  // Materials
  readonly materialLoadingId = signal<string | null>(null);
  readonly materialError = signal<string | null>(null);

  #blurred = false;
  #installTimer?: ReturnType<typeof setTimeout>;
  #lastPlayedVideoId: string | null = null;

  readonly accent = computed(() => accentFor(this.detail()?.specializationName));
  readonly expiry = computed(() => {
    const d = this.detail();
    return d ? expiryInfo(d.expiresAtUtc, d.isExpired) : { label: '', variant: 'neutral' as const };
  });
  readonly ringVariant = computed(() => (this.detail()?.isExpired ? 'info' : 'success'));
  readonly metaLine = computed(() => {
    const d = this.detail();
    if (!d) return '';
    return [d.gradeName, d.specializationName].filter(Boolean).join(' · ');
  });

  readonly assignmentMeta = computed(() => {
    const a = this.detail()?.assignment;
    if (!a) return null;
    if (a.status === 'Completed') {
      return { line: `Completed · ${a.scoreMarks ?? 0}/${a.maxMarks} marks`, cta: 'Review assignment' };
    }
    return { line: `${a.questionCount} questions · open-book, resumable`, cta: 'Continue assignment' };
  });

  readonly quizMeta = computed(() => {
    const q = this.detail()?.quiz;
    if (!q) return null;
    if (q.passed) return { line: `Passed · best ${q.bestPercent ?? 0}%`, cta: 'Review quiz' };
    if (q.attemptsUsed > 0) {
      return { line: `Best ${q.bestPercent ?? 0}% · ${q.attemptsUsed}/${q.attemptCount} attempts used`, cta: 'Try again' };
    }
    return { line: `${q.timeLimitMinutes} min · pass ${q.minPassPercent}% · ${q.attemptCount} attempts`, cta: 'Start attempt' };
  });

  constructor() {
    // Reload whenever the bound route id changes (router input binding sets it before first CD).
    effect(() => {
      const id = this.id();
      if (id) this.#load(id);
    });
  }

  ngOnDestroy(): void {
    this.#clearTimer();
  }

  reload(): void {
    this.#load(this.id());
  }

  goBack(): void {
    void this.#router.navigate(['/sessions']);
  }

  size(m: MySessionMaterial): string {
    return humanizeBytes(m.sizeBytes);
  }

  // ── Deep-link Play (§D/§E.5) ─────────────────────────────────────────────
  /**
   * Fire the 5C gate for a `Playable` video (once), then build the deep link and hand off. A gate
   * failure renders the server's `reason` `detail` inline (§D.2); a success refreshes the detail (the
   * view was decremented). A locked row never reaches here (its Play is disabled).
   */
  play(videoId: string): void {
    const video = this.detail()?.videos.find((v) => v.id === videoId);
    if (!video || video.lockState !== 'Playable' || this.playingId()) return;

    this.playingId.set(videoId);
    this.errorVideoId.set(null);
    this.videoError.set(null);
    this.#lastPlayedVideoId = videoId;

    this.#service.startPlayback(videoId).subscribe({
      next: ({ handoffCode }) => {
        this.playingId.set(null);
        this.#launchDeepLink(videoId, handoffCode);
        this.#refresh();
      },
      error: (err: unknown) => {
        this.playingId.set(null);
        this.errorVideoId.set(videoId);
        this.videoError.set(this.#gateMessage(err));
      },
    });
  }

  retryDeepLink(): void {
    this.installPromptOpen.set(false);
    const url = this.lastDeepLink();
    if (!url) return;
    // Re-navigate to the SAME handoff (within its ~60 s TTL) — do NOT re-fire the gate (double-decrement).
    this.#blurred = false;
    this.openExternal(url);
    this.#armInstallPrompt();
  }

  /** Called when the install-prompt timer fires: if the tab never blurred, no app grabbed the scheme. */
  checkAppLaunched(): void {
    if (!this.#blurred && !this.#document.hidden) this.installPromptOpen.set(true);
  }

  @HostListener('window:blur')
  onWindowBlur(): void {
    this.#blurred = true;
    this.#clearTimer();
  }

  @HostListener('document:visibilitychange')
  onVisibilityChange(): void {
    if (this.#document.hidden) {
      this.#blurred = true;
      this.#clearTimer();
    }
  }

  // ── Materials (§C / F6) ──────────────────────────────────────────────────
  downloadMaterial(material: MySessionMaterial): void {
    if (this.materialLoadingId()) return;
    this.materialLoadingId.set(material.id);
    this.materialError.set(null);
    this.#service.materialUrl(this.id(), material.id).subscribe({
      next: ({ url }) => {
        this.materialLoadingId.set(null);
        this.openUrl(url);
      },
      error: () => {
        this.materialLoadingId.set(null);
        this.materialError.set('Couldn’t open that file. Please try again.');
      },
    });
  }

  // ── Entry cards ──────────────────────────────────────────────────────────
  /**
   * Open the S4 assignment — `Completed` → the answer-key review, otherwise the runner (reachable
   * even when expired, `FR-STU-SES-001`). A route string, not an import — keeps the
   * `feature-sessions → feature-assessment` boundary intact (master plan F6). The session title +
   * the `userAssignment` id ride the navigation state so the runner header / review can use them.
   */
  openAssignment(): void {
    const d = this.detail();
    if (!d?.assignment) return;
    const segments =
      d.assignment.status === 'Completed'
        ? ['/sessions', this.id(), 'assignment', 'review']
        : ['/sessions', this.id(), 'assignment'];
    void this.#router.navigate(segments, {
      state: { sessionTitle: d.title, userAssignmentId: d.assignment.userAssignmentId },
    });
  }

  /**
   * Open the S5 prerequisite quiz — always lands on the **intro** (`/sessions/:id/quiz`), the canonical
   * hub for Start / Resume / Try-again and (when passed) the "Review quiz" affordance to the best
   * terminal attempt's answer key. A route string, not an import — keeps the
   * `feature-sessions → feature-assessment` boundary intact (master plan F8). The session title rides
   * the navigation state so the runner / results / review header can use it when the DTO lacks it.
   */
  openQuiz(): void {
    const d = this.detail();
    if (!d?.quiz) return;
    void this.#router.navigate(['/sessions', this.id(), 'quiz'], {
      state: { sessionTitle: d.title },
    });
  }

  /** Seam for tests — real navigation throws in jsdom. */
  protected openExternal(url: string): void {
    try {
      this.#document.location.href = url;
    } catch {
      // jsdom (and some embedded webviews) can't navigate to a custom scheme — harmless here.
    }
  }

  /** Seam for tests — opens the signed material URL in a new tab. */
  protected openUrl(url: string): void {
    this.#document.defaultView?.open(url, '_blank', 'noopener');
  }

  #launchDeepLink(videoId: string, handoffCode: string): void {
    const sessionId = this.detail()?.id ?? this.id();
    const url = `salah-bahazad://stream?videoId=${videoId}&sessionId=${sessionId}&handoff=${handoffCode}`;
    this.lastDeepLink.set(url);
    this.#blurred = false;
    this.installPromptOpen.set(false);
    this.openExternal(url);
    this.#armInstallPrompt();
  }

  #armInstallPrompt(): void {
    this.#clearTimer();
    this.#installTimer = setTimeout(() => this.checkAppLaunched(), INSTALL_PROMPT_DELAY_MS);
  }

  #clearTimer(): void {
    if (this.#installTimer) clearTimeout(this.#installTimer);
    this.#installTimer = undefined;
  }

  #refresh(): void {
    this.#service.session(this.id()).subscribe({ next: (d) => this.detail.set(d) });
  }

  #load(id: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.#service.session(id).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        if (err instanceof HttpErrorResponse && err.status === 404) {
          // Not enrolled / unknown / other tenant — route back, not a hard error (§B.2).
          void this.#router.navigate(['/sessions']);
          return;
        }
        this.error.set('Please check your connection and try again.');
      },
    });
  }

  /** A gate failure renders the server's `problem.detail` verbatim (already user-safe, §D.2). */
  #gateMessage(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      const detail = (err.error as { detail?: string } | null)?.detail;
      if (detail) return detail;
      if (err.status === 404) return 'This video is no longer available.';
    }
    return 'Something went wrong. Please try again.';
  }
}
