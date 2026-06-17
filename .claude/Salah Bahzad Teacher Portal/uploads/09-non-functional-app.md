# Non-Functional Requirements — Native App (Windows / macOS / iOS / Android)

Quality attributes and constraints for the companion app whose functional scope is in [08](08-functional-app.md). The headline attribute is **capture protection (the black-out)** — specified per-OS below with honest guarantees — followed by security (which also fixes the current desktop app's known issues), distribution, updates, and the rest.

## Contents

- [Capture protection (the black-out)](#capture-protection-the-black-out)
- [Security](#security)
- [Performance](#performance)
- [Reliability](#reliability)
- [Distribution & code signing](#distribution--code-signing)
- [Updates](#updates)
- [Compatibility](#compatibility)
- [Observability](#observability)
- [Privacy](#privacy)
- [Maintainability & delivery](#maintainability--delivery)
- [Accessibility](#accessibility)

---

## Capture protection (the black-out)

> The black-out comes from each OS's secure-surface API — **no DRM**. It is reliable on Android, Windows, and macOS; **iOS is the one honest gap for still screenshots** (see NFR-APP-CAP-005). All of this is version-dependent and SHALL be verified on the targeted OS versions.

| ID | Requirement | Platform behaviour |
|---|---|---|
| NFR-APP-CAP-001 | **Android:** the player window SHALL set `FLAG_SECURE`. | Screenshots and screen recording render **black**, and the app is excluded from the recent-apps thumbnail. No DRM needed. |
| NFR-APP-CAP-002 | **Windows:** the player window SHALL set `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)`. | The window is **excluded from screenshots and screen recording** (OBS, Snipping Tool, etc.). No DRM needed. (Windows 10 2004+.) |
| NFR-APP-CAP-003 | **macOS:** the player window SHALL set `NSWindow.sharingType = .none`. | The window is excluded from screen sharing/recording and screenshots of that window. No DRM needed. |
| NFR-APP-CAP-004 | **iOS — recording:** the app SHALL detect active capture/mirroring via `UIScreen.isCaptured` and **blank/pause** playback until it stops. | Screen recording and AirPlay mirroring are effectively defeated (the recording shows the blanked state). No DRM needed. |
| NFR-APP-CAP-005 | **iOS — still screenshots:** iOS provides **no API to block a single screenshot** without FairPlay DRM. The app SHALL therefore (a) detect screenshots (`userDidTakeScreenshot`) and log/flag the event, and (b) rely on the visible watermark. If a hard screenshot black-out on iPhone is later deemed essential, FairPlay MAY be enabled **for iOS only** as a targeted exception. | Honest gap: a single iOS screenshot captures a watermarked frame; recording is still blocked by NFR-APP-CAP-004. Consistent with the "phone camera is accepted" stance. |
| NFR-APP-CAP-006 | Protection SHALL be enabled **before the first frame** and remain on for the whole time video is on screen. | No unprotected window of exposure. |

## Security

> Several items here explicitly remediate findings in the current desktop app (TLS bypass, tokens in logs).

| ID | Requirement | Notes |
|---|---|---|
| NFR-APP-SEC-001 | Session and device tokens SHALL be stored in the **OS keystore** (iOS/macOS Keychain, Windows Credential Manager, Android Keystore), never in plaintext files or preferences. | — |
| NFR-APP-SEC-002 | TLS certificate validation SHALL **never** be disabled in any build. The current production TLS-bypass override SHALL be removed. | Fixes a current critical finding. |
| NFR-APP-SEC-003 | Bearer tokens, handoff codes, signed URLs, and PII SHALL **never** be written to logs or crash reports. | Fixes the current token-in-logs finding. |
| NFR-APP-SEC-004 | The raw bearer token SHALL never be transported in a deep-link URL; only a short-lived one-time handoff code SHALL be. | Mirrors `FR-APP-AUTH-003`. |
| NFR-APP-SEC-005 | Signed playback URLs and HLS keys SHALL be held in memory only for the playback session and never persisted to disk. | Ephemeral secrets. |
| NFR-APP-SEC-006 | The app SHOULD apply certificate pinning to the API host and MAY apply basic root/jailbreak detection as a deterrent. | Defence in depth. |

## Performance

| ID | Requirement | Target |
|---|---|---|
| NFR-APP-PERF-001 | Cold start to a usable screen | < 3 s on a typical device. |
| NFR-APP-PERF-002 | "Play" tapped → first frame (incl. gate + signed URL) | < 4 s p95 on broadband. |
| NFR-APP-PERF-003 | Smooth adaptive playback up to 1080p without dropped frames on mid-range devices; adaptive bitrate on poor networks. | Watchable everywhere. |

## Reliability

| ID | Requirement | Notes |
|---|---|---|
| NFR-APP-REL-001 | The app SHALL handle network loss gracefully — pause, surface an offline state, and resume/retry without crashing or losing context. | Resilient. |
| NFR-APP-REL-002 | A malformed/expired deep link SHALL route to a clear error, never a crash. | Robust entry. |
| NFR-APP-REL-003 | The development mock playback path SHALL exercise the happy path (the current mock always fails — fix it). | Fixes a current finding. |

## Distribution & code signing

| ID | Requirement | Notes |
|---|---|---|
| NFR-APP-DIST-001 | **iOS:** distributed via the App Store, code-signed under an Apple Developer Program account (~$99/yr). | Store + signing. |
| NFR-APP-DIST-002 | **Android:** distributed via Google Play as a signed AAB (one-time ~$25 registration). | Store + signing. |
| NFR-APP-DIST-003 | **macOS:** Developer ID-signed and **notarized** (or App Store). | Gatekeeper. |
| NFR-APP-DIST-004 | **Windows:** a signed (Authenticode) installer; the installer script SHALL NOT hardcode developer-specific absolute paths. | Fixes a current finding. |
| NFR-APP-DIST-005 | App icons/assets SHALL be present and per-environment as configured (the current build references missing `assets/`). | Fixes a current finding. |

## Updates

| ID | Requirement | Notes |
|---|---|---|
| NFR-APP-UPD-001 | Mobile SHALL use store auto-update; desktop SHALL have an update mechanism. | Ship fixes. |
| NFR-APP-UPD-002 | The backend SHALL be able to enforce a **minimum app version** for playback (to retire builds with security gaps). | Version floor. |

## Compatibility

| ID | Requirement | Notes |
|---|---|---|
| NFR-APP-COMPAT-001 | Minimum OS versions SHALL be defined and documented, chosen so the capture-protection APIs are available — indicatively Windows 10 2004+, macOS 11+, iOS 13+, Android 8+ (confirm during build). | Black-out depends on these. |
| NFR-APP-COMPAT-002 | On an OS/version where the black-out cannot be guaranteed, the app SHALL warn and MAY refuse protected playback rather than play unprotected silently. | Fail safe, not silent. |

## Observability

| ID | Requirement | Notes |
|---|---|---|
| NFR-APP-OBS-001 | Crash/error reporting SHALL be enabled (e.g. Sentry) with **no PII/tokens** in payloads. | Diagnosability, safely. |
| NFR-APP-OBS-002 | Playback failures SHALL be reportable with enough non-sensitive context (failure type, video id, app version) to diagnose. | Supportability. |

## Privacy

| ID | Requirement | Notes |
|---|---|---|
| NFR-APP-PRIV-001 | The app SHALL collect only what playback/auth/device-binding require and SHALL declare it accurately in App Store / Play privacy disclosures. | Store compliance + minors' data. |

## Maintainability & delivery

| ID | Requirement | Notes |
|---|---|---|
| NFR-APP-MAINT-001 | One Flutter codebase SHALL serve all four platforms; platform-specific code SHALL be limited to the secure-surface shims and store packaging. | Low duplication. |
| NFR-APP-MAINT-002 | CI SHALL build (and where possible test) all targets; releases SHALL be repeatable, not hand-built. | Fixes "no CI/CD". |
| NFR-APP-MAINT-003 | The capture-protection behaviour SHALL have a documented manual test matrix per OS (screenshot + screen-record on each), executed before release. | You must verify the black-out actually blacks out. |
| NFR-APP-MAINT-004 | Dead code and unused dependencies SHALL be removed (current app has duplicate providers, an unused Retrofit client, a stray test screen, etc.). | Tidy baseline. |

## Accessibility

| ID | Requirement | Notes |
|---|---|---|
| NFR-APP-A11Y-001 | Player controls SHALL be reachable by assistive tech and respect OS text-size/contrast settings; captions/subtitles SHALL be supported where provided. | Inclusive playback. |

---

➡️ [Back to overview](README.md) · [08 — App functional requirements](08-functional-app.md)
