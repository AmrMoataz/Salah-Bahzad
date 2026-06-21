# HLS Transcoding & Secure Streaming — How It Works

> Implementation companion to [`05-secure-video-streaming-options.md`](05-secure-video-streaming-options.md) (the
> cost/threat-model architecture) and the [Phase 5C contract](contracts/phase5c-video-gate.md). Where 05 says *what* we
> chose and *why* it's cheap, this doc explains the **mechanics**: why HLS, how we produce it with ffmpeg + AES-128, and
> exactly how a client turns "click play" into decrypted, gated playback. Requirement IDs: `FR-PLAT-VID-001..007`.

---

## 1. Why HLS at all (vs. just serving an MP4)

A lesson video is the platform's most valuable and most expensive asset. Serving it as a single progressive file
(`GET …/lesson.mp4`) fails on three fronts:

| Problem with a single MP4 | What HLS gives us |
|---|---|
| **One URL = the whole movie.** Anyone who grabs the link (dev-tools, a shared URL) has the entire file forever. | The video is **chopped into hundreds of small segments** behind a playlist. There is no single file to take. |
| **No access control mid-stream.** Once the URL leaks, it's a durable download. | Each segment is fetched via a **short-lived signed URL**; a leaked playlist is dead within minutes. |
| **No encryption.** A ripper (`yt-dlp`) trivially saves the file. | Segments are **AES-128 encrypted**; the key is released only to an authorized, gated request. |
| **One bitrate.** A weak MENA mobile connection stalls. | HLS is **adaptive-ready** (multi-bitrate is a later enhancement; 5C ships a single rendition first). |

**HLS = HTTP Live Streaming.** Concretely it is two kinds of files:

- A **manifest / playlist** (`.m3u8`) — a small text file listing the segments in order, plus metadata (segment
  duration, the encryption key reference).
- Many **media segments** (`.ts`, ~6 seconds each) — the actual video/audio bytes, here individually **encrypted**.

The player reads the manifest, then fetches and decrypts segments one by one, just ahead of the playhead. This is the
same mechanism Netflix/YouTube use; we add encryption + signed URLs + a server-side gate so it's *ours* and *paid-for-once*.

> **Why not a managed video platform (Mux, Cloudflare Stream, AWS MediaConvert)?** Per `05` §2, the dominant cost for
> video is **egress**, and Cloudflare R2 charges **$0 egress**. Self-hosting the ffmpeg transcode + R2 storage keeps the
> client's media bill at a couple of dollars a month instead of a per-minute encode + per-GB delivery bill. The price we
> pay is running ffmpeg ourselves — which is what this doc is about.

---

## 2. The protection layers (defense in depth)

No single layer is the wall; together they are. (Full threat table in `05` §5/§7.)

1. **Server enrollment gate** — playback is refused without an active, unexpired, paid enrollment *with views remaining*,
   and (if the session is quiz-gated) a passed quiz. `FR-PLAT-VID-001`.
2. **Per-video view budget** — each successful start **decrements** a counter and is **audited**. `FR-PLAT-VID-002`.
3. **AES-128 encryption** — segments are useless without the key; the key endpoint re-checks the gate. `FR-PLAT-VID-003`.
4. **Short-lived signed segment URLs** — generated fresh per playback; a copied manifest expires in minutes. `FR-PLAT-VID-003`.
5. **One-time handoff code** — the web→app deep link never carries a raw token; it carries a single-use code. `FR-PLAT-VID-005`.
6. **OS screen black-out + dynamic watermark** — delivered by the **native app** (out of this engagement; see §7).

This doc implements layers **1–5** (the backend). Layer 6 is the player/native concern.

---

## 3. How we *produce* HLS — the transcode pipeline

### 3.1 Trigger
Nothing changes about upload: staff upload a source video → it streams straight to R2 (`SourceObjectKey`), the
`SessionVideo` is saved `Pending`, and the handler calls `IVideoProcessingQueue.EnqueueTranscodeAsync`. **Before 5C** that
seam was `StubVideoProcessingQueue` (it just flipped the video to `Ready` with no real output). **5C replaces it** with
`HangfireVideoProcessingQueue`, which enqueues a durable `VideoTranscodeJob`.

### 3.2 The job (`VideoTranscodeJob`, runs on the API's Hangfire worker)
Because the job has **no HTTP request**, it opens an `ISystemOperationContext.Begin(tenantId)` scope so the EF global
tenant filter and the audit interceptor work and every write is attributed to the **System** actor.

```
RunAsync(videoId, tenantId):
  scope = systemOperation.Begin(tenantId)
  video.MarkProcessing(); save                       # status: Pending → Processing (audited: System)
  srcUrl = storage.GetSignedReadUrl(SourceObjectKey) # ffmpeg reads the source straight from R2 — no multi-GB
                                                      #   download to the app server's disk (05 §6)
  key, iv = random 16 bytes each                      # the AES-128 content key + IV
  write keyinfo file (key URI placeholder, key path, iv hex)
  ffmpeg  -i srcUrl  -c:v libx264 -c:a aac \          # single rendition (re-encode → uniform output)
          -hls_time 6 -hls_playlist_type vod \
          -hls_key_info_file keyinfo \
          -hls_segment_filename out/seg_%03d.ts \
          out/index.m3u8
  ffprobe srcUrl → exact run length (seconds)         # the duration; never admin-entered (FR-PLAT-SES-002)
  upload out/*.ts  + out/index.m3u8  + the raw key  → R2 private bucket, under the video's HLS prefix
  video.MarkReady(manifestKey, keyObjectKey, durationSeconds); save  # Processing → Ready (audited: System)
  on any failure: video.MarkFailed(); save; log ffmpeg stderr
  finally: delete the temp working dir
```

### 3.3 The AES-128 "key-info file" (the heart of the encryption)
ffmpeg encrypts when given a **key-info file** — a 3-line text file:

```
__HLS_KEY_URI__                 ← line 1: the string ffmpeg writes verbatim into the playlist's #EXT-X-KEY URI=""
/tmp/…/enc.key                  ← line 2: the local file holding the raw 16 random bytes (ffmpeg reads it to encrypt)
8f3b2c…(32 hex)                 ← line 3: the initialization vector (IV)
```

We bake a **placeholder** (`__HLS_KEY_URI__`) into line 1 on purpose — the *real* key URL is injected later, at redeem
time (§4.3), so the **stored** manifest never hard-codes an environment-specific URL. The raw key is uploaded to R2 as a
private object (`enc.key`); it is **only** ever handed out by the gated key endpoint (§4.4).

### 3.4 What ends up in R2 (the storage layout)
```
sessions/{tenantId}/{sessionId}/videos/{guid}.mp4              ← the original source (unchanged, from upload)
sessions/{tenantId}/{sessionId}/videos/hls/{videoId}/
        ├─ index.m3u8     ← the manifest (stored with the placeholder key URI + relative segment names)
        ├─ seg_000.ts     ← AES-128 encrypted segments
        ├─ seg_001.ts
        └─ enc.key        ← the 16-byte AES key (private; never public, never in the manifest)
```
The database stores **only keys** (`FR-PLAT-VID-007`): `SessionVideo.HlsManifestKey = ".../index.m3u8"` and
`SessionVideo.HlsKeyObjectKey = ".../enc.key"`. Segment keys are derived (manifest directory + the segment name in the
playlist), so no per-segment DB rows are needed and the layout can evolve without a migration.

A stored manifest looks like this (note: **relative** segment names, **placeholder** key URI — both rewritten at redeem):
```m3u8
#EXTM3U
#EXT-X-VERSION:3
#EXT-X-TARGETDURATION:7
#EXT-X-MEDIA-SEQUENCE:0
#EXT-X-PLAYLIST-TYPE:VOD
#EXT-X-KEY:METHOD=AES-128,URI="__HLS_KEY_URI__",IV=0x8f3b2c…
#EXTINF:6.000000,
seg_000.ts
#EXTINF:6.000000,
seg_001.ts
#EXT-X-ENDLIST
```

---

## 4. How we *stream* it — the gate, handoff, and playback

The web portal does **not** play video; it deep-links into the native app, which authenticates and plays
(`05` §4). The backend exposes three student-authenticated routes (`RequireStudent`: anon→401, staff→403).

### 4.1 The sequence
```
 Student clicks "Play" in the web portal
   │
   ▼  (1) POST /api/me/videos/{videoId}/playback          ── THE GATE ──
        backend checks:  video Ready?  active+unexpired enrollment?  quiz passed (if gated)?  views remaining?
        on success:      DECREMENT the view counter  +  write VideoPlaybackStarted audit (Student actor)
        returns:         { handoffCode, expiresAtUtc }        ← a one-time code, ~60s TTL. NEVER a URL/token.
   │
   ▼  portal opens device-aware deep link:  salah-bahazad://stream?video=…&handoff=<code>
   │
   ▼  native app authenticates the student, then:
   │
   ▼  (2) POST /api/me/videos/playback/redeem   { handoffCode }   ── EXCHANGE ──
        backend consumes the one-time code (410 if missing/expired/used/not-owner)
        builds a PER-PLAYBACK manifest: every segment name → a short-lived SIGNED R2 URL,
                                        the #EXT-X-KEY URI    → the absolute key endpoint
        returns:  { manifestContent, keyUrl, expiresAtUtc }
   │
   ▼  the app's HLS player loads manifestContent, then for each segment:
   │
   ▼  (3) GET /api/me/videos/{videoId}/hls.key      ── THE KEY ──
        backend RE-checks authorization (active enrollment + quiz passed) — but does NOT decrement
        returns the 16 raw key bytes (application/octet-stream)
   │
   ▼  player fetches each signed segment URL from R2/CDN, decrypts with the key, plays.
        + native app paints the watermark and turns on the OS black-out flag (§7)
```

### 4.2 The gate (#1) — `POST /api/me/videos/{videoId}/playback`
Checks run in order; each failure is a specific, user-readable reason (`FR-PLAT-VID-006`), surfaced as a ProblemDetails
with a machine `reason` extension + a readable `detail`:

| Order | Check | Failure → status · `reason` |
|---|---|---|
| 1 | Video resolves through the caller's tenant-filtered session | `404` (IDOR/tenant — no leak) |
| 2 | `ProcessingStatus == Ready` (manifest present) | `409 not_ready` |
| 3 | An **active, unexpired** enrollment for the video's session, owned by the caller | `403 not_enrolled` / `403 enrollment_expired` |
| 4 | If a gating `UserQuiz` exists for the enrollment, it is **passed** | `403 quiz_required` |
| 5 | `EnrollmentVideoAccess.AccessRemaining > 0` for this video | `403 no_views_remaining` |

On success (all in one DB transaction): `EnrollmentVideoAccess.Decrement()` → write `VideoPlaybackStarted` (Student
actor) → issue a one-time **handoff code** in Redis (`{ videoId, enrollmentId, studentId, tenantId }`, ~60s TTL) → return
`{ handoffCode, expiresAtUtc }`.

> **Why decrement at the gate, not at redeem?** The gate *is* the audited "playback start" (`FR-PLAT-VID-002`). The
> one-time code prevents a single grant from being spent twice; an abandoned/failed redeem does **not** refund the view.
> Decrementing at redeem instead would let a caller mint many codes (and many audit rows) without ever "spending" — so
> we reject that. (Counters reset on re-enroll/extend — `FR-PLAT-ENR-004`.)

### 4.3 Redeem (#2) — `POST /api/me/videos/playback/redeem`
Consumes the one-time code atomically (Redis `GETDEL`); a missing/expired/already-used code, or one not owned by the
caller, → `410 handoff_expired`. Then it builds the **per-playback manifest** from the stored one:

- each relative segment name (`seg_000.ts`) → a **fresh short-lived signed R2 GET URL** (cannot be replayed after TTL);
- the `#EXT-X-KEY` `URI="__HLS_KEY_URI__"` placeholder → the **absolute** key-endpoint URL (#3).

It returns the rewritten manifest **inline** (`manifestContent`) so nothing durable is hotlinkable, plus `keyUrl` and the
soonest `expiresAtUtc`. Building the manifest per-redeem is what makes the signed segment URLs short-lived and
non-shareable (`FR-PLAT-VID-003`).

### 4.4 The key endpoint (#3) — `GET /api/me/videos/{videoId}/hls.key`
This is the URL baked (at redeem) into the manifest's `#EXT-X-KEY`. The HLS client calls it (the native app attaches the
platform JWT). It **re-runs the gate's authorization subset** (active enrollment + quiz passed; tenant/IDOR via the
session) but **never decrements** — an HLS client legitimately re-fetches the key during one sitting. It streams the 16
raw key bytes from the private `enc.key` object. Anon→401, staff/other-student→403, cross-tenant→404. Not separately
audited (it follows an already-audited gate; key re-fetch is high-volume) — mirroring how materials don't re-audit each
signed-URL read.

---

## 5. The data & seams (where each piece lives)

| Piece | Layer | Notes |
|---|---|---|
| `SessionVideo.HlsManifestKey`, `.HlsKeyObjectKey`, `.ProcessingStatus` | Domain | DB stores keys only. One migration adds `hls_key_object_key`. |
| `EnrollmentVideoAccess.AccessRemaining` + `Decrement()` | Domain | Provisioned at enroll (Phase 4); spent here. |
| `UserQuiz.Passed` | Domain | The quiz-gated → videos-unlocked flag (Phase 5B-2); read at the gate. |
| `IVideoProcessingQueue` → `HangfireVideoProcessingQueue` | App seam / Infra | Replaces `StubVideoProcessingQueue`. |
| `IMediaTranscoder` → `FfmpegMediaTranscoder` | App seam / Infra | Isolates the ffmpeg invocation (so tests fake it). |
| `IPlaybackHandoffStore` → `RedisPlaybackHandoffStore` | App seam / Infra | One-time code issue/consume (`GETDEL`). |
| `IFileStorage.OpenReadAsync` | App seam / Infra | New read method (key bytes + manifest text). |
| `VideoTranscodeJob` | Infra (Hangfire) | System-attributed; orchestrates download-via-URL → ffmpeg → upload → status. |

---

## 6. Operating it — the ffmpeg prerequisite

ffmpeg is a **binary the API process shells out to**, not an Aspire service — so there's no AppHost/orchestration change,
but the binary must be present wherever the API (its Hangfire worker) runs:

- **Dev (your machine):** install ffmpeg and put it on `PATH` — `winget install ffmpeg` or `choco install ffmpeg`, then
  `ffmpeg -version` should work in a fresh terminal. Needed before a real transcode runs locally.
- **Prod:** install ffmpeg in the API container image (one `RUN apt-get install -y ffmpeg` line in the Dockerfile).
- **CI:** `apt-get install -y ffmpeg` before the integration test job (so the one real-ffmpeg test runs).

Config lives in a `Transcode` section: `FfmpegPath` (default `ffmpeg`), `HlsTimeSeconds` (default 6),
`SegmentUrlTtlSeconds` (default 120), `HandoffTtlSeconds` (default 60).

> Alternative considered: running ffmpeg as its **own container/worker** so nothing installs locally. Rejected for 5C —
> it adds a separate service + a job queue handoff for no functional gain at this stage. Revisit if transcode load ever
> needs to scale independently of the API.

### Inspecting it locally
With the Aspire stack up (`F5`), after uploading a video:
- The MinIO console (`http://localhost:9001`, `minioadmin`/`minioadmin`) shows `sb-dev-private` → the `…/hls/{videoId}/`
  prefix with `index.m3u8`, `seg_*.ts`, and `enc.key`.
- The `session_videos` row shows `ProcessingStatus = Ready` with both `HlsManifestKey` and `HlsKeyObjectKey` set.
- Driving the three routes with a student JWT (see the [wiring plan](IMPLEMENTATION-PLAN-phase5c-wiring.md)) returns a
  handoff code → a manifest whose segment URLs `GET 200` from MinIO → 16 key bytes from the key endpoint.

---

## 7. What this does **not** do (deferred to the student/native engagement)

- **Dynamic watermark** (`FR-PLAT-VID-004`) — the student serial/phone painted over the frame: a **player** feature.
- **Screenshot/recording black-out** (`FR-PLAT-VID-005`) — OS secure-surface flags (`FLAG_SECURE`, FairPlay,
  `SetWindowDisplayAffinity`, `NSWindow.sharingType`): a **native-app** feature. 5C delivers the backend half — the
  one-time handoff code the device-aware deep link carries.
- **Multi-bitrate ABR** — 5C transcodes a single rendition; an adaptive ladder (240p/480p/720p) is a later enhancement.
- **A CDN in front of R2** — the signed URLs are R2-direct in dev; Cloudflare CDN fronting R2 (`05` §2) is a deployment concern.

---

## 8. One-paragraph summary

A staff upload lands in R2 as a source file. A Hangfire job runs **ffmpeg** to turn it into **AES-128-encrypted HLS** —
a playlist plus many short encrypted segments plus a private key object — all in R2, with the DB holding only the keys.
When a student plays, the backend **gate** verifies their enrollment/quiz/view-budget, spends one view, audits it, and
hands back a **one-time code** (never a URL). The native app exchanges that code for a **freshly-signed, short-lived
manifest**, and fetches the **AES key** from a gated endpoint that re-checks authorization. The player decrypts and
plays; the app adds the watermark and OS black-out. Every layer is cheap (R2 = $0 egress) and server-enforced.
