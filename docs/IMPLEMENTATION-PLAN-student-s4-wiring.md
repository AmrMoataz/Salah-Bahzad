# Student Portal · S4 — WIRING stream (prove the assignment runner + answer-key review live)

> Status: **Planned — not yet built** · Created 2026-06-21 · Proves slice **S4**
> (`docs/IMPLEMENTATION-PLAN-student-portal.md` §S4) end-to-end on the **running Aspire stack** (Postgres + Redis +
> MinIO + API + both Angular apps), exactly like the prior wiring streams. Goal: **zero drift** vs
> `docs/contracts/student-s4-assignments.md` — the **reused** Phase-5B-1 engine (`§A`: load → answer → behaviour events)
> proven through the runner's calls, and the **one new read** (`GET /api/me/assignments/{assignmentId}/review`, `§B`)
> proven the **only** student surface that exposes `isCorrect`, gated to the caller's **own `Completed`** assignment,
> tenant-isolated and IDOR-safe.
>
> Runs **after** the backend + frontend streams merge. Reuses the prior wiring techniques: read the Aspire-assigned ports
> from the dashboard (reassigned every run; discover the PG/MinIO containers **by image**, renamed on every restart);
> verify DB state with `docker exec -i <pg> psql` (snake_case tables, **PascalCase quoted columns** — real names
> `user_assignments` / `assignment_questions` / `assignment_options` (owned) / `assessment_events` / `attendance`
> (**singular**) / `audit_entries`; pipe SQL via **stdin** — PS 5.1 mangles inline `-c "…\"col\"…"`); drive the student
> endpoints with a **Student-role JWT** (the reusable direct-JWT mint from S0/S2/S3/phase5b — short claims `nameid`/`role`
> + `tenant_id`/`token_type`/`device_id`, HS256, `iss=salah-bahazad-api`, `aud=salah-bahazad-admin` — or a real S0 sign-in
> via the `:4300` proxy). **The `/api/me/*` routes do NOT check the device.**

---

## Design reference

This stream verifies behaviour, not pixels, but the **acceptance copy** is the **Student Portal** prototype
(`.claude/Salah Bahzad Student Portal/Student Portal.html`, `screen === 'assignment'` — the **`ASSIGNMENT RUNNER`**).
Confirm the running screens at **:4300** match the prototype responsively while driving the browser check: the breadcrumb
**"Assignment"** + title **"{session} — Homework"**; an **accumulated timer counting UP** (`m:ss` via `fmt(sec)`); a
**Progress bar** (variant success) + **"X of Y answered"**; the **question card** ("Question N" green label, body, a
formula chip, MCQ options A/B/C/D, picked = green); the per-question **"Show hint"/"Hide hint"** toggle; **"← Previous"**
(disabled on first) + a primary **"Next question"/"Submit assignment"**; on submit → back to **session detail** (the
runner has **no inline results screen**). The **answer-key review** screen (`§D`) is a **NEW** student screen — the
prototype has none — mirroring the **admin** `AssignmentReviewComponent` question/option treatment (green-check correct
option / red wrong pick) for visual consistency.

## Pre-flight
- Backend + frontend streams merged; `dotnet test -c Release` green (minus the known baseline image test);
  `npx nx build student-portal` green. **No migration** for S4 — confirm the Aspire Postgres already has
  `user_assignments`, `assignment_questions`, `assignment_options` (owned), `assessment_events`, `attendance`,
  `enrollments`, `sessions`, `audit_entries` (all from Phase 5B-1 / earlier phases; **S4 adds no table, no column**).
- Start via **AppHost (F5)**. Read the API port + both web ports from the dashboard. **If
  `GET /api/me/assignments/{id}/review` 404s as a ROUTE (not a 401/403/200 auth result), the running API is stale** —
  restart the AppHost (the recurring 5B-2/5C/S0/S1/S2/S3 gotcha: Aspire won't hot-add new routes). The three engine
  routes (`by-session` / `answer` / `events`) already shipped in 5B-1, so a stale API still serves *them* — only the
  **new `…/review`** route flips 404→200 after the restart.
- **An `Active`, device-bound student whose enrolled session has a gradeable assignment** is the precondition. S3's wiring
  deliberately left **`ST_Amr` Enrolled (Active) in `Phase3smoke`** with video-access counters, an attendance shell, and
  — from the S2 redeem side-effect — an **assignment snapshot**. **Confirm that snapshot has ≥ 1 question** (psql:
  `assignment_questions` rows for its `user_assignments."Id"`). If the assignment is **vacuous (0 questions)** — the
  session's bank was empty — it is **not gradeable**; instead enroll the student into a session whose bank **has
  questions** (mint a code as staff, redeem as the student) so there is a real, completable assignment. Either mint a
  Student-role JWT directly for that student or sign in through the `:4300` proxy. Confirm `students."Status"=1` (Active).
- **A staff JWT** (Teacher) for fixtures — minting codes (to provision a fresh, completable assignment if needed) and for
  the **staff-403** auth check. Reuse the admin wiring's staff principal.
- The auth **rate-limit is one global ~10/min bucket** shared by `/auth/*` + `/register` — it does **not** gate
  `/api/me/*`, but if you mix in sign-ins, space them.

## Fixtures (reuse seeded data where possible)
- **The S3-left `ST_Amr` / `Phase3smoke` Active enrollment** — the happy-path assignment. Confirm its `user_assignments`
  row exists with `Status=InProgress(0)` and **≥ 1** `assignment_questions`. This is the assignment the runner **answers
  through to `Completed`** (checks #1–#2) and then **reviews** (check #4). *(If S3 or an earlier run already completed it,
  use a freshly-enrolled session for the answer-through and review **that** — `Completed` is one-way; you cannot un-grade
  it without a cascade-delete + refund→unlock to regenerate the snapshot.)*
- **A freshly-enrolled, still-`InProgress` assignment** — for the **`403 assignment_in_progress`** review gate (#5). Mint
  a code as staff for a bank-backed session, redeem as the student (the S2 redeem path), then **do not** answer the last
  question; `GET …/review` on it → `403`. Leaving exactly one question unanswered keeps it `InProgress`.
- **An expirable enrollment** — back-date its `enrollments."ExpiresAtUtc"` to the **past** via psql to prove the
  assignment **stays reachable** after expiry (`FR-STU-SES-001`): `GET …/by-session` still `200`, the answer `PUT` still
  works, and `GET …/review` still `200` (expiry locks **videos + the quiz**, never the assignment). Restore it after.
- **A second tenant** with its own enrolled student + completed assignment — for the tenant-isolation check (#6/#9).
- **A second student** (A/B) enrolled in the **same** session with their **own** completed assignment — for the per-caller
  / IDOR scoping (#6): A's review of A's assignment never leaks B's, and A cannot review B's `{assignmentId}` (→ 404).

## Live checks (target: all green, zero drift)

**Engine reuse — Phase 5B-1, proven through the runner's calls (`§A`):**
1. `GET /api/me/assignments/by-session/{Phase3smoke}` (Student JWT) → **`200 StudentAssignmentDto`**: `id`/`sessionId`/
   `status`, `timeSpentSeconds`, and `questions[]` (each `id`/`order`/`bodyLatex`/`imageUrl`/`hintUrl`/`options[]`
   (`id`/`order`/`text`)/`selectedOptionId`). **Assert the raw JSON contains NO `"isCorrect"`** (the 5B-1 invariant +
   its guard test stand — `§0`/`§A`). Re-`GET` after answering some questions → **saved answers + accumulated
   `timeSpentSeconds` resume** across calls (resumable, `FR-STU-ASG-002`). `404` if the caller has no enrollment for the
   session.
2. **Answer-through → auto-grade-on-last (`§A #2`, `§0`):** `PUT …/questions/{aqId}/answer` `{ "selectedOptionId" }` for
   each question in turn → **`AssignmentProgressDto`** `answeredCount` climbs. **Answering the LAST unanswered question
   auto-grades server-side — there is NO separate "submit" call.** **DB (psql):** `user_assignments."Status"` flips to
   **`Completed`**, `"ScoreMarks"`/`"CorrectCount"`/`"CompletedAtUtc"` set; `attendance."AssignmentScore"` written
   (percent) — and the writer is the **`System`** actor; an **`Answered`** `assessment_events` row appended per answer.
   Then **re-read S3** (`GET /api/me/sessions/{Phase3smoke}`, and the session-detail card) → the **`assignment.status`
   now reads `Completed`** (the S3 progress/CTA flip). Answering a question **after** `Completed` → **`409`**.
3. **Behaviour events + accumulated time (`§A #3`, `§C`):** `POST …/events` with
   `{ type: "Entered"|"Navigated"|"Left", questionOrder?, occurredAtUtc, elapsedMs? }` → **`204`**. **DB (psql):**
   `assessment_events` gains the `Entered`/`Navigated`/`Left` rows **and** `user_assignments."TimeSpentSeconds"` **accrues**
   the `elapsedMs` (time is server-authoritative; the runner's timer resumes from it). Confirm **`"Answered"` is rejected
   as an event `type`** here (it is logged by the answer `PUT`, never posted — `§A`/`§C`).

**The new review read (`§B`):**
4. `GET /api/me/assignments/{assignmentId}/review` (Student JWT) on the **Completed** assignment → **`200
   StudentAssignmentReviewDto`**: `id`/`sessionId`/`sessionTitle`/`status: "Completed"`, the **score**
   (`correctCount`/`questionCount`/`scoreMarks`/`maxMarks`/`percent`), `timeSpentSeconds`/`completedAtUtc`, and
   `questions[]` each with `order`/`bodyLatex`/`imageUrl`/`mark`/`hintUrl`/`options[]`(with **per-option `isCorrect`**)/
   `selectedOptionId`/**per-question `isCorrect`**. **Assert:** `selectedOptionId` echoes what #2 answered; the
   per-option/per-question `isCorrect` match the bank's correct options; and **`percent == round(100 × scoreMarks /
   maxMarks)`** (0 when `maxMarks == 0`). Questions ordered by `Order` asc, options by `Order` asc. This is a **distinct
   DTO** (`StudentAssignmentReviewDto`/`StudentReviewQuestionDto`/`StudentReviewOptionDto`) — **not** a widened runner
   `StudentAssignmentDto`.
5. **`403 assignment_in_progress` gate (`§B.2`):** `GET …/review` on the **still-`InProgress`** fixture (the freshly
   enrolled session, before answering the last question) → **`403` ProblemDetails** with machine `reason ==
   "assignment_in_progress"` + readable `detail` ("Finish the assignment to see your answers and score.") — the key is
   **never** revealed pre-completion.
6. **`404` IDOR / tenant / unknown (`§B.2`):** `GET …/review` for **another student's** `{assignmentId}`, a **cross-tenant**
   `{assignmentId}`, and an **unknown** id → **`404`** each (opaque — never the other student's data, never reveal
   existence). Confirm via psql that the foreign assignment **does** exist (so 404 is the ownership/tenant boundary, not a
   missing row).

**Auth + the isCorrect split + not-audited:**
7. **Auth (`§B.2`):** anonymous (no bearer) → **`401`**; a **staff** (Teacher) JWT → **`403`** (the `RequireStudent`
   filter); the Student JWT → **`200`** — on the review read.
8. **The `isCorrect` SPLIT, side by side (`§0`/`§A`/`§B`):** for the **same** student + **same** completed assignment, the
   **by-session `GET` raw JSON has NO `isCorrect`** (#1) while the **review `GET` DOES** (#4) — the runner shape stays
   correctness-free; the review is the deliberate, gated exception (the 5B-1 "never exposes isCorrect" runner invariant +
   its guard test are **unchanged**).
9. **Not audited (`§E`):** snapshot `audit_entries` count **before/after** the review `GET` → **NO new row** (and the
   by-session `GET` likewise — pure reads of the caller's own homework, parity with `/api/me/catalogue` +
   `/api/me/sessions`). **Tenant isolation (`NFR-SEC-010`) + per-caller (`NFR-SEC-007`):** student B's review of **B's
   own** assignment never leaks A's; **A cannot review B's** (→ 404, #6).

**The screens, live in the browser (`FR-STU-RWD-001/002`, `FR-STU-A11Y-001`):**
10. Open the student app at **:4300**, sign in, open a session → **"Continue assignment"** → the **runner**: answer
    **one question at a time** with the **accumulated timer** ticking up, the **progress bar** + **"X of Y answered"**,
    and the per-question **hint** toggle; **"← Previous"** disabled on the first question; the last question's primary
    reads **"Submit assignment"** and its click answers the last question (auto-grade) → navigate **back to session
    detail** (no inline results), where the assignment card now reads **"Review assignment"**; open the **review** → the
    **answer key** renders (correct option green-checked / your wrong pick red, marks + score + time). Resize: runner +
    review reflow to phone, comfortable targets, matches the prototype across phone/tablet/desktop. *(The visual
    walkthrough is the user's step, as with S0 #9 / S1 #7 / S2 #9 / S3 #10.)*

## Sign-off
- Log the run (counts + the `user_assignments` `Status`/`ScoreMarks`/`CorrectCount`/`CompletedAtUtc`/`TimeSpentSeconds`
  before/after + the `attendance."AssignmentScore"` write (System actor) + the `assessment_events`
  `Entered`/`Navigated`/`Left`/`Answered` rows + the review score/`isCorrect`/`percent` assertion + the **audit no-op**
  before/after the review GET) into this file like the prior wiring logs. Update the master plan's **S4** line from
  *Planned* → **Met** with the date + headline result. Record a memory entry (`student-s4-wiring`). Note any gotchas
  (expect: **stale-API-needs-restart** for the **new `…/review`** route — the three engine routes pre-exist so they won't
  404; Aspire **renames containers + reassigns ports each run** — discover Postgres/MinIO by image, drive via the `:4300`
  proxy not the dynamic API port; the **runner engine is 5B-1** — any drift in load/answer/events is a **5B-1** finding,
  not S4's; the **assignment stays reachable when the enrollment is expired** — only videos + the quiz lock; `Completed`
  is **one-way** — un-grading needs a cascade-delete + refund→unlock to regenerate the snapshot).
- **S4 unblocks S5** (the proctored quiz runner + results + the `QuizHub` — the **same** `feature-assessment` lib; the S3
  session-detail **Quiz** card stays a placeholder until then) **and S6** (profile). The completed assignment + its review
  is the engagement S5 builds alongside; an enrolled student on a quiz-gated session (videos `QuizLocked` until passed) is
  the precondition S5 assumes.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are running the WIRING stream of Student-Portal phase S4 for Salah Bahzad. Prove the assignment slice live on the
running Aspire stack: the REUSED Phase-5B-1 engine (GET /api/me/assignments/by-session/{sessionId} -> answer each question
via PUT .../questions/{aqId}/answer -> POST .../events) driven through the runner, the auto-grade-on-last (Status
Completed + Attendance.AssignmentScore by System + the S3 progress flip), and the ONE new read GET
/api/me/assignments/{assignmentId}/review — the only student surface that exposes isCorrect, gated to the caller's own
Completed assignment. Zero contract drift.

Read first, in order:
1. docs/IMPLEMENTATION-PLAN-student-s4-wiring.md (this doc — the 10 live checks + the Student-JWT + docker-exec-psql +
   discover-Aspire-containers-by-image techniques).
2. docs/contracts/student-s4-assignments.md (the FROZEN contract you're proving — §A the reused engine + StudentAssignmentDto
   (no isCorrect), §B the new GET …/review + StudentAssignmentReviewDto + the 403 assignment_in_progress / 404 IDOR boundary,
   §C the runner interaction rules, §D the review-screen semantics, §E reads-not-audited).
3. docs/contracts/phase5b1-assignments-attendance.md §A/§C (the engine + the staff AssignmentReviewDto the student review
   mirrors) + the prior wiring logs (student-s3-wiring, student-s2-wiring, student-s0-wiring) for the Student-role JWT mint,
   docker-exec-psql (PascalCase quoted columns, singular attendance, pipe SQL via stdin), "Aspire reassigns ports & renames
   containers (resolve by image)", and "stale AppHost 404 -> restart for the NEW route only" gotchas.

Do: F5; confirm GET /api/me/assignments/{id}/review is reachable (else restart for the new route — the three engine routes
pre-exist from 5B-1); get the S3-left Active student (ST_Amr / Phase3smoke) Student JWT + a staff JWT for fixtures; confirm
the assignment snapshot has >=1 question (else mint a code as staff + redeem as the student for a bank-backed session). Run
all checks — engine load (200, NO isCorrect in raw JSON, saved answers + timeSpentSeconds resume); answer-through ->
answeredCount climbs -> last answer auto-grades (psql: user_assignments Status Completed/ScoreMarks/CorrectCount/
CompletedAtUtc + attendance.AssignmentScore System + an Answered assessment_events row + the S3 GET /api/me/sessions/{id}
assignment.status now Completed); POST /events Entered/Navigated/Left -> 204 + assessment_events rows + TimeSpentSeconds
accrued (Answered rejected as a type); the new review GET on the Completed assignment -> 200 StudentAssignmentReviewDto
(per-option AND per-question isCorrect, selectedOptionId echoed, percent == round(100*scoreMarks/maxMarks)); 403
assignment_in_progress on a still-InProgress assignment; 404 IDOR/cross-tenant/unknown assignmentId; 401 anon / 403 staff /
200 student; the isCorrect SPLIT side-by-side (by-session has none, review does, same student+assignment); not-audited
(audit_entries before==after for the review GET, and the by-session GET); tenant isolation + per-caller scoping; and the
browser screens at :4300 (runner one-at-a-time with timer + progress + hint + Submit -> back to detail -> Review assignment
-> answer key renders, responsive). Log the run, flip the master plan S4 bullet to Met, write the student-s4-wiring memory.
```

---

## Run log — 2026-06-22 (✅ Met · 9/9 scripted checks green, zero drift · browser walkthrough #10 = user's step)

**Environment.** The AppHost was already running (and **recycled mid-run** — Aspire renamed containers + reassigned ports, so
Postgres/MinIO were discovered **dynamically by image** `postgres:17.4` / `minio/minio:latest`, not by name). Drove every
endpoint through the student **`:4300` proxy** with a **direct-minted Student JWT** (HS256, secret from
`appsettings.Development.json`, `iss=salah-bahzad-api`, `aud=salah-bahzad-admin`, claims `nameid`/`role`/`tenant_id`/
`token_type`/`device_id` — `JwtSecurityTokenHandler`'s outbound map emits the short `nameid`/`role`). Mint validated live
(student→**200** on `/me`, staff→**403**, anon→**401**). The new `…/review` route returned **401** (not 404) on a no-bearer
probe → the running API **already carried the S4 route** (no restart needed; the three engine routes pre-exist from 5B-1).
DB asserted via `docker exec -i <pg> psql -d DefaultConnection` (`PGPASSWORD=postgres`; snake_case tables, PascalCase quoted
cols; the owned options table is **`assignment_question_options`**, `attendance` is **singular**).

**Fixtures — seeded fresh via the real staff→bank→publish→code→redeem flow** (the user asked for *"good assignments + quizzes
with good LaTeX to see the LaTeX view"*):
- Published, quiz-gated session **"S4 LaTeX Demo - Algebra and Calculus"** (`019eec27`, Math·Algebra, quiz 5/6 pass 60) with
  **6 rich-LaTeX MCQs** (quadratic roots `$x^2-5x+6=0$` [+YouTube hint, mark 2], `$\int_0^1 x^2\,dx$`, `$\frac{d}{dx}\sin x$`,
  `$\sum_{k=1}^{n}k$`, `$\lim_{x\to0}\frac{\sin x}{x}$`, Pythagoras; `maxMarks 7`, all quiz-eligible). Redeemed by **Amr Moataz**
  (`019eea33`, Active) → assignment `019eec2d`, then **answered through (Q1–5 correct, Q6 wrong)** → **Completed 6/7** = the
  **review** fixture.
- Second published session **"S4 LaTeX Runner Demo"** (`019eec32`, same bank) redeemed by Amr → assignment `019eec32-110e…`
  **left InProgress** = the **runner** fixture (so the user can drive the one-at-a-time runner + hint + timer in the browser).
- Other students' assignments (Student Test `019ee589…`) = the **IDOR** fixture.

| # | Check | Result |
|---|---|---|
| 1 | engine load `by-session` (no isCorrect) | **200** InProgress, 6 q, `timeSpent 0`, `selected null`; **raw JSON has NO `isCorrect`** (5B-1 invariant holds); Q1 `hintUrl` present |
| 2 | answer-through → **auto-grade-on-last** | `answeredCount` 1→6; the **6th answer flips Status→Completed** (no separate submit); DB `Status=Completed ScoreMarks=6 CorrectCount=5 Max=7 CompletedAtUtc` set; `attendance.AssignmentScore=86`; audit **`System \| AssignmentGraded`**; **S3 `GET /me/sessions/{id}` `assignment.status=Completed` 6/7**; re-answer → **409** |
| 3 | behaviour events + accrued time | `Entered`/`Navigated`/`Left` → **204**; `Answered` type → **400** (rejected, logged by the answer PUT); `assessment_events` rows; `TimeSpentSeconds` accrued **0→23** (15+8 ms deltas) — server-authoritative |
| 4 | **new review** (Completed) | **200** `StudentAssignmentReviewDto`: status Completed, correct **5/6**, marks **6/7**, **`percent 86 = round(100·6/7)`**, time 23s, `sessionTitle` set; **every option + question carries `isCorrect`**; **all 6 `selectedOptionId` echo our answers**; Q1–5 `isCorrect=true`, **Q6 `isCorrect=false`** (selected≠correct); questions ordered by `order` |
| 5 | review on **InProgress** | **403** `reason=assignment_in_progress`, detail *"Finish the assignment to see your answers and score."* (key never revealed pre-completion) |
| 6 | **IDOR** / unknown | Amr → Student-Test's assignment **404** (the foreign row **exists** → it's the ownership boundary, not a missing row); unknown id **404** |
| 7 | auth on review | anon **401** · staff (Teacher) **403** · student **200** |
| 8 | **isCorrect split** (same student + assignment) | `by-session` raw **NO** `isCorrect` / `review` raw **YES** — the runner shape stays correctness-free; the review is the gated exception |
| 9 | review **not audited** | `audit_entries` **498 → 498** across the review GET + the by-session GET (no new rows; parity with `/me/catalogue` + `/me/sessions`) |
| (tenant) | cross-tenant isolation | one live tenant this run; **per-caller IDOR proven live (#6)** is the same ownership 404 path; cross-tenant is covered by the integration test **`MyAssignmentReviewApiTests.Review_is_404_across_tenants`** (`NFR-SEC-010`) |
| 10 | browser walkthrough at `:4300` | **user's step** (app serves `200`) — two LaTeX sessions are left enrolled for Amr: **"S4 LaTeX Runner Demo"** (InProgress) to drive the runner + hint + timer + Submit, and **"S4 LaTeX Demo - Algebra and Calculus"** (Completed) to open the **answer-key review** |

**Drift: none.** The new review read matches the frozen contract **§B** field-for-field; the reused 5B-1 engine (load/answer/
events/auto-grade) behaves per **§A**; the `403 assignment_in_progress` + `404` IDOR + Completed gate match **§B.2**; reads are
not audited per **§E**; auto-grade is attributed to **`System`** (§0). The runner engine itself is **5B-1's** — any load/answer/
events behaviour is a 5B-1 concern, none observed.

**Fixtures left in place (intentionally, for the browser step):** Amr's two `S4 LaTeX *` enrollments (one Completed, one
InProgress) + the single `AssignmentGraded`/`System` audit row (append-only, hash-chained — must not be deleted). The
earlier accidental empty-draft enrollment was **refunded** (needs a `{reason}` body) then re-redeemed (EnrollOrExtend reused
the row).

**Gotchas confirmed:** (1) Aspire **recycled the stack mid-run** — discover PG/MinIO **by image**, not by the renamed
container. (2) The new `…/review` route was **already live** (401, not 404) — no restart. (3) The shell/tool layer
**collapses `\\`→`\`**, so LaTeX JSON bodies 400 on bind unless POSTed **from files** (`curl --data-binary @q*.json`); an
**em-dash** in a title also breaks JSON binding (use ASCII titles). (4) `refund` requires a `{"reason":…}` body. (5) The
**runner engine is 5B-1** (load/answer/events) — reused unchanged; S4 added only the review read.

**Unblocks S5** (proctored quiz runner + results + `QuizHub` — the same `feature-assessment` lib; session A is quiz-gated, a
ready precondition) **and S6** (profile).
