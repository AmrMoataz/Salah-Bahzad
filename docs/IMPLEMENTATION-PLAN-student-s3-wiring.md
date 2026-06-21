# Student Portal · S3 — WIRING stream (prove my-sessions, session detail & the video gate live)

> Status: **Planned — not yet built** · Created 2026-06-21 · Proves slice **S3**
> (`docs/IMPLEMENTATION-PLAN-student-portal.md` §S3) end-to-end on the **running Aspire stack** (Postgres + Redis +
> MinIO + API + both Angular apps), exactly like the prior wiring streams. Goal: **zero drift** vs
> `docs/contracts/student-s3-my-sessions-video.md` — the **two new reads** (`GET /api/me/sessions` +
> `GET /api/me/sessions/{id}`) and the **material signed-URL read** proven per-caller correct (progress, expiry, lock
> states, gate banner), tenant-isolated and IDOR-safe; and the **deep-link Play** closing through the **reused** Phase-5C
> gate (`POST /api/me/videos/{id}/playback` → **decrement** + `VideoPlaybackStarted` audit + one-time handoff code).
>
> Runs **after** the backend + frontend streams merge. Reuses the prior wiring techniques: read the Aspire-assigned ports
> from the dashboard (reassigned every run; discover the PG/MinIO containers **by image**, renamed on every restart);
> verify DB state with `docker exec -i <pg> psql` (snake_case tables, **PascalCase quoted columns** — real names
> `enrollment_video_access` + `attendance` are **singular**; pipe SQL via **stdin** — PS 5.1 mangles inline `-c
> "…\"col\"…"`); drive the student endpoints with a **Student-role JWT** (the reusable direct-JWT mint from
> S0/S2/phase5b — short claims `nameid`/`role` + `tenant_id`/`token_type`/`device_id`, HS256, `iss=salah-bahazad-api`,
> `aud=salah-bahazad-admin` — or a real S0 sign-in via the `:4300` proxy).

---

## Design reference

This stream verifies behaviour, not pixels, but the **acceptance copy** is the **Student Portal** prototype
(`MY SESSIONS` spotlight + `SESSION DETAIL`). Confirm the running screens at **:4300** match the prototype responsively
while driving the browser check (the spotlight "Jump back in" hero, the filter chips, the detail playlist with lock/access
badges, the gate banner, materials download, and the **Play → deep-link** attempt + install prompt).

## Pre-flight
- Backend + frontend streams merged; `dotnet test -c Release` green (minus the known baseline image test);
  `npx nx build student-portal` green. **No migration** for S3 — confirm the Aspire Postgres has `sessions`,
  `session_videos`, `session_materials`, `enrollments`, `enrollment_video_access`, `user_assignments`, `user_quizzes`,
  `attendance`, `audit_entries` (all from earlier phases; `session_videos.hls_key_object_key` from 5C).
- Start via **AppHost (F5)**. Read the API port + both web ports from the dashboard. **If `GET /api/me/sessions` 404s
  (not 401/403/200), the running API is stale** — restart the AppHost (the recurring 5B-2/5C/S0/S1/S2 gotcha: Aspire
  won't hot-add new routes).
- **An `Active`, device-bound student with provisioned content** is the precondition. S2's wiring deliberately left
  **`ST_Amr` Enrolled (Active) in `Phase3smoke`** with video-access counters + an attendance shell + an assignment (+
  quiz) snapshot — **this is the S3 precondition**. Either mint a Student-role JWT directly for that student or sign in
  through the `:4300` proxy. Confirm `students."Status"=1` (Active).
- **A staff JWT** (Teacher) for fixtures — minting codes, refunding, and (if needed) ensuring a session has **`Ready`**
  transcoded videos (5C's pipeline; ffmpeg on PATH). Reuse the admin wiring's staff principal.
- The auth **rate-limit is one global ~10/min bucket** shared by `/auth/*` + `/register` — it does **not** gate
  `/api/me/*`, but if you mix in sign-ins, space them.

## Fixtures (reuse seeded data where possible)
- **The S2-left `ST_Amr` / `Phase3smoke` Active enrollment** — the happy-path session (has videos with access counters,
  an assignment snapshot, and — from S2's prereq-satisfy step — a quiz snapshot). Confirm it has **≥ 1 `Ready` video**
  with `AccessRemaining > 0` (for the Playable + Play-gate checks) and **≥ 1 material** (for §C). If videos aren't
  `Ready`, transcode one via the 5C pipeline (staff add-video → wait `Ready`).
- **A quiz-gated session the student is enrolled in but has NOT passed** — for `QuizLocked` / `gateState=QuizRequired` /
  the `quiz_required` gate reason. (S2 left a `user_quizzes` snapshot; ensure `Passed=false` — a fresh snapshot is
  unpassed.)
- **An expirable enrollment** — back-date its `enrollments."ExpiresAtUtc"` to the **past** via psql to exercise
  `isExpired` / `lockState=Expired` / `gateState=Expired` / the `enrollment_expired` gate reason **and** prove the
  **assignment + materials stay reachable** after expiry (`FR-STU-SES-001`). Restore it after.
- **A `Refunded` enrollment** (staff `POST /api/enrollments/{id}/refund`) — to prove it's **absent** from the list and
  **404** on detail + material.
- **A `Processing` (not-`Ready`) video** in an enrolled session — for `lockState=NotReady` / the `not_ready` gate `409`.
  (Add a video and check it **before** it finishes transcoding, or leave one mid-pipeline.)
- **A second tenant** with its own enrolled student + session — for the isolation checks.
- **A second student** (A/B) enrolled in the **same** session with different progress — for per-caller scoping.

## Live checks (target: all green, zero drift)

**My-sessions read (`§A`):**
1. `GET /api/me/sessions` (Student JWT, no params) → **`200`** a flat `[MySessionDto]` of the caller's **non-refunded**
   enrollments (the **`Refunded`** fixture **absent**), **DESC by `EnrolledAtUtc`**, each with grade/subject/spec names,
   `videoCount`, `videosWatched`, `progressPercent`, `enrolledAtUtc`/`expiresAtUtc`/`isExpired`, and a **`thumbnailUrl`
   that resolves** (`GET` the signed URL → `200 image`) where keyed (null otherwise). A student with no enrollments →
   `[]`.
2. **Progress (`§E.1`) — the key projection:** before any view, the happy-path session reads `videosWatched: 0`,
   `progressPercent: 0`, `state: "NotStarted"`. **After check #7's Play** (which decrements one view), the **same**
   session reads `videosWatched: 1` and a non-zero `progressPercent` — proving progress is **derived from the gate's
   decrement** (`AccessRemaining < AccessAllowed`), not a stored counter. Spend all views → `state: "Completed"`.
3. **Expiry + `state` filter (`§A.1`/`§E.2`):** back-date one `Active` enrollment's `"ExpiresAtUtc"` → the row reads
   **`isExpired: true`** (proves it's **derived** — `"Status"` stays `Active(0)`); `?state=Expired` returns only it;
   `?state=ExpiringSoon` returns only `≤14d`-non-expired rows; `?state=InProgress`/`Completed`/`NotStarted` match the
   derived completion. Restore the date.

**Session-detail read (`§B`):**
4. `GET /api/me/sessions/{Phase3smoke}` → **`200 MySessionDetailDto`**: `videos` **ordered by `Order`**, each with
   `accessAllowed`/`accessRemaining` + a **`lockState`**; `materials` ordered by `CreatedAtUtc`; `assignment` populated;
   `quiz` populated (quiz-gated). **`lockState` across states (`§E.3`), on real rows:** a `Ready` video with
   `accessRemaining > 0` → **`Playable`** ("{n} of {m} views"); spend it to `0` (#7 repeatedly, or psql) →
   **`Exhausted`** ("0 views left"); the **quiz-gated** session's videos → **`QuizLocked`** while unpassed; the
   **back-dated** session's videos → **`Expired`**; the **`Processing`** video → **`NotReady`**. `gateState` matches:
   `Open` / `QuizRequired` (+ `minPassPercent`) / `Expired`. **Confirm a `Playable` badge ⇔ the gate passes in #7** and a
   locked badge ⇔ the matching gate `reason` in #8.
5. **404 IDOR / tenant / refunded boundary (`§B.2`):** `GET /api/me/sessions/{id}` for a session the caller is **not**
   enrolled in → **`404`**; for the **`Refunded`** enrollment's session → **`404`**; for a **tenant-B** session →
   **`404`** (never the other student's data).

**Material signed-URL read (`§C`):**
6. `GET /api/me/sessions/{Phase3smoke}/materials/{mid}/url` → **`200 SignedUrlDto`** whose `url` **resolves** (`GET` →
   `200`, the file bytes). The **same** material once the enrollment is **back-dated expired** → still **`200`**
   (materials stay available, `FR-STU-SES-001`). A material of a **`Refunded`** enrollment's session → **`404`**; a
   `materialId` **not** of the session → **`404`**. **Not audited:** snapshot `audit_entries` count before/after → **no
   new row** for the read (parity with the admin material read).

**Deep-link Play gate (reused 5C, proven through the new reads) (`§D`):**
7. `POST /api/me/videos/{Playable videoId}/playback` (Student JWT) → **`200 PlaybackHandoffDto`** `{ handoffCode,
   expiresAtUtc }` (a one-time code, ~60 s TTL — **never** a URL/manifest/key). **DB (psql):** the caller's
   `enrollment_video_access."AccessRemaining"` for that video **decremented by 1**; an `audit_entries` row
   **`Action=VideoPlaybackStarted`**, **`ActorType=Student`**, `EntityType=SessionVideo`. Then **re-run #2 + #4** → the
   session's `videosWatched`/`progressPercent` **increased** and the video's `accessRemaining` **dropped** (toward
   `Exhausted`). *(The redeem (#2) + AES-key (#3) chain is **5C's** wiring — not re-driven here; the portal calls only
   the gate. Optionally confirm the handoff code is one-time by redeeming it via the 5C route once → `200`, twice →
   `410`.)*
8. **Gate reason ladder (`§D.2`) — each a specific reason (`FR-STU-VID-004`):** with the Student JWT — a **`Processing`**
   video → **`409 not_ready`**; a video of a **non-enrolled** session → **`403 not_enrolled`**; a video of the
   **back-dated** session → **`403 enrollment_expired`**; a video of the **quiz-gated-unpassed** session →
   **`403 quiz_required`**; a video **spent to 0** → **`403 no_views_remaining`**. Each a ProblemDetails with the machine
   `reason` + readable `detail`. **Confirm each matches the detail's `lockState` in #4.**

**Auth + tenant (every new read):**
9. Anonymous (no bearer) → **`401`**; a **staff** JWT → **`403`**; the Student JWT → **`200`** on all three reads.
   **Tenant isolation (`NFR-SEC-010`):** the tenant-A student's list contains **only** tenant-A sessions; `GET
   /api/me/sessions/{tenant-B session}` → **`404`**. **Per-caller (`NFR-SEC-007`):** students A and B enrolled in the
   **same** session each read **their own** `videosWatched`/`progressPercent`/`accessRemaining`/`lockState`.

**The screens, live in the browser (`FR-STU-RWD-001/002`, `FR-STU-A11Y-001`):**
10. Open the student app at **:4300**, sign in, open **My Sessions** → the **spotlight "Jump back in"** hero + the
    **divided list** render with progress + **expiry chips**; the **filter chips** narrow client-side; open a session →
    the **detail** shows the playlist with **lock/access badges**, the **gate banner** when quiz-gated/expired, the
    **Assignment/Quiz/Materials** cards (Assignment reachable when expired), and a **circular progress ring**; **Play**
    on a playable video fires the gate and attempts the **`salah-bahazad://` deep link** (and the badge/progress update),
    a locked row shows **🔒**, and a gate failure shows the specific reason; **Materials → Download** opens the signed
    URL. Resize: spotlight + playlist reflow to phone, comfortable targets, matches the prototype across phone/tablet/
    desktop. *(The visual walkthrough is the user's step, as with S0 #9 / S1 #7 / S2 #9. The native app handling the deep
    link isn't built — confirm the **install prompt** appears when no app is registered.)*

## Sign-off
- Log the run (counts + the `enrollment_video_access."AccessRemaining"` before/after Play + the `audit_entries`
  `VideoPlaybackStarted` row + the my-sessions states + the detail `lockState`/`gateState` matrix + the gate reason
  ladder) into this file like the prior wiring logs. Update the master plan's **S3** line from *Planned* → **Met** with
  the date + headline result. Record a memory entry (`student-s3-wiring`). Note any gotchas (expect: Aspire port/name
  reassignment + discover-by-image; stale-API-needs-restart for the new routes; `isExpired`/`lockState=Expired` are
  **derived** so they won't show in `enrollments."Status"`; the Play gate is **5C** so any drift there is a 5C finding,
  not S3's; videos must be **`Ready`** — transcode needs ffmpeg on PATH).
- **S3 unblocks S4** (assignments, frontend-only — `/api/me/assignments` exists) and **S5** (quizzes). The Session-detail
  **Assignment** and **Quiz** entry cards route into those engines; an enrolled student on a quiz-gated session (videos
  `QuizLocked` until passed) is the precondition S5 assumes.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are running the WIRING stream of Student-Portal phase S3 for Salah Bahzad. Prove the two new reads (GET
/api/me/sessions + /{id}) + the material signed-URL read + the reused Phase-5C video gate live on the running Aspire
stack, and close the My Sessions -> Session detail -> deep-link Play (decrement + VideoPlaybackStarted audit + handoff
code) loop. Zero contract drift.

Read first, in order:
1. docs/IMPLEMENTATION-PLAN-student-s3-wiring.md (this doc — the 10 live checks + the Student-JWT + docker-exec-psql +
   discover-Aspire-containers-by-image techniques).
2. docs/contracts/student-s3-my-sessions-video.md (the FROZEN contract you're proving — §A/§B reads + the MySession*
   shapes, §C material URL, §D the 5C gate body + the five reason codes, §E the progress/expiry/state + per-video
   lockState order + gateState rules).
3. docs/contracts/phase5c-video-gate.md + the prior wiring logs (student-s2-wiring, student-s0-wiring, phase5c-wiring) for
   the Student-role JWT mint, docker-exec-psql (PascalCase quoted columns, singular enrollment_video_access/attendance,
   pipe SQL via stdin), "Aspire reassigns ports & renames containers (resolve by image)", and "stale AppHost 404 ->
   restart" gotchas. phase5c-wiring covers the gate's own redeem/key chain.

Do: F5; confirm GET /api/me/sessions is reachable (else restart for the new routes); get the S2-left Active student
(ST_Amr / Phase3smoke) Student JWT + a staff JWT for fixtures. Run all checks — my-sessions scope (active incl. past-
expiry, exclude Refunded) + shape + DESC + thumbnail signed-URL resolves; progress derived (videosWatched from
AccessRemaining<AccessAllowed, increments after Play); isExpired DERIVED via back-dated ExpiresAtUtc + the ?state= filter;
detail shape + per-video lockState across Playable/Exhausted/QuizLocked/Expired/NotReady matching the 5C gate + gateState;
the 404 IDOR/tenant/refunded boundary on detail + material; material URL 200 active / 200 expired / 404 refunded + not-
audited; the Play gate (POST /playback -> 200 handoff + AccessRemaining decremented + VideoPlaybackStarted/Student audit
-> progress increases); the gate reason ladder (not_ready/not_enrolled/enrollment_expired/quiz_required/no_views_remaining)
matching #4's lockStates; 401 anon / 403 staff / 200 student + tenant isolation + per-caller scoping; and the browser
screens at :4300 (spotlight + filter + detail playlist badges + gate banner + materials download + Play deep-link attempt
+ install prompt, responsive). Log the run, flip the master plan S3 bullet to Met, write the student-s3-wiring memory.
```

---

## Run log — 2026-06-21 (✅ Met · 9/9 scripted checks, zero functional drift)

**Environment.** Stack was down; started the **AppHost via CLI** (`dotnet run` on `SalahBahazad.AppHost`, Debug) — Postgres/MinIO/Redis came back on the **persisted volumes** (the S2-seeded data survived). Drove every endpoint through the **student `:4300` proxy** with a **direct-minted Student JWT** (HS256, secret from `appsettings.Development.json`, `iss=salah-bahzad-api`, `aud=salah-bahzad-admin`, claims `nameid`/`role`/`tenant_id`/`token_type`/`device_id`). DB asserted via `docker exec -e PGPASSWORD=postgres psql -d DefaultConnection` (password is `postgres`; tables snake_case, columns PascalCase-quoted; `enrollment_video_access`/`attendance` singular). **`GET /api/me/sessions` returned 401 (not 404) → the running API already carried the S3 routes** (no restart needed).

**Precondition (verified, persisted).** Tenant `019ed7e6…` (Salah Bahzad). Student **Amr Moataz** `019eea33…` (Active). Enrolled (all Active, DESC): Session 1, Test Large Video HLS, **Phase3smoke** `019ee0ff…` (1 Ready video, **quiz-gated unpassed** MinPass 80, +assignment +2 materials), 5C Wiring 095115 (1 Ready video, **no quiz**, access 1/1). `EnrollmentStatus` = Active0/Expired1/Refunded2; `VideoProcessingStatus` = Pending0/Processing1/Ready2/Failed3.

| # | Check | Result |
|---|---|---|
| 1 | `GET /api/me/sessions` shape + DESC + thumbnail resolves | **200**, 4 rows DESC by EnrolledAtUtc; refunded-others absent; Session 1 `thumbnailUrl` GET → **200, 650 KB** |
| 2 | Progress **derived** from the gate | pre-Play 5C095115 `watched 0/pct 0/NotStarted`; post-Play `watched 1/pct 100/Completed` |
| 3 | `?state=` filters | `Expired→[Phase3smoke]`, `ExpiringSoon→[Session 1, Test Large]`, `Completed→[5C095115]`, `InProgress→[Session 1]` |
| 4 | Detail per-video `lockState` + `gateState` | **Playable** (5C, Open) · **QuizLocked** (Phase3smoke, QuizRequired, minPass 80) · **Exhausted** (5C after Play) · **Expired** (back-dated, gateState Expired, overrides QuizLocked) · **NotReady** (video set Pending) ; assignment reachable while expired |
| 5 | 404 IDOR boundary | unenrolled session **404**; refunded enrollment's session **404**; cross-tenant session **404** |
| 6 | Material signed URL | **200** + URL **resolves (45 B = handout.pdf)** + **not audited** (audit 410→410); foreign materialId **404**; **200 while expired**; **404 when refunded** |
| 7 | Play gate (5C, reused) | **200** `{handoffCode(48), expiresAtUtc}` (no url/manifest/key); DB `AccessRemaining 1→0`; audit `VideoPlaybackStarted \| Student \| SessionVideo \| "Watched: Lesson 1"` |
| 8 | Gate reason ladder | `not_ready` **409** · `not_enrolled` **403** · `enrollment_expired` **403** · `quiz_required` **403** · `no_views_remaining` **403** — each `reason`+`detail`, matching #4's lockStates |
| 9 | Auth + tenant + per-caller | no-bearer **401** · staff(Teacher) **403** · student **200**; cross-tenant list **[] (count 0)** + detail **404**; per-caller: Lean Amr reads **own** 5C095115 counter (Playable 1/1) while Amr's is spent to 0 |
| 10 | Browser walkthrough at `:4300` | **user's step** (as S0 #9 / S1 #7 / S2 #9) — the AppHost is left running for it |

**Fixtures mutated then restored** (left the DB as found): Phase3smoke expiry (back-date→restore), Session 1 video access (InProgress→restore 3), Session 1 video ProcessingStatus (Pending→restore Ready), Phase3smoke Status (Refunded→restore Active), 5C095115 view (Play decrement 0→restore 1). The one **`VideoPlaybackStarted` audit row** from the real Play is **left intact** (append-only / hash-chained — must not be deleted).

**Findings (non-blocking).** (1) Three gate `detail` strings differ by a word from the contract §D.2 quotes — server says *"…no views **remaining**…"*, *"Your **enrollment** for this session has expired."*, *"…still being **processed**."* vs §D.2's *"…left…"* / *"Your access…"* / *"…prepared."*. The **machine `reason` codes are exact**, and the frontend renders `problem.detail` **verbatim**, so there's **zero functional drift** — these are **5C-gate-owned** copy nits (the wiring plan pre-declared "drift in the gate is a 5C finding, not S3's"); fix is to align §D.2's quotes (or the 5C strings) if desired. (2) Backend had **two compile blockers fixed before the run** (a duplicate `SignedUrlResponse` test record introduced by the S3 stream + a pre-existing `Enrollment.Create(studentName)` mismatch in 4 unit-test files from commit `0701ba3`); solution now builds 0-errors, unit tests 196/0.

**Gotchas confirmed:** Aspire **renames containers + reassigns ports each run** (discover Postgres by image; drive via the `:4300` proxy, not the dynamic API port); the persisted **Postgres password is `postgres`** but Aspire would mismatch a fresh password against a persisted volume (it didn't here); `isExpired`/`lockState=Expired` are **derived** (the back-dated enrollment's `"Status"` stayed `Active(0)`); the Play gate is **5C** (its reason strings + redeem/key chain are 5C-owned).

**Unblocks S4** (assignments — the detail's Assignment card routes there; `/api/me/assignments` exists) **and S5** (quizzes — Phase3smoke is an enrolled, quiz-gated-unpassed session with videos `QuizLocked` until passed, the precondition S5 assumes).
