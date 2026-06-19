# Phase 2 — Students (review, device, history) — Implementation Plan

> Status: **Approved, ready to implement** · Scope: admin-portal Students vertical slice (DB → API → Angular), all tenant-scoped and audited.
> Source: `docs/IMPLEMENTATION-PLAN-admin-portal.md` §Phase 2. Read this file first in any fresh implementation session; `backend/CLAUDE.md` / `frontend/CLAUDE.md` auto-load.

This is a **vertical slice**: one feature end-to-end so it's independently demoable. It also introduces two cross-cutting pieces reused later: the **R2 object-storage seam** (videos/materials in Phase 3) and **device binding**.

---

## Resolved decisions (do not re-litigate)

1. **Student creation path** — build the **anonymous student self-registration endpoint** now (creates a `Pending` student + ID-image upload to R2). It's the natural producer of pending students and the only true test of the R2 upload path. It's a shared backend engine; the student-portal **frontend** stays out of scope.
2. **Device binding depth** — build only the **server-side seam + staff clear/visibility** (`FR-PLAT-DEV-001/004/006`). The consent/binding/fingerprint flow (`FR-PLAT-DEV-002/003/005`) is student-portal behavior; leave an `IDeviceBindingService` interface for later.
3. **Storage strategy** — single `IFileStorage` → `R2FileStorage` (AWS SDK S3) implementation everywhere. Environment only swaps endpoint/creds:
   - **Dev + integration tests** → **MinIO** (S3-compatible). Same code path, offline, free. Wired into the existing **Aspire AppHost**; integration tests use a MinIO Testcontainer.
   - **Staging / Production** → **Cloudflare R2**, one account, two private buckets (`sb-staging-private`, `sb-prod-private`), one least-privilege (Object Read & Write) token each.
   - No `sb-dev-private` bucket — dev needs no cloud creds.
   - Encryption at rest is automatic on R2 (satisfies `FR-PLAT-AST-003`). Buckets are private by default. DB stores **object keys only**, never bytes or public URLs (`FR-PLAT-AST-004`).
   - Config keys (env only, `NFR-SEC-002`): `R2__Endpoint`, `R2__AccessKeyId`, `R2__SecretAccessKey`, `R2__BucketPrivate`, `R2__SignedUrlTtlSeconds`.
   - VPS (Hostinger) supplies staging/prod creds via a root-owned `.env` (`chmod 600`, git-ignored), separate files per env. Aspire is **dev-time only** — the VPS runs plain containers + env → real services.

---

## Architecture & conventions

- **Clean Architecture + CQRS** (source-gen Mediator). Dependencies: Api → Application → Domain; Infrastructure implements Domain interfaces.
- Mirror the existing **Staff / Taxonomy** slices exactly: private-ctor + static-factory entities with behavior methods; handlers load → mutate → `SaveChanges` → mirror to Firebase where relevant → `.ToDto()`; `RequirePermission(...)` default-deny endpoints; automatic audit via `SaveChangesInterceptor` + domain events for semantic reasons.
- EF: global query filter on `TenantId` (no per-handler `Where`); soft-delete for audited entities; gated migrations (no auto-apply on boot); index every tenant root on `(TenantId, <natural key>)`.
- Permissions already exist: `StudentsRead/Approve/Reject/Edit/Deactivate/DeviceClear`. Tenancy + audit + JWT plumbing is in place from Phase 0/1.

---

## Steps

### Backend — Domain & persistence
1. **`Student` aggregate** (`Domain/Entities/Student.cs`, `TenantEntityBase, ISoftDeletable`) — mirrors `Staff` style.
   Fields: `FirebaseUid`, `FullName`, parent contact number(s), `GradeId` (FK), `CityId`/`RegionId` (global refs), `SchoolName`, `IdImageObjectKey` (R2 key only), `Status` (`Pending/Active/Rejected/Inactive`), `RejectionReason`, `TermsAcceptedAtUtc`/`TermsVersion`, `LastSeenAtUtc`.
   Behavior: `Approve()`, `Reject(reason)`, `Deactivate()`/`Reactivate()`, `UpdateContactInfo(gradeId, parentNumbers)`, `RecordSignIn(now)`, `SoftDelete(...)`. Raise domain events carrying the reason for audit enrichment. (`FR-STU-REG-006`, `NFR-PRIV-003`, `FR-ADM-STU-002..006`)
2. **`StudentDevice`** (`Domain/Entities/StudentDevice.cs`) — one active per student + retained history (`FR-PLAT-DEV-001/006`): `StudentId`, `DeviceTokenHash`, `FingerprintSummary`, `BoundAtUtc`, `ClearedAtUtc`/`ClearedById`/`ClearReason`, `IsActive`. Staff `Clear(actorId, reason)`.
3. **EF configs** (`Infrastructure/Persistence/Configurations/`) — one `IEntityTypeConfiguration<T>` each; global tenant filter; index `(TenantId, Status)` on Student; FK to Grade/City/Region.
4. **Migration** `AddStudentsAndDevices` — gated, not auto-applied (`NFR-AVAIL-004`).

### Backend — storage seam
5. **`IFileStorage`** (`Application/Common/Interfaces/`) → **`R2FileStorage`** (`Infrastructure/Services/`, AWS SDK S3 / `AWSSDK.S3`): `UploadPrivateAsync(key, stream, contentType, ct)`, `GetSignedReadUrlAsync(key, ttl, ct)`. Register in `InfrastructureServiceExtensions` from env config.
   - Add **MinIO** to the existing `AppHost/Program.cs` (Aspire Community Toolkit MinIO integration; package inline since AppHost has `ManagePackageVersionsCentrally=false`). Map MinIO endpoint + root creds into the API's `R2__*` env vars (same way Postgres injects its connection string). Surface the MinIO **Console** endpoint for browsing `sb-dev-private`.
   - **Dev-only** "ensure bucket exists" (Development env). Staging/prod buckets are pre-created in Cloudflare — the app never creates prod buckets.
   - Add `AWSSDK.S3` (and the MinIO toolkit pkg) to `Directory.Packages.props` / inline as appropriate.

### Backend — Application features (`Application/Features/Students/`)
6. **Queries:**
   - `ListStudents` — filter by status + grade, search, paged → `PagedResult<StudentListDto>` (`FR-ADM-STU-001`).
   - `GetStudentById` — 360° detail (`FR-ADM-STU-002`).
   - `GetStudentIdImageUrl` — issues a short-lived signed URL **and writes an explicit `AuditEntry` for the access** (`FR-PLAT-AST-003`, `NFR-PRIV-001/002`).
   - `ListStudentLoginHistory` / `ListStudentActivity` — paged, sourced from `AuditEntry` filtered by `EntityId` (`FR-ADM-STU-008`). Enrollment/transaction history tab is a placeholder until Phase 4.
7. **Commands:** `ApproveStudent`, `RejectStudent(reason)` (reason required — validator), `SetStudentActive`, `UpdateStudentContact`. Mirror `SetStaffActiveHandler`: audited automatically; reason captured into the audit `Summary` via the domain event. (`FR-ADM-STU-003..006/010`)
8. **`ClearStudentDevice(reason)`** — deactivates the active `StudentDevice`; reason required + audited (`FR-PLAT-DEV-004`).
9. **`StudentEndpoints.cs`** (`Api/Endpoints/`) — follow `StaffEndpoints` shape; `.RequirePermission(Permission.Students*)`, default-deny; OpenAPI metadata. Register the group.
10. **Anonymous student self-registration endpoint** — creates a `Pending` student, uploads the ID image to R2 (private), records terms acceptance. Anonymous (allow-anonymous, rate-limited). Backend engine only; no student-portal UI. Validates file type/size, stores to R2 (never disk).

### Frontend — `libs/admin-portal/feature-students`
11. **Data-access** (`student.models.ts`, `student.service.ts`) + route in `apps/admin-portal/src/app/app.routes.ts` (`/students`) + sidebar entry. Use typed client / `httpResource`.
12. **Students list** — filter (status/grade) + search + status pills + row actions; reuse shared `table`, `status-pill`, `select`, `empty-state` (`FR-ADM-STU-001`).
13. **Approvals queue** — pending cards/list with inline approve / reject-with-reason (reuse `confirm-dialog` + reason input) (`FR-ADM-STU-003..004`).
14. **Student detail** — profile, **ID-image panel** (signed-URL fetch on demand), **device panel** (bound device + binding date + clear-with-reason), **history tabs** (logins / activity now; enrollments / attendance render empty-state placeholders for Phase 4/5) (`FR-ADM-STU-002/006/007/008`, `FR-PLAT-DEV-006`).

### Verify
- **Unit tests:** Student lifecycle rules, reject-requires-reason, device clear, contact update.
- **Integration tests:** tenant isolation (`NFR-SEC-010`), permission default-deny, full pending→active flow, **ID-image access writes an audit row**, registration→Pending, R2 path via a **MinIO Testcontainer** fixture.
- **Gates:** `dotnet build -c Release && dotnet test`; `npx nx build admin-portal`.
- Before PR: `/code-review` then `/dotnet-claude-kit:security-scan` (minors' PII).

---

## Exit criteria (`FR`/`NFR` traceable)
- Full **pending → active** lifecycle works, with mandatory reasons on reject (`FR-ADM-STU-003/004`).
- **Device clear** works, audited with reason (`FR-PLAT-DEV-004`).
- **ID-image views are audited**; image served only via short-lived signed URL (`FR-PLAT-AST-003`).
- Tenant isolation + permission default-deny proven by tests (`NFR-SEC-010`).
- Build + tests green (backend and frontend).

---

## Risks
- **AWSSDK.S3 / MinIO toolkit** are new deps — presigning TTL/clock-skew need care; mitigated by `IFileStorage` abstraction + MinIO Testcontainer test.
- **PII** (minors' ID images, `NFR-PRIV-001/002`): never log keys/URLs; audit every access; short-lived signed URLs only.
- **Audit reason capture**: the interceptor auto-diffs fields but "reason" is semantic — carry it via a domain event into the audit `Summary` (same pattern Staff will reuse).

---

## Suggested session split (token hygiene)
- **Backend A1** — Steps 1–5 (domain + storage seam). Commit after domain, again after storage.
- **Backend A2** — Steps 6–10 (Application + API + registration). Commit after passing integration tests. Confirm Scalar/OpenAPI reflects new endpoints.
- **Frontend B** — Steps 11–14, after the endpoints exist (reads the OpenAPI contract).

Each chunk: point sessions at this file, mirror existing slices, commit at seams, `/clear` between chunks.
