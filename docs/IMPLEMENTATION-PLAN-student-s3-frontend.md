# Student Portal · S3 — FRONTEND stream (My sessions, session detail & deep-link video)

> Status: **Planned — not yet built** · Created 2026-06-21 · The **app half** of slice **S3** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S3 — the largest slice). Builds the **My Sessions** hub + the
> **Session detail** study screen + the **deep-link Play** flow into a **new** `libs/student-portal/feature-sessions`
> lib. The S0 shell renders the guarded layout; S2's catalogue **Open** CTA currently routes to a placeholder — S3 fills
> the `/sessions` + `/sessions/:id` routes it should land on.
>
> Run in its **own** Claude session, parallel-safe with the backend stream. **File ownership: `frontend/**` only.**
> Match the **frozen contract** (`docs/contracts/student-s3-my-sessions-video.md`) field-for-field — the
> `MySessionDto` / `MySessionDetailDto` shapes (§A.2/§B.1), the progress/expiry/`state` semantics (§E.1/§E.2), the
> per-video `lockState` order (§E.3), the `gateState` banner (§E.4), the Play gate body + the five `reason` codes (§D),
> and the deep-link handoff (§E.5).
>
> Satisfies: `FR-STU-SES-001..004`, `FR-STU-VID-001/003/004`, `FR-STU-RWD-001/002` (responsive), `FR-STU-A11Y-001`
> (a11y). Green gate: `npx nx build student-portal` (AOT type-checks templates) +
> `nx test student-portal-feature-sessions`.

---

## Design source of truth (the student prototype, NOT the Teacher portal)

- **Prototype:** `.claude/Salah Bahzad Student Portal/Student Portal.html` (siblings `Dropdown.jsx`, `support.js`).
  The banners are **`<!-- ===== MY SESSIONS ===== -->`** and **`<!-- ===== SESSION DETAIL ===== -->`**. S3 builds:
  - **My Sessions** (`isSessions`): header **"My sessions"** + subhead **"Pick up where you left off. Assignments stay
    open even after a session expires."**; a **summary-counts** row; a **filter chip-bar** (All / In progress / Expiring
    soon / Completed / Expired); the **`spotlight`** treatment — a **"Jump back in"** hero for the most-advanced still-
    active session (thumbnail tile + title + a `Progress` bar + **"Continue session →"** primary button); and a
    **divided list** (`spotlightRest`) of the remaining sessions with **expiry chips** ("Expires in N days" /
    **"Expired"**) and a per-row CTA (**Start** / **Continue** / **Review**). **Build ONLY the `spotlight` layout** —
    the prototype's `sessionLayout` demo enum (`spotlight | cards | rail`) is dropped (master plan §3.3).
  - **Session detail** (`isDetail`): a **hero band** (thumbnail tile + title + grade·specialization meta + a **circular
    progress ring**); a **mascot-forward gate banner** (amber `#FEF6DD`/`#F5E2A0`) for the quiz/expired gate; a **video
    playlist** (`videoCountLabel` = "N lessons") where each row shows a lock icon or play icon, title, `MM:SS` duration,
    an **access badge** (**"0 views left"** red / **"Locked"** grey / **"{n} of {m} views"** green), and a **Play**
    button (or **🔒** when locked); a one-line note **"Each play opens the lesson in the Salah Bahzad app and consumes
    one view. Videos carry a visible watermark with your name."**; and a right column of entry cards — **Assignment**
    ("Open-book · resumable"), **Prerequisite quiz** ("Timed · unlocks videos"), and **Materials** (a download list).
- **Tokens / assets / icons / fonts:** already mirrored into `apps/student-portal` by S0 — **reuse**, don't re-mirror.
  Mascots for empty/gate states (`assets/salah-*.png`): `salah-relaxing.png` (empty My Sessions), `salah-prerequisite.png`
  (quiz gate), `salah-mascot.png`/subject mascot (header tiles). Outline icons inline via
  `DomSanitizer.bypassSecurityTrustHtml` (the admin/S0/S2 pattern; Angular strips `<svg>` from plain `[innerHTML]`).
- When prototype and this doc conflict, **the prototype wins** on layout/copy; **the contract wins** on field names, the
  Play gate body/errors, and the progress/expiry/lock/gate semantics.

---

## Conventions (mirror `frontend/CLAUDE.md` + master plan §3.2)
- **New lib** `libs/student-portal/feature-sessions` — `project.json` tags `["scope:student-portal","type:feature"]`,
  `prefix:"sb"`, `@nx/jest` test target (byte-for-byte the shape of the S2 `feature-catalogue` project.json). **You must
  also** add the `@sb/student-portal/feature-sessions` path alias to `frontend/tsconfig.base.json`, the **two** route
  entries to `apps/student-portal/src/app/app.routes.ts`, **and** enable the shell nav item — an unrouted lib still
  builds green (the S1-wiring "unrouted feature-attendance" gotcha); prove `/sessions` + `/sessions/:id` resolve at
  `:4300`.
- **Module boundaries:** `scope:student-portal` → `scope:shared` only. Reuse `@sb/shared/ui`; **never** import an
  admin-portal lib. `feature-sessions` may consume `@sb/student-portal/data-access`; it must **not** import
  `feature-catalogue` (S2's catalogue routes *to* the session detail — see F8 — it does not import it).
- Angular v20+: standalone, `OnPush`, signal `input()/output()/model()`, `computed()/effect()`, `inject()`, native
  control flow, typed reactive forms where needed. Cite `FR-*`/`NFR-*` in tests.
- **Reuse the shared UI:** `Button` (+ variants), `Tag`/`StatusPill`/`Chip`, `Progress` (linear — for the spotlight bar),
  `Modal` (`size="confirm"` — for the install prompt), `Alert`, `EmptyState`, `Card`. **Add** the S3-specific
  presentational pieces (F7) — a **circular** progress ring + a **gate banner** + the playlist/list rows — keep them in
  `feature-sessions` (promote to `libs/student-portal/ui` only if a later phase reuses them).

> **SessionThumb note:** S2's `SessionThumbComponent` (in `feature-catalogue`) is the **catalogue card** (price, prereq,
> Enroll/Open CTA) — it is **not** the My-Sessions row. Do **not** cross-import it. The reusable *visual* is the
> **thumbnail tile** (the numbered, mascot-tinted gradient block — the prototype's `dc-import SessionThumb`); build a
> small presentational `SessionTile` in `feature-sessions` (or promote to `student-portal/ui`) and use it in the
> spotlight hero, the list rows, and the detail hero.

---

## Steps

### F1 — Lib scaffold + routing (avoid the unrouted-lib trap)
- `nx g @nx/angular:library feature-sessions --directory=libs/student-portal/feature-sessions` (or copy
  `feature-catalogue`'s `project.json`); confirm the **tags**, `prefix:"sb"`, and the `@nx/jest` target.
- Add `@sb/student-portal/feature-sessions → libs/student-portal/feature-sessions/src/index.ts` to
  `frontend/tsconfig.base.json`; export `MySessionsComponent` + `SessionDetailComponent` from the lib barrel.
- Add **two lazy routes** under the **authenticated shell** + `authGuard` in `app.routes.ts`:
  `{ path: 'sessions', loadComponent: … MySessionsComponent }` and
  `{ path: 'sessions/:id', loadComponent: … SessionDetailComponent }`. Enable the **"My Sessions"** nav item (the
  prototype's **Learn** group — sidebar + bottom-nav). Confirm `:4300/sessions` and a detail route resolve (not just a
  green build).

### F2 — Data access: `MySessionsService` (authenticated — bearer + refresh apply)
In `libs/student-portal/data-access` (beside `CatalogueService`), add a `MySessionsService`. These are **authenticated**
— they ride the existing `studentAuthInterceptor` (bearer attached, 401→refresh replay, `sb_device` cookie via
`withCredentials`). **Do not** add them to `ANONYMOUS_PATHS`.
- `mySessions(state?): Observable<MySession[]>` → `GET /api/me/sessions` (+ optional `?state=`) → `MySessionDto[]` (§A.2).
- `session(id): Observable<MySessionDetail>` → `GET /api/me/sessions/{id}` → `MySessionDetailDto` (§B.1). A `404` → a
  "not found / no longer enrolled" view (route back to `/sessions`), **not** a hard error.
- `materialUrl(sessionId, materialId): Observable<SignedUrl>` → `GET /api/me/sessions/{id}/materials/{mid}/url` (§C).
- `startPlayback(videoId): Observable<PlaybackHandoff>` → `POST /api/me/videos/{videoId}/playback` (§D) → `{ handoffCode,
  expiresAtUtc }`. **It's a state change** (decrements a view) — call it **once** per Play, never speculatively.
- Model the DTOs as TS interfaces with the contract's **string-union** enums (`state`, `lockState`, `gateState`,
  `processingStatus`, assignment/quiz `status`); export them + the models from the data-access barrel.

### F3 — `MySessionsComponent` (the hub — `spotlight` only) — `FR-STU-SES-001`
A standalone `OnPush` screen under the shell + `authGuard`:
- On init `mySessions()` → render. **Header** + subhead (copy above). A **summary-counts** row derived client-side from
  the loaded set (e.g. *N active · N expiring · N completed* — match the prototype's tiles).
- **Filter chip-bar** (All / In progress / Expiring soon / Completed / Expired): filter **client-side** over the loaded
  list (combine the contract's `state` + `isExpired` + `expiresAtUtc` per §E.2 — *Expiring soon* = `!isExpired &&
  expiresAtUtc within 14 days*; *Expired* = `isExpired`). *(The endpoint also supports `?state=`; client-side is the
  deliverable — the enrolled set is small.)*
- **Spotlight hero ("Jump back in"):** pick the **most-advanced still-active** session (highest `progressPercent` among
  `!isExpired` rows in the current filter). Render the `SessionTile` + title + a shared linear **`Progress`** (value =
  `progressPercent`) + the **"{videosWatched} of {videoCount} videos"** label + a **"Continue session →"** primary `lg`
  button → routes to `/sessions/{id}`. Hide the hero when no active session is in the filter.
- **Divided list** (the rest): each row = `SessionTile` + title + an **expiry `Chip`** (`Expires in N days` /
  **`Expired`**, variant `danger` when expired, `warning` when `≤14d`) + a CTA (**Start** when `state==NotStarted`,
  **Review** when `state==Completed`, else **Continue**) → routes to the detail.
- **Mascot empty state** (`salah-relaxing.png`) when the loaded set is empty — with a "Browse the catalogue" link to
  `/catalogue`.
- **Responsive** (`FR-STU-RWD-001/002`) + **a11y** (`FR-STU-A11Y-001`): the list is a labelled list; chips are toggle
  buttons with `aria-pressed`; the hero collapses gracefully on phone; touch-sized targets.

### F4 — `SessionDetailComponent` (the study screen) — `FR-STU-SES-001..004`
A standalone `OnPush` screen at `/sessions/:id` (read the id from the route, call `session(id)`):
- **Hero band:** `SessionTile` + title + grade·specialization meta + a **circular progress ring** (F7) bound to
  `progressPercent`.
- **Gate banner (mascot-forward)** driven by `gateState` (§E.4): `QuizRequired` → amber banner + `salah-prerequisite.png`
  + **"Pass the prerequisite quiz ({minPassPercent}%) to unlock the remaining locked videos. Your assignment is
  available now."**; `Expired` → banner that videos & the quiz are locked but **the assignment stays open**
  (`FR-STU-SES-001`); `Open` → no banner.
- **Video playlist** (`videos`, already ordered): each row from `MySessionVideo` — a **lock icon** when `lockState !=
  Playable` else a play icon; title; `MM:SS` from `lengthSeconds`; an **access badge** by `lockState` (§E.3 → the
  prototype colours: `Exhausted` → "0 views left" red; `Expired`/`QuizLocked`/`NotReady` → "Locked" grey; `Playable` →
  "{accessRemaining} of {accessAllowed} views" green); and a **Play** button (disabled **🔒** when not `Playable`) →
  F5. Render the **app/watermark note** under the list (copy above).
- **Right column entry cards** (`FR-STU-SES-004`): **Assignment** card (status from `assignment` — *Start* /
  *Continue* / *Review* by `status`, with score when `Completed`) → routes to the S4 assignment runner **(reachable even
  when expired** — `FR-STU-SES-001`; until S4 ships, route to a placeholder/disable with a "Coming soon" note, **do not**
  block the build); **Prerequisite quiz** card (shown only when `quiz != null` — *Start attempt* / best% / passed) →
  routes to the S5 quiz intro (placeholder until S5); **Materials** card (F6).
- Responsive + a11y as F3.

### F5 — Deep-link Play flow (no in-browser player) — `FR-STU-VID-001/003/004`, contract §D/§E.5
On a **Play** click for a `Playable` video:
- `startPlayback(videoId)` → on **`200 { handoffCode }`**, build
  `salah-bahazad://stream?videoId={videoId}&sessionId={sessionId}&handoff={handoffCode}` and **navigate** to it
  (`window.location.href`, or click a hidden `<a>`); start a short **visibility/blur timer** — if the tab never blurs
  (no app handled the scheme), open an **install-prompt** `Modal` (store/download links + a "try again" action). *(This
  is the only place the deep-link is built — the backend returns only the code, §D.1.)*
- On a **gate failure**, render `problem.detail` inline next to the video with its remaining-views count: `409 not_ready`
  → "still being prepared"; `403 not_enrolled`/`enrollment_expired`/`quiz_required`/`no_views_remaining` → the server's
  specific message (§D.2). After a **successful** Play, **refresh the detail** (the view was decremented — `accessRemaining`
  drops, possibly to `Exhausted`).
- **No HLS player, no manifest fetch, no AES key, no redeem call** — the portal calls **only** the gate (§G).

### F6 — Materials download — `FR-STU-SES-003`
The Materials card lists `materials` (fileName, `kind` badge, `sizeBytes` humanised). A **Download** action →
`materialUrl(sessionId, materialId)` → open the returned `url` in a new tab (`window.open`, the URL is a short-lived
signed R2 link). Disable nothing on expiry — materials stay available (contract §C / `FR-STU-SES-001`).

### F7 — Presentational pieces (`feature-sessions`-local)
- **`SessionTile`** — the numbered, mascot-tinted thumbnail block (the prototype's `SessionThumb` dc-import): renders
  `thumbnailUrl` when present, else the tinted-gradient + number + subject mascot fallback. Inputs: title, grade,
  thumbnailUrl, subject (for the accent/mascot).
- **`CircularProgress`** — an SVG ring (the prototype's `circ = 2π·15.5` stroke-dash maths) bound to a 0–100 value;
  `role="img"` + `aria-label="{n}% complete"`. *(The shared `Progress` is linear — use it for the spotlight bar; the ring
  is detail-only.)*
- **`GateBanner`** — the amber mascot banner; input `gateState` + `minPassPercent`.
- **`VideoRow`** / **`SessionListRow`** — the playlist row + the My-Sessions list row (badge/CTA logic above).

### F8 — Wire S2's catalogue **Open** CTA → the session detail
S2 left the catalogue card's **Open** CTA (for `Enrolled` sessions) routing to a placeholder. Update it (and the shell's
relevant landing) to navigate to **`/sessions/{id}`** now that the detail exists. This is the only `feature-catalogue`
touch — a **route string**, not an import — keep the module boundary intact.

### F9 — Tests (Jest; `whenStable()`, never `fakeAsync` — the 5B-1 jsdom gotcha)
- `my-sessions.component.spec.ts`: renders a row per session; **empty list → mascot empty state**; the **filter chip-bar**
  narrows client-side (Expiring soon = `≤14d` non-expired; Expired = `isExpired`); the **spotlight** picks the highest-
  `progressPercent` active session and shows **"Continue session →"**; an expired row shows the **Expired** chip; CTA
  label is Start/Continue/Review by `state`.
- `session-detail.component.spec.ts`: renders the playlist ordered; **per-video badge by `lockState`** (Exhausted →
  "0 views left", QuizLocked/Expired/NotReady → "Locked", Playable → "{n} of {m} views"); the **gate banner** shows for
  `QuizRequired` (with `minPassPercent`) and `Expired`, absent for `Open`; the **Assignment** card is reachable when the
  session is **expired** (`FR-STU-SES-001`); the **Quiz** card shows only when `quiz != null`; a `404` from `session()`
  routes back to `/sessions`.
- `play-flow.spec.ts`: clicking Play on a `Playable` video calls `POST /api/me/videos/{id}/playback` **once**, then
  builds the `salah-bahazad://stream?…&handoff=…` URL (assert the constructed string); a **gate `403 quiz_required`**
  renders the server `detail` inline and **does not** navigate; a locked (**🔒**) row's Play is disabled. *(Stub the
  navigation + the visibility timer; assert the install-prompt opens when no blur fires.)*
- `materials.spec.ts`: Download → `GET /api/me/sessions/{id}/materials/{mid}/url` then opens the returned `url`.
- `my-sessions.service.spec.ts`: each method hits the right path **with** a bearer (not exempted); `mySessions('Expired')`
  sends `?state=Expired`; `startPlayback` POSTs to the gate; all map the DTOs (string-union enums) correctly.

## Exit criteria
A signed-in student opens **My Sessions**, sees their enrolled sessions (spotlight hero + divided list with progress +
expiry chips), filters by state, and opens one; the **Session detail** shows the playlist with correct lock/access badges,
the gate banner when quiz-gated or expired, reachable Assignment/Quiz/Materials, and a circular progress ring; **Play**
fires the gate and either deep-links to the app (and the view decrements) or surfaces the specific gate reason, with an
install prompt when no app handles the scheme; materials download via a signed URL; the catalogue **Open** CTA now lands
here; the screens are responsive + a11y-clean on phone/tablet/desktop. `npx nx build student-portal` (AOT) +
`nx test student-portal-feature-sessions` green. Hand to wiring.

## Out of scope (defer)
The **`cards` / `rail`** My-Sessions layouts (demo-only — **not built**, master plan §3.3 / contract §G); **in-browser
video playback** / any HLS player / manifest / AES key (deep-link only, §G); the native-app **watermark** + OS
**black-out** (`FR-STU-VID-002`/`FR-PLAT-VID-004/005`); the **assignment runner** (S4) + **quiz intro/runner/results**
(S5) — the entry cards route there once built (placeholder until then); profile (S6); any change to the **5C gate /
transcode** (reused as-is); server-side pagination (the flat list is frozen, contract §A).

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the FRONTEND stream of Student-Portal phase S3 (My Sessions + Session detail + deep-link video Play)
for Salah Bahzad (Angular v20+, Nx). Edit frontend/** ONLY. The app, shell, auth, catalogue, and student session already
exist from S0/S1/S2 — you add a NEW libs/student-portal/feature-sessions lib.

Read first, in order:
1. frontend/CLAUDE.md (Angular v20+ conventions, tokens, icons, module boundaries).
2. docs/IMPLEMENTATION-PLAN-student-s3-frontend.md — THIS doc. DESIGN SOURCE OF TRUTH =
   .claude/Salah Bahzad Student Portal/Student Portal.html banners MY SESSIONS (spotlight layout ONLY — drop cards/rail)
   + SESSION DETAIL.
3. docs/contracts/student-s3-my-sessions-video.md — the FROZEN contract: §A/§B (GET /api/me/sessions + /{id} + the
   MySession* DTOs), §C (material signed-URL read), §D (POST /api/me/videos/{id}/playback body + the five reason codes —
   render problem.detail verbatim), §E (progress/expiry/state, per-video lockState order, gateState, the deep-link
   handoff). The browser calls ONLY the gate — never redeem/key/manifest.
4. The S2 code to reuse/port: libs/student-portal/feature-catalogue (DO NOT import it — the catalogue Open CTA ROUTES to
   /sessions/:id), the studentAuthInterceptor + StudentAuthStore (these reads + the Play POST are AUTHENTICATED — bearer +
   refresh apply, do NOT exempt them), CatalogueService (the pattern for MySessionsService), and the app.routes.ts +
   tsconfig.base.json alias + shell nav pattern.

Build: scaffold libs/student-portal/feature-sessions (tags scope:student-portal/type:feature, prefix sb, @nx/jest) AND
wire its tsconfig alias + the /sessions and /sessions/:id routes + the shell nav item (an unrouted lib still builds green
— prove both resolve at :4300). A MySessionsService (mySessions(state?), session(id), materialUrl, startPlayback —
authenticated). A MySessionsComponent (spotlight ONLY: header + summary counts + client-side filter chip-bar + "Jump back
in" hero with shared linear Progress + "Continue session →" + divided list with expiry chips + Start/Continue/Review CTA +
mascot empty state). A SessionDetailComponent (hero + circular progress ring + mascot gate banner by gateState + video
playlist with lockState badges [0 views left / Locked / n of m views] + Play/🔒 + the app/watermark note + Assignment/
Prerequisite-quiz/Materials entry cards; Assignment reachable when EXPIRED). The deep-link Play flow: startPlayback ->
200 -> build salah-bahazad://stream?videoId=&sessionId=&handoff= -> navigate -> install-prompt Modal on no-app; render the
gate reason inline on failure; refresh after success; NO browser player. Materials download via signed URL. Local
presentational pieces (SessionTile, CircularProgress, GateBanner, rows). Update S2's catalogue Open CTA to route to
/sessions/:id (route string, not import).

Jest with whenStable() (NOT fakeAsync): my-sessions render + empty + filter + spotlight selection + CTA-by-state; detail
playlist + lockState badges + gateState banner + assignment-reachable-when-expired + quiz-only-when-present + 404->back;
play flow builds the deep-link + renders gate reason + disabled 🔒 + install-prompt; materials fetch+open; service hits
the right paths WITH a bearer and maps the string-union enums. Responsive + a11y. Green gate:
`npx nx build student-portal` + `nx test student-portal-feature-sessions`. Report both.
```
