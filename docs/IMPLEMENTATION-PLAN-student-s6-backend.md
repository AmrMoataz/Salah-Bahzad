# Student Portal · S6 — BACKEND stream (self-service profile read + update)

> Status: **Planned — not yet built** · Created 2026-06-22 · The **engine half** of slice **S6** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S6 — the student's self-service Profile screen). This is the **FINAL
> vertical slice**; S6 **closes the student-portal plan (S0..S6)**. (The personalized Home / weekly-plan phase is
> separately planned, post-S6.)
>
> Satisfies `FR-STU-PRO-001/002` (the student **views + updates** their own personal info — full name, the two
> parent/guardian phones, school, city, region; grade **read-only**), `FR-STU-DEV-003` (sees their **bound device** +
> bind date), and the read half of `FR-STU-PRO-003` (the bound-device info; password + device-reset + sign-out are
> client-only, §F). This stream adds **two** new student endpoints — **`GET /api/me/profile`** + **`PUT
> /api/me/profile`** (`RequireStudent`) — mirroring the existing **staff** `ProfileEndpoints` (`/api/profile`) but scoped
> to the **`Student`** aggregate under `/api/me`. **No new aggregate. No migration. Change the contract
> (`docs/contracts/student-s6-profile.md`) first if anything moves.**
>
> **Four user-confirmed decisions (2026-06-22), binding on this stream:** (1) **avatar = initials only** — **no `Avatar`
> field, no DTO field, no storage, no migration** (§5/§F); (2) **device reset = contact-support only** — **no new
> student endpoint** (recovery is the existing staff clear); (3) **password = Firebase email reset link** — **no
> backend**; (4) **email = read-only Firebase identity** — **no `Email` column, not in either DTO**. Decisions 2–4 are
> client-only and out of this backend stream entirely (§F).
>
> Gate: `dotnet test -c Release` green (the one pre-existing `QuestionBank` image-test failure is the known baseline);
> then the **wiring** stream (`IMPLEMENTATION-PLAN-student-s6-wiring.md`) proves it live on Aspire and **closes the
> plan**.

---

## Design reference

This stream ships **no screen**; its two JSON shapes feed the **Student Portal** prototype's **`PROFILE`** section
(`.claude/Salah Bahzad Student Portal/Student Portal.html`): the header band (Avatar **initials only**, name, sub-line,
"Active" Chip), the "Personal information" left card (full name / **email disabled** / school / **grade disabled** /
city / region + parent-phone subsection + "Save changes"), and the "Bound device" right card (summary + bind date). The
authority is `docs/contracts/student-s6-profile.md` §A (the two routes + DTOs), §B (error modes), §C (update + name +
device semantics), §E (audit). The three modals (device-reset INFO, Firebase change-password, sign-out) are **all
client-only** (§D of the contract) — this backend stream provides **no** endpoint for any of them (§F).

---

## 1. Frozen contract (this stream)

Implements **`docs/contracts/student-s6-profile.md` §A** verbatim. Two routes on a **new** `MeProfileEndpoints :
IEndpointGroup` (`/api/me/profile`), both `RequireStudent` (anon → 401, staff → 403), both resolving the caller by
`ICurrentUserResolver.UserId`; tenant + soft-delete are the EF global query filter. **The caller's `Student` row always
exists** (it is the JWT subject) → **no 404-self** on either route (§B).

| # | Method & path | Body | Returns | Notes |
|---|---|---|---|---|
| 1 | `GET /api/me/profile` | — | `200 StudentProfileDto` | Caller's own profile + resolved grade/city/region names + the active bound-device summary. Pure read — **NOT audited**. |
| 2 | `PUT /api/me/profile` | `UpdateMyStudentProfileRequest` | `200 StudentProfileDto` (updated) | Updates the **seven** writable fields via the **new** `Student.UpdateOwnProfile(...)` domain method (grade unchanged); `SaveChangesAsync` via the transactional pipeline → **audited by the `SaveChanges` interceptor**. Returns the re-read DTO. |

### 1.1 `StudentProfileDto` (GET + PUT result — contract §A.1)

```jsonc
// 200 · StudentProfileDto  — NO email field (email is shown client-side from Firebase, contract §C.2);
//                            NO avatar field (initials only, decision 1, §F)
{
  "id": "guid",
  "fullName": "string",
  "phoneNumber": "string",
  "parentPhonePrimary": "string",
  "parentPhoneSecondary": "string|null",
  "schoolName": "string",
  "gradeId": "guid",   "gradeName": "string|null",   // READ-ONLY display (tenant-owned taxonomy) — NOT in the PUT body
  "cityId": "guid",    "cityName": "string|null",    // global-seeded reference
  "regionId": "guid",  "regionName": "string|null",  // global-seeded reference; belongs to cityId
  "status": "Active",                                 // StudentStatus string name (JsonStringEnumConverter) → "Active" Chip
  "boundDevice": {                                    // active StudentDevice; null when none — token hash NEVER exposed (§C.5)
    "summary": "string|null",                         // StudentDevice.FingerprintSummary
    "boundAtUtc": "…"                                 // StudentDevice.BoundAtUtc (ISO-8601)
  }
}
```

### 1.2 `UpdateMyStudentProfileRequest` (PUT body — contract §A.2; only the writable fields)

```jsonc
{
  "fullName": "string",                  // NotEmpty, MaximumLength(200)
  "phoneNumber": "string",               // NotEmpty, MaximumLength(32)
  "schoolName": "string",                // NotEmpty, MaximumLength(200)
  "cityId": "guid",                      // NotEmpty; must exist (§3.3)
  "regionId": "guid",                    // NotEmpty; must exist AND belong to cityId (§3.3) — else 400
  "parentPhonePrimary": "string",        // NotEmpty, MaximumLength(32)
  "parentPhoneSecondary": "string|null"  // optional; MaximumLength(32) when present
}
// NOT updatable: gradeId (staff-managed, FR-ADM-STU-005) and email (Firebase identity, §C.2).
```

**No new aggregate, no migration** (no new column — avatar deferred, §5/§F).

## 2. Pre-flight (confirm — mirror, do NOT rebuild)

- **The staff profile slice** (`Api/Endpoints/ProfileEndpoints.cs` → `Application/Features/Profile/Queries/GetMyProfile`
  + `.../Commands/UpdateMyProfile`) — **the template.** Mirror it exactly:
  - `ProfileEndpoints` maps `GET /api/profile` (`GetMyProfileQuery` → `GetMyProfileHandler`:
    `db.Staff.AsNoTracking().FirstOrDefault(s => s.Id == currentUser.UserId)` → `.ToDto()`) and `PUT /api/profile`
    (`UpdateMyProfileCommand` → handler: fetch by `currentUser.UserId`, call the domain method, `SaveChangesAsync`,
    return `.ToDto()`). The command is `IRequest<…> , ITransactionalRequest` (the transactional pipeline opens the tx;
    the **`SaveChanges` audit interceptor** writes the field-diff row — the handler writes **no** explicit audit).
    Validator co-located (`UpdateMyProfileValidator` = `NotEmpty` + `MaximumLength`).
  - S6 is the **same pattern on the `Student` aggregate**, under `/api/me/profile`, gated `RequireStudent()`.
- **`MeCatalogueEndpoints` / `MeSessionsEndpoints`** (`Api/Endpoints/`) — the **shape** for the new `IEndpointGroup`: a
  `MapGroup("/api/me/profile")` with `.WithTags(...).WithOpenApi()`, each route `.RequireStudent()` (anon → 401, staff →
  403). **`MeProfileEndpoints.cs` does not exist yet** — create it alongside these. (Note: the staff `ProfileEndpoints`
  uses `RequireAuthorization()` on the group; the student group uses **`RequireStudent()` per route**, matching
  `MeCatalogueEndpoints`.)
- **`Student` entity** (`Domain/Entities/Student.cs`) — fields verified: `FullName` (≤200), `PhoneNumber` (≤32),
  `ParentPhonePrimary` (≤32), `ParentPhoneSecondary?` (≤32), `GradeId`, `CityId`, `RegionId`, `SchoolName` (≤200),
  `Status` (`StudentStatus`). **No `Email` field. No `Avatar` field** (confirmed — decisions 1 + 4). The existing
  `UpdateContactInfo(Guid gradeId, string phoneNumber, string parentPhonePrimary, string? parentPhoneSecondary)` is
  **staff-side** (`FR-ADM-STU-005`): it takes a `gradeId` (would let a student change their own grade) and **omits**
  `FullName`/`SchoolName`/`CityId`/`RegionId`. **Do NOT reuse it** — add a new self-edit method (§3.1).
- **`StudentDevice` entity** (`Domain/Entities/StudentDevice.cs`) — `StudentId`, `DeviceTokenHash` (**never exposed**),
  `FingerprintSummary?`, `BoundAtUtc`, `IsActive`. Active device = `IsActive == true`. The active-device projection for
  `boundDevice` mirrors `StudentDetailLoader` (§3.2).
- **`StudentDetailLoader`** (`Features/Students/StudentDetailLoader.cs`) — **the name-resolution template**: grade name
  via `db.Grades.IgnoreQueryFilters().Where(g => g.Id == student.GradeId).Select(g => g.Name)` (ignore filters so an
  archived/soft-deleted grade still resolves — grade is tenant-owned + soft-deletable), city via
  `db.Cities.Where(c => c.Id == student.CityId).Select(c => c.NameEn)`, region via
  `db.Regions...Select(r => r.NameEn)` (City/Region are **global-seeded**, no tenant filter), and the active device via
  `db.StudentDevices.AsNoTracking().FirstOrDefault(d => d.StudentId == student.Id && d.IsActive)`. **Mirror this loader's
  joins** for the GET (§3.2).
- **`BoundDeviceInfo(string? Summary, DateTimeOffset BoundAtUtc)`** (`Features/Auth/DTOs/StudentAuthResponse.cs`) — the
  **exact** bound-device shape already returned by exchange/refresh; the new `StudentProfileDto.boundDevice` re-uses this
  shape (same field names/types). (`StudentAuthResponse.cs`'s own comment says "email/avatar are **S6's**
  `/api/me/profile` concern" — this is that slice; per decisions 1 + 4 there is still **no** email/avatar field.)
- **`ICurrentUserResolver`** — `.UserId` (the student id, from `ClaimTypes.NameIdentifier`), `.IsAuthenticated`,
  `.ActorType` (`RequireStudentFilter` checks `== "Student"`). Tenant is **automatic** via the global query filter
  (`Student` is `ITenantOwned`) — **never** write a per-handler `Where(x => x.TenantId == …)`.
- **`RequireStudentExtensions.RequireStudent()`** (`Api/Authorization/RequireStudent.cs`) — anon → 401, staff → 403.
  Both new routes use it.
- **`ITransactionalRequest`** (`Application/Common/Interfaces/`) — the marker the staff `UpdateMyProfileCommand` carries;
  the pipeline behaviour opens the transaction and the `SaveChanges` audit interceptor writes the diff. The new PUT
  command carries it too.

## 3. Application — the two new requests (`Features/Profile/...` , student-scoped)

Keep the student profile beside the staff profile under `Features/Profile/` in **student-scoped** sub-folders so the
mirror is obvious (implementer's call — the contract §G freezes the routes + DTO names, not the folder). Suggested:
`Features/Profile/Queries/GetMyStudentProfile/` + `.../Commands/UpdateMyStudentProfile/`, with the DTO + mapping in
`Features/Profile/DTOs/StudentProfileDtos.cs`. Both resolve the caller by `ICurrentUserResolver.UserId`; tenant +
soft-delete are the global filter.

### 3.1 Domain — the **new** `Student.UpdateOwnProfile(...)` method (contract §0 / §C.1)

Add to `Domain/Entities/Student.cs` (do **not** reuse `UpdateContactInfo`). Signature backend-owned; **must leave
`GradeId` UNCHANGED** and set exactly the seven writable fields, trimming as `Register`/`Resubmit` do:

```csharp
/// <summary>The student edits their OWN profile (FR-STU-PRO-001/002). Updates the seven writable fields and
/// LEAVES GradeId UNCHANGED — a student cannot change their own grade (staff-managed, FR-ADM-STU-005).
/// Distinct from the staff-side UpdateContactInfo (which takes a gradeId and omits name/school/city/region).</summary>
public void UpdateOwnProfile(
    string fullName, string phoneNumber, string schoolName,
    Guid cityId, Guid regionId, string parentPhonePrimary, string? parentPhoneSecondary)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
    ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
    ArgumentException.ThrowIfNullOrWhiteSpace(schoolName);
    ArgumentException.ThrowIfNullOrWhiteSpace(parentPhonePrimary);
    if (cityId == Guid.Empty) throw new ArgumentException("A student must have a city.", nameof(cityId));
    if (regionId == Guid.Empty) throw new ArgumentException("A student must have a region.", nameof(regionId));

    FullName = fullName.Trim();
    PhoneNumber = phoneNumber.Trim();
    SchoolName = schoolName.Trim();
    CityId = cityId;
    RegionId = regionId;
    ParentPhonePrimary = parentPhonePrimary.Trim();
    ParentPhoneSecondary = string.IsNullOrWhiteSpace(parentPhoneSecondary) ? null : parentPhoneSecondary.Trim();
    // GradeId, Status, Email (none), Avatar (none) — UNCHANGED.
}
```

> The handler-level FluentValidation (§3.4) is the user-facing 400; these guard clauses are the domain invariant. The
> **city/region existence + region-belongs-to-city** check lives in the handler/validator (§3.3), not here (the entity
> doesn't see the reference set).

### 3.2 `GetMyStudentProfile`

- `GetMyStudentProfileQuery() : IRequest<StudentProfileDto>`. **No validator needed.** (Mirror the staff
  `GetMyProfileQuery`.)
- `GetMyStudentProfileHandler(IAppDbContext db, ICurrentUserResolver currentUser)`:
  1. `var studentId = currentUser.UserId;`
  2. `var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId, ct);` — the caller is
     the JWT subject, so this is **always** non-null; defensively `?? throw new NotFoundException("Profile", studentId)`
     (the staff handler does the same; it is **not** an error path the contract surfaces — §B has no 404-self). Tenant +
     soft-delete are the global filter.
  3. Resolve names **mirroring `StudentDetailLoader`**: `gradeName` via `db.Grades.IgnoreQueryFilters()...Select(g =>
     g.Name)`; `cityName` via `db.Cities.Where(c => c.Id == student.CityId).Select(c => c.NameEn)`; `regionName` via
     `db.Regions.Where(r => r.Id == student.RegionId).Select(r => r.NameEn)`.
  4. Resolve the **active** device: `var device = await db.StudentDevices.AsNoTracking().FirstOrDefaultAsync(d =>
     d.StudentId == student.Id && d.IsActive, ct);` → `null` when none.
  5. `return student.ToProfileDto(gradeName, cityName, regionName, device);` (manual mapping — §3.4). **Pure read — not
     audited** (§E).

### 3.3 `UpdateMyStudentProfile`

- `UpdateMyStudentProfileCommand(string FullName, string PhoneNumber, string SchoolName, Guid CityId, Guid RegionId,
  string ParentPhonePrimary, string? ParentPhoneSecondary) : IRequest<StudentProfileDto>, ITransactionalRequest` (the
  marker → transactional pipeline + audit interceptor, exactly like the staff `UpdateMyProfileCommand`). The
  endpoint builds this from the `UpdateMyStudentProfileRequest` body (§4).
- `UpdateMyStudentProfileHandler(IAppDbContext db, ICurrentUserResolver currentUser)`:
  1. `var student = await db.Students.FirstOrDefaultAsync(s => s.Id == currentUser.UserId, ct) ?? throw new
     NotFoundException("Profile", currentUser.UserId);` — **tracked** (not `AsNoTracking`), the JWT subject; tenant +
     soft-delete via the global filter.
  2. **City/region existence + region-belongs-to-city** check (the §A.2 `400`). Against the **global-seeded** set:
     `var region = await db.Regions.FirstOrDefaultAsync(r => r.Id == command.RegionId && r.CityId == command.CityId,
     ct);` → `null` ⇒ `throw new ValidationException(...)` (or `BadRequestException`, whichever the codebase maps to
     `400` — mirror how S1 registration validates the same pair). This single query proves *region exists* **and**
     *belongs to the city*; a separate `db.Cities.AnyAsync(c => c.Id == command.CityId)` is only needed if the contract
     wants a distinct "unknown city" message (a bad city ⇒ no matching region ⇒ already 400). Either way the result is a
     **400** (§B).
  3. `student.UpdateOwnProfile(command.FullName, command.PhoneNumber, command.SchoolName, command.CityId,
     command.RegionId, command.ParentPhonePrimary, command.ParentPhoneSecondary);` — **grade unchanged**.
  4. `await db.SaveChangesAsync(ct);` — the **`SaveChanges` audit interceptor** writes the field-diff `Student` audit row
     (`ActorType=Student`, `Portal=student`, `BeforeJson`/`AfterJson`). The handler writes **no** explicit audit (mirror
     staff `UpdateMyProfile`).
  5. Re-read + return the DTO. Re-resolve names + the active device exactly as §3.2 (the writable set changed
     city/region, so re-resolve `cityName`/`regionName` from the **updated** ids; `gradeName` + device are unchanged but
     re-resolving keeps one code path). Return `student.ToProfileDto(gradeName, cityName, regionName, device)`.
     *(Implementer's call: factor steps 3.2.3–3.2.5 + 3.3.5 into one private `LoadProfileDtoAsync(db, student, ct)`
     helper so GET and PUT return an identical shape — like `StudentDetailLoader` is shared across the student
     mutations.)*

### 3.4 DTOs + validator + mapping

`Features/Profile/DTOs/StudentProfileDtos.cs` — field order = the contract §A.1 shape:

```csharp
/// <summary>The caller's single active bound device (FR-STU-DEV-003) — same shape as the auth BoundDeviceInfo.
/// The device-token hash is NEVER exposed.</summary>
public sealed record ProfileBoundDeviceDto(string? Summary, DateTimeOffset BoundAtUtc);

/// <summary>The signed-in student's own profile (S6, contract §A.1, FR-STU-PRO-001/002/003 + FR-STU-DEV-003).
/// NO email field (Firebase identity, §C.2); NO avatar field (initials only, decision 1).</summary>
public sealed record StudentProfileDto(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string ParentPhonePrimary,
    string? ParentPhoneSecondary,
    string SchoolName,
    Guid GradeId, string? GradeName,        // read-only display
    Guid CityId, string? CityName,
    Guid RegionId, string? RegionName,
    StudentStatus Status,                    // serialized as the string name ("Active")
    ProfileBoundDeviceDto? BoundDevice);     // null when no active device
```

Manual `.ToProfileDto(this Student s, string? gradeName, string? cityName, string? regionName, StudentDevice? device)`
extension (no mapping library; never map in the handler body) — projects the active device to
`new ProfileBoundDeviceDto(device.FingerprintSummary, device.BoundAtUtc)` (or `null`). **`DeviceTokenHash` is never
touched** (§C.5).

`UpdateMyStudentProfileValidator : AbstractValidator<UpdateMyStudentProfileCommand>` (co-located, mirror the register +
staff validators):

```csharp
RuleFor(x => x.FullName).NotEmpty().WithMessage("Your name is required.").MaximumLength(200);
RuleFor(x => x.PhoneNumber).NotEmpty().WithMessage("Your phone number is required.").MaximumLength(32);
RuleFor(x => x.SchoolName).NotEmpty().WithMessage("Your school is required.").MaximumLength(200);
RuleFor(x => x.ParentPhonePrimary).NotEmpty().WithMessage("A parent/guardian phone is required.").MaximumLength(32);
RuleFor(x => x.ParentPhoneSecondary).MaximumLength(32);   // optional
RuleFor(x => x.CityId).NotEmpty().WithMessage("Select your city.");
RuleFor(x => x.RegionId).NotEmpty().WithMessage("Select your region.");
```

> The **city/region existence + region-belongs-to-city** check is a DB lookup, so it lives in the **handler** (§3.3 step
> 2) — the validator only guards `NotEmpty`. (An async validator that queries `db.Regions` is an acceptable alternative;
> either yields a **400**. Match whichever the S1 registration uses for the same pair.) `Status`/`GradeId`/email are
> **not** in the command, so nothing validates them — they are structurally un-updatable.

## 4. API — the new endpoint group (`Api/Endpoints/MeProfileEndpoints.cs`)

**New** `internal sealed class MeProfileEndpoints : IEndpointGroup` (alongside `MeCatalogueEndpoints` /
`MeSessionsEndpoints`), modelled on the staff `ProfileEndpoints` but `RequireStudent()` per route:

```csharp
internal sealed class MeProfileEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me/profile").WithTags("Profile").WithOpenApi();

        group.MapGet("", GetAsync)
            .RequireStudent()
            .WithName("GetMyStudentProfile")
            .WithSummary("The signed-in student's own profile + resolved names + active bound device")
            .Produces<StudentProfileDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapPut("", UpdateAsync)
            .RequireStudent()
            .WithName("UpdateMyStudentProfile")
            .WithSummary("Update the signed-in student's own profile (grade + email NOT changeable)")
            .Produces<StudentProfileDto>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> GetAsync(ISender sender, CancellationToken ct)
        => Results.Ok(await sender.Send(new GetMyStudentProfileQuery(), ct));

    private static async Task<IResult> UpdateAsync(
        [FromBody] UpdateMyStudentProfileRequest request, ISender sender, CancellationToken ct)
        => Results.Ok(await sender.Send(new UpdateMyStudentProfileCommand(
            request.FullName, request.PhoneNumber, request.SchoolName,
            request.CityId, request.RegionId, request.ParentPhonePrimary, request.ParentPhoneSecondary), ct));
}

/// <summary>PUT body — only the writable fields (contract §A.2). gradeId + email are NOT here.</summary>
internal sealed record UpdateMyStudentProfileRequest(
    string FullName, string PhoneNumber, string SchoolName,
    Guid CityId, Guid RegionId, string ParentPhonePrimary, string? ParentPhoneSecondary);
```

`RequireStudent()` gives the 401 (anon) / 403 (staff). The validation pipeline gives the 400. **No URL id → no IDOR
surface** (`NFR-SEC-007`): the student is the JWT subject. **No new endpoint group for any modal** — device-reset /
change-password / sign-out are client-only (§F).

## 5. Migration

**None.** No new column. `Student`, `StudentDevice`, `City`/`Region`, `Grade` all exist. **Avatar is deferred** (decision
1) — **no `Avatar` field, no DTO field, no `IFileStorage` use, no storage work, no migration**. Email is **not** stored
(decision 4) — Firebase identity. *(Recorded in §F so it isn't silently dropped.)*

## 6. Tests (`dotnet test -c Release`)

Integration (`WebApplicationFactory` + Testcontainers, **Student-role JWT** — reuse the `/api/me/*` student helper:
`factory.CreateClientForStudent(tenant, student.Id)`; seed the student with known grade/city/region + an active
`StudentDevice`; mirror the catalogue/sessions API tests). Add **`MyProfileApiTests.cs`**.

- **`GET` shape, names + bound device (§A.1, the happy path):** seed an `Active` student with a grade + city + region + an
  active device → `GET /api/me/profile` → `200 StudentProfileDto`: `id` echoes the caller, the seven personal fields
  match, `gradeName`/`cityName`/`regionName` resolve (assert the **names**, not just the ids), `status == "Active"`
  (string), and `boundDevice` carries `{ summary, boundAtUtc }` from the active `StudentDevice`. **`boundDevice` is
  `null`** when the student has no active device (separate seed). Assert the raw JSON has **no `email`** and **no
  `avatar`/`deviceTokenHash`** key (decisions 1 + 4 + §C.5).
- **`PUT` persists all seven writable fields + is audited (§A.2/§E):** `PUT` a new `fullName`/`phoneNumber`/`schoolName`/
  `cityId`/`regionId`/`parentPhonePrimary`/`parentPhoneSecondary` → `200` echoing the updates (re-resolved
  `cityName`/`regionName`), and a **new `audit_entries` row** is written for the `Student` by the interceptor
  (`ActorType == "Student"`, the diff present) — query the audit table before/after.
- **Grade NOT changeable (§C.1):** `PUT` a body with **no** `gradeId` (it isn't in the request) → `gradeId`/`gradeName`
  in the response are **unchanged** from the GET. (Optionally send a stray `gradeId` field in raw JSON and assert it is
  ignored — the request record has no such property.)
- **Email NOT stored/returned (§C.2/decision 4):** assert the GET **and** PUT response JSON contain **no `email`** key,
  and there is no way to set it (the request has no email field).
- **`400` validation (§B):** empty `fullName` → `400`; over-length `schoolName` (>200) → `400`; an **unknown** `cityId`
  → `400`; a `regionId` that exists but **does not belong** to `cityId` → `400` (assert the status; the detail is the
  FluentValidation/handler message).
- **`401` anon / `403` staff (§B):** no bearer → `401`; a **staff** JWT (`StaffRole.Teacher`) → `403` (the
  `RequireStudent` filter); Student JWT → `200`.
- **Cross-tenant isolation (`NFR-SEC-010`):** a tenant-A student JWT only ever reads/writes the tenant-A row; the global
  filter scopes `db.Students` automatically. *(There is **no URL id** to point at another tenant — IDOR is
  structurally impossible here, see below — so the isolation test is implicitly the "I get my own row, never anyone
  else's" assertion: two students in two tenants each `GET` and see only their own data.)*
- **GET NOT audited (§E):** assert **no new `audit_entries` row** for the `GET /api/me/profile` call (parity with
  `/api/me/catalogue` + `/api/me/sessions`; query the audit table before/after). Only `PUT` audits.
- **`boundDevice` never leaks the token hash (§C.5):** assert the response JSON has no `deviceTokenHash` / `tokenHash`
  key even when an active device exists.

> **IDOR not applicable.** Both routes resolve the caller **only** by `ICurrentUserResolver.UserId` — there is **no URL
> id** and no other-student path to exploit (`NFR-SEC-007`). A student can only ever read/edit **their own** row (the JWT
> subject). There is **no 404-self** (the subject always exists) and **no 409** (grade/email/status aren't writable here).
> So — unlike the S4 review read — there is no foreign-id / in-progress test; the cross-tenant test above is the relevant
> isolation guard.

## Done = ready for wiring

Contract §A/§B/§C/§E satisfied; the two new `/api/me/profile` routes mirror the staff profile pattern on the `Student`
aggregate; the **new `Student.UpdateOwnProfile`** leaves grade unchanged; email/avatar absent from both DTOs (decisions
1 + 4); device-reset / change-password / sign-out have **no** backend (decisions 2 + 3, §F); suite green (minus the
known baseline image test); **no migration**. Hand to `IMPLEMENTATION-PLAN-student-s6-wiring.md` — the **final** S6
wiring **closes the student-portal plan (S0..S6)**.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the BACKEND stream of Student-Portal phase S6 (the FINAL slice — it closes the student-portal plan
S0..S6) for Salah Bahzad (.NET 10, Clean Architecture + CQRS + source-gen Mediator). Edit backend/** ONLY. Add TWO new
student endpoints — GET + PUT /api/me/profile — mirroring the EXISTING staff ProfileEndpoints (/api/profile) but scoped
to the Student aggregate. NO new aggregate, NO migration.

Four binding user decisions (2026-06-22), all but irrelevant to this backend stream EXCEPT as exclusions:
(1) avatar = initials only — NO Avatar field, NO DTO field, NO storage, NO migration; (2) device reset = contact-support
only — NO new student endpoint; (3) password = Firebase email reset link — NO backend; (4) email = read-only Firebase
identity — NO Email column, NOT in either DTO. So this stream builds ONLY the GET + PUT profile read/update; the three
modals are client-only.

Read first, in order:
1. backend/CLAUDE.md (Multi-tenancy + EF query filters, Minimal API, Auth, Testing, Audit).
2. docs/contracts/student-s6-profile.md — the FROZEN contract: §A (the two routes + StudentProfileDto +
   UpdateMyStudentProfileRequest — seven writable fields, NO gradeId/email/avatar), §B (401/403/400, NO 404-self, NO
   409), §C (writable-vs-read-only split + the new domain method that leaves grade unchanged; email read-only-from-
   Firebase; city/region cascade + existence 400; bound-device read with token hash NEVER exposed), §E (GET not audited
   / PUT audited via the SaveChanges interceptor), §F (deferred set), §G (frozen vs stream-owned). Change the contract
   first if anything moves.
3. The templates to mirror: Api/Endpoints/ProfileEndpoints.cs + Application/Features/Profile/Queries/GetMyProfile/* +
   .../Commands/UpdateMyProfile/* (the staff GET/PUT profile pattern: resolve by ICurrentUserResolver.UserId, domain
   method, SaveChangesAsync as ITransactionalRequest → audited by the interceptor, co-located NotEmpty+MaximumLength
   validator); Api/Endpoints/MeCatalogueEndpoints.cs / MeSessionsEndpoints.cs (the /api/me IEndpointGroup + RequireStudent
   shape); Application/Features/Students/StudentDetailLoader.cs (grade IgnoreQueryFilters + city/region NameEn + active
   StudentDevice resolution — mirror for the GET names + boundDevice); Domain/Entities/Student.cs (add UpdateOwnProfile;
   do NOT reuse UpdateContactInfo — it takes gradeId + omits name/school/city/region); Domain/Entities/StudentDevice.cs
   (active = IsActive; DeviceTokenHash NEVER exposed); Features/Auth/DTOs/StudentAuthResponse.cs (the BoundDeviceInfo
   shape the boundDevice re-uses); Api/Authorization/RequireStudent.cs.

Build: NEW MeProfileEndpoints : IEndpointGroup (/api/me/profile, GET + PUT, RequireStudent); NEW GetMyStudentProfileQuery
+ handler (db.Students by currentUser.UserId, name joins grade(IgnoreQueryFilters)/city/region, active StudentDevice →
boundDevice, .ToProfileDto()); NEW UpdateMyStudentProfileCommand (ITransactionalRequest) + co-located validator + handler
(city/region existence + region-belongs-to-city → 400; Student.UpdateOwnProfile leaving Grade UNCHANGED; SaveChangesAsync
→ audited by interceptor; re-read DTO); NEW Student.UpdateOwnProfile(fullName, phoneNumber, schoolName, cityId, regionId,
parentPhonePrimary, parentPhoneSecondary) leaving GradeId unchanged; StudentProfileDto + ProfileBoundDeviceDto +
UpdateMyStudentProfileRequest + a manual .ToProfileDto(). NO email field, NO avatar field, NO device-reset endpoint, NO
password backend, NO migration. Tenant is automatic (global filter) — never write Where(x => x.TenantId == ...).

Tests (xUnit v3 + Testcontainers + FluentAssertions, Student-role JWT): GET shape incl resolved grade/city/region names
+ boundDevice (and null when no device, and NO email/avatar/deviceTokenHash key in JSON); PUT persists all seven writable
fields + writes a Student audit row (ActorType=Student) via the interceptor; grade NOT changeable + email NOT stored/
returned; 400 on empty/over-long/unknown-or-mismatched city/region; 401 anon / 403 staff; cross-tenant isolation
(NFR-SEC-010, each tenant's student sees only their own row); GET NOT audited (no new audit_entries row); boundDevice
never leaks the token hash; note IDOR is not applicable (no URL id, caller = JWT subject). Green gate:
`dotnet test -c Release` (the one pre-existing QuestionBank image test may stay red — baseline). Report it.
```
