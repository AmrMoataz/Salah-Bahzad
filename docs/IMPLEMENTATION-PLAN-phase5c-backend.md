# Phase 5C — Backend stream (Secure video gate: HLS + AES-128)

> Builds the engine half of `docs/contracts/phase5c-video-gate.md`. **Backend-only** slice — no frontend stream.
> Satisfies `FR-PLAT-VID-001/002/003/006/007` + the handoff half of `005`. Change the **contract first** if anything moves.
> Gates: `dotnet test -c Release` green (the one pre-existing QuestionBank image-test failure is the known baseline);
> then the **wiring** stream (`IMPLEMENTATION-PLAN-phase5c-wiring.md`) proves it live on Aspire.

## Pre-flight
- Re-read the contract §A–§H and `backend/CLAUDE.md` (audit, tenancy, video/asset, EF, security checklist).
- Confirm what already exists (do **not** rebuild): `IFileStorage.{UploadPrivateStreamingAsync,GetSignedReadUrlAsync}`,
  `SessionVideo.{SourceObjectKey,HlsManifestKey,ProcessingStatus,MarkProcessing/Ready/Failed}`,
  `EnrollmentVideoAccess.AccessRemaining`, `UserQuiz.Passed`, `ISystemOperationContext`, the Hangfire server (5B-2),
  `IAuditWriter`, `ExecuteInTransactionAsync`, `ICurrentUserResolver`, the `IEndpointGroup` auto-discovery +
  `RequireStudent()` helper.

## Step 1 — Domain (no I/O; unit-tested)
1. `EnrollmentVideoAccess.Decrement()` — guard `AccessRemaining > 0`, else throw a domain exception mapped later to
   `no_views_remaining`; decrement on success. Add `ResetTo` is already there — leave it.
2. `SessionVideo`: add `HlsKeyObjectKey` (`string?`); extend `MarkReady(string? hlsManifestKey, string? hlsKeyObjectKey)`
   (keep the existing single-arg overload working for the unit tests / interceptor). Null until `Ready`.
3. Unit tests: `Decrement` happy path + throw-at-zero; `MarkReady` sets both keys; `MarkFailed`/`ReplaceSource` reset both.

## Step 2 — Application seams + DTOs
1. `IPlaybackHandoffStore`: `Task<string> IssueAsync(PlaybackHandoff payload, TimeSpan ttl, CancellationToken)` and
   `Task<PlaybackHandoff?> ConsumeAsync(string code, CancellationToken)` (**one-time** — read-and-delete). Record
   `PlaybackHandoff(Guid VideoId, Guid EnrollmentId, Guid StudentId, Guid TenantId)`.
2. `IMediaTranscoder`: `Task<TranscodeOutput> TranscodeToEncryptedHlsAsync(string localSourcePath, string outputDir,
   string keyUri, CancellationToken)` returning `TranscodeOutput(string ManifestPath, byte[] KeyBytes, IReadOnlyList<string> SegmentPaths)`.
   ffmpeg-only; no R2/DB knowledge.
3. DTOs (contract §B): `PlaybackHandoffDto(string HandoffCode, DateTimeOffset ExpiresAtUtc)`,
   `PlaybackManifestDto(string ManifestContent, string KeyUrl, DateTimeOffset ExpiresAtUtc)`.

## Step 3 — Application: the gate (#1) `StartVideoPlaybackCommand`
- `Features/Videos/Commands/StartVideoPlayback/` — command `(Guid VideoId)`; handler:
  1. Load the video via tenant-filtered `Session` (`Include(Videos)`); `NotFoundException` if absent (404).
  2. `ProcessingStatus == Ready` else `ConflictException("not_ready")`.
  3. Active unexpired `Enrollment` for `video.SessionId` owned by `currentUser.UserId`, else
     `ForbiddenException("not_enrolled" | "enrollment_expired")`.
  4. If the session has a gating `UserQuiz` and `!Passed` → `ForbiddenException("quiz_required")`.
  5. Load `EnrollmentVideoAccess` for `(enrollmentId, videoId)`; `Decrement()` (maps domain throw →
     `ForbiddenException("no_views_remaining")`). Wrap 5–7 in `ExecuteInTransactionAsync`.
  6. `auditWriter.WriteAsync("VideoPlaybackStarted","SessionVideo",videoId,$"Watched: {title}")` — Student actor.
  7. `handoffStore.IssueAsync(...)`; return `PlaybackHandoffDto`.
- Map the new exception `reason`s to ProblemDetails in the existing exception→status middleware (add the six reason
  codes; keep machine `reason` + readable `detail`).

## Step 4 — Application: redeem (#2) + key (#3)
1. `RedeemPlaybackQuery`/handler `(string HandoffCode)`: `ConsumeAsync` → `410 handoff_expired` if null **or** not owned
   by the caller. Re-load the video; fetch the stored relative manifest object; for each segment issue a short-lived
   signed R2 URL (`GetSignedReadUrlAsync`); rewrite the manifest text inline; set the `#EXT-X-KEY URI` to the absolute
   key endpoint; return `PlaybackManifestDto` with `ExpiresAtUtc` = the soonest segment TTL.
2. `GetHlsKeyQuery`/handler `(Guid VideoId)`: re-run the **authorization** subset of the gate (active enrollment +
   quiz-passed; tenant/IDOR via the session) — **no decrement** — then return the 16 key bytes from
   `HlsKeyObjectKey` (stream the private object). Endpoint returns `application/octet-stream`.

## Step 5 — Infrastructure
1. `RedisPlaybackHandoffStore` (`IConnectionMultiplexer`): `IssueAsync` = `SET code → json EX ttl`; `ConsumeAsync` =
   atomic `GETDEL`. Code = cryptographically random (e.g. 32 hex).
2. `FfmpegMediaTranscoder`: write the ffmpeg **key-info file** (key URI line = `keyUri`, key file path, hex IV), invoke
   `Transcode:FfmpegPath` with HLS + `-hls_key_info_file`, capture stderr for diagnostics, throw on non-zero exit.
3. `HangfireVideoProcessingQueue` implements `IVideoProcessingQueue` by `backgroundJobs.Enqueue<VideoTranscodeJob>(j =>
   j.RunAsync(videoId, tenantId))`. **Delete `StubVideoProcessingQueue`** and swap the DI registration.
4. `Jobs/VideoTranscodeJob` (public `RunAsync(Guid videoId, Guid tenantId)`): set the `ISystemOperationContext` scope;
   `MarkProcessing` (save) → download source to temp → `IMediaTranscoder.Transcode…` (key URI = the §B#3 path for this
   video) → upload segments + relative manifest + key object to the private bucket under the deterministic prefix →
   `MarkReady(manifestKey, keyObjectKey)` (save) → on exception `MarkFailed` (save) + log stderr → `finally` clean temp.
5. `Transcode` options class bound from config (`FfmpegPath`, segment TTL seconds, handoff TTL seconds).

## Step 6 — API endpoints (`VideoEndpoints : IEndpointGroup`, group `/api/me/videos`)
- `MapPost("/{videoId:guid}/playback", …).RequireStudent()` → `PlaybackHandoffDto` (+ 403/404/409 ProblemDetails).
- `MapPost("/playback/redeem", …).RequireStudent()` → `PlaybackManifestDto` (+ 410).
- `MapGet("/{videoId:guid}/hls.key", …).RequireStudent()` → `Results.File(bytes, "application/octet-stream")` (+ 403/404).
- Auto-discovered — no `Program.cs` change. OpenAPI summaries + `Produces<>` per the contract.

## Step 7 — Migration (gated)
- `dotnet ef migrations add Phase5C_HlsKeyObjectKey -c <DbContext>` for the single `session_videos.hls_key_object_key`
  column. **Do not** auto-apply (`NFR-AVAIL-004`). Verify the up/down is the one column only.

## Step 8 — Tests (`dotnet test -c Release`)
- **Unit** (no ffmpeg): gate ordering → each of the six `reason`s; `Decrement` at zero; one-time handoff (fake store)
  rejects reuse + wrong-owner; manifest rewrite signs every segment + sets the key URI; redeem ownership check.
- **Integration** (MinIO + Redis Testcontainers, real `R2FileStorage`): full chain with a **faked `IMediaTranscoder`**
  (writes a tiny canned manifest+segment+key) — upload → enqueue → Ready → playback (decrement + `VideoPlaybackStarted`
  audited) → redeem (segments reachable in MinIO, key URI = endpoint) → key endpoint returns 16 bytes / 401 anon /
  403 staff / 404 cross-tenant; one-time code reuse → 410; exhausted → `no_views_remaining`; quiz-unpassed → `quiz_required`.
- **One opt-in integration test** runs the **real** `FfmpegMediaTranscoder` against an ffmpeg-generated 2 s `testsrc`
  clip — `[Fact(Skip…)]` unless ffmpeg is present; CI installs ffmpeg so it runs there. Asserts encrypted `.ts` +
  manifest land in MinIO and the gate serves them.

## Done = ready for wiring
- Contract §A–§H satisfied; the stub is gone; suite green (minus the known baseline image test); migration generated +
  reviewed. Hand to `IMPLEMENTATION-PLAN-phase5c-wiring.md`.
