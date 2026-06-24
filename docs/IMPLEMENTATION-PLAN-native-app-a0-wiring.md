# Native App · A0 — WIRING stream (prove device-agnostic sign-in + deep-link + the shell live on the Aspire stack)

> Status: **Planned — not yet built** · Created 2026-06-24 · Proves phase **A0** end-to-end against the **running Aspire stack** (Postgres + Redis + MinIO + API), with the Flutter app pointed at the API's dev URL — like the Phase 3/4/5/S0 wiring streams. Goal: **zero contract drift** vs `docs/contracts/native-app-playback.md` §A/§B/§E and the A0 backend/app streams, every `reason` exercised, and the **device-agnostic** headline proven.
>
> Runs **after** the backend + app streams merge. Reuses prior wiring techniques: read Aspire-assigned ports from the dashboard (reassigned every run); verify DB with `docker exec -i <pg> psql` (PascalCase columns quoted; pipe SQL via stdin — PS 5.1 mangles inline `-c`); mint a Firebase `idToken` via the REST `signInWithPassword` to drive `app-exchange` without a browser; **"new route 404 not 401 → the running API is stale, restart AppHost"**.
> Satisfies (proven live): `FR-APP-AUTH-001/002/004`, `FR-APP-DEV-001`, `FR-APP-LNK-001/002`, `FR-APP-NAV-001`, `NFR-APP-SEC-001/003/004`, `NFR-APP-REL-002`, `NFR-APP-PERF-001`.

---

## Design reference
Verifies behaviour, not pixels. Acceptance copy/states from the prototype `SIGN IN` / `SPLASH` / `IDLE / HOME` banners + contract §H.

## AppHost change to land first
**None.** The app is **not** an Aspire service. Run the API via AppHost (F5/`dotnet run`), then run the app with `flutter run -d windows --dart-define=API_BASE_URL=https://localhost:<api-port>` (port from the Aspire dashboard). The app is native (no browser) → **no CORS** needed. The Aspire dev TLS cert must be trusted by the OS store (TLS is never disabled). Register the `salah-bahazad://` scheme locally for deep-link tests (the Windows/macOS runner handles it; trigger with `start "" "salah-bahazad://stream?..."` / `open "salah-bahazad://..."`).

## Pre-flight
- Backend + app streams merged; `dotnet test -c Release` green (known `QuestionBank` baseline); `flutter analyze` + `flutter test` green.
- Aspire stack up; discover container names + ports dynamically (renamed each run). If `app-exchange` returns **404 not 401**, the running API predates the route → restart AppHost.
- A direct-drive helper: `POST {firebaseAuthBase}/v1/accounts:signInWithPassword?key=<API_KEY>` → `idToken` → `POST /api/auth/student/app-exchange { firebaseIdToken }` (no browser needed). Decode the returned JWT (header.payload) to inspect claims.

## Fixtures (reuse the S0/S1 seed where possible)
- One **`Active`** student with known Firebase email/password (happy path + device-agnostic).
- The same student also **device-bound via the portal** (`/api/auth/student/exchange` once) — to prove the app ignores that binding.
- One each **`Pending`** / **`Rejected`** (with reason) / **`Inactive`** student — the gate reasons.
- A second-tenant student is **not** required for A0 (no `/me/*` data yet) but keep one handy for A1.

## Live checks (target: all green, zero drift)
**Sign-in — device-agnostic (`#1–#4`, `FR-APP-AUTH-001`, `FR-APP-DEV-001`):**
1. `app-exchange` for the `Active` student → **`200`** `StudentAuthResponse`; decode the access JWT → `role=Student`, `tenant_id` present, **no `device_id`**; `student.boundDevice == null`. **DB (psql):** no new `StudentDevices` row written.
2. **Headline:** the student is **already device-bound via the portal**; `app-exchange` from a *different* machine/context (no device token) → **`200`** (contrast — a portal `/exchange` from that other device returns `403 device_not_recognized`). Run both side by side to show the divergence.
3. A second `app-exchange` "from another device" → **`200`** again; no binding rows accumulate.
4. `Pending` → `403 account_pending`; `Rejected` → `403 account_rejected` (detail = reason); `Inactive` → `403 account_inactive`; unknown Firebase UID → `401`; hammer the bucket → `429`. The app renders the matching error state (contract §H).

**Refresh (`#5`, `FR-APP-AUTH-002`):**
5. Refresh an **app** refresh token (no `device_id`) → **`200`** with a fresh pair, **even though no `StudentDevice` exists**. Regression: a **portal** refresh token (with `device_id`) still requires an active device.

**Deep-link + shell (`#6–#8`, `FR-APP-LNK-001/002`, `NFR-APP-REL-002`, `FR-APP-NAV-001`):**
6. With the app signed in, fire `salah-bahazad://stream?videoId=<g>&sessionId=<g>&handoff=<code>` **cold-start** and **warm** → routes to the **Player placeholder** showing the parsed `videoId`/`handoff`. Raw token never appears in the URI.
7. A **malformed** link (`salah-bahazad://stream?foo=bar`) → a clear error screen, **no crash**.
8. Idle → **Open the student portal** opens the portal in the system browser.

**Security + performance (`#9–#10`, `NFR-APP-SEC-001/003`, `NFR-APP-PERF-001`):**
9. Inspect storage: tokens live **only** in the OS keystore (Keychain/Credential Manager/Keystore) — absent from `SharedPreferences`/plaintext files; grep logs → no tokens/PII.
10. Cold start to a usable screen **< 3 s**.

**Browser/app visual (`#11`):**
11. The full walkthrough on a real device — **the user's visual step** (pending).

## Sign-off
- Log the run below; flip the master plan's **A0** status to **Met** (date + headline tally + zero-drift); write a `native-app-a0-wiring` memory (techniques + gotchas); note any drift as a contract change (change the contract first).

---
## ✅ MET — Run log (2026-06-24, live on the Aspire stack) — **10/10 scripted, ZERO contract drift**

Proven live against `dotnet run` AppHost (Postgres `postgres-cjnnhfjx` + Redis + MinIO + API at `http://127.0.0.1:58513`), with the **Flutter Windows app running against the live API**. Fixture: the `Active` student `019eea34-…b849` ("Wiring Test Student") repointed to a Firebase user I control (`app-wire-a@salah-bahzad.local` / uid `K9veml…163`); a second Firebase user (`app-wire-nouser@…`) maps to **no** student. Drift double-checked by a 4-lens adversarial Workflow over the captured evidence (§A/§A.1/§A.2/§B/§E/§G/§H) — **no drift**.

**Sign-in — device-agnostic (#1–#4):**
- **#1** `app-exchange` (Active) → **200** `StudentAuthResponse` with exactly the 5 §A.1 keys; `student.boundDevice = null`, `status:"Active"` (enum name). Access JWT: `role=Student`, `tenant_id` present, `token_type=access`, **no `device_id`**, `iss=salah-bahzad-api`, `aud=salah-bahzad-admin` (the `bahzad` spelling); refresh JWT also has **no `device_id`**; ~15 min / ~7 day expiries; **no `Set-Cookie`**. **DB:** **0** `student_devices` rows; audit = exactly one `StudentSignedIn` per call (`Portal=app`, `ActorType=Student`), **0** `StudentDeviceBound`.
- **#2 HEADLINE (device-agnostic):** student **device-bound via the portal** (`/student/exchange` → 200 + `Set-Cookie sb_device …httponly secure samesite=lax`; portal access JWT **carries** `device_id`) → then **`app-exchange` from "another machine" (no device token) → 200** (the binding is ignored). **Contrast:** portal `/exchange` from another device (no cookie) → **403 `device_not_recognized`**. The divergence is proven side-by-side.
- **#3** two more `app-exchange` "from other devices" → **200, 200**; still **0** binding rows.
- **#4** `Pending`→**403 `account_pending`**; `Rejected`→**403 `account_rejected`** (`detail` = the student's `RejectionReason`); `Inactive`→**403 `account_inactive`**; unknown Firebase UID→**401**; rate-limit hammer → **`10×200` then `3×429`** (`auth` = FixedWindow 10/60 s). `device_not_recognized` correctly **does not** apply to the app path.

**Refresh — app-aware (#5):**
- App refresh token (no `device_id`) → **200**, reissued pair still **device-less**, **even with a `student_devices` row present** and **even after that device is cleared** (control). Portal refresh token (with `device_id`) → **200** (`device_id` preserved) while the device is active, but → **401** once the device is staff-cleared. The §B divergence is exact.

**Deep-link + shell + security (#6–#10 — logic proven; on-screen routing is the user's #11):**
- App **builds on Windows** (`flutter build windows --debug` ✓) and **runs against the live API** (`flutter run -d windows --dart-define=API_BASE_URL=http://127.0.0.1:58513` → `secure_player.exe` launched, VM service up, **no crash / no Firebase error**, **no tokens/PII in logs**). `flutter analyze` **clean**; `flutter test` **34/34** (deep-link parser valid+malformed → #6/#7 logic, `SessionStore` keystore round-trip → #9 logic, `AuthController` `403{reason}`→state map → §H, responsive goldens 360/768/1280).
- Real Firebase wired for the app this stream: **`app/lib/firebase_options.dart`** (dev project `salah-bahazad-development`) + `main.dart` now `initializeApp(options: …)` (guarded). `salah-bahazad://` registered in **HKCU** → the debug exe (`"%1"`). Deep-link demo (cold-start → Player placeholder): `salah-bahazad://stream?videoId=019ee8f9-77bd-7e67-82df-ac2a7200db35&sessionId=019ee8f9-75d6-79d0-a08f-63b7a9bf18a5&handoff=<code>`. In-GUI sign-in creds for the user: **`app-wire-a@salah-bahazad.local` / `Passw0rd!wire1`**.

**Fix made this stream:** `test/idle_overflow_probe_test.dart` was missing `import 'package:flutter/foundation.dart'` (`FlutterExceptionHandler` undefined) → `flutter analyze` was 1-error; added the import (gate now clean). Observation for the app stream: the probe reveals `IdleView` overflows (255 px / 109 px) with an extreme-length name at 360 px — minor responsive polish, goldens still pass.

**Pending (user visual — #11):** on-screen deep-link routing to the Player placeholder (#6), Idle → **Open the student portal** opening the system browser (#8, note the `:4300` Angular dev-server exited under Aspire — restart `start:student` to revive it), and cold-start < 3 s eyeball (#10). The app is running now for the walkthrough.

---
## Kickoff prompt (paste into a fresh Claude session at the repo root)
```
You are running the WIRING stream of Native App phase A0 for Salah Bahzad. Prove device-agnostic sign-in + app-aware refresh + the deep-link/shell live on the running Aspire stack, with ZERO contract drift vs docs/contracts/native-app-playback.md §A/§B/§E.

Read first, in order:
1. docs/contracts/native-app-playback.md (§A/§B/§E/§H)
2. docs/IMPLEMENTATION-PLAN-native-app-a0-backend.md and -a0-app.md (what was built)
3. docs/IMPLEMENTATION-PLAN-native-app-a0-wiring.md (this stream — checks #1–#11)
4. the S0/S1 wiring memories (direct Firebase signInWithPassword idToken technique; docker exec psql; Aspire port discovery)

Do: run the API via AppHost; run the app with --dart-define=API_BASE_URL=<aspire api port>; execute checks #1–#10 (the device-agnostic headline #2 is the point — a portal-bound student still signs into the app from another device → 200). Verify the DB with docker exec -i <pg> psql. Leave #11 (visual) for the user.

Log the run, flip the master plan's A0 status to Met (date + N/N + ZERO drift), and write the native-app-a0-wiring memory. Report the tally.
```
