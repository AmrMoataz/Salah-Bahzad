# Student Portal · S2 — BACKEND stream (the catalogue read)

> Status: **Planned — not yet built** · Created 2026-06-21 · The **engine half** of slice **S2** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S2). The **redeem** path (`POST /api/enrollments/redeem`) and its whole
> side-effect cycle (counters, payment, attendance shell, `EnrollmentCreated` → assignment/quiz snapshots, prerequisite
> + one-active gates) **already exist** (Phase 4 + the 5B engines) and are **reused verbatim** — frozen in
> `docs/contracts/student-s2-catalogue-enroll.md` §B. This stream adds **exactly one** new student read:
> **`GET /api/me/catalogue`**.
>
> Satisfies discovery `FR-STU-CAT-001/002` + the publish-to-catalogue surface `FR-PLAT-SES-008`. **No new aggregate, no
> migration.** **Change the contract (`docs/contracts/student-s2-catalogue-enroll.md` §A) first if anything moves.**
>
> Gate: `dotnet test -c Release` green (the one pre-existing `QuestionBank` image-test failure is the known baseline);
> then the **wiring** stream (`IMPLEMENTATION-PLAN-student-s2-wiring.md`) proves it live on Aspire.

---

## Design reference

This stream ships **no screen**; its one JSON shape feeds the **Student Portal** prototype's **`CATALOGUE`** cards grid
(`SessionThumb` card: thumbnail, title, grade/subject/spec, price, prerequisite badge, Enroll/Open CTA). The authority is
`docs/contracts/student-s2-catalogue-enroll.md` §A + the per-caller rules in §C.

---

## 1. Frozen contract (this stream)

Implements **`docs/contracts/student-s2-catalogue-enroll.md` §A** verbatim:

`GET /api/me/catalogue` · `RequireStudent` · `200 IReadOnlyList<CatalogueSessionDto>` · optional `gradeId? subjectId?
specializationId? search?` filters · **published-only**, tenant + soft-delete auto-scoped, ordered `CreatedAtUtc` DESC,
**not paginated**. Each row carries display fields + a signed `thumbnailUrl` + the **prerequisite** badge/satisfied flag
(§C.2) + the **caller's** `enrollmentState`/`enrolledExpiresAtUtc` (§C.1). The redeem route is **untouched**.

## 2. Pre-flight (confirm — do NOT rebuild)

- **`POST /api/enrollments/redeem`** (`EnrollmentEndpoints.RedeemAsync` → `RedeemCodeCommand`/`RedeemCodeHandler`) — the
  whole redeem engine + `EnrollmentWorkflow.EnrollOrExtendAsync` (prerequisite gate, one-active 409, counters/payment/
  attendance, `EnrollmentCreated`). **Reused as-is.** This stream does **not** edit it.
- **`ListSessionsHandler`** (`Features/Sessions/Queries/ListSessions`) — **the template** for the catalogue's
  name-resolution joins: it already resolves grade/subject/specialization names with `IgnoreQueryFilters()` (so a
  soft-deleted taxonomy row still shows its label) and computes per-session `videoCount`/`enrolledCount` via grouped
  sub-queries. **Mirror that shape** — the catalogue differs only in: `Status == Published` filter, **no pagination**,
  the extra **prerequisite** + **per-caller enrollment** projections, and the signed `thumbnailUrl`.
- **`EnforcePrerequisiteGateAsync`** (`EnrollmentWorkflow`) — copy its predicate **exactly** for `prerequisiteSatisfied`
  (§C.2): no prereq → true; prereq has no questions → true; else `UserAssignments.Any(Completed)`.
- **`SessionDetailLoader` / `GetSessionByIdHandler`** — the `IFileStorage` signed-URL pattern for `thumbnailUrl`
  (short-lived). Reuse the same call for the catalogue thumbnail; `null` key → `null` url.
- **`ICurrentUserResolver`** — `.UserId` is the student id, `.TenantId` the tenant (exactly as `RedeemCodeHandler` and
  `GetMyAssignmentHandler` use them). `TimeProvider clock` for `now` in the expiry derivation (§C.1).
- **`RequireStudentExtensions.RequireStudent()`** (`Api/Authorization/RequireStudent.cs`) — the endpoint filter (anon →
  401, staff → 403). The catalogue endpoint uses it, like `/api/me/assignments`.

## 3. Application — `ListCatalogue`

`Features/Sessions/Queries/ListCatalogue/` (a sibling of `ListSessions` — it is a session read; keep it next to the
admin one so the shared name-resolution helper is obvious):

- `ListCatalogueQuery(Guid? GradeId, Guid? SubjectId, Guid? SpecializationId, string? Search) : IRequest<IReadOnlyList<CatalogueSessionDto>>`.
- **No validator needed** (all params optional; an empty/garbage filter simply matches nothing). *(If a reviewer wants a
  `search` max-length guard, a co-located validator is fine — keep the params frozen.)*
- `ListCatalogueHandler(IAppDbContext db, ICurrentUserResolver currentUser, IFileStorage fileStorage, TimeProvider clock)`:
  1. `var studentId = currentUser.UserId; var now = clock.GetUtcNow();`
  2. Base query: `db.Sessions.AsNoTracking().Where(s => s.Status == SessionStatus.Published)` (tenant + soft-delete are
     the global filter). Apply the optional filters mirroring `ListSessionsHandler` (grade direct; subject via
     `db.Specializations.Any(sp => sp.Id == s.SpecializationId && sp.SubjectId == subjectId)`; specialization direct;
     `search` → `Title.ToLower().Contains(term)`). `OrderByDescending(s => s.CreatedAtUtc).ToListAsync()`.
  3. Resolve **grade/subject/specialization names** exactly like `ListSessionsHandler` (`IgnoreQueryFilters` on the name
     dictionaries only). Compute **`videoCount`** per session (grouped `db.SessionVideos`).
  4. **Prerequisite titles:** for the distinct non-null `PrerequisiteSessionId`s, `IgnoreQueryFilters` a
     `{ Id → Title }` dictionary (a prereq may be archived/soft-deleted yet still gate — keep its label).
  5. **`prerequisiteSatisfied`:** for the prereq set, batch two facts — which prereqs **have questions**
     (`db.Questions.Where(q => prereqIds.Contains(q.SessionId)).Select(q => q.SessionId).Distinct()`) and which the
     **caller has completed** (`db.UserAssignments.Where(a => a.StudentId == studentId && prereqIds.Contains(a.SessionId)
     && a.Status == AssignmentStatus.Completed).Select(a => a.SessionId)`). Then per session: no prereq → true; prereq
     **not** in the has-questions set → true; else prereq **in** the completed set. *(Mirror `EnforcePrerequisiteGateAsync`.)*
  6. **Per-caller enrollment:** `db.Enrollments.Where(e => e.StudentId == studentId && sessionIds.Contains(e.SessionId))`
     → at most one per session (`FR-PLAT-ENR-006`); to be safe take the latest by `EnrolledAtUtc`. Derive
     `enrollmentState`/`enrolledExpiresAtUtc` per **§C.1** (Active+unexpired → `Enrolled`+expiry; Active+`ExpiresAtUtc <= now`
     → `Expired`; `Refunded` → `Refunded`; none → `NotEnrolled`). **Do not** read `Status == Expired` — derive it.
  7. **`thumbnailUrl`:** for each session with a `ThumbnailObjectKey`, issue a short-lived signed URL via `fileStorage`
     (same call `SessionDetailLoader` uses); null key → null. *(Batch/await as the loader does; keep it simple — the
     published set is small.)*
  8. `return sessions.Select(s => s.ToCatalogueDto(…)).ToList();`
- **`CatalogueSessionDto`** + **`SessionMappings.ToCatalogueDto(this Session s, …)`** in
  `Features/Sessions/DTOs/SessionDtos.cs` (beside `SessionListDto`/`ToListDto`) — the field order is the contract §A.2
  shape. Manual mapping, no library.

## 4. API — endpoint

New `MeCatalogueEndpoints : IEndpointGroup` (mirrors `AssignmentEndpoints`/`EnrollmentEndpoints` — an `IEndpointGroup`
auto-discovered by the existing registration):
```csharp
var group = app.MapGroup("/api/me/catalogue").WithTags("Catalogue").WithOpenApi();

group.MapGet("/", ListCatalogueAsync)
    .RequireStudent()
    .WithName("ListCatalogue")
    .WithSummary("Browse the tenant's published sessions with the caller's enrollment + prerequisite state")
    .Produces<IReadOnlyList<CatalogueSessionDto>>();
```
```csharp
private static async Task<IResult> ListCatalogueAsync(
    ISender sender, CancellationToken cancellationToken,
    [FromQuery] Guid? gradeId = null, [FromQuery] Guid? subjectId = null,
    [FromQuery] Guid? specializationId = null, [FromQuery] string? search = null)
    => Results.Ok(await sender.Send(
        new ListCatalogueQuery(gradeId, subjectId, specializationId, search), cancellationToken));
```
Scalar/OpenAPI annotations as above (master plan §3.1). `RequireStudent()` gives the 401/403; no other `.Produces` needed
(the read can't 404 — an unknown filter just yields `[]`).

## 5. Migration

**None.** `Session`, `Enrollment`, `Question`, `UserAssignment`, `Specialization`, `Subject`, `Grade` all exist. Pure read.

## 6. Tests (`dotnet test -c Release`)

Integration (`WebApplicationFactory` + Testcontainers, **Student-role JWT** — reuse the redeem/assignment tests' student
principal helper):

- **Published-only + shape:** seed a tenant with `Draft` + `Published` + `Archived` sessions → `GET /api/me/catalogue`
  returns **only** the `Published` ones, DESC by `CreatedAtUtc`, with grade/subject/specialization names, `videoCount`,
  `price`, `validityDays`, and a `thumbnailUrl` when a thumbnail key exists (null otherwise).
- **Filters:** `?gradeId=`, `?subjectId=` (via specialization), `?specializationId=`, `?search=` each narrow the set;
  combined filters intersect; a no-match filter → `[]` (200).
- **`enrollmentState` (§C.1) — the key projection:** for one session, assert all four states from real rows: no
  enrollment → `NotEnrolled`; an `Active` future-expiry enrollment → `Enrolled` + `enrolledExpiresAtUtc` set; an `Active`
  row with `ExpiresAtUtc` in the **past** → `Expired` (proves it's **derived**, not `Status`-read); a `Refunded` row →
  `Refunded`. A `ValidityDays == 0` session → `Enrolled` with `enrolledExpiresAtUtc == null`.
- **`prerequisiteSatisfied` (§C.2):** session with no prereq → true; prereq **with** questions, caller **not** completed
  → **false** (+ `prerequisiteTitle` populated); same prereq after the caller completes its assignment → **true**;
  prereq **without** questions → true (vacuous). *(This is the FR-STU-CAT-002 gate the card reads — prove it matches the
  redeem gate.)*
- **IDOR / per-caller scoping (`NFR-SEC-007`):** student A's catalogue reflects **A's** enrollment/completion state, not
  B's (seed both enrolled in the same session with different states; assert each caller sees their own).
- **Tenant isolation (`NFR-SEC-010`):** a student of tenant A never sees tenant B's published sessions (seed both;
  assert the returned set ⊆ A's).
- **Auth gating:** anonymous → `401`; a **staff** JWT → `403` (the `RequireStudent` filter); a Student JWT → `200`.

## Done = ready for wiring

Contract §A satisfied; redeem untouched; suite green (minus the known baseline image test); **no migration**. Hand to
`IMPLEMENTATION-PLAN-student-s2-wiring.md`.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the BACKEND stream of Student-Portal phase S2 for Salah Bahzad (.NET 10, Clean Architecture +
CQRS + source-gen Mediator). Edit backend/** ONLY. This is a SMALL stream: add ONE student read endpoint. The redeem
engine (POST /api/enrollments/redeem) already exists and is REUSED AS-IS — do not touch it.

Read first, in order:
1. backend/CLAUDE.md (Multi-tenancy, EF query filters, Minimal API, Testing).
2. docs/contracts/student-s2-catalogue-enroll.md — the FROZEN contract: §A (GET /api/me/catalogue: params, the
   CatalogueSessionDto shape, published-only, flat list), §C (the enrollmentState derivation + the prerequisiteSatisfied
   predicate), §B (redeem — already built, reused). Change the contract first if anything moves.
3. The templates to mirror: Application/Features/Sessions/Queries/ListSessions/ListSessionsHandler.cs (name-resolution
   joins with IgnoreQueryFilters + grouped video counts), Application/Features/Enrollment/EnrollmentWorkflow.cs
   (EnforcePrerequisiteGateAsync — copy the predicate), Application/Features/Sessions/SessionDetailLoader.cs (IFileStorage
   thumbnail signed URL), Api/Endpoints/AssignmentEndpoints.cs (the /api/me/* + RequireStudent endpoint-group shape),
   and ICurrentUserResolver (.UserId = studentId, .TenantId).

Build: a NEW ListCatalogue query/handler under Features/Sessions/Queries/ListCatalogue (published-only,
ICurrentUserResolver.UserId for the caller, TimeProvider for now); the CatalogueSessionDto + SessionMappings.ToCatalogueDto
beside SessionListDto; derive enrollmentState (Active+unexpired=Enrolled, Active+past-expiry=Expired [DERIVED, not
Status==Expired], Refunded=Refunded, none=NotEnrolled) and prerequisiteSatisfied (mirror EnforcePrerequisiteGateAsync);
short-lived signed thumbnailUrl. Wire GET /api/me/catalogue (RequireStudent) in a new MeCatalogueEndpoints : IEndpointGroup.
DO NOT paginate; DO NOT touch redeem. NO migration.

Tests (xUnit v3 + Testcontainers + FluentAssertions, Student-role JWT): published-only + shape + DESC order; each filter
narrows; all four enrollmentState values incl. the past-expiry-derives-Expired and validityDays==0 cases;
prerequisiteSatisfied true/false/vacuous matching the redeem gate; IDOR per-caller scoping; cross-tenant isolation;
401 anon / 403 staff / 200 student. Green gate: `dotnet test -c Release` (the one pre-existing QuestionBank image test
may stay red — baseline). Report it.
```
