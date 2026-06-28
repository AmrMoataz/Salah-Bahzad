# Implementation Plan — Native App (Flutter · Windows / macOS / iOS / Android)

> Status: **Planned — not yet built** · Created 2026-06-23 · Scope: the **Secure Player** — a brand-new, single-codebase **Flutter** companion app whose only job is **protected video playback with the OS capture black-out**. It is **not** a second portal. The shared engines it consumes — student sign-in/exchange + device binding (Phase **S0**), the student self-profile read, and the **secure-video gate** (gate → one-time handoff → signed AES-128 HLS manifest → key) (Phase **5C**) — **already exist and are reused as-is**.
>
> Written against `docs/08-functional-app.md` (`FR-APP-*`), `docs/09-non-functional-app.md` (`NFR-APP-*`), `docs/05-secure-video-streaming-options.md` (the browser→app handoff + HLS architecture), and `docs/hls-transcoding-and-streaming.md`. The **design source of truth** is the prototype `.claude/Salah Bahzad App/Secure Video App (standalone).html` (the "Secure Player" frame board) — **not** `docs/tokens.*` / `docs/03-components.md` (deprecated for design). Requirement IDs (`FR-APP-*`, `NFR-APP-*`) are cited throughout so every phase is traceable.
>
> This plan **mirrors `docs/IMPLEMENTATION-PLAN-student-portal.md`** in shape (one horizontal foundation phase → vertical-slice delivery; each slice = a frozen contract + parallel-safe streams) and follows the same house conventions — see §3 (the binding "same architecture" contract). It is a **sibling** document: the admin plan explicitly scopes the Flutter app out of itself (`docs/IMPLEMENTATION-PLAN-admin-portal.md:3`), and the student plan names "the native engagement" as future work (`§7`).

---

## 1. Context — what already exists (the baseline)

The app is **frontend-led** (Flutter-led): the backend it needs is essentially complete. Everything below was verified in `backend/src/**` during planning (file:line cited).

> **Assumption:** the baseline is HEAD with the uncommitted S0–S5C work present and green (194 unit + the integration suite, the one pre-existing `QuestionBank` image-test failing as the known baseline). The native app reuses these engines unchanged.

### 1.1 Already built and reused **as-is**

| Capability | Endpoint(s) | Built in | Requirement |
|---|---|---|---|
| Student sign-in exchange (Firebase ID token → platform session; `Active`-only; status gate → `403 {reason}`) | `POST /api/auth/student/exchange` (`AllowAnonymous`, rate-limit `auth`) | S0 | `FR-APP-AUTH-001`, `FR-APP-DEV-001` |
| Role-aware token refresh (re-checks `Active` + active device binding) | `POST /api/auth/refresh` (`AllowAnonymous`, rate-limit `auth`) | S0 | `FR-APP-AUTH-002` |
| Student self-profile (watermark phone half + bound-device summary) | `GET /api/me/profile` (`RequireStudent`) | S6/profile | `FR-APP-VID-003` |
| **Play gate** — active enrollment + quiz-passed (where applicable) + access remaining; **decrements one view**; audits `VideoPlaybackStarted`; mints a one-time handoff | `POST /api/me/videos/{videoId}/playback` (`RequireStudent`) | 5C | `FR-APP-VID-001/005` |
| **Redeem** — handoff → per-playback signed HLS manifest (`.m3u8` w/ signed R2 segment URLs, ~120 s TTL); binds `handoff.StudentId == caller` | `POST /api/me/videos/playback/redeem` (`RequireStudent`) | 5C | `FR-APP-VID-001/002`, `FR-APP-AUTH-003` |
| **AES-128 key** — 16 key bytes over an authenticated request; re-checks `Active`/not-expired/quiz-passed; no decrement, no audit | `GET /api/me/videos/{videoId}/hls.key` (`RequireStudent`) | 5C | `FR-APP-VID-002` |
| Deep-link Play handoff already fired by the portal (`salah-bahazad://stream?videoId=…&sessionId=…&handoff=…`) | portal `session-detail.component.ts:489` | S3 | `FR-APP-LNK-001..004` |
| Audit (`VideoPlaybackStarted`, `StudentSignedIn`, `StudentDeviceBound`, `StudentSignInRejected`, all `ActorType=Student`) + EF global `TenantId` filter | interceptor / `AuditWriter` | 5A/S0 | `FR-APP-SCOPE-003` |

**Handoff mechanics (frozen by 5C, reused):** 48-char hex from `RandomNumberGenerator`, stored in Redis under `playback:handoff:` with **~60 s TTL**, **single-use** (atomic `GETDEL`), payload `{ videoId, enrollmentId, studentId, tenantId }`. Never a JWT, never a URL. (`RedisPlaybackHandoffStore.cs:19-44`, `PlaybackOptions.cs:13`.)

### 1.2 Gaps — what this plan adds (small, backend touches are minimal)

| Gap | Resolution (decided) | Requirement | Phase |
|---|---|---|---|
| **No student serial.** `Student` has only `Id`(GUID)/`FullName`/`PhoneNumber`; the watermark needs a serial. | Add `Serial` to `Student` — **randomly generated**, unique, human-readable (`STU-XXXXXX`, Crockford base32); minted at registration + **backfilled** for existing students; surfaced on `GET /api/me/profile`. The watermark renders **serial + full name**. **One migration.** | `FR-APP-VID-003` | **A1** |
| **Device binding must NOT apply to the app** (user decision 2026-06-23). The existing exchange binds/enforces one device → it would `403 device_not_recognized` a student signing in on a second machine. | Add a **device-agnostic** app sign-in: `POST /api/auth/student/app-exchange` (no bind, no enforce, **no `device_id` claim**) + make `refresh` app-aware (skip the device re-check for app tokens). A student signs in on **any** machine; the existing browser exchange stays portal-only and device-bound. | `FR-APP-DEV-001/002` | **A0** |
| **No min-app-version floor** (`NFR-APP-UPD-002`). | Config-driven per-platform floor (hot-reloadable via `IOptionsMonitor`). `GET /api/app/version-status?platform=&version=` for a launch check; **hard server-side enforcement at `redeem`** (the app-only playback step) via `X-App-Version`+`X-Platform` → `426 outdated_app`. See §7-A4 + contract §F. | `FR-APP-UPD-001`, `NFR-APP-UPD-002` | **A4** |
| **Deep-link param drift (three-way).** Live portal emits `videoId`/`sessionId`/`handoff`; docs use `video`/`session`. | **Freeze the canonical keys to what the portal emits today** (`videoId`, `sessionId`, `handoff`); the app's URI parser accepts those. Update docs `05`/`08` to match (doc-only). | `FR-APP-LNK-002/003` | **A0** |

### 1.3 Intentional non-implementations (resolved earlier, confirmed by the design)

- **No device binding in the app at all** (user decision 2026-06-23). The player is **device-agnostic** — a student may sign in on any machine (`FR-APP-DEV-001`, `docs/08` §E, updated). Anti-sharing is the **watermark (serial + name)** + the per-video view cap, not device identity. One-device binding stays a **portal** concern (`FR-PLAT-DEV-*`). No consent screen, no "device not recognised" state, no app-managed device token. *Trade-off (informed): the app accepts cross-device sign-in in exchange for watermark-traceability; portal/account access remains one-device-bound.*
- **No offline attendance.** Removed from scope (`docs/08` §J deleted). The prototype has no attendance screen.
- **No in-app browsing/enrollment/assignments/quizzes.** Those stay in the web portal (`FR-APP-SCOPE-002`). The app is reached only by **Play** deep-link; its idle/home screen just points back to the portal.

---

## 2. Decisions captured

| Decision | Choice |
|---|---|
| Engagement shape | **Flutter-led vertical slices.** New top-level `app/` codebase; per-slice frozen contract + parallel-safe streams (**backend / app / wiring**, where *app* plays the *frontend* role). |
| Codebase location | **New top-level `app/`** — a true sibling to `backend/`, `frontend/`, `docs/` (own `pubspec.yaml`, `.gitignore`, CI). **Not** inside the Nx workspace. *(Rename to `native-app/` on request.)* |
| Flutter version | **Latest stable** (pinned in `app/.fvmrc` / CI). |
| Platform scope (v1) | **All four** — Windows, macOS, iOS, Android — from one codebase. The shared responsive UI is written once; per-OS work = the capture-protection shim + each store's signing/packaging pipeline. |
| Min OS versions | Windows 10 **2004+**, macOS **11+**, iOS **13+**, Android **8+** (so the capture-protection APIs exist; `NFR-APP-COMPAT-001`). Where the black-out can't be guaranteed → **warn + refuse** (`NFR-APP-COMPAT-002`), never play unprotected. |
| Auth model | **Device-agnostic.** App holds a **persistent keystore session** (single sign-in on any machine, silent refresh) via a new `app-exchange` that does **no** device binding. The deep-link handoff is a **video-playback** handoff (redeemed against that session), **not** a session-bootstrap token — see §10.1. |
| iOS still-screenshot | **Accepted gap** (`NFR-APP-CAP-005`): detect + log + rely on watermark. FairPlay (iOS-only) is the deferred escape hatch, **not** v1. |
| Watermark identity | **Serial + full name** (e.g. `STU-7K2M9 · Layla Ahmed`). Serial is randomly generated + unique (§1.2). This is the app's **primary anti-sharing deterrent** (it replaces device binding). |
| State management | **Riverpod** (signals-equivalent, testable) + a thin repository layer — mirrors the portal's store/data-access split conceptually. *(Confirm vs. Bloc in A0.)* |
| Player engine | `video_player` + an HLS/ABR-capable backend per platform (ExoPlayer / AVPlayer / `media_kit` on desktop) — confirm AES-128 + custom key-loader support in A1. |

---

## 3. Conventions to follow **exactly** (binding — the "same architecture" contract)

### 3.1 App — a new `app/CLAUDE.md` (create in A0, mirrors `frontend/CLAUDE.md` discipline)
- **Latest stable Flutter**, null-safe Dart, `flutter analyze` clean, `dart format` enforced.
- **Feature-first folders** under `app/lib/` with clear layers: `core/` (theme/tokens, networking, secure storage, deep-link, capture-protection shims), `data/` (DTOs + repositories mapping the frozen contract field-for-field), `features/` (`auth`, `player`, `idle`, `splash`, `errors`), `app/` (router, shell, responsive scaffold).
- **One responsive widget tree per screen** — reflow via `LayoutBuilder`/`MediaQuery`, never separate phone/desktop screens (§6).
- **Capture protection is a `core/secure_surface` package** — one `MethodChannel` per desktop platform + an iOS `EventChannel`; pure-Dart elsewhere (the watermark is a Flutter overlay, not native).
- **Secrets discipline (remediates the legacy app):** TLS validation **never** disabled (`NFR-APP-SEC-002`); tokens/handoffs/signed-URLs/PII **never** logged (`NFR-APP-SEC-003`); signed URLs + HLS keys held **in memory only** (`NFR-APP-SEC-005`); session/device tokens in the **OS keystore** via `flutter_secure_storage` (`NFR-APP-SEC-001`).
- **Tests:** widget + unit tests per feature (`flutter test`); a faked transport so the dev playback path exercises the **happy** path (`NFR-APP-REL-003` — the legacy mock always failed).

### 3.2 Backend — mirror `backend/CLAUDE.md` (only the four §1.2 touches)
- Clean Architecture + CQRS + Mediator; new reads under `/api/me/*` + `RequireStudent` (or `AllowAnonymous` for `min-version`); EF global `TenantId` filter (never per-handler `Where`); manual `.ToDto()` mapping (no AutoMapper); Scalar/OpenAPI + xUnit v3 + Testcontainers; audit only on state change (reads not audited).
- The serial migration is the **only** schema change in the whole plan.

### 3.3 Intentional non-implementations
Listed in §1.3 + §9 so nothing is silently dropped: no consent screen, no offline attendance, no in-app catalogue/quizzes, no FairPlay (v1), no per-request device re-validation (a **platform** gap, §10.3, affects the portal too — out of scope here).

---

## 4. Workspace & architecture plan (the concrete shape)

### 4.1 New top-level `app/` (Flutter)

```
app/
├─ CLAUDE.md                      # app conventions (3.1)
├─ pubspec.yaml                   # latest Flutter; riverpod, video_player/media_kit,
│                                 # flutter_secure_storage, app_links, dio, sentry_flutter
├─ .fvmrc / .gitignore
├─ android/ ios/ macos/ windows/  # 4 runners (linux NOT a target)
├─ assets/
│  └─ brand/                      # logo-small, logo-white, salah-mascot, salah-relaxing,
│                                 # salah-failed  (from the portal design system / _ds)
├─ fonts/                         # Nunito Sans, Permanent Marker, Caveat, a mono face
└─ lib/
   ├─ main.dart
   ├─ app/                        # router, AppShell, ResponsiveScaffold, window chrome
   ├─ core/
   │  ├─ theme/                   # SbTokens (colors/typography/radii/spacing) — §5.2
   │  ├─ net/                     # Dio client (pinned TLS, X-App-Version, X-Platform)
   │  ├─ storage/                 # keystore session + device-token (secure_storage)
   │  ├─ deeplink/                # app_links parser (videoId/sessionId/handoff) — §1.2
   │  └─ secure_surface/          # capture-protection plugin (4 platform channels)
   ├─ data/                       # DTOs + repositories (auth, profile, playback)
   └─ features/
      ├─ splash/  signin/  player/  idle/  errors/
```

### 4.2 Capture-protection plugin (`core/secure_surface`)

| Platform | Native call (on at player mount, off at dismount — before first frame, `*-CAP-006`) | Channel |
|---|---|---|
| Android | `window.addFlags(FLAG_SECURE)` / clear on dispose | Kotlin `MethodChannel` on the `FlutterActivity` window |
| Windows | `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` / reset `WDA_NONE` | C++ Win32 plugin resolving the Flutter view `HWND` |
| macOS | `NSWindow.sharingType = .none` / restore `.readOnly` | Swift plugin on the `NSWindow` |
| iOS | KVO `UIScreen.main.isCaptured` → blank/pause; `userDidTakeScreenshot` → log/flag | Swift plugin streaming events over an `EventChannel` |

`COMPAT-002` fail-safe: if the platform/version can't guarantee the black-out (e.g. Windows < 2004), **warn + refuse** protected playback.

### 4.3 Backend additions (no new aggregate; **one** migration total)
- A1: `Student.Serial` (randomly generated, unique; + `/api/me/profile` field; minted at registration + backfilled) — the only migration.
- A0: `POST /api/auth/student/app-exchange` (device-agnostic) + app-aware `refresh` — no migration.
- A4: `GET /api/app/version-status` + `X-App-Version`/`X-Platform` enforcement at `redeem` — no migration (config-driven, `IOptionsMonitor`).

### 4.4 Run/orchestration
The app is **not** an Aspire service. Wiring runs it against the running Aspire stack (Postgres + Redis + MinIO + API) via the API's dev URL. Deep-link testing uses the OS protocol handler registration (per platform).

---

## 5. Design plan — the "Secure Player" look (design source of truth: the prototype)

> **Design source of truth = `.claude/Salah Bahzad App/Secure Video App (standalone).html`** (the "Secure Player" frame board). Anchors are the prototype's **comment banners** by name (e.g. `<!-- ============ PLAYER ============ -->`), not line numbers. The visual language is **continuous with the web portals** — same brand fonts, cream paper, mascots, blue/green accents — so a student crossing portal → Play → app sees one product.

### 5.1 Frame board → app
The prototype renders one `SecureApp` component at three frames — **phone** (live flow), **tablet** (landscape sign-in), **desktop** (macOS controls-left / Windows controls-right, **custom window chrome, no OS toolbar**) — proving every screen reflows from 402 px to 1180 px. The Flutter app reproduces this as **one responsive tree** (§6) plus a **frameless desktop window** with custom title bar (drag region + min/max/close + in-bar account + "Secure" pill).

### 5.2 Design tokens (extracted from the prototype → `core/theme/SbTokens`)
- **Typography:** `Nunito Sans` (body), `Permanent Marker` (display/brand headings), `Caveat` (the faint math-doodle motif), a monospace face (`Cascadia Mono` → bundle or substitute) for codes/timers/watermark.
- **Brand/blue:** `#2C6FB3` (primary) · `#1E3A5F` / `#16263d` / `#122739` (navy depths) · `#9fc4ec` (accents on dark).
- **Paper:** `#FBFBF7` / `#F4F3ED` / `#e7e5df` (board).
- **Player dark:** `#0E1620` / `#0a111c` / `#05090f`.
- **Secure-green:** `#46A33E` / `#86C7A6` / `#7FC375`; **amber:** `#F3C12E` (views-left / warnings).
- **Text ramp:** `#1A1A16` → `#3D3C35` → `#54534A` → `#6F6E63` → `#98968A` → `#A8A697`.
- **Borders:** `#DAD8CC` / `#ECEBE2` / `#cfcdc2`. **Radii:** cards 12–20, pills 999, device frame 42–56.
- **Assets:** `logo-small`, `logo-white`, `salah-mascot`, `salah-relaxing`, `salah-failed` (reuse the portal's mascot set; memory: portal already ships the mascot mapping).

### 5.3 The five screens (each = a prototype banner = an A-phase deliverable)

| Screen | Design anchor (banner) | Essentials | Phase |
|---|---|---|---|
| **Splash / deep-link** | `<!-- ============ SPLASH / DEEP-LINK HANDLER ============ -->` | Navy radial gradient, math doodles, logo + mascot, a 3-step progress checklist ("Secure link received → Verifying handoff code → Opening secure session") + bar, monospace `salah-bahazad://stream?video=…` + "no token in URL". Parses link, redeems, routes to player. | A0 |
| **Sign in** | `<!-- ============ SIGN IN ============ -->` | Split brand-panel + form (email/password, "Keep me signed in", **Sign in**, "or", **Continue with Google**); "Only active students can sign in. Identity is verified with Firebase." Stacks on phone. | A0 |
| **Player** | `<!-- ============ PLAYER ============ -->` | Dark stage; top bar (back, lesson title, **Encrypted** chip, **"N of M views left"**); **dual-layer watermark** (tiled wash + chip repositioning ~2.6 s) of **serial + full name**; **"Screen capture blocked"** banner; controls (play/pause, seek + timers, **speed 1×/1.25×/1.5×/2×**, mute, fullscreen) — **no download/export**. | A1 |
| **Idle / home** | `<!-- ============ IDLE / HOME ============ -->` | Paper theme; hero ("Welcome back, …", **Open the student portal**, relaxing mascot); **"How to start a lesson"** card (press Play in the portal on this device); 3-card "Your account is protected" strip; **Sign out**. Account bar hidden on desktop (window chrome carries it). | A0 (shell) / A3 (polish) |
| **Failure / retry** | `<!-- ============ FAILURE / RETRY ============ -->` | Failed-mascot, title + message + primary (+ optional secondary) action. **Seven** states with verbatim copy (see contract §H). "Your place is saved — nothing is lost." | A3 |

### 5.4 Desktop window chrome (anchors: `macOS · controls left`, `Windows · controls right`)
Frameless window (`bitsdojo_window` or equivalent): 44 px navy title bar, draggable; **macOS** traffic-lights left, **Windows** min/max/close right; logo + "Secure Player" + green **Secure** pill + account (name/role/avatar). The player is hosted inside this chrome (`hide-app-bar` equivalent — the in-screen top account bar is suppressed when the window chrome is present).

---

## 6. Responsive strategy (fully responsive — a hard requirement)

- **One widget tree per screen, reflowed by width** — never a phone build vs a desktop build. Mirrors the prototype's single `SecureApp` driven by `isPhone = width < 560`.
- **Breakpoints (`core/theme/Breakpoints`):** `compact < 560` (phone — single column, stacked sign-in, 1-col status grid, bottom-anchored actions) · `medium 560–1024` (tablet — row sign-in, 3-col status grid) · `expanded ≥ 1024` (desktop — same, inside the custom window chrome). These match the prototype's 402 / 862 / 1180 frames.
- **Reflow rules carried over verbatim:** sign-in `column→row`; hero `column→row`; status grid `1fr → 1fr 1fr 1fr`; hero padding `20→30`; player controls wrap; type scales via `clamp()`-equivalent (`MediaQuery` text scaling honoured, `NFR-APP-A11Y-001`).
- **Orientation + window resize** are live (the player must survive rotation and desktop resize without losing playback context — ties to `FR-APP-ERR-002`/`NFR-APP-REL-001`).
- **Verification:** golden tests at 360 / 768 / 1280 widths per screen; a manual matrix row per breakpoint in the wiring stream.

---

## 7. The phased plan

Each phase: **Goal · Backend · App · Design anchor · Reqs · Exit · Streams**. **A0** is the horizontal foundation; **A1–A4** are vertical slices. Phases that touch the backend get a **frozen contract** + **3 streams** (backend/app/wiring); pure-app phases get **app + wiring** (the wiring being the on-device capture/manual matrix). The cross-cutting playback contract is frozen once in `docs/contracts/native-app-playback.md` (companion to this plan).

### A0 — Foundation: fresh app, shell, theme, auth, deep-link, keystore session
> **Status: ✅ Met — 2026-06-24.** Wiring proven live on the Aspire stack: **10/10 scripted checks, ZERO contract drift** (4-lens adversarial verify clean); the device-agnostic headline holds (a portal-bound student signs into the app from another machine → 200, while the portal path rejects an unrecognised device → 403). The **Flutter Windows app builds & runs against the live API** (real Firebase wired). On-screen walkthrough (#11) is the user's visual step. Streams: **backend DONE · app DONE · wiring DONE**. See `IMPLEMENTATION-PLAN-native-app-a0-{backend,app,wiring}.md`.
- **Goal:** a signed-in student can launch the app (cold or via a `salah-bahazad://` link), land in a themed, responsive shell, and persist a silent-refreshing session — with **no** consent screen.
- **App:** scaffold `app/` (4 platforms) + `app/CLAUDE.md`; design tokens + responsive scaffold + fonts/assets; **Splash**, **Sign in**, **Idle** screens; Firebase auth (email/pw + Google) → `POST /api/auth/student/app-exchange` (**device-agnostic** — signs in on any machine) → keystore session; silent refresh; status-gate `403 {reason}` → error states; **deep-link registration + parser** (`videoId`/`sessionId`/`handoff`, cold-start + warm, malformed → clear error not crash).
- **Backend:** add the **device-agnostic `POST /api/auth/student/app-exchange`** (no bind/enforce, no `device_id` claim) + make `refresh` app-aware (skip the device re-check for app tokens); canonicalize the deep-link param keys (doc-only). **No migration.**
- **Design anchor:** `SPLASH`, `SIGN IN`, `IDLE / HOME` banners + the device-chrome banners.
- **Reqs:** `FR-APP-SCOPE-001..003`, `FR-APP-AUTH-001/002/004`, `FR-APP-LNK-001/002`, `FR-APP-DEV-001`, `FR-APP-NAV-001`, `NFR-APP-SEC-001/003/004`, `NFR-APP-REL-002`, `NFR-APP-PERF-001`.
- **Exit:** app launches < 3 s to a usable screen on all four platforms; sign-in→shell works; the three reason-gated states render; a `salah-bahazad://…` link routes to a player placeholder; tokens live only in the keystore.
- **Streams:** backend (app-exchange) · app (foundation) · wiring (live exchange + deep-link on the Aspire stack).

### A1 — Player + gate + watermark (the core slice)
> **Status: ✅ Backend + App DONE; wiring API-level PROVEN live on the Aspire stack (ZERO contract drift) — 2026-06-26.** Backend `Student.Serial` + the one migration shipped (unit 212/212, integ 230/231 = known catalogue baseline); the Flutter Player shipped (media_kit + loopback key-proxy, `flutter analyze` clean + 91/91 + windows/apk builds). Live wiring green: profile `serial` (2nd field, `STU-XXXXXX`); gate→redeem→key with **`AccessRemaining −1` on the gate only** + one `VideoPlaybackStarted` audit; redeem/key spend nothing; consumed/forged/foreign handoff → `410`; `not_enrolled`/`no_views_remaining` → `403`; anon `401`/staff `403`. **Found+fixed live:** the UUIDv7 backfill collision (first-hex→last-hex). **Pending (user):** the on-screen libmpv decode/render visual (#16) + true cross-tenant `404` (single-tenant dev env). · **Contract:** `docs/contracts/native-app-playback.md` · See `…-a1-{backend,app,wiring}.md`.
- **Goal:** from a Play deep-link, the app redeems the handoff and plays the encrypted lesson with the moving watermark and view-budget surfaced.
- **App:** **Player** screen — `POST /api/me/videos/playback/redeem` → `PlaybackManifestDto` → play **AES-128 HLS** with a custom **key-loader** hitting `GET /api/me/videos/{id}/hls.key` (auth header; key in memory only); controls (play/pause/seek/speed/volume/fullscreen, **no download**); **dual-layer watermark** (**serial + full name**); **"N of M views left"**; retry reuses the same handoff within TTL (no double-decrement); ABR up to 1080p.
- **Backend:** add `Student.Serial` — **randomly generated**, unique (`STU-XXXXXX` Crockford base32), minted at registration + **backfilled** for existing students; surface on `/api/me/profile`. **The one migration.**
- **Design anchor:** `PLAYER` banner.
- **Reqs:** `FR-APP-VID-001..005`, `FR-APP-AUTH-003`, `NFR-APP-SEC-005`, `NFR-APP-PERF-002/003`, `NFR-APP-REL-001`, `FR-APP-ERR-002`.
- **Exit:** Play → first frame < 4 s p95; key/URLs never on disk; watermark legible + repositioning; views decrement and surface; expired handoff → clean re-Play guidance.
- **Streams:** backend (serial) · app (player) · wiring (live gate→redeem→key→play, view decrement, IDOR/tenant 404s).

### A2 — Capture protection (the black-out), per-OS
> **Status: Planned — not yet built.**
- **Goal:** while video is on screen, screenshots and recordings render black on every platform that can guarantee it; iOS recording is defeated and screenshots are flagged.
- **App:** the `core/secure_surface` plugin (§4.2) — toggle **on before the first frame**, **off** on leaving the player; iOS `isCaptured` → blank/pause + resume; `userDidTakeScreenshot` → log/flag; `COMPAT-002` warn-and-refuse where unguaranteed.
- **Backend:** none.
- **Design anchor:** the player's "Screen capture blocked" banner + the prototype's "Preview black-out" affordance.
- **Reqs:** `FR-APP-CAP-001..003`, `NFR-APP-CAP-001..006`, `NFR-APP-COMPAT-001/002`.
- **Exit:** the documented **manual capture-protection matrix** (`NFR-APP-MAINT-003`) passes per OS (screenshot + screen-record each); protection provably precedes the first frame.
- **Streams:** app (shims) · wiring (the on-device manual matrix — this is the "live check").

### A3 — Failure states, idle polish, observability
> **Status: App stream DONE (2026-06-27).** Sentry (`sentry_flutter ^8.10.0` +
> `SentryLogSink` + `SentryFlutter.init` in `main.dart`, gated on `SENTRY_DSN`
> dart-define), offline mid-playback (`connectivity_plus ^6.1.4`: pause on
> connectivity loss, auto-resume on restore, engine-error → connectivity check →
> `offline` vs `server` state), and playback-failure context (`videoId` field on
> log calls) are all wired. Idle + failure states + sign-out were already complete
> from A1/A2 — audit confirmed no polish work needed. Wiring stream (7-state live
> drive) is the user's step — see `IMPLEMENTATION-PLAN-native-app-a3-wiring.md`.
- **Goal:** every failure has a specific, recoverable, watermark-safe state; crashes/playback failures report without leaking secrets.
- **Backend:** none.
- **App:** the failure-state screen + its **seven** design states (contract §H; the 8th, *update-required*, lands with A4); verbatim copy + right actions; retry without losing player context; offline pause/resume; idle/home polish (security strip, "how to start", sign-out clears the session + cached URLs/keys); Sentry (no PII/tokens); playback-failure context (type, video id, app version).
- **Reqs:** `FR-APP-ERR-001/002`, `FR-APP-AUTH-004`, `NFR-APP-REL-001/003`, `NFR-APP-OBS-001/002`, `NFR-APP-A11Y-001`.
- **Exit:** all seven states reachable + correct; signing out purges secrets; crash payloads carry zero tokens/PII.
- **Streams:** app · wiring (drive each reason live: unauthorized/forbidden/maxviews/expired/notfound/offline/server).

### A4 — Distribution, signing, CI, min-version, updates
> **Status: Planned — not yet built.**
- **Goal:** repeatable, signed releases on all four stores/channels; the backend can retire stale builds.
- **App:** Android signed **AAB** (Play); iOS App Store (Apple Dev Program); macOS Developer-ID **signed + notarized**; Windows **Authenticode** installer with **no hardcoded paths**; per-env assets present; cert pinning (`SHOULD`); CI builds (and tests where possible) all four; min-version check on launch + before playback.
- **Backend:** config-driven per-platform version floor (hot-reloadable via `IOptionsMonitor`); `GET /api/app/version-status?platform=&version=` (launch check) + **hard enforcement at `redeem`** via `X-App-Version`+`X-Platform` → `426 outdated_app` (the app-only playback step, so the portal's gate call is unaffected). Adds the *update-required* app state.
- **Reqs:** `NFR-APP-DIST-001..005`, `NFR-APP-UPD-001/002`, `FR-APP-UPD-001`, `NFR-APP-MAINT-001/002/004`, `NFR-APP-PRIV-001`, `NFR-APP-SEC-002/006`.
- **Exit:** a signed artifact per platform from CI (not hand-built); a forced-update path proven (gate rejects a stale `X-App-Version`); privacy disclosures drafted.
- **Streams:** backend (min-version) · app (packaging/CI) · wiring (forced-update + a signed-build smoke per platform).

---

## 8. Cross-cutting (every phase)
- **Fully responsive** (§6) — golden tests at 3 widths per screen; a manual breakpoint row each wiring run.
- **Security hygiene** — TLS never bypassed; nothing sensitive logged; secrets in keystore / memory-only (remediates the legacy app every phase).
- **Tenant + audit** ride the reused backend; the app adds no audit and trusts the server gate (never the URI's `videoId`/`sessionId` for authorization).
- **Tests land with features** (`flutter test`); the dev path exercises the **happy** mock (`NFR-APP-REL-003`).
- **"No drift"** — the app's DTOs/URI parser match `docs/contracts/native-app-playback.md` field-for-field; change the contract first.
- **A11y** — controls reachable by assistive tech; OS text-size/contrast honoured; captions where provided.

## 9. Out of scope / deferred (recorded so nothing is silently dropped)
- Offline attendance (removed). · In-app catalogue/enrollment/assignments/quizzes (portal). · Device-consent screen (inherited). · FairPlay / hard iOS screenshot block (deferred escape hatch). · Per-request device re-validation on `/api/me/*` (a **portal**-side hardening item — out of scope here; the app is intentionally device-agnostic, §1.3). · Linux desktop target. · A handoff-code→session bootstrap endpoint (§10.1).

## 10. Resolved & open items
1. ✅ **Handoff semantics resolved.** The 5C handoff is a **video-playback** handoff redeemed against the app's **own keystore session** (it enforces `handoff.StudentId == caller`), **not** a session-bootstrap token. `FR-APP-AUTH-003` ("exchange for a session") is satisfied by the persistent keystore session + sign-in fallback. If signed out when a link arrives, the 60 s handoff likely expires → app signs in and routes to idle with "press Play again". **No new backend endpoint in v1.**
2. ✅ **Deep-link keys resolved.** Canonical = what the portal emits today: `videoId`, `sessionId` (advisory routing only), `handoff` (the credential). Docs `05`/`08` to be updated to match (doc-only).
3. ✅ **Resolved — student serial (`FR-APP-VID-003`).** Add `Student.Serial` in A1: **randomly generated**, unique (`STU-XXXXXX` Crockford base32), minted at registration + backfilled (one migration). The watermark renders **serial + full name**.
4. ✅ **Resolved — no device binding in the app (`FR-APP-DEV-*`; `docs/08` §E updated 2026-06-23).** The player is **device-agnostic**: a student signs in on any machine via the new `app-exchange` (no bind/enforce, no `device_id`). Accountability = the watermark (serial + name) + the view cap. The earlier `X-Device-Token` idea is dropped. *Informed trade-off: device-lock → watermark-traceability for the app; portal access stays one-device-bound.*
5. ✅ **Resolved — min app version (`NFR-APP-UPD-002`).** Config-driven per-platform floor (`IOptionsMonitor`); `GET /api/app/version-status` launch check + hard `426 outdated_app` enforced at `redeem` (the app-only playback step, so the portal's gate call is untouched). See contract §F.
6. ⏳ **Open — JWT audience.** Student tokens carry `aud=salah-bahzad-admin` (shared with staff; the JWT uses the `bahzad` spelling, **not** the deep-link's `bahazad`). The app validates that as-is; minting an app-specific audience is optional, deferred.

## 11. Per-phase docs to produce (as each phase starts, mirroring the portal plan)
- **A0:** ✅ **Met (2026-06-24)** — backend + app + wiring all DONE; proven live on Aspire (10/10, zero drift). `IMPLEMENTATION-PLAN-native-app-a0-{backend,app,wiring}.md`.
- **Auth follow-up — Google sign-in on Windows:** 📋 **Planned (2026-06-25)** — closes the `FR-APP-AUTH-001` Windows gap (`google_sign_in` has no Windows impl); system-browser OAuth loopback (PKCE) → same Firebase `signInWithCredential` → `app-exchange`. App-only, no contract change. `IMPLEMENTATION-PLAN-native-app-google-windows.md`.
- **A1:** `docs/contracts/native-app-playback.md` (**frozen first** — companion to this plan) + `…-a1-{backend,app,wiring}.md`.
- **A2:** `…-a2-{app,wiring}.md` (+ the capture-protection manual matrix doc).
- **A3:** `…-a3-{app,wiring}.md`.
- **A4:** `…-a4-{backend,app,wiring}.md`.
- **Register** this engagement in `docs/IMPLEMENTATION-PLAN-student-portal.md` (a `### Native App (post-Home)` pointer + a Gaps-table row) so it's traceable from the master.

---

➡️ Next: freeze `docs/contracts/native-app-playback.md`, then open A0. · Requirements: [08 — App functional](08-functional-app.md) · [09 — App non-functional](09-non-functional-app.md) · Design: `.claude/Salah Bahzad App/Secure Video App (standalone).html`.
