# Student Portal · S1 — WIRING stream (prove registration → pending → approve → sign-in live)

> Status: **Planned — not yet built** · Created 2026-06-21 · Proves slice **S1**
> (`docs/IMPLEMENTATION-PLAN-student-portal.md` §S1) end-to-end on the **running Aspire stack** (Postgres + Redis +
> MinIO + API + both Angular apps), exactly like the prior wiring streams. Goal: **zero drift** vs
> `docs/contracts/student-s1-registration.md`, every register error exercised, the **new grades-by-slug** read proven
> tenant-scoped, and the **register → pending → (staff approve) → S0 sign-in** loop closed.
>
> Runs **after** the backend + frontend streams merge. Reuses the prior wiring techniques: read the Aspire-assigned
> ports from the dashboard (reassigned every run); verify DB state with `docker exec -i <pg> psql` (snake_case tables,
> **PascalCase quoted columns**; pipe SQL via **stdin** — PS 5.1 mangles inline `-c "…\"col\"…"`); mint Firebase tokens
> via the **Identity Toolkit REST** API (disable the outbound-internet sandbox for those calls).

---

## Design reference

This stream verifies behaviour, not pixels, but the **acceptance copy** is the **Student Portal** prototype
(`AUTH: REGISTER` wizard + its pending state, and `AUTH: LOGIN` for the sign-in reconciliation). The `403 { reason }`
strings the pending/rejected reconciliation checks come from S0 (master plan §S0). Confirm the running wizard at
**:4300** matches the prototype responsively while driving check #7.

## Pre-flight
- Backend + frontend streams merged; `dotnet test -c Release` green (minus the known baseline image test);
  `npx nx build student-portal` green. **No migration** for S1 — confirm the Aspire Postgres has `tenants`, `grades`,
  `students`, `cities`, `regions`, `terms_acceptances`, `audit_entries` (all from earlier phases).
- Start via **AppHost (F5)**. Read the API port + both web ports from the dashboard. **If `GET /api/reference/grades`
  404s (not 400/200), the running API is stale** — restart the AppHost (the recurring 5B-2/5C/S0 gotcha: Aspire won't
  hot-add new routes).
- **Confirm the real `tenantSlug`:** read the seeded tenant's `Slug` from psql (`SELECT "Slug" FROM tenants;`) and make
  sure `environment.tenantSlug` (or `window.__SB_TENANT__`) matches it — a wrong slug surfaces as `404 Tenant`.
- **Minting a fresh Firebase account without the browser** (to script `POST /register` like prior wiring scripted the
  engines): call Identity Toolkit **`accounts:signUp?key=<Firebase:WebApiKey>`** with a unique email/password →
  `{ idToken, localId }`. (The browser check #7 exercises the real Google + manual UI.) Disable the outbound sandbox
  for the REST call; loopback (`:4300` proxy) + `docker exec` psql work normally.
- **Scripting the multipart POST:** use `curl.exe -F` (or the Bash tool) — `curl -F firebaseIdToken=… -F tenantSlug=…
  -F fullName=… … -F idImage=@./fixture.jpg "<api>/api/students/register"`. **Don't** set `Content-Type` manually (let
  curl set the multipart boundary). PS 5.1's native multipart is fiddly — prefer `curl.exe`.

## Fixtures
- A small **valid ID image** (`fixture.jpg`, < 5 MB) + an **oversized**/wrong-type file for the negative checks.
- The seeded **tenant slug**, one **grade id** (from the new grades read), one **city id** + a **region id** of that
  city (from the reference reads). A **second tenant** (if present) + one of *its* grades — for the isolation check #2.
- A **previously-registered** Firebase account (or reuse the one from #3) — for the `409` duplicate check.

## Live checks (target: all green, zero drift)

**New grades reference (`§B#3`, the only new route):**
1. `GET /api/reference/grades?tenantSlug=<seeded slug>` (anonymous, no bearer) → **`200`** a non-empty `[{id,name}]`,
   ordered by name, **soft-deleted grades excluded**. `?tenantSlug=` **omitted/blank** → **`400`**; an **unknown slug**
   → **`404`**.
2. **Tenant isolation (`NFR-SEC-010`):** the seeded tenant's slug returns **only** its own grades — never the second
   tenant's. (Seed/confirm both in psql; assert the returned id-set ⊆ tenant A's grades.) This is the key check — the
   endpoint deliberately bypasses the global filter, so prove the explicit `TenantId` filter holds.
3. The **cascade reads still work** for the wizard: `GET /api/reference/cities` → list; `GET /api/reference/cities/
   {cityId}/regions` → only that city's regions. (Regression — they're unchanged, just confirm reachable via `:4300`.)

**Registration happy path (`§A`, `FR-STU-REG-001..008`):**
4. `accounts:signUp` → fresh `idToken` → `curl -F …` `POST /api/students/register` with all §A.1 fields + `idImage=@`
   → **`201`** `{ studentId, status:"Pending" }`, `Location: /api/students/{id}`.
   **DB (psql):** one `students` row, `"Status"=0` (Pending), the grade/city/region/school/parent phones persisted, a
   non-null ID-image object key; the terms consent recorded **on the student row** (`"TermsVersion"` + non-null
   `"TermsAcceptedAtUtc"` — Phase-2 stores it denormalized; there is **no** `terms_acceptances` table); an
   `audit_entries` row `Action=StudentRegistered`, **`ActorType=Student`**, `Portal=student` (`§E`).
   **Storage (MinIO):** the ID image object exists under the student's private key — confirm via the **staff**
   `GET /api/students/{id}/id-image` issuing a signed URL that resolves (and that this view is itself audited), or list
   the bucket with `mc`/the MinIO console. The student never receives the image.

**Registration error modes (`§A.3`):**
5. **`409`** — re-`POST /register` with the **same** Firebase account (same UID, same tenant) → `409` "An account
   already exists for this sign-in." **`404`** — a bogus `gradeId` (and separately a bogus `cityId`, a `regionId` not
   in `cityId`, and an unknown `tenantSlug`) each → `404`. **`400`** — omit a required field / oversized or wrong-type
   `idImage` → `400` with the FluentValidation messages. **`429`** — hammer `/register` past the `auth` window → `429`.

**Pending / rejected reconciliation with S0's exchange (`§C`, `FR-STU-REG-009`, `FR-PLAT-AUTH-005`):**
6. The student from #4 (still **Pending**) → mint a fresh `idToken` (`accounts:signInWithPassword`) →
   `POST /api/auth/student/exchange` → **`403 account_pending`**. Then **staff-reject** the student (admin
   `POST /api/students/{id}/reject` with a reason — or set `"Status"=2` + `RejectionReason` in psql) → exchange →
   **`403 account_rejected`** whose `detail` = the stored `RejectionReason`. Then **staff-approve** (admin approve, or
   `"Status"=1`) → exchange → **`200`** `StudentAuthResponse` + the **device binds** (the S0 happy path; `Set-Cookie:
   sb_device`, a `StudentDevices` row, `StudentSignedIn`/`StudentDeviceBound` audit). This proves the S1→S0 handoff.

**The wizard, live in the browser (`FR-STU-RWD-001/002`, `FR-STU-A11Y-001`):**
7. Open the student app at **:4300** → **Create account** from the login screen → run the **real** wizard: **manual**
   (email/pw) and, separately, **Google** (popup prefills name + read-only email, still asks phone); Step 2 grade
   loads from the new endpoint, the city→region cascade works, ≥ 1 parent phone is enforced, the ID picker rejects
   > 5 MB / wrong type, terms (incl. one-device policy) gates submit; submit → the **pending** state renders with the
   redeem-after-approval copy. Resize: single-column on phone, comfortable targets, matches the prototype across phone/
   tablet/desktop. Confirm a server `400`/`409` surfaces readable copy in the UI (not a raw error).

## Sign-off
- Log the run (counts + the `students`/`terms_acceptances`/`audit_entries` rows + the grades-by-slug result + the
  reconciliation `403`/`200` ladder) into this file like the prior wiring logs. Update the master plan's **S1** line
  from *Planned* → **Met** with the date + headline result. Record a memory entry (`student-s1-wiring`). Note any
  gotchas (expect: Aspire port/name reassignment; stale-API-needs-restart for the new route; wrong `tenantSlug` → 404;
  multipart-with-`curl.exe` vs PS 5.1; Firebase REST sandbox-disable; `students."Status"` is an int enum
  0=Pending/1=Active/2=Rejected/3=Inactive).
- **S1 unblocks S2** (catalogue & enrollment) — an `Active`, signed-in, device-bound student is the precondition the
  catalogue's `RequireStudent` reads assume.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are running the WIRING stream of Student-Portal phase S1 for Salah Bahzad. Prove the registration wizard +
the new anonymous grades-by-slug read live on the running Aspire stack, and close the register -> pending -> approve ->
S0 sign-in loop. Zero contract drift.

Read first, in order:
1. docs/IMPLEMENTATION-PLAN-student-s1-wiring.md (this doc — the 7 live checks + the accounts:signUp-to-get-an-idToken
   and curl.exe -F multipart techniques).
2. docs/contracts/student-s1-registration.md (the FROZEN contract you're proving — esp. §A field names, §B#3 grades,
   §A.3 errors, §C the S0-exchange reconciliation, §E audit).
3. The prior wiring logs (student-s0-wiring, phase5c-wiring) for the Firebase-REST + docker-exec-psql techniques and
   the "stale AppHost 404 -> restart" / "Aspire reassigns ports & container names" gotchas.

Do: F5; confirm the seeded tenant slug (psql) matches env; then run all checks — grades?tenantSlug= (200 tenant-scoped,
soft-deleted excluded; blank->400; unknown slug->404; cross-tenant isolation), cities/regions cascade, register happy
path (accounts:signUp -> idToken -> curl -F POST /register -> 201 Pending + students row Status=0 + terms_acceptances +
StudentRegistered/Student/student audit + ID image in MinIO via the staff signed-URL), error modes (409 duplicate, 404
bad grade/city/region/slug, 400 validation/oversized image, 429), the pending/rejected/approved reconciliation through
POST /api/auth/student/exchange (403 account_pending -> reject -> 403 account_rejected[+reason] -> approve -> 200 + device
binds), and the browser wizard at :4300 (manual + Google, cascade, >=1 parent phone, ID guard, terms gate, pending
render, responsive). Log the run, flip the master plan S1 bullet to Met, write the student-s1-wiring memory.
```

---

## MET — 2026-06-21 · all scripted checks green (1–6 + rate-limit), ZERO product drift (browser #7 pending user)

Driven on the running Aspire stack (Postgres `postgres-rrwqzrej` + Redis + MinIO `minio-zzkwsnue`/`sb-dev-private` +
API `:50482` + both Angular apps) through the **`:4300` student proxy**, with **real Firebase** (Identity Toolkit
`accounts:signUp`). Env `tenantSlug=salah-bahzad` confirmed == the seeded tenant slug. Subject: **Wiring Test Student**
`019eea34-8f30-78ad-9e1d-4cb854c5b849`, FirebaseUid `Hlhop0wepVW73WRk8UL7HFUkmUF3`, tenant
`019ed7e6-98bb-7db2-afbb-575170e45a50`.

- **#1 grades?tenantSlug=** ✅ `200` 2 grades for `salah-bahzad`; blank/missing `tenantSlug` → `400`; unknown slug →
  `404` ("Tenant 'does-not-exist-xyz' was not found").
- **#2 isolation + soft-delete (LIVE)** ✅ seeded a temp 2nd tenant `wiretest-t2` + 1 grade → `salah-bahzad` returns
  **only** its 2 grades (not the temp's), `wiretest-t2` returns **only** its grade; soft-deleting it → `[]`; cleanup →
  `404`. The endpoint bypasses the EF global filter, so this proves the explicit `TenantId` filter holds live (stronger
  than S0's deferred-to-integration approach).
- **#3 cascade** ✅ `cities` → 27; Alexandria → 44 regions.
- **#4 register happy path** ✅ `signUp → idToken → curl -F POST /register` → `201 {studentId, status:"Pending"}`.
  psql: `students."Status"=0`, all fields persisted, `IdImageObjectKey` set, **terms on the student row**
  (`TermsVersion=v1` + `TermsAcceptedAtUtc`), FirebaseUid matches; audit `StudentRegistered` **Student/student**.
  **MinIO:** `sb-dev-private/students/<tenant>/<student>/id-images/….png` (1.8 KiB, `image/png`).
- **#5 errors** ✅ `409` duplicate ("An account already exists for this sign-in."); `404` nonexistent grade +
  region-not-in-city; `400` missing field + wrong image type; **`429`** after the 10/min `auth` bucket (9×`400` then
  6×`429`). *(Note: an all-zeros GUID hits `NotEmpty()` → `400`, not `404`; use a non-empty nonexistent id for the 404
  path. Oversized-image `400` is covered by the validator + `ReferenceGradesAnonymousTests`/Phase-2 register tests.)*
- **#6 reconciliation (the S1→S0 loop)** ✅ Pending → `403 account_pending`; Rejected(+reason) → `403 account_rejected`
  (detail = the stored `RejectionReason`); Active → `200` + `Set-Cookie: sb_device=…; max-age=31536000; path=/; secure;
  samesite=lax; httponly` + token `role=Student`/`device_id`/`tenant_id`, `student.status=Active`. psql: one active
  `student_devices` row (`DeviceTokenHash` len 44 = SHA-256, fingerprint persisted); audit `StudentSignInRejected`×2 +
  `StudentSignedIn` + `StudentDeviceBound`, all **Student/student**.
- **#7 browser @ :4300** ⏳ the app serves (`/` + `/register` → `200`; the real `RegisterComponent` is routed and the S0
  placeholder is deleted). The visual walkthrough (manual + Google popup, cascade, ≥1 parent phone, ID-size guard,
  terms gate, pending render, responsive) is the user's visual step — as with S0 #9. `nx build student-portal` (AOT) +
  the `feature-auth` Jest suite were the frontend stream's gate.

**Static contract-drift audit: PASS** — `ReferenceEndpoints` (new anonymous `/grades`),
`ListGradesForRegistrationHandler` (tenant-by-slug + `IgnoreQueryFilters` + explicit `TenantId` + `!IsDeleted`), the
register multipart field names + `StudentRegistrationResultDto` + the four error statuses, and the `StudentRegistered`
audit (Student/student) all match `docs/contracts/student-s1-registration.md`.

**Doc correction (not product drift):** there is **no `terms_acceptances` table** — Phase-2 register records consent
**denormalized on the student** (`TermsVersion` + `TermsAcceptedAtUtc`). The contract §E/§A and check #4 were corrected
to the student columns. Consent is recorded (`FR-STU-REG-007`).

**Gotchas hit + handled:**
1. **Sandbox file reads:** `curl -F @/tmp/…` fails (`curl: (26) Failed to open/read local data`) — the Bash-tool
   sandbox restricts curl's file opens to the **project dir** even though `cat` reads `/tmp` fine. Stage the upload
   inside the CWD and reference it relatively (`-F idImage=@wiring-idimage.png`). Separately, **disabling** the sandbox
   for Firebase REST also breaks loopback (`HTTP 000`) — so keep `/register` + `/exchange` calls **sandboxed**, and run
   only `accounts:signUp`/`signInWithPassword` with the sandbox off.
2. **AppHost restarted mid-run** → every container was renamed (`postgres-qggdfmzv`→`postgres-rrwqzrej`, minio, redis,
   pgadmin). The `:4300` proxy auto-rebinds to the new API port (cities still `200`); the **DB persisted** (named
   volume). Resolve the PG/MinIO container by **image** each call: `docker ps … | awk -F'\t' '$2 ~ /^postgres:/'`.
3. **Auth rate-limit is ONE global 10/min bucket** shared by `/register` + `/auth/*` (the S0 finding) — space the
   functional checks so the deliberate `429` hammer (run **last**) doesn't starve the reconciliation exchange.
4. **MinIO busybox lacks `grep`** — `mc alias set loc … "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD"` (here `minioadmin`)
   then `mc stat loc/sb-dev-private/<key>` directly.
5. DB naming unchanged from S0: snake_case tables (`students`, `student_devices`, `audit_entries`) + **PascalCase quoted
   columns**; `students."Status"` int enum 0=Pending/1=Active/2=Rejected/3=Inactive; `docker exec -i -e PGPASSWORD=postgres`.

**Not committed.** S1's engine + the S1→S0 reconciliation are proven live; the browser visual walkthrough (#7) is the
only remaining user step.
