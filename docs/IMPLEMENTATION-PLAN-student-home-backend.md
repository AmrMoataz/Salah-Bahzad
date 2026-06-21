# Student Portal · Home (Weekly study plan) — BACKEND stream

> Status: **Planned — not yet built** · Created 2026-06-21 · The **engine half** of the **Home** slice (net-new, beyond
> the master plan's S0–S6 — the master treats "Home" as the catalogue; this adds a *personalized* Home). Adds **one** new
> student read — **`GET /api/me/plan`** — a server-composed, Redis-cached **weekly plan** derived **entirely from existing
> state** (enrollments, the 5C video-access counters, assignment/quiz status). **No new aggregate, no migration, no new
> domain field, no fabricated due date, no stored plan.** The only persistence is the **HybridCache** entry (disposable).
>
> Satisfies `FR-STU-SES-001` (progress + expiry — the only real deadline), `FR-PLAT-ENR-003/-007` (validity window +
> prerequisite-assignment gate), `FR-PLAT-QZ-008`/`FR-STU-QZ-010` (gating quiz unlocks the same session's videos),
> `FR-STU-CAT-003` (enrollment is code-only → `Redeem` steps), `NFR-PERF-001/-005`, `NFR-SCAL-004`, `NFR-SEC-007/-010`.
>
> **Change the contract (`docs/contracts/student-home-weekly-plan.md`) first if anything moves.** Keep the route +
> `MyPlanDto`/`MyPlanStepDto`/`MyPlanRecentDto` fields + the four enums exactly as frozen there. Gate:
> `dotnet test -c Release` green (the one pre-existing `QuestionBank` image-test failure is the known baseline); then the
> wiring stream proves it live on Aspire.

---

## Design reference

This stream ships **no screen**. Its single JSON shape feeds the Student-Portal **Home** mock (hero + 4 KPI cards +
"Your plan" list + "This week" + "Recently enrolled"), reconciled to the platform's real capabilities by
`docs/contracts/student-home-weekly-plan.md` (the design-anchor + ground-rule reconciliation is in that file's header +
§0). The authority for every field/enum is **`docs/contracts/student-home-weekly-plan.md` §A.1/§B**; for the algorithm,
**§E**; for caching/invalidation, **§C/§D**.

---

## 1. Frozen contract (this stream)

Implements `docs/contracts/student-home-weekly-plan.md` **§A/§B/§C/§D/§E** verbatim:

- **§A** — `GET /api/me/plan` · `RequireStudent` · **no query parameters** · **always `200 MyPlanDto`** (an empty/
  onboarding plan when the caller has no active enrollments — **never 404**). Served from the Redis/HybridCache when warm;
  computed + cached on a miss (§C). `401` anon, `403` staff (the `RequireStudent` filter) are the only non-200s (§A.2).
- **§A.1** — the `MyPlanDto` shape: `isoWeek`/`weekStartUtc`/`weekEndUtc`/`generatedAtUtc`, the headline counters
  (`totalSteps`/`completedSteps`/`overdueSteps`), the `kpis` block, the nullable `focus` block, `steps` (≤ 7), and
  `recentlyEnrolled` (≤ 5). Plus `MyPlanStepDto` and `MyPlanRecentDto`.
- **§B** — the four string-name enums: `MyPlanStepKind` (`Quiz`/`Videos`/`Assignment`/`Redeem`), `MyPlanStepStatus`
  (`Pending`/`Completed` — **no `Overdue`**), `MyPlanDueState` (`None`/`ExpiringSoon`/`Expired`), `MyPlanActionType`
  (`Navigate`/`Redeem`).
- **§C** — the `HybridCache.GetOrCreateAsync` wiring: key `plan:{tenantId}:{studentId}:{isoWeek}`, TTL = time-to-week-end
  (floored to a small minimum), `LocalCacheExpiration ≤ 60 s`, tag `plan:{studentId}`.
- **§D** — cache invalidation on the existing domain events via `RemoveByTagAsync("plan:{studentId}")`.
- **§E** — the algorithm: inputs reuse the S3 derivations verbatim (§E.1), focus selection (§E.2), gate-ordered step build
  capped at 7 with chunked Videos (§E.3), empty/expired/all-done shapes (§E.4), KPIs + `recentlyEnrolled` (§E.5).

**Reads are not audited** (§F — parity with `/api/me/sessions` + `/api/me/catalogue`); this read changes no state and never
invokes the Play gate. The three `/api/me/videos` gate routes are untouched.

## 2. Pre-flight (confirm — do NOT rebuild; cite line refs)

All of these already exist on `HEAD` and are **reused**; this stream invents no second derivation.

- **The S3 enrolled-set projection** — `ListMySessionsHandler` (`Features/Sessions/Queries/ListMySessions/
  ListMySessionsHandler.cs`) is the **template for the per-enrollment projection**: the non-refunded enrollment load
  (L29–39: `Where(e => e.StudentId == studentId && e.Status != EnrollmentStatus.Refunded)` + the inline
  `e.VideoAccesses.Count(a => a.AccessRemaining < a.AccessAllowed)` watched-count, L38), the **batched** `videoCount` per
  session via a grouped `db.SessionVideos` sub-query (L52–56), and the `IgnoreQueryFilters` grade/spec/subject **name**
  resolution (L60–78). The `isExpired`/`progressPercent`/completion derivations (L88–92) and `DeriveCompletion` (L129–132)
  are reused **as the plan's inputs** (§E.1).
- **`GetMySessionHandler`** (`Features/Sessions/Queries/GetMySession/GetMySessionHandler.cs`) — the **template for the
  gate + assignment/quiz status**: the `UserQuiz` predicate (`db.UserQuizzes.FirstOrDefault(q => q.EnrollmentId ==
  enrollment.Id)`, L86–88) → `hasGatingQuiz`/`quizPassed` (L89–90 — the **same** predicate the 5C
  `StartVideoPlaybackHandler` uses, L57–60), and the `UserAssignment` load (L118–120). The plan reuses these to decide a
  step's `status`/`blocked`, not to render a detail screen.
- **`AttendanceProjector.WatchedByEnrollmentAsync`** (`Features/Attendance/AttendanceProjector.cs`, L130–136) — **the
  canonical batched `videosWatched`** predicate (`AccessRemaining < AccessAllowed`), keyed by enrollment id, **one
  round-trip for the whole set**. This is the no-N+1 shape (§C / `NFR-PERF-005`) the plan's KPIs sum over. **Do not invent
  a second derivation** (contract §0/§E.1).
- **`Session.PrerequisiteSessionId`** (`Domain/Entities/Session.cs:43`, nullable Guid) — the **only** ordering signal the
  domain has; §E.2's prerequisite-depth walk follows this chain. There is **no** `Course`/`Order`/`PublishedAt`/`DueDate`
  field (confirmed; the contract §0/§G design Path B / fabricated dates out).
- **`EnrollmentStatus`** — only `Active`/`Refunded` are written; `Expired` is **never** flipped by the writer (the S3 +
  catalogue handlers derive expiry). The plan's "active" = `!isExpired` (derived), "enrolled set" = `Status != Refunded`
  (`ListMySessionsHandler.cs:31`).
- **`ICurrentUserResolver`** — `.UserId` = student id, `.TenantId` = tenant (as the S3 handlers use them). **`TimeProvider
  clock`** for `now`/`isoWeek` — never `DateTimeOffset.UtcNow` (`GetMySessionHandler.cs:26`).
- **`RequireStudentExtensions.RequireStudent()`** (`Api/Authorization/`) — anon → 401, staff → 403; every `/api/me/*`
  route uses it (`MeSessionsEndpoints.cs:28`).
- **HybridCache is registered but has no Application consumer yet** — `InfrastructureServiceExtensions.cs:187`
  (`services.AddHybridCache();`) with the Redis L2 wired when `ConnectionStrings__redis` is present (L178–184), L1-only
  otherwise. **This stream is HybridCache's first read-side consumer.** No new cache service — inject `HybridCache`
  directly into the handler (it lives in `Microsoft.Extensions.Caching.Hybrid`).
- **The notification-handler pattern** — `EnrollmentSideEffectsHandler` (`Features/Enrollment/EventHandlers/`) shows the
  shape: an `internal sealed class … : INotificationHandler<TEvent>` in `Features/<Aggregate>/EventHandlers/`, dispatched
  **post-commit** (events fire after `SaveChanges`). The §D invalidation handlers copy this.
- **The events that move plan state** (§D) and what they carry:
  - `EnrollmentCreatedEvent` / `EnrollmentExtendedEvent` / `EnrollmentRefundedEvent` — **all carry `StudentId`**
    (`Domain/Events/Enrollment*Event.cs`).
  - `QuizGradedEvent(Guid StudentId, Guid GatedSessionId, int BestPercent, bool Passed)` — **carries `StudentId`**;
    raised by `UserQuiz` on submit/timeout (`Domain/Entities/UserQuiz.cs:131,150`).
  - `AssignmentGradedEvent(…, Guid StudentId, …)` — **carries `StudentId`**; raised only when the **last** question is
    answered and the assignment auto-grades (`Domain/Entities/UserAssignment.cs:145`).
- **The two no-event write paths the contract §D seam covers (verified):**
  - **Video playback raises NO domain event.** `StartVideoPlaybackHandler` decrements + writes the audit row via
    `IAuditWriter.WriteAsync` directly (L66–72) — there is **no** `VideoPlaybackStartedEvent`. Contract §D therefore
    invalidates **inline** here via the `IStudentPlanCache` seam (§3.5).
  - **A non-final `AnswerQuestion` raises NO event.** `AssignmentGradedEvent` fires only on completion
    (`UserAssignment.cs:145`); answering question 3-of-8 moves the step's `progress` but dispatches nothing. Contract §D
    (revised) invalidates **inline** in `AnswerQuestionHandler` too, so `progress.done` stays correct intra-week — both
    inline drops go through the same seam (§3.5).

## 3. Application — one query + the DTOs + the invalidation handlers

### 3.1 The query (`Features/Sessions/Queries/GetMyPlan/`)

Implementer's call on folder per contract §H ("`Features/Me/Queries/GetMyPlan/` **or** `Features/Sessions/Queries/
GetMyPlan/`"). **Recommendation: `Features/Sessions/Queries/GetMyPlan/`** — it sits beside `ListMySessions`/`GetMySession`
whose enrollment projection + name-resolution + signed-thumbnail helpers it reuses directly, and the `ToMy*Dto` mappings
already live in `Features/Sessions/DTOs/SessionDtos.cs`.

- `GetMyPlanQuery() : IRequest<MyPlanDto>` — **no parameters** (the student + tenant come from the JWT). **No validator**
  (nothing to validate; parity with `ListMySessionsQuery`).
- `GetMyPlanHandler(IAppDbContext db, ICurrentUserResolver currentUser, IFileStorage fileStorage, TimeProvider clock,
  HybridCache cache)`:
  1. `var studentId = currentUser.UserId; var tenantId = currentUser.TenantId; var now = clock.GetUtcNow();`
  2. Compute `isoWeek`/`weekStartUtc`/`weekEndUtc` from `now` (UTC, ISO-8601 — `System.Globalization.ISOWeek`).
  3. `var key = $"plan:{tenantId}:{studentId}:{isoWeek}";` → `return await cache.GetOrCreateAsync(key, ct => ComputeAsync
     (now, weekStart, weekEnd, isoWeek, ct), options, tags: [$"plan:{studentId}"], cancellationToken);` (§C wiring in
     §3.4). The `ComputeAsync` factory is the §E algorithm.

> **Why cache the whole DTO, not the raw rows:** the contract makes the **first authenticated Home load the compute**
> (§C "compute-on-read"). The factory closure must be `static`-friendly (no captured `db` in a way the source-gen Mediator
> dislikes) — pass the handler's collaborators through the factory's state arg or capture `this` (HybridCache's overload
> with a state object avoids the closure allocation; either is fine for v1).

### 3.2 `ComputeAsync` — the §E algorithm (the factory body)

This is the only non-trivial logic; it is a **pure read**, all inputs tenant-scoped by the global filter (`NFR-SEC-010`).
**Batch every concern — no N+1** (`NFR-PERF-005`, `< 300 ms` p95 `NFR-PERF-001`):

1. **Load the enrolled set once** (mirror `ListMySessionsHandler.cs:29–39`): non-refunded enrollments
   (`Status != Refunded`), projecting `Id`, `SessionId`, `EnrolledAtUtc`, `ExpiresAtUtc`, and the inline
   `VideoAccesses.Count(a => a.AccessRemaining < a.AccessAllowed)` watched-count. `OrderByDescending(EnrolledAtUtc)`.
   Empty → go straight to §E.4 "no enrollments at all".
2. **Batch the side tables by id-set** (each one round-trip):
   - `videoCount` per session — grouped `db.SessionVideos` (`ListMySessionsHandler.cs:52–56`).
   - `UserQuiz` per enrollment — `db.UserQuizzes.Where(q => enrollmentIds.Contains(q.EnrollmentId))` projecting
     `EnrollmentId`/`Passed`/`MinPassPercent` → dictionary (mirror `AttendanceProjector.QuizByEnrollmentAsync`).
   - `UserAssignment` per enrollment — `db.UserAssignments.Where(a => enrollmentIds.Contains(a.EnrollmentId))` projecting
     `EnrollmentId`/`Status`/`AnsweredCount`(see note)/`QuestionCount`/`ScoreMarks`/`MaxMarks`/`CompletedAtUtc` →
     dictionary. *(Note: `AnsweredCount` is a computed property over the owned `_questions` collection — to keep it one
     round-trip, either `Include` the questions or count answered via a projected sub-query; the enrolled set is small.)*
   - Session display fields + names (title/spec/subject/grade) — reuse `ListMySessionsHandler`'s `IgnoreQueryFilters`
     dictionaries (L60–78); `PrerequisiteSessionId` per session (for §E.2 depth + §E.3 roll-forward).
3. **Per-enrollment derive (§E.1)** into a small in-memory record: `videosWatched`, `videoCount`, `progressPercent`
   (`videoCount == 0 ? 0 : round(100 × watched / count)`, `MidpointRounding.AwayFromZero` — match
   `ListMySessionsHandler.cs:91`), `isExpired` (`ExpiresAtUtc != null && <= now`), `completion` (reuse
   `DeriveCompletion`), `hasGatingQuiz`/`quizPassed`/`minPassPercent`, `assignment` status + `answered`/`questionCount`.
   `incomplete` = `completion != Completed || (assignment exists && !Completed) || (hasGatingQuiz && !quizPassed)`;
   `active` = `!isExpired` (contract §E.1 exact).
4. **Pick focus (§E.2)** among **active & incomplete**: order by `ExpiresAtUtc` asc nulls-last → `EnrolledAtUtc` desc;
   first wins; none → `focus = null` (§E.4). *(Optional tie-break: prefer earlier-in-the-`PrerequisiteSessionId`-chain over
   the **already-loaded** rows only — do not add a query for non-enrolled ancestors.)*
5. **Build steps (§E.3)** in gate order, appending to a list, **stopping at 7** (drop the lowest-priority tail):
   Quiz (only if `hasGatingQuiz`, never `blocked`) → Videos (one **chunked** step with `progress={done,total}`, `blocked =
   hasGatingQuiz && !quizPassed`, `blockedReason = "Pass the quiz to unlock the videos"`) → Assignment (never `blocked` by
   expiry — reachable when expired, `FR-STU-SES-001`) → Redeem-next roll-forward (only if focus fully complete; a
   **generic** "Ready for your next session" `Redeem` — naming the specific successor is optional, §E.3 #4) → ≤ 2
   secondary `ExpiringSoon` nudges for **other** active-incomplete enrollments (`ExpiresAtUtc ≤ now + 14d`). Step titles/
   subtitles/labels/keys are the **verbatim** strings in contract §E.3 (e.g. `key = "videos:{sessionId}"`, `title = "Watch
   your lessons"`, `subtitle = "{watched} of {count} watched"`).
6. **Per-step `dueState`/`expiresAtUtc` (§E.3 tail)** from **its** session's expiry: `Expired` if that session
   `isExpired` && step incomplete; else `ExpiringSoon` if active && `ExpiresAtUtc ≤ now + 14d`; else `None`.
7. **Empty/expired-only/all-done (§E.4)** — when `focus == null`: no enrollments → one `Redeem` step ("Redeem a code" /
   "Unlock your first session"); only expired-with-incomplete-assignments → ≤ 3 `Assignment` steps (`dueState = Expired`)
   + a trailing `Redeem` ("Renew access — get a new code"); all complete → any roll-forward `Redeem` else `[]`.
8. **KPIs + `recentlyEnrolled` (§E.5)** — `activeSessions` = active & not-Completed enrollments; `videosWatched`/
   `videosTotal`/`overallProgressPercent` summed over the **active** set; `completedSessions` over the **whole**
   non-refunded set; `recentlyEnrolled` = the non-refunded set `EnrolledAtUtc` DESC, top 5 → `MyPlanRecentDto`.
9. **Headline counters (§A.1)** — `totalSteps` = `steps.Count`, `completedSteps` = `count(status == Completed)`,
   `overdueSteps` = `count(dueState == Expired && status != Completed)`.
10. **Sign the focus thumbnail** only (the steps + recent rail carry no signed URL per §A.1) — `IFileStorage.
    GetSignedReadUrlAsync(thumbnailObjectKey)` when the focus session has a key, else null (`ListMySessionsHandler.cs:99–
    104`). `generatedAtUtc = now`.

### 3.3 DTOs + enums + mappings (`Features/Sessions/DTOs/SessionDtos.cs`)

Add **beside** the existing `MySession*` types so the `ToMy*Dto` helpers + `JsonStringEnumConverter` enum convention are
obvious:

- **Records:** `MyPlanDto`, `MyPlanStepDto`, `MyPlanRecentDto`, plus the nested `MyPlanFocusDto` and `MyPlanKpisDto` and
  `MyPlanStepProgressDto` / `MyPlanStepActionDto` — field order = contract §A.1 verbatim (the JSON keys are camelCased by
  the host's serializer; keep the DTO property names PascalCase as elsewhere in the file).
- **Enums:** `MyPlanStepKind`, `MyPlanStepStatus`, `MyPlanDueState`, `MyPlanActionType` (§B) — serialized as **string
  names** (the host already registers `JsonStringEnumConverter`).
- **Mappings:** small `.ToPlanStepDto(...)`, `.ToPlanRecentDto(...)`, `.ToPlanFocusDto(...)` static extensions — **no
  mapping library, never map in the handler body** (backend `CLAUDE.md` mapping convention). The step DTOs are composed,
  not 1:1 from an entity, so the mapping helpers take the computed primitives (as `ToMyDto` does at L365).

### 3.4 The HybridCache options (§C)

```csharp
var ttl = weekEndUtc - now;                  // time remaining until Sunday 23:59:59Z of isoWeek
if (ttl < TimeSpan.FromMinutes(5)) ttl = TimeSpan.FromMinutes(5);   // floor so the week rolls over cleanly
var options = new HybridCacheEntryOptions
{
    Expiration = ttl,                        // L2 (Redis) absolute
    LocalCacheExpiration = TimeSpan.FromSeconds(60),   // L1 ≤ 60 s so a tag-drop on another node is seen quickly
};
```
Tag the entry `plan:{studentId}` (the `tags:` arg of `GetOrCreateAsync`) so a single `RemoveByTagAsync` drops **all** of
that student's cached weeks (§D). The key carries `tenantId` as defence-in-depth (isolation is already enforced by the
global filter on every underlying read — §C).

### 3.5 Cache invalidation (§D — the `IStudentPlanCache` seam)

Contract §D makes invalidation a **seam**, not purely event-driven (verified: video playback + partial answers raise no
event). Add a tiny **`IStudentPlanCache`** (Application interface; Infrastructure impl over `HybridCache`):
`InvalidateAsync(Guid studentId)` ≡ `RemoveByTagAsync($"plan:{studentId}")`. Call it from **every** student state-change —
via an `INotificationHandler` where a domain event exists, **inline** where none does:

| Write path | Domain event? | Where `InvalidateAsync` is called |
|---|---|---|
| `EnrollmentCreatedEvent` / `EnrollmentExtendedEvent` / `EnrollmentRefundedEvent` (carry `.StudentId`) | yes | `INotificationHandler` (`Features/Sessions/EventHandlers/`) |
| `QuizGradedEvent` (carries `.StudentId`) | yes | `INotificationHandler` |
| `AssignmentGradedEvent` (carries `.StudentId`) | yes | `INotificationHandler` |
| `AnswerQuestionHandler` — non-final answer (no event) | **no** | **inline** at the end of the handler (`currentUser.UserId`) |
| `StartVideoPlaybackHandler` — 5C gate decrement (no event) | **no** | **inline** after the audit write (`…cs:72`, `currentUser.UserId`) |

Post-commit / inline-after-write, off the request path's critical work (`NFR-SCAL-004`); the `studentId` is in scope at
both inline call sites, so **no new domain event** is introduced (the smallest faithful change — contract §D rationale).
Mirror `EnrollmentSideEffectsHandler`'s `internal sealed … : INotificationHandler<T>` shape for the evented ones. Worst
case (a missed drop) self-heals at the weekly TTL; the seam keeps it correct intra-week (the wiring proves it by re-reading
after a real engine write).

## 4. API — endpoint group

The contract (§H) allows `MeHomeEndpoints : IEndpointGroup` **or** extending `MeSessionsEndpoints`. **Recommendation: a new
`MeHomeEndpoints : IEndpointGroup`** (auto-discovered like `MeSessionsEndpoints`/`MeCatalogueEndpoints`) — Home is its own
concern and its own Scalar tag.

```csharp
internal sealed class MeHomeEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me").WithTags("My Home").WithOpenApi();

        group.MapGet("/plan", GetPlanAsync)
            .RequireStudent()
            .WithName("GetMyPlan")
            .WithSummary("The caller's current weekly study plan: KPIs, focus session, ordered steps, recently enrolled")
            .Produces<MyPlanDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> GetPlanAsync(ISender sender, CancellationToken cancellationToken)
        => Results.Ok(await sender.Send(new GetMyPlanQuery(), cancellationToken));
}
```
Thin `ISender.Send(...)` delegate; **no path/query params** (no IDOR surface — §0). `RequireStudent()` supplies 401/403;
the route **can't 404** (always-200, §A.2).

## 5. Migration

**None.** Every input row already exists (`Enrollment`, `EnrollmentVideoAccess`, `Session`, `SessionVideo`,
`UserAssignment`, `UserQuiz`). No new field, no `DueDate`/`Order`/`PublishedAt` (the contract §0/§G forbid them). The plan
is derived state + a disposable HybridCache entry. *(HybridCache + Redis are already registered;
`InfrastructureServiceExtensions.cs:178–187` — no infra change.)*

## 6. Tests (`dotnet test -c Release`)

Integration (`WebApplicationFactory` + Testcontainers **Postgres + Redis**, **Student-role JWT** — reuse the catalogue/
S3 student principal helper). Seed via the redeem engine or directly. With Redis present HybridCache promotes to L2 so the
invalidation tests are real.

- **Focus matrix (§E.2/§E.3) — the heart:** seed one student across the matrix and assert `focus` + the ordered `steps`:
  - **No enrollment** → `focus == null`, zeroed `kpis`, `steps` = one `Redeem` step (§E.4), `recentlyEnrolled == []`.
  - **Fresh enroll, quiz-gated, unpassed** → focus = that session; steps = `Quiz` (Pending) then `Videos` with
    `blocked == true` + `blockedReason` set; Videos `status` Pending.
  - **Quiz passed, partway videos** → no Quiz step (or Completed in the completed sub-list per §E.3), `Videos` unblocked
    with `progress = {done: watched, total: count}`, label `Continue`.
  - **Assignment incomplete** → `Assignment` step Pending with `progress` answered/total; never `blocked` by expiry.
  - **Focus fully complete + a next session whose prerequisite == focus** → a roll-forward `Redeem` step
    (`action.type == Redeem`, no `route`).
  - **Two active-incomplete enrollments, one `ExpiringSoon`** → focus is the soonest-to-expire (§E.2 order); a secondary
    `ExpiringSoon` nudge appears for the other (`dueState == ExpiringSoon`, ≤ 2).
  - **Expired-only with incomplete assignment** → `focus == null`, an `Assignment` step `dueState == Expired` + a trailing
    `Redeem` (§E.4).
- **Prerequisite-depth ordering (§E.2 #2):** two active-incomplete enrollments with equal expiry, one a prerequisite of
  the other → the earlier-in-the-chain session is `focus`.
- **Cap of 7 (§E.3):** seed enough qualifying steps that the raw list exceeds 7 → exactly 7 returned, lowest-priority tail
  dropped.
- **KPIs (§E.5):** `activeSessions` excludes expired + Completed; `videosWatched`/`videosTotal`/`overallProgressPercent`
  sum the active set (`overallProgressPercent == round(100 × watched / total)`, 0 when total == 0); `completedSessions`
  counts the whole non-refunded set incl. expired-but-finished.
- **Always-200 / empty (§A.2):** a student with no enrollments → `200` (not 404) with the onboarding `Redeem` step.
- **Cache-invalidation path (§D) — prove the seam drop:** read `/api/me/plan` (warm the cache) → drive a real state change
  via the existing engines and re-read, asserting the step moved **without** waiting for TTL:
  - **Quiz passed** (`QuizGradedEvent`) → the `Videos` step un-`blocked` on the next read.
  - **Assignment completed** (`AssignmentGradedEvent`) → the `Assignment` step `status == Completed`.
  - **Enrollment created** (redeem) → a focus candidate appears; **refunded** → it disappears.
  - **Video playback** (the 5C gate, inline seam drop) → the `Videos` `progress.done` increments on the next read.
  - **Partial assignment answer** (`AnswerQuestionHandler`, inline seam drop) → the `Assignment` `progress.done` increments
    on the next read (no TTL wait).
- **Tenant isolation (`NFR-SEC-010`):** a student of tenant A never sees tenant B's sessions in `focus`/`steps`/`kpis`/
  `recentlyEnrolled`; the cache key's `tenantId` segment + the global filter both hold (seed a tenant B student with the
  same id is impossible — assert cross-tenant rows never leak into A's plan).
- **Not audited (§F):** a `GET /api/me/plan` writes **no** new `audit_entries` row (parity with `/api/me/sessions`).
- **Auth gating:** anonymous → `401`; **staff** JWT → `403`; Student JWT → `200`.
- **Reuse-fidelity (regression):** the S3 `MySessionsApiTests` + the 5C `VideoEndpoints` tests still pass (this stream
  adds no change to those paths beyond the optional inline `RemoveByTagAsync` in `StartVideoPlaybackHandler`).

## 7. Definition of done = ready for wiring

Contract §A/§B/§C/§D/§E satisfied; `GET /api/me/plan` is `RequireStudent`, no-params, always-200; the S3 derivations +
`AttendanceProjector` predicate reused (no second derivation, no N+1); HybridCache `GetOrCreateAsync` + the §D
`RemoveByTagAsync` handlers wired on the existing events; **no migration**; suite green (`dotnet test -c Release`, minus
the known baseline `QuestionBank` image test — report it). The 5C gate + S3 reads untouched (save the optional inline
tag-drop). Hand to `IMPLEMENTATION-PLAN-student-home-wiring.md`.

## 8. Frozen vs. backend-owned

- **Frozen (`docs/contracts/student-home-weekly-plan.md`):** the `GET /api/me/plan` path + `RequireStudent` + no-params +
  always-200 (§A); the `MyPlanDto`/`MyPlanStepDto`/`MyPlanRecentDto`/`kpis`/`focus` field names + types (§A.1); the four
  enums (§B); the cache key/TTL/tag scheme (§C); the invalidation event set (§D); the focus-selection order (§E.2), the
  step order + cap of 7 + chunked-Videos + verbatim titles/subtitles/keys/labels (§E.3); the empty/expired/all-done shapes
  (§E.4); KPIs + `recentlyEnrolled` (§E.5); "read, not audited" (§F); the deferred/not-built stance (§G). Reuse of the S3
  derivations verbatim (§0/§E.1).
- **Backend owns (this stream):** the query folder/handler/validator names (recommend `Features/Sessions/Queries/
  GetMyPlan/`, keep route + DTOs frozen); the DTO/enum + `.ToPlan*Dto()` mapping location (in `SessionDtos.cs` beside the
  `MySession*` types); the `MeHomeEndpoints : IEndpointGroup` wiring + Scalar/OpenAPI annotations; the **batched** §E
  projections (reuse `ListMySessionsHandler`'s enrollment projection + `AttendanceProjector` predicate, one round-trip per
  concern); the prerequisite-depth walk; the `HybridCache.GetOrCreateAsync` call + options; the §D invalidation
  `INotificationHandler`(s) + the `IStudentPlanCache` seam (inline `InvalidateAsync` in `StartVideoPlaybackHandler` **and**
  `AnswerQuestionHandler`); and the integration tests (focus matrix, tenant isolation, cache-invalidation path, empty/
  expired states).
- **Frontend owns:** the `feature-home` lib + route, `PlanService`, the hero/KPI cards/"Your plan" list/"This week" bar/
  "Recently enrolled" rail/mascot empty-state, read-only ticks + `blocked` disabled rows + `dueState` badges + per-`kind`
  CTA wiring, full responsiveness + a11y, the Jest specs (contract §H "Frontend owns").
- **Wiring owns:** proving the slice live on Aspire across the focus matrix + KPIs + the cache-invalidation round-trip
  (read → watch/pass/complete via the real engines → re-read shows the step moved) + tenant isolation + `< 300 ms` warm
  (contract §H "Wiring owns").

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the BACKEND stream of the Student-Portal HOME (weekly study plan) for Salah Bahzad (.NET 10, Clean
Architecture + CQRS + source-gen Mediator). Edit backend/** ONLY. Add ONE student READ endpoint: GET /api/me/plan
(RequireStudent, NO params, ALWAYS 200). NO migration, NO new aggregate, NO new domain field, NO fabricated due dates, NO
stored plan — the plan is DERIVED state cached in HybridCache and invalidated on existing domain events.

Read first, in order:
1. backend/CLAUDE.md (Multi-tenancy / EF global query filters, Mapping conventions, Minimal API, Caching=HybridCache,
   Testing).
2. docs/contracts/student-home-weekly-plan.md — the FROZEN contract: §A (GET /api/me/plan + MyPlanDto), §A.1 (the DTO
   shapes incl. MyPlanStepDto/MyPlanRecentDto), §B (the 4 string enums), §C (HybridCache key/TTL/tag), §D (cache
   invalidation event set), §E (the algorithm: §E.1 reuse S3 derivations verbatim, §E.2 focus selection incl.
   prerequisite-depth, §E.3 gate-ordered steps cap 7 + chunked Videos, §E.4 empty/expired/all-done, §E.5 KPIs +
   recentlyEnrolled), §F (read, not audited), §G/§H. Change the contract first if anything moves.
3. The templates to mirror: Application/Features/Sessions/Queries/ListMySessions/ListMySessionsHandler.cs (the non-refunded
   enrollment projection + inline watched-count + batched videoCount + IgnoreQueryFilters name resolution + isExpired/
   progress/DeriveCompletion), GetMySession/GetMySessionHandler.cs (the UserQuiz gate predicate + UserAssignment status),
   Application/Features/Attendance/AttendanceProjector.cs (WatchedByEnrollmentAsync = AccessRemaining < AccessAllowed,
   batched per enrollment — REUSE, do not re-derive), Application/Features/Enrollment/EventHandlers/
   EnrollmentSideEffectsHandler.cs (the INotificationHandler shape), Infrastructure/InfrastructureServiceExtensions.cs
   (AddHybridCache already registered — this is its first read consumer), and ICurrentUserResolver (.UserId/.TenantId) +
   TimeProvider.

Build: a GetMyPlan query/handler under Features/Sessions/Queries/GetMyPlan; MyPlanDto/MyPlanStepDto/MyPlanRecentDto +
the 4 enums + .ToPlan*Dto() mappings in Features/Sessions/DTOs/SessionDtos.cs; the §E algorithm (batched, NO N+1, < 300ms);
HybridCache.GetOrCreateAsync(key="plan:{tenant}:{student}:{isoWeek}", TTL=time-to-week-end floored, tag="plan:{student}");
INotificationHandler(s) calling RemoveByTagAsync("plan:{studentId}") on EnrollmentCreated/Extended/Refunded, QuizGraded,
AssignmentGraded. Define a tiny IStudentPlanCache seam (InvalidateAsync = RemoveByTagAsync("plan:{studentId}")) and call it
inline in BOTH StartVideoPlaybackHandler (video playback raises NO event) and AnswerQuestionHandler (partial answer raises
none) — per contract §D. Wire a new
MeHomeEndpoints : IEndpointGroup (RequireStudent). Reads NOT audited. Always 200 (empty/onboarding plan when no
enrollments — never 404).

Tests (xUnit v3 + Testcontainers Postgres+Redis + FluentAssertions, Student-role JWT): the focus matrix (no-enroll /
quiz-gated-unpassed → Videos blocked / partway videos / assignment incomplete / focus complete → roll-forward Redeem /
expiring-soon nudge / expired-only → Expired assignment + Redeem); prerequisite-depth tie-break; cap of 7; KPIs; always-200
empty; the cache-invalidation path (read → pass quiz / complete assignment / enroll / refund / watch a video via the real
engines → re-read shows the step moved without TTL); tenant isolation (NFR-SEC-010); not-audited; 401 anon / 403 staff /
200 student. Green gate: dotnet test -c Release (the one pre-existing QuestionBank image test may stay red — baseline).
Report it.
```
