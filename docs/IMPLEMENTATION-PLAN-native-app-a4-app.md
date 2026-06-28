# Native App · A4 — APP stream (update-required state + startup version check + signing/packaging/CI)

> Status: **Planned — not yet built** · Created 2026-06-28 · The **app half** of phase **A4** in `docs/IMPLEMENTATION-PLAN-native-app.md` (§A4). This stream delivers four things in parallel: (1) the 8th §H error state (`update-required`, `426 outdated_app`), (2) a startup version check against `GET /api/app/version-status`, (3) signing + packaging for all four platforms, and (4) a CI pipeline that builds (and tests) every target repeatably. Run in its own session after A3 is confirmed green.
>
> Satisfies: `FR-APP-UPD-001`, `NFR-APP-DIST-001..005`, `NFR-APP-UPD-001/002`, `NFR-APP-MAINT-001/002/004`, `NFR-APP-PRIV-001`, `NFR-APP-SEC-002/006`. No contract changes — the `426 outdated_app` row is already in `docs/contracts/native-app-playback.md §H` and `§F`.
>
> **File ownership: `app/**` only.** Gate: `flutter analyze` clean + `flutter test` green + signed artifacts per platform.

---

## Design reference

The `update-required` screen follows the **`FAILURE / RETRY`** banner in the prototype (`.claude/Salah Bahzad App/Secure Video App (standalone).html`): failed mascot, title, message, and a **single primary action** — "Update the app" → opens the `storeUrl` from the `426` response body in the system browser (`url_launcher`). No secondary action (the student cannot dismiss this block and play anyway). This is the 8th §H state that was explicitly deferred from A3.

The startup version check produces either a soft nudge overlay on the Idle screen (`update_available`) or a hard block before the Idle screen (`update_required`). The hard block is the same `ErrorStateView` as the player failure path; the soft nudge is a dismissible banner on `IdleView`.

---

## 1. Frozen contract (this stream)

- **Consumes `docs/contracts/native-app-playback.md §F`** verbatim — both the `version-status` response shape and the `426 outdated_app` + `detail: storeUrl` from `redeem`.
- **Consumes `docs/contracts/native-app-playback.md §H`** — `update-required` row: title `"Update required"`, primary action `Update the app`.
- **`X-App-Version` + `X-Platform` headers** on the `redeem` call are **already sent** by `ApiClient` (per `app/CLAUDE.md` Networking section and `core/net/api_client.dart`). Verify this is in place; if not, add it in this stream.
- **No other contract changes.** All DTO shapes are frozen.

---

## 2. Pre-flight — confirm what already EXISTS (do not rebuild)

Read `app/CLAUDE.md` + master plan §A4 + the A3 app plan. Then confirm in code:
- **`PlayerErrorKind` enum** (`features/player/player_state.dart:16`) — `unauthorized`, `forbidden`, `maxviews`, `expired`, `notfound`, `offline`, `server`, `generic`. The comment at `:55` says *"`426 outdated_app` row is A4 and not handled here."* — that is the exact gap to fill.
- **`PlayerError.fromApi`** (`player_state.dart:55-163`) — handles `401`, `403`, `404`, `410`, network errors, `5xx`. Does **not** handle `426`. A4 adds it.
- **`ErrorStateView`** (`features/errors/error_state_view.dart`) — reused for the `update-required` overlay; confirm it accepts a `storeUrl` action or can be wired through the router action pattern already in place.
- **`ApiClient`** (`core/net/api_client.dart`) — confirm `X-App-Version: <APP_VERSION>` + `X-Platform: <platform>` are sent as default headers on every call. If the header is only on authenticated calls, also add to the `version-status` call (which is anonymous but benefits from having it).
- **`AppConfig`** (`core/net/app_config.dart`) — confirm `APP_VERSION` + `PORTAL_URL` are already `--dart-define` params. Add `APP_VERSION` version string (e.g. `"1.0.0"`) to `AppConfig` if not already present.
- **`app_links`** deep-link service — already wired from A0.
- **No existing `version-status` client** — the whole §3 below is new.

---

## 3. Update-required state (the 8th §H state)

### 3.1 `PlayerErrorKind` — add `updateRequired`

In `features/player/player_state.dart`, extend the enum:
```dart
enum PlayerErrorKind {
  unauthorized, forbidden, maxviews, expired, notfound, offline, server,
  updateRequired,  // 426 outdated_app — A4; requires the student to update before playback
  generic,
}
```

### 3.2 `PlayerError.fromApi` — handle `426`

In the `fromApi` factory, before the fallthrough `server` case, add:
```dart
// 426 Upgrade Required — "Update required" (contract §H / §F.2; storeUrl in ProblemDetails.detail)
if (e.statusCode == 426) {
  final storeUrl = e.detail;   // detail carries the store URL from the backend
  return PlayerError(
    kind: PlayerErrorKind.updateRequired,
    title: 'Update required',
    message: 'A newer version of the app is needed to play this lesson. Update and try again.',
    storeUrl: storeUrl,
  );
}
```

Update `PlayerError` to carry an optional `storeUrl` field (used only for `updateRequired`):
```dart
final class PlayerError {
  const PlayerError({
    required this.kind,
    required this.title,
    required this.message,
    this.storeUrl,
  });

  final PlayerErrorKind kind;
  final String title;
  final String message;
  final String? storeUrl;   // NEW — non-null only for updateRequired
  // … rest unchanged
}
```

### 3.3 `ErrorStateView` — update-required rendering

In `features/errors/error_state_view.dart`, the `update-required` state renders:
- Title: `"Update required"` (verbatim §H)
- Message: copy from `PlayerError.message`
- Primary button: `"Update the app"` — launches `storeUrl` via `url_launcher` (`launchUrl(Uri.parse(storeUrl!), mode: LaunchMode.externalApplication)`). If `storeUrl` is null or empty, show `"Visit the store"` and open a generic fallback (the app's support page or the store search for "Salah Bahzad").
- **No secondary action** — this is a hard block.
- Mascot: `SbAssets.mascotFailed` (same as other failure states).
- Reassurance footer: `"Nothing has been lost — press Update to continue."` (replaces the generic `"Your place is saved…"` text since there's no player context here).

The action routing in `player_page.dart` adds a `PlayerErrorKind.updateRequired` branch:
```dart
case PlayerErrorKind.updateRequired:
  final url = err.storeUrl;
  if (url != null && url.isNotEmpty) {
    launchUrl(Uri.parse(url), mode: LaunchMode.externalApplication);
  }
```

---

## 4. Startup version check

### 4.1 New DTO — `data/dtos/app_version_status.dart`

Field-for-field from contract §F.1:
```dart
final class AppVersionStatusDto {
  const AppVersionStatusDto({
    required this.status,
    required this.minVersion,
    required this.latestVersion,
    required this.storeUrl,
  });

  final String status;         // "ok" | "update_available" | "update_required"
  final String minVersion;
  final String latestVersion;
  final String storeUrl;

  factory AppVersionStatusDto.fromJson(Map<String, dynamic> json) => AppVersionStatusDto(
    status: json['status'] as String,
    minVersion: json['minVersion'] as String,
    latestVersion: json['latestVersion'] as String,
    storeUrl: json['storeUrl'] as String? ?? '',
  );
}
```

### 4.2 New repository — `data/version_repository.dart`

```dart
abstract interface class VersionRepository {
  Future<AppVersionStatusDto> checkStatus();
}

final class RemoteVersionRepository implements VersionRepository {
  const RemoteVersionRepository(this._client, this._config);
  final ApiClient _client;
  final AppConfig _config;

  @override
  Future<AppVersionStatusDto> checkStatus() async {
    final resp = await _client.dio.get<Map<String, dynamic>>(
      '/api/app/version-status',
      queryParameters: {
        'platform': _config.platform,    // AppPlatform canonical name (android|ios|windows|macos)
        'version': _config.appVersion,   // from --dart-define APP_VERSION
      },
    );
    return AppVersionStatusDto.fromJson(resp.data!);
  }
}
```

> **No auth header on `version-status`** — it is `AllowAnonymous` on the backend. If `ApiClient` injects `Authorization` on all calls, the anonymous call still works (the backend ignores the header). No special case needed.

### 4.3 New provider + notifier — `features/splash/version_check_notifier.dart`

The Riverpod `AsyncNotifier` fetches the version status on `build` and exposes:
- `loading` → splash shows the existing progress checklist (already animated).
- `data(AppVersionStatusDto)` → route based on `status`.
- `error` → treat as `ok` (a network failure at launch should not block playback; the `redeem` step will catch a genuinely stale build).

```dart
@riverpod
class VersionCheckNotifier extends AsyncNotifier<AppVersionStatusDto> {
  @override
  Future<AppVersionStatusDto> build() =>
      ref.read(versionRepositoryProvider).checkStatus();
}
```

Providers:
```dart
final versionRepositoryProvider = Provider<VersionRepository>((ref) =>
  RemoteVersionRepository(ref.read(apiClientProvider), ref.read(appConfigProvider)));
```

### 4.4 Routing — integrate into `SplashPage`

In `features/splash/splash_page.dart` (or wherever the splash state machine runs), after the existing "Verifying handoff code" / "Opening secure session" steps, add a version check step:

1. Call `VersionCheckNotifier.build()`.
2. `status == "update_required"` → navigate to a **blocking update screen** (a new `/update-required` route, rendering `ErrorStateView` with `PlayerErrorKind.updateRequired` and the `storeUrl`).
3. `status == "update_available"` → proceed normally (store the `storeUrl` for the soft nudge on Idle).
4. `status == "ok"` or network error → proceed normally.
5. The blocking update screen has **no back navigation** — it is the terminal state until the student updates.

### 4.5 Soft nudge on Idle — `update_available`

In `features/idle/idle_view.dart`, accept an optional `updateAvailable` flag + `storeUrl`. When `updateAvailable` is `true`, show a **dismissible amber banner** at the top:

```
┌─────────────────────────────────────────────┐
│ 🔔  A new version is available.  [Update]  ✕ │
└─────────────────────────────────────────────┘
```

- Banner uses `SbColors.amber` / `SbColors.paper` — consistent with the prototype's amber warnings.
- `[Update]` → `launchUrl(storeUrl)`. `✕` → dismisses the banner for this session (not persisted).
- Banner is hidden when `storeUrl` is empty.

---

## 5. Signing & packaging

> The signing credentials (keystores, certificates, provisioning profiles) are stored in **secrets, not in the repo**. Each platform section below identifies what secret material is needed and how it is injected via CI environment variables. Local signing is manual (developer's own credentials); CI uses the secrets vault.

### 5.1 Android — signed AAB (Google Play)

**What is needed:**
- An upload keystore (`.jks`) registered with Google Play — generated once, stored in CI secrets, **never committed**.
- `key.properties` file at `android/key.properties` — injected by CI, **gitignored**:
  ```
  storeFile=upload-keystore.jks
  storePassword=<from secret>
  keyAlias=upload
  keyPassword=<from secret>
  ```
- `android/app/build.gradle` — already has `signingConfigs.release` block reading `key.properties` if the file exists; if not, confirm and add it following the standard Flutter pattern.

**What to fix (`NFR-APP-DIST-004` / `NFR-APP-DIST-005`):**
- **Missing assets** — `android/app/src/main/res/` mipmap icons must exist. Generate all densities from the `assets/brand/logo-small` source using `flutter_launcher_icons` or by hand. Add `flutter_launcher_icons` to `dev_dependencies` and a `flutter_icons:` block in `pubspec.yaml` (android + ios + macos + windows all at once).
- **Hardcoded paths** — audit `android/` Gradle files for any absolute dev-machine paths; if found, replace with relative paths or `$ANDROID_HOME`.

**Build command (release):**
```bash
flutter build appbundle \
  --release \
  --dart-define=API_BASE_URL=https://api.salahbahzad.com \
  --dart-define=APP_VERSION=1.0.0 \
  --dart-define=PORTAL_URL=https://student.salahbahzad.com \
  --dart-define=SENTRY_DSN=$SENTRY_DSN \
  --dart-define=SENTRY_ENV=production
```
Output: `build/app/outputs/bundle/release/app-release.aab`.

### 5.2 iOS — App Store

**What is needed (Apple Developer Program ~$99/yr):**
- Distribution certificate (`.p12`) + provisioning profile (`.mobileprovision`) — stored in CI secrets.
- `ios/Runner/Info.plist` — `CFBundleURLSchemes` entry for `salah-bahazad` already present from A0. Confirm `CFBundleDisplayName`, `CFBundleIdentifier` (e.g. `com.salahbahzad.secureplayer`) are correct.
- `ios/Runner/GoogleService-Info.plist` — Firebase config; injected by CI from a secret (already gitignored from A0).
- Privacy manifest (`PrivacyInfo.xcprivacy`) — App Store requires this for v17+ SDKs; declare only what's actually used (network, local storage for the keystore). See §6.

**Build command (archive):**
```bash
flutter build ipa \
  --release \
  --export-options-plist=ios/ExportOptions.plist \
  --dart-define=API_BASE_URL=https://api.salahbahzad.com \
  --dart-define=APP_VERSION=1.0.0 \
  --dart-define=PORTAL_URL=https://student.salahbahzad.com \
  --dart-define=SENTRY_DSN=$SENTRY_DSN \
  --dart-define=SENTRY_ENV=production
```
`ios/ExportOptions.plist` specifies `method = app-store`, `signingStyle = manual`, provisioningProfiles map. **This file is committed** (no secrets in it; secrets come from the keychain on the CI Mac runner).

### 5.3 macOS — Developer-ID signed + notarized

**What is needed:**
- Developer-ID Application certificate in the CI Mac keychain.
- `macos/Runner/Release.entitlements` — confirm `com.apple.security.network.client = true` (for HTTPS calls) is present; add if missing.
- Notarization credentials: Apple ID + app-specific password, or a Notarytool API key (preferred) — CI secrets.

**Build + notarize script:**
```bash
# 1. Build
flutter build macos --release \
  --dart-define=API_BASE_URL=https://api.salahbahzad.com \
  --dart-define=APP_VERSION=1.0.0 \
  --dart-define=PORTAL_URL=https://student.salahbahzad.com \
  --dart-define=SENTRY_DSN=$SENTRY_DSN \
  --dart-define=SENTRY_ENV=production

# 2. Sign (already happens during `flutter build macos --release` with the cert in the keychain)
# 3. Zip and notarize
ditto -c -k --keepParent \
  "build/macos/Build/Products/Release/Secure Player.app" \
  secure_player_macos.zip
xcrun notarytool submit secure_player_macos.zip \
  --key "$NOTARY_API_KEY_PATH" \
  --key-id "$NOTARY_KEY_ID" \
  --issuer "$NOTARY_ISSUER_ID" \
  --wait
xcrun stapler staple "build/macos/Build/Products/Release/Secure Player.app"
# 4. Package as .dmg (optional for v1; can ship the .app in a .zip first)
```

**App name:** Confirm `PRODUCT_BUNDLE_IDENTIFIER` in `macos/Runner.xcodeproj/project.pbxproj` and the CFBundleName in `Info.plist` match the chosen store name. `NSWindow.sharingType = .none` is already set by the A2 `secure_surface` macOS shim — no new entitlements needed for that.

### 5.4 Windows — Authenticode installer (fix hardcoded paths)

**`NFR-APP-DIST-004` fix — remove hardcoded absolute paths:**
- Read the existing `windows/` CMakeLists and MSIX/installer scripts. If any absolute path to a developer's machine appears, replace with `${CMAKE_CURRENT_SOURCE_DIR}` or environment variables.
- The `ATL` discovery block in `windows/CMakeLists.txt` already uses glob-to-find-VS pattern (from A0) — confirm it works without a hardcoded VS path.

**What is needed:**
- Authenticode certificate (EV preferred for SmartScreen; OV acceptable) — `.pfx` from a CA, stored in CI secrets.
- Windows installer: use `flutter build windows --release` output + a simple [Inno Setup](https://jrsoftware.org/isinfo.php) script to produce a signed installer `.exe`. MSIX is an alternative (recommended for Microsoft Store distribution; use `msix` Flutter package).
- Deep-link scheme registration: `salah-bahazad://` must be registered in the registry at install time. Inno Setup script adds the reg keys; MSIX includes a `<uap:Protocol>` entry in the package manifest.

**Key installer requirements:**
- No hardcoded `C:\Users\<dev>` paths anywhere in the installer script.
- Install to `%LOCALAPPDATA%\SalahBahzad\SecurePlayer` (per-user install, no UAC elevation needed).
- Register `salah-bahazad://` URI scheme pointing at the installed `.exe` with `%1` argument.
- `flutter_secure_storage` writes to Windows Credential Manager — confirm no extra setup needed in the installer (it doesn't).

**Sign the installer:**
```powershell
signtool sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 /f "$env:SIGNING_CERT_PATH" /p "$env:SIGNING_CERT_PASSWORD" installer\SalahBahzadSecurePlayerSetup.exe
```

**Build command:**
```powershell
flutter build windows --release `
  --dart-define=API_BASE_URL=https://api.salahbahzad.com `
  --dart-define=APP_VERSION=1.0.0 `
  --dart-define=PORTAL_URL=https://student.salahbahzad.com `
  --dart-define=SENTRY_DSN=$env:SENTRY_DSN `
  --dart-define=SENTRY_ENV=production
```
Output directory: `build\windows\x64\runner\Release\`. Package this directory into the installer.

---

## 6. Privacy disclosures (`NFR-APP-PRIV-001`)

**What the app actually collects / uses:**
| Data | Reason | Stored where |
|---|---|---|
| Email address | Firebase authentication | Firebase (not the app's server) |
| Student name + serial | Visible watermark only | Memory while the player is open |
| Playback events (video ID, timestamp) | Audit + view cap | Backend, not the device |
| Crash context (failure type, video ID, app version) | Sentry — no PII | Sentry (no name, no email, no token) |
| Session token | Persistent sign-in | OS keystore |
| Network connectivity status | Offline detection | Memory only |

**App Store (iOS) — required:**
- `NSPrivacyAccessedAPITypes` in `PrivacyInfo.xcprivacy` if any required reason APIs are used (e.g. `UserDefaults` via `flutter_secure_storage`). Confirm with the Flutter plugin manifest.
- App Store Connect privacy questionnaire: data categories used, linked to user identity (authentication data), collected for app functionality only.

**Google Play (Android) — required:**
- Data safety form in Play Console: personal info (email, name) → collected, linked to identity, required for core functionality. No sale. Crash data (anonymous) → collected, not linked to user.

**No in-app privacy policy screen is required for v1** (the app is a closed B2B product for enrolled students; the privacy policy lives on the web portal). Add the portal's privacy policy URL to `AppConfig` for linking from the Idle screen if needed.

---

## 7. CI pipeline — GitHub Actions

Four workflows — one job matrix per platform, triggered on `push` to `main` and on `pull_request`.

**`app/.github/workflows/ci.yml`** (or at the repo root `.github/workflows/app-ci.yml`):

```yaml
name: App CI

on:
  push:
    branches: [main]
    paths: ['app/**']
  pull_request:
    paths: ['app/**']

jobs:
  # ── Tests (runs on every PR, fast) ──────────────────────────────────────
  test:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: app
    steps:
      - uses: actions/checkout@v4
      - uses: subosito/flutter-action@v2
        with:
          channel: stable
      - run: flutter pub get
      - run: flutter analyze
      - run: flutter test

  # ── Android build (signed AAB on main) ──────────────────────────────────
  build-android:
    runs-on: ubuntu-latest
    needs: test
    if: github.ref == 'refs/heads/main'
    defaults:
      run:
        working-directory: app
    steps:
      - uses: actions/checkout@v4
      - uses: subosito/flutter-action@v2
        with:
          channel: stable
      - name: Write key.properties
        env:
          KEYSTORE_B64: ${{ secrets.ANDROID_KEYSTORE_B64 }}
          STORE_PASSWORD: ${{ secrets.ANDROID_STORE_PASSWORD }}
          KEY_PASSWORD: ${{ secrets.ANDROID_KEY_PASSWORD }}
        run: |
          echo "$KEYSTORE_B64" | base64 -d > android/upload-keystore.jks
          cat > android/key.properties <<EOF
          storeFile=upload-keystore.jks
          storePassword=$STORE_PASSWORD
          keyAlias=upload
          keyPassword=$KEY_PASSWORD
          EOF
      - run: flutter pub get
      - run: flutter build appbundle --release
          --dart-define=API_BASE_URL=${{ vars.API_BASE_URL }}
          --dart-define=APP_VERSION=${{ vars.APP_VERSION }}
          --dart-define=PORTAL_URL=${{ vars.PORTAL_URL }}
          --dart-define=SENTRY_DSN=${{ secrets.SENTRY_DSN }}
          --dart-define=SENTRY_ENV=production
      - uses: actions/upload-artifact@v4
        with:
          name: android-aab
          path: app/build/app/outputs/bundle/release/app-release.aab

  # ── macOS build (signed + notarized on main) ─────────────────────────────
  build-macos:
    runs-on: macos-latest
    needs: test
    if: github.ref == 'refs/heads/main'
    defaults:
      run:
        working-directory: app
    steps:
      - uses: actions/checkout@v4
      - uses: subosito/flutter-action@v2
        with:
          channel: stable
      - name: Import Developer-ID certificate
        env:
          CERT_P12_B64: ${{ secrets.MACOS_CERT_P12_B64 }}
          CERT_PASSWORD: ${{ secrets.MACOS_CERT_PASSWORD }}
        run: |
          echo "$CERT_P12_B64" | base64 -d > /tmp/cert.p12
          security create-keychain -p "" build.keychain
          security import /tmp/cert.p12 -k build.keychain -P "$CERT_PASSWORD" -T /usr/bin/codesign
          security set-key-partition-list -S apple-tool:,apple: -s -k "" build.keychain
          security list-keychains -s build.keychain
          security default-keychain -s build.keychain
          security unlock-keychain -p "" build.keychain
      - run: flutter pub get
      - run: flutter build macos --release
          --dart-define=API_BASE_URL=${{ vars.API_BASE_URL }}
          --dart-define=APP_VERSION=${{ vars.APP_VERSION }}
          --dart-define=PORTAL_URL=${{ vars.PORTAL_URL }}
          --dart-define=SENTRY_DSN=${{ secrets.SENTRY_DSN }}
          --dart-define=SENTRY_ENV=production
      - name: Notarize
        env:
          NOTARY_API_KEY_B64: ${{ secrets.NOTARY_API_KEY_B64 }}
          NOTARY_KEY_ID: ${{ secrets.NOTARY_KEY_ID }}
          NOTARY_ISSUER_ID: ${{ secrets.NOTARY_ISSUER_ID }}
        run: |
          echo "$NOTARY_API_KEY_B64" | base64 -d > /tmp/notary.p8
          ditto -c -k --keepParent \
            "build/macos/Build/Products/Release/Secure Player.app" \
            /tmp/secure_player_macos.zip
          xcrun notarytool submit /tmp/secure_player_macos.zip \
            --key /tmp/notary.p8 \
            --key-id "$NOTARY_KEY_ID" \
            --issuer "$NOTARY_ISSUER_ID" \
            --wait
          xcrun stapler staple "build/macos/Build/Products/Release/Secure Player.app"
      - uses: actions/upload-artifact@v4
        with:
          name: macos-app
          path: app/build/macos/Build/Products/Release/

  # ── iOS build (IPA on main; requires macOS runner) ───────────────────────
  build-ios:
    runs-on: macos-latest
    needs: test
    if: github.ref == 'refs/heads/main'
    defaults:
      run:
        working-directory: app
    steps:
      - uses: actions/checkout@v4
      - uses: subosito/flutter-action@v2
        with:
          channel: stable
      - name: Write GoogleService-Info.plist
        env:
          GSIP_B64: ${{ secrets.IOS_GOOGLE_SERVICE_INFO_B64 }}
        run: echo "$GSIP_B64" | base64 -d > ios/Runner/GoogleService-Info.plist
      - name: Import iOS distribution cert + profile
        # ... (standard iOS signing steps — import p12 + mobileprovision)
        env:
          DIST_CERT_B64: ${{ secrets.IOS_DIST_CERT_B64 }}
          DIST_CERT_PASSWORD: ${{ secrets.IOS_DIST_CERT_PASSWORD }}
          PROV_PROFILE_B64: ${{ secrets.IOS_PROV_PROFILE_B64 }}
        run: |
          echo "$DIST_CERT_B64" | base64 -d > /tmp/ios_dist.p12
          echo "$PROV_PROFILE_B64" | base64 -d > /tmp/profile.mobileprovision
          security create-keychain -p "" build.keychain
          security import /tmp/ios_dist.p12 -k build.keychain -P "$DIST_CERT_PASSWORD" -T /usr/bin/codesign
          security set-key-partition-list -S apple-tool:,apple: -s -k "" build.keychain
          security list-keychains -s build.keychain
          security default-keychain -s build.keychain
          security unlock-keychain -p "" build.keychain
          mkdir -p ~/Library/MobileDevice/Provisioning\ Profiles
          cp /tmp/profile.mobileprovision ~/Library/MobileDevice/Provisioning\ Profiles/
      - run: flutter pub get
      - run: flutter build ipa --release
          --export-options-plist=ios/ExportOptions.plist
          --dart-define=API_BASE_URL=${{ vars.API_BASE_URL }}
          --dart-define=APP_VERSION=${{ vars.APP_VERSION }}
          --dart-define=PORTAL_URL=${{ vars.PORTAL_URL }}
          --dart-define=SENTRY_DSN=${{ secrets.SENTRY_DSN }}
          --dart-define=SENTRY_ENV=production
      - uses: actions/upload-artifact@v4
        with:
          name: ios-ipa
          path: app/build/ios/ipa/

  # ── Windows build + sign (on main; self-hosted or windows-latest runner) ─
  build-windows:
    runs-on: windows-latest
    needs: test
    if: github.ref == 'refs/heads/main'
    defaults:
      run:
        working-directory: app
    steps:
      - uses: actions/checkout@v4
      - uses: subosito/flutter-action@v2
        with:
          channel: stable
      - run: flutter pub get
      - run: flutter build windows --release
          --dart-define=API_BASE_URL=${{ vars.API_BASE_URL }}
          --dart-define=APP_VERSION=${{ vars.APP_VERSION }}
          --dart-define=PORTAL_URL=${{ vars.PORTAL_URL }}
          --dart-define=SENTRY_DSN=${{ secrets.SENTRY_DSN }}
          --dart-define=SENTRY_ENV=production
      - name: Sign installer
        env:
          SIGNING_CERT_B64: ${{ secrets.WINDOWS_SIGNING_CERT_B64 }}
          SIGNING_CERT_PASSWORD: ${{ secrets.WINDOWS_SIGNING_CERT_PASSWORD }}
        run: |
          # TODO: run Inno Setup / MSIX packaging then signtool
          # (Inno Setup script: app/installer/setup.iss)
          echo "Signing step — fill in when installer script is ready"
      - uses: actions/upload-artifact@v4
        with:
          name: windows-build
          path: app/build/windows/x64/runner/Release/
```

> **Secrets needed (register in GitHub repo Settings → Secrets → Actions):**
> - `ANDROID_KEYSTORE_B64`, `ANDROID_STORE_PASSWORD`, `ANDROID_KEY_PASSWORD`
> - `MACOS_CERT_P12_B64`, `MACOS_CERT_PASSWORD`, `NOTARY_API_KEY_B64`, `NOTARY_KEY_ID`, `NOTARY_ISSUER_ID`
> - `IOS_DIST_CERT_B64`, `IOS_DIST_CERT_PASSWORD`, `IOS_PROV_PROFILE_B64`, `IOS_GOOGLE_SERVICE_INFO_B64`
> - `WINDOWS_SIGNING_CERT_B64`, `WINDOWS_SIGNING_CERT_PASSWORD`
> - `SENTRY_DSN`
>
> **Variables (non-secret, in repo Settings → Variables):** `API_BASE_URL`, `APP_VERSION`, `PORTAL_URL`.

---

## 8. `flutter_launcher_icons` — app icons on all platforms (`NFR-APP-DIST-005`)

Add to `dev_dependencies`:
```yaml
flutter_launcher_icons: ^0.14.0
```

Add to `pubspec.yaml`:
```yaml
flutter_launcher_icons:
  android: true
  ios: true
  macos: true
  windows: true
  image_path: "assets/brand/logo-small.png"  # must be ≥ 1024×1024 px transparent PNG
```

Generate:
```bash
dart run flutter_launcher_icons
```

Commit the generated icon files — they are build artefacts that belong in the repo (not gitignored) so the build is reproducible without the `dart run` step.

---

## 9. Tests

The version check and the `update-required` state get unit + widget coverage:

**`test/features/player/update_required_state_test.dart`:**
- `PlayerError.fromApi` with a fake `ApiException(statusCode: 426, detail: 'https://play.google.com/…')` → `PlayerError(kind: PlayerErrorKind.updateRequired, storeUrl: 'https://…')`.
- `PlayerError.fromApi` with `statusCode: 426, detail: null` → `storeUrl` is `null`, action falls back.

**`test/features/splash/version_check_notifier_test.dart`:**
- `status == "update_required"` → router navigates to `/update-required`.
- `status == "update_available"` → routes normally + stores `storeUrl`.
- `status == "ok"` → routes normally.
- Network error → treats as `ok` (no block).

**Golden test — `test/golden/update_required_golden_test.dart`:**
- `ErrorStateView` with `kind: updateRequired` renders the "Update required" state at 360/768/1280 widths.

---

## 10. Green gate

```bash
flutter analyze          # zero issues
flutter test             # all tests pass (including new §9 tests)
flutter build windows --debug    # confirm ATL fix still holds
flutter build apk --debug        # confirm android icons present
flutter build macos              # confirm macOS signs (local cert or skip signing in dev)
```

The CI pipeline is the definitive gate for signed artifacts. Local builds can be unsigned (`--no-codesign` on iOS/macOS) for development.

---

## Deferred

- **Cert pinning** (`NFR-APP-SEC-006`, `SHOULD`) — add `dio_certificate_pinning` or embed the leaf/intermediate cert in `AppConfig`; the SHOULD gives room to defer past v1. Flag with a TODO in `ApiClient`.
- **Root/jailbreak detection** (`NFR-APP-SEC-006`, `MAY`) — `flutter_jailbreak_detection` or equivalent; explicitly deferred from v1.
- **macOS App Store distribution** (alternative to Developer-ID) — deferred; Developer-ID + notarize is sufficient for v1 direct distribution.
- **Microsoft Store (MSIX)** — deferred; Authenticode installer is v1.
- **Windows Inno Setup script** (`app/installer/setup.iss`) — the CI YAML above has a placeholder; the actual script is a Windows-only authoring task done outside this stream's scope.
- **FairPlay for iOS still-screenshot block** (`NFR-APP-CAP-005`) — remains out of v1 scope.
