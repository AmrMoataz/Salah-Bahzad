# Native App · A3 — APP stream (Sentry + offline mid-playback + failure polish)

> Status: **DONE** · Created + implemented 2026-06-27 · Closes phase **A3** app stream.
> No backend changes. No contract changes (`docs/contracts/native-app-playback.md` is
> untouched). Files changed: `pubspec.yaml`, `lib/core/logging/sentry_log_sink.dart`
> (new), `lib/main.dart`, `lib/features/player/player_controller.dart`.

---

## What A3 found was **already complete** (audit, no code needed)

After reading every file in the player pipeline, the following were fully built in A1/A2:

| Item | Where |
|---|---|
| All 7 §H failure states — verbatim §H copy, right actions | `player_state.dart:PlayerError.fromApi` |
| `ErrorStateView` — mascot, reassurance footer, primary + secondary buttons | `features/errors/error_state_view.dart` |
| `_PlayerErrorOverlay` — inline failure over the dark stage | `features/player/player_view.dart` |
| `retry()` — reuses manifest within TTL, no double-decrement | `features/player/player_controller.dart` |
| Primary action routing (retry / signIn / openPortal / backToPortal) | `features/player/player_page.dart` |
| Idle — security strip (3 cards) + how-to card + sign-out + footer | `features/idle/idle_view.dart` (fully polished) |
| `signOut()` — cancels refresh timer, clears Firebase session, clears keystore | `features/auth/auth_controller.dart` |
| In-memory secrets auto-purge | Provider disposal (`_teardown` clears proxy + manifest) |

---

## What A3 actually built

### 1. Sentry crash reporting (`NFR-APP-OBS-001/002`)

**`pubspec.yaml`** — added:
```yaml
sentry_flutter: ^8.10.0
```

**`lib/core/logging/sentry_log_sink.dart`** (new) — `LogSink` impl:
- `LogLevel.error` and above → `Sentry.captureException` (Issues list)
- Below `error` → `Sentry.addBreadcrumb` (trail)
- **Never throws** into the logger (`LogSink` contract)
- `sendDefaultPii = false` in Sentry options + call-site redaction = no
  tokens / handoffs / signed-URLs / PII in payloads (`NFR-APP-SEC-003`)

**`lib/main.dart`** — split into two branches on `SENTRY_DSN` env var:
- **Empty DSN (dev/CI):** keep the existing `runZonedGuarded` +
  `Log.installErrorHandlers()` path — nothing changes in development.
- **Non-empty DSN (production):** `SentryFlutter.init(...)` → inside
  `appRunner`, call `Log.configure(..., extraSinks: [SentryLogSink()])` then
  boot. Do **not** call `Log.installErrorHandlers()` — Sentry installs its
  own `FlutterError` / `PlatformDispatcher` hooks, double-installing corrupts
  the chain.
- Shared boot logic extracted to `_boot()` to avoid duplication.

Pass at build time:
```
flutter build apk --dart-define=SENTRY_DSN=https://…@sentry.io/…
                  --dart-define=SENTRY_ENV=production
                  --dart-define=APP_VERSION=1.0.0
```
In dev, omit `SENTRY_DSN` — Sentry is disabled, no changes to the local
debug experience.

### 2. Offline mid-playback (`FR-APP-ERR-002`, offline §H state)

**`pubspec.yaml`** — added:
```yaml
connectivity_plus: ^6.1.4
```

**`lib/features/player/player_controller.dart`** — two changes:

**(a) Connectivity listener** (`_onConnectivityChanged`) — subscribed in
`_bindEngine`, cancelled in `_teardown`:
- **Connectivity lost while playing** → `_engine.pause()` immediately, so a
  screen recorder captures a still frame rather than live lesson content.
- **Connectivity restored while paused** → `_engine.play()` auto-resumes if
  and only if the player is in a clean `paused` state (not `error`/`ended`).
  A player in `error` requires an explicit retry — no surprise auto-resume
  after a long outage.

**(b) Engine error → connectivity check** (`_onEngineError`):
- When libmpv reports an error (buffer exhausted, segment fetch failed), check
  connectivity at that moment:
  - All results `none` → show the `offline` §H state ("You're offline", "Try
    again" → `retry()` reuses the manifest, no re-mint).
  - Otherwise → show the `server` §H state ("Something went wrong", "Try
    again").
- Added `videoId` field to the log call for Sentry breadcrumb context.

**`_offlineError()` factory** — new private method mirroring the verbatim
§H offline copy (same message as `PlayerError.fromApi` network branch).

---

## Green gate

```
flutter analyze                  # clean
flutter test                     # unit + goldens pass (no new test surface in A3;
                                 #   connectivity scenarios proven in wiring)
flutter build windows --debug
flutter build apk --debug
flutter build macos              # (on this Mac)
```

The existing `secure_surface_test.dart` / `player_page_secure_surface_test.dart`
/ `capture_banner_golden_test.dart` remain green — no change to the surface or
the test fakes.

---

## Deferred to A4

The 8th §H state, `update-required` (`426 outdated_app`), lands with A4
(distribution + min-version enforcement). The `PlayerError.fromApi` factory
already has a comment noting this; `PlayerErrorKind` has no `updateRequired`
variant yet.
