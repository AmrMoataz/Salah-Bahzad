# FROZEN CONTRACT — Student Portal · Home · Weekly study plan (`GET /api/me/plan`)

> Status: **Frozen** · Created 2026-06-21 · Slice: Student-Portal **Home** (net-new, beyond the master plan's S0–S6 —
> the master treats "Home" as the **catalogue**, screen #5 / `FR-STU-CAT-001`; this adds a *personalized* Home).
> **Design anchor:** the proposed Home mock (hero + 4 KPI cards + "Your tasks" list + "This week" + "Recently enrolled")
> reconciled to the platform's real capabilities. **Behaviour authority:** `FR-STU-SES-001` (progress + expiry — the only
> real deadline), `FR-PLAT-ENR-003/-007` (validity window + prerequisite gate = the prerequisite's **assignment**),
> `FR-PLAT-QZ-008` / `FR-STU-QZ-010` (gating quiz unlocks the **same** session's videos), `FR-STU-CAT-003` (enrollment is
> **code-only**), `NFR-PERF-001/-005`, `NFR-SCAL-004`, `NFR-SEC-007/-010`, `NFR-A11Y-001`.
>
> Satisfies: a single, honest "what do I do next" surface that keeps the student on track over the week. It adds **one**
> new student read — **`GET /api/me/plan`** — a server-composed, Redis-cached **weekly plan** derived entirely from
> existing state (enrollments, the 5C video-access counters, assignment/quiz status). **No new aggregate, no migration,
> no new domain field.** Frontend + wiring cite this file field-for-field. **Change this file first if anything moves.**

## 0. Ground rules

- **One new read, zero new persistence.** The backend adds **only** `GET /api/me/plan`. Every datum is **derived** from
  rows that already exist: the caller's `Enrollment` set, their `EnrollmentVideoAccess` counters (the source of truth the
  5C gate decrements), `UserAssignment`, `UserQuiz`, and `Session`/`SessionVideo`. **No `DueDate`/`PublishedAt`/`Order`
  field is invented** (the domain has none — verified) and **no plan is stored in Postgres**. The only persistence is the
  **Redis cache** (§C), which is a disposable projection.
- **The plan is *derived state*, never a user-editable to-do list.** Steps appear and disappear as the underlying state
  changes (watch the last video → the "watch" step flips to `Completed`). There are **no interactive checkboxes that the
  student toggles** — a step's `status` is computed, and the UI renders a *read-only* completion tick (this corrects the
  mock's editable-checkbox affordance).
- **No fabricated deadlines.** The mock's "Due in 3d / 5d / 7d" on videos/assignments/quizzes is **dropped** — the domain
  has no such dates and `FR-STU-ASG-002` / `FR-PLAT-QZ-004` design them out. The **only** real deadline is **enrollment
  expiry** (`Enrollment.ExpiresAtUtc`, `FR-PLAT-ENR-003`); it is the sole source of a step's `dueState`/`expiresAtUtc`.
- **Path A — current-frontier, one focus session at a time** (user decision 2026-06-21). The plan does **not** guess
  "this week's session" (impossible — `Session` has no order/release/course metadata). It anchors on the caller's most
  *urgent incomplete* active enrollment and lists the real steps to finish **that** session, then — only when it's done —
  rolls forward one step. Anti-overwhelm is structural: ≤ 7 steps, focus on one session (§E).
- **Students can't self-enroll / self-renew.** Enrollment is **code-only** (`FR-STU-CAT-003`); extend/refund are
  staff-side (`FR-PLAT-ENR-004`). So a "get into the next session" or "your access expired" step renders as
  **`Redeem`** (open the code modal / "ask your teacher for a code"), **never** a dead "Enroll"/"Renew" button.
- **Authenticated student surface.** `GET /api/me/plan` uses **`RequireStudent()`** (anon → 401, staff → 403) — identical
  to `/api/me/catalogue|sessions|assignments|quizzes|videos`. The student id + tenant come from the **JWT**
  (`ICurrentUserResolver.UserId`/`.TenantId`), never a URL id — there is **no** path parameter, so no IDOR surface.
- **Tenant isolation is automatic.** The EF global query filter scopes every read to the caller's tenant and excludes
  soft-deleted rows. **Never** write a per-handler `Where(x => x.TenantId == …)`. A cross-tenant isolation integration
  test is required (`NFR-SEC-010`).
- **Reads are not audited.** `GET /api/me/plan` is a pure read (parity with `/api/me/sessions` and `/api/me/catalogue`) —
  **not** audited. It triggers no state change; it never calls the Play gate (that stays the frontend's explicit action
  on the session-detail screen).
- **Derivations reuse the S3 primitives verbatim.** "Videos watched", `isExpired`, completion `state`, `hasGatingQuiz`,
  `quizPassed`, and assignment/quiz status are **the same** rules frozen in `docs/contracts/student-s3-my-sessions-video.md`
  (§E) and implemented by `ListMySessionsHandler` / `GetMySessionHandler` / `AttendanceProjector.WatchedByEnrollmentAsync`.
  This contract **reuses** them — it does **not** invent a second derivation.
- **Money & enums over the wire.** Enums are **string names** (`JsonStringEnumConverter`) — the frontend models them as
  string unions. Dates are ISO-8601 `…AtUtc`. The greeting name is **frontend-owned** (the student's first name is
  already in `StudentAuthStore`); the DTO carries **no** PII beyond session titles.

## A. Weekly plan — `GET /api/me/plan` (NEW · `RequireStudent`)

`RequireStudent` · `200 MyPlanDto`. Returns the caller's current weekly plan: KPI roll-up, the focus session, the ordered
steps, and a recently-enrolled list. **No query parameters.** Always `200` (an empty/onboarding plan when the caller has
no active enrollments — never 404). Served from the Redis cache when warm; computed + cached on a miss (§C).

### A.1 Result — `MyPlanDto`

```jsonc
// 200 · MyPlanDto
{
  "isoWeek": "2026-W25",                  // ISO-8601 week label the plan frames (server clock, UTC)
  "weekStartUtc": "…", "weekEndUtc": "…", // the Monday..Sunday window of isoWeek (for the "This week" header)
  "generatedAtUtc": "…",                  // when this snapshot was computed (the cache stamp; lets the UI show freshness)

  // headline counters (drive the hero copy "You have N tasks — M overdue" + the "This week" bar)
  "totalSteps": 4,                        // == steps.length (Pending + Completed) — the bar's denominator
  "completedSteps": 1,                    // == count(steps where status == "Completed") — the bar's numerator (ALWAYS consistent with steps[])
  "overdueSteps": 0,                      // == count(steps where dueState == "Expired" && status != "Completed")

  // KPI cards — derived from the caller's enrolled set (the same source as GET /api/me/sessions)
  "kpis": {
    "activeSessions": 2,                  // enrollments that are active (not expired) AND not Completed
    "videosWatched": 12, "videosTotal": 24,   // Σ over active enrollments (videosWatched = §E counters; total = SessionVideo count)
    "overallProgressPercent": 51,         // videosTotal == 0 ? 0 : round(100 × videosWatched / videosTotal)
    "completedSessions": 1                // enrollments whose completion state == Completed (incl. expired-but-finished)
  },

  // the focus session (Path A) — null when the caller has no active, incomplete enrollment (onboarding/all-done state)
  "focus": {
    "sessionId": "guid", "title": "string",
    "specializationName": "string|null",
    "thumbnailUrl": "string|null",        // short-lived signed R2 URL (same pattern as /me/sessions); null if none
    "progressPercent": 33,
    "expiresAtUtc": "…|null",             // null == no-expiry session
    "isExpired": false,
    "expiresInDays": 6|null,              // ceil((expiresAtUtc - now)/1d); null when no expiry; negative never sent (use isExpired)
    "dueState": "None"                    // "None" | "ExpiringSoon" | "Expired"  (ExpiringSoon = active & ≤ 14 days)
  },

  "steps": [ /* MyPlanStepDto, ordered — see §E.3; length ≤ 7 */ ],

  "recentlyEnrolled": [ /* MyPlanRecentDto, EnrolledAtUtc DESC, ≤ 5 */ ]
}
```

```jsonc
// MyPlanStepDto — one actionable step (or a completed one, kept for the "Completed" sub-list)
{
  "key": "string",                  // stable identity for *this* step within the plan, so the UI can track/animate it.
                                    //   "quiz:{userQuizId}" | "videos:{sessionId}" | "assignment:{userAssignmentId}" | "redeem:{sessionId}"
  "kind": "Quiz",                   // "Quiz" | "Videos" | "Assignment" | "Redeem"
  "title": "Pass the gating quiz",  // server-composed, honest label (NOT a fabricated due date)
  "subtitle": "Unlocks 4 videos",   // context line ("3 of 8 lessons watched", "Get a code from your teacher", …); may be null
  "sessionId": "guid",
  "sessionTitle": "string",
  "specializationName": "string|null",   // for the per-row accent chip (same accent system as the catalogue/dashboard)

  "status": "Pending",              // "Pending" | "Completed"  (there is NO "Overdue" status — see dueState)
  "blocked": false,                 // true when an earlier gate blocks this step (videos blocked until the quiz passes)
  "blockedReason": "string|null",   // user-safe reason when blocked ("Pass the quiz to unlock the videos"); null otherwise

  // real-deadline signal — derived ONLY from enrollment expiry (the one honest deadline)
  "dueState": "None",               // "None" | "ExpiringSoon" | "Expired"
  "expiresAtUtc": "…|null",         // the step's session expiry (mirrors focus.expiresAtUtc for focus-session steps)

  "progress": { "done": 1, "total": 5 } | null,   // for chunked/multi-item steps (Videos: watched/total; Assignment: answered/total). null for Quiz/Redeem.

  "action": {                       // what the frontend does — an in-portal route or a redeem intent; NEVER a fabricated external URL
    "type": "Navigate",             // "Navigate" | "Redeem"
    "route": "/sessions/{sessionId}", // in-portal deep link (session detail; the runner/quiz are reached from there). null when type == "Redeem".
    "label": "Continue"             // CTA text: "Start" | "Continue" | "Watch" | "Open" | "Redeem"
  }
}
```

```jsonc
// MyPlanRecentDto — the "Recently enrolled" rail
{ "sessionId": "guid", "title": "string", "specializationName": "string|null", "enrolledAtUtc": "…" }
```

### A.2 Error modes — ProblemDetails

| Status | When |
|---|---|
| `401` | No bearer (anonymous). |
| `403` | A **staff** JWT (the `RequireStudent` filter). |
| `200` | Always otherwise — incl. an **empty plan** (`focus: null`, `steps: []` or a single `Redeem`/browse onboarding step, zeroed KPIs) when the caller has no active enrollments. The UI shows the mascot empty-state + "Browse the catalogue". |

## B. Enums (string names over the wire)

| Enum | Values | Meaning |
|---|---|---|
| `MyPlanStepKind` | `Quiz` `Videos` `Assignment` `Redeem` | The action type. `Redeem` = open the code modal (next session / re-enroll after expiry). |
| `MyPlanStepStatus` | `Pending` `Completed` | Derived completion. No `Overdue` value — urgency rides `dueState`. |
| `MyPlanDueState` | `None` `ExpiringSoon` `Expired` | From enrollment expiry only. `ExpiringSoon` = active & `ExpiresAtUtc ≤ now + 14d`. |
| `MyPlanActionType` | `Navigate` `Redeem` | `Navigate` carries an in-portal `route`; `Redeem` carries none (frontend opens `/redeem`). |

## C. Caching (Redis via HybridCache — reuse the registered seam)

- **Store:** `HybridCache` (L1 in-process + Redis L2), already registered (`InfrastructureServiceExtensions.cs` —
  `AddHybridCache()`); **no new cache service.** L1-only in unit hosts, L2 in dev/integration — the handler is agnostic.
- **Key:** `plan:{tenantId}:{studentId}:{isoWeek}` (ISO-8601 week from the injected `TimeProvider`, UTC). Tenant in the
  key is defence-in-depth; isolation is already enforced by the global filter on every underlying read.
- **TTL:** `HybridCacheEntryOptions.Expiration` = **time remaining until the end of `isoWeek`** (Sunday 23:59:59Z),
  floored to a small minimum (e.g. 5 min) so the week naturally rolls over; `LocalCacheExpiration` ≤ 60 s so L1 never
  serves a stale step after an invalidation on another node.
- **Tag:** tag every entry with `plan:{studentId}` so a single `RemoveByTagAsync("plan:{studentId}")` drops all of a
  student's cached weeks on any state change (§D).
- **Compute-on-read:** the handler is `GetOrCreateAsync(key, factory, options, tags)`. A cold read computes the plan
  (§E) once and caches it; warm reads are L1/L2 hits. This is what makes "compute on login" work — the first authenticated
  Home load *is* the compute. Heavy work stays within `NFR-PERF-001` (< 300 ms p95) because the underlying reads are the
  same small per-student projections S2/S3 already ship (no N+1 — batch the counters/assignment/quiz lookups, `NFR-PERF-005`).
- **Thumbnail URL is signed per read, never cached.** `focus.thumbnailUrl` is a *short-lived* signed R2 URL whose
  lifetime (minutes) is far shorter than the plan's week-long TTL, so caching it would serve an expired URL. The
  **cached snapshot carries only the focus thumbnail's R2 object key**; the handler signs it **fresh on every read**
  (outside `GetOrCreateAsync`) and fills `focus.thumbnailUrl` then — same short-lived `IFileStorage.GetSignedReadUrlAsync`
  pattern as `/me/sessions`. The cache holds the derived plan, not any signed URL.

## D. Cache invalidation (a small seam called from every student state-change)

The plan must reflect a state change **the next time it's read**, not next week. **Verified in code:** not every relevant
write raises a domain event — the 5C Play gate (`StartVideoPlaybackHandler`) decrements + audits **inline** with no event,
and a non-final assignment answer (`AnswerQuestionHandler`) raises none (only the *last* answer raises `AssignmentGraded`).
So invalidation is a **seam**, not purely event-driven:

- Define a tiny **`IStudentPlanCache`** (Application interface, Infrastructure impl over `HybridCache`) with
  `InvalidateAsync(Guid studentId)` ≡ `RemoveByTagAsync("plan:{studentId}")`.
- Call it from **each** student state-change — via the existing `INotificationHandler<T>` where a domain event exists, and
  **inline** at the end of the write where none does:

| Write path | Domain event? | How the plan cache is dropped |
|---|---|---|
| Enrollment created (redeem) / extended / refunded | yes (`EnrollmentCreated/Extended/Refunded`, carry `StudentId`) | `INotificationHandler` → `InvalidateAsync` |
| Quiz graded / passed (best recomputed) | yes (carries `StudentId`) | `INotificationHandler` → `InvalidateAsync` |
| Assignment **completed** (last answer auto-grades) | yes (`AssignmentGraded`, carries `StudentId`) | `INotificationHandler` → `InvalidateAsync` |
| Assignment **partial** answer (progress moves) | **no** | **inline** `InvalidateAsync` at the end of `AnswerQuestionHandler` |
| Video playback / access decremented (5C gate) | **no** | **inline** `InvalidateAsync` at the end of `StartVideoPlaybackHandler` |

> Why a seam, not new domain events: it's the smallest faithful change (no new `VideoPlaybackStartedEvent` in the Domain —
> the `studentId` is already in scope at both inline call sites), keeps invalidation uniform, and stays off the request
> path's critical work (`NFR-SCAL-004`). Worst case (a missed drop) self-heals at the weekly TTL; the seam keeps it correct
> intra-week — which the wiring proves by re-reading after a real engine write (no TTL wait). **Note the L1 caveat:**
> `LocalCacheExpiration ≤ 60 s` (§C) means a single node may serve a step up to ~60 s stale after another node's
> invalidation; acceptable for a study plan, and the inline drop clears the *writing* node immediately.

## E. The algorithm (frozen — Path A, current-frontier)

All inputs are the caller's own rows, tenant-scoped by the global filter. `now = TimeProvider.GetUtcNow()`.

### E.1 Inputs (reuse S3 derivations verbatim)

For each of the caller's `Enrollment` rows **excluding `Refunded` and soft-deleted** (the §0 enrolled-set scope), compute,
exactly as `docs/contracts/student-s3-my-sessions-video.md` §E:
- `videoCount`, `videosWatched` (= `EnrollmentVideoAccess` rows with `AccessRemaining < AccessAllowed`), `progressPercent`.
- `isExpired` = `ExpiresAtUtc != null && ExpiresAtUtc <= now`.
- `completion` ∈ {`NotStarted`,`InProgress`,`Completed`} (`Completed` iff `videoCount > 0 && videosWatched == videoCount`).
- `hasGatingQuiz` / `quizPassed` (the `UserQuiz` for the enrollment; `quizPassed` = `BestPercent >= MinPassPercent`).
- `assignment` status (`UserAssignment.Status`, `answered`/`questionCount`); `incompleteAssignment` = exists & `!Completed`.

An enrollment is **incomplete** iff `completion != Completed` **or** `incompleteAssignment` **or** (`hasGatingQuiz && !quizPassed`).
An enrollment is **active** iff `!isExpired`.

### E.2 Pick the focus session (first wins)

Among the caller's **active & incomplete** enrollments, order by:
1. `ExpiresAtUtc` **ascending, nulls last** (soonest-to-expire = most urgent — the one real deadline);
2. then `EnrolledAtUtc` **descending** (the most recently started = the "current" session).

The first is `focus`. *(Optional refinement, implementer's call: break ties by preferring the one earliest in its
`PrerequisiteSessionId` chain so a chain finishes in order — computed over the **already-loaded** enrolled rows only; do
**not** add a query to load non-enrolled ancestors just for this tie-break.)* If there are **no** active & incomplete
enrollments, `focus = null` and §E.4 handles the empty / expired-only / all-done plan.

### E.3 Build the focus session's steps (gate-ordered), then secondary, capped at 7

Append in this order; stop at **7** total (drop later/lower-priority items first; the cap is structural anti-overwhelm):

1. **Quiz** — only if `focus.hasGatingQuiz`. `status = quizPassed ? Completed : Pending`; `kind=Quiz`;
   `title="Pass the gating quiz"`, `subtitle="Unlocks {focus.videoCount} videos"`; `action=Navigate /sessions/{focus}` label `Start`/`Continue`. Never `blocked`.
2. **Videos** — if `focus.videoCount > 0` and `focus.videosWatched < focus.videoCount`. ONE chunked step (no per-video
   explosion): `kind=Videos`, `progress={done:videosWatched,total:videoCount}`, `title="Watch your lessons"`,
   `subtitle="{videosWatched} of {videoCount} watched"`; `action=Navigate /sessions/{focus}` label
   `videosWatched==0 ? "Watch" : "Continue"`. **`blocked = focus.hasGatingQuiz && !quizPassed`** with
   `blockedReason="Pass the quiz to unlock the videos"`. `status = Completed` when all watched (kept for the Completed list).
3. **Assignment** — if `focus` has a `UserAssignment`. `kind=Assignment`,
   `status = assignment.Completed ? Completed : Pending`, `progress={done:answered,total:questionCount}`,
   `title="Finish your assignment"`, `subtitle` = `Completed ? "Score {scoreMarks}/{maxMarks}" : "{answered} of {questionCount} answered"`;
   `action=Navigate /sessions/{focus}` label `Open`. **Reachable even when the session is expired** (`FR-STU-SES-001`) — assignment steps are never `blocked` by expiry.
   *(Impl: `answered` is computed only for the **focus** assignment — one row, so Include-ing its questions is bounded;
   `scoreMarks` is null until `Completed`, so guard the subtitle.)*
4. **Redeem-next (roll-forward)** — only if the focus session is **fully complete** (videos done, assignment completed,
   quiz passed-or-absent). A **generic** step: `kind=Redeem`, `title="Ready for your next session"`,
   `subtitle="Get a code from your teacher to unlock it"`, `action=Redeem` (no route — the frontend opens `/redeem`).
   *(Optional enhancement, implementer's call: if a single not-yet-enrolled successor whose `PrerequisiteSessionId ==
   focus.sessionId` is cheaply discoverable, name it — `title="Unlock {nextTitle}"`; but a specific-successor lookup is
   **not** a hard requirement, and never fabricate one.)*
5. **Secondary expiry nudges (cross-session, ≤ 2)** — for **other** active enrollments (not `focus`) that are incomplete
   **and** `ExpiringSoon` (`ExpiresAtUtc ≤ now + 14d`), append a compact step: `kind=Videos` (or `Assignment` if videos
   done), `dueState=ExpiringSoon`, `title="{title} expires soon"`, `subtitle="Expires in {n} days — finish your lessons"`,
   `action=Navigate /sessions/{that}` label `Continue`. These carry the **only** time pressure in the plan.

For every step, set `dueState`/`expiresAtUtc` from **its** session's expiry: `Expired` if that session `isExpired` and the
step is incomplete; else `ExpiringSoon` if active & `ExpiresAtUtc ≤ now + 14d`; else `None`. `overdueSteps` =
count(`dueState==Expired && status!=Completed`).

### E.4 Empty / expired-only / all-done plans (deterministic shapes)

- **No enrollments at all:** `focus=null`, `kpis` zeroed, `steps=[ exactly one Redeem step ]` (`title="Redeem a code"`,
  `subtitle="Unlock your first session"`, `action=Redeem`), `recentlyEnrolled=[]`. The step list is **never** `[]` here —
  always the one Redeem step — so the empty state is deterministic; the frontend renders that row plus the mascot
  "Browse the catalogue" empty-state.
- **Only expired enrollments with incomplete assignments:** `focus=null`; append, per expired-incomplete enrollment
  (≤ 3), an **Assignment** step (`dueState=Expired`) — the assignment stays open after expiry (`FR-STU-SES-001`) — then a
  trailing `Redeem` step (`title="Renew access"`, `subtitle="Get a new code from your teacher"`). No fabricated "Renew"
  action; it's a `Redeem`.
- **All enrollments complete:** `focus=null`, `steps=[ one roll-forward Redeem step ]` (`title="Ready for your next
  session"`). Hero → a congratulatory "You're all caught up". KPIs still reflect totals.

### E.5 `recentlyEnrolled` + KPIs

- `recentlyEnrolled` = the caller's non-refunded enrollments ordered by `EnrolledAtUtc` DESC, top **5**, projected to
  `MyPlanRecentDto` (id, title, specialization, enrolledAtUtc). The UI renders "Added N days ago" client-side.
- `kpis` per §A.1, summed over the **active** enrolled set for `activeSessions`/videos/progress, and the **whole**
  non-refunded set for `completedSessions`.

## F. Audit (`FR-PLAT-AUD-002`)

`GET /api/me/plan` is a **pure read — not audited** (parity with `/api/me/sessions`, `/api/me/catalogue`). It changes no
state and never invokes the Play gate. The audited `VideoPlaybackStarted` / assignment / quiz events remain owned by their
existing screens; this Home only *reads* their resulting state and *invalidates its cache* (§D, not an audit concern).

## G. Deferred / **NOT built**

- **No fabricated due dates / reminders / push.** Notifications are deferred backlog (`FR-PLAT-NOT-*`, §14 "optional"); the
  plan surfaces only the real `ExpiresAtUtc` deadline. A weekly **reminder** notification is out of scope here.
- **No editable to-do items / streaks / gamification / points.** Steps are derived; there is no student-authored task, no
  streak counter, no badges (all net-new domain + infra — explicitly out of scope).
- **No `Course`/`Track`/curriculum ordering, no `PublishedAt`/release schedule (Path B).** The plan deliberately does not
  guess "this week's session"; it follows the student's frontier. Revisit Path B only if the teacher confirms a fixed
  sequential weekly course.
- **No student self-enroll / self-renew.** `Redeem` steps open the code modal; extend/refund stay staff-side.
- **The weekly Hangfire `RecurringJob` + Hangfire dashboard are OPTIONAL (§I), not required for v1.** v1 ships
  compute-on-read + event invalidation (§C/§D); the recurring job only pre-warms / rolls the week and lands later with
  notifications.

## H. Frozen vs. stream-owned

- **Frozen (this file):** the `GET /api/me/plan` path + `RequireStudent` + no-params + always-200; the `MyPlanDto` /
  `MyPlanStepDto` / `MyPlanRecentDto` / `kpis` / `focus` field names + types (§A.1); the four enums (§B); the
  **cache key/TTL/tag** scheme (§C); the **invalidation event set** (§D); the **focus-selection order** (§E.2), the
  **step order + cap of 7 + chunked-Videos** (§E.3), the empty/expired/all-done shapes (§E.4); "read, not audited" (§F);
  the deferred/not-built stance (§G). Reuse of the S3 derivations verbatim (§0/§E.1).
- **Backend owns:** the query folder/names (`Features/Me/Queries/GetMyPlan/` or `Features/Sessions/Queries/GetMyPlan/` —
  implementer's call; keep route + DTOs frozen), the DTO + `.ToPlan*Dto()` mapping location, the `MeHomeEndpoints :
  IEndpointGroup` (or extend `MeSessionsEndpoints`) wiring, the **batched** projections (reuse `ListMySessionsHandler`'s
  enrollment projection + `AttendanceProjector` predicate; one round-trip per concern, no N+1), the prerequisite-depth
  walk, the `HybridCache.GetOrCreateAsync` call + the invalidation `INotificationHandler`(s), and the integration tests
  (incl. tenant isolation + the cache-invalidation path).
- **Frontend owns:** the new `feature-home` lib + route (replace `HomePlaceholderComponent` at the shell's empty path),
  the `PlanService` in `data-access`, the hero (greeting from `StudentAuthStore` + headline counts), the 4 KPI cards
  (reuse the dashboard's stat-card CSS/accent/icon patterns by **replication**, not feature→feature import), the
  "Your plan" list (Pending + a collapsed Completed sub-list, read-only ticks, `blocked` disabled rows with reason,
  `dueState` badges, per-`kind` CTA wiring to `route`/redeem), the "This week" bar (`completedSteps/totalSteps`), the
  "Recently enrolled" rail ("Added N days ago" client-side), the mascot empty-state, full responsiveness + WCAG keyboard/
  AT (`FR-STU-RWD-001`/`NFR-A11Y-001`), and the Jest specs.
- **Wiring owns:** proving the slice live on the Aspire stack — `/api/me/plan` returns the right focus + gate-ordered steps
  for a real student across the matrix (no enrollment / fresh enroll / quiz-gated-unpassed → Videos `blocked` / partway
  videos / assignment incomplete / focus complete → roll-forward Redeem / expiring-soon → `ExpiringSoon` / expired-only →
  Expired assignment + Redeem); KPIs match the enrolled set; **cache invalidation** proven (read → watch a video / pass the
  quiz / complete the assignment via the real engines → re-read shows the step moved without waiting for TTL); tenant
  isolation + per-caller correctness; `< 300 ms` warm.

## I. Optional follow-up (not v1) — proactive weekly job + Hangfire dashboard

Recorded so it isn't reinvented; **ship only after v1 is proven and notifications are scoped:**
- A **`RecurringJob.AddOrUpdate`** (the codebase's *first* recurring job — current jobs are all one-shot `Enqueue`/`Schedule`)
  that, at each ISO-week boundary, drops/pre-warms `plan:{studentId}` for active students and (once `FR-PLAT-NOT-*` lands)
  enqueues a "here's your week" notification. Retry-safe, off the request path (`NFR-SCAL-004`).
- **`app.UseHangfireDashboard("/hangfire", …)`** — Hangfire's built-in scheduled/recurring/processing/failed UI (not
  currently wired). **Must** carry an authorization filter restricting it to **Teacher/admin** (the dashboard is
  unauthenticated by default — `NFR-SEC-002/-005`); never expose it open.
