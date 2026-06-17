# Functional Requirements — Native App (Windows / macOS / iOS / Android)

The native companion app whose job is **protected video playback with the screenshot/recording black-out** the browser can't deliver, and (future) **offline attendance**. It is *not* a second portal: browsing, enrollment, assignments, and quizzes stay in the responsive web portal. Students reach the app by clicking **Play** in the portal, which deep-links into the app for their device.

Shared engines (auth/identity, enrollment gate, video access/audit, device binding) live in [01 — Platform/shared](01-functional-platform-shared.md). The video architecture and the browser→app handoff are in [05](05-secure-video-streaming-options.md); the OS-level capture protection is specified in [09 — App non-functional](09-non-functional-app.md).

## Contents

- [A. Scope & platforms](#a-scope--platforms)
- [B. Flow & screen inventory](#b-flow--screen-inventory)
- [C. Authentication & session](#c-authentication--session)
- [D. Deep-link launch & browser→app handoff](#d-deep-link-launch--browserapp-handoff)
- [E. Device binding](#e-device-binding)
- [F. Secure video playback](#f-secure-video-playback)
- [G. Capture protection (the black-out)](#g-capture-protection-the-black-out)
- [H. Failure & offline states](#h-failure--offline-states)
- [I. Updates & return to portal](#i-updates--return-to-portal)
- [J. Future: offline attendance](#j-future-offline-attendance)

---

## A. Scope & platforms

| ID | Requirement | Notes |
|---|---|---|
| FR-APP-SCOPE-001 | The app SHALL ship from a single codebase to **Windows, macOS, iOS, and Android** (Flutter — reuse the existing desktop app). | One codebase, four targets. |
| FR-APP-SCOPE-002 | The app's responsibilities SHALL be limited to: authenticated **secure video playback** with capture protection, and (future) **offline attendance**. Catalogue, enrollment, assignments, and quizzes SHALL remain in the web portal. | Not a second portal. |
| FR-APP-SCOPE-003 | The app SHALL be tenant-aware: a session belongs to one tenant, and the app SHALL operate within the signed-in student's tenant. | Tenant-ready. |

## B. Flow & screen inventory

| # | Screen | Purpose | Satisfies |
|---|---|---|---|
| 1 | **Splash / deep-link handler** | Cold-start entry; parse the incoming link and route | FR-APP-LNK-002 |
| 2 | **Sign in** | Firebase auth when there's no valid session/handoff | FR-APP-AUTH-001/003 |
| 3 | **Device consent** | Bind this device on first use (or block if another is bound) | FR-APP-DEV-001/002 |
| 4 | **Player** | The core screen: protected HLS playback + watermark + black-out | FR-APP-VID-*, FR-APP-CAP-* |
| 5 | **Failure / retry** | Specific error states with the right action | FR-APP-ERR-001/002 |
| 6 | **Idle / home** | Shown when launched without a link; button to open the web portal | FR-APP-NAV-001 |
| 7 | *(future)* **Attendance** | Offline attendance capture & sync | FR-APP-OFF-001 |

## C. Authentication & session

| ID | Requirement | Notes |
|---|---|---|
| FR-APP-AUTH-001 | The app SHALL authenticate the student via the same identity provider as the portal (Firebase) and exchange it for a platform session; only `Active` students may sign in. | One identity everywhere. |
| FR-APP-AUTH-002 | The app SHALL persist its session **securely in the OS keystore** (Keychain / Credential Manager / Keystore) and refresh without re-login until expiry or revocation. | See `NFR-APP-SEC-*`. |
| FR-APP-AUTH-003 | When launched from a deep link carrying a **one-time handoff code**, the app SHALL exchange it for a session; if the code is missing/expired, the app SHALL prompt sign-in. The raw bearer token SHALL **never** be read from a URL. | Fixes today's "JWT in the URL" smell. |
| FR-APP-AUTH-004 | Sign-out SHALL clear the stored session, device-scoped tokens, and any cached playback URLs/keys. | Clean logout. |

## D. Deep-link launch & browser→app handoff

| ID | Requirement | Notes |
|---|---|---|
| FR-APP-LNK-001 | The app SHALL register the platform deep link — `salah-bahazad://` scheme on Windows/macOS, **Universal Links** on iOS, **App Links** on Android — so the OS routes *Play* to the app. | OS protocol handler. |
| FR-APP-LNK-002 | On **cold start** from a link the splash SHALL parse the URI and route to the player; if the app is **already running**, the live link SHALL be handled and route to the player. | Both entry paths. |
| FR-APP-LNK-003 | The deep link SHALL carry the **session id, video id, and a short-lived one-time handoff code** only — never the raw token. The app SHALL validate and consume the code server-side. | Minimal, safe payload. |
| FR-APP-LNK-004 | If the app is not installed when the portal attempts the link, the portal SHALL prompt installation (`FR-STU-VID-003`); after install, opening the link SHALL resume to the correct player. | Install fallback. |

## E. Device binding

| ID | Requirement | Notes |
|---|---|---|
| FR-APP-DEV-001 | On first authenticated use, the app SHALL participate in **device binding** (one device per student); if no device is bound it SHALL show the consent prompt and bind on acceptance (`FR-PLAT-DEV-*`). | Anti-sharing. |
| FR-APP-DEV-002 | The app SHALL store the device token in the OS keystore and present it on each request; playback from a device other than the bound one SHALL be refused with a clear "device not recognised — contact support" message. | Enforcement. |
| FR-APP-DEV-003 | After staff clear a student's device, the next app launch SHALL be allowed to re-bind. | Recovery. |

## F. Secure video playback

| ID | Requirement | Notes |
|---|---|---|
| FR-APP-VID-001 | To play, the app SHALL request a **short-lived signed HLS URL** from the backend, which enforces the gate (active enrollment + quiz passed where applicable + access remaining) and records the view + audit. | Server is the gate (`FR-PLAT-VID-001/002`). |
| FR-APP-VID-002 | The app SHALL play **AES-128-encrypted HLS**, fetching the decryption key over an authenticated request; it SHALL NOT expose the media URL or key. | Encrypted delivery. |
| FR-APP-VID-003 | The app SHALL paint a **dynamic visible watermark** (student serial/phone, repositioning periodically) over the video. | Traceability (`FR-PLAT-VID-004`). |
| FR-APP-VID-004 | The player SHALL offer standard controls (play/pause, seek, speed, volume, fullscreen) with **no download/export** affordance and no exposed source URL. | Hardened player. |
| FR-APP-VID-005 | Each successful play SHALL consume one access against the per-video cap and the app SHALL surface the remaining count. | View budget. |

## G. Capture protection (the black-out)

> Per-OS mechanisms, guarantees, and honest caveats are specified in [09 — App non-functional → Capture protection](09-non-functional-app.md). Functional expectations:

| ID | Requirement | Notes |
|---|---|---|
| FR-APP-CAP-001 | While video is on screen, the app SHALL enable the OS secure-surface protection so **screenshots and screen recordings render black** — Android `FLAG_SECURE`, iOS protected player layer + capture detection, Windows `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)`, macOS `NSWindow.sharingType = .none`. | The core requirement. |
| FR-APP-CAP-002 | On iOS/Android, if active screen capture or mirroring is detected, playback SHALL pause/blank and resume only when capture stops. | Recording defence. |
| FR-APP-CAP-003 | Protection SHALL be enabled **before the first frame** and disabled only when leaving the player. | No unprotected window. |

## H. Failure & offline states

| ID | Requirement | Notes |
|---|---|---|
| FR-APP-ERR-001 | The app SHALL present specific, user-readable states for: **unauthorized** (signed out / session expired), **forbidden** (not enrolled / wrong device), **max-views-reached**, **expired enrollment**, **video not found**, **offline/network**, and **server error** — each with the right action (sign in, retry, back to portal). | Clear failure UX. |
| FR-APP-ERR-002 | Transient network errors SHALL offer **retry** without losing player context. | Resilience. |

## I. Updates & return to portal

| ID | Requirement | Notes |
|---|---|---|
| FR-APP-UPD-001 | The app SHALL support updating to the latest version (store auto-update on mobile; an update path on desktop) and SHALL be able to **require a minimum version** before playback. | Ship fixes; enforce floor. |
| FR-APP-NAV-001 | From the idle/home screen the student SHALL be able to open the **web portal** in the system browser. | Return path. |

## J. Future: offline attendance

| ID | Requirement | Notes |
|---|---|---|
| FR-APP-OFF-001 | *(Future)* The app SHALL support recording attendance **offline** and syncing it when back online. Out of scope for the first release, but the architecture SHALL NOT preclude it. | The business reason the mobile app exists; sequence it after secure playback. |

---

➡️ Next: [09 — App non-functional requirements](09-non-functional-app.md) · [Back to overview](README.md)
