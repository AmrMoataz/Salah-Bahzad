# Native App · Auth follow-up — Google (Firebase) sign-in on **Windows** desktop

> Status: **Planned — not yet built** · Created 2026-06-25 · A cross-cutting **auth** enhancement to the `app/` Flutter codebase, layered on top of phase **A0** (`docs/IMPLEMENTATION-PLAN-native-app-a0-app.md` §F4). Closes the one platform gap in `FR-APP-AUTH-001`: today "Continue with Google" is **hidden on Windows** because the `google_sign_in` plugin ships no Windows implementation. This adds a Windows path so Google sign-in works on **all four** targets — through **Firebase**, exactly as on mobile.
>
> **File ownership: `app/**` only.** No backend change: the frozen contract `docs/contracts/native-app-playback.md` §A `POST /api/auth/student/app-exchange` is untouched — it still receives a **Firebase ID token**. One-time, out-of-repo: an OAuth "Desktop app" client must be created in the Firebase project's Google Cloud console (see §G-Setup).
> Satisfies: `FR-APP-AUTH-001` (Windows). Honors: `NFR-APP-SEC-002` (TLS never disabled), `NFR-APP-SEC-003` (tokens/PII never logged), `NFR-APP-REL-003` (tests never touch Firebase/network), `NFR-APP-MAINT-001/004` (seam, no dead code).
> Green gate: `flutter analyze` clean + `flutter test` (unit + the Windows Sign-in golden now including the Google button) + `flutter build windows --debug`.

---

## Background — why it's Windows-only, and why it's still "through Firebase"

On **every** platform, Google sign-in reduces to one Firebase call:

```dart
final cred = GoogleAuthProvider.credential(idToken: ..., accessToken: ...);
final userCred = await FirebaseAuth.instance.signInWithCredential(cred);
final firebaseIdToken = await userCred.user!.getIdToken();   // → app-exchange (unchanged)
```

`google_sign_in` is **not** a separate auth system — it is only the *token source* that shows the native Google account picker and returns the Google `idToken`/`accessToken` that Firebase then consumes. It ships native implementations for **Android / iOS / macOS / Web only** — there is **no Windows** implementation, and its `authenticate()` throws on Windows. That single gap is why `AppPlatform.googleSignInSupported` returns `false` on Windows today and the button is hidden.

Firebase cannot do the whole flow itself on Windows native either: the FlutterFire plugin does **not** implement provider popups/redirects (`signInWithProvider`) on Windows (that exists only on Web). So on Windows we must obtain the Google OAuth token ourselves, then hand it to the **same** Firebase credential step. The browser step is unavoidable; the Firebase step is unchanged.

**Decision — system-browser OAuth loopback (PKCE), not an embedded webview.** Google's published guidance for desktop/"installed" apps is the system browser + loopback-IP redirect; Google discourages (and sometimes blocks, `disallowed_useragent`) embedded webviews for OAuth. The system-browser flow is both more secure and more reliable, and needs no extra heavy dependency — `url_launcher` is already a dependency and `dart:io HttpServer` covers the loopback listener.

## Approach (Windows only; mobile/macOS keep `google_sign_in` verbatim)

1. Generate PKCE `code_verifier` + `code_challenge` (S256) and a random `state`.
2. Bind a one-shot `HttpServer` on `127.0.0.1:0` (ephemeral port; loopback is Windows-Firewall-exempt → no prompt).
3. `launchUrl(authUri, mode: LaunchMode.externalApplication)` → Google's auth endpoint with `client_id`, `redirect_uri=http://127.0.0.1:<port>`, `response_type=code`, `scope=openid email profile`, `code_challenge`, `code_challenge_method=S256`, `state`.
4. User signs in in their real browser → Google redirects to the loopback with `?code=…&state=…`.
5. The local server validates `state`, captures `code`, serves a minimal "You can close this tab and return to Salah Bahzad" page, then shuts down.
6. Token exchange: `POST https://oauth2.googleapis.com/token` with `code`, `code_verifier`, `client_id`, `client_secret`, `redirect_uri`, `grant_type=authorization_code` → `id_token` + `access_token`.
7. `GoogleAuthProvider.credential(idToken, accessToken)` → `FirebaseAuth.signInWithCredential` → `getIdToken()` → existing `appExchange`. **Identical from here on.**

No custom URL-scheme registration is required (loopback uses `http://127.0.0.1`); the `salah-bahazad://` deep-link scheme is a separate concern owned by A4.

## Architecture — one new seam, isolated and testable

`IdentityProvider` (the existing boundary) is unchanged. Introduce a `GoogleCredentialSource` so the platform difference is the only thing that varies, and the Firebase half is shared:

```
IdentityProvider                         (interface — unchanged)
└─ FirebaseIdentityProvider              signInWithGoogle = source.getCredential() → signInWithCredential → getIdToken
   └─ GoogleCredentialSource             NEW seam: "produce a Google AuthCredential"
      ├─ PluginGoogleCredentialSource       wraps google_sign_in   (android/ios/macos)
      └─ DesktopGoogleCredentialSource      loopback OAuth → credential   (windows)
         └─ DesktopOAuthClient            NEW seam: launch browser + loopback server + token POST (fakeable)
```

- The loopback mechanics live behind `DesktopOAuthClient` so `flutter test` injects a fake — **no browser, no sockets, no Firebase** in tests (`NFR-APP-REL-003`).
- PKCE generation + auth-URL building are **pure functions** → unit-tested directly.
- Provider-generic by design: adding **Apple** / **Microsoft** later is a second `…CredentialSource` over the same `DesktopOAuthClient` + the matching `OAuthProvider('apple.com'|'microsoft.com')` — no rework. *(Google Play is an Android distribution channel, not a sign-in provider; "Sign in with Google" already covers Android.)*

## Steps

### G1 — Config (`core/net/app_config.dart`)
- Add desktop Google OAuth fields sourced from `--dart-define` (mirroring `API_BASE_URL`): `googleDesktopClientId`, `googleDesktopClientSecret`, optional `googleScopes` (default `openid email profile`). For "Desktop app" OAuth clients the secret is **not** a true secret (Google's installed-app model embeds it in the client) — still injected via config, never hardcoded.
- `bool get hasDesktopGoogleOAuth => googleDesktopClientId.isNotEmpty;`

### G2 — `GoogleCredentialSource` seam (`features/auth/google/google_credential_source.dart`)
- `abstract class GoogleCredentialSource { Future<AuthCredential> getCredential(); }`
- `PluginGoogleCredentialSource` — lift the existing `google_sign_in` body out of `FirebaseIdentityProvider.signInWithGoogle()` verbatim (initialize → authenticate → `GoogleAuthProvider.credential`). Behavior on mobile/macOS is byte-for-byte unchanged.

### G3 — Desktop OAuth client (`features/auth/google/desktop_oauth_client.dart`)
- Pure helpers: `Pkce.generate()` (verifier/challenge), `buildAuthUrl(...)`, `parseRedirect(uri, expectedState)`.
- `DesktopOAuthClient` interface: `Future<OAuthTokens> authorize({authority, tokenEndpoint, clientId, clientSecret, scopes})` returning `{idToken, accessToken}`.
- `SystemBrowserOAuthClient` impl: bind `HttpServer` on `127.0.0.1:0`; `launchUrl(externalApplication)`; await the single redirect (with a timeout + user-cancel path); validate `state`; close server; POST token exchange via a **bare** Dio/`HttpClient` (TLS on); map failures to `IdentityException('google_*', …)`.

### G4 — `DesktopGoogleCredentialSource` (`features/auth/google/desktop_google_credential_source.dart`)
- Calls `DesktopOAuthClient.authorize(...)` with config from G1 → `GoogleAuthProvider.credential(idToken, accessToken)`.
- Missing config → `IdentityException('google_unconfigured', 'Google sign-in isn't set up for desktop yet.')`.

### G5 — Refactor `FirebaseIdentityProvider` (`features/auth/identity_provider.dart`)
- Inject a `GoogleCredentialSource`. `signInWithGoogle()` = `final cred = await _googleSource.getCredential(); final userCred = await _auth.signInWithCredential(cred); return _idTokenOf(userCred);` — **one** Firebase path for all platforms.
- `googleSupported` = `true` on mobile/macOS, and on Windows when `config.hasDesktopGoogleOAuth`.
- `signOut()` keeps the provider-aware Google sign-out for the plugin platforms; desktop has no plugin session to clear.

### G6 — Platform + DI (`core/platform/app_platform.dart`, `app/providers.dart`)
- `AppPlatform.googleSignInSupported` no longer hard-excludes Windows (delegated to the identity provider, which also knows config). Keep an `isWindows` helper for picking the source.
- `identityProvider` wires `PluginGoogleCredentialSource` on mobile/macOS, `DesktopGoogleCredentialSource` on Windows.

### G7 — Sign-in screen (`features/signin/sign_in_page.dart`)
- Source `googleSupported` from `ref.read(identityProvider).googleSupported` (it now encodes platform **and** config) instead of `appPlatformProvider`. **`sign_in_view.dart` is unchanged** — the button + "or" divider already render whenever `googleSupported` is true; on Windows they now appear.

### G8 — Tests (`flutter test`)
- **Pure unit:** PKCE (`challenge == base64url(sha256(verifier))`), `buildAuthUrl` (all params + encoding), `parseRedirect` (valid, `state` mismatch → throw, `error=access_denied` → cancel).
- **Source unit:** `DesktopGoogleCredentialSource` over a **fake** `DesktopOAuthClient` → returns a Google credential; unconfigured → `google_unconfigured`; cancel/timeout → mapped `IdentityException`. No real browser/socket/Firebase.
- **Golden:** the **Windows** Sign-in (1280 width) now renders the Google button + divider (regenerate `--update-goldens`, eyeball the diff). Mobile/macOS goldens unchanged.

### G9 — Verification spike (do FIRST; decides primary vs fallback) — see Risk
- On a real Windows build, confirm `FirebaseAuth.signInWithCredential(GoogleAuthProvider.credential(idToken:…))` returns a usable Firebase ID token (feed a hand-obtained Google `id_token`). ~30 min.
- **If the plugin path works** (expected): ship G1–G8 as written.
- **If it doesn't:** swap only G4's tail to mint the Firebase ID token via Firebase REST `accounts:signInWithIdp` (`postBody=id_token=<google>&providerId=google.com`, `returnSecureToken=true`) using the bare Dio. `app-exchange` and the rest of the plan are unaffected.

## §G-Setup — one-time, outside the repo
- Firebase Console → the project's Google Cloud → **APIs & Services → Credentials → Create OAuth client ID → Desktop app**. Note the `client_id` + `client_secret`.
- Ensure the **Google** provider is enabled in Firebase Auth → Sign-in method (already on for the portal).
- Provide both to debug/release runs via `--dart-define=GOOGLE_DESKTOP_CLIENT_ID=…` / `--dart-define=GOOGLE_DESKTOP_CLIENT_SECRET=…` (and document in `app/CLAUDE.md` Networking).

## Risk & mitigation
- **`signInWithCredential` on Windows (firebase_auth 6.5.4).** Primary path; email/password already works on this app's Windows build and credential sign-in is the documented FlutterFire desktop path. De-risked by the G9 spike; clean REST `signInWithIdp` fallback keeps the contract intact either way.
- **Loopback UX.** Timeout + an explicit "Cancelled / didn't finish in the browser" state (reuses the existing `AuthError` rendering); the served redirect page tells the user to return to the app.
- **Security.** PKCE + `state`; TLS never disabled on the token POST; tokens/`code`/PII never logged (`NFR-APP-SEC-003`); nothing persisted outside the existing keystore session.

## Exit criteria
- On **Windows**, "Continue with Google" is visible and completes: browser → consent → `app-exchange` → keystore session → Idle, **device-agnostic** (no device prompt).
- Mobile/macOS Google sign-in **unchanged** (still `google_sign_in`); email/password unchanged on all platforms.
- `flutter analyze` clean; unit + golden green; `flutter build windows --debug` succeeds.
- No backend/contract change; tokens only in the keystore; nothing sensitive logged.

## Out of scope (defer)
- **Apple** and **Microsoft** providers — the `…CredentialSource` + `OAuthProvider(...)` seam is ready; add when needed (Apple also needs its Services ID/key + the `sign_in_with_apple` flow on mobile).
- Linux desktop. · Any change to the device-agnostic exchange (the app still never calls the portal `/exchange`).

---
## Kickoff prompt (paste into a fresh Claude session at the repo root)
```
You are adding Google (Firebase) sign-in on the WINDOWS desktop build of the Salah Bahzad native app. Edit app/** ONLY. No backend/contract change.

Read first, in order:
1. docs/IMPLEMENTATION-PLAN-native-app-google-windows.md (this plan)
2. app/CLAUDE.md (conventions: Riverpod Notifier, view↔page split, tokens-only, TLS never off, tests never touch Firebase)
3. app/lib/features/auth/identity_provider.dart + app/lib/features/signin/* + app/lib/app/providers.dart (current auth seam)
4. docs/contracts/native-app-playback.md §A (app-exchange takes a Firebase ID token — unchanged)

Do G9 (the Windows signInWithCredential spike) FIRST and report which path (plugin vs REST signInWithIdp fallback) you're taking. Then implement G1–G8: AppConfig desktop-OAuth fields (--dart-define); GoogleCredentialSource seam (Plugin = lift existing google_sign_in body; Desktop = system-browser PKCE loopback via url_launcher + dart:io HttpServer + token exchange behind a fakeable DesktopOAuthClient); FirebaseIdentityProvider refactored to one signInWithCredential path; googleSupported = mobile/macOS || (windows && config present); sign_in_page sources googleSupported from the identity provider (sign_in_view UNCHANGED).

Green gate: flutter analyze clean + flutter test (pure PKCE/url/redirect unit + fake-driven DesktopGoogleCredentialSource unit + Windows Sign-in golden now WITH the Google button) + flutter build windows --debug. Report all.
```
