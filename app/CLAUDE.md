# Salah Bahzad — Secure Player (Flutter)

The **native companion app**: a single Flutter codebase (Windows · macOS · iOS · Android) whose only job is **protected video playback**. It is **not** a second portal — browsing, enrollment and quizzes stay in the web portal; the app is reached by a **Play deep link**. Read this before changing any `app/**` code.

Plans & contract (authoritative):
- `docs/IMPLEMENTATION-PLAN-native-app.md` — the master plan (phases A0–A4), workspace (§4), design (§5), responsive (§6).
- `docs/contracts/native-app-playback.md` — the **frozen** app↔backend surface. Match it **field-for-field**; change the contract first if anything moves.
- Per-phase streams: `docs/IMPLEMENTATION-PLAN-native-app-a0-{backend,app,wiring}.md`, …

## Design source of truth

The prototype **`.claude/Salah Bahzad App/Secure Video App (standalone).html`** — anchored by its comment banners (`SIGN IN`, `SPLASH / DEEP-LINK HANDLER`, `PLAYER`, `IDLE / HOME`, `FAILURE / RETRY`, and the desktop-chrome banners `macOS · controls left` / `Windows · controls right`). Tokens, the five screens and the responsive rules are catalogued in master plan §5–§6. Brand fonts + mascots are mirrored from the Salah Bahzad design system (the same set the web portals ship).

⚠️ **Not** the Teacher Portal prototype, **not** `docs/tokens.*` — those are deprecated for design.

## Conventions

- **Latest stable Flutter**, null-safe Dart. `flutter analyze` must be **clean** (zero issues) and `dart format` is enforced.
- **Riverpod** for state (`Notifier` + `NotifierProvider`, no codegen) + a thin repository layer that maps the contract DTOs field-for-field. Immutable state; `ConsumerWidget`/`ConsumerStatefulWidget` for the UI.
- **One responsive widget tree per screen** — reflow by width via `ResponsiveBuilder`/`LayoutBuilder`, **never** a separate phone vs desktop build. Breakpoints (`core/responsive/breakpoints.dart`): `compact < 560` / `medium 560–1024` / `expanded ≥ 1024` (the prototype's 402 / 862 / 1180 frames).
- **Design tokens only** — every widget references `core/theme/SbColors`/`SbSpace`/`SbRadii`/`SbMotion`/`SbFonts`, never an inline hex/size. Change a value in `sb_tokens.dart`, not in a widget.
- **Presentational view ↔ page split** — each screen is a pure `…View` (deterministic, golden-tested) plus a `…Page` (`Consumer…`) that wires Riverpod + navigation. Keep timers/animations/Firebase out of the views.
- **Security hygiene (remediates the legacy app), every phase:**
  - TLS validation is **never** disabled (`NFR-APP-SEC-002`) — `ApiClient` never sets `badCertificateCallback`. Dev talks to the API over **HTTP** (the AppHost `app` endpoint) because Dart can't trust the local dev cert; prod is HTTPS with a real CA cert (see Networking).
  - Tokens / handoffs / signed URLs / PII are **never** logged (`NFR-APP-SEC-003`). `PlaybackRequest.toString()` redacts the handoff.
  - The session lives **only** in the OS keystore (`flutter_secure_storage`, `NFR-APP-SEC-001/004`) — never `SharedPreferences`/files. Signed URLs + HLS keys will be **memory-only** (A1).
  - The app **never** trusts the deep link's `videoId`/`sessionId` for authorization — only the server gate.
- **Device-agnostic** (contract §0/§A): the app authenticates via `POST /api/auth/student/app-exchange` (no binding, no `device_id`); it **never** calls the portal's device-bound `/api/auth/student/exchange`. Anti-sharing is the watermark (serial + name, A1) + the per-video view cap.
- **Tests land with features** (`flutter test`): widget/golden + unit per feature. The dev/test path exercises the **happy** path with hand fakes — Firebase is never touched in tests (`NFR-APP-REL-003`).

## `lib/` layer map (master §4.1)

```
lib/
├─ main.dart                 # bootstrap: desktop window + Firebase init + ProviderScope
├─ app/                      # app.dart (root), router.dart (go_router + redirect),
│                            # app_window_chrome.dart (desktop), providers.dart (DI)
├─ core/
│  ├─ theme/                 # SbTokens, SbText/SbFonts, SbTheme, SbAssets
│  ├─ responsive/            # Breakpoints + SbLayout, ResponsiveBuilder
│  ├─ platform/              # AppPlatform (X-Platform value, desktop, controls side)
│  ├─ net/                   # ApiClient (Dio, single-flight 401-refresh), TokenRefresher,
│  │                         # AppConfig, ApiException
│  ├─ storage/               # SessionStore (keystore), Session
│  └─ deeplink/              # PlaybackRequest (parser), DeepLinkService, PendingDeepLink
├─ data/                     # DTOs (contract §A/§C) + AuthRepository
├─ features/                 # splash / signin / idle / player / errors / auth
└─ widgets/                  # shared: MathDoodles, SecurePill
```

## Networking

- Base URL / app version / portal URL come from `--dart-define` (`API_BASE_URL`, `APP_VERSION`, `PORTAL_URL`) with dev defaults in `AppConfig`. The dev default is **`http://localhost:5080`** — the AppHost's stable, named `app` HTTP endpoint (`WithHttpEndpoint(port: 5080, name: "app")`). It is **not** `:5010` (that's Aspire's DCP control plane, not the API) and **not** the API's dynamic proxied port (Aspire reassigns it every run). **Dev is HTTP on purpose:** Dart/Flutter does not read the OS certificate store, so the ASP.NET Core dev cert can't be trusted client-side and an HTTPS dev endpoint would fail the TLS handshake (`HandshakeException`). TLS is a prod concern — production builds pass `--dart-define=API_BASE_URL=https://…` pointing at a real CA-signed cert Dart trusts. This does **not** weaken `NFR-APP-SEC-002`: `ApiClient` still never disables validation / sets `badCertificateCallback`.
- `ApiClient` sends `X-App-Version` + `X-Platform` on every call (contract §G), injects the Bearer from the in-memory `SessionStore`, and does a **single-flight** 401 refresh that retries the original request once. `TokenRefresher` uses a **bare** Dio (no interceptor) so a refresh 401 can never recurse.
- **Windows Google sign-in** uses a system-browser OAuth loopback (PKCE) instead of `google_sign_in` (which has no Windows impl). It needs a Google **"Desktop app"** OAuth client (created once in the Firebase project's Google Cloud console), passed via `--dart-define=GOOGLE_DESKTOP_CLIENT_ID=…` / `--dart-define=GOOGLE_DESKTOP_CLIENT_SECRET=…` (optional `GOOGLE_SCOPES`, default `openid email profile`). With no client id, `AppConfig.hasDesktopGoogleOAuth` is false and the Google button stays hidden on Windows (mobile/macOS are unaffected — they keep the plugin). The desktop client's "secret" is Google's installed-app pseudo-secret (not truly secret), still injected via config, never hardcoded; PKCE+`state` protect the exchange and TLS is never disabled on the token POST. Firebase consumes the resulting Google tokens via the same `signInWithCredential` path as mobile.

## Platform notes

- **Deep-link scheme** `salah-bahazad://stream` (note the `bahazad` spelling — distinct from the JWT's `bahzad`) is registered per platform: Android intent-filter (`AndroidManifest.xml`), iOS/macOS `CFBundleURLTypes` (`Info.plist`). Windows custom-scheme registration is an installer/registry concern (A4).
- **Desktop window chrome** is custom & frameless (`window_manager`, `TitleBarStyle.hidden`) — macOS controls left, Windows controls right. The in-screen account bar is hidden when the chrome is present.
- **Windows build + ATL:** `flutter_secure_storage_windows` compiles against ATL (`<atlstr.h>`). The selected MSVC toolset (often the standalone Build Tools) may lack ATL even when another VS edition on the machine ships it. `windows/CMakeLists.txt` discovers ATL across VS installs and adds it — do not remove that block or the Windows build breaks with `C1083: atlstr.h`.
- **Firebase:** `firebase_core`/`firebase_auth` build on all four platforms. With no project configured yet (A0), `Firebase.initializeApp()` is guarded in `main.dart` so the app still launches; a real project + `firebase_options.dart` lands in the wiring stream. Google sign-in goes through one `FirebaseAuth.signInWithCredential` path on every platform; the *credential source* varies — `google_sign_in` plugin on Android/iOS/macOS (`AppPlatform.googleSignInPluginSupported`), a system-browser OAuth loopback on Windows (`DesktopGoogleCredentialSource`, gated on `AppConfig.hasDesktopGoogleOAuth`). Whether the button shows is `identityProvider.googleSupported`, not the platform alone.
- **Android** `minSdk = 23` (Firebase Auth 6.x / firebase_core 4.x floor).

## Build / test (the green gate)

```
flutter analyze              # must be clean
flutter test                 # unit + golden (360 / 768 / 1280)
flutter build windows --debug
flutter build apk --debug
```

Golden references live in `test/golden/goldens/`. Regenerate with `flutter test --update-goldens` after an intentional visual change, and eyeball the diff. Goldens load the bundled brand fonts from disk (`test/support/test_fonts.dart`) so text renders real glyphs.

## Out of scope (A0) — deferred

Real video playback / redeem / HLS / AES key-loader / watermark (**A1**) · capture-protection shims (**A2**) · the full seven failure states + Sentry + offline polish (**A3**) · packaging / signing / CI / min-version UI (**A4**) · FairPlay · Linux desktop.
