# FROZEN CONTRACT — Student Portal · S1 · Registration & onboarding

> Status: **Frozen** · Created 2026-06-21 · Slice: Student-Portal **S1** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S1). **Design anchor:** the prototype's
> **`AUTH: REGISTER`** section (`.claude/Salah Bahzad Student Portal/Student Portal.html`) — the two-step wizard +
> its **success/pending** and **rejected** terminal states. The authority for behaviour is
> `FR-STU-REG-001..009`, `FR-PLAT-AUTH-003` (Google social sign-up), `NFR-PRIV-001/003` (minors' PII).
>
> Satisfies: anonymous self-registration `FR-STU-REG-001..008`; readable pending/rejection state `FR-STU-REG-009`;
> Google + email/password sign-up `FR-PLAT-AUTH-003`. **Mostly already built** — the register engine shipped in
> Phase 2. This contract **documents the existing surface as frozen** and adds **one** new anonymous read
> (`GET /api/reference/grades`) so the wizard's grade dropdown can populate. Frontend + wiring cite this file
> field-for-field. **Change this file first if anything moves.**

## 0. Ground rules

- **Frontend-led slice.** The wizard is the deliverable; the backend is **the existing `POST /api/students/register`
  (reused as-is, frozen here) plus one small new reference read**. No new aggregate, no migration.
- **Anonymous surface.** Registration and all three reference reads are `AllowAnonymous`. The register POST is
  `RequireRateLimiting("auth")` (the shared fixed-window 10/min bucket, config-overridable). The wizard has **no
  platform JWT** — identity is the **Firebase ID token** the form carries (`FR-STU-REG-002`).
- **Tenant by slug, not by JWT.** Anonymous callers have no tenant claim, so tenant comes from a **`tenantSlug`**
  the client supplies (the register form field today; a `?tenantSlug=` query for the new grades read). Tenant-owned
  reads (grades) **must `IgnoreQueryFilters()` + filter `TenantId == tenant.Id` explicitly** — the EF global filter
  resolves to `Guid.Empty` with no JWT and would return nothing. Cities/regions are **global** reference data (no
  tenant) and stay as-is. (`§B`)
- **No new status endpoint.** The pending/rejected screens are driven by (a) the register `201` result's
  `status:"Pending"` immediately after submit, and (b) **S0's** `POST /api/auth/student/exchange` returning
  `403 { reason }` (`account_pending` | `account_rejected` + the stored `RejectionReason` | `account_inactive`) when
  a not-yet-approved student later tries to sign in. S1 adds **no** read for this. (`§C`)
- **PII discipline (`NFR-PRIV-001/003`).** The ID image is a minor's PII: validated (`jpeg|png|webp`, **≤ 5 MB**),
  uploaded to the **private** bucket, never to disk, never returned to the student. Already enforced by the handler.

## A. Register — `POST /api/students/register` (EXISTS — frozen as-is)

`AllowAnonymous` · `RequireRateLimiting("auth")` · `DisableAntiforgery()` · **`multipart/form-data`** · returns
`201 Created` `StudentRegistrationResultDto`. **No change in S1** — documented so frontend builds to it exactly.

### A.1 Form fields (multipart)

| Field | Type | Required | Notes |
|---|---|---|---|
| `firebaseIdToken` | string | ✓ | The Firebase ID token of the just-created account (email/pw **or** Google). Server verifies it (`FR-STU-REG-002`). |
| `tenantSlug` | string | ✓ | Lower-cased + trimmed server-side; resolves the tenant (`404` if unknown). Client value = the configured tenant slug (`§F`). |
| `fullName` | string | ✓ | ≤ 200. |
| `phoneNumber` | string | ✓ | The student's own phone. ≤ 32. |
| `parentPhonePrimary` | string | ✓ | First parent/guardian phone (**≥ 1 parent phone required**). ≤ 32. |
| `parentPhoneSecondary` | string | — | Optional second parent phone. ≤ 32. |
| `gradeId` | guid | ✓ | Must be a non-deleted grade **of this tenant** (`404 Grade` otherwise). Populated from `§B` grades. |
| `cityId` | guid | ✓ | Global city (`404 City` otherwise). From `§B` cities. |
| `regionId` | guid | ✓ | Must belong to `cityId` (`404 Region` otherwise). From `§B` regions. |
| `schoolName` | string | ✓ | ≤ 200. |
| `termsVersion` | string | ✓ | The accepted terms version string (`§F`); recorded as a `TermsAcceptance` consent. ≤ 50. |
| `idImage` | file | ✓ | `image/jpeg` \| `image/png` \| `image/webp`, **> 0 and ≤ 5 MB**. |

> The OpenAPI shape is the existing `RegisterStudentForm` record; the actual binding is `[FromForm]` parameters +
> `IFormFile idImage` (the file part is bound separately). **Field names are frozen** — send them verbatim.

### A.2 Result — `StudentRegistrationResultDto`

```jsonc
// 201 Created · Location: /api/students/{studentId}
{ "studentId": "<guid>", "status": "Pending" }   // StudentStatus enum, serialized as the string "Pending"
```

### A.3 Error modes (frozen) — ProblemDetails

| Status | When |
|---|---|
| `400` | FluentValidation failure (any `§A.1`/`§D` rule) **or** an invalid/expired Firebase ID token. |
| `404` | `tenantSlug` unknown, or `gradeId`/`cityId`/`regionId` not found / region not in city. |
| `409` | A **live** account (Pending/Active/Inactive) already exists for this Firebase UID in this tenant (`"An account already exists for this sign-in."`). A **Rejected** prior registration does **not** 409 — it is re-submitted (see `§A.4`). |
| `429` | `auth` rate-limit tripped. |

### A.4 Server behaviour (for reference — already implemented, do not rebuild)

Verify Firebase → resolve tenant by slug → validate grade (tenant-scoped) / city / region → look up any existing
student for this Firebase UID:
- **none** → `Student.Register(...)` (creates **Pending** student + records the terms consent), audit `StudentRegistered`;
- **Rejected** → `Student.Resubmit(...)` re-uses the same row: overwrites the editable details, clears
  `RejectionReason`, moves it back to **Pending**, audit **`StudentResubmitted`**. So a rejected registration is
  never a dead-end for that email — the student keeps the **same Firebase account** and re-submits (FR-ADM-STU-004
  follow-up);
- **live** (Pending/Active/Inactive) → `409`.

Then upload the (fresh) ID image to private R2 → save (transactional) → write the audit row (`§E`) → return `201`
with `status:"Pending"` in both the register and resubmit cases.

## B. Reference data for the wizard dropdowns (all `AllowAnonymous`)

| # | Method & path | State | Returns | Notes |
|---|---|---|---|---|
| 1 | `GET /api/reference/cities` | **EXISTS** | `IReadOnlyList<CityDto>` | Global; `CityDto(Id, NameEn, NameAr)`. Ordered by `NameEn`. |
| 2 | `GET /api/reference/cities/{cityId}/regions` | **EXISTS** | `IReadOnlyList<RegionDto>` | Global; `RegionDto(Id, CityId, NameEn, NameAr)`. The city→region cascade. |
| 3 | **`GET /api/reference/grades?tenantSlug=<slug>`** | **NEW (S1)** | `IReadOnlyList<GradeDto>` | **Tenant-scoped** grades for the wizard. Resolve tenant by slug (**`404 Tenant`** if unknown); **`IgnoreQueryFilters()` + `WHERE TenantId == tenant.Id && !IsDeleted`**; ordered by `Name`. |

```jsonc
// GET /api/reference/grades?tenantSlug=salah-bahzad  →  200
[ { "id": "<guid>", "name": "Grade 1 Secondary" }, ... ]
// Reuses the existing Taxonomy GradeDto(Id, Name, CreatedAtUtc, UpdatedAtUtc); the wizard reads only id + name.
```

**Frozen for #3:** the path + `tenantSlug` query param; `AllowAnonymous`; tenant-by-slug resolution with explicit
tenant filter (not the global filter); `404` when the slug is unknown; the `id`/`name` fields the wizard reads.
A missing/blank `tenantSlug` → `400`.

> **Why a new endpoint and not `/api/taxonomy/grades`:** grades are **tenant-owned**, and the taxonomy route is
> `RequirePermission(TaxonomyRead)` (staff-only) — an anonymous wizard cannot call it, and it derives tenant from the
> staff JWT. The master plan's baseline table assumed grades were already anonymous like cities/regions; they are not.
> This reference read closes that single gap (user-confirmed 2026-06-21, "Add `/api/reference/grades?tenantSlug=`").

## C. Status read — **no new endpoint** (driven by what S0 already shipped)

- **Immediately after submit:** the `201` result carries `status:"Pending"` → render the **pending** success state.
- **On a later sign-in attempt by a not-yet-approved student:** S0's `POST /api/auth/student/exchange` returns
  **`403 { reason }`** — `account_pending` (readable detail), `account_rejected` (detail = the stored
  `RejectionReason`, `FR-STU-REG-009`), or `account_inactive`. The S1 frontend reuses S0's `StudentAuthStore.status()`
  signal + status screens to render these. **S1 adds no status query.**

## D. Validation rules (frozen — from `RegisterStudentValidator`)

`fullName` ≤ 200 · `phoneNumber` required, ≤ 32 · `parentPhonePrimary` required, ≤ 32 · `parentPhoneSecondary`
≤ 32 · `gradeId`/`cityId`/`regionId` required (non-empty Guid) · `schoolName` required, ≤ 200 · `termsVersion`
required, ≤ 50 · `idImage` content-type ∈ {`image/jpeg`,`image/png`,`image/webp`} · `idImage` length **> 0 and
≤ 5 MB**. The frontend mirrors these for inline UX but the **server is authoritative** (`NFR-SEC`); surface the
server's `400` field messages.

## E. Audit (`FR-PLAT-AUD-002`)

- **`StudentRegistered`** — one row per successful registration. `EntityType=Student`, `EntityId=studentId`,
  **`ActorType=Student`**, `Portal=student`, summary "Student self-registered (pending review)." Written via
  `IAuditWriter` (the request is anonymous, so the interceptor is a no-op — same pattern as the S0 exchange). **Already
  implemented.**
- **`StudentResubmitted`** — one row when a **Rejected** student re-submits (the reused-row path in `§A.4`). Same
  `EntityType`/`EntityId`/`ActorType=Student`/`Portal=student`; summary "Rejected student re-submitted registration
  (pending review)." Written the same explicit way. The original `StudentRejected` row stays, so the
  rejected→fixed→pending trail is preserved.
- The reference reads (`§B`) are **pure reads — not audited** (consistent with cities/regions and the catalogue reads).

## F. Client-supplied constants (frontend provisioning)

- **`tenantSlug`** — single-tenant today, so the wizard sends a **configured constant**: `environment.tenantSlug`,
  runtime-overridable via **`window.__SB_TENANT__`** exactly like `window.__SB_API_URL__` (S0). Sent as the register
  form field **and** the `?tenantSlug=` on `§B#3`. (Multi-tenant host→slug resolution is a later concern; out of S1.)
- **`termsVersion`** — a frontend constant (e.g. `environment.termsVersion`, default `"v1"`). The terms step's
  consent checkbox(es) — including the **one-device-policy** acknowledgement (the device-binding consent S0 deferred
  to registration, master plan §S0/F4) — gate the submit button and set the value sent.

## G. Frozen vs. stream-owned

- **Frozen (this file):** the `POST /api/students/register` field names/types + `StudentRegistrationResultDto` +
  the four error statuses (`§A`); the three reference routes incl. the **new** `GET /api/reference/grades?tenantSlug=`
  shape, anonymity, tenant-by-slug + explicit-filter rule, and its `404`/`400` (`§B`); the validation rules (`§D`);
  the `StudentRegistered` audit row (`§E`); "no new status endpoint" (`§C`); `tenantSlug`/`termsVersion` as client
  constants (`§F`).
- **Backend owns:** the new query's folder/name (`Features/Reference/Grades/Queries/ListGradesForRegistration`),
  whether to reuse `GradeDto` vs a slim reference DTO (must keep `id`+`name`), the endpoint wiring in
  `ReferenceEndpoints`, and the integration tests.
- **Frontend owns:** the wizard's two-step UX, Firebase account creation (email/pw **and** Google), inline validation,
  the ID-image picker + client-side size/type guard, the success/pending + rejected renders, and the Jest specs.
- **Wiring owns:** proving the whole flow live on the Aspire stack — grades-by-slug populates, a multipart submit
  creates a `Pending` student with the ID image in private R2 + the `StudentRegistered` audit row, every error
  (`400`/`404`/`409`/`429`) renders, and the pending/rejected states reconcile with S0's exchange `403`.
