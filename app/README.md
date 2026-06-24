# Secure Player

The **Salah Bahzad** native companion app — a single **Flutter** codebase (Windows · macOS · iOS · Android) whose only job is **protected video playback**. Browsing, enrollment and quizzes stay in the web portal; the app is reached by a **Play deep link**.

> Conventions, architecture, the design source of truth, and the build/test gate are in **[`CLAUDE.md`](CLAUDE.md)**. Read it before changing code.

## Run

```bash
flutter run \
  --dart-define=API_BASE_URL=https://localhost:5010 \
  --dart-define=PORTAL_URL=https://localhost:56092 \
  --dart-define=APP_VERSION=1.0.0
```

## Green gate

```bash
flutter analyze                 # clean
flutter test                    # unit + golden (360 / 768 / 1280)
flutter build windows --debug
flutter build apk --debug
```

## Status

**Phase A0** — foundation: design system, responsive shell + desktop window chrome, device-agnostic Firebase sign-in → `app-exchange` → keystore session + silent refresh, deep-link parser, and the Splash / Sign in / Idle screens + a Player placeholder. Real playback (redeem → AES-128 HLS → watermark) and capture protection arrive in A1/A2. See `docs/IMPLEMENTATION-PLAN-native-app.md`.
