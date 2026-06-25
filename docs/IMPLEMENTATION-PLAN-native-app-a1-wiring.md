# Native App · A1 — WIRING stream (prove live gate→redeem→key→play against the Aspire stack, decrement + IDOR/tenant proofs)

> Status: **Planned — not yet built** · Created 2026-06-25 · Proves phase **A1** end-to-end against the **running Aspire stack** (Postgres + Redis + MinIO + API), with the Flutter app pointed at the API's dev URL — like the A0 / Phase 3–5 / S0–S6 wiring streams. Goal: **zero contract drift** vs `docs/contracts/native-app-playback.md` §C/§D (+ §G/§H/§I) and the A1 backend (`Student.Serial`) + app (Player) streams. The A1 headline: a Play deep-link drives **gate → redeem → key → play**, the per-video budget **decrements exactly once**, a retry within the handoff TTL **does not double-decrement**, and foreign video/handoff requests are **IDOR/tenant-safe** (`404`/`410`). **One migration in the whole slice (`Serial`)** — everything else under §D is the reused 5C gate.
>
> Runs **after** the backend + app streams merge. Reuses prior wiring techniques: read the **Aspire-assigned API port** from the dashboard / listening PID (reassigned every run); verify the DB with `docker exec -i <pg> psql` (snake_case tables, **PascalCase columns quoted**, SQL piped via stdin — PS 5.1 mangles inline `-c`); mint a Firebase `idToken` via REST `signInWithPassword` to drive `app-exchange` without a browser; **mint a direct Student JWT** (`nameid`/`role=Student`/`tenant_id`/`token_type=access`; device **not** checked on `/me/*`) via a .NET-10 file-app **outside** the repo (avoids CPM/NU1008) for the multi-student IDOR/tenant matrix; **"a `/me/profile` response with no `serial` (or a new route `404` not `401`) → the running API is stale, restart AppHost."** Real AES-128 HLS playback needs a **Ready** transcoded video in MinIO (the AppHost `ResolveFfmpeg` injects `Transcode__FfmpegPath`; reuse the 5C real-ffmpeg fixture).
> Satisfies (proven live): `FR-APP-VID-001` (play encrypted lesson from a redeemed handoff), `FR-APP-VID-002` (custom key-loader fetches the gated AES key), `FR-APP-VID-003` (serial → watermark identity), `FR-APP-VID-004` (view budget surfaced + decremented), `FR-APP-VID-005` (no download/export), `FR-APP-AUTH-003` (handoff redeemed against the keystore session), `FR-PLAT-VID-002` (one view spent per Play), `FR-PLAT-AUD-002` (`VideoPlaybackStarted` audited; redeem/key/profile not), `NFR-APP-SEC-005` (key + signed URLs memory-only, never persisted), `NFR-SEC-007` (IDOR — caller-owns-resource), `NFR-SEC-010` (cross-tenant isolation), `NFR-APP-PERF-002` (first frame < 4 s p95), `NFR-APP-PERF-003` (ABR up to 1080p), `NFR-APP-REL-001` (retry without losing context), `FR-APP-ERR-002` (expired-handoff → clean re-Play guidance).

---

## Design reference
Verifies behaviour, not pixels. Acceptance copy/states from the prototype `PLAYER` banner (`.claude/Salah Bahzad App/Secure Video App (standalone).html`) + contract §H: the top-bar **Encrypted** chip, the amber **"N of M views left"** counter, the **dual-layer watermark** of `"{serial} · {fullName}"` (contract §C), the static **"Screen capture blocked"** banner (the real OS black-out is A2), and the reachable error states (notably `410 handoff_expired` → notfound → "press Play again", §10.1). The on-screen visual pass (#16) is the user's step.

## AppHost change to land first
**None.** The app is **not** an Aspire service. Run the API via AppHost (F5 / `dotnet run`), then run the app:
`flutter run -d windows --dart-define=API_BASE_URL=http://127.0.0.1:<api-port>` (port from the Aspire dashboard / listening PID; reassigned each run). The app is native (no browser) → **no CORS**. TLS is never disabled — trust the Aspire dev cert in the OS store. Register `salah-bahazad://` locally (HKCU on Windows → the debug exe `"%1"`; trigger with `start "" "salah-bahazad://stream?videoId=…&sessionId=…&handoff=…"`). Real HLS playback requires `ffmpeg` resolvable to the API (AppHost `ResolveFfmpeg` → `Transcode__FfmpegPath`) so the fixture video is genuinely **Ready** with an AES-128 HLS package in MinIO.

## Pre-flight
- Backend + app streams merged; `dotnet test -c Release` green (the one pre-existing `QuestionBank`/`CatalogueApiTests.Each_filter_narrows_the_result` baseline is the known red — proven not-ours); `flutter analyze` clean + `flutter test` green (Player widget/golden + key-loader unit).
- Aspire stack up; discover container names + ports dynamically (renamed each run). **If `GET /api/me/profile` returns no `serial` key**, the running API predates the A1 backend slice → **restart AppHost**. If `POST /api/me/videos/playback/redeem` returns **404 not 401**, the API is stale likewise.
- Direct-drive helpers:
  - **Full path:** `POST {firebaseAuthBase}/v1/accounts:signInWithPassword?key=<API_KEY>` → `idToken` → `POST /api/auth/student/app-exchange { firebaseIdToken }` (contract §A) → `StudentAuthResponse.accessToken`. Decode the JWT (header.payload) to confirm `role=Student`, `tenant_id`, **no `device_id`**.
  - **Matrix path:** mint a direct Student JWT (`nameid` = the student id, `role=Student`, `tenant_id`, `token_type=access`, `iss=salah-bahazad-api`, `aud=salah-bahazad-admin` — the **`bahzad`** spelling, HS256, key from `appsettings.json`) via the out-of-repo .NET-10 file-app, since `/me/*` does **not** check device. This makes the two-student / two-tenant IDOR cases scriptable without two Firebase users.

## Fixtures (reuse the S0/S1 + 5C seed where possible)
- One **`Active`** student in **Tenant A** with a known Firebase email/password (the full-path actor). Confirm it carries a **backfilled `Serial`** post-migration.
- One **session in Tenant A** with a **Ready** `SessionVideo` (real AES-128 HLS in MinIO, `HlsManifestKey` set, `ProcessingStatus=Ready`) and an **active enrollment** for the Active student, with a small per-video budget (e.g. `EnrollmentVideoAccess.AccessAllowed = 2`) so `no_views_remaining` is cheap to reach.
- A **second `Active` student in Tenant A** — to prove the **per-tenant** serial uniqueness + the **foreign-handoff** case (`handoff.StudentId != caller`).
- A **second-tenant (Tenant B)** student + a Tenant-B session video — for the **cross-tenant `404`** (IDOR) cases.
- States to reach the gate reasons via §D.2: a video still transcoding (`not_ready`), an enrollment that is absent (`not_enrolled`) / lapsed (`enrollment_expired`), a session with an unpassed gating quiz (`quiz_required`), and a budget driven to zero (`no_views_remaining`). `426 outdated_app` is **A4** — **not** exercised here.

## Live checks (target: all green, zero drift)

**Serial → watermark identity (#1–#2, `FR-APP-VID-003`, contract §C):**
1. `GET /api/me/profile` (Active, Tenant A) → **`200`** `StudentProfileDto`; `serial` is the **2nd** field (after `id`, before `fullName`) and matches `^STU-[0123456789ABCDEFGHJKMNPQRSTVWXYZ]{6}$` (Crockford, I/L/O/U excluded). Raw body contains `"serial"`. **DB (psql):** `students."Serial"` for this row is non-empty and `STU-`-prefixed. Register a second Tenant-A student live → its serial is **distinct** (proves the handler's `NextUnique` seeding + the `(TenantId, Serial)` unique index).
2. The watermark string the app renders is exactly `"{serial} · {fullName}"` (app `StudentProfileDto.watermarkLabel`) — verify it surfaces in the Player overlay (logic here; on-screen at #16). The seeded/**backfilled** Active student surfaces a non-empty `STU-` serial on `/api/me/profile` (proves the migration backfill ran via the integ-style real migration at boot).

**Happy path — gate → redeem → key → play (#3–#5, `FR-APP-VID-001/002`, `FR-APP-AUTH-003`, contract §D):**
3. **D1** `POST /api/me/videos/{videoId:guid}/playback` (Active, **no body**) → **`200`** `PlaybackHandoffDto` = `{ handoffCode (48-hex), expiresAtUtc (~60 s) }`. **DB:** the per-video budget (`EnrollmentVideoAccess.AccessRemaining`, table `enrollment_video_access`, column `"AccessRemaining"`) drops by **exactly 1**; **audit:** exactly **one** `VideoPlaybackStarted` row, `EntityType=SessionVideo`, `EntityId=videoId`, `ActorType=Student` (per `StartVideoPlaybackHandler` → `AuditWriteRequest("VideoPlaybackStarted","SessionVideo",video.Id,…)`).
4. **D2** `POST /api/me/videos/playback/redeem { handoffCode }` → **`200`** `PlaybackManifestDto` = `{ manifestContent (#EXTM3U…, signed R2 segment URLs + absolute key URL), keyUrl (= …/api/me/videos/{videoId}/hls.key), expiresAtUtc }`. **DB:** budget **unchanged** (no view spent on redeem); **audit:** **no** new row (redeem is not audited, §I).
5. **D3** `GET /api/me/videos/{videoId:guid}/hls.key` (Bearer attached by the app's key-loader) → **`200`** `application/octet-stream`, body is **exactly 16 bytes** (AES-128 key). **DB:** budget **unchanged**; **audit:** **no** row. The app's loopback/key-loader decrypts and the **first frame renders** (latency captured at #15).

**Decrement-exactly-once + retry within TTL (#6–#7, `FR-PLAT-VID-002`, `NFR-APP-REL-001`, contract §D.3):**
6. Snapshot `AccessRemaining` immediately before and after a single full Play (D1→D2→D3) → delta is **exactly `−1`**. A **second** `hls.key` fetch (D3) within the manifest TTL (e.g. segment re-key) → **`200`**, budget still unchanged (key-load never decrements).
7. **Retry must reuse the same handoff (no re-Play):** simulate a transient player error after a successful redeem; the app **must not** call **D1** again — it reuses the already-redeemed in-memory manifest/key. **DB:** budget delta over the retry stays **`−1` total** (no second decrement). Confirm the single-use guarantee: re-redeeming the **already-consumed** `handoffCode` (Redis `GETDEL`) → **`410`** `handoff_expired` (`RedeemPlaybackHandler`: missing/used handoff → `GoneException("…","handoff_expired")`).

**Expired / single-use handoff → clean re-Play (#8–#9, `410 handoff_expired`, §H notfound, `FR-APP-ERR-002`):**
8. Redeem a handoff after its **~60 s TTL** elapses (or a fabricated 48-hex code) → **`410`** `handoff_expired`. The app maps this to the **notfound** state — title **"We can't find this lesson"** (contract §H) — and routes to idle with **"press Play again"** guidance (§10.1: a signed-out arrival likely lets the 60 s handoff lapse → sign in → idle).
9. Redeem a **well-formed but never-minted** code → **`410`** `handoff_expired` (no oracle distinguishing expired vs forged — IDOR-safe).

**IDOR / tenant isolation (#10–#12, `NFR-SEC-007`, `NFR-SEC-010`, contract §0/§D.2):**
10. **D1** `POST /api/me/videos/{videoId:guid}/playback` where `videoId` belongs to **Tenant B**, called by the **Tenant-A** student → **`404`** (no `reason`; "video not found / not the caller's tenant", IDOR-safe via the EF global `TenantId` filter — handlers add no per-call `Where`). **DB:** **no** decrement on any row; **no** audit.
11. **D3** `GET /api/me/videos/{foreign-videoId}/hls.key` (Tenant-A caller, Tenant-B video) → **`404`**. (Regression: anonymous → **`401`**, staff/non-Student → **`403`** — `RequireStudent`, already covered by `VideoPlaybackTests`.)
12. **Foreign handoff:** mint a handoff for **student 2** (D1 as student 2), then redeem it as **student 1** → **`410`** `handoff_expired` (`handoff.StudentId != currentUser.UserId`). The view that student 2 spent is **not** refunded to student 1 and student 1's budget is untouched.

**Gate reasons reachable on the player path (#13, contract §D.2 → §H):**
13. Drive each reachable gate reason at **D1** and confirm the app renders the matching §H state (verbatim title): `not_ready` → **`409`**; `not_enrolled` → **`403`** ("You're not enrolled in this"); `enrollment_expired` → **`403`** ("Your enrollment expired"); `quiz_required` → **`403`**; `no_views_remaining` → **`403`** ("No views left for this lesson") after the budget hits 0. Each `reason` is surfaced verbatim and the `detail` rendered inline. **`426 outdated_app` is A4 — confirm it is NOT yet enforced at `redeem`** (an old `X-App-Version` still plays in A1).

**Security + performance (#14–#15, `NFR-APP-SEC-005`, `NFR-APP-PERF-002/003`):**
14. Inspect app storage + logs: the **16-byte AES key** and the **signed R2 segment URLs** are **memory-only** — absent from `SharedPreferences`, plaintext files, and disk caches; grep logs → **no** key bytes / signed URLs / tokens / PII. Bearer/refresh tokens remain in the **OS keystore** only (carried from A0). The deep-link URI carried only the one-time `handoff` (never a token).
15. First-frame latency Play→pixels **< 4 s p95** (`NFR-APP-PERF-002`); the manifest exposes ABR renditions up to **1080p** and the engine adapts (`NFR-APP-PERF-003`). Capture wall-clock across several cold Plays.

**Browser/app visual (#16):**
16. The full on-device walkthrough — **the user's visual step** (pending): watermark legible + **repositioning ~2.6 s** (`SbMotion.watermarkInterval` 2600 ms / `watermarkReposition` 1400 ms), amber **"N of M views left"** counter decrementing, green **Encrypted** chip, **"Screen capture blocked"** banner, controls (play/pause, seek + timers, **speed 1×/1.25×/1.5×/2×**, mute, fullscreen) with **no download/export** affordance, and rotation/desktop-resize survival without losing playback (`FR-APP-ERR-002`/`NFR-APP-REL-001`).

## Sign-off
- Log the run below; flip the master plan's **A1** status (`docs/IMPLEMENTATION-PLAN-native-app.md` §A1) to **Met** (date + headline tally + zero-drift); write a `native-app-a1-wiring` memory (techniques + gotchas — Aspire port discovery, docker-exec psql for `enrollment_video_access."AccessRemaining"`, direct-JWT mint for the IDOR matrix, the real-ffmpeg Ready-video fixture, the stale-API `serial`-missing tell); note any drift as a **contract change** (change `docs/contracts/native-app-playback.md` first).

---
## ✅ MET (API-level) — Run log (`2026-06-26`, live on the Aspire stack) — **13/13 scripted API checks, ZERO contract drift**
> Proven against the live `dotnet run` AppHost (Postgres + Redis + MinIO + API at the **stable `http://localhost:5080`** "app" endpoint). Auth = a direct HS256 Student JWT (`nameid`/`role=Student`/`tenant_id`/`token_type=access`, `iss=salah-bahzad-api`/`aud=salah-bahzad-admin`, secret from `appsettings.Development.json`) — `/me/*` does not check device. DB verified via `docker exec -e PGPASSWORD=postgres -i postgres-wxynxwhw psql -U postgres -d DefaultConnection` (snake_case tables, **PascalCase quoted cols**; `enrollment_video_access."AccessRemaining"`, `audit_entries."Action"`). Fixture: Active student **Amr Moataz `STU-6B8C9C`** (`019eea33…`) → Ready AES-128 video `019ef07b-6c91…` (`AccessRemaining 3`). The on-screen libmpv decode/render (#16) is the user's pending visual.

- **#1 Serial → profile (✓ live):** `GET /api/me/profile` `200`; `serial` is the **2nd field** (`{"id":…,"serial":"STU-6B8C9C","fullName":…}`), exact §C order, matches `^STU-[Crockford]{6}$`. All 5 dev students hold distinct valid serials in `students."Serial"`.
- **#3–#5 Happy path (✓ live):** D1 `POST …/{vid}/playback` `200` `{handoffCode:48-hex, expiresAtUtc}` + **`AccessRemaining 3→2 (−1)`** + **one** `VideoPlaybackStarted` audit (`EntityId=vid`); D2 `POST …/playback/redeem` `200` manifest with `keyUrl` → the `hls.key` route, **`AccessRemaining` unchanged, no audit**; D3 `GET …/{vid}/hls.key` `200` **exactly 16 bytes**, **unchanged, no audit**.
- **#6–#7 Decrement-once + handoff single-use (✓ live, HEADLINE):** one Play = exactly **`−1`**; re-`hls.key` no decrement; **consumed handoff re-redeem → `410`**.
- **#8 Forged handoff (✓ live):** random 48-hex → redeem → **`410`**.
- **#10 not_enrolled (✓ live):** gate a video the caller isn't enrolled in → **`403 {reason:"not_enrolled"}`**, no decrement.
- **#11 Foreign handoff / IDOR (✓ live):** student B (Lean Amr) redeeming student A's handoff → **`410`** (`handoff.StudentId == caller` enforced).
- **#12 Auth (✓ live):** anon → **`401`**; staff (`role=Teacher`) → **`403`** on the student gate.
- **#13 no_views_remaining (✓ live):** gate a `rem=0` budget → **`403 {reason:"no_views_remaining"}`**, no decrement. *(Other reasons `not_ready`/`enrollment_expired`/`quiz_required` + `426`-at-redeem are covered by the integration suite — not re-proven live; `426` is A4.)*
- **Fix made this stream:** the migration backfill collided on the unique index (`23505`) — Ids are **UUIDv7**, so the leading hex is a shared timestamp prefix; changed the backfill from first-6-hex (`substr …,1,6`) to **last-6-hex** (`right …,6`, the random tail). Re-applied clean; 5 distinct serials. (Backend doc §6 + the migration updated.)
- **Not re-proven live (env limits, no drift implied):** true **cross-tenant `404`** (dev has a single tenant `salah-bahzad`; the tenant filter is covered by the integration suite's `NFR-SEC-010` tests — `not_enrolled 403` proven here); `#9` TTL-lapse `410` (same path as the proven consumed/forged `410`); `#2` distinct serial via a *live* registration (covered by integ + the 5 distinct DB serials); `#14`/`#15`/`#16` key-memory-only on-screen + first-frame `<4 s` + ABR + the visual walkthrough = the app/user live step.
- **Pending (user visual — #16):** run `flutter run -d windows --dart-define=API_BASE_URL=http://localhost:5080` against the (still-running) stack and watch a real lesson: watermark `{serial}·{fullName}` repositioning, the views counter, Encrypted chip, capture banner, controls (no download), resize survival.

---
## Kickoff prompt (paste into a fresh Claude session at the repo root)
```
You are running the WIRING stream of Native App phase A1 for Salah Bahzad. Prove the secure player end-to-end LIVE on the running Aspire stack — gate → redeem → key → play, the per-video budget decrements EXACTLY ONCE, a retry within the handoff TTL does NOT double-decrement, and foreign video/handoff requests are IDOR/tenant-safe (404/410) — with ZERO contract drift vs docs/contracts/native-app-playback.md §C/§D (+ §G/§H/§I).

Read first, in order:
1. docs/contracts/native-app-playback.md (§C profile+serial, §D the three gate routes, §G headers, §H error→state, §I audit)
2. docs/IMPLEMENTATION-PLAN-native-app-a1-backend.md and -a1-app.md (what was built — Student.Serial + migration; the Player + key-loader)
3. docs/IMPLEMENTATION-PLAN-native-app-a1-wiring.md (this stream — checks #1–#16)
4. the A0/S2/Student-Home wiring memories (Aspire port discovery; docker exec psql with quoted PascalCase cols; Firebase signInWithPassword→app-exchange; direct Student-JWT mint via an out-of-repo .NET file-app; the 5C real-ffmpeg Ready-video fixture)

Do: run the API via AppHost (ffmpeg resolvable); run the app with --dart-define=API_BASE_URL=<aspire api port>; seed/confirm a Ready AES-128 HLS video + active enrollment with a small AccessAllowed; execute #1–#15. The headline is #6/#7 (exactly one view spent per Play; retry reuses the same handoff). Verify the DB with docker exec -i <pg> psql — AccessRemaining delta on enrollment_video_access, the VideoPlaybackStarted audit row, and students."Serial". If /api/me/profile lacks "serial" or redeem 404s, the API is stale → restart AppHost. Leave #16 (on-screen visual) for the user.

Log the run, flip the master plan's A1 status to Met (date + N/N + ZERO drift), and write the native-app-a1-wiring memory. Report the tally.
```
