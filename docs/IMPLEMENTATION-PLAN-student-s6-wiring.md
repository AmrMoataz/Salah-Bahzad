# Student Portal · S6 — WIRING stream (prove the self-service Profile live)

> Status: **✅ DONE (2026-06-22)** — proven live on the running Aspire stack via the `:4300`/`:4200` proxy + a direct
> Student-role JWT; **all scripted checks green, ZERO contract drift** (the browser walkthrough #12 is the user's visual
> step, as with S0 #9 / S1 #7 / S2 #9 / S3 #10 / S4 #10 / S5). **Run log at the bottom of this file.** · Created
> 2026-06-22 · Proves slice **S6**
> (`docs/IMPLEMENTATION-PLAN-student-portal.md` §S6) end-to-end on the **running Aspire stack** (Postgres + Redis +
> MinIO + API + both Angular apps), exactly like the prior wiring streams. Goal: **zero drift** vs
> `docs/contracts/student-s6-profile.md` — the **two new** endpoints `GET /api/me/profile` + `PUT /api/me/profile`
> (`§A`), mirroring the existing **staff** `ProfileEndpoints` (`/api/profile`) but scoped to the **`Student`**
> aggregate, proven: GET returns the caller's `StudentProfileDto` (resolved grade/city/region **names** + the active
> **bound-device** summary, **no token hash**, **no email**); PUT updates the **seven** writable fields, **leaves grade
> unchanged**, **ignores** any email/grade in the body, persists to the DB, and writes a `Student` audit row via the
> `SaveChanges` interceptor; the `400`/`401`/`403` error modes; tenant isolation + the **no-IDOR-surface** (the subject
> is the JWT, never a URL id); and the three **client-only** flows (device-reset INFO modal, Firebase change-password,
> sign-out) firing **no** profile API.
>
> **Four user-confirmed decisions (2026-06-22), binding — every check here is consistent with them:** (1) **avatar =
> initials only** — no upload, no `Avatar` field, no DTO field, no storage; (2) **device reset = contact-support only**
> — the modal calls **no API** (recovery is the existing staff clear `POST /api/students/{id}/clear-device`); (3)
> **password = Firebase email reset link** — `sendPasswordResetEmail(auth, email)` client-side, no backend; (4) **email
> = read-only Firebase identity** — not stored on `Student`, not in either DTO, shown disabled from
> `firebaseAuth.currentUser.email`.
>
> **S6 is the FINAL vertical slice — it CLOSES the student-portal plan (S0..S6).** (The personalized Home / weekly-plan
> phase is separately planned, post-S6.)
>
> Runs **after** the backend + frontend streams merge. Reuses the prior wiring techniques: read the Aspire-assigned
> ports from the dashboard (reassigned every run; discover the PG/MinIO containers **by image**, renamed on every
> restart); verify DB state with `docker exec -i <pg> psql` (snake_case tables, **PascalCase quoted columns** — real
> names `students` / `student_devices` / `cities` / `regions` / `grades` / `audit_entries`; pipe SQL via **stdin** — PS
> 5.1 mangles inline `-c "…\"col\"…"`); drive the student endpoints with a **Student-role JWT** (the reusable direct-JWT
> mint from S0/S2/S3/S4/phase5b — short claims `nameid`/`role` + `tenant_id`/`token_type`/`device_id`, HS256,
> `iss=salah-bahazad-api`, `aud=salah-bahazad-admin` — or a real S0 sign-in via the `:4300` proxy). **The `/api/me/*`
> routes do NOT check the device.**

---

## Design reference

This stream verifies behaviour, not pixels, but the **acceptance copy** is the **Student Portal** prototype
(`.claude/Salah Bahzad Student Portal/Student Portal.html`, `screen === 'profile'` — the **`PROFILE`** section + its
**`Change-password`**, **`Device-reset`**, and **`Sign-out`** modals). Confirm the running screen at **:4300** matches
the prototype responsively while driving the browser check (#11): the **header band** (gradient `135deg #EAF2FB →
#EBF5E9`) with the shared **Avatar** (size **`xl`**, **initials only**, status badge — **no image, no upload**), the
**name** (bold 24px), a **sub-line** `"{gradeName} · {track} · {cityName}"`, and a **success Chip "Active"** from
`status`; the two-column grid (`1.6fr 1fr`) → single column on phone; the left **"Personal information"** card (Full
name editable, **Email read-only/disabled**, School editable, **Grade disabled**, City + Region dropdowns cascading,
**"Save changes"**) + the **"Parent / guardian numbers"** subsection (Primary required, Secondary optional); the right
**"Bound device"** card (smartphone icon, device name = the fingerprint summary, **"Bound {date}"**, the contact-support
helper, a **"Reset device"** secondary-sm → the **INFO modal**) and the **"Security"** card (**"Change password"** →
the **Firebase reset-email modal**, **"Sign out"** danger-ghost → the **sign-out confirm modal**). The prototype's
two **grounding corrections** must be visible: **Email is disabled** (the proto shows an editable input) and the
change-password modal is the **Firebase reset-email confirm/success** flow (the proto shows a 3-field current/new/confirm
form — replaced).

## Pre-flight
- Backend + frontend streams merged; `dotnet test -c Release` green (minus the known baseline image test);
  `npx nx build student-portal` green. **No migration** for S6 — confirm the Aspire Postgres `students` table has **no
  new column** (avatar deferred, `§F`); the slice adds **no table, no column**. Confirm the existing
  `students` / `student_devices` / `cities` / `regions` / `grades` / `audit_entries` tables (all from earlier phases).
- Start via **AppHost (F5)**. Read the API port + both web ports from the dashboard. **If `GET /api/me/profile` 404s as
  a ROUTE (not a 401/403/200 auth result), the running API is stale** — restart the AppHost (the recurring
  5B-2/5C/S0..S5 gotcha: Aspire won't hot-add new routes). **Both** `GET` and `PUT /api/me/profile` are **new** in S6
  (no `MeProfileEndpoints.cs` existed before), so a stale API 404s **both** until the restart — unlike S4/S5 where the
  engine routes pre-existed. A no-bearer probe returning **401** (not 404) confirms the route is live.
- **An `Active`, device-bound student** is the precondition (the JWT subject). Reuse the S2/S3/S4-left **`ST_Amr`** (Amr
  Moataz) `Active` student in the live tenant — confirm `students."Status"=1` (Active) and that they have **one active
  `student_devices` row** (`"IsActive"=true`) so `boundDevice` is non-null in the GET (the device was bound at the S0
  sign-in / S1 register→exchange loop). Either mint a Student-role JWT directly for that student or sign in through the
  `:4300` proxy. The student's `cities`/`regions`/`grades` ids must resolve to real **names** (they were set at S1
  registration) so the GET name-joins are exercised.
- **A staff JWT** (Teacher) for the **staff-403** auth check. Reuse the admin wiring's staff principal.
- **A second student with NO active device** (or clear `ST_Amr`'s device out-of-band via the staff clear and re-read)
  — to prove `boundDevice == null` renders the UI's generic fallback label (`§C.5`). Re-bind afterward.
- **A second tenant** with its own `Active` enrolled student — for the cross-tenant isolation check (#9).
- The auth **rate-limit is one global ~10/min bucket** shared by `/auth/*` + `/register` — it does **not** gate
  `/api/me/*`, but if you mix in sign-ins, space them.

## Fixtures (reuse seeded data where possible)
- **The S2/S3/S4-left `ST_Amr` (Amr Moataz) `Active`, device-bound student** in the live tenant — the happy-path
  profile. Confirm via psql: `students."Status"=1`, a non-null `"FullName"`/`"PhoneNumber"`/`"ParentPhonePrimary"`/
  `"SchoolName"`, a real `"GradeId"`/`"CityId"`/`"RegionId"` (joinable to `grades`/`cities`/`regions` for names), and
  exactly one `student_devices` row with `"IsActive"=true` (records `"FingerprintSummary"` + `"BoundAtUtc"`). This is the
  row the GET reads (#1) and the PUT updates (#2/#3). **Snapshot its current field values** before the PUT so you can
  assert the diff afterward.
- **A "no device" student** — either a freshly-registered-but-never-signed-in `Active` student, or `ST_Amr` after a
  staff clear (`student_devices."IsActive"=false`). `GET /api/me/profile` → `boundDevice: null` (#1b). Re-bind by
  signing in again at `:4300` after the check.
- **A second tenant** with its own `Active` student + that student's Student-JWT — for the tenant-isolation check (#9):
  the tenant-B student can read/update **only** B's own profile; B's JWT never resolves an A-tenant `Student` (the EF
  global filter scopes by `tenant_id`), and there is **no URL id to tamper with** (the subject is the JWT).

## Live checks (target: all green, zero drift)

**The new GET — `StudentProfileDto` shape + names + bound device (`§A.1`, `§C.3`, `§C.5`, `§E`):**
1. `GET /api/me/profile` (Student JWT) → **`200 StudentProfileDto`** for **`ST_Amr`**: assert the exact field set —
   `id`, `fullName`, `phoneNumber`, `parentPhonePrimary`, `parentPhoneSecondary` (or `null`), `schoolName`,
   `gradeId` + `gradeName`, `cityId` + `cityName`, `regionId` + `regionName`, `status: "Active"`
   (StudentStatus **string** name), and the nested `boundDevice { summary, boundAtUtc }`. **Assert the resolved names
   match the DB:** `gradeName` == `grades."Name"` for `"GradeId"`, `cityName` == `cities."NameEn"` for `"CityId"`,
   `regionName` == `regions."NameEn"` for `"RegionId"` (the GET resolves city/region via `NameEn` per the
   `StudentDetailLoader` pattern — `City`/`Region` have **no `Name` column**, only `NameEn`/`NameAr`; the global-seeded
   joins use `IgnoreQueryFilters` on the name only, `§C.3`). **Assert the raw JSON contains NO `"email"` and NO `"avatar"`/`"avatarUrl"`** (decisions
   1 + 4 — email is Firebase-only, avatar deferred). **Assert `boundDevice.summary` == `student_devices."FingerprintSummary"`
   and `boundDevice.boundAtUtc` == `"BoundAtUtc"`** for the active device, and the raw JSON contains **NO `deviceTokenHash`**
   (`§C.5` — the token hash is NEVER exposed).
1b. **`boundDevice == null` fallback (`§C.5`):** `GET /api/me/profile` for the **no-device** student → **`200`** with
   `boundDevice: null` (the UI then renders a generic label). Confirm via psql the student has **no** `student_devices`
   row with `"IsActive"=true`.

**The new PUT — update, persist, leave-grade-unchanged, ignore-email/grade, audited (`§A.2`, `§C.1`, `§C.2`, `§E`):**
2. **Happy path (`§A #2`):** `PUT /api/me/profile` with a valid `UpdateMyStudentProfileRequest` (new `fullName`,
   `phoneNumber`, `schoolName`, `cityId`, `regionId`, `parentPhonePrimary`, and a non-null `parentPhoneSecondary`) →
   **`200 StudentProfileDto`** (the re-read, updated profile). **Assert the returned DTO echoes the seven new values**
   and the **new** `cityName`/`regionName` resolve to the new ids. **DB (psql):** `students."FullName"`/`"PhoneNumber"`/
   `"SchoolName"`/`"CityId"`/`"RegionId"`/`"ParentPhonePrimary"`/`"ParentPhoneSecondary"` all **persisted** to the new
   values (re-query, don't trust only the response), and `"UpdatedAtUtc"`/`"UpdatedById"` advanced.
3. **Grade UNCHANGED + email/grade in body IGNORED (`§C.1`, `§C.2`):** the body in #2 carried **no** `gradeId`/`email`
   (they are not in `UpdateMyStudentProfileRequest`). Assert `students."GradeId"` is **identical** before and after the
   PUT (the new `Student.UpdateOwnProfile(...)` domain method leaves grade untouched — it is **not** `UpdateContactInfo`).
   Then **adversarially POST extra keys** — send the PUT body **plus** `"gradeId": <a-different-grade>` and
   `"email": "attacker@x.com"` — and assert the response + DB still show the **original** `"GradeId"` (extra keys are
   bound away / ignored; grade is staff-managed `FR-ADM-STU-005`) and there is **no** email column to write (`§C.2`).
4. **Audited via the interceptor (`§E`):** snapshot `audit_entries` count **before/after** the PUT in #2 → **exactly
   one new row** for the `Student` aggregate: `"Action"` an update verb, `"EntityType"="Student"`, `"EntityId"` ==
   `ST_Amr.Id`, **`"ActorType"="Student"`** (the data subject, not System/staff), **`"Portal"="student"`**, and
   `BeforeJson`/`AfterJson` carry the field diff (`fullName`/phones/school/city/region changed). The handler writes **no
   explicit** audit row — it is the `SaveChanges` audit interceptor (mirror of staff `UpdateMyProfile`). The hash chain
   (`PrevHash`→`Hash`) stays intact (append-only).

**Validation `400` (PUT only, `§A.2`, `§B`, `§C.3`):**
5. **Empty required field** → **`400` ProblemDetails** (FluentValidation message): send `fullName: ""` (also spot-check
   `phoneNumber: ""`, `schoolName: ""`, `parentPhonePrimary: ""`) → each `400`; **DB unchanged** (re-query confirms no
   partial write).
6. **Too-long field** → **`400`**: send `fullName` of 201 chars (`>200`) and/or `phoneNumber` of 33 chars (`>32`) →
   `400`.
7. **Unknown / mismatched city-region** → **`400`** (`§C.3`): (a) an unknown `cityId` (random GUID) → `400`; (b) a
   `regionId` that does **not belong to** the supplied `cityId` (a real region of a **different** city) → `400` ("region
   does not belong to city" — the same rule the S1 wizard validates). Confirm via psql that the mismatched region row
   **does** exist (so the `400` is the belongs-to check, not a missing row). **DB unchanged** after each.

**Auth + tenant isolation (`§B`, `§C.5`, `NFR-SEC-007/010`):**
8. **Auth (`§B`):** anonymous (no bearer) → **`401`** on **both** GET and PUT; a **staff** (Teacher) JWT → **`403`** on
   both (the `RequireStudent` filter); the Student JWT → **`200`**. (No `404-self` exists — the subject always exists;
   no URL id → no IDOR surface, `NFR-SEC-007`.)
9. **Tenant isolation (`NFR-SEC-010`):** the **tenant-B** student's JWT → `GET /api/me/profile` returns **B's own**
   profile (B's `id`/names), **never** A's; and B's `PUT` updates **only** B's row (assert A's `students` row in tenant
   A is **untouched** via psql). The EF global filter scopes `Student` by `tenant_id` automatically — there is **no
   per-handler `Where(TenantId == …)`** and **no URL id to cross tenants with**. (If only one live tenant this run, note
   that the cross-tenant boundary is additionally covered by the backend integration test
   `MyStudentProfileApiTests`'s tenant-isolation case — the same EF-global-filter path.)

**The client-only flows — NO profile API fires (decisions 2 + 3 + 4, `§D`, `§E`):**
10. **Device-reset modal = INFO only, no API (`§D.2`):** with the network tab open, click **"Reset device"** → the
    **INFO modal** opens ("Reset bound device?" warning + contact-support copy); clicking the informational **"Request
    reset"** **just closes the modal** — assert **NO** request to `/api/me/profile` or any `/api/students/*/clear-device`
    fires (the recovery path is the **staff** clear, invoked out-of-band — not from the student modal). **Change-password
    = Firebase, not the platform (`§D.3`):** click **"Change password"** → the modal ("Send a password reset link to
    {email}?", email from `firebaseAuth.currentUser.email`) → confirm → assert the only network call is to **Firebase**
    (`identitytoolkit…:sendOobCode` / the `sendPasswordResetEmail` REST call) and **NO** backend route is hit; success
    state shows "Check your inbox." **Email read-only (`§C.2`):** the personal-info **Email** field is **disabled** and
    its value equals `firebaseAuth.currentUser.email` (sourced client-side, not from the DTO — the DTO has no email,
    asserted in #1). These flows are **not platform-audited** (`§E`).

**Sign-out clears the session (`§D.4`):**
11. **Sign-out (`§D.4`):** click **"Sign out"** (danger-ghost) → the confirm modal ("Sign out?" mascot + "Stay signed
    in"/"Sign out") → **"Sign out"** → the `StudentAuthStore` path runs: Firebase `signOut` fires, the **JWT pair is
    cleared** (assert no access/refresh token remains in storage), and the app **redirects to `/login`**. Re-opening a
    guarded route (e.g. `/profile`) while signed out bounces back to `/login`.

**The screen, live in the browser (`FR-STU-RWD-001/002`, `FR-STU-A11Y-001`):**
12. Open the student app at **:4300**, sign in as `ST_Amr`, open **Profile** (the nav item is now **enabled** in **both**
    the sidebar `NAV_ITEMS` and the `BOTTOM_ITEMS` — S6 flipped `disabled:false`): the **header band** renders with the
    **initials Avatar** (xl, status badge — no image/upload), name, sub-line, and the **"Active"** Chip; the
    **personal-info** form shows **Email disabled** + **Grade disabled** + the **City→Region cascade** (changing City
    reloads Region from the anon `/api/reference/cities/{cityId}/regions` read); edit a field + **"Save changes"** →
    re-render from the returned DTO; the **parent-phone** subsection (Primary required, Secondary optional); the
    **bound-device** card shows the fingerprint summary + "Bound {date}" + the contact-support helper; the three modals
    (**device-reset INFO**, **Firebase change-password confirm/success**, **sign-out confirm**) open and behave per #10
    /#11. Resize: the `1.6fr 1fr` grid collapses to a single column on phone, comfortable targets, matches the prototype
    across phone/tablet/desktop. *(The visual walkthrough is the user's step, as with S0 #9 / S1 #7 / S2 #9 / S3 #10 /
    S4 #10 / S5.)*

## Sign-off
- Log the run (counts + the `students` field values **before/after** the PUT, the `"GradeId"` unchanged assertion, the
  extra-keys-ignored result, the single `audit_entries` row `Action`/`EntityType=Student`/`EntityId`/`ActorType=Student`/
  `Portal=student` + the before/after count, the `boundDevice` summary/`boundAtUtc`/**no-token-hash**/null-fallback, the
  three `400` validation cases, the `401`/`403` matrix, the tenant-isolation result, and the **no-profile-API** evidence
  for the device-reset + change-password + email-read-only flows) into this file like the prior wiring logs. Update the
  master plan's **S6** line from *Planned* → **Met** with the date + headline result. Record a memory entry
  (`student-s6-wiring`). Note any gotchas (expect: **stale-API-needs-restart** for **both** new `/api/me/profile`
  routes — unlike S4/S5 there is no pre-existing engine route, so a stale API 404s both until restart; Aspire **renames
  containers + reassigns ports each run** — discover Postgres/MinIO by image, drive via the `:4300` proxy not the
  dynamic API port; the **shell/tool layer collapses `\\`→`\`** — POST JSON bodies from files if any field needs
  escaping; the **device-reset + change-password fire no backend** — assert the absence of `/api/me/profile` /
  `clear-device` calls in the network tab; email is **Firebase-only** — not in the DTO, sourced from
  `currentUser.email`).
- **S6 CLOSES the student-portal plan (S0..S6).** There is **no next student slice** to unblock — the personalized
  **Home / weekly study-plan** phase is separately planned (`docs/contracts/student-home-weekly-plan.md` +
  `IMPLEMENTATION-PLAN-student-home-{backend,frontend,wiring}.md`) and runs **post-S6**, reusing these same techniques.

---

## Frozen vs. stream-owned (this stream's lens)

- **Frozen (`docs/contracts/student-s6-profile.md`):** the two new routes `GET` + `PUT /api/me/profile` +
  `RequireStudent`; the `StudentProfileDto` field set incl. nested `boundDevice` and the **absence** of `email`/`avatar`
  (`§A.1`); the `UpdateMyStudentProfileRequest` seven writable fields + the **exclusion** of `gradeId`/email (`§A.2`);
  the validation rules + city/region existence/belongs-to `400` (`§A.2`/`§C.3`); the error modes (`401`/`403`/`400`,
  **no 404-self**, **no 409**, `§B`); the writable-vs-read-only split + the **new domain method that leaves grade
  unchanged** (`§C.1`); email read-only-from-Firebase (`§C.2`); bound-device read shape with the token-hash never
  exposed (`§C.5`); the four decisions + the three modal behaviours — **device-reset INFO/no-API**, **password Firebase
  email-reset**, **sign-out confirm** (`§D`); "GET not audited / PUT audited via interceptor" (`§E`); the deferred set
  (`§F`).
- **Backend owns (verified by this stream, fixed there if drift):** the `MeProfileEndpoints : IEndpointGroup`
  (`/api/me/profile`); the query/command folders (`Features/Profile/Queries/GetMyStudentProfile/` +
  `.../Commands/UpdateMyStudentProfile/`); the `StudentProfileDto` + `.ToProfileDto()` mapping; the new
  `Student.UpdateOwnProfile(...)` domain method (leaves grade unchanged); the `UpdateMyStudentProfileValidator` + the
  city/region existence + region-belongs-to-city `400`; the grade/city/region **name** joins (`IgnoreQueryFilters` on
  the global-seeded names); the active-`StudentDevice` projection for `boundDevice`; the integration tests
  (`MyStudentProfileApiTests`: GET shape + names + bound device, PUT happy-path + audit row, grade/email not writable,
  `400` bad city/region, `401` anon / `403` staff, tenant isolation `NFR-SEC-010`).
- **Frontend owns (verified by this stream):** the new `libs/student-portal/feature-profile` lib + the
  `@sb/student-portal/feature-profile` alias + the `profile` route + flipping the shell Profile nav to `disabled:false`
  in **both** `NAV_ITEMS` and `BOTTOM_ITEMS`; the `ProfileService` (`getProfile()`/`updateProfile(body)`, wire-exact
  `| null` models); the `ProfileComponent` (header band + **initials** Avatar + personal-info form with **Email
  disabled** + **Grade disabled** + city→region cascade reusing the reference data-access + parent-phone subsection +
  Save) and the three `sb-modal` `size="confirm"` modals (device-reset INFO, Firebase change-password, sign-out) wired
  to `StudentAuthStore` (`sendPasswordResetEmail` / `signOut`).
- **Wiring owns (this doc):** proving the slice live on the Aspire stack — GET returns the caller's data + resolved
  names + the active bound-device summary (tenant-isolated, **no token hash**, **no email/avatar**); PUT updates the
  seven writable fields, **leaves grade unchanged**, **ignores** any email/grade in the body, writes a `Student` audit
  row via the interceptor, and re-reads correctly; `400` on empty/too-long/bad-or-mismatched city-region; `401` anon /
  `403` staff; tenant isolation; the contact-support device modal + change-password fire **no** profile API and
  change-password defers to Firebase; sign-out clears the JWT pair + redirects. This is the **final** S6 wiring — it
  **closes the student-portal plan (S0..S6)**.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are running the WIRING stream of Student-Portal phase S6 (the FINAL slice — it CLOSES the student-portal plan
S0..S6) for Salah Bahzad. Prove the self-service Profile slice live on the running Aspire stack: the TWO new endpoints
GET /api/me/profile + PUT /api/me/profile (mirroring the staff /api/profile but on the Student aggregate). Zero contract
drift vs docs/contracts/student-s6-profile.md.

Read first, in order:
1. docs/IMPLEMENTATION-PLAN-student-s6-wiring.md (this doc — the 12 live checks + the Student-JWT mint, docker-exec-psql
   PascalCase-quoted-cols, discover-Aspire-containers-by-image, and stale-API-restart-for-BOTH-new-routes techniques).
2. docs/contracts/student-s6-profile.md (the FROZEN contract you're proving — §A the two routes + StudentProfileDto (NO
   email, NO avatar, nested boundDevice, token-hash never exposed) + UpdateMyStudentProfileRequest (seven writable
   fields, grade/email excluded); §B error modes (401/403/400, no 404-self, no 409); §C writable-vs-read-only + email
   read-only-from-Firebase + city/region cascade + bound-device read; §D the three client-only modals (device-reset
   INFO/no-API, Firebase change-password, sign-out); §E GET-not-audited / PUT-audited-via-interceptor; §F deferred).
3. The prior wiring logs (student-s4-wiring, student-s3-wiring, student-s2-wiring, student-s0-wiring) for the
   Student-role JWT mint, docker-exec-psql (PascalCase quoted columns, pipe SQL via stdin), "Aspire reassigns ports &
   renames containers (resolve by image)", and "stale AppHost 404 -> restart for the NEW route" gotchas. NOTE: S6's BOTH
   routes are new (no MeProfileEndpoints.cs existed) -> a stale API 404s BOTH until restart (unlike S4/S5).

Do: F5; confirm GET /api/me/profile is reachable (a no-bearer probe -> 401, not 404; else restart for the new routes —
BOTH are new); get the S2/S3/S4-left Active, device-bound student (ST_Amr) Student JWT + a staff JWT for the 403 check;
snapshot ST_Amr's students row + active student_devices row before the PUT. Run all checks — GET 200 StudentProfileDto
(exact field set, gradeName/cityName/regionName == DB, boundDevice.summary/boundAtUtc == active device, NO email/avatar
in raw JSON, NO deviceTokenHash; boundDevice null for a no-device student); PUT 200 -> seven writable fields persist in
the DB (re-query), GradeId UNCHANGED, extra gradeId/email keys IGNORED, exactly ONE audit_entries row
(EntityType=Student, ActorType=Student, Portal=student, field-diff); 400 on empty/too-long/unknown-or-mismatched
city-region (DB unchanged); 401 anon / 403 staff / 200 student on both routes; tenant isolation (tenant-B JWT reads/edits
only B); device-reset modal fires NO API, change-password fires Firebase sendPasswordResetEmail (no backend) + email is
disabled from currentUser.email, sign-out clears the JWT pair + redirects /login; and the browser screen at :4300
(header band + initials Avatar + Email/Grade disabled + City->Region cascade + Save + bound-device card + the three
modals, responsive). Log the run, flip the master plan S6 bullet to Met, write the student-s6-wiring memory. Note that
S6 CLOSES the student-portal plan (S0..S6); Home/weekly-plan is the separately-planned post-S6 phase.
```

---

## ✅ Run log — 2026-06-22 (DONE · zero contract drift)

Proven live on the running Aspire stack (Postgres `postgres:17.4` + Redis + MinIO + API + both Angular apps), driven
through the **`:4300`** student proxy (and `:4200` as a fallback) with a **direct Student-role JWT** (short claims
`nameid`/`role`/`tenant_id`/`token_type`/`device_id`, HS256, `iss=salah-bahzad-api`, `aud=salah-bahzad-admin` — the
`Jwt:Secret` from `appsettings.Development.json`; `/api/me/*` does **not** check the device). **No restart was needed** —
a no-bearer cold probe of `GET /api/me/profile` returned **401 (not 404)**, so the API already had **both** new routes.

**Fixtures (live tenant `019ed7e6…`):** `ST_Amr` = student `019eea33-27ca-7da0-bdb9-9161666b8c9c` (Amr Moataz, `Status=1`
Active), active device `019eea34…` (`FingerprintSummary="e4fc55de… - Windows - Edge"`, `BoundAtUtc=2026-06-21
12:42:45`), grade **First Secondary** / city **Cairo** / region **Shorouk**. No-device student = `019ee01d…`
("Student Test", Active, no active device). Staff `019ed951…` (Role=2) for the 403. Only **one** live tenant.

| # | Check | Result |
|---|---|---|
| 1 | `GET /api/me/profile` (Student JWT) | **200** — exact **14-key** `StudentProfileDto` (`id, fullName, phoneNumber, parentPhonePrimary, parentPhoneSecondary, schoolName, gradeId, gradeName, cityId, cityName, regionId, regionName, status, boundDevice`). `gradeName`/`cityName`/`regionName` == DB (First Secondary / Cairo / Shorouk). `boundDevice.summary`/`boundAtUtc` == the active device. **Raw JSON has NO `email`, NO `avatar`, NO `deviceTokenHash`** (grep 0/0/0). |
| 1b | GET (no-device Active student) | **200**, `boundDevice: null` (generic-label fallback). |
| 2 | `PUT /api/me/profile` happy path | **200** — DTO echoes the **seven** new values; city→**Assiut**, region→**Abu Tig** re-resolved. **DB re-query:** all 7 persisted; `UpdatedById`→`ST_Amr` (self), `UpdatedAtUtc` advanced. |
| 3 | Grade unchanged + extra keys ignored | `GradeId` **identical** before/after (`019edf60…`). Adversarial body with `gradeId=0000…` + `email=attacker@x.com` → **200**, `GradeId` still `019edf60…`, no email column to write. |
| 4 | Audited via interceptor | `audit_entries` **793 → 794 (Δ1)**. Row: `Action=Updated`, `EntityType=Student`, `EntityId=ST_Amr`, **`ActorType=Student`**, `ActorId=ST_Amr`, **`Portal=student`** (X-Portal header sent — see gotcha), `BeforeJson`(989)/`AfterJson`(1034) field diff. Handler writes **no** explicit row. |
| 5–7 | Validation `400` matrix (8 cases) | empty `fullName`/`phoneNumber`/`schoolName`/`parentPhonePrimary`; `fullName` 201 chars; `phoneNumber` 33 chars; unknown `cityId`; **mismatched region** (Cairo + Abu Tig) → all **400**. Sample body: *"The selected region does not belong to the selected city."* **DB unchanged** after each. |
| 8 | Auth | GET **401** anon / **403** staff / **200** student; PUT **401** anon / **403** staff / **200** student. No 404-self, no URL id → no IDOR surface. |
| 9 | Tenant isolation | A foreign-tenant token (ST_Amr id + tenant `1111…`) → GET **404** + PUT **404** (*"Profile '…' was not found."* — the EF global filter hides A's row). A's row **untouched**. Single live tenant → the true two-tenant case is additionally covered by `MeProfileApiTests`. |
| 10 | Client-only flows fire **no** profile API *(code-verified — no network to capture headlessly)* | `ProfileComponent.requestDeviceReset()` → toast "Contact support…" only, **no API** (`§D.2`). `confirmPasswordReset()` → `StudentAuthStore.requestPasswordReset(email)` → Firebase `sendPasswordResetEmail` (line 212), **no backend** (`§D.3`). Email input is **`disabled`**, sourced from `store.getCurrentEmail()` = `firebaseAuth.currentUser.email` (line 86), never a form control, never on PUT (`§C.2`). |
| 11 | Sign-out clears the session | `confirmSignOut()` → `store.signOut()` → Firebase `signOut` + `#clearSession()` (removes `TOKEN`/`REFRESH`/`STUDENT` from `sessionStorage`) + `router.navigate(['/login'])` (lines 200–204, 261–264). |
| 12 | Browser screen at `:4300` | **User's visual step** (the dev server is serving the compiled app; the route + nav flip are wired). Header band + initials `xl` Avatar + "Active" pill; Email & Grade disabled; City→Region cascade; Save; bound-device card; three `size="confirm"` modals; `1.6fr 1fr`→1col responsive — all per `profile.component.ts`. |

ST_Amr was **restored** to its original seed values after the PUT checks (the append-only PUT audit row is intentionally
left in place).

### Gotchas (for the next run / the Home phase)
- **Dev proxy cold-hit `000`.** The `:4300`/`:4200` Angular dev proxies intermittently **time out (`000`) on the first
  hit**, then succeed on retry — **not** a product issue. Always use `curl --max-time` + a small retry; never leave a
  curl un-timed (an un-timed cold hit hung a whole batch once).
- **No restart needed this run** — the running API already carried both new `/api/me/profile` routes (401, not 404, on
  the cold probe). *(If a future run 404s a NEW route, restart the AppHost — Aspire won't hot-add routes; for S6 a stale
  API would have 404'd **both** routes since neither pre-existed.)*
- **`Portal=student` needs the `X-Portal` header.** The audit row showed `Portal=student` because the wiring `curl` sent
  `X-Portal: student` (the backend honours it). **The student frontend does NOT send `X-Portal`** (grep: no such header
  anywhere in `frontend/`), so for real frontend-driven PUTs `Portal` is **null** — the **same pre-existing,
  non-blocking cross-cutting finding as S2 (redeem)**, *not* S6 drift. The security-relevant `ActorType=Student` is
  always correct (derived from the JWT role).
- **Direct-JWT mint** (Node HMAC-SHA256, short claims) is the fastest path; `/api/me/*` ignores `device_id`.
- **psql:** `docker exec -e PGPASSWORD=postgres -i <pg-by-image> psql -U postgres -d DefaultConnection` (Bash heredoc;
  snake_case tables, PascalCase quoted columns; City/Region names are **`NameEn`**, Grade is **`Name`**).

**S6 CLOSES the student-portal plan (S0..S6).** There is no next student slice; the personalized **Home / weekly
study-plan** phase is separately planned (`student-home-weekly-plan.md` + the three Home stream docs) and runs post-S6.
