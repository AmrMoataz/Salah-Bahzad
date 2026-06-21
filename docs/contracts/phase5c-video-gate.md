# FROZEN CONTRACT — Phase 5C · Secure video gate (HLS + AES-128)

> Status: **Frozen** · Created 2026-06-21 · Slice: Phase **5C** (the final sub-phase of Phase 5; closes the
> admin-portal plan's backend scope). **No design anchor** — this slice has **no admin screen**; its consumers are
> the future student portal + Flutter app. The authority is `FR-PLAT-VID-001..007` and `docs/05-secure-video-streaming-options.md`.
>
> Satisfies: video gate + accounting + audit `FR-PLAT-VID-001/002/006`; HLS + AES-128 + short-lived signed URLs
> `FR-PLAT-VID-003/007`; the one-time deep-link **handoff code** half of `FR-PLAT-VID-005`. **Replaces the Phase-3
> transcode stub** (`StubVideoProcessingQueue`) with a real ffmpeg pipeline. Reads the *quiz-passed → videos-unlocked*
> state 5B-2 produced (`UserQuiz.Passed`) and spends the `EnrollmentVideoAccess.AccessRemaining` Phase-4 provisioned.

## 0. Ground rules

- **Backend-only engagement.** The video **gate** is the engine — REST endpoints a future student portal / native app
  calls. 5C ships **no admin screen and no frontend stream**; wiring drives every route with a **student JWT** (the
  Phase-4 redeem + 5B-engine pattern). Streams are **backend + wiring** only.
- **Auth:** every engine route is `RequireStudent` (anon → 401, staff → 403); `userId`/`tenantId` come from the JWT;
  handlers IDOR-check ownership through the tenant-filtered aggregate. **No permission/catalog change.**
- **Decision — full real transcode.** ffmpeg produces **AES-128-encrypted HLS** (user-confirmed 2026-06-21), not a
  passthrough. ffmpeg is **shelled out from the existing in-API Hangfire worker** (no new Aspire container); the binary
  is a host/image prerequisite (§F). MinIO/R2, Redis, and Hangfire are **already wired** — no AppHost infra add.
- **Tenant-scoped:** the video resolves through its tenant-filtered `Session`; `EnrollmentVideoAccess` through the
  `ITenantOwned` `Enrollment`. Cover cross-tenant isolation (`NFR-SEC-010`).
- **Migration required** (gated, one column): `session_videos.hls_key_object_key` (the R2 key of the AES-128 key
  object). Handoff codes + the per-playback state live in **Redis** (no migration). The DB still stores **only object
  keys** — never bytes, never durable URLs (`FR-PLAT-VID-007`).

## A. Transcode pipeline — real, replaces `StubVideoProcessingQueue`

Triggered exactly as today: `AddSessionVideo` / `ReplaceSource` persist the `Pending` video then call
`IVideoProcessingQueue.EnqueueTranscodeAsync`. **Now real:** `HangfireVideoProcessingQueue` enqueues a durable
`VideoTranscodeJob` (fire-and-forget) instead of flipping `Ready` inline.

`VideoTranscodeJob.RunAsync(Guid videoId, Guid tenantId)` (public, Hangfire DI-activated; runs as **System** via
`ISystemOperationContext` so its writes audit as `System` + tenant with no `HttpContext` — the 5B-2 job pattern):
1. `video.MarkProcessing()`.
2. Download the source (`SourceObjectKey`) from R2 to a temp working dir.
3. Generate a random **16-byte AES-128 key + IV**; write an ffmpeg **key-info file** whose **key URI is the stable
   gated key endpoint** (`§B #3`, `/api/me/videos/{videoId}/hls.key`) — *not* a bare URL with the key in it.
4. Shell out to ffmpeg → **single-rendition** AES-128 HLS (`-hls_segment_type mpegts -hls_key_info_file …`): encrypted
   `.ts` segments + an `.m3u8` whose segment URIs are **relative** and whose `#EXT-X-KEY URI` is the key endpoint.
   *(Multi-bitrate ABR is a documented later enhancement — out of 5C to bound scope.)*
5. Upload segments + the relative manifest to the **private** bucket under a deterministic prefix; upload the **AES key**
   as a separate private object.
6. `video.MarkReady(hlsManifestKey)` + set `HlsKeyObjectKey`. On any failure → `video.MarkFailed()`; always clean the
   temp dir. The status flip is audited (System) via the existing interceptor.
- **Seam for testability:** ffmpeg is isolated behind `IMediaTranscoder` (Application) → `FfmpegMediaTranscoder`
  (Infrastructure). The job owns R2 + DB + key storage; the transcoder owns only the ffmpeg invocation, so gate tests
  fake it and a single opt-in integration test exercises the real binary (§ backend plan).

## B. Playback gate — REST, student-facing (`RequireStudent`)

| # | Method & path | Returns | Notes |
|---|---|---|---|
| 1 | `POST /api/me/videos/{videoId}/playback` | `PlaybackHandoffDto` | The gate (`FR-PLAT-VID-001/002/006`): authorize → **decrement** → audit → issue a **one-time handoff code**. Never returns a URL/token. |
| 2 | `POST /api/me/videos/playback/redeem` | `PlaybackManifestDto` | Body `{ handoffCode }`. Consumes the one-time code (`410` if missing/expired/used/not-owner) → builds the signed manifest. `FR-PLAT-VID-003`. |
| 3 | `GET  /api/me/videos/{videoId}/hls.key` | `application/octet-stream` (16 bytes) | The AES-128 **key endpoint** the HLS client calls (the manifest's `#EXT-X-KEY URI`). Re-authorizes; **does not** decrement. |

**Gate logic (#1), in order — each failure is a specific, user-readable `reason` (`FR-PLAT-VID-006`):**
1. Resolve the video via its tenant-filtered `Session` → `404` if not found / cross-tenant (IDOR, `NFR-SEC-007`).
2. `ProcessingStatus == Ready` (manifest present) else `409 not_ready`.
3. Caller's **active, unexpired** `Enrollment` for the video's session, else `403` — `not_enrolled` | `enrollment_expired`.
4. If the session is quiz-gated (has a prerequisite `UserQuiz`) and `!UserQuiz.Passed` → `403 quiz_required`.
5. Caller's `EnrollmentVideoAccess.AccessRemaining > 0` for this video, else `403 no_views_remaining`.
6. **`EnrollmentVideoAccess.Decrement()`** (atomic, in the command transaction).
7. Write a **`VideoPlaybackStarted`** audit row — **Student** actor (who watched what, when — `FR-PLAT-VID-002`).
8. Issue a one-time **handoff code** (Redis, ~60 s TTL) mapping → `{ videoId, enrollmentId, studentId, tenantId }`.

```jsonc
// PlaybackHandoffDto (#1) — the deep-link payload; the raw token/URL is NEVER returned here (FR-PLAT-VID-005)
{ "handoffCode": "5f3c…", "expiresAtUtc": "2026-06-21T10:00:60Z" }

// PlaybackManifestDto (#2) — manifest content is built per-redeem so segment URLs are short-lived + non-replayable
{ "manifestContent": "#EXTM3U…",          // .m3u8 text: each segment URI rewritten to a short-lived signed R2 URL;
  "keyUrl": "https://…/api/me/videos/{videoId}/hls.key",   //   the #EXT-X-KEY URI = the stable gated key endpoint
  "expiresAtUtc": "2026-06-21T10:05:00Z" } //   the shortest of the signed-segment TTLs
```

**Reason codes (frozen):** `not_ready` (409), `not_enrolled` (403), `enrollment_expired` (403), `quiz_required` (403),
`no_views_remaining` (403), `handoff_expired` (410, on #2). Each is a ProblemDetails with a machine `reason` + readable `detail`.

## C. Secure HLS — how the bytes stay protected

- **Segments** are AES-128-encrypted private R2 objects. The **redeem (#2)** step fetches the stored relative manifest
  and rewrites every segment URI to a **fresh short-lived signed R2 GET URL**, returning the rewritten manifest
  **inline** (`manifestContent`). Nothing durable is hotlinkable; a leaked manifest dies with its TTL (`FR-PLAT-VID-003`).
- **The AES key** is served only by the gated key endpoint (#3), which re-runs the gate's *authorization* subset (active
  enrollment + quiz-passed) but **never decrements** (an HLS client re-fetches the key across a session). The key URI is
  baked into the manifest at transcode time as the **stable** endpoint path; the native player attaches the platform JWT
  when fetching it. In wiring (no player) it's hit directly with the student JWT.
- **Decrement happens at the gate (#1), not at redeem** — #1 is the audited "playback start" (`FR-PLAT-VID-002`); the
  one-time code prevents double-spend, and a failed/abandoned redeem does **not** refund the view. *(Alternative —
  decrement-at-redeem — explicitly rejected: it lets a caller mint many codes without spending.)*

## D. Backend model / persistence deltas
- `EnrollmentVideoAccess.Decrement()` — **new** domain method (guards `AccessRemaining > 0`; throws → mapped to
  `no_views_remaining`). No schema change.
- `SessionVideo.HlsKeyObjectKey` (`string?`) — **new** nullable column; set alongside `HlsManifestKey` on `MarkReady`.
  The **one gated migration**. Segments live under a deterministic prefix derived from the manifest key (no extra column).
- **No new entity.** Handoff codes + their payload are Redis-only.
- **New seams:** `IPlaybackHandoffStore` (issue / one-time `GETDEL` consume) → `RedisPlaybackHandoffStore`;
  `IMediaTranscoder` → `FfmpegMediaTranscoder`; `IVideoProcessingQueue` → **real** `HangfireVideoProcessingQueue`
  (replaces `StubVideoProcessingQueue`).

## E. Audit (`FR-PLAT-VID-002`, `FR-PLAT-AUD-002`)
- **`VideoPlaybackStarted`** — one row per successful gate (#1), **Student** actor, `EntityType=SessionVideo`,
  summary "Watched: {title}". This is the "who watched what, when" trail.
- Transcode status transitions (`Processing`/`Ready`/`Failed`) are written by the **System** actor (the job has no
  `HttpContext`; `ISystemOperationContext`), via the existing `SaveChangesInterceptor` diff — no bespoke event required.
- The key endpoint (#3) and redeem (#2) are **not** separately audited (they follow an already-audited #1; key re-fetch
  is high-volume) — mirrors materials not re-auditing each signed-URL read.

## F. Infrastructure — ffmpeg prerequisite (no AppHost change)
- ffmpeg must be reachable by the API process: **dev** → on `PATH`; **prod** → installed in the API container image;
  **CI** → `apt-get install -y ffmpeg` before the integration job. Path overridable via `Transcode:FfmpegPath`
  (default `ffmpeg`). No new Aspire resource — the job runs inside the API's Hangfire server, which already exists.
- MinIO (R2), Redis (handoff store + Hangfire mapping), and the Hangfire server are unchanged from 5B-2.

## G. Boundary — deferred to the student/native engagement
- **`FR-PLAT-VID-004` dynamic watermark** (student serial/phone over the video, disable right-click/PiP) — a **player**
  concern; no player here.
- **`FR-PLAT-VID-005` screen black-out** (OS secure-surface flags) — a **native-app** concern. 5C delivers the
  backend half: the **one-time handoff code** that the device-aware deep link carries (never the raw token).
- Attendance "videos watched" stays as 5B-2 left it; surfacing playback counts in attendance is out of 5C.
  **Follow-up (2026-06-21, post-5C):** now surfaced — the attendance projector derives "videos watched" as the
  count of per-video access counters with a spent view (`AccessRemaining < AccessAllowed`), the single source of
  truth the gate already maintains (no stored counter, resets correctly on re-enroll). The student-detail
  "Enrollments & attendance" card renders this per-session progress.

## H. Frozen vs. stream-owned
- **Frozen:** the 3 routes + `RequireStudent`; the `PlaybackHandoffDto`/`PlaybackManifestDto` field names/types; the
  six `reason` codes + their status codes; **decrement-at-gate**; the one-time-code handoff (never a raw URL at #1);
  **AES-128** + per-redeem signed-segment manifest + stable gated key endpoint; `VideoPlaybackStarted` = Student,
  transcode = System; the `EnrollmentVideoAccess.Decrement` gate on `AccessRemaining`.
- **Backend owns:** the Redis key shapes + TTLs, the ffmpeg invocation/flags, the HLS prefix layout, manifest rewrite
  details, EF mapping + the migration, the `IMediaTranscoder` test seam.
- **Wiring owns:** proving the whole chain live on the Aspire stack with a student JWT — upload → real transcode →
  gate → decrement → redeem → reachable signed segments + gated key — plus every `reason`, one-time-code reuse,
  tenant isolation, and default-deny.
