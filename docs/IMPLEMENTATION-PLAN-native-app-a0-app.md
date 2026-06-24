# Native App · A0 — APP stream (fresh Flutter codebase: shell, theme, auth, deep-link, keystore session)

> Status: **Planned — not yet built** · Created 2026-06-24 · The **app half** of phase **A0** in `docs/IMPLEMENTATION-PLAN-native-app.md` (§7-A0). Stands up a brand-new top-level **`app/`** Flutter codebase (Windows/macOS/iOS/Android), the design system, the responsive shell, device-agnostic Firebase sign-in, the keystore session, and the deep-link parser. **No business video playback yet** — the Player is a placeholder (real playback is A1).
>
> Run in its **own** Claude session, parallel with the backend stream. **File ownership: `app/**` only** (a new sibling to `backend/`/`frontend/`/`docs/`). Match the contract **`docs/contracts/native-app-playback.md`** (§A app-exchange, §C profile, §E deep-link, §G headers, §H error map) **field-for-field**.
> Satisfies: `FR-APP-SCOPE-001..003`, `FR-APP-AUTH-001/002/004`, `FR-APP-LNK-001/002`, `FR-APP-DEV-001`, `FR-APP-NAV-001`, `NFR-APP-SEC-001/003/004`, `NFR-APP-REL-002`, `NFR-APP-PERF-001`, `NFR-APP-MAINT-001/004`, `NFR-APP-A11Y-001`.
> Green gate: `flutter analyze` clean + `flutter test` (widget + golden) + a build smoke (`flutter build windows --debug` and `flutter build apk --debug`).

---

## Design source of truth (READ THIS)
The prototype `.claude/Salah Bahzad App/Secure Video App (standalone).html` — anchors by **comment banner** name (`<!-- ============ SIGN IN ============ -->`, `SPLASH / DEEP-LINK HANDLER`, `IDLE / HOME`, and the desktop-chrome banners `macOS · controls left` / `Windows · controls right`). Tokens, the five screens, the desktop window chrome, and the responsive rules are catalogued in master plan §5–§6. Brand fonts + mascots come from the same design system the portals use (reuse the portal's mascot/logo assets). **Not** the Teacher Portal prototype, **not** `docs/tokens.*`.

## Conventions (mirror `frontend/CLAUDE.md` discipline + the new `app/CLAUDE.md`)
- Latest stable Flutter; `flutter analyze` clean; `dart format` enforced; null-safe.
- **Riverpod** for state; a thin repository layer mapping the contract DTOs field-for-field. OnPush-equivalent: immutable state + `Notifier`s.
- **One responsive widget tree per screen** (master §6 breakpoints `compact<560 / medium 560–1024 / expanded≥1024`); never a phone build vs a desktop build.
- **Security hygiene (remediates the legacy app):** TLS validation **never** disabled (`NFR-APP-SEC-002`); tokens/handoffs/PII **never** logged (`NFR-APP-SEC-003`); session in the **OS keystore** via `flutter_secure_storage` (`NFR-APP-SEC-001`); no dead code/unused deps (`NFR-APP-MAINT-004`).

## Steps

### F1 — Scaffold the `app/` project
- From repo root: `flutter create --org com.salahbahzad --project-name secure_player --platforms=android,ios,macos,windows app` (Linux is **not** a target). Pin Flutter (`app/.fvmrc` or document `flutter --version`).
- `app/pubspec.yaml` deps: `flutter_riverpod`, `dio`, `flutter_secure_storage`, `app_links`, `firebase_core`, `firebase_auth`, `google_sign_in`, `go_router`, `bitsdojo_window` (desktop chrome). *(Player/Sentry/capture deps come in A1–A4.)*
- Author **`app/CLAUDE.md`** (the conventions above + the `lib/` layer map from master §4.1).
- Strip the `flutter create` counter demo (no dead code).

### F2 — Design system + responsive scaffold (`core/theme/`, `app/`)
- `SbTokens` — colors, typography, radii, spacing from master §5.2 (brand blue `#2C6FB3`, navy `#1E3A5F`, paper `#FBFBF7`/`#F4F3ED`, player dark `#0E1620`, secure-green `#46A33E`, amber `#F3C12E`, the text ramp + borders). Bundle fonts (Nunito Sans, Permanent Marker, Caveat, a mono) in `app/fonts/` + `pubspec`. Add brand assets to `app/assets/brand/` (logo, `salah-mascot`, `salah-relaxing`, `salah-failed`).
- `Breakpoints` + `ResponsiveScaffold` (reflow helpers: `isCompact`, two-pane vs stacked, grid columns).
- `AppWindowChrome` (desktop only, `bitsdojo_window`): frameless 44px navy title bar, drag region, **macOS** traffic-lights left / **Windows** min-max-close right, in-bar logo + "Secure" pill + account slot (anchors: the desktop-chrome banners).

### F3 — Networking + secure storage (`core/net/`, `core/storage/`)
- `ApiClient` (Dio): base URL from `--dart-define=API_BASE_URL`; default headers `X-App-Version` + `X-Platform`; **TLS validation always on** (dev trusts the Aspire dev cert via the platform store — never disable). 401-on-`/api/me/*` → single-flight refresh interceptor → retry once.
- `SessionStore` (`flutter_secure_storage`): persist/read/clear access + refresh tokens + `accessTokenExpiresAt`. **Keystore only**; never `SharedPreferences`/files.

### F4 — Auth (`features/auth/`, `data/`)
- `AuthRepository`: `appExchange(firebaseIdToken)` → `POST /api/auth/student/app-exchange` → `StudentAuthResponse` (contract §A); `refresh(refreshToken)` (§B); `me()` → `GET /api/me/profile` (§C, for the watermark identity later). DTOs map the contract field-for-field (note `boundDevice` is `null`).
- Firebase init (`firebase_core`); sign-in: email/password + Google (`google_sign_in` → Firebase credential) → Firebase ID token → `appExchange`. **The app never calls the portal `/exchange`.**
- `AuthController` (Riverpod `Notifier`): states `unknown / signedOut / signingIn / active / error(reason)`; silent refresh scheduled before expiry; sign-out clears the session (`FR-APP-AUTH-004`).
- Map exchange `403 {reason}` → the error states (`account_pending`/`account_rejected`/`account_inactive`) per contract §H; `401`/`429`/network handled.

### F5 — Deep-link (`core/deeplink/`)
- `DeepLinkService` (`app_links`): register `salah-bahazad://` (custom scheme — Windows/macOS) + Universal Links (iOS) / App Links (Android) per-platform manifests/Info.plist/entitlements.
- Parse `salah-bahazad://stream?videoId=…&sessionId=…&handoff=…` → `PlaybackRequest{ videoId, sessionId?, handoff }` (canonical keys, contract §E). Handle **cold-start** (initial link) **and warm** (link stream). Malformed/missing → a clear error route, **never a crash** (`NFR-APP-REL-002`).
- Raw token is **never** read from a URL (`NFR-APP-SEC-004`); only `handoff`.

### F6 — Screens (`features/splash`, `signin`, `idle` + a player placeholder)
- **Splash / deep-link handler** — navy gradient + math doodles + mascot + 3-step progress; if a deep link is pending → (signed-in) route to the **Player placeholder**, (signed-out) → Sign in; else → Idle or Sign in.
- **Sign in** — responsive split (brand panel + form: email/password, "Keep me signed in", **Sign in**, Google); "Only active students can sign in." Reflows to one column on `compact`.
- **Idle / home** — hero ("Welcome back", **Open the student portal** → `FR-APP-NAV-001` opens the portal in the system browser), "How to start a lesson" card, 3-card security strip, **Sign out**. Account bar hidden when the desktop window chrome is present.
- **Player placeholder** — a stub that shows the parsed `PlaybackRequest` and "playback lands in A1" (so the deep-link route is provable now). Real redeem/HLS/watermark/capture are A1/A2.

### F7 — Router + shell wiring (`app/`)
- `go_router` with an auth+deep-link redirect: signed-out → Sign in; signed-in, no link → Idle; pending deep link → Splash → Player placeholder. Desktop wraps everything in `AppWindowChrome`.

### F8 — Tests (`flutter test`)
- **Golden** tests for Splash / Sign in / Idle at **360 / 768 / 1280** widths (proves "fully responsive").
- **Unit:** `AuthController` happy path + each `403 {reason}` → state, with a **fake `AuthRepository`** (the dev path exercises the **happy** mock — `NFR-APP-REL-003`); `DeepLinkService` parser (valid + malformed); `SessionStore` round-trip (mocked secure storage).

## Exit criteria
- App launches **< 3 s** to a usable screen on all four platforms (`NFR-APP-PERF-001`); `flutter analyze` clean.
- Email/pw + Google sign-in → `app-exchange` → keystore session → Idle shell, **on any machine** (no device prompt).
- The three reason-gated states render (`account_pending`/`rejected`/`inactive`).
- A `salah-bahazad://stream?videoId=…&handoff=…` link (cold + warm) routes to the Player placeholder; malformed → clear error, no crash.
- Tokens exist **only** in the OS keystore; nothing sensitive logged. Golden tests green at 3 widths.

## Out of scope (defer)
- Real video playback / redeem / HLS / AES key-loader / watermark (**A1**). · Capture protection shims (**A2**). · The full failure-state set + Sentry + offline polish (**A3**). · Packaging/signing/CI/min-version UI (**A4**). · FairPlay, Linux.

---
## Kickoff prompt (paste into a fresh Claude session at the repo root)
```
You are implementing the APP (Flutter) stream of Native App phase A0 for Salah Bahzad — a brand-new top-level app/ codebase. Edit app/** ONLY.

Read first, in order:
1. docs/IMPLEMENTATION-PLAN-native-app.md (§4 workspace, §5 design plan, §6 responsive)
2. docs/contracts/native-app-playback.md (§A app-exchange, §C profile, §E deep-link, §G headers, §H errors — match field-for-field)
3. docs/IMPLEMENTATION-PLAN-native-app-a0-app.md (this stream)
4. DESIGN SOURCE OF TRUTH = .claude/Salah Bahzad App/Secure Video App (standalone).html (banners: SIGN IN, SPLASH / DEEP-LINK HANDLER, IDLE / HOME, desktop chrome) — NOT the Teacher Portal prototype, NOT docs/tokens.*

Build: scaffold app/ (flutter create, 4 platforms, project secure_player) + app/CLAUDE.md; the SbTokens design system + responsive scaffold + desktop window chrome; Dio client (TLS always on, X-App-Version/X-Platform, single-flight 401-refresh) + keystore SessionStore; device-agnostic Firebase sign-in (email/pw + Google) → POST /api/auth/student/app-exchange → keystore session + silent refresh + 403{reason}→states; app_links deep-link parser (videoId/sessionId/handoff, cold+warm, malformed→error not crash); Splash/Sign in/Idle screens + a Player placeholder; go_router auth+deep-link redirect. No real playback, no capture protection.

Green gate: `flutter analyze` clean + `flutter test` (golden at 360/768/1280 + AuthController/DeepLink/SessionStore unit) + `flutter build windows --debug` and `flutter build apk --debug`. Report all three.
```
