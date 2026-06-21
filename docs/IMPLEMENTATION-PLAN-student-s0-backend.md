# Student Portal · S0 — BACKEND stream (Student sign-in exchange + full device binding)

> Status: **Planned — not yet built** · Created 2026-06-21 · The **engine half** of foundation phase **S0** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S0). This is the one **real backend** piece of the otherwise
> frontend-led student engagement: the current Firebase exchange is **staff-only** (`ExchangeFirebaseTokenHandler`
> looks up `db.Staff` and 401s everyone else), and the `StudentDevice` entity exists but its **binding flow is not
> built** (the entity's own XML doc says the consent/binding/fingerprint flow is "built later behind
> `IDeviceBindingService`"). S0 backend adds the dedicated **student exchange** + the **device-binding service**.
>
> Satisfies: `FR-STU-AUTH-001` (student sign-in), `FR-PLAT-AUTH-005` (status-gated sign-in), `FR-STU-REG-009`
> (readable rejection reason), `FR-PLAT-AUTH-002/006` (platform JWT + refresh), `FR-STU-DEV-001..003` +
> `FR-PLAT-DEV-001..006` (full device binding). **Change this doc's frozen contract (§1) first if anything moves.**
>
> Gates: `dotnet test -c Release` green (the one pre-existing `QuestionBank` image-test failure is the known
> baseline); then the **wiring** stream (`IMPLEMENTATION-PLAN-student-s0-wiring.md`) proves it live on Aspire.

---

## Design reference (what this stream is built against)

This stream ships **no screen**, but its API shapes and `reason` codes are designed to feed the **Student Portal**
prototype — **not** the admin/Teacher prototype. The authoritative design is:

- **Prototype:** `.claude/Salah Bahzad Student Portal/Student Portal.html` (siblings `Dropdown.jsx`, `support.js`).
  The relevant HTML-comment section banners are **`<!-- ===== AUTH: LOGIN ===== -->`** (the sign-in screen that calls
  `/api/auth/student/exchange`) and **`<!-- ===== PROFILE ===== -->`** (the one-device copy: *"Only one device can
  access content. To switch devices, contact support to reset the binding."* and the **"Reset bound device?"** modal).
  The `403` `reason` strings below are written so the frontend can map each to that copy verbatim.
- **Status copy** the reasons drive (from `AUTH: REGISTER`): *"…then your teacher approves it"* (pending) and the
  *"pending approval — Salah will review and approve it"* state; rejection shows the stored `RejectionReason`.

The backend owns only the JSON; the frontend stream owns rendering it against those banners.

---

## 1. Frozen contract (this phase) — routes, DTOs, cookie, reasons

> S0 has **no separate `docs/contracts/` file** (the master plan freezes contracts only for S1/S2/S3/S6); this section
> **is** the frozen contract for the auth + device surface. Frontend + wiring cite it. Freeze before parallel work.

### 1.1 Routes (added to the existing `AuthEndpoints : IEndpointGroup`, group `/api/auth`)

| # | Method & path | Auth | Returns | Notes |
|---|---|---|---|---|
| 1 | `POST /api/auth/student/exchange` | `AllowAnonymous`, `RequireRateLimiting("auth")` | `StudentAuthResponse` | Firebase ID token → **Student** lookup → status gate → device bind/enforce → Student-role JWT pair. **`Set-Cookie`** issues/refreshes the device token on a successful bind. |
| 2 | `POST /api/auth/refresh` | `AllowAnonymous`, `RequireRateLimiting("auth")` | `AuthTokenResponse` *or* `StudentAuthResponse` | **Reused, made role-aware** — a `token_type=refresh` JWT whose role is `Student` reloads the `Student` (not `Staff`), re-checks `Active` + the device cookie, and reissues a Student pair preserving `device_id`. Staff refresh is unchanged. |

The **staff** `POST /api/auth/exchange` is **untouched** (separate surface, `FR-PLAT-AUTH-004`).

### 1.2 DTOs (Application/Features/Auth/DTOs)

```jsonc
// StudentAuthResponse (#1 success, and #2 when the refresh token is a student's) — parallels AuthTokenResponse
{ "accessToken": "…", "refreshToken": "…",
  "accessTokenExpiresAt": "2026-06-21T10:15:00Z", "refreshTokenExpiresAt": "2026-06-28T10:00:00Z",
  "student": { "id": "<guid>", "fullName": "…", "status": "Active",
               "boundDevice": { "summary": "Android · Chrome", "boundAtUtc": "2026-06-21T10:00:00Z" } } }
```
`StudentInfo(Guid Id, string FullName, StudentStatus Status, BoundDeviceInfo? BoundDevice)` ·
`BoundDeviceInfo(string? Summary, DateTimeOffset BoundAtUtc)`. (Email/avatar are S6's `/api/me/profile` concern — keep
this minimal; `FullName` feeds the prototype's *"Welcome back, {firstName}!"*.)

### 1.3 The device-token cookie (`FR-PLAT-DEV-005`)

- Name `sb_device` · **`HttpOnly`** · `Secure` · `SameSite=Lax` in dev (same-origin via the Angular proxy),
  **`SameSite=None; Secure`** in staging/prod (cross-origin portal↔API) · `Path=/` · long-lived (e.g. 365 d).
- Value = a **server-signed** opaque token (HMAC over `{studentId, deviceGuid, issuedAt}` with `Jwt:Secret` or a
  dedicated `Device:SigningKey`). The DB stores only its **hash** (`StudentDevice.DeviceTokenHash`) — never the raw
  token (it's a credential). The raw token lives only in the cookie.
- A `X-Device-Fingerprint` request header carries the **secondary** signal (client-built UA/platform summary) →
  persisted as `StudentDevice.FingerprintSummary` for staff visibility (`FR-PLAT-DEV-006`).

### 1.4 Reason codes (frozen) — each a ProblemDetails with machine `reason` + readable `detail`

| `reason` | Status | When | `detail` (readable) |
|---|---|---|---|
| `account_pending` | 403 | `Status == Pending` | "Your account is pending approval. Your teacher will review it soon." |
| `account_rejected` | 403 | `Status == Rejected` | The stored `RejectionReason` (`FR-STU-REG-009`). |
| `account_inactive` | 403 | `Status == Inactive` | "Your account has been deactivated. Contact support." |
| `device_not_recognized` | 403 | a different device than the one bound | "This device isn't recognised. Contact support to reset your bound device." |
| *(no student account)* | 401 | Firebase UID has no `Student` row | "This account doesn't have student access." (mirrors the staff 401) |
| *(refresh rejected)* | 401 | refresh token invalid / student no longer `Active` | "Your session has expired. Please sign in again." |

### 1.5 Audit (frozen) — "everything is audited" (`FR-PLAT-AUD-002`, client-confirmed)

- **`StudentSignedIn`** — one row per **successful** exchange. `EntityType=Student`, `EntityId=studentId`,
  **`ActorType=Student`**, `Portal=student`.
- **`StudentDeviceBound`** — one row whenever a **new** `StudentDevice` is bound (first sign-in / post-staff-clear).
  Student actor. (Re-using an already-bound device writes no audit row — nothing changed.)
- **`StudentSignInRejected`** — one row per **blocked** attempt **where a `Student` is resolved** (i.e. the
  status-gate failures `account_pending`/`account_rejected`/`account_inactive` and `device_not_recognized`).
  `EntityType=Student`, `EntityId=studentId`, `ActorType=Student`, `Portal=student`, `Summary` carries the machine
  `reason`. *(The `(no student account)` 401 has no tenant/student to attribute, so it is **logged**, not audited —
  noted so it isn't read as a gap.)*
- The role-aware **refresh** is **not** separately audited (it follows an already-audited sign-in; mirrors the staff
  refresh, which isn't audited either).

> **Decisions locked (user-confirmed 2026-06-21):** (a) the **same `/api/auth/refresh`** serves staff *and* students
> (made role-aware) — no second refresh endpoint; (b) **blocked sign-in attempts are audited** (`StudentSignInRejected`
> above) to match the client's "everything is audited" requirement.

---

## 2. Pre-flight
- Re-read `backend/CLAUDE.md` — **Authentication & authorization**, **Device binding**, **Audit log**, **Multi-tenancy**,
  and the **Security checklist**. Re-read the master plan §3.1 (binding conventions).
- Confirm what **already exists** (do **not** rebuild):
  - `ExchangeFirebaseTokenHandler` / `RefreshTokenHandler` (staff) + `AuthEndpoints` (`/api/auth/exchange|refresh`,
    `RequireRateLimiting("auth")` = fixed-window 10/min).
  - `JwtTokenService` (`IssueAccessToken(Staff)` / `IssueRefreshToken(Staff)` / `ValidateRefreshToken`); the bearer
    pipeline uses the default `ClaimTypes.Role` mapping, so a token with `role=Student` validates and flows through
    `RequireStudent()`.
  - `CurrentUserResolver` already classifies `ActorType == "Student"` from a `role=Student` claim and reads the
    **`device_id`** claim — **which `JwtTokenService.BuildToken` does not currently set** (see Step 4).
  - `Student` (status lifecycle `Pending/Active/Rejected/Inactive`, `RejectionReason`, **`LastSeenAtUtc` +
    `RecordSignIn(now)` already present** → **no migration**), `Student.Register`.
  - `StudentDevice.Bind(...)` / `Clear(...)` (one active per student, history retained) + the existing
    `ClearStudentDevice` command + `StudentDeviceClearedEvent` (staff-clear surface, `FR-PLAT-DEV-004`).
  - `IFirebaseAuthService.VerifyIdTokenAsync` → `FirebaseTokenClaims` (`.Uid`, `.Email`); `IAppDbContext.Students` /
    `.StudentDevices`; `IAuditWriter`; `RequireStudent()`.

## 3. Application — the student exchange (`ExchangeStudentFirebaseToken`)
`Features/Auth/Commands/ExchangeStudentFirebaseToken/` — command
`(string FirebaseIdToken, string? RawDeviceToken, string? Fingerprint, string? IpAddress)` + co-located validator.
Handler (parallels the staff handler, but for `Student`):
1. `firebaseAuth.VerifyIdTokenAsync` → claims.
2. **Student lookup, cross-tenant** (sign-in has no tenant claim): `db.Students.IgnoreQueryFilters()
   .FirstOrDefault(s => s.FirebaseUid == claims.Uid)`; null → `UnauthorizedAccessException` (401, "no student access").
   **Discover the tenant from the row** (no tenant slug needed at sign-in, unlike registration).
3. **Status gate** (`FR-PLAT-AUTH-005`): `Pending`→`account_pending`, `Rejected`→`account_rejected`(+`RejectionReason`),
   `Inactive`→`account_inactive` (new `ForbiddenException` carrying the machine `reason`); `Active` continues.
4. **Device bind/enforce** via `IDeviceBindingService` (Step 5) inside `ExecuteInTransactionAsync`:
   - load the student's active `StudentDevice` (if any);
   - **no active device** → `Bind` a new one from a freshly issued device token (first sign-in / post-staff-clear) →
     the handler returns the raw token so the endpoint can `Set-Cookie`; audit `StudentDeviceBound`;
   - **active device + matching cookie** (`Verify(rawDeviceToken) == device.DeviceTokenHash`) → OK, re-issue/refresh the
     cookie, no new row;
   - **active device + missing/mismatched cookie** → `ForbiddenException("device_not_recognized")` (one-device
     enforcement, `FR-PLAT-DEV-001/003`). *(Consent for binding is captured at registration via the one-device-policy
     terms checkbox — S1; sign-in binds transparently.)*
5. `student.RecordSignIn(clock.GetUtcNow())`; save.
6. Issue the **Student** JWT pair (Step 4) with `device_id = device.Id`; audit **`StudentSignedIn`** (Student actor,
   `Portal:"student"`, `FR-PLAT-AUD-002`). Return `StudentAuthResponse` + (on bind) the raw device token out-of-band to
   the endpoint.
- Map the new `reason`s in the existing exception→ProblemDetails middleware (machine `reason` + readable `detail`).
- **Audit blocked attempts (required, §1.5):** whenever a `Student` is resolved but the attempt is refused
  (status-gate or `device_not_recognized`), write a **`StudentSignInRejected`** row (Student actor, `reason` in the
  summary) before throwing. One row, no PII beyond the student id. The `(no student account)` 401 is logged only (no
  tenant to attribute). Use `IAuditWriter` directly (the request isn't an authenticated platform principal yet, so the
  interceptor is a no-op — same pattern as `RegisterStudentHandler`).

## 4. Infrastructure — student JWT + device service
1. **`JwtTokenService` student overloads** on `IJwtTokenService`:
   `IssueStudentAccessToken(Student student, Guid deviceId)` / `IssueStudentRefreshToken(Student student, Guid deviceId)`.
   `BuildToken` gains a `role` + optional `deviceId` parameter so it can stamp `ClaimTypes.Role = "Student"`,
   `tenant_id`, and **`device_id`** (the claim `CurrentUserResolver` already reads). Extend `ValidateRefreshToken` to
   surface `device_id` → add `string? DeviceId` to `TokenPrincipal` and a `Role` string (already present) so the
   refresh handler can branch.
2. **`IDeviceBindingService`** (Application interface) → `DeviceBindingService` (Infrastructure):
   `Issue(studentId, deviceGuid) → (rawToken, hash)` (HMAC-signed, `Device:SigningKey`/`Jwt:Secret`),
   `Verify(rawToken) → hash?`/bool, `Summarize(fingerprint) → string?`. DI `AddSingleton`.
3. **`RefreshTokenHandler` made role-aware:** if `principal.Role == "Student"`, reload `db.Students
   .IgnoreQueryFilters().FirstOrDefault(Id == principal.UserId)`, re-check `Status == Active` + (optionally) that the
   `device_id` still maps to an active `StudentDevice`; reissue a **student** pair (preserving `device_id`) → return
   `StudentAuthResponse`. Else the existing staff path. Any failure → 401.
4. **Cookie/CORS (Step 6 + Program.cs):** the API must allow credentialed cross-origin calls from the student origin in
   staging/prod — add the student portal origin to `Cors:AllowedOrigins` and ensure the CORS policy uses
   `AllowCredentials()` (dev is same-origin via the proxy, so the cookie just flows). **No secret committed**
   (`NFR-SEC-002`) — `Device:SigningKey` from config/secret store.

## 5. API — endpoints
- In `AuthEndpoints.Map`: add `group.MapPost("/student/exchange", ExchangeStudentAsync).RequireRateLimiting("auth")
  .AllowAnonymous().WithName("ExchangeStudentFirebaseToken").WithSummary(...).Produces<StudentAuthResponse>(200)
  .Produces<ProblemDetails>(400/401/403/429)`.
- `ExchangeStudentAsync` reads the `sb_device` cookie (`httpContext.Request.Cookies["sb_device"]`) and the
  `X-Device-Fingerprint` header, sends the command, and on a **bind** writes the cookie via
  `httpContext.Response.Cookies.Append("sb_device", rawToken, options)` (§1.3 options). Returns `Results.Ok(response)`.
- Refresh endpoint unchanged in shape; the handler now returns either DTO (both share the wire fields the client reads).

## 6. Migration
- **None.** `Student.LastSeenAtUtc` and the entire `StudentDevice` table already exist (migrations
  `AddStudentsAndDevices` / `AddStudentPhoneNumber`). Device tokens + their hash reuse existing columns. If a
  reviewer insists on a `Device:SigningKey` rotation column, that's a follow-up — **not** S0.

## 7. Tests (`dotnet test -c Release`)
- **Unit:**
  - status gate → each of `account_pending` / `account_rejected`(+reason) / `account_inactive`; `Active` proceeds.
  - device logic with a fake `IDeviceBindingService`/clock: first sign-in **binds** (one `StudentDevice`, `IsActive`);
    matching cookie re-uses it (no second row); missing/mismatched cookie → `device_not_recognized`.
  - issued student access token carries `role=Student`, `tenant_id`, **`device_id`** (decode + assert claims).
- **Integration (`WebApplicationFactory` + Testcontainers, faked Firebase):**
  - register → staff-approve → **exchange** → `200` `StudentAuthResponse` + **`Set-Cookie: sb_device` HttpOnly**; a
    `StudentDevice` row exists; a `StudentSignedIn` audit row with **Student** actor + `Portal=student`.
  - `Pending`/`Rejected`/`Inactive` → `403` + the right `reason` (+ `RejectionReason` echoed for rejected), **and each
    writes a `StudentSignInRejected` audit row** (Student actor, reason in summary) — §1.5.
  - **second device** (no/forged cookie, different fingerprint) → `403 device_not_recognized` + a `StudentSignInRejected`
    row; same cookie → `200`. First bind writes `StudentDeviceBound`; re-use writes none.
  - **refresh** with the student refresh token → `200`, role still `Student`, `device_id` preserved; then deactivate
    the student → refresh → `401`.
  - **default-deny / isolation:** a staff Firebase account on `/student/exchange` → `401`; anon → `401`; a student of
    tenant A never receives tenant B's id (`NFR-SEC-010`). The staff `/exchange` still works (no regression).

## Done = ready for wiring
Contract §1 satisfied; staff exchange untouched; suite green (minus the known baseline image test); **no migration**.
Hand to `IMPLEMENTATION-PLAN-student-s0-wiring.md`.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the BACKEND stream of Student-Portal phase S0 (student sign-in exchange + full device binding)
for Salah Bahzad (.NET 10, Clean Architecture + CQRS + source-gen Mediator). Edit backend/** ONLY.

Read first, in order:
1. backend/CLAUDE.md (Authentication, Device binding, Audit, Multi-tenancy, Security checklist)
2. docs/IMPLEMENTATION-PLAN-student-s0-backend.md (frozen contract §1 + steps 2–7) — THIS is the contract; S0 has no
   separate docs/contracts file. Change §1 first if anything moves.
3. The existing staff auth as the template: Application/Features/Auth/Commands/ExchangeFirebaseToken + RefreshToken,
   Infrastructure/Services/JwtTokenService + CurrentUserResolver, Domain/Entities/StudentDevice + Student,
   Api/Endpoints/AuthEndpoints, Api/Authorization/RequireStudent.

Build: a NEW ExchangeStudentFirebaseToken command/handler (Firebase → Student lookup by FirebaseUid IgnoreQueryFilters
→ status gate 403 {reason}(+rejectionReason) → device bind/enforce → Student JWT pair + StudentSignedIn audit);
IDeviceBindingService + DeviceBindingService (HMAC-signed HttpOnly sb_device cookie, store only the hash, fingerprint
as secondary); JwtTokenService student overloads that stamp role=Student + device_id; make RefreshTokenHandler
role-aware (Student branch reloads Student, re-checks Active + device, reissues a student pair); wire
POST /api/auth/student/exchange (AllowAnonymous, RequireRateLimiting("auth"), Set-Cookie on bind). Keep the staff
exchange untouched. NO migration (Student.LastSeenAtUtc + StudentDevice already exist).

Tests (xUnit v3 + Testcontainers + FluentAssertions): status-gate reasons, device bind/enforce, student token claims,
refresh role-awareness, default-deny + tenant isolation. Green gate: `dotnet test -c Release` (the one pre-existing
QuestionBank image test may stay red — baseline). Report the result.
```
