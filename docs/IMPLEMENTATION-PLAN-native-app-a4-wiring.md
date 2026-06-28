# Native App · A4 — WIRING stream (forced-update live path + signed-build smoke per platform)

> Status: **Ready to run** · Created 2026-06-28 · A4 backend + app streams both complete (`flutter analyze` clean, `flutter test` 94/94, backend compiles). `AppVersions` is now in `appsettings.Development.json` (all platforms at `1.0.0` baseline). Start the Aspire AppHost and work through checks 1–9 below.

---

## Pre-flight

- A4 backend stream complete: `dotnet test -c Release` green.
- A4 app stream complete: `flutter analyze` clean + `flutter test` green.
- Aspire AppHost running: `dotnet run --project backend/src/SalahBahazad.AppHost` (F5). API at `http://localhost:5080`.
- A student account in Tenant A with a **Ready** AES-128 HLS `SessionVideo` and `access_remaining ≥ 1` (reuse A1/5C fixture).
- `flutter run -d windows` (or `flutter run -d macos`) — the A4 app code live on at least one desktop platform.

---

## Fixtures and helpers

**Mint a Student JWT (app-exchange):**
```bash
curl -s -X POST http://localhost:5080/api/auth/student/app-exchange \
  -H "Content-Type: application/json" \
  -d '{"firebaseIdToken":"<FIREBASE_ID_TOKEN>"}' | jq .
# → { "accessToken": "...", ... }
```
Store `accessToken` as `$STUDENT_JWT`.

**Mint a Play handoff:**
```bash
curl -s -X POST "http://localhost:5080/api/me/videos/<VIDEO_ID>/playback" \
  -H "Authorization: Bearer $STUDENT_JWT" | jq .
# → { "handoffCode": "<CODE>", ... }
```

**Set a version floor via config (hot-reload):**
Edit `backend/src/SalahBahazad.Api/appsettings.Development.json` — add or update:
```json
"AppVersions": {
  "Platforms": {
    "windows":  { "MinVersion": "2.0.0", "LatestVersion": "2.0.0", "StoreUrl": "https://example.com/update" },
    "android":  { "MinVersion": "2.0.0", "LatestVersion": "2.0.0", "StoreUrl": "https://play.google.com/" },
    "ios":      { "MinVersion": "2.0.0", "LatestVersion": "2.0.0", "StoreUrl": "https://apps.apple.com/" },
    "macos":    { "MinVersion": "2.0.0", "LatestVersion": "2.0.0", "StoreUrl": "https://example.com/update" }
  }
}
```
The floor `2.0.0` exceeds the app's `APP_VERSION=1.0.0` → every redeem triggers `426 outdated_app`.

To restore normal operation, reset `MinVersion` to `"1.0.0"` (or lower).

> **Hot-reload note:** `IOptionsMonitor` picks up `appsettings.Development.json` changes **without an API restart** on a live Aspire run — the file is watched. Confirm the first check after saving takes the new floor.

---

## Live checks

### Check 1 — `GET /api/app/version-status` (anonymous, no auth needed)

**1a — `ok` status (floor at `1.0.0`, app at `1.0.0`):**
```bash
curl -s "http://localhost:5080/api/app/version-status?platform=android&version=1.0.0" | jq .
```
**Expected:** `{ "status": "ok", "minVersion": "1.0.0", "latestVersion": "1.0.0", "storeUrl": "..." }`

**1b — `update_required` status (floor at `2.0.0`, app at `1.0.0`):**
Set the floor to `2.0.0` (see Fixtures above), then:
```bash
curl -s "http://localhost:5080/api/app/version-status?platform=windows&version=1.0.0" | jq .
```
**Expected:** `{ "status": "update_required", "minVersion": "2.0.0", ... }`

**1c — `update_available` status:**
Set `MinVersion: "0.9.0"`, `LatestVersion: "1.1.0"`, then:
```bash
curl -s "http://localhost:5080/api/app/version-status?platform=ios&version=1.0.0" | jq .
```
**Expected:** `{ "status": "update_available", ... }`

**1d — unknown platform → 400:**
```bash
curl -s "http://localhost:5080/api/app/version-status?platform=linux&version=1.0.0" | jq .
```
**Expected:** `{ "status": 400, "detail": "..." }` (ProblemDetails)

**1e — missing version → 400:**
```bash
curl -s "http://localhost:5080/api/app/version-status?platform=android" | jq .
```
**Expected:** `400`.

---

### Check 2 — `redeem` with stale `X-App-Version` → `426 outdated_app`

Set the floor to `2.0.0`. Mint a fresh handoff (gate call → `$HANDOFF`). Then:

```bash
curl -s -X POST http://localhost:5080/api/me/videos/playback/redeem \
  -H "Authorization: Bearer $STUDENT_JWT" \
  -H "Content-Type: application/json" \
  -H "X-App-Version: 1.0.0" \
  -H "X-Platform: windows" \
  -d '{"handoffCode":"'"$HANDOFF"'"}' | jq .
```
**Expected:** `HTTP 426 Upgrade Required` with `{ "reason": "outdated_app", "detail": "https://example.com/update" }`.

Confirm the handoff was **not consumed** (it should remain in Redis — the `426` fires before the GETDEL). Redeem again with `X-App-Version: 2.0.0` → `200 PlaybackManifestDto`.

> **How to confirm the handoff was not consumed:** mint a fresh handoff, attempt the `426`-triggering redeem, then redeem again with a compliant version — should succeed. If the second redeem fails with `410 handoff_expired`, the handoff was consumed (a bug).

---

### Check 3 — `redeem` without version headers → `200` (leniency rule)

Still with floor `2.0.0`. Mint a handoff. Redeem with **no** `X-App-Version` / `X-Platform` headers:
```bash
curl -s -X POST http://localhost:5080/api/me/videos/playback/redeem \
  -H "Authorization: Bearer $STUDENT_JWT" \
  -H "Content-Type: application/json" \
  -d '{"handoffCode":"'"$HANDOFF"'"}' | jq .
```
**Expected:** `200 PlaybackManifestDto`. The portal uses this path (never sends version headers) — confirming it is not accidentally blocked.

---

### Check 4 — gate (`StartVideoPlayback`) unaffected by version floor

Still with floor `2.0.0`. Call the gate with old `X-App-Version`:
```bash
curl -s -X POST "http://localhost:5080/api/me/videos/<VIDEO_ID>/playback" \
  -H "Authorization: Bearer $STUDENT_JWT" \
  -H "X-App-Version: 1.0.0" \
  -H "X-Platform: windows" | jq .
```
**Expected:** `200 PlaybackHandoffDto`. The gate is unaffected — only `redeem` enforces the version.

---

### Check 5 — app renders `update-required` on the device

With the floor still at `2.0.0`:

1. Launch the app on the running platform (`flutter run -d windows`).
2. The splash page calls `GET /api/app/version-status` → `status: update_required`.
3. **Expected:** the blocking update screen renders — `ErrorStateView` with title `"Update required"`, the "Update the app" button visible.
4. Tap "Update the app" → the `storeUrl` opens in the system browser (or a warning if `storeUrl` is empty). ✅
5. There is **no back navigation** — the student cannot dismiss this block. ✅

Reset the floor to `1.0.0`:

6. Cold-launch the app again → reaches the Idle screen normally. ✅

---

### Check 6 — soft nudge on Idle (`update_available`)

Set `MinVersion: "0.9.0"`, `LatestVersion: "1.5.0"`, `StoreUrl: "https://example.com/update"`. Cold-launch:

**Expected:** the Idle screen shows the amber nudge banner at the top: `"A new version is available"` + `[Update]` + `✕`. Tapping `✕` dismisses it for this session. Tapping `[Update]` opens the store URL. ✅

---

### Check 7 — player `update-required` overlay (via `redeem` `426`)

With floor at `2.0.0` and the app running at `1.0.0`:

1. Navigate to a Play deep-link while signed in. The splash resolves the session and attempts to play.
2. The app sends `X-App-Version: 1.0.0` on `redeem` → `426 outdated_app`.
3. **Expected:** the player shows the `update-required` overlay (title `"Update required"`, "Update the app" button). ✅

> This check exercises the `426` path that lands during the `redeem` step (rather than during the startup version check). It proves both paths lead to the same error state.

---

### Check 8 — signed artifact smoke per platform

Run locally (or verify from CI artifacts on `main`). Each build must complete without errors:

**Android (APK/AAB — unsigned local debug build is sufficient for smoke):**
```bash
cd app
flutter build apk --debug \
  --dart-define=API_BASE_URL=http://localhost:5080 \
  --dart-define=APP_VERSION=1.0.0 \
  --dart-define=PORTAL_URL=http://localhost:4200
# → build/app/outputs/flutter-apk/app-debug.apk
```
Sideload on an Android device or emulator. App launches, signs in, reaches Idle. ✅

**macOS (local unsigned — CI signs):**
```bash
flutter build macos \
  --dart-define=API_BASE_URL=http://localhost:5080 \
  --dart-define=APP_VERSION=1.0.0 \
  --dart-define=PORTAL_URL=http://localhost:4200
# Opens from build/macos/Build/Products/Release/Secure Player.app
open "build/macos/Build/Products/Release/Secure Player.app"
```
App window opens with correct chrome. Sign in. ✅

**Windows (local unsigned):**
```powershell
flutter build windows --debug `
  --dart-define=API_BASE_URL=http://localhost:5080 `
  --dart-define=APP_VERSION=1.0.0 `
  --dart-define=PORTAL_URL=http://localhost:4200
# → build\windows\x64\runner\Debug\secure_player.exe
```
Run the exe. App opens. ✅

**iOS (CI-signed; local developer-signed if a device is attached):**
If a provisioned device is available:
```bash
flutter build ipa --debug \
  --dart-define=API_BASE_URL=http://localhost:5080 \
  --dart-define=APP_VERSION=1.0.0 \
  --dart-define=PORTAL_URL=http://localhost:4200
```
Otherwise, treat the CI `build-ios` job artifact as the smoke. ✅

> **CI-signed artifact verification (for main-branch builds):** after the CI pipeline completes, download the `android-aab` artifact and verify it is signed:
> ```bash
> java -jar bundletool.jar dump manifest --bundle=app-release.aab | grep versionName
> ```
> Download the `macos-app` artifact and run `codesign -dv --verbose=4 "Secure Player.app"` to confirm Developer-ID signing and a `notarized` staple.

---

### Check 9 — hot-reload version floor (no API restart)

With the API running on the Aspire stack:
1. `MinVersion` is `1.0.0` → `version-status` returns `ok`.
2. Edit `appsettings.Development.json`: set `MinVersion` to `2.0.0`. Save.
3. Wait 2–5 s (the file watcher cycle).
4. `curl http://localhost:5080/api/app/version-status?platform=android&version=1.0.0` → **`update_required`**. No restart. ✅
5. Revert `MinVersion` to `1.0.0` → `ok` again. ✅

---

## Exit criteria

All 9 checks pass:
- `version-status` returns correct `ok` / `update_available` / `update_required` for all platform × version combinations.
- `redeem` with `X-App-Version < floor` → `426 outdated_app`; handoff survives (not consumed).
- `redeem` with no version headers → `200` (portal leniency).
- Gate unaffected by version headers.
- App renders `update-required` blocking screen + soft nudge correctly.
- `update-required` player overlay triggers on `426` from `redeem`.
- Signed / signable artifacts produced from local builds on each platform.
- Hot-reload version floor takes effect without API restart.

**A4 closes. The native app slice is complete (A0 → A4).**

---

## Known open items at A4 close

- **iOS store URL** — populate in `appsettings.Production.json` when the App Store listing is approved.
- **Windows installer script** (`app/installer/setup.iss`) — the CI YAML has a placeholder; create the Inno Setup script and test the installer registration of the `salah-bahazad://` URI scheme.
- **Cert pinning** (`NFR-APP-SEC-006`, SHOULD) — deferred; flag in `ApiClient` as a follow-up.
- **Google sign-in on Windows** — `IMPLEMENTATION-PLAN-native-app-google-windows.md` is a separate planned stream and is independent of A4.
- **A2 capture-protection manual matrix** (`NFR-APP-MAINT-003`) — separate wiring (still planned); the matrix document should be created before the first public release.
