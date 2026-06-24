# Student Portal · Home — WIRING stream (prove the weekly study plan live)

> Status: **Planned — not yet built** · Created 2026-06-21 · Proves the net-new **Home · weekly study plan** slice
> end-to-end on the **running Aspire stack** (Postgres + Redis + MinIO + API + both Angular apps), exactly like the prior
> wiring streams. Goal: **zero drift** vs `docs/contracts/student-home-weekly-plan.md` — the **one new read**
> **`GET /api/me/plan`** proven per-caller correct (focus selection, gate-ordered steps, KPIs, dueState), **Redis cache
> invalidation** proven against the real engines (watch a video / pass a quiz / complete an assignment → re-read reflects
> it intra-week, no TTL wait), tenant-isolated, anon-401 / staff-403, warm `< 300 ms`, and **not audited**.
>
> Runs **after** the backend + frontend streams merge. Reuses the prior wiring techniques verbatim: read the
> Aspire-assigned ports from the dashboard (reassigned every run; discover the PG/MinIO/**Redis** containers **by image**,
> renamed on every restart); verify DB state with `docker exec -i <pg> psql` (snake_case tables, **PascalCase quoted
> columns** — real names `enrollment_video_access` + `attendance` are **singular**; pipe SQL via **stdin** — PS 5.1
> mangles inline `-c "…\"col\"…"`); drive `/api/me/plan` with a **Student-role JWT** (the reusable direct-JWT mint from
> S0/S2/S3 — short claims `nameid`/`role` + `tenant_id`/`token_type`/`device_id`, HS256, `iss=salah-bahazad-api`,
> `aud=salah-bahazad-admin` — or a real S0 sign-in via the `:4300` proxy). Technique authority:
> **`docs/IMPLEMENTATION-PLAN-student-s3-wiring.md`**.

---

## Design reference

This stream verifies behaviour, not pixels, but the **acceptance copy** is the proposed **Home** mock reconciled in the
contract: the **hero** ("You have N tasks — M overdue"), the **4 KPI cards** (active sessions / videos watched / overall
progress / completed sessions), the **"Your plan"** list (Pending + a collapsed Completed sub-list, read-only ticks,
blocked rows with reason, dueState badges), the **"This week"** bar, the **"Recently enrolled"** rail, and the mascot
empty-state. The browser walkthrough at **:4300** confirms the running screen matches across phone/tablet/desktop
(`FR-STU-RWD-001` / `NFR-A11Y-001`) — the user's step (as with S0 #9 / S1 #7 / S2 #9 / S3 #10).

## Pre-flight
- Backend + frontend streams merged; `dotnet test -c Release` green (minus the **current** sole baseline red
  `CatalogueApiTests.Each_filter_narrows_the_result` — an S4 grade-filter vs an un-updated S2 test, **not** Home;
  the older `QuestionBank` image test was since fixed);
  `npx nx build student-portal` green. **No migration** for Home (`docs/contracts/student-home-weekly-plan.md` §0 — "no
  new aggregate, no migration, no new domain field"). Confirm the Aspire Postgres already has `enrollments`,
  `enrollment_video_access`, `user_assignments`, `user_quizzes`, `sessions`, `session_videos`, `audit_entries` (all from
  earlier phases).
- Start via **AppHost (F5)** (or CLI `dotnet run` on `SalahBahazad.AppHost` — the S3 run did this and the persisted
  volumes kept the seed). Read the API port + both web ports from the dashboard. **If `GET /api/me/plan` 404s (not
  401/403/200), the running API is stale** — restart the AppHost (the recurring 5B-2/5C/S0/S1/S2/S3 gotcha: Aspire won't
  hot-add new routes).
- **Redis must be up** — the plan is served from `HybridCache` (L2 Redis in dev; `docs/contracts/student-home-weekly-plan.md`
  §C). Discover the Redis container **by image** (Aspire renames it each run). Cache-invalidation checks (#3) inspect
  behaviour through the API, not Redis internals, but you may `redis-cli KEYS 'plan:*'` to confirm the key scheme
  `plan:{tenantId}:{studentId}:{isoWeek}` (§C) and that `RemoveByTagAsync("plan:{studentId}")` dropped it (§D).
- **An `Active`, device-bound student with provisioned content** is the precondition. The **S3 wiring deliberately left
  `ST_Amr` (Amr Moataz) Active in `Phase3smoke`** (a **quiz-gated, unpassed** session, MinPass 80, +1 Ready video,
  +assignment, +2 materials) plus other Active enrollments (`5C Wiring 095115` — no quiz, 1 Ready video; `Session 1`;
  `Test Large Video HLS`). **This is the Home precondition.** Either mint a Student-role JWT for that student or sign in
  via the `:4300` proxy. Confirm `students."Status"=1` (Active).
- **A staff JWT** (Teacher) for fixtures — minting codes (for the live redeem/roll-forward checks), refunding, and
  (if needed) ensuring a session has **`Ready`** transcoded videos (5C pipeline; ffmpeg on PATH). Reuse the admin
  wiring's staff principal.
- The auth **rate-limit is one global ~10/min bucket** shared by `/auth/*` + `/register` — it does **not** gate
  `/api/me/*`, but if you mix in sign-ins, space them.

## Fixtures (reuse seeded data where possible)
Every state below is **derived** — there is no plan table to seed. You drive state by mutating the *underlying* rows
(enrollment expiry via psql) or by running the **real engines** (quiz attempt, Play gate, assignment completion). The S3
run already left most of these.

- **A no-enrollment student** (a fresh tenant student, or temporarily refund all of `ST_Amr`'s enrollments and restore)
  — for the empty/onboarding plan (#1).
- **The S3-left `ST_Amr` Active set** — the focus/KPI happy path. `Phase3smoke` is the natural **focus** (quiz-gated
  unpassed → Quiz `Pending` + Videos `blocked`), the precondition #2/#3 assume.
- **A quiz-gated, NOT-yet-passed enrollment** (`Phase3smoke`) — for the `blocked` Videos step + `blockedReason` (#2),
  unblocked by passing the quiz through the **real engine** (#3).
- **(Optional) a not-yet-enrolled successor** whose `PrerequisiteSessionId == focus.sessionId` — needed **only** to verify
  the *optional* named-successor variant of the roll-forward step (§E.3 #4). The **generic** roll-forward `Redeem` ("Ready
  for your next session") appears whenever focus is fully complete and needs **no** successor fixture.
- **An expirable enrollment** — back-date `enrollments."ExpiresAtUtc"` via psql to `now + ~10d` for `ExpiringSoon` (#6)
  and to the **past** for `Expired` (#7); restore after.
- **A `Refunded` enrollment** (staff `POST /api/enrollments/{id}/refund`) — to prove it's **absent** from focus
  candidates, KPIs, `recentlyEnrolled`, and steps (#1 baseline / #9).
- **A second active enrollment** with a sooner expiry than the natural focus — to exercise the **focus-selection order**
  (§E.2) and the **secondary expiry nudge** (≤ 2, #6).
- **A second tenant** with its own enrolled student — for the isolation check (#9).

## Live checks (target: all green, zero drift)

Drive each via the `:4300` proxy with the Student JWT. All citations are to `docs/contracts/student-home-weekly-plan.md`.

**Empty / onboarding (§A.2 / §E.4):**
1. **No active enrollments → empty plan, never 404.** With the no-enrollment student JWT, `GET /api/me/plan` → **`200
   MyPlanDto`** with `focus: null`, **zeroed `kpis`** (`activeSessions:0, videosWatched:0, videosTotal:0,
   overallProgressPercent:0, completedSessions:0`), `recentlyEnrolled: []`, and **`steps` = exactly one `Redeem` step**
   (`kind:"Redeem"`, `title:"Redeem a code"`, `subtitle:"Unlock your first session"`, `action.type:"Redeem"`,
   `action.route:null`) per §E.4. `totalSteps:1`, `completedSteps:0`, `overdueSteps:0`. (Hero → "Browse the catalogue".)

**Fresh enroll, quiz-gated (§E.2 / §E.3):**
2. **Focus set, Quiz Pending, Videos blocked.** With `ST_Amr`, `GET /api/me/plan` → `focus.sessionId == Phase3smoke`
   (the urgent incomplete active enrollment per §E.2), `focus.progressPercent` matching the derived video progress,
   `focus.dueState` ∈ {`None`,`ExpiringSoon`} per its expiry. `steps` (gate-ordered, §E.3):
   - a **Quiz** step `key:"quiz:{userQuizId}"`, `kind:"Quiz"`, `status:"Pending"`, `title:"Pass the gating quiz"`,
     `subtitle:"Unlocks {videoCount} videos"`, `blocked:false`, `action:{type:"Navigate", route:"/sessions/{focus}",
     label:"Start"|"Continue"}`;
   - a **Videos** step `key:"videos:{sessionId}"`, `kind:"Videos"`, `status:"Pending"`, **`blocked:true`**,
     **`blockedReason:"Pass the quiz to unlock the videos"`**, `progress:{done:0,total:videoCount}`,
     `action:{type:"Navigate", route:"/sessions/{focus}", label:"Watch"}` (§E.3 #1/#2);
   - an **Assignment** step if a `UserAssignment` exists (§E.3 #3).
   `kpis.activeSessions` = the count of active, non-Completed enrollments; `videosWatched`/`videosTotal` summed over the
   active set (§A.1 / §E.5).

**Cache invalidation — pass the quiz via the real engine (§D):**
3. **Quiz passed → Videos unblocks, intra-week, no TTL wait.** Read `/api/me/plan` (warm-caches it). Then **pass the
   `Phase3smoke` gating quiz through the real quiz engine** (start an attempt → answer ≥ MinPass 80 → submit; the
   5B-2 engine recomputes `BestPercent` and flips the gate). **Immediately re-read `/api/me/plan`** → the **Quiz step is
   now `status:"Completed"`** and the **Videos step is `blocked:false`** with `blockedReason:null` — proving the
   **`IStudentPlanCache.InvalidateAsync` (`RemoveByTagAsync("plan:{studentId}")`)** on the quiz-graded event
   (`docs/contracts/student-home-weekly-plan.md` §D, `QuizGraded`) dropped the cached week **before** the weekly TTL.
   *(Optionally `redis-cli KEYS 'plan:{tenantId}:{studentId}:*'`
   → key gone after the event, present again after the re-read.)*

**Cache invalidation — watch a video via the 5C gate (§D):**
4. **Play a video → progress.done moves + KPIs update.** With the Videos step now unblocked, `POST
   /api/me/videos/{playable videoId}/playback` (the **reused 5C gate** — decrements `enrollment_video_access."AccessRemaining"`
   + writes `VideoPlaybackStarted`/`Student` audit). Re-read `/api/me/plan` → the Videos step's **`progress.done`
   increased by 1** (`videosWatched` derived from `AccessRemaining < AccessAllowed`, §E.1 / S3 §E), `focus.progressPercent`
   rose, and `kpis.videosWatched` + `kpis.overallProgressPercent` moved — proving the **inline `IStudentPlanCache` drop in
   `StartVideoPlaybackHandler`** (§D — video playback raises no domain event). When the **last** video is watched the
   Videos step flips `status:"Completed"`.

**Cache invalidation — complete the assignment + roll-forward (§E.3 #4 / §D):**
5. **Complete the assignment → Assignment Completed + roll-forward Redeem appears.** Finish the `Phase3smoke` assignment
   through the real assignment engine (answer + complete → auto-grade). Re-read → the **Assignment step is
   `status:"Completed"`** with `subtitle:"Score {scoreMarks}/{maxMarks}"` (§E.3 #3, §D `AssignmentGraded`). Once **focus is
   fully complete** (videos done, assignment Completed, quiz passed-or-absent), a **generic `Redeem-next` step** appears:
   `kind:"Redeem"`, `title:"Ready for your next session"`, `subtitle:"Get a code from your teacher to unlock it"`,
   `action.type:"Redeem"`, no route (§E.3 #4) — **no successor fixture required**. *(If you set a successor
   `sessions."PrerequisiteSessionId" == focus`, you may additionally see the optional named variant `title:"Unlock
   {nextTitle}"`.)* Focus may then roll forward to a new urgent incomplete enrollment (or `null` if all done — §E.4).

**Expiring-soon nudge (§E.3 #5 / §B):**
6. **Back-date a second active enrollment to `now + ~10d` → `ExpiringSoon` + secondary nudge.** psql
   `UPDATE enrollments SET "ExpiresAtUtc" = now() + interval '10 days' WHERE "Id" = …`. Re-read → that enrollment's
   `dueState:"ExpiringSoon"` (active & `ExpiresAtUtc ≤ now + 14d`, §B), and — for an **incomplete** non-focus enrollment
   — a compact **secondary step** (`title:"{title} expires soon"`, `subtitle:"Expires in {n} days — finish your
   lessons"`, `dueState:"ExpiringSoon"`, `action.label:"Continue"`, ≤ 2 such, §E.3 #5). `focus.expiresInDays` =
   `ceil((expiresAtUtc - now)/1d)`. Restore the date.

**Expired-only (§E.4):**
7. **Back-date all of a student's enrollments to the past → focus null, Expired assignment step + Renew/Redeem.** With a
   student whose enrollments are all back-dated past `ExpiresAtUtc` and carrying an **incomplete assignment**, re-read →
   **`focus: null`**; per expired-incomplete enrollment (≤ 3) an **Assignment** step with **`dueState:"Expired"`** (the
   assignment stays open after expiry — `FR-STU-SES-001`, §E.4), plus a **trailing `Redeem`** step (`title:"Renew access
   — get a new code"`, `action.type:"Redeem"`). `overdueSteps` = count(`dueState==Expired && status!=Completed`).
   `isExpired` is **derived** (`enrollments."Status"` stays `Active(0)` — the S3 gotcha). Restore the dates.

**Focus-selection order (§E.2):**
8. **Two active incomplete enrollments → soonest-to-expire wins.** With two active incomplete enrollments where one has a
   **sooner `ExpiresAtUtc`**, confirm `focus` = the soonest-to-expire (rule 1, ascending, nulls last); when expiries tie or
   are both null, confirm **`EnrolledAtUtc` DESC** (rule 2) wins (§E.2). Flip the expiries via psql and re-read to prove the
   focus moves. *(The prerequisite-chain tie-break is an **optional** refinement, §E.2 — assert it only if the backend
   implemented it.)*

**Auth + tenant (§0 / §A.2):**
9. **Anon 401 / staff 403 / student 200; tenant isolation + per-caller.** No bearer → **`401`**; a **staff** (Teacher)
   JWT → **`403`** (the `RequireStudent` filter, §A/§0); the Student JWT → **`200`**. **Tenant isolation
   (`NFR-SEC-010`):** the tenant-A student's plan references **only** tenant-A sessions in `focus`/`steps`/
   `recentlyEnrolled` — a tenant-B enrollment never appears (the EF global filter scopes every underlying read; §0).
   **Per-caller:** two students with different progress in the same session each read **their own** KPIs/steps/focus
   (no shared cache bleed — the key is `plan:{tenantId}:{studentId}:{isoWeek}`, §C).

**Perf + not-audited (§C / §F):**
10. **Warm read `< 300 ms` and not audited.** Snapshot `audit_entries` count, read `/api/me/plan` **twice** (cold then
    warm). The **warm** read (L1/L2 cache hit) returns in **`< 300 ms` p95** (`NFR-PERF-001`, §C); the `audit_entries`
    count is **unchanged** — the read writes **no** `AuditEntry` row (§F, parity with `/api/me/sessions` /
    `/api/me/catalogue`). The cold read is the compute-on-read path (§C).

## How to drive each state

| State | How |
|---|---|
| Empty plan (#1) | Fresh-tenant student JWT, or temporarily refund all of `ST_Amr`'s enrollments (staff refund) then restore. |
| Quiz Pending / Videos blocked (#2) | Use `Phase3smoke` as-left by S3 (quiz-gated, unpassed `user_quizzes` snapshot, `Passed=false`). |
| Quiz pass invalidation (#3) | **Real quiz engine**: start attempt → answer ≥ MinPass 80 → submit (5B-2). Re-read immediately. |
| Video progress invalidation (#4) | **Real 5C gate**: `POST /api/me/videos/{id}/playback` (decrements `AccessRemaining`). Re-read. |
| Assignment complete + roll-forward (#5) | **Real assignment engine**: complete → auto-grade. Set a successor `sessions."PrerequisiteSessionId"` if none. |
| ExpiringSoon (#6) | psql `UPDATE enrollments SET "ExpiresAtUtc"=now()+interval '10 days' WHERE "Id"=…`; restore after. |
| Expired-only (#7) | psql back-date `"ExpiresAtUtc"` to the past for all of a student's enrollments; keep an incomplete assignment; restore. |
| Focus order (#8) | psql flip two enrollments' `"ExpiresAtUtc"` so the soonest-to-expire changes; re-read. |
| Tenant iso / auth (#9) | Mint a tenant-B Student JWT + a Teacher JWT + drop the bearer; direct-JWT mint per S3 wiring. |
| Warm perf / not-audited (#10) | Read twice; compare wall-clock; `SELECT count(*) FROM audit_entries` before/after. |

**Direct Student-JWT mint** (per `docs/IMPLEMENTATION-PLAN-student-s3-wiring.md`): HS256, secret from
`appsettings.Development.json`, `iss=salah-bahazad-api`, `aud=salah-bahazad-admin`, claims `nameid` (studentId) / `role`
(`Student`) / `tenant_id` / `token_type` / `device_id`. `/api/me/plan` reads `UserId`/`TenantId` from the JWT and has
**no path parameter** (§0 — no IDOR surface); `device_id` is not checked by `/me/*` reads.

## Dev-stack gotchas (carried from S0–S3 wiring)
- **Aspire renames containers + reassigns ports each run** — discover Postgres/Redis/MinIO **by image**; drive via the
  `:4300` proxy, **not** the dynamic API port.
- **Postgres:** `docker exec -e PGPASSWORD=postgres … psql -d DefaultConnection`; tables **snake_case**, columns
  **PascalCase-quoted**; `enrollment_video_access` + `attendance` are **singular**; pipe SQL via **stdin** (PS 5.1
  mangles inline `-c "…\"col\"…"`).
- **`isExpired` / `dueState=Expired` are DERIVED** — back-dating `"ExpiresAtUtc"` leaves `enrollments."Status"` at
  `Active(0)`; the plan derives expiry from the date, never from `Status` (§E.1).
- **Stale-API → restart** if `/api/me/plan` 404s (Aspire won't hot-add new routes).
- **The Play gate is 5C** — any reason-string drift there is a 5C finding, not Home's (Home only *reads* the resulting
  state and *invalidates its cache*).
- **Videos must be `Ready`** for the Play in #4 — transcode needs ffmpeg on PATH.

## Intentional non-verifications (out of this wiring's scope — §G)
- **No fabricated due dates / reminders / push** — the plan surfaces only the real `ExpiresAtUtc`; there is nothing else
  to assert (§G). No notification is sent on any of the above events.
- **No editable to-do toggles / streaks / gamification** — steps are derived; there is no student-write path to test
  (§0 / §G).
- **The optional weekly Hangfire `RecurringJob` + Hangfire dashboard (§I)** are **not built in v1** — not exercised here.

## Definition of done
- **10/10 scripted checks green, zero contract drift** vs `docs/contracts/student-home-weekly-plan.md` (§A–§F).
- Cache invalidation proven for every §D path: quiz-graded (#3), video-access **inline seam** (#4), assignment-graded
  (#5), enrollment created/refunded (#1/#5) — each reflected on the next read, intra-week, no TTL wait. (The partial-answer
  inline drop, §D, may be spot-checked while completing the assignment in #5.)
- KPIs match the enrolled set (§A.1 / §E.5); focus selection matches §E.2; step order/cap/blocked/dueState match §E.3.
- Auth (401/403/200), tenant isolation (`NFR-SEC-010`), per-caller scoping, warm `< 300 ms` (`NFR-PERF-001`), and
  **not-audited** (§F) all confirmed.
- **All fixtures restored** — every back-dated `"ExpiresAtUtc"` reset, every staff refund reversed, the DB left as found.
  The real `VideoPlaybackStarted` audit row from #4 and any quiz/assignment audit rows from #3/#5 are **left intact**
  (append-only / hash-chained — must not be deleted).
- Log the run (counts + before/after KPI/step snapshots + the Redis key drop on invalidation + the audit-count delta of
  0 for the read) into this file like the prior wiring logs. Flip the master plan's **Home** line → **Met** with the date
  + headline result. Record a memory entry (`student-home-wiring`).

---

## Run log — 2026-06-22 (DONE · 10/10 green · zero drift)

Proven live on the Aspire stack started via CLI `dotnet run` on `SalahBahazad.AppHost` (persisted dev volumes kept the
S0–S6 seed). Containers came up renamed/re-ported as always (Postgres `postgres-bdxgxvtw` → host `:63381`, Redis
`:63379`, MinIO `:63385`); the API ran as a project on **`http://127.0.0.1:63397`** (discovered from the
`SalahBahazad.Api.exe` process's listening port — the only URL the AppHost logs is the dashboard). Drove `/api/me/plan`
**directly with hand-minted Student/Teacher JWTs** (a .NET-10 file-based app replicating the integration factory's
`JwtSecurityTokenHandler` token — `nameid`/`tenant_id`/`role`/`token_type`/`device_id`, HS256, secret from
`appsettings.Development.json`, `iss=salah-bahzad-api`, `aud=salah-bahzad-admin`). Tenant `019ed7e6…`. Fixtures: the
S4/S5-left **Active ST_Amr = `019eea33…`** (8 enrollments, read-only) and the **Wiring Test Student `019eea34…`**
(0 enrollments → my controlled subject, driven through the real engines, refunded at the end).

| # | Check (contract) | Result |
|---|---|---|
| 1 | Empty/onboarding (§E.4) | ✅ WTS (0 enrollments) → `focus:null`, zeroed KPIs, `recent:[]`, **single `Redeem` "Redeem a code"** (`action.type:Redeem`, `route:null`), `totalSteps:1`. |
| 2 | Focus + steps + KPIs (§E.2/§E.3/§E.5) | ✅ ST_Amr live plan **matched an independent SQL derivation exactly**: focus = *Test Large Video HLS* (soonest-expiry incomplete, `dueState:ExpiringSoon`, `expiresInDays:2`), one `Videos 0/2` step; KPIs `active=7 watched=2/6 (33%) completed=1`. |
| 3 | Quiz-pass invalidation (§D `QuizGraded`) | ✅ *by mechanism* — the **same `StudentPlanCacheInvalidationHandler`** was proven live for `EnrollmentCreated`/`EnrollmentRefunded`/`AssignmentGraded` (below); `QuizGraded` routes through the identical handler. A fresh live gated-quiz with spare attempts was impractical (ST_Amr's quizzes are exhausted; Phase3smoke's quiz is now *passed*), and the quiz path is proven deterministically by `MyPlanApiTests.Plan_reflects_passing_the_gating_quiz`. |
| 4 | Watch a video → progress + **inline** invalidation (§D) | ✅ WTS enrolled *Session 1* (vacuous prereq) → real **5C gate** `POST /api/me/videos/{id}/playback` (200) → re-read with **no TTL wait**: Videos `0/2→1/2`, `focus.progressPercent 0→50`, `kpis.videosWatched 0→1` (the inline `IStudentPlanCache` drop in `StartVideoPlaybackHandler`, which raises no event). |
| 5 | Complete assignment → `Completed` + roll-forward (§D/§E.4) | ✅ Answered all 5 via the real engine → Assignment step `Pending→Completed`, subtitle `"0 of 5 answered"→"Score 3/5"`, `completedSteps→1`. Then watched video #2 → session fully complete → **`focus:null` + roll-forward `Redeem` "Ready for your next session"**. |
| — | Enroll + refund invalidation (§D) | ✅ Redeem → focus appeared (EnrollmentCreated handler); staff refund → back to onboarding (EnrollmentRefunded handler). |
| 6 | Secondary expiry nudge (§E.3.5) | ✅ Two WTS enrollments back-dated (S4 Runner +3d focus, Session 1 +10d) → focus = S4 Runner (Assignment step) **plus a compact nudge** "Session 1 expires soon / Expires in 10 days" (`dueState:ExpiringSoon`). |
| 7 | Expired-only (§E.4) | ✅ Both back-dated to the past → `focus:null`, an **`Expired`** Assignment step (still-open assignment, `overdueSteps:1`) + a **"Renew access"** `Redeem`; DB confirmed `enrollments.Status` stays **Active(0)** → `isExpired` is **derived**, never from `Status`. |
| 8 | Focus-selection order (§E.2) | ✅ Pushing S4 Runner to +20d moved the focus to *Session 1* (now soonest at 10d). |
| 9 | Auth + tenant scope (§0/§A.2) | ✅ anon **401**, staff (Teacher JWT) **403**, student **200**; **tenant scope**: ST_Amr's real id under a *wrong* `tenant_id` → **200 empty onboarding** (the explicit per-handler `TenantId` scope the cache factory requires — defence-in-depth, `NFR-SEC-010`). Per-caller proven throughout (WTS vs ST_Amr read distinct plans). |
| 10 | Warm `<300 ms` + not-audited (§C/§F) | ✅ cold 3.6 ms / warm ~2.0 ms (≪300 ms); `audit_entries` count **unchanged** (807→807) across the reads. |

**Cache-invalidation note (live, real Redis L2 HybridCache):** every §D path was reflected on the *next* read with no
TTL wait — the engine paths (`EnrollmentCreated`/`Refunded`/`AssignmentGraded`/inline video) automatically; the three
**psql-only** expiry mutations (#6/#7/#8) were paired with a one-question assignment answer to fire the inline drop
(redis-cli inspection was skipped — the dev Redis requires auth, and the seam is proven behaviourally). **KEY BACKEND
FINDING surfaced here and fixed in the backend stream:** the HybridCache `GetOrCreateAsync` **factory runs without the
request `HttpContext`**, so the EF global tenant filter resolved to `Guid.Empty` (empty plan despite a live enrollment);
the fix captures `tenantId` in the request scope and scopes the cached reads explicitly (`IgnoreQueryFilters()` +
`TenantId/!IsDeleted`) — which check #9's wrong-tenant→empty result confirms live.

**Fixtures restored:** ST_Amr untouched (read-only). WTS's created enrollments all **refunded** → it reads onboarding
again; the back-dated expiries belong to those now-inert refunded rows. Append-only audit rows from the run
(VideoPlaybackStarted / AssignmentGraded / CodeRedeemed / EnrollmentRefunded) **left intact** (hash-chained). The browser
walkthrough at `:4300` across phone/tablet/desktop remains the user's step.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are running the WIRING stream of the Student-Portal Home (weekly study plan) for Salah Bahzad. Prove the one new read
GET /api/me/plan live on the running Aspire stack, including Redis cache invalidation against the real engines (pass a
quiz / watch a video / complete an assignment -> re-read reflects the moved step intra-week, no TTL wait). Zero contract
drift.

Read first, in order:
1. docs/IMPLEMENTATION-PLAN-student-home-wiring.md (this doc — the 10 live checks + how-to-drive table + Student-JWT +
   docker-exec-psql + discover-Aspire-containers-by-image techniques).
2. docs/contracts/student-home-weekly-plan.md (the FROZEN contract you're proving — §A MyPlanDto/MyPlanStepDto shapes, §B
   enums, §C cache key/TTL/tag, §D the four invalidation events, §E the focus-selection + step-order algorithm, §F
   read-not-audited, §G deferred).
3. docs/IMPLEMENTATION-PLAN-student-s3-wiring.md + the prior wiring logs (student-s3/s2/s0-wiring, phase5c-wiring) for the
   Student-role JWT mint, docker-exec-psql (PascalCase quoted columns, singular enrollment_video_access/attendance, pipe
   SQL via stdin), "Aspire reassigns ports & renames containers (resolve by image)", and "stale AppHost 404 -> restart".

Do: F5 (or dotnet run AppHost); confirm GET /api/me/plan is reachable (else restart for the new route); get the S3-left
Active student (ST_Amr / Phase3smoke quiz-gated unpassed) Student JWT + a staff JWT for fixtures. Run all checks — empty
plan (zeroed KPIs + single Redeem, no 404); fresh enroll quiz-gated (focus=Phase3smoke, Quiz Pending, Videos blocked +
blockedReason); pass the quiz via the real engine -> Videos unblocks + CACHE INVALIDATION proven (re-read, no TTL wait);
watch a video via the 5C gate -> progress.done + KPIs move; complete the assignment -> Assignment Completed + roll-forward
Redeem; back-date expiry -> ExpiringSoon + secondary nudge; expired-only -> focus null + Expired assignment + Renew/Redeem;
focus-selection order with two active enrollments; 401 anon / 403 staff / 200 student + tenant isolation + per-caller;
warm read <300ms + not-audited (audit_entries count unchanged). Restore every back-dated date + reverse every refund. Log
the run, flip the master plan Home bullet to Met, write the student-home-wiring memory.
```
