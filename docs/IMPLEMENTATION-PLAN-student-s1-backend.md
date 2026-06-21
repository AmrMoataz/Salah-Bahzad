# Student Portal · S1 — BACKEND stream (anonymous tenant-scoped grades reference)

> Status: **Planned — not yet built** · Created 2026-06-21 · The **small engine half** of slice **S1** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S1). Registration itself (`POST /api/students/register`) and the
> city/region reference reads **already exist** (Phase 2 / Phase 1) and are **reused as-is** — frozen in
> `docs/contracts/student-s1-registration.md`. This stream adds **exactly one** new route so the anonymous wizard's
> **grade** dropdown can populate: **`GET /api/reference/grades?tenantSlug=<slug>`**.
>
> Satisfies the wizard's grade-list need behind `FR-STU-REG-004`/`FR-PLAT-TAX-005`. **No new aggregate, no migration.**
> **Change the contract (`docs/contracts/student-s1-registration.md` §B#3) first if anything moves.**
>
> Gate: `dotnet test -c Release` green (the one pre-existing `QuestionBank` image-test failure is the known baseline);
> then the **wiring** stream (`IMPLEMENTATION-PLAN-student-s1-wiring.md`) proves it live on Aspire.

---

## Design reference

This stream ships **no screen**; its one JSON shape feeds the **Student Portal** prototype's **`AUTH: REGISTER`**
wizard (Step 2 grade picker). The authority is `docs/contracts/student-s1-registration.md` §B. The response reuses the
existing `GradeDto` — the wizard reads only `id` + `name`.

---

## 1. Frozen contract (this stream)

Implements **`docs/contracts/student-s1-registration.md` §B#3** verbatim:

`GET /api/reference/grades?tenantSlug=<slug>` · `AllowAnonymous` · `200 IReadOnlyList<GradeDto>` ·
`400` (missing/blank `tenantSlug`) · `404` (unknown slug). Tenant resolved by slug; **`IgnoreQueryFilters()` +
`WHERE TenantId == tenant.Id && !IsDeleted`**, ordered by `Name`. The existing `register`, `cities`, and
`cities/{id}/regions` routes are **untouched**.

## 2. Pre-flight (confirm — do NOT rebuild)

- `ReferenceEndpoints : IEndpointGroup` (group `/api/reference`, `AllowAnonymous`) — `ListCities` +
  `ListRegionsByCity`. **The new grades route is added here**, beside them.
- `RegisterStudentHandler` — the **template** for anonymous tenant-by-slug resolution + the explicit grade filter:
  it already does `db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug)` (→ `NotFoundException("Tenant", …)`) and
  `db.Grades.IgnoreQueryFilters().AnyAsync(g => g.Id == … && g.TenantId == tenant.Id && !g.IsDeleted)`. **Reuse that
  exact tenant + grade-filter shape** — the only difference is returning the list instead of validating one id.
- Existing `ListGradesQuery`/`ListGradesHandler` (Taxonomy) **cannot be reused** — it leans on the **global tenant
  query filter** (JWT-derived) and `AsNoTracking().OrderBy(Name)`. With no JWT the filter resolves to `Guid.Empty` and
  returns nothing. The new handler must therefore scope tenant **explicitly** (above). Reuse only the `GradeDto` +
  `Grade.ToDto()` mapping.
- `IAppDbContext.Tenants` / `.Grades`; `Grade.IsDeleted`; `NotFoundException` → `404` in the existing
  exception→ProblemDetails middleware.

## 3. Application — `ListGradesForRegistration`

`Features/Reference/Grades/Queries/ListGradesForRegistration/` (a sibling of the existing `Reference/Cities` /
`Reference/Regions` query folders — it is **reference**, not taxonomy management):

- `ListGradesForRegistrationQuery(string TenantSlug) : IRequest<IReadOnlyList<GradeDto>>`.
- Co-located `ListGradesForRegistrationValidator` — `RuleFor(x => x.TenantSlug).NotEmpty()` (blank → `400`).
- `ListGradesForRegistrationHandler(IAppDbContext db)`:
  1. `slug = query.TenantSlug.Trim().ToLowerInvariant();`
  2. `tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug) ?? throw new NotFoundException("Tenant", query.TenantSlug);`
  3. `grades = await db.Grades.IgnoreQueryFilters().AsNoTracking().Where(g => g.TenantId == tenant.Id && !g.IsDeleted).OrderBy(g => g.Name).ToListAsync(ct);`
  4. `return grades.Select(g => g.ToDto()).ToList();`
- Reuse the existing `Taxonomy.DTOs.GradeDto` + `GradeMappings.ToDto()` (no duplicate DTO). The wizard reads `id`+`name`
  only; the extra timestamp fields are harmless. *(If a reviewer prefers a slim anonymous DTO, a `Reference.DTOs`
  `GradeRefDto(Id, Name)` is acceptable — but keep the wire fields `id`+`name` frozen either way.)*

## 4. API — endpoint

In `ReferenceEndpoints.Map`, beside `cities`:
```csharp
group.MapGet("/grades", ListGradesForRegistrationAsync)
    .AllowAnonymous()
    .WithName("ListGradesForRegistration")
    .WithSummary("List a tenant's grades for the sign-up form (anonymous, by tenant slug)")
    .Produces<IReadOnlyList<GradeDto>>()
    .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
    .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
```
```csharp
private static async Task<IResult> ListGradesForRegistrationAsync(
    [FromQuery] string tenantSlug, ISender sender, CancellationToken cancellationToken)
    => Results.Ok(await sender.Send(new ListGradesForRegistrationQuery(tenantSlug), cancellationToken));
```
Scalar/OpenAPI annotations as above (master plan §3.1). No rate-limit needed (parity with `cities`/`regions`, which
are unthrottled anonymous reference reads).

## 5. Migration

**None.** Grades, tenants, and the soft-delete flag all exist. Pure read.

## 6. Tests (`dotnet test -c Release`)

- **Integration (`WebApplicationFactory` + Testcontainers, anonymous — no token):**
  - seed a tenant + 2 grades (1 soft-deleted) → `GET /api/reference/grades?tenantSlug=<slug>` → `200`, **1** grade,
    ordered by name, soft-deleted **excluded**; shape is `id`+`name`.
  - **unknown slug** → `404`; **missing/blank `tenantSlug`** → `400`.
  - **tenant isolation (`NFR-SEC-010`):** tenant A's slug **never** returns tenant B's grades (seed both; assert the
    set equals A's). This is the key correctness test — the endpoint bypasses the global filter, so prove the explicit
    filter is right.
  - **anonymous reachability:** no `Authorization` header → still `200` (it is `AllowAnonymous`); a stale/garbage
    bearer is ignored.
- (No unit test needed beyond the handler's filter — the integration test covers it end-to-end. The register endpoint
  already has its Phase-2 coverage; **do not** duplicate it here.)

## Done = ready for wiring

Contract §B#3 satisfied; `register`/`cities`/`regions` untouched; suite green (minus the known baseline image test);
**no migration**. Hand to `IMPLEMENTATION-PLAN-student-s1-wiring.md`.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the BACKEND stream of Student-Portal phase S1 for Salah Bahzad (.NET 10, Clean Architecture +
CQRS + source-gen Mediator). Edit backend/** ONLY. This is a SMALL stream: add ONE anonymous reference endpoint.

Read first, in order:
1. backend/CLAUDE.md (Multi-tenancy, EF query filters, Minimal API, Testing).
2. docs/contracts/student-s1-registration.md §B#3 (the FROZEN contract for GET /api/reference/grades?tenantSlug=) and
   §0 (the anonymous tenant-by-slug + explicit-filter rule). Change the contract first if anything moves.
3. The template to mirror: Application/Features/Students/Commands/RegisterStudent/RegisterStudentHandler.cs (tenant by
   slug -> 404; Grades.IgnoreQueryFilters().Where(TenantId == tenant.Id && !IsDeleted)) and
   Api/Endpoints/ReferenceEndpoints.cs (the anonymous /api/reference group with cities + regions).

Build: a NEW Reference query ListGradesForRegistration(string TenantSlug) + validator (TenantSlug NotEmpty) + handler
(resolve tenant by slug; IgnoreQueryFilters + Where TenantId == tenant.Id && !IsDeleted; OrderBy Name; reuse the
Taxonomy GradeDto + Grade.ToDto()); wire GET /api/reference/grades?tenantSlug= (AllowAnonymous) in ReferenceEndpoints
beside cities. DO NOT reuse the Taxonomy ListGradesQuery (it relies on the JWT global filter -> empty for anonymous).
Leave register/cities/regions untouched. NO migration.

Tests (xUnit v3 + Testcontainers + FluentAssertions, anonymous): 200 + soft-deleted excluded + ordered; unknown slug
-> 404; blank tenantSlug -> 400; cross-tenant isolation (A's slug never returns B's grades); anonymous reachability.
Green gate: `dotnet test -c Release` (the one pre-existing QuestionBank image test may stay red — baseline). Report it.
```
