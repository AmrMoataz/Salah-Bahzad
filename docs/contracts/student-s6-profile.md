# FROZEN CONTRACT — Student Portal · S6 · Profile (self-service account)

> Status: **Frozen** · Created 2026-06-22 · Slice: Student-Portal **S6** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S6). **Design anchor:** the prototype's **`PROFILE`** section + its
> **`Change-password`**, **`Device-reset`**, and **`Sign-out`** modals in
> `.claude/Salah Bahzad Student Portal/Student Portal.html`. Behaviour authority is `FR-STU-PRO-001..003`,
> `FR-PLAT-AUTH-009` (password delegated entirely to Firebase), `FR-STU-DEV-002/003`, `FR-PLAT-DEV-004` (staff clear).
>
> Satisfies: the student views + edits their own personal info — full name, the two parent/guardian phones, school,
> city, region (grade **read-only**) (`FR-STU-PRO-001/002`); sees their **bound device** + bind date
> (`FR-STU-DEV-003`); changes their password via the identity provider (`FR-STU-PRO-003`, `FR-PLAT-AUTH-009`); and
> signs out. This slice adds **two** new backend endpoints — **`GET /api/me/profile`** + **`PUT /api/me/profile`**
> (§A) — mirroring the existing **staff** `ProfileEndpoints` (`/api/profile`) but scoped to the **`Student`** aggregate
> under `/api/me`. **No new aggregate, no migration.**
>
> **Four user-confirmed decisions (2026-06-22), binding:** (1) **avatar = initials only** — no upload control, no
> `Avatar` field, no storage; (2) **device reset = contact-support only** — the "Reset device" button opens an
> **informational** modal and calls **no API** (recovery is the existing staff clear `POST /api/students/{id}/clear-device`);
> (3) **password = Firebase email reset link** — `sendPasswordResetEmail(auth, email)` client-side, no form, no backend;
> (4) **email = read-only Firebase identity** — not stored on `Student`, not in either DTO, shown disabled from
> `firebaseAuth.currentUser.email`.
>
> **S6 CLOSES the student-portal plan (S0..S6).** The personalized Home / weekly-plan phase is separately planned,
> post-S6. Frontend + wiring cite this file field-for-field. **Change this file first if anything moves.**

## 0. Ground rules

- **Backend = two new endpoints; mirror the staff profile pattern.** S6 adds **only** `GET /api/me/profile` (read) and
  `PUT /api/me/profile` (update self), built exactly like the existing **staff** `GET|PUT /api/profile`
  (`Api/Endpoints/ProfileEndpoints.cs` → `Application/Features/Profile/Queries/GetMyProfile` +
  `.../Commands/UpdateMyProfile`) but on the **`Student`** aggregate. **No `/api/me/profile` or `MeProfileEndpoints.cs`
  exists yet** — both are created (a new `IEndpointGroup MeProfileEndpoints.cs` alongside `MeCatalogueEndpoints.cs` /
  `MeSessionsEndpoints.cs`). **No new aggregate, no migration** (no new column — avatar is deferred, §F).
- **A NEW domain method — do NOT reuse `UpdateContactInfo`.** The staff-side `Student.UpdateContactInfo(Guid gradeId,
  string phoneNumber, string parentPhonePrimary, string? parentPhoneSecondary)` (`FR-ADM-STU-005`) takes a **`gradeId`**
  (would let the student change their own grade) and **omits** `FullName`/`SchoolName`/`CityId`/`RegionId`. S6 needs a
  **new** self-edit method (backend-owned name, e.g. `Student.UpdateOwnProfile(string fullName, string phoneNumber,
  string schoolName, Guid cityId, Guid regionId, string parentPhonePrimary, string? parentPhoneSecondary)`) that
  updates the seven writable fields and **leaves `GradeId` UNCHANGED** (§C.1).
- **Authenticated student surface.** Both endpoints use **`RequireStudent()`** (anon → 401, staff → 403) — identical to
  `/api/me/catalogue|sessions|assignments|quizzes|videos`. **The student id + tenant come from the JWT**
  (`ICurrentUserResolver.UserId` / `.TenantId`), never a URL id — the caller student is resolved by
  `db.Students.FirstOrDefault(s => s.Id == currentUser.UserId)`. There is **no URL id and no IDOR surface**
  (`NFR-SEC-007`): a student can only ever read/edit **their own** row (the JWT subject).
- **Tenant isolation is automatic.** `Student` is `ITenantOwned` → the EF global query filter scopes it to the caller's
  tenant and excludes soft-deleted rows. **Never** write a per-handler `Where(x => x.TenantId == …)`. Cross-tenant
  isolation is covered by an integration test on the new reads (`NFR-SEC-010`). (Name joins for the **global-seeded**
  `City`/`Region` may need `IgnoreQueryFilters` on the *name* lookup only — §C.3, backend-owned, mirroring
  `StudentDetailDto`/catalogue name resolution.)
- **No email column.** The `Student` entity has **no `Email` field** and **no `Avatar` field** — verified
  (`Domain/Entities/Student.cs`). Email lives **only** in Firebase; it is **not** in the GET or PUT DTO and is shown
  client-side, read-only, from `firebaseAuth.currentUser.email` (decision 4, §C.2).
- **Reads not audited; the update is.** `GET /api/me/profile` is a **pure read of the caller's own data** → **NOT
  audited** (parity with `/api/me/catalogue` + `/api/me/sessions`; the bound-device summary is low-sensitivity and was
  already returned at sign-in, so — like those reads, unlike the audited private ID-image read — it is not audited;
  `NFR-PRIV-001`'s audited-PII-reads apply to **staff** viewing a student's PII, **not** the data subject viewing their
  own profile). `PUT /api/me/profile` is a **state change** → **audited automatically by the `SaveChanges` audit
  interceptor** (mirror staff `UpdateMyProfile`, §E).
- **Enums over the wire are string names** (`JsonStringEnumConverter`) — the frontend models `status` as a string
  union. Dates are ISO-8601 `…AtUtc`. Validation messages are FluentValidation strings, co-located with the request
  (`NotEmpty` + `MaximumLength`, mirroring the register validators, §A.2).

## A. Profile — `GET` + `PUT /api/me/profile` (**NEW** · `RequireStudent`)

Two routes on a new `MeProfileEndpoints : IEndpointGroup` (`/api/me/profile`), modelled on the staff
`ProfileEndpoints` (`/api/profile`). Both resolve the caller by `currentUser.UserId`; tenant is automatic. The caller's
`Student` row **always exists** (it is the JWT subject) — so there is **no 404-self** path (§B).

| # | Method & path | Body | Returns | Notes |
|---|---|---|---|---|
| 1 | `GET /api/me/profile` | — | `200 StudentProfileDto` | The caller's own profile + resolved grade/city/region names + the active bound-device summary. Pure read — **not audited**. |
| 2 | `PUT /api/me/profile` | `UpdateMyStudentProfileRequest` | `200 StudentProfileDto` (updated) | Updates the seven writable fields via the new domain method (grade unchanged); `SaveChangesAsync` as the transactional pipeline → **audited by the interceptor**. Returns the re-read DTO. |

### A.1 Result — `StudentProfileDto` (shaped to the prototype's `PROFILE` header + personal-info + bound-device cards)

```jsonc
// 200 · StudentProfileDto  (NO email field — email is shown client-side from Firebase, §C.2)
{
  "id": "guid",
  "fullName": "string",
  "phoneNumber": "string",
  "parentPhonePrimary": "string",
  "parentPhoneSecondary": "string|null",
  "schoolName": "string",
  // grade is READ-ONLY (display only) — not in the PUT body (§C.1)
  "gradeId": "guid",
  "gradeName": "string|null",            // tenant-owned taxonomy display name (disabled field + header sub-line)
  // city → region (editable; the cascade reuses the anon /api/reference reads, §C.4)
  "cityId": "guid",   "cityName": "string|null",
  "regionId": "guid", "regionName": "string|null",
  "status": "Active",                    // StudentStatus string name; a signed-in student is "Active" → success Chip
  // bound device (FR-STU-DEV-003) — the active StudentDevice; the token hash is NEVER exposed (§C.5)
  "boundDevice": {
    "summary": "string|null",            // StudentDevice.FingerprintSummary (e.g. "Windows / Chrome"); null → UI shows a generic label
    "boundAtUtc": "…"                    // StudentDevice.BoundAtUtc (ISO-8601) → "Bound {date}"
  }                                       // null when the caller has no active device bound
}
```

### A.2 Request — `UpdateMyStudentProfileRequest` (PUT body; only the writable fields)

```jsonc
// PUT /api/me/profile  →  200 StudentProfileDto (the updated profile)
{
  "fullName": "string",                  // NotEmpty, MaximumLength(200)
  "phoneNumber": "string",               // NotEmpty, MaximumLength(32)
  "schoolName": "string",                // NotEmpty, MaximumLength(200)
  "cityId": "guid",                      // NotEmpty; must exist (§C.3)
  "regionId": "guid",                    // NotEmpty; must exist AND belong to cityId (§C.3) — else 400
  "parentPhonePrimary": "string",        // NotEmpty, MaximumLength(32)
  "parentPhoneSecondary": "string|null"  // optional; MaximumLength(32) when present
}
// NOT updatable: gradeId (grade is staff-managed, FR-ADM-STU-005) and email (Firebase identity, §C.2).
```

> **Validation (mirror the register validators, co-located `UpdateMyStudentProfileValidator`):** `fullName` NotEmpty
> `≤200`; `phoneNumber` NotEmpty `≤32`; `schoolName` NotEmpty `≤200`; `parentPhonePrimary` NotEmpty `≤32`;
> `parentPhoneSecondary` `≤32` (optional); `cityId`/`regionId` NotEmpty. The **city/region existence + region-belongs-to-city**
> check (a `400` when the pair is unknown or mismatched) is enforced in the handler/validator against the **global-seeded**
> `City`/`Region` set (§C.3) — the same shape the S1 registration wizard validates.

## B. Error modes — ProblemDetails

Same format as the templates' error-mode tables. The caller's `Student` row is the JWT subject, so it **always exists**
— there is **no 404** on either route.

| Status | Machine `reason` | Readable `detail` (render it) | When |
|---|---|---|---|
| `401` | — | (unauthorized) | No bearer (anonymous) — the `RequireStudent` filter. |
| `403` | — | (forbidden) | A **staff** JWT (the `RequireStudent` filter rejects non-students). |
| `400` | — | FluentValidation message | **PUT only** — a required field is empty / too long, or `cityId`/`regionId` is unknown or `regionId` does not belong to `cityId` (§C.3). |
| `200` | — | — | **GET:** the caller's profile. **PUT:** the updated profile (re-read `StudentProfileDto`). |

> There is **no 404-self** (the subject always exists), **no 409** (no conflicting-state path — grade/email are not
> writable, status is not changed here), and **no device/password endpoint to error on** (decisions 2 + 3 are
> client-only, §D).

## C. Update + interaction semantics (frozen)

### C.1 Writable vs. read-only fields (`FR-STU-PRO-001/002`)

- **Writable (PUT body, §A.2):** `fullName`, `phoneNumber`, `schoolName`, `cityId`, `regionId`, `parentPhonePrimary`,
  `parentPhoneSecondary`. Applied via the **new** `Student.UpdateOwnProfile(...)` domain method (§0), which trims and
  sets exactly these and **leaves `GradeId` untouched**.
- **Read-only `grade`:** `gradeId`/`gradeName` are returned by GET for display (a **disabled** dropdown + the header
  sub-line) but are **not** in the PUT body — a student cannot change their own grade (it is staff-managed,
  `FR-ADM-STU-005`). Do **not** reuse `UpdateContactInfo` (which takes a `gradeId`).
- **Read-only `email`:** see §C.2 — not stored, not in either DTO.
- **`status`** is returned for the header Chip only; it is never set by this slice.

### C.2 Email — read-only Firebase identity (decision 4 · **grounding correction**)

The `Student` entity has **no `Email` column** (verified) — email lives only in Firebase. Email is therefore **not** in
`StudentProfileDto` and **not** in `UpdateMyStudentProfileRequest`. The frontend displays it **read-only / disabled**,
sourced client-side from `firebaseAuth.currentUser.email`.
> **Grounding correction:** the prototype's personal-info card renders **Email** as an editable `<input>`. It is
> **not** editable and **not** stored on the student — S6 renders it disabled and sources it from Firebase. (Changing
> the email is a Firebase identity operation, out of scope for S6.)

### C.3 City → region cascade + existence (`FR-STU-PRO-001`)

`City`/`Region` are **global-seeded** reference data (no `TenantId`); `Region` belongs to a `City`. On PUT, the handler
validates that `cityId` and `regionId` exist and that `regionId` belongs to `cityId` (else **`400`**, §B) — the same
rule the S1 registration applies. For the GET name resolution (`cityName`/`regionName`) the join is against the global
set; because they carry no tenant filter the lookup may use `IgnoreQueryFilters` on the **name** only (backend-owned,
mirroring `StudentDetailDto`/catalogue name resolution). `gradeName` resolves against the tenant-owned `Grade` taxonomy.

### C.4 Reference dropdowns reused (no new reference read)

The EDIT form's **City** and **Region** dropdowns (with the city → region cascade) **reuse the existing anonymous
`GET /api/reference/*` reads** (cities, regions) already used by the S1 wizard — the `studentAuthInterceptor` skips
`/api/reference/`. **Grade is read-only**, so **no grades read is needed** in S6. **No new reference endpoint.**

### C.5 Bound device (`FR-STU-DEV-003`) — read-only summary, never the token

GET surfaces the caller's **active** `StudentDevice` (`IsActive == true`) as `boundDevice.summary`
(`StudentDevice.FingerprintSummary`) + `boundDevice.boundAtUtc` (`StudentDevice.BoundAtUtc`) — the same shape the
sign-in/refresh `BoundDeviceInfo(string? Summary, DateTimeOffset BoundAtUtc)` already returns. **`DeviceTokenHash` is
NEVER exposed.** `boundDevice` is `null` when the caller has no active device. The device card is **display-only** — the
"Reset device" button is contact-support-only (§D.2); there is **no student device endpoint** (§F).

## D. Screen + modal semantics (frozen — what the profile screen shows)

Driven by §A's `StudentProfileDto`, the prototype's `PROFILE` section, and decisions 1–4. The prototype binds the
layout/copy; this contract binds the behaviour. Where they conflict, the prototype wins on pixels/copy, this contract
wins on data + calls.

### D.0 Profile screen

- **Header band:** gradient (`135deg #EAF2FB → #EBF5E9`), the shared **Avatar** component (size **`xl`**, **initials
  only**, status badge — **no image, no upload**, decision 1), the name (bold 24px), a sub-line
  `"{gradeName} · {specialization/track} · {cityName}"` (prototype copy; render from the resolved names), and a
  **success Chip "Active"** from `status`. Two-column grid desktop (`1.6fr 1fr`) → single column on mobile
  (`FR-STU-RWD-001/002`).
- **Left card — "Personal information":** **Full name** (editable), **Email** (read-only/disabled, from Firebase —
  decision 4 / §C.2), **School** (editable), **Grade** (**disabled** — §C.1), **City** (dropdown), **Region** (dropdown,
  cascades from City — §C.3/§C.4). A **"Save changes"** primary button → `PUT /api/me/profile` (§A #2) → re-render from
  the returned DTO. Subsection **"Parent / guardian numbers":** **Primary** (required) + **Secondary** (optional)
  (`FR-STU-PRO-002`).
- **Right column — "Bound device" card:** smartphone icon, device name = `boundDevice.summary` (fall back to a generic
  label when `null`), `"Bound {boundDevice.boundAtUtc}"`, helper *"Only one device can access content. To switch
  devices, contact support to reset the binding."*, and a **"Reset device"** secondary-sm button → opens the **device-reset
  INFO modal** (§D.2). **"Security" card:** **"Change password"** secondary → the **Firebase reset-email modal** (§D.3);
  **"Sign out"** danger-ghost → the **sign-out confirm modal** (§D.4).

### D.1 Three modals — all use the shared `sb-modal` `size="confirm"`

(Confirm-modal patterns already exist: the leave-quiz modal in `feature-assessment`, the enroll modal in
`feature-catalogue`.)

### D.2 Device-reset modal — **INFO only, no API** (decision 2 · `FR-STU-DEV-002` · **grounding correction**)

Title **"Reset bound device?"** + a warning Alert *"One device only … contact support to reset … a limited number of
times"* + **Cancel** / an **informational "Request reset"** action that **just closes the modal** — it calls **no API**.
The recovery path is the **existing staff** clear `POST /api/students/{id}/clear-device`
(`Permission.StudentsDeviceClear`, `FR-PLAT-DEV-004`), invoked by staff out-of-band; the student modal merely tells them
to contact support.
> **Grounding correction:** the prototype's device-reset confirm button is a **no-op `closeModal`** — S6 keeps it as a
> pure informational/contact-support modal. **No new student device-reset endpoint is built** (§F).

### D.3 Change-password modal — Firebase email reset link (decision 3 · `FR-STU-PRO-003`, `FR-PLAT-AUTH-009` · **grounding correction**)

A confirm/info modal **"Send a password reset link to {email}?"** (email from `firebaseAuth.currentUser.email`) → the
action calls **`sendPasswordResetEmail(auth, email)` client-side** (the `StudentAuthStore` already imports
`sendPasswordResetEmail`) → success state **"Check your inbox."** **No backend, no platform reset logic** — password is
delegated **entirely** to Firebase (`FR-PLAT-AUTH-009`: the platform SHALL NOT build its own reset emails/logic).
> **Grounding correction:** the prototype shows a 3-field **current / new / confirm** password form — S6 **replaces**
> it with the Firebase reset-email confirm/success modal.

### D.4 Sign-out modal

Title **"Sign out?"** + mascot + *"You will need to sign in again … your progress is saved"* + **"Stay signed in"** /
**"Sign out"**. **Sign out** → the `StudentAuthStore` sign-out path (Firebase `signOut` + clear the JWT pair + redirect
`/login`).

## E. Audit (`FR-PLAT-AUD-002`)

- **`GET /api/me/profile`** — **pure read of the caller's own data, NOT audited** (parity with `/api/me/catalogue` +
  `/api/me/sessions`; the bound-device summary is low-sensitivity and already returned at sign-in. `NFR-PRIV-001`'s
  audited-PII-reads apply to **staff** viewing a student's PII, **not** the data subject viewing their own profile).
- **`PUT /api/me/profile`** — a **state change**, **audited automatically by the `SaveChanges` audit interceptor**
  (field-diff `BeforeJson`/`AfterJson`, `ActorType=Student`, `Portal=student`) — exactly like the staff
  `UpdateMyProfile`. The handler does **not** write an explicit audit row.
- The client-only flows are **not** platform-audited here: **change-password** is a Firebase operation (Firebase's own
  logs); **device-reset** is contact-support-only (no API → nothing to audit; the actual staff clear is audited
  separately by `FR-PLAT-DEV-004`); **sign-out** is the existing auth path.

## F. Deferred / **NOT built**

- **Real avatar upload (decision 1).** S6 ships **initials-only** display via the shared Avatar component — **no upload
  control, no `Avatar` field on `Student`, no DTO field, no `IFileStorage` use, no migration, no storage work.**
  `FR-STU-PRO-001` mentions an avatar; the **real avatar upload is DEFERRED** (a future slice would add an avatar object
  key + `UploadPrivateAsync`/`GetSignedReadUrlAsync` + a migration). Recorded here so it isn't silently dropped.
- **No student device-reset endpoint (decision 2).** The "Reset device" button is contact-support-only and calls no
  API; the recovery path is the existing staff `POST /api/students/{id}/clear-device` (`FR-PLAT-DEV-004`). No new
  student-facing reset endpoint, no `IDeviceBindingService` student route in S6.
- **No password form / no password backend (decision 3).** Change-password is `sendPasswordResetEmail` client-side; the
  prototype's 3-field form is replaced. The platform builds **no** reset email/logic (`FR-PLAT-AUTH-009`).
- **Email not editable (decision 4).** Email is read-only Firebase identity, not stored, not in either DTO; changing it
  is a Firebase identity operation out of scope for S6.
- **No new reference endpoint** — the city/region dropdowns reuse the existing anon `/api/reference/*` reads (§C.4).
- **No migration** — no new column.

## G. Frozen vs. stream-owned

- **Frozen (this file):** the **two new** routes `GET` + `PUT /api/me/profile` + `RequireStudent`; the
  `StudentProfileDto` field names/types incl. the nested `boundDevice` and the **absence** of `email`/`avatar` (§A.1);
  the `UpdateMyStudentProfileRequest` body — the **seven** writable fields and the **exclusion** of `gradeId`/email
  (§A.2); the validation rules (§A.2); the error modes (`401`/`403`/`400`, **no 404-self**, **no 409**, §B); the
  writable-vs-read-only split + the **new domain method that leaves grade unchanged** (§C.1); email read-only-from-Firebase
  (§C.2); the city/region cascade + existence `400` + reference-reuse (§C.3/§C.4); the bound-device read shape with the
  token-hash never exposed (§C.5); the four decisions and the three modal behaviours — **device-reset INFO/no-API**,
  **password Firebase email-reset**, **sign-out confirm** (§D); "GET not audited / PUT audited via interceptor" (§E);
  the deferred set (§F).
- **Backend owns:** the new `MeProfileEndpoints : IEndpointGroup` (`/api/me/profile`) wiring; the query/command folders
  (`Features/Profile/Queries/GetMyStudentProfile/` + `.../Commands/UpdateMyStudentProfile/` — implementer's call, keep
  the routes + DTO frozen); the `StudentProfileDto` + `.ToProfileDto()` mapping location; the new
  `Student.UpdateOwnProfile(...)` domain method (name/signature backend-owned, must leave grade unchanged); the
  `UpdateMyStudentProfileValidator` + the city/region existence + region-belongs-to-city `400` check; the grade/city/region
  **name** joins (`IgnoreQueryFilters` on the global-seeded names, §C.3); the active-`StudentDevice` projection for
  `boundDevice`; and the integration tests (GET shape + names + bound device, PUT happy path + audit row, grade/email
  not writable, `400` bad city/region, `401` anon / `403` staff, tenant isolation `NFR-SEC-010`).
- **Frontend owns:** the **new** `libs/student-portal/feature-profile` lib (project name
  `student-portal-feature-profile`; tags `["scope:student-portal","type:feature"]`; prefix `sb`; barrel exports
  `ProfileComponent`) + the `@sb/student-portal/feature-profile` path alias + the `profile` route in
  `apps/student-portal/src/app/app.routes.ts` + flipping the shell Profile nav item to `disabled: false` in **both**
  `NAV_ITEMS` and `BOTTOM_ITEMS`; the `ProfileService` in `data-access/.../profile/` (`getProfile()` /
  `updateProfile(body)`, wire-exact models with `| null`); the `ProfileComponent` (header band + initials Avatar +
  personal-info form with **Email disabled** + **Grade disabled** + city→region cascade reusing the reference
  data-access + parent-phone subsection + Save) and the three `sb-modal` `size="confirm"` modals (device-reset INFO,
  Firebase change-password, sign-out) wired to the `StudentAuthStore` (`sendPasswordResetEmail` / `signOut`); the Jest
  specs (`whenStable()`, not `fakeAsync`).
- **Wiring owns:** proving the slice live on the Aspire stack — `GET /api/me/profile` returns the caller's data + resolved
  grade/city/region names + the active bound-device summary (tenant-isolated, no token hash); `PUT` updates the seven
  writable fields, **leaves grade unchanged**, ignores any email/grade in the body, writes a `Student` audit row via the
  interceptor, and re-reads correctly; `400` on a bad/mismatched city/region; `401` anon / `403` staff; the
  contact-support device modal fires **no** API; change-password fires Firebase `sendPasswordResetEmail`; sign-out clears
  the JWT pair + redirects. This is the **final** S6 wiring — it **closes the student-portal plan (S0..S6)**.
