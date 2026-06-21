# FROZEN CONTRACT — Student Portal · S3 · My sessions, session detail & secure video

> Status: **Frozen** · Created 2026-06-21 · Slice: Student-Portal **S3** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S3 — the **largest** slice). **Design anchor:** the prototype's
> **`MY SESSIONS`** section (the **`spotlight`** layout only) + the **`SESSION DETAIL`** section in
> `.claude/Salah Bahzad Student Portal/Student Portal.html`. Behaviour authority is `FR-STU-SES-001..004`,
> `FR-STU-VID-001..005`, `FR-PLAT-VID-001..007` (the video gate, frozen separately in
> `docs/contracts/phase5c-video-gate.md`), and `FR-PLAT-ENR-003` (validity window).
>
> Satisfies: the enrolled-content hub with progress + expiry countdown, after which **videos and the quiz lock while
> the assignment stays open** (`FR-STU-SES-001`); the ordered **video playlist** with per-video remaining access + lock
> state (`FR-STU-SES-002`); **materials** read via signed URL (`FR-STU-SES-003`); the **assignment & quiz entry points**
> (`FR-STU-SES-004`); and the **deep-link Play** that fires the gate then hands off to the native app
> (`FR-STU-VID-001/003/004`). **The video gate already exists** — Phase 5C shipped the three `/api/me/videos` routes
> (gate → one-time handoff → redeem → AES key), proven live. This contract **freezes that gate as-is** (§D, reused
> verbatim) and adds **two** new student reads — **`GET /api/me/sessions`** + **`GET /api/me/sessions/{id}`** — and
> **one** new student signed-URL read — **`GET /api/me/sessions/{id}/materials/{materialId}/url`**. Frontend + wiring
> cite this file field-for-field. **Change this file first if anything moves.**

## 0. Ground rules

- **Backend = two new reads + one material-URL read + the frozen 5C gate.** The deliverable screens are **My Sessions**
  and **Session detail**; the backend adds **only** `GET /api/me/sessions`, `GET /api/me/sessions/{id}`, and
  `GET /api/me/sessions/{id}/materials/{materialId}/url`. The **playback gate** (`POST /api/me/videos/{videoId}/playback`
  and its redeem + key siblings) is **reused verbatim** from Phase 5C (`docs/contracts/phase5c-video-gate.md`) — no
  signature change. **No new aggregate, no migration** (`Session`, `SessionVideo`, `SessionMaterial`, `Enrollment`,
  `EnrollmentVideoAccess`, `UserAssignment`, `UserQuiz`, `Attendance` all exist).
- **Authenticated student surface.** Every endpoint uses **`RequireStudent()`** (anon → 401, staff → 403) — identical to
  `/api/me/catalogue|assignments|quizzes|videos`. **The student id + tenant come from the JWT**
  (`ICurrentUserResolver.UserId` / `.TenantId`), never a URL id. The `{id}` in `/api/me/sessions/{id}` is a **session**
  id whose ownership is proven by the caller's own enrollment row — a non-enrolled / cross-student session id resolves to
  **404**, not the other student's data (no IDOR surface, `NFR-SEC-007`).
- **Tenant isolation is automatic.** The EF global query filter scopes `Sessions`/`Enrollments` to the caller's tenant
  and excludes soft-deleted rows. **Never** write a per-handler `Where(x => x.TenantId == …)`. Cross-tenant isolation is
  covered by an integration test on each new read (`NFR-SEC-010`).
- **My Sessions = the caller's *enrolled* sessions** (`FR-STU-SES-001`). Both reads load the caller's **own**
  `Enrollment` rows (`StudentId == currentUser.UserId`), **excluding `Refunded`** (access reversed → back to the
  catalogue) and soft-deleted. **`Active` rows whose `ExpiresAtUtc` is in the past are still returned** — they surface as
  **expired** (videos+quiz locked, assignment open), they do **not** disappear (the prototype's `expired_done` /
  `expired_incomplete` rows). This is the catalogue's mirror image: the catalogue derives `Expired` to offer **re-enroll**;
  My Sessions derives `Expired` to keep the **assignment reachable**.
- **Progress is derived, never stored.** "Videos watched" = the count of the caller's `EnrollmentVideoAccess` rows for the
  session with a **spent** view (`AccessRemaining < AccessAllowed`) — the **single source of truth the 5C gate
  decrements** (no stored counter, auto-resets on re-enroll). This is the exact predicate the admin attendance projector
  uses (`AttendanceProjector.WatchedByEnrollmentAsync`); S3 reuses it. `progressPercent = videoCount == 0 ? 0 :
  round(100 × videosWatched / videoCount)`.
- **The playlist's lock state mirrors the gate, but the gate stays authoritative.** Each video's `lockState` (§E.3) is
  computed in the **same order** the 5C gate authorizes (`StartVideoPlaybackHandler`), so the badge **predicts** the
  Play result — but pressing Play still calls the real gate, which remains the security boundary (a forced Play on a
  locked video returns the gate's `reason`, surfaced verbatim).
- **Reads are not audited; the Play gate is.** All three S3 reads are pure reads — **not** audited (parity with
  `/api/me/catalogue` and the admin material-URL read: a session thumbnail and a session material are low-sensitivity,
  not minors' PII — unlike the audited private ID-image read). The **Play gate (#1)** writes the audited
  **`VideoPlaybackStarted`** (Student) — **already implemented in 5C**; the frontend treats Play as a state change
  (decrements a view) and must not double-fire it.
- **Money & enums over the wire.** Enums are **string names** (`JsonStringEnumConverter`) — the frontend models them as
  string unions. Dates are ISO-8601 `…AtUtc`. Durations are integer **seconds** (`lengthSeconds`), rendered `MM:SS` by
  the UI (the 5C follow-up that replaced manual length input with ffprobe).

## A. My sessions — `GET /api/me/sessions` (NEW · `RequireStudent`)

`RequireStudent` · `200 IReadOnlyList<MySessionDto>`. Returns the caller's enrolled sessions (`Active` incl. past-expiry;
**not** `Refunded`/soft-deleted), each with progress + expiry + a derived state, ordered so the **most-recently enrolled**
is first (`EnrolledAtUtc` DESC). **Not paginated** (a student's enrolled set is small — the prototype's spotlight + list).

### A.1 Query parameters (all optional — the prototype's filter chips)

| Param | Type | Notes |
|---|---|---|
| `state` | string? | `"InProgress" \| "Completed" \| "ExpiringSoon" \| "Expired"` — narrows to one filter chip. Omitted → all. |

> The prototype filters **client-side** over the loaded set (the chip-bar: All / In progress / Expiring soon / Completed /
> Expired). So the happy path calls this endpoint **with no params** and the frontend filters in the browser; the `state`
> param exists for completeness/tests and **must** be honoured server-side. `ExpiringSoon` = `Active`, not expired,
> `ExpiresAtUtc` within **14 days** (the prototype's `expiresDays <= 14` warning threshold).

### A.2 Result — `MySessionDto` (shaped to the prototype's spotlight hero + list row)

```jsonc
// 200 · IReadOnlyList<MySessionDto> — ordered by EnrolledAtUtc DESC
{
  "id": "guid",                          // the SESSION id (the route param of §B)
  "enrollmentId": "guid",                // the caller's enrollment (for client correlation; never trust client ids server-side)
  "title": "string",
  "gradeName": "string|null",
  "subjectName": "string|null",          // derived via the specialization
  "specializationName": "string|null",
  "thumbnailUrl": "string|null",         // short-lived signed R2 URL (same pattern as catalogue/detail); null if no thumbnail
  // progress (§E.1)
  "videoCount": 0,                       // total session videos
  "videosWatched": 0,                    // EnrollmentVideoAccess rows with AccessRemaining < AccessAllowed
  "progressPercent": 0,                  // round(100 × videosWatched / videoCount); 0 when videoCount == 0
  // expiry (§E.2)
  "enrolledAtUtc": "…",
  "expiresAtUtc": "…|null",              // null == no-expiry session (ValidityDays == 0)
  "isExpired": false,                    // DERIVED: ExpiresAtUtc != null && ExpiresAtUtc <= now
  "state": "InProgress"                  // "NotStarted" | "InProgress" | "Completed" (completion only — see §E.2); combine with isExpired in the UI
}
```

### A.3 Error modes — ProblemDetails

| Status | When |
|---|---|
| `401` | No bearer (anonymous). |
| `403` | A **staff** JWT (the `RequireStudent` filter). |
| `200` | Always otherwise — an empty `[]` when the caller has no (non-refunded) enrollments (the UI shows the mascot empty state). |

## B. Session detail — `GET /api/me/sessions/{id}` (NEW · `RequireStudent`)

`RequireStudent` · `200 MySessionDetailDto`. The full study view for **one** enrolled session: header, progress, the
gate banner state, the ordered **video playlist** with per-video lock state + remaining access, the **materials** list
(names only — bytes via §C), and the **assignment** + **quiz** entry status. `{id}` is the **session** id; ownership is
the caller's own enrollment.

### B.1 Result — `MySessionDetailDto`

```jsonc
// 200 · MySessionDetailDto
{
  "id": "guid", "title": "string", "description": "string|null",
  "gradeId": "guid",   "gradeName": "string|null",
  "subjectId": "guid", "subjectName": "string|null",
  "specializationId": "guid", "specializationName": "string|null",
  "thumbnailUrl": "string|null",
  // enrollment + progress (§E.1/§E.2)
  "enrollmentId": "guid",
  "enrolledAtUtc": "…", "expiresAtUtc": "…|null", "isExpired": false,
  "videoCount": 0, "videosWatched": 0, "progressPercent": 0,
  // gate banner (§E.4)
  "gateState": "Open",                   // "Open" | "QuizRequired" | "Expired"
  "hasGatingQuiz": false,                // a UserQuiz exists for this enrollment (the session is quiz-gated)
  "quizPassed": false,                   // UserQuiz.Passed (false when !hasGatingQuiz)
  "minPassPercent": 0,                   // the gate's pass mark (for the banner copy "… (60%) …"); 0 when !hasGatingQuiz
  // collections
  "videos": [ /* MySessionVideoDto, ordered by Order asc */ ],
  "materials": [ /* MySessionMaterialDto, ordered by CreatedAtUtc asc */ ],
  "assignment": { /* MyAssignmentStatusDto */ } | null,   // null only if the session has no assignment snapshot
  "quiz": { /* MyQuizStatusDto */ } | null                // null when the session is not quiz-gated
}
```

```jsonc
// MySessionVideoDto — one playlist row
{
  "id": "guid", "title": "string", "order": 0,
  "lengthSeconds": 0,                    // 0 until ProcessingStatus == Ready (ffprobe-computed)
  "processingStatus": "Ready",           // "Pending" | "Processing" | "Ready" | "Failed"
  "accessAllowed": 0, "accessRemaining": 0,   // the caller's EnrollmentVideoAccess for this video
  "lockState": "Playable"                // "Playable" | "QuizLocked" | "Expired" | "Exhausted" | "NotReady" — see §E.3
}
```

```jsonc
// MySessionMaterialDto — names only; bytes fetched via §C
{ "id": "guid", "fileName": "string", "kind": "PDF", "sizeBytes": 0 }   // kind = upper-case extension label
```

```jsonc
// MyAssignmentStatusDto — reachable even when the session is expired (FR-STU-SES-001)
{ "userAssignmentId": "guid", "status": "InProgress",   // "InProgress" | "Completed"
  "scoreMarks": 0|null, "maxMarks": 0, "correctCount": 0|null, "questionCount": 0,
  "completedAtUtc": "…|null" }
```

```jsonc
// MyQuizStatusDto — the gating quiz (null in the parent when the session is not quiz-gated)
{ "userQuizId": "guid", "passed": false, "bestPercent": 0|null,
  "minPassPercent": 0, "attemptsUsed": 0, "attemptCount": 0,   // attemptCount = total attempts allowed
  "timeLimitMinutes": 0, "questionCount": 0 }
```

### B.2 Error modes — ProblemDetails

| Status | When |
|---|---|
| `401` | No bearer. |
| `403` | A **staff** JWT (`RequireStudent`). |
| `404` | `{id}` is not a session the caller is enrolled in (unknown id, **other tenant**, or only a **`Refunded`**/soft-deleted enrollment) — the IDOR/tenant boundary. |
| `200` | The caller has a non-refunded enrollment for the session. |

## C. Material signed URL — `GET /api/me/sessions/{id}/materials/{materialId}/url` (NEW · `RequireStudent`)

`RequireStudent` · `200 SignedUrlDto`. Issues a **short-lived signed R2 URL** to read/download one material of an
enrolled session (`FR-STU-SES-003`). Mirrors the **admin** `GET /api/sessions/{id}/materials/{materialId}/url`
(`GetMaterialDownloadUrlHandler`) — same `SignedUrlDto`, same `IFileStorage.GetSignedReadUrlAsync`, **not audited**
(materials are readings, not minors' PII).

```jsonc
// 200 · SignedUrlDto
{ "url": "https://…", "expiresAtUtc": "…" }
```

| Status | When |
|---|---|
| `401`/`403` | Anon / staff. |
| `404` | The session isn't one the caller is enrolled in (non-refunded), **or** `materialId` isn't a material of that session. |
| `200` | Otherwise — the signed URL. **Available while expired** (materials, like the assignment, stay reachable after expiry — `FR-STU-SES-001`); **not** available for a `Refunded` enrollment. |

## D. Video Play gate — `POST /api/me/videos/{videoId}/playback` (EXISTS — frozen as-is, Phase 5C #1)

`RequireStudent` · returns `200 PlaybackHandoffDto`. **No change in S3** — documented so the frontend builds to it
exactly. The student + tenant come from the JWT; the handler authorizes → **decrements** one view → audits → issues a
**one-time handoff code**. **The browser calls only this route**; the redeem (#2) and AES-key (#3) routes are the
**native app's** job (frozen in `phase5c-video-gate.md` §B) — the portal **never** receives a manifest, URL, or key.

### D.1 Result — `PlaybackHandoffDto`

```jsonc
// 200 · the deep-link payload — the raw token/URL is NEVER returned here (FR-PLAT-VID-005)
{ "handoffCode": "5f3c…", "expiresAtUtc": "…" }   // code is one-time, ~60 s TTL
```

### D.2 Gate reason codes (frozen — from `StartVideoPlaybackHandler`) — each a specific UI failure (`FR-STU-VID-004`)

| Status | Machine `reason` | Readable `detail` (render it) | Cause |
|---|---|---|---|
| `409` | `not_ready` | "This video is still being prepared." | `ProcessingStatus != Ready`. |
| `403` | `not_enrolled` | "You are not enrolled in this session." | No active enrollment for the video's session. |
| `403` | `enrollment_expired` | "Your access to this session has expired." | `ExpiresAtUtc <= now`. |
| `403` | `quiz_required` | "Pass the prerequisite quiz to unlock this video." | Gating `UserQuiz` exists and `!Passed`. |
| `403` | `no_views_remaining` | "You have no views left for this video." | `EnrollmentVideoAccess.AccessRemaining <= 0`. |
| `404` | — | (not found) | Unknown / cross-tenant `videoId` (IDOR). |

> **Frozen:** the path, verb, `PlaybackHandoffDto` shape, the five `reason` codes + statuses, decrement-at-gate, and the
> one-time handoff (never a raw URL at #1). The **deep link** the frontend builds from the handoff
> (`salah-bahazad://stream?…&handoff=<code>`) is **frontend-owned** (§E.5) — the backend only returns the code. The
> `reason` strings above are the **server's** `detail`; the frontend renders `problem.detail` (already user-safe) so
> "every failure shows a specific reason + remaining-count feedback" (`FR-STU-VID-004`) is satisfied — the playlist's
> per-video `accessRemaining` (§B.1) supplies the count.

## E. Per-caller computation (frozen rules the new reads must implement)

### E.1 Progress (`FR-STU-SES-001/002`) — reuse the attendance projector's predicate exactly

For each session, `videosWatched` = count of the caller's `EnrollmentVideoAccess` rows (joined via the caller's
`Enrollment` for that session) where **`AccessRemaining < AccessAllowed`** (≥ 1 view spent). `videoCount` = the session's
`SessionVideo` count. `progressPercent = videoCount == 0 ? 0 : (int)Math.Round(100.0 × videosWatched / videoCount)`. This
is `AttendanceProjector.WatchedByEnrollmentAsync` — **do not invent a second derivation**; the gate's decrement is the
only writer, so progress auto-resets when a re-enroll resets the counters (`FR-PLAT-ENR-004`).

### E.2 Expiry + `state` (`FR-STU-SES-001`, `FR-PLAT-ENR-003`)

- `isExpired` = `ExpiresAtUtc != null && ExpiresAtUtc <= clock.GetUtcNow()` — **derived**, exactly like the catalogue
  (`Enrollment.Status` is never flipped to `Expired` by the writer; `EnrollmentStatus.Expired` is unused). Use the
  handler's injected `TimeProvider`.
- `state` describes **completion only** (independent of expiry, so the UI can show e.g. "Completed" + an "Expired" chip
  — the prototype's `expired_done`): `Completed` iff `videoCount > 0 && videosWatched == videoCount`; else `NotStarted`
  iff `videosWatched == 0`; else `InProgress`.
- The `state=ExpiringSoon` **filter** (§A.1) selects `!isExpired && ExpiresAtUtc != null && ExpiresAtUtc <= now + 14d`.
  *(`ExpiringSoon` is a filter predicate, not a value of the `state` field — the field carries completion; expiry rides
  `isExpired` + `expiresAtUtc`.)*

### E.3 Per-video `lockState` (`FR-STU-SES-002`) — mirror the 5C gate order

Computed per video so the badge **predicts** the gate (§0). First matching rule wins:

| Order | `lockState` | When | Prototype badge |
|---|---|---|---|
| 1 | `Expired` | the session `isExpired` (locks **all** videos; `FR-STU-SES-001`) | "Locked" (grey) |
| 2 | `QuizLocked` | `hasGatingQuiz && !quizPassed` (locks **all** videos until the quiz passes) | "Locked" (grey) |
| 3 | `NotReady` | `processingStatus != Ready` (still transcoding — rare once published) | "Locked" (grey) |
| 4 | `Exhausted` | `accessRemaining == 0` | "0 views left" (red) |
| 5 | `Playable` | otherwise | "{accessRemaining} of {accessAllowed} views" (green) |

> Rules 1–2 are **session-level** (they drive the gate banner, §E.4, and lock every video); rules 3–4 are **per-video**.
> The 5C gate's own order is `not_ready → not_enrolled → enrollment_expired → quiz_required → no_views_remaining`; the
> caller here **is** enrolled (it's their session), so `not_enrolled` can't occur, and the display surfaces the
> student-meaningful reasons (expired/quiz) before the operational one (not-ready) — the gate remains authoritative on
> Play, and a `Playable` badge is exactly the set that passes the gate.

### E.4 Gate banner `gateState` (`FR-STU-SES-001/004`)

`Expired` if `isExpired`; else `QuizRequired` if `hasGatingQuiz && !quizPassed`; else `Open`. The frontend renders the
mascot-forward banner from this (`QuizRequired` → the prototype's "Pass the prerequisite quiz (`{minPassPercent}`%) to
unlock the remaining locked videos. Your assignment is available now."; `Expired` → videos+quiz locked, assignment open).
`hasGatingQuiz` = a `UserQuiz` exists for the caller's enrollment (`db.UserQuizzes.Any(q => q.EnrollmentId == …)`);
`quizPassed` = that `UserQuiz.Passed` (`BestPercent >= MinPassPercent`, the `≥` fix). This is **the same predicate the
5C gate uses** for `quiz_required` (`StartVideoPlaybackHandler`/`GetHlsKeyHandler`).

### E.5 The deep-link Play handoff (frontend-owned — recorded so it isn't reinvented)

On Play the frontend `POST`s the gate (§D), and on `200` builds
`salah-bahazad://stream?videoId={videoId}&sessionId={sessionId}&handoff={handoffCode}` and navigates to it (the native
app authenticates, calls redeem + key, and plays with OS black-out + the name watermark). If no app handles the scheme
(detected via a visibility/blur timeout), the portal shows an **install prompt** (store/download links). **No in-browser
HLS player, no manifest, no AES key in the browser** (master plan §8.4). On a gate failure the `reason` `detail` (§D.2)
renders inline with the video's remaining-views count.

## F. Audit (`FR-PLAT-AUD-002`, `FR-PLAT-VID-002`)

- `GET /api/me/sessions`, `GET /api/me/sessions/{id}`, `GET /api/me/sessions/{id}/materials/{materialId}/url` — **pure
  reads, not audited** (parity with `/api/me/catalogue` and the admin material-URL read; the signed thumbnail + material
  URLs are low-sensitivity, not the audited private ID-image read).
- `POST /api/me/videos/{videoId}/playback` — **audited** (`VideoPlaybackStarted`, `ActorType=Student`,
  `EntityType=SessionVideo`, "Watched: {title}") — **already implemented in 5C**; the frontend treats Play as a state
  change (it decrements a view) and must not double-fire it.

## G. Deferred / **NOT built** (master plan §3.3 / §7 / §8.4)

- **In-browser video playback** — the FR-STU-VID-005 watermarked-browser interim is **deliberately not built**; the
  portal ships **no** HLS player (deep-link only). Mobile-web video waits for the native app. **No** AES key ever
  reaches a browser.
- **The native-app concerns** — the in-player **watermark** (`FR-STU-VID-002/004`) and OS **screenshot/recording
  black-out** (`FR-PLAT-VID-004/005`) live in the native/desktop app; 5C delivered the backend half (the one-time
  handoff code the deep link carries). The portal only **fires the gate and deep-links**.
- **The `cards` / `rail` My-Sessions layouts** — the prototype's `sessionLayout` demo enum (`spotlight | cards | rail`,
  default `spotlight`) is a designer toggle; **only `spotlight` is built** (master plan §3.3), the enum is dropped.
- **The redeem (#2) + key (#3) routes** are not called by the portal (native-app surface, frozen in 5C). No change to
  the gate, the transcode pipeline, or any 5C route.

## H. Frozen vs. stream-owned

- **Frozen (this file):** the `GET /api/me/sessions` + `GET /api/me/sessions/{id}` paths, `RequireStudent`, the `state`
  filter (§A.1), the `MySessionDto` / `MySessionDetailDto` / `MySessionVideoDto` / `MySessionMaterialDto` /
  `MyAssignmentStatusDto` / `MyQuizStatusDto` field names + types + ordering (§A.2/§B.1), the **enrolled-set scope**
  (Active incl. past-expiry, exclude Refunded/soft-deleted, §0), the **404** IDOR/tenant boundary on detail + material
  (§B.2/§C), the material-URL path + `SignedUrlDto` (§C), the **progress** derivation (§E.1), the **expiry/`state`**
  derivation (§E.2), the per-video **`lockState`** order (§E.3), the **`gateState`** rule (§E.4); the **Play gate**
  path/`PlaybackHandoffDto`/the five `reason` codes + statuses + decrement-at-gate (§D, reused from 5C); "reads not
  audited, Play audited" (§F); the deep-link-only / no-browser-player stance (§G).
- **Backend owns:** the query folders/names (`Features/Sessions/Queries/ListMySessions/` + `GetMySession/` +
  `GetMyMaterialUrl/` — implementer's call, keep routes + DTOs frozen), the DTO + `.ToMy*Dto()` mapping locations, the
  `MeSessionsEndpoints : IEndpointGroup` wiring, the name-resolution joins (mirror `SessionDetailLoader` /
  `ListSessionsHandler`: `IgnoreQueryFilters` on grade/subject/spec **names** only), the batched progress/quiz/assignment
  projections, the signed-URL calls, and the integration tests.
- **Frontend owns:** the My-Sessions spotlight hero + summary counts + filter chip-bar + divided list + mascot empty
  state, the Session-detail hero + circular progress ring + mascot gate banner + video playlist (lock/access badges +
  Play) + materials list + assignment/quiz entry cards, the **deep-link Play** + install-prompt + gate-error rendering
  (§E.5), the new `feature-sessions` lib, and the Jest specs.
- **Wiring owns:** proving the slice live on the Aspire stack — My Sessions shows the enrolled set with real
  progress + expiry (tenant-isolated, per-caller), Session detail shows the playlist/materials/assignment/quiz with
  correct lock states across **active / expiring / expired / quiz-gated / exhausted**, the material signed URL resolves,
  and **Play fires the 5C gate** (decrement + `VideoPlaybackStarted` audit + handoff code) then deep-links — surfacing
  every gate `reason`, with the redeem/key chain proven by 5C's own wiring (not re-driven here).
