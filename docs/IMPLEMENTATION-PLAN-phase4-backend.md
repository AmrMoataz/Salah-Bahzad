# Phase 4 — BACKEND stream (Enrollment, codes & payments seam)

> Run this in its **own** Claude session, in parallel with the frontend stream. Created 2026-06-20.
>
> **Read first:** `backend/CLAUDE.md` (conventions, domain model, business rules) and the
> **frozen contract** `docs/contracts/phase4-codes-enrollment.md` (the API shape you must produce).
> **Template to mirror:** the Phase 3 Sessions slice (`Features/Sessions`, `Features/Questions`) — copy its structure.
>
> **File ownership (do not cross):** this stream edits **`backend/**` only**. Do not touch anything under
> `frontend/`. The only coupling to the frontend is the frozen contract — match it field-for-field.

## Goal
Author the Code + Enrollment + Payment aggregates end-to-end (Domain → Application → Infrastructure → Api → tests),
exposing exactly the endpoints in the frozen contract. Green gate: `dotnet build -c Release` +
`dotnet test -c Release` (needs Docker for Postgres Testcontainers).

## Architecture & established seams (reuse, don't reinvent)
- **Clean Architecture + CQRS**, source-gen Mediator (`IRequest`/`IRequestHandler`, `INotification`).
- **Entity bases:** `TenantEntityBase` (TenantId + `SetTenant`), `EntityBase` (Id/Created/Updated), `ISoftDeletable`
  (`Domain/Common/`). Copy the `Session` aggregate’s factory + guarded-mutator + domain-event style verbatim.
- **Permissions ALREADY EXIST AND ARE BUNDLED** — `CodesRead/Generate/Disable/Delete` (300–303),
  `EnrollmentsRead/Unlock/Refund` (400–402) in `Domain/Enums/Permission.cs`, mapped to roles in
  `Application/Features/Auth/PermissionCatalog.cs`. **One change:** add `Permission.EnrollmentsRefund` to
  `AssistantPermissions` (the README matrix + prototype grant Assistants refund; the forward-declared bundle omitted it —
  contract §5). No other auth change for staff endpoints.
- **Audit (first-class — `FR-PLAT-AUD-002` names every Phase-4 action explicitly).** Pick the right mechanism per action:
  1. `AuditSaveChangesInterceptor` auto-captures field-diffs on **writes** for free (status flips, soft-delete) — no work.
  2. `IAuditableDomainEvent` on the domain events gives **semantic, readable summaries**: `CodeBatchGenerated`,
     `CodeDisabled/Enabled/Deleted`, `CodeRedeemed` ("student X redeemed `SB-…` for ‘Session’"), `EnrollmentCreated`
     (distinguish **redeem** vs **unlock** from `Method`), `EnrollmentRefunded` ("staff Y refunded X, returned `SB-…`").
  3. **`IAuditWriter` explicit entries for reads that must be audited.** ⚠️ **CSV export (#3/#4) is a `GET` and never
     reaches `SaveChanges`, so the interceptor will NOT capture it** — write it explicitly (mirror how Phase 2 audits the
     ID-image view read). Same for any other read FR-PLAT-AUD-002 calls out.
  Actor comes from the JWT context the interceptor already reads: **student** for redeem (#12), **staff** for everything
  else; `System` is reserved for Phase 5’s automated side-effects (`FR-PLAT-AUD-005`). Every entry stays append-only +
  hash-chained + tenant-stamped (existing infra). Net: **generate, export, disable, enable, delete, redeem, unlock,
  refund each leave exactly one audit entry** — assert this in A9.
- **Pipeline:** `ITransactionalRequest` marker → `TransactionBehavior`; `ValidationBehavior` (FluentValidation).
- **Endpoints:** `IEndpointGroup.Map(...)` auto-discovered; `MapGroup("/api/…")` + `RequirePermission(...)` +
  `.WithName/.WithSummary/.Produces<>` (see `StudentEndpoints`/`SessionEndpoints`).
- **Read models:** `PagedResult<T>`; manual `.ToDto()` static extensions co-located with the feature.

## Steps

### A1 — Domain: Code + CodeBatch aggregates
`Domain/Entities/CodeBatch.cs`, `Code.cs` (`: TenantEntityBase, ISoftDeletable`); `Domain/Enums/CodeStatus.cs`
(`Active | Inactive | Used`); events in `Domain/Events/` (`CodeBatchGenerated`, `CodeDisabled`, `CodeEnabled`,
`CodeDeleted`, `CodeRedeemed`).
- `CodeBatch` (provenance root, immutable after mint): `Label`, `SessionId`, `Value`, `Quantity`, created metadata.
  Factory `Generate(tenantId, sessionId, sessionPriceDefault, value, quantity, label)` → also produces its `Code`
  children (same tx). Raise `CodeBatchGeneratedEvent`.
- `Code` (lifecycle root): `Serial` (unique per tenant), `BatchId`, `SessionId` (denormalized for the register filter),
  `Value`, `Status` (default `Active`), redemption join (`RedeemedByStudentId?`, `RedeemedEnrollmentId?`,
  `RedeemedAtUtc?`). Methods: `Disable()`/`Enable()` (guard **not `Used`** → else throw → 409), `MarkRedeemed(studentId,
  enrollmentId, now)` (guard `Active` + value match handled by the redeem handler), `ReturnAfterRefund()`
  (`Used → Active`, clears join), `SoftDelete(...)` (guard not `Used`). Serial generation = Crockford base32
  `SB-XXXXX-XXXXX` helper (collision-retry against the tenant).
- Unit-test every guard (mirror `tests/SalahBahazad.UnitTests/Domain/SessionTests.cs`).

### A2 — Domain: Enrollment aggregate (+ Payment seam)
`Domain/Entities/Enrollment.cs` (`: TenantEntityBase, ISoftDeletable`), `EnrollmentVideoAccess.cs` (child),
`PaymentTransaction.cs` (child or sibling — owned by the enrollment); `Domain/Enums/EnrollmentStatus.cs`
(`Active | Expired | Refunded`), `EnrollmentMethod.cs` (`Code | Unlock`), `PaymentMethod.cs`, `PaymentStatus.cs`;
events `EnrollmentCreatedEvent`, `EnrollmentRefundedEvent`, `EnrollmentExtendedEvent`.
- `Enrollment`: `StudentId`, `SessionId`, `Status`, `Method`, `CodeId?`, `Amount`, `EnrolledAtUtc`, `ExpiresAtUtc?`.
  Factory `Create(tenantId, studentId, session, method, codeId, amount, now)` — computes `ExpiresAtUtc` =
  `now + session.ValidityDays` (**`ValidityDays == 0` ⇒ no expiry ⇒ `null`**), provisions one
  `EnrollmentVideoAccess` per session video from the video’s `AccessCount`, raises `EnrollmentCreatedEvent`.
- `EnrollmentVideoAccess` (child): `VideoId`, `AccessAllowed`, `AccessRemaining`. (Decrement/gate is **Phase 5**.)
- Methods: `Extend(session, now)` (`FR-PLAT-ENR-004`: reset counters + push expiry **in place**, raise
  `EnrollmentExtendedEvent`), `Refund(now)` (`Active → Refunded`, raise `EnrollmentRefundedEvent`; guard `Active`).
- `PaymentTransaction`: `Method`, `Amount`, `CodeId?`, `Status` (`Completed`/`Refunded`), `CreatedAtUtc`,
  `ProviderRef?` (always null this phase). Created `Completed` on enroll; refund writes a reversing entry / flips status
  (`FR-PLAT-PAY-001/002` — seam only, no gateway calls).
- Unit-test: expiry math (incl. `0 ⇒ null`), counter provisioning, refund guard, extend resets-not-duplicates.

### A3 — Infrastructure: EF config + migration
- `Persistence/Configurations/`: `CodeBatchConfiguration`, `CodeConfiguration`, `EnrollmentConfiguration`,
  `EnrollmentVideoAccessConfiguration`, `PaymentTransactionConfiguration` (mirror `SessionConfiguration`).
  Tenant **and** soft-delete global query filters on the roots (`CodeBatch`, `Code`, `Enrollment`); `OwnsMany` for
  `EnrollmentVideoAccess` + `PaymentTransaction` (or FK child tables — keep them owned/immutable where possible).
  **Unique index `(TenantId, Serial)`** on `Code`; indexes `(TenantId, Status)`, `(TenantId, SessionId)`,
  `(TenantId, BatchId)` on `Code`; `(TenantId, SessionId, Status)` + **filtered unique** "one `Active` enrollment per
  `(TenantId, StudentId, SessionId)`" on `Enrollment` (`FR-PLAT-ENR-006`).
- Add DbSets to `Application/Common/Interfaces/IAppDbContext.cs` **and** `Infrastructure/Persistence/AppDbContext.cs`:
  `CodeBatches`, `Codes`, `Enrollments` (`PaymentTransactions`/`EnrollmentVideoAccess` may be navigation-only).
- Migration `AddCodesAndEnrollment` (gated — never auto-applied; `NFR-AVAIL-004`). Build via Infrastructure-as-startup /
  `-c Release` per the project’s VS-lock workaround.

### A4 — Infrastructure: enrollment side-effect seam (stub now, Phase 5 real)
- `Application/Common/Interfaces/IEnrollmentSideEffects.cs` — `GenerateAssessmentsAsync(Guid enrollmentId)`
  (assignment snapshot `FR-PLAT-ASG-001` + prerequisite quiz `FR-PLAT-QZ-001`).
- `Infrastructure/Services/StubEnrollmentSideEffects.cs` — logs intent, no-op (the assignment/quiz **engines** are
  Phase 5). Wire an `EnrollmentCreatedEvent` `INotificationHandler` that calls it. Register in
  `InfrastructureServiceExtensions`. *This mirrors Phase 3’s `StubVideoProcessingQueue`.*
- **Counter provisioning + payment + attendance shell are NOT stubbed** — they happen for real in A2/A5.
- `Attendance` shell: minimal `Attendance` row per `(student, session)` created on enroll (`FR-PLAT-ATT-001`); score
  columns are written by the Phase 5 grading engine. (If you prefer, defer the `Attendance` entity itself to Phase 5 and
  only raise the event — but creating the shell now keeps the enrollment tx complete. Recommended: create the shell.)

### A5 — Infrastructure: CSV export
- `Application/Common/Interfaces/ICodeExporter.cs` → `Infrastructure/Services/CsvCodeExporter.cs` — streams the
  contract §2 column set. No Hangfire (real async export + `.xlsx` is Phase 5); synchronous stream is fine for batch
  sizes ≤ 1000. Endpoints return `Results.File(stream, "text/csv", filename)`.

### A6 — Application: Codes CQRS  (`Features/Codes/`)
Commands (`ITransactionalRequest` + `Validator`): `GenerateCodeBatch` (mint batch + children; default `value` to
`session.Price`; `FR-PLAT-COD-001`), `DisableCode`, `EnableCode`, `DeleteCode` (all guard **not `Used`** → 409).
Queries: `ListCodes` (filters `search/status/batchId/sessionId`; paged; **join** code→batch(label)→session(title) and
code→student(redeemed-by name); `FR-PLAT-COD-005`), `ExportCodes` (filtered CSV), `ExportBatch` (one batch CSV).
DTOs + `CodeMappings.ToDto()` per contract.

### A7 — Application: Enrollment CQRS  (`Features/Enrollment/`)
Commands: `UnlockSession` (staff; `{ studentId }`; bypass code & price; create-or-extend; `FR-PLAT-ENR-002`),
`RefundEnrollment` (`{ reason? }`; `Active → Refunded`; **return the code** `Used → Active` when `method == Code`;
reversing `PaymentTransaction`; `FR-PLAT-ENR-008`), `RedeemCode` (student self; `{ serial }`; validate
Active+value-match+no-active-dup → enroll/extend; `MarkRedeemed`; `FR-PLAT-ENR-001`).
Queries: `ListSessionEnrollments` (`EnrollmentListDto`, paged; progress fields 0 — Phase 5), `ListStudentEnrollments`
(`StudentEnrollmentDto`, paged).
- **Cycle of truth:** unlock + redeem funnel into one private `EnrollOrExtend` path so the side-effects, counters,
  payment, attendance shell, and the `EnrollmentCreatedEvent` fire identically (`FR-PLAT-ENR-005`).
- **Fill `enrolledCount`:** update the Phase 3 `ListSessions` + `GetSessionById` queries to count `Active` enrollments
  (contract §5; shape unchanged).

### A8 — Api: endpoints
- `Api/Endpoints/CodeEndpoints.cs` (`IEndpointGroup`) — contract rows #1–7 under `/api/codes`.
- `Api/Endpoints/EnrollmentEndpoints.cs` — rows #8–12 (#8/#9 under `/api/sessions/{id}`, #10/#12 under
  `/api/enrollments`, #11 under `/api/students/{id}`).
- Every route `RequirePermission(...)` per the contract; `.Produces<>` the documented shapes + `ProblemDetails`.
- **#12 redeem** is gated by an authenticated **Student-role** principal — add the minimal student-role policy (the one
  new auth touch). Confirm it is **not** reachable by staff-only tokens and vice-versa.

### A9 — Tests
- **Unit** (`UnitTests/Domain/`): code status guards (disable/enable/delete blocked when `Used`), serial uniqueness
  helper, enrollment expiry math (`0 ⇒ null`), counter provisioning, refund guard, extend-resets-not-duplicates.
- **Integration** (`IntegrationTests/`, mirror `SessionApiTests` + `SalahBahazadApiFactory`):
  - generate → register shows them → **export CSV** has the right columns; **tenant isolation `NFR-SEC-010`** (batch B
    never sees tenant A’s codes).
  - **redeem happy path** (student JWT): value-match enroll, code → `Used`, counters provisioned, `PaymentTransaction`
    `Completed`, attendance shell created, `EnrollmentCreatedEvent` fired (stub side-effects invoked).
  - redeem **409s**: used code, price mismatch, second active enrollment (`FR-PLAT-ENR-006`).
  - **unlock** (staff) bypasses price; **refund** flips status + returns the code + reversing payment; re-enroll/extend
    resets counters & pushes expiry (`FR-PLAT-ENR-004`) without a duplicate row.
  - **default-deny:** Assistant blocked on generate/disable/delete (Teacher-only); anonymous → 401; a staff token
    rejected on #12 and a student token rejected on staff routes.
  - **audit coverage**: exactly one entry per lifecycle event **including the read-only CSV export** (explicit
    `IAuditWriter`, proving the interceptor-miss is handled); **redeem attributed to the student actor**, unlock/refund/
    generate/disable/enable/delete to staff; semantic summaries present; hash chain intact (`FR-PLAT-AUD-002/005`).

## Exit criteria
All contract endpoints implemented and returning the documented shapes; `dotnet build -c Release` +
`dotnet test -c Release` green; OpenAPI/Scalar shows the new `Codes` + `Enrollment` groups; `enrolledCount` now real.
Hand off to the wiring stream.

## Out of scope (defer to Phase 5 — documented, not skipped)
- **Assignment & prerequisite-quiz snapshot generation** (`FR-PLAT-ENR-005` partial, `FR-PLAT-ASG-001`,
  `FR-PLAT-QZ-001`) — Phase 4 ships the `IEnrollmentSideEffects` seam **stubbed**; the engines that consume the
  snapshots are Phase 5 (exactly as Phase 3 stubbed the transcode queue).
- **Prerequisite-assignment-completion enrollment gate** (`FR-PLAT-ENR-007`) — needs real assignments; enforced Phase 5.
- **Per-video access decrement + video playback gate** (`FR-PLAT-VID-001..006`) — counters are *provisioned* now,
  *spent* in Phase 5.
- **Attendance scoring, quiz/assignment review, dashboard KPIs, full audit-log browser** — Phase 5 frontend.
- **Hangfire async export + true `.xlsx`, online payment gateway** (`FR-PLAT-PAY-002`) — later.
