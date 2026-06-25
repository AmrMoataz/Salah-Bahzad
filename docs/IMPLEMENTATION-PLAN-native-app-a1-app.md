# Native App · A1 — APP stream (Player screen: redeem → AES-128 HLS + key-loader, watermark, view budget)

> Status: **Planned — not yet built** · Created 2026-06-25 · The **app half** of phase **A1** in `docs/IMPLEMENTATION-PLAN-native-app.md` (§7-A1, lines 191–199). Builds the **Player** screen on top of the A0 foundation: from a Play deep link it redeems the handoff (`POST /api/me/videos/playback/redeem`), plays **AES-128 HLS** through a custom **key-loader** that authenticates `GET /api/me/videos/{id}/hls.key` (the 16-byte key stays **in memory only**), renders the **dual-layer watermark** of `{serial} · {fullName}`, surfaces **"N of M views left"**, and ships player controls (play/pause/seek/speed 1×–2×/volume/fullscreen — **no download**). Everything the player calls on the backend **already EXISTS** (the 5C video gate + A0 auth); the **one** new backend touch (`Student.Serial` on `/api/me/profile`) is the **separate A1 backend stream** — the app DTO already carries `serial`. **No new backend work in this stream.**
>
> Run in its **own** Claude session, parallel with the backend stream. **File ownership: `app/**` only.** Match the contract **`docs/contracts/native-app-playback.md`** (§C profile, §D the three gate routes + DTO shapes, §G headers, §H error→state map) **field-for-field** — **change the contract first if anything moves.**
> Satisfies: `FR-APP-VID-001` (redeem → AES-128 HLS playback), `FR-APP-VID-002` (custom authenticated key-loader, key memory-only), `FR-APP-VID-003` (watermark = serial + full name), `FR-APP-VID-004` ("N of M views left"), `FR-APP-VID-005` (controls, no download, ABR ≤1080p), `FR-APP-AUTH-003` (handoff redeemed against the keystore session; retry reuses the same handoff within TTL — no double-decrement), `NFR-APP-SEC-005` (signed segment URLs + AES key never persisted), `NFR-APP-PERF-002/003` (first frame < 4 s p95 / smooth ABR), `NFR-APP-REL-001` (survives resize/rotation without losing playback context), `FR-APP-ERR-002` (player-reachable failure states render inline).
> Green gate: `flutter analyze` clean + `flutter test` (widget + golden at 360/768/1280) + a build smoke (`flutter build windows --debug` and `flutter build apk --debug`).
>
> ⚠️ **Pre-registered technical risk (master plan §2 / §A1):** there is **no video engine in `app/pubspec.yaml` yet**, and the real risk is the **authenticated AES-128 key fetch** (native HLS engines fetch the `#EXT-X-KEY` URI **without** the Bearer → `401`). **F1 is a spike** that must prove the chosen engine can attach an auth header to the key request (or accept a loopback-proxy URL) **before** the rest of the screen is built. See F1/F3.

---

## Design source of truth (READ THIS)
The prototype **`.claude/Salah Bahzad App/Secure Video App (standalone).html`** — anchor on the **`<!-- ============ PLAYER ============ -->`** banner (and the desktop-chrome banners `macOS · controls left` / `Windows · controls right` for how the player is hosted inside the frameless window). The prototype renders its frames as embedded base64 PNGs (not greppable text), so the **authoritative readable spec is master plan §5.3 (the `PLAYER` row, line 158) + §5.2 tokens** — read those alongside the banner. **Not** the Teacher Portal prototype, **not** `docs/tokens.*`, **not** `docs/03-components.md` (deprecated for design).

Player spec (from the `PLAYER` banner via master §5.3):
- **Stage:** dark, full-bleed video on `SbColors.playerBg` `#0E1620` / `playerMid` `#0A111C` / `playerDeep` `#05090F`; stage tint `playerStageA` `#15233A` (`sb_tokens.dart:29-32`).
- **Top bar:** back chevron · lesson title · green **Encrypted** chip (shield, `SbColors.greenSoft` `#86C7A6`, `sb_tokens.dart:37`) · **"N of M views left"** counter in amber (`SbColors.amber` `#F3C12E`, `sb_tokens.dart:44`), mono face (`SbFonts.mono`). The existing `_EncryptedChip` / `_TopBar` in the A0 placeholder are ready-made reference.
- **Dual-layer watermark** of **`{serial} · {fullName}`** (contract §C — serial + name, **NOT** phone): layer 1 = faint tiled wash across the frame; layer 2 = a single chip that **repositions ~2.6 s**. Durations already exist: `SbMotion.watermarkInterval` 2600 ms / `SbMotion.watermarkReposition` 1400 ms (`sb_tokens.dart:121-122`). Mono font (`SbFonts.mono`).
- **"Screen capture blocked" banner** — a **static reassurance banner only** in A1 (the real OS black-out flag is A2's `core/secure_surface`).
- **Controls (wrap on narrow widths):** play/pause · seek bar **with elapsed/remaining timers** · **speed 1× / 1.25× / 1.5× / 2×** · mute/volume · fullscreen. **No download / no export control** — explicit (`FR-APP-VID-005`).
- **Progress accent:** `SbColors.accentBlue` `#3E8EDE` (`sb_tokens.dart:19`). **A1 adds no new hex** — every player dark, amber, green, and both watermark durations already live in `sb_tokens.dart`.

## Conventions (mirror `app/CLAUDE.md` + `frontend/CLAUDE.md` discipline)
- Latest stable Flutter; `flutter analyze` **clean** (zero issues); `dart format` enforced; null-safe.
- **Riverpod** (`Notifier` + `NotifierProvider`, no codegen) + a thin repository mapping contract DTOs **field-for-field**.
- **Presentational view ↔ page split** — `player_view.dart` is a pure, golden-tested `…View` (no timers/engine/Firebase); `player_page.dart` (`ConsumerStatefulWidget`) wires Riverpod, the engine lifecycle, and navigation. (`app/CLAUDE.md` "view ↔ page split".)
- **One responsive widget tree** — controls reflow/wrap by width via `ResponsiveBuilder`/`LayoutBuilder`; breakpoints `compact < 560 / medium 560–1024 / expanded ≥ 1024`. Never a phone build vs a desktop build. Inside the desktop frameless chrome the in-screen account bar stays suppressed (master §5.4).
- **Design tokens only** — reference `SbColors`/`SbMotion`/`SbFonts`/`SbRadii`, never an inline hex/size.
- **Security hygiene (this is the security-critical screen):** the **AES key and signed segment/manifest URLs are memory-only — never written to keystore, `SharedPreferences`, a file, or a log** (`NFR-APP-SEC-005`). TLS validation is **never** disabled (`ApiClient` never sets `badCertificateCallback`). The handoff and tokens are never logged (`PlaybackRequest.toString()` already redacts the handoff). The app **never** trusts the deep link's `videoId`/`sessionId` for authorization — only the server gate.
- **Reuse A0, do not rebuild:** the deep-link → `/player` route is already wired (`router.dart:54-65` redirects a `PendingValid` link to `/player`); `ApiClient` (`api_client.dart`) already injects the Bearer, sends `X-App-Version`+`X-Platform` (contract §G), and does single-flight 401-refresh; `SessionStore.current.accessToken` (`session_store.dart:23`) is the Bearer the key-loader needs; `AuthRepository.me()` (`auth_repository.dart:35`) already fetches the profile and `StudentProfile.watermarkLabel` (`student_profile.dart:41`) already returns the exact `"{serial} · {fullName}"` string. **Do not widen any of these.**

## Steps

### F1 — Video engine + the AES-128 key-loader **spike** (`app/pubspec.yaml`) ⚠️ DO THIS FIRST
- **Add the engine dep — none exists today** (neither `video_player` nor `media_kit` is in `pubspec.yaml`). `video_player` has **no Windows/macOS backend**, and desktop is a first-class target, so a single cross-platform engine is required. **Recommended: `media_kit` (libmpv)** — it exposes `http-header-fields`, so the Bearer can ride directly on the key/segment requests and libmpv decrypts AES-128 HLS natively across all four OSes. (Alternative engines are evaluated in F3.)
- **The risk to retire before building the screen (`FR-APP-VID-001/002`, `NFR-APP-SEC-005`):** native HLS engines fetch the `#EXT-X-KEY` URI **without** the app's JWT, so an un-augmented key request `401`s. The spike must prove **one** of: (a) the engine attaches an `Authorization: Bearer` header to the key (and, if needed, segment) request, **or** (b) the engine plays a manifest whose key URL points at the in-process loopback proxy (F3). AES-128 *decryption* is supported by ExoPlayer/AVPlayer/libmpv — **the gap is the authenticated key fetch + keeping the key/URLs off disk.**
- **Output of the spike:** a one-paragraph note in the wiring doc recording the chosen engine + injection mechanism, and a throwaway test that streams one real AES-128 HLS asset end-to-end. Only after this passes do F4–F6 proceed.
- ABR target **up to 1080p** (`FR-APP-VID-005`); first frame **< 4 s p95** (`NFR-APP-PERF-002`).

### F2 — Playback DTOs + repository (`app/lib/data/`)
- **CREATE `app/lib/data/dtos/playback_handoff.dart`** — `PlaybackHandoff` mapping `PlaybackHandoffDto` (contract §D.1) **field-for-field**: `handoffCode` (48-hex), `expiresAtUtc` (ISO-8601 offset). Used only when the app calls the gate directly (D1, below).
- **CREATE `app/lib/data/dtos/playback_manifest.dart`** — `PlaybackManifest` mapping `PlaybackManifestDto` (contract §D.1) **field-for-field**: `manifestContent` (the rewritten `.m3u8` text — signed R2 segment URLs ~120 s TTL + an absolute key URL baked into `#EXT-X-KEY METHOD=AES-128,URI="…",IV=0x…`), `keyUrl` (absolute URL to `/api/me/videos/{id}/hls.key`), `expiresAtUtc`.
- **CREATE `app/lib/data/playback_repository.dart`** — wraps contract §D over the existing `ApiClient` (so it inherits Bearer + `X-App-Version`/`X-Platform` + single-flight refresh):
  - `redeem(String handoffCode)` → `POST /api/me/videos/playback/redeem` body `{ "handoffCode": "…" }` → `PlaybackManifest` (**D2 — no view spent**). Enforced server-side: `handoff.StudentId == caller`.
  - `startPlayback(String videoId)` → `POST /api/me/videos/{videoId}/playback` (no body) → `PlaybackHandoff` (**D1 — spends one view**, audits `VideoPlaybackStarted`). Only for a direct-play path; the deep-link path **already arrives with a handoff** minted by the portal's D1, so the normal player path is **D2 → D3**, not D1.
  - `keyBytes(String videoId)` → `GET /api/me/videos/{videoId}/hls.key` → raw 16 bytes (`application/octet-stream`) **for the key-loader** (D3). Returns the `Uint8List`; the caller keeps it in memory only.
  - Map gate errors (contract §D.2) to a typed exception carrying status + `reason` + `detail` (reuse the A0 `ApiException`): `409 not_ready`, `403 not_enrolled` / `enrollment_expired` / `quiz_required` / `no_views_remaining`, `404` (not found / wrong tenant — IDOR-safe), `410 handoff_expired`. **`426 outdated_app` is A4, not A1** — do not handle it here (the app still sends the headers, enforcement ships A4).

### F3 — HLS key-loader / loopback proxy seam (`app/lib/core/playback/`)
- **CREATE `app/lib/core/playback/hls_key_loader.dart`** — the seam that authenticates the AES-128 key fetch: given a `videoId`, return the 16 key bytes from `PlaybackRepository.keyBytes`, attaching `Authorization: Bearer ${store.current.accessToken}` (D3 is `RequireStudent`). The key is **held in memory only and never persisted/logged** (`NFR-APP-SEC-005`).
- **CREATE `app/lib/core/playback/local_manifest_proxy.dart`** — the **recommended, engine-agnostic** mechanism (pick in F1; fallbacks in F3): run a Dart `HttpServer` bound to `127.0.0.1:0`, serve `manifestContent` from memory with its key URL rewritten to the loopback, and on the key (and, if needed, segment) request attach the Bearer and stream the bytes. **Precedent already in the repo:** `app/lib/features/auth/google/desktop_oauth_client.dart:145-150` already does a `HttpServer.bind(InternetAddress.loopbackIPv4, 0)` loopback — same pattern, reuse it. The key never touches disk; the proxy lives only while the player is mounted.
- **Engine options (record the pick in the F1 spike note):**
  1. **Local loopback injector (recommended, engine-agnostic):** above — satisfies `NFR-APP-SEC-005` independent of engine.
  2. **`media_kit` (libmpv) `http-header-fields`:** Bearer rides directly on key/segment requests; one engine for all four OSes.
  3. **Per-platform native key-loader:** ExoPlayer `HttpDataSource.Factory` default headers (Android); `AVAssetResourceLoaderDelegate` intercepting the key URI (iOS/macOS) — most native work, via `secure_surface`-style MethodChannels. **Heaviest; only if 1/2 fail the spike.**
- **CREATE `app/lib/app/providers.dart` additions:** `playbackRepositoryProvider`, `hlsKeyLoaderProvider` / `localManifestProxyProvider`, `playerControllerProvider` (F4). Mirror the existing provider style in `providers.dart`.

### F4 — Player controller + state (`app/lib/features/player/player_controller.dart`)
- **CREATE** a Riverpod `Notifier` holding an immutable `PlayerState`: `loading / playing / paused`, `position`, `duration`, `buffered`, `speed` (1× / 1.25× / 1.5× / 2×), `muted` (+ volume), `fullscreen`, `viewsLeft` (see open question below), and an `error(reason)` slot mapped from contract §D.2/§H.
- **Flow on mount:** read the consumed `PlaybackRequest` (`pendingDeepLinkProvider`, already released by the placeholder pattern at `player_placeholder_page.dart:30-36`) → `redeem(request.handoff)` (D2) → feed `manifestContent` to the engine via the F3 key-loader/proxy → start playback. **No D1 call on this path** — the handoff already came from the portal's gate.
- **Retry rule (`FR-APP-AUTH-003`, contract §D.3):** on a transient failure **within the handoff TTL**, retry the **same** handoff/manifest — **never re-call D1/redeem in a way that spends a second view** (no double-decrement). On `410 handoff_expired` (TTL elapsed), route to idle with **"press Play again"** guidance (master plan §10.1) — the app does not silently re-mint.
- **Position ticker stays off the widget rebuild path:** the engine is the clock — push immutable state from its position stream, never a wall-clock `DateTime.now()` poll that drifts. Survive resize/rotation without losing playback context (`NFR-APP-REL-001`).

### F5 — Player page + view (`app/lib/features/player/player_page.dart`, `player_view.dart`)
- **CREATE `player_page.dart`** (`ConsumerStatefulWidget`, **replaces** the A0 placeholder): owns the engine lifecycle (create on mount, dispose on leave), drives `redeem → key → play` via `playerControllerProvider`, and mounts/unmounts the **capture-flag hook** (a no-op stub in A1; A2's `core/secure_surface` fills the native side). Releases the pending deep link after the first frame (reuse the `addPostFrameCallback(... consume())` pattern from `player_placeholder_page.dart:34-36`).
- **CREATE `player_view.dart`** — pure presentational `…View` (golden-tested): dark stage, top bar (back · title · **Encrypted** chip · **"N of M views left"** in amber), the watermark layer, the controls bar, and the static "Screen capture blocked" banner. **No engine/timer/Firebase** inside the view — it takes a `PlayerState` + callbacks.

### F6 — Player widgets (`app/lib/features/player/widgets/`)
- **CREATE `dynamic_watermark.dart`** — the dual-layer watermark of `profile.watermarkLabel` (= `"{serial} · {fullName}"`, `student_profile.dart:41`): layer 1 a faint **tiled wash**, layer 2 a single **chip repositioning every `SbMotion.watermarkInterval` (2600 ms)** with a `watermarkReposition` (1400 ms) move. Mono font, legible-but-unobtrusive (`FR-APP-VID-003`). Identity comes from `AuthRepository.me()` (already wired); **the backend stream populates `serial`** — until then it renders the empty-string fallback (`student_profile.dart:46`).
- **CREATE `player_controls.dart`** — play/pause · seek bar **with elapsed/remaining timers** (mono) · **speed 1× / 1.25× / 1.5× / 2×** · mute/volume · fullscreen. **No download/export control** (`FR-APP-VID-005`). Wraps on `compact`.
- **CREATE `capture_blocked_banner.dart`** — the static "Screen capture blocked" pill/banner (reassurance only in A1; real black-out = A2).
- **(Optionally) promote `app/lib/widgets/encrypted_chip.dart`** from the placeholder's inlined `_EncryptedChip` (`player_placeholder_page.dart:117-150`) so the player top bar reuses it.

### F7 — Router + provider wiring (`app/lib/app/`)
- **MODIFY `app/lib/app/router.dart`** — swap the `PlayerPlaceholderPage` import (`router.dart:9`) and route builder (`router.dart:30-33`) for the real `PlayerPage`. **The redirect logic at `router.dart:54-65` already routes a `PendingValid` link to `/player` — no redirect change needed.**
- **MODIFY `app/lib/app/providers.dart`** — register the F2–F4 providers (`playbackRepositoryProvider`, key-loader/proxy, `playerControllerProvider`).
- **DELETE `app/lib/features/player/player_placeholder_page.dart`** once the real page lands (no dead code — `app/CLAUDE.md`). Keep its `_TopBar`/`_EncryptedChip` as the reference for F5/F6 before deleting.

### F8 — Tests (`flutter test`)
- **Golden** (`player_view.dart` at **360 / 768 / 1280** widths, per `app/CLAUDE.md`): loading, playing (controls + timers), and the watermark layer rendered; goldens load the bundled mono/brand fonts from `test/support/test_fonts.dart`. Controls **wrap** on `compact`.
- **Unit (hand fakes, Firebase/engine never touched — `NFR-APP-REL-003`):**
  - `PlayerController` happy path: fake `PlaybackRepository` returns a `PlaybackManifest` → controller reaches `playing`; speed/mute/fullscreen toggles mutate state.
  - **Retry within TTL reuses the same handoff** (no second `redeem`/D1) — assert the fake repo's `redeem`/`startPlayback` call counts (`FR-APP-AUTH-003`, contract §D.3).
  - Each gate reason → state, **using the §H rows that exist**: `403 not_enrolled` → *forbidden*, `403 no_views_remaining` → *maxviews*, `403 enrollment_expired` → *expired*, `404`/`410 handoff_expired` → *notfound*. **`409 not_ready` and `403 quiz_required` have NO dedicated §H row** — render their `detail` inline (a generic player-reachable failure state; `FR-APP-ERR-002`), do **not** invent a §H title/action for them. (Resolve in the contract if a verbatim title is wanted.)
  - **Key/URL hygiene:** assert the AES key and signed URLs never reach the logger or `SessionStore` (`NFR-APP-SEC-005`) — capture via a fake logger + a no-write storage spy.
  - `HlsKeyLoader` / `LocalManifestProxy`: the key request carries `Authorization: Bearer …`; the key bytes are returned and not persisted.

## Exit criteria
- A Play deep link → **first frame < 4 s p95** (`NFR-APP-PERF-002`); the encrypted lesson plays via AES-128 HLS with the custom key-loader (`FR-APP-VID-001/002`).
- The AES key + signed segment/manifest URLs exist **only in memory** — nothing on disk, in keystore, or in any log (`NFR-APP-SEC-005`); proven by the F8 hygiene tests.
- The **dual-layer watermark** of `{serial} · {fullName}` is legible and **repositions ~2.6 s** (`FR-APP-VID-003`).
- **"N of M views left"** is surfaced in the top bar (`FR-APP-VID-004`) — pending the source decision (open question).
- Controls work: play/pause, seek + timers, **speed 1×/1.25×/1.5×/2×**, mute/volume, fullscreen; **no download control exists** (`FR-APP-VID-005`); ABR scales **up to 1080p**.
- **Retry within the handoff TTL reuses the same handoff** (no second view decrement); `410 handoff_expired` → clean "press Play again" guidance (`FR-APP-AUTH-003`).
- Playback survives window resize / rotation without losing context (`NFR-APP-REL-001`); `flutter analyze` clean; goldens green at 3 widths.

## Out of scope (defer)
- The real OS **capture black-out** / `core/secure_surface` (**A2**) — A1 ships only the static "Screen capture blocked" banner. · The **full seven failure-state screen** + Sentry + offline polish (**A3**) — A1 handles only the player-reachable inline states. · **Min-version UI** + the `426 outdated_app` / *update-required* state (**A4**) — the app still sends `X-App-Version`/`X-Platform`, but enforcement ships A4. · Packaging / signing / CI (**A4**). · `Student.Serial` field + migration + `/api/me/profile` surface (**A1 BACKEND stream**, not this stream). · FairPlay, Linux.

---

## Open questions — RESOLVED
- ✅ **"N of M views left" source (`FR-APP-VID-004`) — RESOLVED 2026-06-26 (option a).** The frozen contract **§D** now carries **`accessRemaining`** (the "N", post-Play remaining) + **`accessAllowed`** (the "M", total granted) on **`PlaybackManifestDto`** (the redeem response). The redeem handler reads them off the enrollment's video-access (the handoff already carries `EnrollmentId`) — no new endpoint, no extra round-trip. The app maps them onto `PlayerState.viewsLeft`/`viewsTotal` and the top-bar counter renders `"{N} of {M} views left"` (falling back to the marked chip only if an older API omits them). Proven: backend `VideoPlaybackTests` (`accessRemaining=1/accessAllowed=2`), app `91/91` + golden ("2 of 3 views left"), and **live** on the Aspire stack (`accessRemaining`/`accessAllowed` in the manifest JSON). The earlier fallback-chip warning is superseded.

---
## Kickoff prompt (paste into a fresh Claude session at the repo root)
```
You are implementing the APP (Flutter) stream of Native App phase A1 for Salah Bahzad — the Player screen on top of the existing app/ foundation. Edit app/** ONLY.

Read first, in order:
1. docs/contracts/native-app-playback.md (§C profile, §D the three gate routes + PlaybackHandoffDto/PlaybackManifestDto shapes + §D.2 reasons, §G headers, §H error→state map — match field-for-field; change the contract first if anything moves)
2. docs/IMPLEMENTATION-PLAN-native-app.md (§A1 lines 191–199, §5.3 PLAYER row, §5.2 tokens, §6 responsive, §10.1 handoff-expiry guidance)
3. docs/IMPLEMENTATION-PLAN-native-app-a1-app.md (this stream)
4. app/CLAUDE.md (conventions, layer map, security hygiene, networking)
5. DESIGN SOURCE OF TRUTH = .claude/Salah Bahzad App/Secure Video App (standalone).html (banner: PLAYER, plus the desktop-chrome banners) — NOT the Teacher Portal prototype, NOT docs/tokens.*, NOT docs/03-components.md.

Build: FIRST do the F1 key-loader spike — add a video engine (recommended media_kit) and PROVE the authenticated AES-128 key fetch works (engine header injection OR a 127.0.0.1:0 loopback proxy reusing desktop_oauth_client.dart:145-150) before building the screen. Then: playback DTOs + PlaybackRepository over the existing ApiClient (redeem D2 / startPlayback D1 / keyBytes D3); the core/playback key-loader + loopback proxy (key memory-only, NFR-APP-SEC-005); the PlayerController (redeem→key→play, retry reuses the SAME handoff within TTL — no double-decrement); player_page (engine lifecycle, replaces the A0 placeholder) + pure player_view; widgets = dual-layer watermark of {serial}·{fullName}, controls (play/pause/seek/timers/speed 1×–2×/volume/fullscreen, NO download), static "Screen capture blocked" banner; swap the placeholder in router.dart + wire providers.dart; delete the placeholder page. Reuse A0: the /player route + redirect, ApiClient (Bearer + X-App-Version/X-Platform + single-flight refresh), SessionStore.current.accessToken, AuthRepository.me() + StudentProfile.watermarkLabel. No capture black-out (A2), no 426/min-version (A4), no Serial backend work (A1 backend stream).

FLAG, do not invent: "N of M views left" has no field in the frozen contract for the deep-link path — surface the open question, do not hardcode.

Green gate: `flutter analyze` clean + `flutter test` (golden at 360/768/1280 + PlayerController/key-loader/retry/hygiene unit) + `flutter build windows --debug` and `flutter build apk --debug`. Report all three.
```
