# Phase 5C — Wiring stream (prove the secure video gate live)

> Proves `docs/contracts/phase5c-video-gate.md` end-to-end on the **running Aspire stack** (Postgres + MinIO + Redis +
> API), exactly like the Phase 3/4/5A/5B wiring streams. **Backend-only** — there is no admin screen and the dev proxy
> does not forward a player; every check is driven by a **direct student JWT** (the Phase-4 redeem technique). Goal:
> **zero contract drift**, every `reason` exercised, isolation + default-deny held.

## Pre-flight
- ffmpeg on the host `PATH` (the API's Hangfire worker shells out to it — §F). Confirm `ffmpeg -version`.
- Backend stream merged; `dotnet test -c Release` green (minus the known baseline image test); migration **applied** to
  the Aspire Postgres (`dotnet ef database update -c <DbContext>` / Infrastructure-as-startup, per the backend build note).
- Start the stack via AppHost (F5). Note the API port Aspire assigns (it reassigns — read it from the dashboard, don't
  assume). Reuse the direct-JWT + `docker exec … psql` (PascalCase quoted columns) helpers from the 5A/5B wiring logs.

## Fixtures (reuse existing seed where possible)
- A published session **B** with ≥1 video and a small `AccessCount` (e.g. 2) so exhaustion is quick to hit.
- A second session **A** as B's prerequisite **with** a quiz, to exercise the `quiz_required` gate (reuse the 5B-2 chain).
- One enrolled student (`EnrollmentVideoAccess` provisioned), one student who has **not** passed A's quiz, and a
  second-tenant video for the isolation check.

## Live checks (target: all green, zero drift)
**Transcode (real ffmpeg):**
1. Upload a source video (admin) → poll the video → `ProcessingStatus` goes `Pending`→`Processing`→`Ready`; on Ready,
   `HlsManifestKey` **and** `HlsKeyObjectKey` are set.
2. `docker exec` into MinIO (or the console :9001) → encrypted `.ts` segments + the `.m3u8` + the key object exist under
   the deterministic prefix in `sb-dev-private`. The DB row holds **keys only** (no bytes/URLs) — `FR-PLAT-VID-007`.

**The gate (#1):**
3. Pass-quiz student → `POST /api/me/videos/{id}/playback` → `200` `PlaybackHandoffDto` (code + expiry, **no URL**).
   `EnrollmentVideoAccess.AccessRemaining` dropped by 1 (psql). A `VideoPlaybackStarted` audit row exists with the
   **Student** actor (`FR-PLAT-VID-002`).
4. Reasons (`FR-PLAT-VID-006`): not-enrolled student → `403 not_enrolled`; the unpassed-quiz student → `403 quiz_required`;
   spend the budget to 0 then retry → `403 no_views_remaining`; a `Pending`/`Processing` video → `409 not_ready`.

**Redeem (#2) + key (#3):**
5. `POST /api/me/videos/playback/redeem` with the #3-step code → `200` `PlaybackManifestDto`; `manifestContent` is valid
   `.m3u8` whose segment URIs are **signed R2 URLs** that actually `GET 200` from MinIO, and whose `#EXT-X-KEY URI` is
   the `/api/me/videos/{id}/hls.key` endpoint (`FR-PLAT-VID-003`).
6. **Re-use the same handoff code** → `410 handoff_expired` (one-time). Let a code expire → `410`.
7. `GET /api/me/videos/{id}/hls.key` with the student JWT → `200`, 16 bytes; **AccessRemaining unchanged** (key fetch
   never decrements). Anon → `401`; staff JWT → `403`; another student → `403`.

**Isolation + default-deny (`NFR-SEC-010`, `FR-PLAT-AUTH`):**
8. Second-tenant video id on any route → `404` (tenant filter, not a leak). Staff JWT on #1/#2 → `403`; anon → `401`.

## Sign-off
- Log the run (counts + the AccessRemaining deltas + the audit actor) into this file like the prior wiring logs; update
  the master plan's 5C bullet from *Planned* → **Met** with the date + headline result; record a memory entry
  (`phase5c-wiring`). **5C closes the admin-portal plan's backend scope** — the only remaining `FR-PLAT-VID` items are
  `004` (player watermark) and the OS black-out half of `005`, both deferred to the student/native engagement.

---

## MET — 2026-06-21 · 22/22 live checks, ZERO drift

Proven on the running Aspire stack (Postgres + MinIO + Redis + API + Angular) via the `:4200` proxy, driven by
direct-minted platform JWTs (teacher + student), with **real ffmpeg**. A throwaway PowerShell smoke (minted HS256
tokens, generated a real 2 s clip with ffmpeg, drove the admin + engine APIs, verified DB state via `docker exec psql`)
ran **22 assertions, all green**:

- **Transcode (real ffmpeg):** create session → multipart upload (file part last) → poll → `ProcessingStatus=Ready`;
  `session_videos.HlsKeyObjectKey` persisted (key object in MinIO). DB holds keys only.
- **Gate (#1):** `POST /api/me/videos/{id}/playback` → 200 + one-time handoff code (no URL); `AccessRemaining` 1→0
  (psql); one `VideoPlaybackStarted` audit row, **ActorType=Student**.
- **Redeem (#2):** `POST /api/me/videos/playback/redeem` → 200; manifest's `__HLS_KEY_URI__` replaced by the absolute
  `/api/me/videos/{id}/hls.key`; the rewritten segment URI is a signed R2 URL that **fetches 200 from MinIO**.
- **One-time code:** reusing the handoff code → **410** `handoff_expired`.
- **Key (#3):** `GET /api/me/videos/{id}/hls.key` (student) → 200 + **16 bytes**; `AccessRemaining` unchanged
  (key fetch never decrements).
- **Reasons:** exhausted → 403 `no_views_remaining`; not-enrolled student → 403 `not_enrolled`.
- **Default-deny / isolation:** anon → 401; staff token → 403 (RequireStudent); cross-tenant video id → 404.

**Gotchas hit + fixed during wiring:**
1. The running API couldn't resolve `ffmpeg` (its process PATH predated the install). Fixed by making the **AppHost
   resolve ffmpeg's absolute path** (PATH → winget `…/WinGet/Links` → choco) and inject `Transcode__FfmpegPath`;
   restart the AppHost to pick it up. The API now runs ffmpeg regardless of its own PATH.
2. Aspire assigns new container names/ports each run → discover the Postgres container name dynamically for psql.
3. PowerShell 5.1 mangles embedded double-quotes when passing `-c "…\"col\"…"` to `docker.exe` → pipe SQL via
   `docker exec -i … psql` stdin instead (PascalCase columns need the quotes).

**5C is complete; the admin-portal plan's backend scope (Phases 0–5) is closed.**
