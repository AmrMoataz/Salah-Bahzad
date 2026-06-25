# FROZEN CONTRACT — Native App · Auth + Secure Video Playback (`/api/me/videos/*`, `/api/auth/*`, `/api/app/*`)

> Status: **Frozen** · Created 2026-06-23 · Updated 2026-06-24 · Slice: the **Native App** (Flutter Secure Player) in `docs/IMPLEMENTATION-PLAN-native-app.md` (phases A0–A4). **Design anchor:** the prototype's **`SIGN IN`**, **`SPLASH / DEEP-LINK HANDLER`**, and **`PLAYER`** banners (`.claude/Salah Bahzad App/Secure Video App (standalone).html`). **Behaviour authority:** `FR-APP-AUTH-001..004`, `FR-APP-LNK-001..004`, `FR-APP-DEV-001..002`, `FR-APP-VID-001..005`, `FR-APP-UPD-001`, `NFR-APP-SEC-001..006`, `NFR-APP-UPD-002`.
>
> Satisfies the app↔backend surface. **Most of this already EXISTS and is reused as-is** (S0 auth + 5C video gate); **four touches are NEW** (marked **NEW**): a **device-agnostic** `app-exchange`, `Student.Serial` on the profile read, a min-version read + `X-App-Version` check at `redeem`, and the canonical deep-link param keys. **No new aggregate. One migration total** (`Serial`). The app stream + wiring cite this file field-for-field. **Change this file first if anything moves.**

## 0. Ground rules
- **Authenticated student surface.** Every `/api/me/*` route is `RequireStudent`: anonymous → `401`, non-Student (staff) → `403`, student id from the JWT `nameid` (no IDOR). `/api/auth/*` and `/api/app/*` are `AllowAnonymous`.
- **Device-agnostic (user decision 2026-06-24).** The app authenticates via **`app-exchange`** and is **not** subject to device binding — a student may sign in on any machine; app JWTs carry **no `device_id`**. Anti-sharing is the watermark (serial + name) + the view cap, not device identity. One-device binding stays a **portal** concern.
- **Tenant isolation is automatic.** EF global `TenantId` filter; handlers never add a per-call `Where(TenantId)`. The app **never** trusts the deep-link's `videoId`/`sessionId` for authorization — only the server gate.
- **The token is never in a URL.** The deep link carries only a one-time **handoff code**; the bearer/refresh tokens live in the OS keystore (`NFR-APP-SEC-004`). Signed segment URLs + the AES key are **memory-only**, never persisted (`NFR-APP-SEC-005`).
- **Enums & time over the wire.** `Status` is a string name (`Active`/`Pending`/`Rejected`/`Inactive`); all `…AtUtc` are ISO-8601 offsets.
- **Reads are not audited.** `/api/me/profile`, `.../hls.key`, `/api/app/version-status` write no audit. Only the **play gate** audits (`§I`).

---

## A. App sign-in exchange — `POST /api/auth/student/app-exchange` (**NEW** · `AllowAnonymous` + rate-limit `auth`) · `200 StudentAuthResponse`
Firebase ID token → **device-agnostic** platform session. Only `Active` students pass; status gates return `403 {reason}` (`§H`). **No device binding**: no `sb_device` cookie, no `X-Device-Token`, no `X-Device-Fingerprint`; the issued JWTs carry **no `device_id`**. *(The existing browser `POST /api/auth/student/exchange` stays portal-only and device-bound — the app does not use it.)*

**Request** — `StudentExchangeRequest`:
```jsonc
{ "firebaseIdToken": "string" }   // Firebase ID token from email/pw or Google sign-in
```
### A.1 Result — `StudentAuthResponse`
```jsonc
{
  "accessToken": "string",               // JWT: nameid, tenant_id, role=Student, token_type=access  (NO device_id)
  "refreshToken": "string",              // JWT token_type=refresh (NO device_id)
  "accessTokenExpiresAt": "2026-…Z",     // ~15 min
  "refreshTokenExpiresAt": "2026-…Z",    // ~7 days
  "student": {
    "id": "guid",
    "fullName": "string",
    "status": "Active",                  // enum name
    "boundDevice": null                  // always null for the app (no binding)
  }
}
```
> JWT facts the app must accept unchanged (verbatim from `appsettings.json`): `iss=salah-bahzad-api`, `aud=salah-bahzad-admin` — **note the `bahzad` spelling** (no second "a"), which differs from the deep-link scheme `salah-bahazad://`. HS256. The app should read these from config, not hardcode.

### A.2 Error modes — ProblemDetails (`reason` extension)
| Status | reason | When |
|---|---|---|
| `403` | `account_pending` | student not yet approved |
| `403` | `account_rejected` | rejected (detail = rejection reason) |
| `403` | `account_inactive` | deactivated |
| `401` | — | Firebase UID maps to no student |
| `429` | — | `auth` rate-limit (one global bucket) |
> `device_not_recognized` does **not** apply to the app (no binding).

---

## B. Token refresh — `POST /api/auth/refresh` (EXISTS, **made app-aware**) · `AllowAnonymous` + rate-limit `auth` · `200 StudentAuthResponse`
Role-aware; returns `StudentAuthResponse` for a student refresh token. Re-checks `Active`. **NEW:** for an **app token (no `device_id`)** it **skips** the device re-check (the existing portal path still requires `device_id` to map to an active `StudentDevice` → else `401`).
```jsonc
{ "refreshToken": "string" }   // RefreshTokenRequest
```

## C. Self-profile — `GET /api/me/profile` (EXISTS, **+NEW `serial`**) · `RequireStudent` · `200 StudentProfileDto`
The watermark identity source.
```jsonc
{
  "id": "guid",
  "serial": "STU-7K2M9X",                // NEW — randomly-generated unique serial, STU- + 6 Crockford chars (FR-APP-VID-003)
  "fullName": "string",                  // watermark renders: "{serial} · {fullName}"
  "phoneNumber": "+9647701800000",
  "parentPhonePrimary": "string", "parentPhoneSecondary": "string|null",
  "schoolName": "string",
  "gradeId": "guid", "gradeName": "string|null",
  "cityId": "guid", "cityName": "string|null",
  "regionId": "guid", "regionName": "string|null",
  "status": "Active",
  "boundDevice": null                    // null for app sessions
}
```
> **NEW:** `Serial` is added to `Student` — **randomly generated**, **unique**, human-readable (`STU-XXXXXX`, Crockford base32; ambiguous chars excluded; uniqueness-checked). Minted at registration (`Student.Register`) and **backfilled** for existing students — **the one migration**. The **watermark = `serial + fullName`** (not phone).

---

## D. Secure-video gate (EXISTS · all `RequireStudent`) — the three routes the app drives in order

| # | Method & path | Auth | Returns | Notes |
|---|---|---|---|---|
| D1 | `POST /api/me/videos/{videoId:guid}/playback` | `RequireStudent` | `200 PlaybackHandoffDto` | **Spends one view**, audits `VideoPlaybackStarted`, mints a ~60 s single-use handoff. No body. *(Normally the **portal** calls this and deep-links the result; the app may also call it directly.)* |
| D2 | `POST /api/me/videos/playback/redeem` | `RequireStudent` | `200 PlaybackManifestDto` | Body `{ "handoffCode": "…" }`. Enforces `handoff.StudentId == caller`. **NEW:** also enforces min-version (`§F`). No view spent. |
| D3 | `GET /api/me/videos/{videoId:guid}/hls.key` | `RequireStudent` | `200` `application/octet-stream` | 16 raw AES-128 key bytes. Re-checks Active/not-expired/quiz-passed. No decrement, no audit. |

### D.1 Result shapes
```jsonc
// D1 — PlaybackHandoffDto
{ "handoffCode": "48-hex", "expiresAtUtc": "2026-…Z" }   // ~60 s TTL, single-use (Redis GETDEL)

// D2 — PlaybackManifestDto
{ "manifestContent": "#EXTM3U…",      // rewritten .m3u8: signed R2 segment URLs (~120 s TTL) + absolute key URL
  "keyUrl": "https://…/api/me/videos/{id}/hls.key",
  "expiresAtUtc": "2026-…Z",
  "accessRemaining": 2,                  // NEW — views left AFTER this Play (the "N", FR-APP-VID-004)
  "accessAllowed": 3,                    // NEW — total views granted for this enrollment+video (the "M")
  "videoTitle": "Quadratic equations",   // NEW — the SessionVideo's own title (player top-bar title)
  "watermark": "STU-7K2M9X · Layla Ahmed" }  // NEW — bound student's "{serial} · {fullName}" overlay (FR-APP-VID-003; never the phone)
```

### D.2 Error modes (frozen — verbatim reasons; the app renders `detail` inline)
| Status | reason | When |
|---|---|---|
| `409` | `not_ready` | video still transcoding |
| `403` | `not_enrolled` | no active enrollment |
| `403` | `enrollment_expired` | enrollment lapsed |
| `403` | `quiz_required` | gating quiz not passed |
| `403` | `no_views_remaining` | per-video cap reached |
| `404` | — | video not found / not the caller's tenant (IDOR-safe) |
| `410` | `handoff_expired` | redeem with a missing/expired/foreign handoff |
| `426` | `outdated_app` | **NEW** — redeem from a build below the min version (`§F`) |

### D.3 Server behaviour (reference — already implemented, do not rebuild)
Gate order (`StartVideoPlaybackHandler`): tenant/video `404` → `not_ready 409` → `not_enrolled 403` → `enrollment_expired 403` → `quiz_required 403` → `no_views_remaining 403` → decrement view → `SaveChanges` (in a transaction) → mint handoff **after commit**. Redeem signs each segment + rewrites the key URI. The app **must not** re-Play on retry within TTL — reuse the same handoff (no double-decrement).

---

## E. Deep-link URI (EXISTS today; **canonical keys frozen here**)
The portal builds, on Play (`session-detail.component.ts:489`):
```
salah-bahazad://stream?videoId={videoId}&sessionId={sessionId}&handoff={handoffCode}
```
- **Canonical keys = `videoId`, `sessionId`, `handoff`** (what runs today). `sessionId`/`videoId` are **advisory routing hints only**; `handoff` is the credential. The app's parser keys on these. *(Docs `05`/`08` use `video`/`session` — to be updated to match; the app accepts the live keys.)*
- Scheme `salah-bahazad://` (the **`bahazad`** spelling — distinct from the JWT's `bahzad`) on Windows/macOS; **Universal Links** (iOS) / **App Links** (Android) resolve the same. App not installed → the portal shows the install prompt (its 1500 ms blur timer + store modal).

---

## F. Min app version (**NEW**)
### F.1 Launch check — `GET /api/app/version-status?platform={p}&version={v}` (`AllowAnonymous`) · `200`
```jsonc
{ "status": "ok",                  // "ok" | "update_available" | "update_required"
  "minVersion": "1.4.0",
  "latestVersion": "1.6.0",
  "storeUrl": "https://…" }        // the store/download URL for {platform}
```
- `platform` ∈ `android | ios | windows | macos`. The app calls this on startup; `update_required` → blocking **update-required** screen; `update_available` → soft nudge.

### F.2 Hard enforcement at `redeem`
- The app sends `X-App-Version: <semver>` + `X-Platform: <platform>` on `POST /api/me/videos/playback/redeem`. If `version < min[platform]` (or the header is missing/garbage) → **`426 Upgrade Required`**, `reason: outdated_app`, `detail` carrying the store URL → the app shows the update-required state.
- **Why `redeem`, not the gate:** the gate (`D1`) is also called by the **browser portal** (to mint the handoff); `redeem` is **app-only** (the browser never plays), so versioning it retires stale apps without breaking the portal.
- The floor is **config-driven**, per platform, hot-reloadable via `IOptionsMonitor<AppVersionsOptions>` (`AppVersions:{platform}:{Min,Latest,StoreUrl}`). May graduate to an admin-managed DB setting later (out of scope v1).

---

## G. Client-supplied headers (the app sends on authenticated calls)
- `Authorization: Bearer <accessToken>` (all `/api/me/*`).
- `X-App-Version: <semver>` + `X-Platform: <android|ios|windows|macos>` (at least on `redeem`; `§F`).
- **No device headers** (no `X-Device-Token` / `X-Device-Fingerprint`) — the app is device-agnostic.

## H. Error → app state map (`FR-APP-ERR-001`; copy verbatim from the prototype `FAILURE / RETRY` banner; *update-required* is NEW, not in the prototype)
| App state | Trigger (status/reason) | Title (verbatim) | Primary action |
|---|---|---|---|
| unauthorized | `401` (expired/blank session) | "Your session expired" | Sign in again |
| forbidden | `403 not_enrolled` | "You're not enrolled in this" | Open the portal |
| maxviews | `403 no_views_remaining` | "No views left for this lesson" | Back to portal |
| expired | `403 enrollment_expired` | "Your enrollment expired" | Open the portal |
| notfound | `404` / `410 handoff_expired` | "We can't find this lesson" | Back to portal |
| offline | network failure | "You're offline" | Try again |
| server | `5xx` | "Something went wrong" | Try again |
| update-required | `426 outdated_app` | "Update required" | Update the app |

## I. Audit (`FR-PLAT-AUD-002` — reference)
The play gate writes **`VideoPlaybackStarted`** (one row, `ActorType=Student`). `app-exchange` writes `StudentSignedIn` (`ActorType=Student`; **no** `StudentDeviceBound` — no binding) or `StudentSignInRejected` on a blocked attempt. Redeem, key, profile, refresh, and version-status write **no** audit. The app adds none.

## J. Frozen vs. stream-owned
- **Frozen (this file):** the routes, DTO shapes, reason codes, the canonical deep-link keys, the `app-exchange`/`serial`/`min-version` additions, the error→state map, the device-agnostic stance.
- **Backend owns:** the `app-exchange` endpoint + the app-aware `refresh` branch, the `Serial` field + migration + backfill + registration mint, the `version-status` endpoint + `redeem` version check, integration tests (`dotnet test -c Release`).
- **App owns:** the Flutter UI, Firebase calls, the keystore + deep-link plumbing, the AES key-loader, the watermark (serial + name) + capture shims, widget/golden tests (`flutter test`).
- **Wiring owns:** proving the whole flow live on the Aspire stack — `app-exchange` (any machine) → deep-link → redeem → key → play → view-decrement, every reason in `§H` (incl. `426 outdated_app`), tenant/IDOR `404`s, and the per-OS capture matrix.
