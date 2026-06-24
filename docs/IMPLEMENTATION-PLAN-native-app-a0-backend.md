# Native App · A0 — BACKEND stream (device-agnostic app sign-in + app-aware refresh)

> Status: **Planned — not yet built** · Created 2026-06-24 · The **engine half** of phase **A0** in `docs/IMPLEMENTATION-PLAN-native-app.md` (§7-A0). Almost nothing is new: S0 already built Firebase verification, the student status gate, JWT issuance, and refresh. This stream adds **one device-agnostic sign-in path** for the app and makes refresh **app-aware** — so a student can sign in from **any machine** with no device binding. **No migration.**
>
> Satisfies: `FR-APP-AUTH-001` (Firebase → session, Active-only), `FR-APP-AUTH-002` (refresh without re-login), `FR-APP-DEV-001` (device-agnostic; no bind/enforce), `FR-APP-LNK-002/003` (deep-link key canonicalization, doc-only). Implements **`docs/contracts/native-app-playback.md` §A, §B, §E** verbatim. **Change the contract (`docs/contracts/native-app-playback.md`) first if anything moves.**
>
> Run in its **own** Claude session, parallel with the app stream. **File ownership: `backend/**` only**, plus the doc-only edits to `docs/05-secure-video-streaming-options.md` and `docs/08-functional-app.md` (deep-link keys). Gates: `dotnet test -c Release` green (the one pre-existing `QuestionBank` image-test failure is the known baseline); then the **wiring** stream proves it live on Aspire.

---

## Design reference
No UI. This stream owns only the JSON the app's Sign-in screen (`<!-- ============ SIGN IN ============ -->`) and Splash (`<!-- ============ SPLASH / DEEP-LINK HANDLER ============ -->`) consume. Behaviour authority: contract §A/§B + `FR-APP-AUTH-*`, `FR-APP-DEV-001`.

## 1. Frozen contract (this stream)
Implements `docs/contracts/native-app-playback.md`:
- **§A** — `POST /api/auth/student/app-exchange` (**NEW**, `AllowAnonymous` + rate-limit `auth`) → `200 StudentAuthResponse`; JWTs carry **no `device_id`**; `student.boundDevice = null`; **no** device binding; status gates `403 {reason}` (`account_pending`/`account_rejected`/`account_inactive`), `401` no-account, `429` rate-limit. **No `device_not_recognized`.**
- **§B** — `POST /api/auth/refresh` (EXISTS, **made app-aware**): for an app token (no `device_id`) skip the device re-check; the portal path (with `device_id`) is unchanged.
- **§E** — deep-link canonical keys `videoId`/`sessionId`/`handoff` (doc-only: align `docs/05` + `docs/08`).
- JWT facts unchanged: `iss=salah-bahzad-api`, `aud=salah-bahzad-admin` (the **`bahzad`** spelling), HS256.

## 2. Pre-flight — confirm what already EXISTS (do **not** rebuild)
Re-read `backend/CLAUDE.md` (Authentication & authorization, Audit log, Multi-tenancy, Security checklist) + master plan §3.2. Then confirm in code:
- Existing student exchange `POST /api/auth/student/exchange` — `AuthEndpoints.cs:38-47` (route), handler `ExchangeStudentFirebaseTokenHandler.cs`. Reuse its **Firebase verification**, **student-by-UID + tenant resolution**, and **status gate** (`:33-76`, reasons at `:33-36`) — do **not** re-implement them.
- Device binding — `DeviceBindingService.cs:22-30`, `StudentDevice` entity, the cookie/fingerprint inputs at `AuthEndpoints.cs:87-94`, the bind decision `ExchangeStudentFirebaseTokenHandler.cs:85-97`. The app path **skips all of this**.
- JWT issuance — `JwtTokenService.cs:26-30,72-99`; **`device_id` is added only when a deviceId is supplied** → passing `null` yields a token **without** `device_id` (this is the whole trick; no JwtTokenService change needed).
- Refresh — `RefreshTokenHandler.cs:37-39` (role-aware split) + `:76-128` (re-checks `Active` and that `device_id` maps to an active `StudentDevice` → else `401`). This is the only place to make app-aware.
- Response shape — `StudentAuthResponse.cs:12-28` (reuse as-is; `BoundDevice` will be `null` for app sessions).
- Audit events — `StudentSignedIn` / `StudentDeviceBound` / `StudentSignInRejected` (`ExchangeStudentFirebaseTokenHandler.cs:127-150,182-191`). The app path writes `StudentSignedIn` / `StudentSignInRejected` only.
- Config — `appsettings.json:13-14` (`Jwt:Issuer = salah-bahzad-api`, `Jwt:Audience = salah-bahzad-admin`); rate-limit policy `auth`.

## 3. Application
**3.1 Command + handler — `Features/Auth/Commands/ExchangeStudentAppToken/`**
- `ExchangeStudentAppTokenCommand(string FirebaseIdToken)` + co-located `…Validator` (non-empty token).
- Handler steps: (1) verify Firebase ID token; (2) resolve student by Firebase UID (cross-tenant lookup as the existing handler does) → none ⇒ `401`; (3) **status gate** — `Active` continues, else `ForbiddenException(detail, reason)` with `account_pending`/`account_rejected`(detail = rejection reason)/`account_inactive`; (4) **no device work**; (5) issue access + refresh JWTs via `JwtTokenService` with **`deviceId: null`**; (6) audit `StudentSignedIn` (`ActorType=Student`, `Portal=app`); (7) return `StudentAuthResponse` with `BoundDevice = null`.
- **DRY:** extract the shared Firebase-verify + student-lookup + status-gate + token-issue path used by the existing handler into a private helper / small application service taking a `bool bindDevice` (or a `SignInMode`), so the two handlers cannot drift. The existing portal handler keeps `bindDevice: true`; the app handler is `bindDevice: false`.

**3.2 Refresh — make `RefreshTokenHandler` app-aware**
- Read `device_id` from the refresh token. **If absent (app token):** skip the `StudentDevice` lookup; still re-check the student exists, `!IsDeleted`, `Status == Active` → else `401`. **If present (portal token):** unchanged (`:76-128`).

## 4. Infrastructure
None new. (No Redis/R2/Hangfire touch; rate-limit policy `auth` already exists.)

## 5. API — endpoints
- `authGroup.MapPost("/student/app-exchange", …).AllowAnonymous().RequireRateLimiting("auth").WithName("ExchangeStudentAppToken").WithSummary("Device-agnostic student sign-in for the native app").Produces<StudentAuthResponse>(200).ProducesProblem(401).ProducesProblem(403).ProducesProblem(429);` — body `StudentExchangeRequest{ FirebaseIdToken }` (reuse the existing request record); **no cookie set, no device headers read**.
- `/student/refresh` (existing `/auth/refresh`) — wiring unchanged; only the handler branch changes.
- **Doc-only (this stream owns it):** in `docs/05-secure-video-streaming-options.md` and `docs/08-functional-app.md`, change the deep-link example keys `?session=…&video=…` → `?videoId=…&sessionId=…&handoff=…` to match what the portal emits (contract §E).

## 6. Migration
**None.** No schema change in A0 (the `Serial` migration is A1).

## 7. Tests (`dotnet test -c Release`)
- **Unit:** `app-exchange` issues tokens with **no `device_id`** claim and `BoundDevice == null`; status gate maps `Pending/Rejected/Inactive` → the three reasons; validator rejects empty token.
- **Integration (WebApplicationFactory + Testcontainers, faked Firebase pinned to a UID):**
  - Active student → `200`; decode JWT → `role=Student`, `tenant_id` set, **no `device_id`**.
  - **Device-agnostic (headline):** seed a student already **device-bound via the portal exchange**, then call `app-exchange` (no device token) → **`200`** (contrast: a second portal `/exchange` from a different device would be `403 device_not_recognized`).
  - Two `app-exchange` calls "from different devices" (no device token either time) → both `200`, no binding rows written.
  - `Pending`/`Rejected`/`Inactive` → `403` with `reason` = `account_pending`/`account_rejected`/`account_inactive`; unknown UID → `401`; rate-limit → `429`.
  - **Refresh:** an app refresh token (no `device_id`) → `200` even with **no** `StudentDevice`; a portal refresh token (with `device_id`) still requires an active device (regression).
  - **Audit:** `app-exchange` writes exactly one `StudentSignedIn` and **no** `StudentDeviceBound`; a blocked attempt writes `StudentSignInRejected`.

## Done = ready for wiring
Contract §A/§B satisfied; `app-exchange` + app-aware refresh built with no device binding and no migration; suite green minus the known `QuestionBank` baseline; deep-link doc keys aligned. Hand to `IMPLEMENTATION-PLAN-native-app-a0-wiring.md`.

---
## Kickoff prompt (paste into a fresh Claude session at the repo root)
```
You are implementing the BACKEND stream of Native App phase A0 (device-agnostic app sign-in + app-aware refresh) for Salah Bahzad. Edit backend/** ONLY, plus the two doc-only deep-link-key edits in docs/05 and docs/08.

Read first, in order:
1. docs/contracts/native-app-playback.md (§A app-exchange, §B refresh, §E deep-link keys — the frozen authority)
2. docs/IMPLEMENTATION-PLAN-native-app-a0-backend.md (this stream)
3. backend/CLAUDE.md (Authentication & authorization, Audit log, Multi-tenancy, Security checklist)
4. backend/src/SalahBahazad.Api/Endpoints/AuthEndpoints.cs + Features/Auth/.../ExchangeStudentFirebaseTokenHandler.cs + RefreshTokenHandler.cs + JwtTokenService.cs (confirm what EXISTS; do NOT rebuild)

Build: a new POST /api/auth/student/app-exchange (AllowAnonymous + rate-limit "auth") that mirrors the existing student exchange's Firebase-verify + student/tenant resolution + status gate but performs NO device binding and issues JWTs with NO device_id (pass deviceId:null to JwtTokenService); return StudentAuthResponse with BoundDevice=null; audit StudentSignedIn/StudentSignInRejected only. Make RefreshTokenHandler app-aware: skip the device re-check when the token has no device_id. Extract the shared sign-in path so the two handlers can't drift. Align the deep-link example keys in docs/05 + docs/08 to videoId/sessionId/handoff. No migration.

Tests (dotnet test -c Release): the integration cases in §7, especially the device-agnostic headline (a portal-bound student still signs into the app from another device → 200) and app-refresh without a StudentDevice → 200.

Green gate: `dotnet test -c Release` (known baseline: the one QuestionBank image test). Report the result.
```
