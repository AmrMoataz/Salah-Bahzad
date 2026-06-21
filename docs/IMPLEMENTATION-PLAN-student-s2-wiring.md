# Student Portal · S2 — WIRING stream (prove catalogue + enroll-by-code live)

> Status: **Planned — not yet built** · Created 2026-06-21 · Proves slice **S2**
> (`docs/IMPLEMENTATION-PLAN-student-portal.md` §S2) end-to-end on the **running Aspire stack** (Postgres + Redis +
> MinIO + API + both Angular apps), exactly like the prior wiring streams. Goal: **zero drift** vs
> `docs/contracts/student-s2-catalogue-enroll.md` — the **new `GET /api/me/catalogue`** read proven published-only,
> tenant-isolated, and per-caller correct (enrollment state + prerequisite flag); and the **redeem → consumed code →
> enrollment + side-effects → catalogue card flips to `Enrolled`** loop closed through the **reused** Phase-4 engine.
>
> Runs **after** the backend + frontend streams merge. Reuses the prior wiring techniques: read the Aspire-assigned
> ports from the dashboard (reassigned every run; discover the PG/MinIO containers **by image**, they're renamed on
> every restart); verify DB state with `docker exec -i <pg> psql` (snake_case tables, **PascalCase quoted columns**; pipe
> SQL via **stdin** — PS 5.1 mangles inline `-c "…\"col\"…"`); drive the student endpoints with a **Student-role JWT**
> (the reusable direct-JWT smoke technique from S0/phase5b — or a real S0 sign-in via the `:4300` proxy).

---

## Design reference

This stream verifies behaviour, not pixels, but the **acceptance copy** is the **Student Portal** prototype
(`CATALOGUE` cards + the `Enroll modal`). Confirm the running catalogue at **:4300** matches the prototype responsively
while driving check #9 (cards grid, specialization filter, the segmented code modal, the card flipping to **Open** after
a redeem).

## Pre-flight
- Backend + frontend streams merged; `dotnet test -c Release` green (minus the known baseline image test);
  `npx nx build student-portal` green. **No migration** for S2 — confirm the Aspire Postgres has `sessions`,
  `enrollments`, `enrollment_video_accesses`, `codes`, `questions`, `user_assignments`, `attendances`, `audit_entries`
  (all from earlier phases).
- Start via **AppHost (F5)**. Read the API port + both web ports from the dashboard. **If `GET /api/me/catalogue` 404s
  (not 401/403/200), the running API is stale** — restart the AppHost (the recurring 5B-2/5C/S0/S1 gotcha: Aspire won't
  hot-add new routes).
- **A signed-in `Active`, device-bound student** is the precondition (S0/S1 deliver it). Either mint a Student-role JWT
  directly (the S0/phase5b technique — sign the same claims the API issues: `role=Student`, `userId`/`tenant_id`/
  `device_id`) or run a real S0 sign-in through the `:4300` proxy and grab the access token. Confirm the student is
  **`Active`** (`students."Status"=1`) in a tenant that has **published** sessions.
- **A staff JWT** (Teacher) for the **fixtures** — minting a code (`POST /api/codes/batches`, `CodesGenerate`) and, if
  needed, publishing a session / setting a prerequisite. Reuse the admin wiring's staff principal.
- The auth **rate-limit is one global ~10/min bucket** shared by `/auth/*` + `/register` (the S0/S1 finding) — it does
  **not** gate `/api/me/*` or `/api/enrollments/redeem`, but if you mix in sign-ins, space them.

## Fixtures (reuse seeded data where possible)
- **≥ 2 published sessions** in the student's tenant with **different specializations** (for the filter check) — reuse
  the Phase-3/4 seeded/published sessions, or publish via the staff API. At least one with a **thumbnail** (for the
  signed-URL check) and one **Free** (`price = 0`).
- A **`Draft`** and an **`Archived`** session in the same tenant — to prove they're **excluded**.
- A session with a **prerequisite that has questions** (for the `prerequisiteSatisfied=false` check) + the ability to
  **complete** that prerequisite's assignment for the student (drive the S4 assignment engine, or set a
  `user_assignments` row `Status=Completed` in psql) to flip it true.
- A **code** minted for one published session at its **current price** (`POST /api/codes/batches` → grab a `serial` from
  the batch export / psql `codes`), for the redeem happy path. A **disabled** code + a code whose session price was
  **changed after mint** (for the 409s).
- **A second tenant** with its own published session — for the isolation check #5.
- **A second student** (A/B) — for the per-caller IDOR check #4.

## Live checks (target: all green, zero drift)

**Catalogue read (`§A`, the only new route):**
1. `GET /api/me/catalogue` (Student JWT, no params) → **`200`** a flat `[CatalogueSessionDto]` of **only `Published`**
   sessions (the `Draft` + `Archived` fixtures **absent**), **DESC by `CreatedAtUtc`**, each with grade/subject/spec
   names, `videoCount`, `price` (and `0` for the Free one), `validityDays`, and a **non-null `thumbnailUrl`** for the
   thumbnailed session that **resolves** (GET the signed URL → 200 image) — null for one without. Empty tenant → `[]`.
2. **Filters (`§A.1`):** `?specializationId=<one>` returns only that spec's sessions; `?gradeId=`, `?subjectId=` (via
   specialization), and `?search=<title substr>` each narrow; a no-match filter → `[]` (200).
3. **`enrollmentState` (`§C.1`) — the key projection:** a not-enrolled session reads **`NotEnrolled`**. After check #7's
   redeem, the **same** session reads **`Enrolled`** with `enrolledExpiresAtUtc` set (or `null` for a `validityDays=0`
   session). In psql, set one of the student's active enrollments' `"ExpiresAtUtc"` to the **past** → catalogue reads
   **`Expired`** (proves it's **derived**, not a `"Status"` read). Refund one (staff `POST /api/enrollments/{id}/refund`)
   → catalogue reads **`Refunded`**.
4. **`prerequisiteSatisfied` (`§C.2`):** the prereq-with-questions session reads **`false`** with `prerequisiteTitle`
   populated **before** the student completes the prerequisite's assignment; after completing it (S4 engine or psql
   `user_assignments.Status=Completed`) the **same** session reads **`true`**. A no-prereq session reads `true`. *(This
   is the FR-STU-CAT-002 gate the card disables Enroll on — confirm it matches the redeem 409 in #8.)*
5. **Tenant isolation (`NFR-SEC-010`):** the tenant-A student's catalogue contains **only** tenant-A published sessions,
   never tenant B's (confirm both seeded in psql; assert the returned id-set ⊆ A's).
6. **Per-caller IDOR (`NFR-SEC-007`) + auth gating:** students A and B enrolled in the **same** session with different
   states each see **their own** `enrollmentState`. Anonymous (no bearer) → **`401`**; a **staff** JWT → **`403`**; the
   Student JWT → **`200`**.

**Redeem loop (reused Phase-4 engine, proven through the catalogue) (`§B`):**
7. Mint a code (staff) for a not-yet-enrolled published session at its current price → `POST /api/enrollments/redeem`
   (Student JWT) `{ "serial": "<serial>" }` → **`201`** `EnrollmentDto` (`status:"Active"`, `method:"Code"`,
   `amount==price`, `expiresAtUtc` per `validityDays`), `Location: /api/enrollments/{id}`.
   **DB (psql):** the **code** flips `"Status"` → **`2` (Used)** with `RedeemedByStudentId`/`RedeemedAtUtc` set; one
   **`enrollments`** row (`"Status"=0` Active, `Method=0` Code, `Amount==price`); **`enrollment_video_accesses`** rows
   provisioned per session video (`FR-PLAT-ENR-005`); a **`payment_transactions`** Completed row; an **`attendances`**
   shell; a **`user_assignments`** (+ `user_quizzes` if the session has a gating quiz) snapshot from the
   `EnrollmentCreated` handler; an **`audit_entries`** row `Action=CodeRedeemed`, **`ActorType=Student`**,
   `Portal=student`. Then **re-run check #3** → the session's catalogue card now reads **`Enrolled`**.

**Redeem error modes (`§B.3`) — each a specific message (`FR-STU-CAT-005`):**
8. With the Student JWT: a **bogus serial** → **`409`** "This code is invalid or no longer available."; a **disabled**
   code → `409` "This code is not available for redemption."; a code whose **session price changed after mint** → `409`
   "This code's value no longer matches the session price."; **re-redeem** a code for a session the student is **already
   actively enrolled** in → `409` "This student already has an active enrollment for this session."; a code for a session
   whose **prerequisite the student hasn't completed** → `409` "Complete the prerequisite assignment first." (and confirm
   #4 showed that same session as `prerequisiteSatisfied:false`); an **empty/over-long serial** → **`400`**.

**The catalogue, live in the browser (`FR-STU-RWD-001/002`, `FR-STU-A11Y-001`):**
9. Open the student app at **:4300**, sign in, open **Catalogue** → the **cards grid** renders (price/prereq badge/CTA);
   the **specialization filter** narrows client-side; **Enroll** (from a card **and** the **Redeem FAB**) opens the
   **segmented code modal**, **paste** distributes a serial, a good code → success + the card flips to **Open**, a bad
   code → the server's specific message inline; a **prerequisite-unmet** card shows a **disabled Enroll** + the hint.
   Resize: cards reflow to a single column on phone, comfortable targets, matches the prototype across phone/tablet/
   desktop. *(The visual walkthrough is the user's step, as with S0 #9 / S1 #7.)*

## Sign-off
- Log the run (counts + the `sessions`/`enrollments`/`enrollment_video_accesses`/`codes`/`audit_entries` rows + the
  catalogue states before/after redeem + the error ladder) into this file like the prior wiring logs. Update the master
  plan's **S2** line from *Planned* → **Met** with the date + headline result. Record a memory entry
  (`student-s2-wiring`). Note any gotchas (expect: Aspire port/name reassignment + discover-by-image; stale-API-needs-
  restart for the new route; `enrollmentState=Expired` is **derived** so it won't show in `"Status"`; the redeem engine
  is Phase-4 so any "drift" there is a Phase-4 finding, not S2's).
- **S2 unblocks S3** (My Sessions, session detail & secure video) — an enrolled student with provisioned video access +
  an attendance shell is the precondition S3's `/api/me/sessions` reads assume.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are running the WIRING stream of Student-Portal phase S2 for Salah Bahzad. Prove the new catalogue read + the reused
redeem engine live on the running Aspire stack, and close the redeem -> consumed code -> enrollment + side-effects ->
catalogue card flips to Enrolled loop. Zero contract drift.

Read first, in order:
1. docs/IMPLEMENTATION-PLAN-student-s2-wiring.md (this doc — the 9 live checks + the Student-JWT + docker-exec-psql +
   discover-Aspire-containers-by-image techniques).
2. docs/contracts/student-s2-catalogue-enroll.md (the FROZEN contract you're proving — §A catalogue shape, §C the
   enrollmentState/prerequisiteSatisfied rules, §B redeem body + the six 409 detail strings + 400, §D audit).
3. The prior wiring logs (student-s1-wiring, student-s0-wiring, phase4-wiring) for the Student-role JWT, docker-exec-psql
   (PascalCase quoted columns, pipe SQL via stdin), "Aspire reassigns ports & renames containers (resolve by image)",
   and "stale AppHost 404 -> restart" gotchas. phase4-wiring covers the redeem engine's own checks.

Do: F5; confirm GET /api/me/catalogue is reachable (else restart for the new route); get an Active student's Student JWT
+ a staff JWT for fixtures. Run all checks — catalogue published-only + shape + thumbnail signed-URL + DESC order; filters
narrow; enrollmentState NotEnrolled/Enrolled(+expiry)/Expired(DERIVED via past ExpiresAtUtc)/Refunded; prerequisiteSatisfied
false->true around completing the prereq assignment; tenant isolation; per-caller IDOR + 401 anon/403 staff/200 student;
the redeem happy path (mint code -> POST /redeem -> 201 + code Used + enrollment + video-access counters + payment +
attendance + assignment/quiz snapshot + CodeRedeemed/Student/student audit -> catalogue card now Enrolled); the redeem
error ladder (409 invalid/disabled/price-mismatch/already-enrolled/prereq-unmet + 400); and the browser catalogue at
:4300 (grid, spec filter, segmented+paste enroll modal from a card AND the Redeem FAB, card flip, disabled-on-unmet-prereq,
responsive). Log the run, flip the master plan S2 bullet to Met, write the student-s2-wiring memory.
```
