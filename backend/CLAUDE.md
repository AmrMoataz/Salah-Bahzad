# SalahBahazad.Api — CLAUDE.md

Private-tutoring platform backend. Single-tenant now, multi-tenant by design.
Managed by **dotnet-claude-kit**. Run `/dotnet-claude-kit:plan` before starting any feature.

---

## Solution layout

```
src/
  SalahBahazad.Domain/          # Entities, domain events, value objects, interfaces
  SalahBahazad.Application/     # CQRS handlers (Mediator), validators (FluentValidation), DTOs
  SalahBahazad.Infrastructure/  # EF Core, Hangfire, Redis, R2, Firebase, SignalR backplane
  SalahBahazad.Api/             # ASP.NET Core host, endpoints/controllers, middleware, hubs
tests/
  SalahBahazad.UnitTests/       # Domain rules, application handlers — no I/O
  SalahBahazad.IntegrationTests/# WebApplicationFactory + Testcontainers (Postgres + Redis)
```

## Architecture

**Clean Architecture + CQRS** (source-generated Mediator by Artem Shykhermanov). Cross-layer rules:

- Dependencies point inward: Api → Application → Domain. Infrastructure implements Domain interfaces.
- No EF types, HTTP types, or Hangfire types in Application or Domain.
- Each command/query lives in `Application/Features/<Aggregate>/Commands|Queries/`.
- Validators go in the same folder as their request (`<CommandName>Validator`).
- Domain entities raise `IDomainEvent`s (implement `INotification`); handlers in Application subscribe via `INotificationHandler<T>`.

## Tech stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10 |
| Framework | ASP.NET Core (Minimal API endpoints, optionally controllers) |
| ORM | EF Core + Npgsql (PostgreSQL) |
| CQRS | Mediator (Artem Shykhermanov, source-generated) + FluentValidation pipeline behaviour |
| Mapping | Manual — static `.ToDto()` / `.ToDomain()` extension methods, no library |
| Auth IdP | Firebase Authentication (both staff and students) |
| Platform tokens | Short-lived JWT issued by us after Firebase token verification |
| Caching / backplane | Redis — HybridCache (L1+L2) + SignalR Redis backplane |
| Background jobs | Hangfire + Hangfire.PostgreSql |
| Real-time | SignalR (quiz timer, notifications seam) |
| File / video storage | Cloudflare R2 (AWS SDK v2) — signed URLs, never serve from app disk |
| API docs | Scalar (OpenAPI) |
| Testing | xUnit v3 + Testcontainers + FluentAssertions + Verify |
| Logging | Serilog → structured JSON |
| Observability | OpenTelemetry (traces + metrics) |

## CQRS & Mediator

- Package: `Mediator` (NuGet: `Mediator.SourceGenerator` + `Mediator.Abstractions`) — source-generated at compile time, zero reflection, zero allocations at dispatch.
- Interfaces mirror MediatR: `IRequest<TResponse>`, `IRequestHandler<TRequest, TResponse>`, `INotification`, `INotificationHandler<T>`.
- Pipeline behaviours (`IPipelineBehavior<TRequest, TResponse>`) used for: validation (FluentValidation), transaction scope, and audit context injection.
- Domain events implement `INotification`; dispatched by the Infrastructure layer after `SaveChanges` (not before, so events fire only on successful commits).

## Mapping conventions

No mapping library. All mapping is explicit static extension methods:

- Entity → DTO: `ToDto()` extension in `Application/Features/<Aggregate>/` or a shared `Mappings/` folder within the feature.
- Persistence model → Domain entity: `ToDomain()` / `ToDataModel()` in `Infrastructure/Persistence/`.
- Keep mappings close to the type they map **from** — not in a central file.
- Never map inside a handler body; call the extension method.

## Multi-tenancy (cross-cutting constraint)

- **Every tenant-owned root entity carries `TenantId` (Guid FK).**
- A `CurrentTenantResolver` (single place, resolved from JWT claim or host) provides `TenantId` before any business logic.
- EF Core **global query filters** enforce tenant isolation automatically — never write per-handler `Where(x => x.TenantId == …)`.
- `City` and `Region` are global reference data (no `TenantId`); seeded from an authoritative Egypt dataset.
- Tenant isolation must be covered by automated integration tests (`NFR-SEC-010`).

## Domain model (primary aggregates)

```
Tenant
User (Student | Assistant | Teacher) — links to FirebaseUid
Session → SessionVideo, SessionMaterial, QuizSetting
Enrollment (was SessionPurchase) → per-video VideoAccess counters
Code (batch, value, Active/Inactive/Used/Deleted lifecycle)
Question → QuestionVariation (MCQ, LaTeX/image body)
UserAssignment → AssignmentQuestion (immutable snapshot)
UserQuiz → QuizAttempt (immutable attempt snapshot)
Attendance (per student+session: assignment score, best quiz score, videos watched)
AuditEntry (append-only, hash-chained)
AssessmentEvent (high-volume in-assignment/in-quiz behaviour telemetry — separate table)
StudentDevice (one active per user, history retained)
City / Region (global reference, seeded)
TermsAcceptance
PaymentTransaction (code redemption seam; online gateway deferred)
Grade / Subject / Specialization (tenant-managed taxonomy)
```

## Authentication & authorization

- Firebase verifies identity (email/password + Google social). **The platform stores no passwords.**
- Backend verifies the Firebase ID token, then issues a **short-lived platform JWT** containing `userId`, `tenantId`, `role`, and `deviceId`.
- Authorization is expressed as **granular permissions** bundled into `Teacher` / `Assistant` / `Student` roles.
- **Server-side enforcement on every endpoint** — UI hiding is never the only control.
- Refresh + revocation supported. Session tokens expire; revocation clears server-side revocation list (Redis).
- Password reset delegated entirely to Firebase — no app-side reset email logic.
- SignalR hubs authenticate with the same JWT scheme; **no query-string credentials** (`NFR-SEC-005`).

## Device binding

- Each student binds exactly one device: a long-lived, HttpOnly, signed **device token** (server-issued) + fingerprint as a secondary signal.
- Sign-in or content access from an unbound device is rejected with a clear message.
- Staff can clear a device (audited); next login re-binds.

## Audit log

- `AuditEntry` is **append-only and immutable** at the application layer (no update/delete route).
- Written by a `SaveChangesInterceptor` (automatic field-diff for every auditable entity) **and** explicit domain/application event handlers for semantically rich actions.
- All entries include: `TenantId`, `ActorId`/`ActorRole`, `Action`, `EntityType`/`EntityId`, `Summary`, `BeforeJson`/`AfterJson`, `Ip`, `Portal`, `DeviceId`, `PrevHash`/`Hash`.
- Hash chain (`PrevHash` → `Hash`) makes deletions or back-dating detectable.
- The audited "everything" list is defined in `FR-PLAT-AUD-002` (sign-in, registration, enrollment, codes, quiz events, video access, device, staff, taxonomy, …).
- `AssessmentEvent` is the separate high-volume table for in-assignment navigation and quiz focus-loss pings.

## Video & asset storage

- Source videos and HLS renditions live in **Cloudflare R2**. Nothing is stored on the app server disk.
- Video delivered as **HLS + AES-128**, short-lived signed URLs — no single downloadable file.
- Dynamic watermark (student serial/phone) rendered by the player.
- Screenshot/recording black-out is the native app's responsibility via OS flags — **no DRM**.
- Private assets (ID images, paid PDFs) require an auth + enrollment check before a signed URL is issued.
- Database stores only R2 object keys / HLS manifest paths — never file bytes or durable public URLs.

## Background jobs (Hangfire)

- Auto-grade completed assignment → write attendance score
- Auto-forfeit quiz attempt on timer expiry (server-side, authoritative)
- Export code batches to CSV/Excel
- Notifications seam (deferred per FR-PLAT-NOT-*)
- All jobs are retry-safe and run outside the request path (`NFR-SCAL-004`).

## Key business rules (enforce in tests)

- A code is redeemable **exactly once**, only while `Active`, and only for a session whose price equals the code's value (`FR-PLAT-COD-003`).
- Enrollment gate: if a session has a prerequisite, the student must have **completed that prerequisite's assignment** before enrolling (`FR-PLAT-ENR-007`).
- Quiz gate: a session's videos are locked until the gating quiz is **passed (score ≥ minimum pass %)** — fix the current `>` vs `≥` bug (`FR-PLAT-QZ-008`).
- A student holds at most one active enrollment per session (`FR-PLAT-ENR-006`).
- Quiz forfeit: leaving the page / losing connection forfeits the active attempt with score 0, consuming that attempt (`FR-PLAT-QZ-004`).
- Soft-delete only for any entity that participates in audit, attendance, or financial history (`FR-PLAT-ROLE-004`).
- Taxonomy items in use cannot be hard-deleted (`FR-PLAT-TAX-004`).
- A Teacher cannot create or elevate a staff account to a role higher than their own (`FR-PLAT-ROLE-002`).

## EF Core conventions

- One `IEntityTypeConfiguration<T>` per entity, in `Infrastructure/Persistence/Configurations/`.
- **Global query filter on `TenantId`** (via `CurrentTenantResolver`) on every tenant-owned entity.
- Owned snapshot types (`AssignmentQuestion`, `QuizAttempt` answers) use `OwnsMany` — immutable once written.
- `EntityBase` provides `CreatedById/At`, `UpdatedById/At` on all entities.
- Migrations are **gated**: no auto-apply on prod boot. Migrations are a deliberate deploy step (`NFR-AVAIL-004`).
- Index every tenant root on `(TenantId, <natural key>)`. Index `AuditEntry` on `(TenantId, OccurredAtUtc)`, `(EntityType, EntityId)`, `(ActorId)`.

## Testing standards

- Integration tests use `WebApplicationFactory` + **Testcontainers** (PostgreSQL + Redis containers).
- Every test that exercises a multi-tenant rule must prove cross-tenant isolation (`NFR-SEC-010`).
- Priority coverage: enrollment gate, quiz scoring/forfeit, code lifecycle, tenant isolation, video access control.
- Snapshot tests for complex response shapes — use Verify.
- Use `FluentAssertions` for readable assertions.

## Naming conventions

- Avoid perpetuating existing typos from the legacy codebase: `Infrastructure` (not `Infrustructure`), `Activities` (not `Activites`), `Pagination` (not `Pagtination`) (`NFR-MAINT-004`).
- `Specialization` (not `Topic`) in all new code — rename in UI/API surface.
- `Enrollment` (not `SessionPurchase`) in all new code.

## Security checklist (before any PR)

- [ ] No secrets committed — all from environment / secret store (`NFR-SEC-002`)
- [ ] Every new endpoint has a `[RequirePermission]` (or equivalent) attribute
- [ ] IDOR: validate the caller owns the resource — GUIDs in URLs are not a substitute for authorization (`NFR-SEC-007`)
- [ ] File uploads: type/size validated, stored to R2, never to disk
- [ ] Audit: any state-changing action emits an `AuditEntry`
- [ ] New SignalR hub authenticates via JWT, not query-string

## dotnet-claude-kit skills quick reference

| Task | Skill |
|---|---|
| New feature end-to-end | `/dotnet-claude-kit:plan` → `/dotnet-claude-kit:scaffold` |
| API design / versioning | `/dotnet-claude-kit:minimal-api` `/dotnet-claude-kit:api-versioning` |
| EF Core / migrations | `/dotnet-claude-kit:ef-core` |
| Auth implementation | `/dotnet-claude-kit:authentication` |
| Caching (HybridCache/Redis) | `/dotnet-claude-kit:caching` |
| Background jobs | see Hangfire docs — no dedicated skill |
| Tests | `/dotnet-claude-kit:testing` `/dotnet-claude-kit:tdd` |
| Security audit | `/dotnet-claude-kit:security-scan` |
| Code review | `/dotnet-claude-kit:code-review` |
| Build errors | `/dotnet-claude-kit:build-fix` |
| OpenAPI / Scalar | `/dotnet-claude-kit:openapi` `/dotnet-claude-kit:scalar` |
| Logging (Serilog) | `/dotnet-claude-kit:serilog` |
| OpenTelemetry | `/dotnet-claude-kit:opentelemetry` |
| Health checks | `/dotnet-claude-kit:health-check` |
| Docker / CI | `/dotnet-claude-kit:docker` `/dotnet-claude-kit:ci-cd` |

## Requirements traceability

Full requirements live in the repo's `../docs/` folder (this CLAUDE.md is in `backend/`):
- `../docs/README.md` — scope, roles, glossary
- `../docs/01-functional-platform-shared.md` — auth, tenancy, enrollment, quiz, audit, video
- `../docs/02-functional-admin-portal.md` — admin portal features
- `../docs/03-functional-student-portal.md` — student portal features
- `../docs/04-non-functional-requirements.md` — security, perf, scale, observability
- `../docs/05-secure-video-streaming-options.md` — R2 + HLS architecture
- `../docs/06-database-and-event-sourcing-assessment.md` — schema decisions, audit architecture
- `../docs/07-implementation-gap-analysis.md` — delta from current code
- `../docs/08-functional-app.md` / `../docs/09-non-functional-app.md` — Flutter app requirements
- `../docs/01-brand.md` · `../docs/02-foundations.md` · `../docs/03-components.md` · `../docs/tokens.{css,scss,json}` — design system & tokens
- `../docs/IMPLEMENTATION-PLAN-admin-portal.md` — the phased build plan
