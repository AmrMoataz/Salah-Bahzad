# Student Portal · S3 — BACKEND stream (my-sessions + session-detail reads)

> Status: **Planned — not yet built** · Created 2026-06-21 · The **engine half** of slice **S3** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S3 — the largest slice). The **video gate** (the three
> `/api/me/videos` routes: gate → redeem → AES key) and its whole pipeline **already exist** (Phase 5C) and are
> **reused verbatim** — frozen in `docs/contracts/phase5c-video-gate.md` and re-stated in
> `docs/contracts/student-s3-my-sessions-video.md` §D. This stream adds **three** new student reads:
> **`GET /api/me/sessions`**, **`GET /api/me/sessions/{id}`**, and
> **`GET /api/me/sessions/{id}/materials/{materialId}/url`**.
>
> Satisfies `FR-STU-SES-001..004` (the enrolled-content hub: progress + expiry, video playlist with access/lock state,
> materials, assignment/quiz entry points). **No new aggregate, no migration.** **Change the contract
> (`docs/contracts/student-s3-my-sessions-video.md` §A/§B/§C/§E) first if anything moves.**
>
> Gate: `dotnet test -c Release` green (the one pre-existing `QuestionBank` image-test failure is the known baseline);
> then the **wiring** stream (`IMPLEMENTATION-PLAN-student-s3-wiring.md`) proves it live on Aspire.

---

## Design reference

This stream ships **no screen**; its three JSON shapes feed the **Student Portal** prototype's **`MY SESSIONS`**
(spotlight) + **`SESSION DETAIL`** sections. The authority is `docs/contracts/student-s3-my-sessions-video.md` §A/§B/§C +
the per-caller rules in §E. The gate (`POST /api/me/videos/{id}/playback`) the **frontend** calls on Play is the
**existing 5C** endpoint — this stream does **not** touch it.

---

## 1. Frozen contract (this stream)

Implements **`docs/contracts/student-s3-my-sessions-video.md` §A/§B/§C** verbatim:

- `GET /api/me/sessions` · `RequireStudent` · `200 IReadOnlyList<MySessionDto>` · optional `state?` filter · the
  caller's **enrolled** sessions (`Active` incl. past-expiry, **exclude `Refunded`**/soft-deleted), tenant auto-scoped,
  ordered `EnrolledAtUtc` DESC, **not paginated**. Each row carries display fields + a signed `thumbnailUrl` + derived
  **progress** (§E.1) + **expiry/`state`** (§E.2).
- `GET /api/me/sessions/{id}` · `RequireStudent` · `200 MySessionDetailDto` · the full detail (header, progress, gate
  banner state, ordered video playlist with per-video `lockState`, materials names, assignment + quiz status). `{id}` is
  a **session** id; a non-enrolled / cross-tenant / refunded id → **404** (the IDOR boundary).
- `GET /api/me/sessions/{id}/materials/{materialId}/url` · `RequireStudent` · `200 SignedUrlDto` · short-lived signed R2
  URL; 404 when the session isn't an enrolled (non-refunded) one or the material isn't its. **Available while expired**,
  **not** for `Refunded`.

The three `/api/me/videos` gate routes are **untouched** (reused as-is).

## 2. Pre-flight (confirm — do NOT rebuild)

- **The 5C video gate** (`VideoEndpoints` → `StartVideoPlaybackHandler` / `RedeemPlaybackHandler` / `GetHlsKeyHandler`,
  `Features/Videos/*`) — the whole gate + transcode pipeline. **Reused as-is.** This stream does **not** edit it; the
  frontend calls `POST /api/me/videos/{videoId}/playback` directly.
- **`AttendanceProjector.WatchedByEnrollmentAsync`** (`Features/Attendance/AttendanceProjector.cs`, ~L127) — **the
  template** for `videosWatched`: `e.VideoAccesses.Count(a => a.AccessRemaining < a.AccessAllowed)`. Reuse this exact
  predicate (§E.1); do **not** invent a second one. `ListStudentAttendanceHandler` (~L80) shows the per-session
  `videoCount` via a grouped `db.SessionVideos` sub-query — mirror it.
- **`SessionDetailLoader.LoadAsync`** (`Features/Sessions/SessionDetailLoader.cs`) — **the template** for the detail: the
  `IFileStorage.GetSignedReadUrlAsync` thumbnail call, the **video ordering** (`OrderBy(v => v.Order)`), the **material
  ordering** (`OrderBy(m => m.CreatedAtUtc)`), and the grade/subject/spec **name resolution** with `IgnoreQueryFilters`.
  S3's detail differs only in: scoped to the caller's enrollment (404 otherwise), per-video **caller access counters** +
  **`lockState`**, the **gate banner** state, and the **assignment/quiz** status projections.
- **`ListSessionsHandler`** (`Features/Sessions/Queries/ListSessions`) — the name-resolution + grouped-count joins for
  the **list** read (same `IgnoreQueryFilters`-on-names pattern).
- **`GetMaterialDownloadUrlHandler`** (`Features/Sessions/Queries/GetMaterialDownloadUrl`) + the admin endpoint
  `GET /api/sessions/{id}/materials/{materialId}/url` (`SessionEndpoints`, ~L169) — **the template** for §C: same
  `SignedUrlDto`, same IDOR-safe session→material lookup, **not audited**. S3's version scopes the session by the
  caller's **enrollment** instead of `Permission.SessionsRead`.
- **The quiz-gate predicate** — `StartVideoPlaybackHandler` (~L56) + `GetHlsKeyHandler` (~L45):
  `db.UserQuizzes.FirstOrDefault(q => q.EnrollmentId == enrollment.Id)`; `quiz is not null && !quiz.Passed` →
  `quiz_required`. Copy this for `hasGatingQuiz`/`quizPassed`/`gateState` (§E.4) so the banner matches the gate.
- **Entities (read-only here):** `Enrollment` (`StudentId`, `SessionId`, `Status`, `ExpiresAtUtc`, `EnrolledAtUtc`,
  `VideoAccesses`), `EnrollmentVideoAccess` (`VideoId`, `AccessAllowed`, `AccessRemaining`), `SessionVideo` (`Order`,
  `Title`, `LengthSeconds`, `ProcessingStatus`, `AccessCount`), `SessionMaterial` (`FileName`, `ContentType`,
  `ObjectKey`, `SizeBytes`), `UserAssignment` (`EnrollmentId`, `Status`, `ScoreMarks`, `MaxMarks`, `CorrectCount`,
  `QuestionCount`, `CompletedAtUtc`), `UserQuiz` (`EnrollmentId`, `Passed`, `BestPercent`, `MinPassPercent`,
  `AttemptsUsed`, `AttemptCount`, `TimeLimitMinutes`, `QuestionCount`).
- **`ICurrentUserResolver`** — `.UserId` = student id, `.TenantId` = tenant (as `RedeemCodeHandler` /
  `GetMyAssignmentHandler` / `ListCatalogueHandler` use them). **`TimeProvider clock`** for `now` in the expiry
  derivation (§E.2) — never `DateTimeOffset.UtcNow`.
- **`RequireStudentExtensions.RequireStudent()`** (`Api/Authorization/RequireStudent.cs`) — anon → 401, staff → 403.
  Every S3 route uses it, like `/api/me/catalogue`.

## 3. Application — three queries (`Features/Sessions/Queries/…`)

Keep them next to `ListCatalogue` / `ListSessions` / `GetSessionById` so the shared name-resolution + signed-URL helpers
are obvious. All three resolve the caller via `ICurrentUserResolver.UserId`; tenant + soft-delete are the global filter.

### 3.1 `ListMySessions`
- `ListMySessionsQuery(MySessionState? State) : IRequest<IReadOnlyList<MySessionDto>>` (`MySessionState` = the §A.1 enum;
  treat an unknown value as "no filter"). **No validator needed.**
- `ListMySessionsHandler(IAppDbContext db, ICurrentUserResolver currentUser, IFileStorage fileStorage, TimeProvider clock)`:
  1. `var studentId = currentUser.UserId; var now = clock.GetUtcNow();`
  2. Load the caller's **non-refunded** enrollments: `db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId
     && e.Status != EnrollmentStatus.Refunded)` (tenant + soft-delete auto) **including** `VideoAccesses` (for the
     watched count) — or project the watched count inline like `WatchedByEnrollmentAsync`. `OrderByDescending(e =>
     e.EnrolledAtUtc)`.
  3. Join the sessions (`db.Sessions` by `SessionId`) for title + grade/subject/spec **ids**; resolve **names** with the
     `IgnoreQueryFilters` dictionaries (mirror `ListSessionsHandler`). Compute **`videoCount`** per session (grouped
     `db.SessionVideos`) and **`videosWatched`** per enrollment (the `AccessRemaining < AccessAllowed` count).
  4. Per row derive **`isExpired`** (`ExpiresAtUtc != null && ExpiresAtUtc <= now`), **`progressPercent`** (§E.1), and
     **`state`** (`Completed`/`InProgress`/`NotStarted`, §E.2 — completion only).
  5. **Apply the `state` filter** (§A.1) if provided: `InProgress`/`Completed`/`NotStarted` match the derived `state` on
     non-expired rows; `Expired` → `isExpired`; `ExpiringSoon` → `!isExpired && ExpiresAtUtc != null && ExpiresAtUtc <=
     now + 14d`. *(Filter in memory after projection, or push into the query — the enrolled set is small.)*
  6. **`thumbnailUrl`:** short-lived signed URL per session with a `ThumbnailObjectKey` (the `SessionDetailLoader` call);
     null key → null.
  7. `return rows.Select(r => r.ToMyDto(…)).ToList();`

### 3.2 `GetMySession`
- `GetMySessionQuery(Guid SessionId) : IRequest<MySessionDetailDto>`.
- `GetMySessionHandler(IAppDbContext db, ICurrentUserResolver currentUser, IFileStorage fileStorage, TimeProvider clock)`:
  1. Resolve the caller's **non-refunded** enrollment for `SessionId`:
     `db.Enrollments.FirstOrDefaultAsync(e => e.StudentId == studentId && e.SessionId == SessionId && e.Status !=
     EnrollmentStatus.Refunded)` → **`throw new NotFoundException(...)`** if null (the §B.2 404 — the IDOR/tenant
     boundary; the global filter already excludes other tenants/soft-deleted, so a cross-tenant id is naturally null).
  2. Load the session (+ `Videos`, `Materials`) — reuse `SessionDetailLoader`'s load shape; resolve names with
     `IgnoreQueryFilters`; thumbnail signed URL.
  3. `videosWatched`/`videoCount` + `isExpired` + `progressPercent` (as 3.1).
  4. **Gate:** `var quiz = await db.UserQuizzes.AsNoTracking().FirstOrDefaultAsync(q => q.EnrollmentId == enrollment.Id);`
     → `hasGatingQuiz = quiz is not null`, `quizPassed = quiz?.Passed == true`, `minPassPercent = quiz?.MinPassPercent
     ?? 0`; `gateState` per §E.4 (`Expired` → `QuizRequired` → `Open`).
  5. **Videos:** order by `Order`; per video pull the caller's `EnrollmentVideoAccess` (`enrollment.VideoAccesses`
     by `VideoId`) for `accessAllowed`/`accessRemaining`; compute **`lockState`** per the §E.3 order (Expired →
     QuizLocked → NotReady → Exhausted → Playable). Map to `MySessionVideoDto`.
  6. **Materials:** order by `CreatedAtUtc`; map to `MySessionMaterialDto` (no URL here — `kind` = upper-case extension,
     same as `SessionDetailLoader`).
  7. **Assignment:** `db.UserAssignments.FirstOrDefaultAsync(a => a.EnrollmentId == enrollment.Id)` → `MyAssignmentStatusDto`
     (null only if no snapshot). **Quiz:** the `quiz` from step 4 → `MyQuizStatusDto` (null when `!hasGatingQuiz`).
  8. `return detail.ToMyDetailDto(…)`.

### 3.3 `GetMyMaterialUrl`
- `GetMyMaterialUrlQuery(Guid SessionId, Guid MaterialId) : IRequest<SignedUrlDto>`.
- `GetMyMaterialUrlHandler(IAppDbContext db, ICurrentUserResolver currentUser, IFileStorage fileStorage)`:
  mirror `GetMaterialDownloadUrlHandler` but gate on the **caller's non-refunded enrollment** for `SessionId` (404 if
  none), then load the `SessionMaterial` by `(SessionId, MaterialId)` (404 if not its), then
  `fileStorage.GetSignedReadUrlAsync(material.ObjectKey)` → `SignedUrlDto`. **No audit.** *(Materials stay available while
  the enrollment is `Active`-but-expired — do not add an expiry gate here; `FR-STU-SES-001`.)*

### 3.4 DTOs + mappings
`MySessionDto`, `MySessionDetailDto`, `MySessionVideoDto`, `MySessionMaterialDto`, `MyAssignmentStatusDto`,
`MyQuizStatusDto` in `Features/Sessions/DTOs/SessionDtos.cs` (beside `CatalogueSessionDto`/`SessionDetailDto`) — field
order = the contract §A.2/§B.1 shapes. `SignedUrlDto` already exists (reuse). Manual `.ToMyDto()` / `.ToMyDetailDto()` /
`.ToMyVideoDto()` extensions — **no mapping library**, never map in a handler body.

## 4. API — endpoint group

New `MeSessionsEndpoints : IEndpointGroup` (auto-discovered, mirrors `MeCatalogueEndpoints`/`VideoEndpoints`):
```csharp
var group = app.MapGroup("/api/me/sessions").WithTags("My Sessions").WithOpenApi();

group.MapGet("/", ListMySessionsAsync)
    .RequireStudent().WithName("ListMySessions")
    .WithSummary("List the caller's enrolled sessions with progress + expiry")
    .Produces<IReadOnlyList<MySessionDto>>();

group.MapGet("/{id:guid}", GetMySessionAsync)
    .RequireStudent().WithName("GetMySession")
    .WithSummary("One enrolled session's detail: playlist, materials, assignment & quiz status")
    .Produces<MySessionDetailDto>()
    .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

group.MapGet("/{id:guid}/materials/{materialId:guid}/url", GetMyMaterialUrlAsync)
    .RequireStudent().WithName("GetMySessionMaterialUrl")
    .WithSummary("Short-lived signed URL for one material of an enrolled session")
    .Produces<SignedUrlDto>()
    .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
```
Handlers are thin `ISender.Send(...)` delegates (the `[FromQuery] string? state` on the list maps to the
`MySessionState?`; parse leniently — unknown → no filter). `RequireStudent()` gives the 401/403; the list can't 404.

## 5. Migration

**None.** `Session`, `SessionVideo`, `SessionMaterial`, `Enrollment`, `EnrollmentVideoAccess`, `UserAssignment`,
`UserQuiz` all exist. Pure reads. *(The only S-phase video migration — `session_videos.hls_key_object_key` — shipped in
5C.)*

## 6. Tests (`dotnet test -c Release`)

Integration (`WebApplicationFactory` + Testcontainers, **Student-role JWT** — reuse the catalogue/assignment student
principal helper). Seed via the redeem engine or directly.

- **My-sessions scope + shape:** a student with **`Active`**, **past-expiry-`Active`**, and **`Refunded`** enrollments →
  `GET /api/me/sessions` returns the first two (refunded **absent**), DESC by `EnrolledAtUtc`, with grade/subject/spec
  names, `videoCount`, a `thumbnailUrl` when keyed (null otherwise), and `enrollmentId`.
- **Progress derivation (§E.1) — the key projection:** spend views via the gate (or set `AccessRemaining < AccessAllowed`
  directly) → `videosWatched` counts only the spent-view accesses; `progressPercent` = round(100×watched/count);
  `videoCount == 0` → `progressPercent == 0`, `state == NotStarted`. All-watched → `state == Completed`.
- **Expiry + `state` (§E.2):** an `Active` row with `ExpiresAtUtc` in the **past** → `isExpired == true` (proves it's
  **derived**, `Status` stays `Active`); a no-expiry session (`ExpiresAtUtc == null`) → `isExpired == false`. The
  `?state=` filter narrows: `Expired` returns only expired, `ExpiringSoon` returns only `≤14d` non-expired, `Completed`/
  `InProgress`/`NotStarted` match the derived completion.
- **Detail happy path (§B.1):** `GET /api/me/sessions/{id}` for an enrolled session → videos **ordered by `Order`** with
  per-video `accessAllowed`/`accessRemaining` + `lockState`; materials ordered by `CreatedAtUtc`; `assignment` populated;
  `quiz` populated **iff** quiz-gated.
- **Per-video `lockState` (§E.3) — across states:** assert the precedence on one session: when **expired** → all videos
  `Expired`; when **quiz-gated + not passed** → all `QuizLocked`; a `Ready` video with `accessRemaining == 0` →
  `Exhausted`; with `accessRemaining > 0` → `Playable`; a `Processing` video → `NotReady`. **Mirror the 5C gate order**
  (expired/quiz beat per-video; a `Playable` badge ⇔ the gate would pass).
- **Gate banner (§E.4):** `gateState == Expired` when expired; `QuizRequired` + `minPassPercent` set when the gating quiz
  is unpassed; `Open` otherwise. `hasGatingQuiz`/`quizPassed` match the 5C `quiz_required` predicate (prove against the
  same fixture the gate uses).
- **404 IDOR/tenant boundary (§B.2/§C):** a session the caller is **not** enrolled in → `404` (not the data); a
  **`Refunded`**-only enrollment → `404`; a session in **another tenant** → `404`; `GET …/materials/{mid}/url` for a
  material **not** belonging to an enrolled session → `404`.
- **Material URL (§C):** an enrolled (`Active`) session → `200 SignedUrlDto`; the **same** session once **expired** →
  still `200` (materials stay available, `FR-STU-SES-001`); a `Refunded` enrollment → `404`. **Not audited** (assert no
  new `audit_entries` row for the read — parity with the admin material read).
- **Tenant isolation (`NFR-SEC-010`):** a student of tenant A never sees tenant B's sessions in the list, and `GET
  /api/me/sessions/{B-session}` → `404`.
- **Auth gating:** anonymous → `401`; **staff** JWT → `403`; Student JWT → `200`.
- **Gate untouched (regression):** the 5C `VideoEndpoints` tests still pass (this stream adds no change there).

## Done = ready for wiring

Contract §A/§B/§C satisfied; the 5C gate untouched; suite green (minus the known baseline image test); **no migration**.
Hand to `IMPLEMENTATION-PLAN-student-s3-wiring.md`.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the BACKEND stream of Student-Portal phase S3 for Salah Bahzad (.NET 10, Clean Architecture +
CQRS + source-gen Mediator). Edit backend/** ONLY. Add THREE student READ endpoints. The Phase-5C video gate (the three
/api/me/videos routes) already exists and is REUSED AS-IS — do not touch it. NO migration.

Read first, in order:
1. backend/CLAUDE.md (Multi-tenancy, EF query filters, Minimal API, Testing).
2. docs/contracts/student-s3-my-sessions-video.md — the FROZEN contract: §A (GET /api/me/sessions + MySessionDto), §B
   (GET /api/me/sessions/{id} + MySessionDetailDto/MySessionVideoDto/MySessionMaterialDto/MyAssignmentStatusDto/
   MyQuizStatusDto + the 404 IDOR boundary), §C (the material signed-URL read), §D (the 5C gate — reused), §E (the
   progress / expiry+state / per-video lockState / gateState derivations). Change the contract first if anything moves.
3. The templates to mirror: Application/Features/Attendance/AttendanceProjector.cs (WatchedByEnrollmentAsync —
   AccessRemaining < AccessAllowed; reuse this exact predicate), Application/Features/Sessions/SessionDetailLoader.cs
   (thumbnail signed URL + video/material ordering + name resolution with IgnoreQueryFilters),
   Application/Features/Sessions/Queries/GetMaterialDownloadUrl/GetMaterialDownloadUrlHandler.cs (the material-URL
   template), Application/Features/Videos/Commands/StartVideoPlayback/StartVideoPlaybackHandler.cs (the quiz-gate
   predicate: UserQuiz for the enrollment, !Passed => quiz_required — copy it for hasGatingQuiz/quizPassed/gateState),
   Api/Endpoints/MeCatalogueEndpoints.cs (the /api/me/* + RequireStudent endpoint-group shape), and ICurrentUserResolver
   (.UserId = studentId, .TenantId) + TimeProvider for now.

Build: NEW ListMySessions, GetMySession, GetMyMaterialUrl queries/handlers under Features/Sessions/Queries; the
MySession* DTOs + .ToMy*Dto() mappings beside CatalogueSessionDto/SessionDetailDto; My Sessions = the caller's enrollments
where Status != Refunded (Active incl. past-expiry), DESC by EnrolledAtUtc; derive isExpired (ExpiresAtUtc <= now, NOT
Status), progressPercent (watched/count), state (NotStarted/InProgress/Completed = completion only), per-video lockState
(Expired -> QuizLocked -> NotReady -> Exhausted -> Playable), gateState (Expired -> QuizRequired -> Open); GetMySession
404s on a non-enrolled/refunded/cross-tenant session id; material URL 404s likewise, stays available while expired, NOT
audited. Wire a new MeSessionsEndpoints : IEndpointGroup (RequireStudent). DO NOT paginate; DO NOT touch the 5C gate.

Tests (xUnit v3 + Testcontainers + FluentAssertions, Student-role JWT): scope (active incl. expired, exclude refunded) +
shape + DESC; progress derivation (watched < allowed, count==0 edge); isExpired DERIVED + the ?state= filter; detail
ordering + assignment/quiz population; per-video lockState across expired/quiz/notready/exhausted/playable matching the
5C gate order; gateState; the 404 IDOR/tenant boundary on detail + material; material URL 200 active / 200 expired / 404
refunded + not-audited; cross-tenant isolation; 401 anon / 403 staff / 200 student. Green gate: `dotnet test -c Release`
(the one pre-existing QuestionBank image test may stay red — baseline). Report it.
```
