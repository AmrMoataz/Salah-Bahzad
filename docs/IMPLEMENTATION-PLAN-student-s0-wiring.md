# Student Portal · S0 — WIRING stream (prove sign-in + device binding + the shell live, and the AppHost run both portals)

> Status: **Planned — not yet built** · Created 2026-06-21 · Proves foundation phase **S0**
> (`docs/IMPLEMENTATION-PLAN-student-portal.md` §S0) end-to-end on the **running Aspire stack** (Postgres + Redis +
> MinIO + API + **both** Angular apps), exactly like the Phase 3/4/5A/5B/5C wiring streams. Goal: **zero contract
> drift** vs `IMPLEMENTATION-PLAN-student-s0-backend.md` §1, every `reason` exercised, device binding enforced, the
> shell guarded, and **F5 launches admin (:4200) + student (:4300) together**.
>
> Runs **after** the backend + frontend streams merge. Reuses the prior wiring techniques: read the Aspire-assigned
> ports from the dashboard (Aspire reassigns them every run), and verify DB state with `docker exec -i <pg> psql`
> (PascalCase columns need quotes; pipe SQL via **stdin** — PowerShell 5.1 mangles inline `-c "…\"col\"…"`).

---

## Design reference

This stream verifies behaviour, not pixels, but the **acceptance copy** comes from the **Student Portal** prototype
(`.claude/Salah Bahzad Student Portal/Student Portal.html`): the shell visuals (`APP` banner — sidebar / drawer+scrim /
bottom-nav + Redeem FAB / header), the `AUTH: LOGIN` screen, and the pending/rejected/one-device status copy. The
`reason` strings checked below map 1:1 to that copy (master plan §S0). Confirm the running student app matches the
prototype responsively while driving the checks.

---

## AppHost change to land first (the "run both portals" half of S0)

In `backend/src/SalahBahazad.AppHost/Program.cs`, add a **second `AddNpmApp`** beside the admin one so a single **F5**
runs both portals plus all infra:
```csharp
builder.AddNpmApp("student-portal", "../../../frontend", "start:student-portal")
    .WithReference(api)                  // injects services__api__http__0 for the proxy
    .WithEnvironment("BROWSER", "none")
    .WithHttpEndpoint(targetPort: 4300)  // admin stays 4200
    .ExcludeFromManifest();
```
- Add a `start:student-portal` script to `frontend/package.json` → `nx serve student-portal --port 4300`.
- Add `apps/student-portal/proxy.conf.js` that forwards **`/api` AND `/hubs`** off `services__api__http__0` (the admin
  proxy forwards only `/api`; the student portal **is** the QuizHub's consumer from S5, so it must forward `/hubs`
  too — wire it now so S5 needs no proxy change). Mirror the admin proxy otherwise.
- Staging/prod only: ensure the API's `Cors:AllowedOrigins` includes the student origin and the policy
  `AllowCredentials()` (the HttpOnly `sb_device` cookie is cross-origin there; dev is same-origin via the proxy).

## Pre-flight
- Backend + frontend streams merged; `dotnet test -c Release` green (minus the known baseline image test);
  `npx nx build student-portal` green. **No migration** for S0 — but confirm the Aspire Postgres already has the
  `Students` + `StudentDevices` tables (from earlier phases).
- Start the stack via **AppHost (F5)**. Read the API port + both web ports from the dashboard. **If new routes 404 (not
  401), the running API is stale** — restart the AppHost (the 5B-2/5C gotcha: Aspire won't hot-add new endpoints).
- **Getting a Firebase ID token without the browser** (to script the exchange like prior wiring scripted the engine):
  call the Firebase Auth REST endpoint `signInWithPassword?key=<Firebase:WebApiKey>` with a seeded test student's
  email/password → `idToken`, then `POST /api/auth/student/exchange`. (The browser check in #8 exercises the real UI.)

## Fixtures (reuse seed where possible)
- One **`Active`** student (approved) with a known Firebase email/password — the happy path + device binding.
- One **`Pending`**, one **`Rejected`** (with a `RejectionReason`), one **`Inactive`** student — the status gates.
- A **staff** Firebase account — the default-deny check (#7). A **second-tenant** student — isolation (#7).
- Clear any pre-bound `StudentDevice` for the Active student so #2 starts unbound (or assert re-bind behaviour).

## Live checks (target: all green, zero drift)

**Sign-in exchange (#1, `FR-STU-AUTH-001` / `FR-PLAT-AUTH-005`):**
1. Active student → Firebase REST `signInWithPassword` → `idToken` → `POST /api/auth/student/exchange` (no `sb_device`
   cookie yet, with `X-Device-Fingerprint`) → **`200` `StudentAuthResponse`** (access + refresh + `student{...}`).
   Response carries **`Set-Cookie: sb_device`** with `HttpOnly` (and `Secure`/`SameSite` per env).
2. **Token shape:** decode the access token → `role=Student`, `device_id` present, `tenant_id` = the student's tenant.
3. **DB (psql):** exactly one `StudentDevices` row for the student with `IsActive=true` + a `DeviceTokenHash` (never the
   raw token) + the `FingerprintSummary`; the student's `LastSeenAtUtc` is set; an `AuditEntries` row
   `Action=StudentSignedIn`, **ActorType=Student**, `Portal=student` (`FR-PLAT-AUD-002`).

**Device binding / one-device enforcement (`FR-PLAT-DEV-001/003/005`):**
4. Re-exchange **with the `sb_device` cookie returned in #1** → `200`, and **no second `StudentDevices` row** is created
   (same device re-uses the binding).
5. Re-exchange **without / with a forged `sb_device` cookie** (and/or a different fingerprint) → **`403`
   `device_not_recognized`**. Then exercise the existing staff **clear-device** path → re-exchange → a **new** active
   device binds (the old row is retained as history, `IsActive=false` — `FR-PLAT-DEV-004/006`).

**Status gates (`FR-PLAT-AUTH-005`, `FR-STU-REG-009`):**
6. Pending → `403 account_pending`; Rejected → `403 account_rejected` whose `detail` is the stored `RejectionReason`;
   Inactive → `403 account_inactive`. Each is a ProblemDetails with the machine `reason` + readable `detail`.
   **Audit (psql):** each blocked attempt — and the `device_not_recognized` of #5 — writes a `StudentSignInRejected`
   `AuditEntries` row (ActorType=Student, `reason` in the summary), per contract §1.5 ("everything is audited").

**Default-deny + isolation (`NFR-SEC-010`, `NFR-SEC-007`):**
7. A **staff** Firebase account on `/api/auth/student/exchange` → **`401`** (no `Student` row). Anonymous (no token) on
   any `RequireStudent` route → `401`; a **staff** platform token on a `RequireStudent` route → `403`. A second-tenant
   student never receives tenant-A data (the exchange stamps tenant from the student record).

**Silent refresh (`FR-PLAT-AUTH-006`):**
8. The student refresh token → `POST /api/auth/refresh` → `200` new pair, `role=Student` + `device_id` **preserved**.
   Then deactivate the student (admin) → refresh → **`401`** (reload re-checks `Active`).

**The shell, live in the browser (`FR-STU-RWD-001/002`, `FR-STU-A11Y-001`):**
9. Open the student app at **:4300**, sign in via the **real UI** (Google or email/password) → land in the guarded
   shell. Resize: **desktop/tablet → sidebar**, **mobile → drawer + scrim + bottom-nav with the Redeem FAB**; the
   header shows crumb/title + the notifications bell + the user chip; Sign out clears the session and returns to
   `/login`; hitting a guarded route while anonymous redirects to `/login` (default-deny). Confirm the student proxy
   reaches **`/api`** (and `/hubs` resolves, ready for S5).

## Sign-off
- Log the run (counts + the `StudentDevices`/audit rows + the decoded token claims) into this file like the prior
  wiring logs. Update the master plan's **S0** line from *Planned* → **Met** with the date + headline result. Record a
  memory entry (`student-s0-wiring`). Note any gotchas (expect: Aspire port/name reassignment; stale-API-needs-restart
  for the new routes; cookie `SameSite`/`withCredentials` behaviour through the proxy vs cross-origin).
- **S0 unblocks S1–S6** — the shell, the authenticated `/api/me/*` caller identity, and the device binding all the
  later student slices assume.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are running the WIRING stream of Student-Portal phase S0 for Salah Bahzad. Prove student sign-in + device binding +
the responsive shell live on the running Aspire stack, and make the AppHost launch BOTH portals. Zero contract drift.

Read first, in order:
1. docs/IMPLEMENTATION-PLAN-student-s0-wiring.md (this doc — the AppHost change + the 9 live checks + the
   Firebase-REST-to-get-an-idToken technique)
2. docs/IMPLEMENTATION-PLAN-student-s0-backend.md §1 (the FROZEN contract you're proving)
3. The prior wiring logs (phase5c-wiring, phase5b2-wiring) for the direct-token + docker-exec-psql techniques and the
   "stale AppHost 404 → restart" / "Aspire reassigns ports & container names" gotchas.

Do: add the second AddNpmApp("student-portal", …, "start:student-portal") on :4300 + the start:student-portal script +
apps/student-portal/proxy.conf.js forwarding /api AND /hubs; F5; then run all checks — exchange happy path (200 +
Set-Cookie HttpOnly sb_device + token role=Student/device_id/tenant_id + StudentDevices row + StudentSignedIn/Student
audit + LastSeenAtUtc), one-device enforcement (same cookie reuses; forged/missing → 403 device_not_recognized;
staff-clear → re-bind, old row retained), status gates (pending/rejected[+reason]/inactive → 403 reason), default-deny
(staff→401 on student exchange; anon→401, staff token→403 on RequireStudent; cross-tenant isolation), silent refresh
(role + device_id preserved; deactivate → 401), and the browser shell at :4300 (responsive sidebar↔drawer↔bottom-nav +
Redeem FAB, guards, sign out). Log the run in the wiring doc, flip the master plan's S0 bullet to Met, write the
student-s0-wiring memory.
```

---

## MET — 2026-06-21 · 8/9 live checks pass, ZERO contract drift (browser #9 pending user)

Driven on the running Aspire stack (Postgres `postgres-abceusgd` + Redis + MinIO + API + both Angular apps) through the
**`:4300` student proxy**, with **real Firebase** (Identity Toolkit `signInWithPassword`, `student2@salah-bahzad.local`).
Subject: student **Lean Amr** `019ee053-530d-719d-8383-b0e540ae0fa2`, tenant `019ed7e6-98bb-7db2-afbb-575170e45a50`.

- **#1 exchange** ✅ `200` `StudentAuthResponse` (status `Active`, `boundDevice` populated); `Set-Cookie: sb_device=…;
  max-age=31536000; path=/; secure; samesite=lax; httponly` (§1.3 exactly).
- **#2 token claims** ✅ `role=Student`, `device_id=019ee9de-1662-7918-88c7-9d5591c6fd27`, `tenant_id` matches.
- **#3 persistence** ✅ `students."Status"=1` (Active), `LastSeenAtUtc` stamped; **one** `student_devices` row —
  `DeviceTokenHash` length 44 (SHA-256, **not** the raw token), fingerprint persisted, `IsActive=t`; audit
  `StudentSignedIn` **and** `StudentDeviceBound`, `ActorType=Student`, `Portal=student` (§1.5).
- **#4 re-exchange WITH cookie** ✅ `200`, `boundAtUtc` unchanged, still **1** device, `StudentDeviceBound` still **1**.
- **#5 device enforcement** ✅ (a) missing/forged cookie → `403 device_not_recognized` (+2 `StudentSignInRejected`);
  (b) staff-clear (psql-simulated — the real `ClearStudentDevice` endpoint already shipped) → re-exchange **re-binds** a
  new active device, old row **retained** `IsActive=f`+ClearReason, `StudentDeviceBound`→**2**.
- **#6 status gates** ✅ Pending→`403 account_pending`; Rejected→`403 account_rejected` (detail = stored
  `RejectionReason`); Inactive→`403 account_inactive`; each audited `StudentSignInRejected`; restored Active.
- **#7 default-deny** ✅ anon on `RequireStudent` (`/api/me/assignments/by-session`) → `401`; student token → `404`
  (authorizes; 404 only because the sessionId is random); invalid Firebase token → `401`. *(staff-token→403 and
  cross-tenant deferred to the integration suite — no staff token provided; single tenant today.)*
- **#8 refresh** ✅ student refresh → `200`, `role=Student` + `device_id` preserved; after deactivate → `401`; restored.
- **#9 browser shell @ :4300** ⏳ pending the user's visual walkthrough (responsive sidebar↔drawer↔bottom-nav + Redeem
  FAB, guards, sign-out). The shell's Jest suites + `nx build student-portal` (AOT) are green; the device state was
  cleared at the end so a browser sign-in binds fresh.

**Static contract-drift audit: PASS** — `ExchangeStudentFirebaseTokenHandler`, `DeviceBindingService` (hash-only HMAC),
`StudentAuthResponse`/`StudentInfo`/`BoundDeviceInfo`, `JwtTokenService` student overloads (`role=Student`+`device_id`),
role-aware `RefreshTokenHandler`, `AuthEndpoints` (route + cookie), AppHost (4300, proxy forwards `/api`+`/hubs`) — all
match contract §1. `npx nx build student-portal` → green.

**Gotchas hit + handled:**
1. Outbound internet (Firebase REST) is sandboxed here → minted the ID token with the sandbox disabled; loopback
   (`:4300` proxy) + `docker exec` psql work normally.
2. DB naming on this stack is **mixed** — snake_case **tables** (`students`, `student_devices`, `audit_entries`) with
   **PascalCase quoted columns** (`"Status"`, `"DeviceTokenHash"`, …); `"Status"` is an integer enum
   (0=Pending,1=Active,2=Rejected,3=Inactive). `docker exec` psql needs `PGPASSWORD=postgres` (no interactive prompt).
3. The device-token cookie's internal deviceGuid ≠ the JWT `device_id` (= `StudentDevice.Id`) — **by design**
   (independent identifiers; verification is HMAC + hash-match, not GUID equality), **not** drift.
4. Naming note (not drift): the AppHost npm script is **`start:student`** (not `start:student-portal` as the plan text
   suggested) and the serve port **4300** is set in `apps/student-portal/angular.json` (so no `--port` flag needed).

**BUG FOUND + FIXED during #9 (browser):** the real browser login `400`'d with an empty body. Root cause —
`getDeviceFingerprint()` built the `X-Device-Fingerprint` header as `"<id> · <os> · <browser>"` using `·` (U+00B7);
**HTTP header values must be ASCII**, so Kestrel rejected the request with an empty-body `400` **before** the endpoint
(which is why it carried CORS headers but no ProblemDetails body, and why the scripted checks — which used an ASCII
fingerprint — never hit it). Fixed in `libs/student-portal/data-access/.../device-fingerprint.ts`: ASCII separator
`" - "` + a defensive `replace(/[^\x20-\x7E]/g,'')` strip. Verified live (ASCII fingerprint → endpoint reached, `403`
not `400`); `nx build student-portal` green. *(This is the value the design copy renders as "Windows · Edge" — the
middle dot is fine for on-screen display, just not inside an HTTP header.)*

**Not committed.** S0's backend/contract proven live; the browser walkthrough (#9) is unblocked by the fingerprint fix
and is the only remaining user step.
