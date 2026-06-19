# Phase 3 — BACKEND stream (Sessions, content & question bank)

> Run this in its **own** Claude session, in parallel with the frontend stream. Created 2026-06-19.
>
> **Read first:** `backend/CLAUDE.md` (conventions, domain model, business rules) and the
> **frozen contract** `docs/contracts/phase3-sessions.md` (the API shape you must produce).
> **Template to mirror:** the Phase 2 Students slice — copy its structure exactly.
>
> **File ownership (do not cross):** this stream edits **`backend/**` only**. Do not touch anything
> under `frontend/`. The only coupling to the frontend is the frozen contract — match it field-for-field.

## Goal
Author the Session aggregate + question bank end-to-end (Domain → Application → Infrastructure → Api → tests),
exposing exactly the endpoints in the frozen contract. Green gate: `dotnet build -c Release` +
`dotnet test -c Release` (needs Docker for Postgres + MinIO Testcontainers).

## Architecture & established seams (reuse, don't reinvent)
- **Clean Architecture + CQRS**, source-gen Mediator (`IRequest`/`IRequestHandler`, `INotification`).
- **Entity bases:** `TenantEntityBase` (TenantId + `SetTenant`), `EntityBase` (Id/Created/Updated), `ISoftDeletable`.
- **Permissions ALREADY EXIST** — `SessionsRead/Create/Edit/Delete/Publish` (200–204), `QuestionsRead/Create/Edit/Delete`
  (500–503) in `Domain/Enums/Permission.cs`, already bundled in `Application/Features/Auth/PermissionCatalog.cs`.
  **No auth changes.**
- **Storage seam:** `IFileStorage.UploadPrivateAsync` / `GetSignedReadUrlAsync` (Application) →
  `R2FileStorage` (Infrastructure). Object-key convention from `RegisterStudentHandler.BuildObjectKey`. **No change needed.**
- **Audit:** automatic field-diff via `AuditSaveChangesInterceptor`; rich semantic actions via
  `IAuditableDomainEvent` on domain events; explicit entries via `IAuditWriter` (rarely needed here — staff actions
  carry a JWT tenant so the interceptor captures them).
- **Pipeline:** `ITransactionalRequest` marker → `TransactionBehavior`; `ValidationBehavior` (FluentValidation).
- **Endpoints:** `IEndpointGroup.Map(...)` auto-discovered; `MapGroup("/api/…")` + `RequirePermission(...)` +
  `.WithName/.WithSummary/.Produces<>`; multipart via `[FromForm]` + `IFormFile` (see `StudentEndpoints.RegisterAsync`).
- **Read models:** `PagedResult<T>`; manual `.ToDto()` static extensions co-located with the feature.

## Steps

### A1 — Domain: Session aggregate
`Domain/Entities/Session.cs` (`: TenantEntityBase, ISoftDeletable`), `SessionVideo.cs`, `SessionMaterial.cs`,
`QuizSetting.cs`; `Domain/Enums/SessionStatus.cs`, `VideoProcessingStatus.cs`; events in `Domain/Events/`.
- `Session`: Title, Description, Price, ThumbnailObjectKey?, ValidityDays (0–365), GradeId, SpecializationId,
  Status (default Draft), PrerequisiteSessionId?. Factory `Create(...)`; methods `UpdateDetails`, `SetThumbnail`,
  `SetPrerequisite(Guid?)` (block **direct self-reference** only — full cycle walk is the handler's job),
  `UpdateQuizSettings`, `Publish`/`Archive` (status guards), `SoftDelete`. Raise `SessionCreated/Published/Archived/Deleted`.
- `SessionVideo` (child, holds SessionId): Title, Order, **LengthMinutes** (admin-entered), AccessCount,
  SourceObjectKey, HlsManifestKey?, ProcessingStatus. Methods on `Session`: `AddVideo`, `UpdateVideo`
  (title/lengthMinutes/accessCount + optional source replace), `ReorderVideos`, `RemoveVideo`;
  on video: `MarkProcessing/MarkReady/MarkFailed`.
- `SessionMaterial`: FileName, ContentType, ObjectKey, SizeBytes.
- `QuizSetting` (owned 1:1): TimeLimitSeconds, QuestionCount, AttemptCount, MinPassPercent (0–100).
- Unit-test every guard (mirror `tests/SalahBahazad.UnitTests/Domain/StudentTests.cs`).

### A2 — Domain: Question bank
`Domain/Entities/Question.cs` (`: TenantEntityBase, ISoftDeletable`), `QuestionOption` (owned), `QuestionVariation`
(child with its own owned options).
- `Question`: SessionId, BodyLatex?, ImageObjectKey?, Mark, IsValidForQuiz, HintUrl?, Options, Variations.
  Invariants in factory/mutators: options ≥ 2 with **exactly one** correct; body LaTeX **and/or** image present.
  Methods: `Create`, `Update`, `SetImage/ClearImage`, `AddVariation`, `UpdateVariation`, `RemoveVariation`, `SoftDelete`.
- `QuestionVariation`: BodyLatex?, ImageObjectKey?, own Options. Same body + option invariants.

### A3 — Infrastructure: EF config + migration
- `Persistence/Configurations/`: `SessionConfiguration`, `SessionVideoConfiguration`, `SessionMaterialConfiguration`,
  `QuizSettingConfiguration`, `QuestionConfiguration`, `QuestionVariationConfiguration` (mirror `StudentConfiguration`).
  Tenant **and** soft-delete global query filters on the two roots (`Session`, `Question`); `OwnsMany` for options +
  `OwnsOne` for QuizSetting; FK SessionId; indexes `(TenantId, GradeId)`, `(TenantId, Status)`, `(SessionId)`.
- Add DbSets to `Application/Common/Interfaces/IAppDbContext.cs` **and** `Infrastructure/Persistence/AppDbContext.cs`:
  `Sessions`, `SessionVideos`, `SessionMaterials`, `Questions` (videos/materials/variations may be navigation-only).
- Migration `AddSessionsAndQuestionBank` (gated — never auto-applied; see `NFR-AVAIL-004`).
  Build migrations with Infrastructure-as-startup / `-c Release` per the project's VS-lock workaround.

### A4 — Infrastructure: transcode seam (no Hangfire yet)
- `Application/Common/Interfaces/IVideoProcessingQueue.cs` — `EnqueueTranscode(Guid videoId, string sourceKey)`.
- `Infrastructure/Services/StubVideoProcessingQueue.cs` — immediately marks the video `Ready` (logs intent).
  Register in `InfrastructureServiceExtensions`. Real Hangfire + HLS is **Phase 5** (`FR-PLAT-VID-001..006`).
- Thumbnails/materials/question images reuse `IFileStorage` as-is.

### A5 — Application: Sessions CQRS  (`Features/Sessions/`)
Commands (all `ITransactionalRequest` + a `Validator`): `CreateSession` (includes `description`, `FR-PLAT-SES-001`),
`UpdateSessionDetails`, `SetSessionThumbnail` (stream upload→R2→key), `SetPrerequisite` (**cycle detection by walking
the chain in the handler**, 409 on cycle/self), `UpdateQuizSettings` (minutes-based; reject `questionCount` > eligible
count), `PublishSession`, `ArchiveSession`, `DeleteSession`, `AddSessionVideo` (upload→enqueue transcode),
`UpdateSessionVideo` (title/lengthMinutes/accessCount + optional source replace), `ReorderSessionVideos`,
`RemoveSessionVideo`, `AddSessionMaterial` (upload), `RemoveSessionMaterial`.
Queries: `ListSessions` (filter by `gradeId/subjectId/status/search`; paged; stats questions/videos/enrolled; returns
grade/**subject**/specialization **names** — subject derived via `Specialization.SubjectId`. **No thumbnail in the list**
— the admin row renders a specialization-accent tile), `GetSessionById` (full detail incl. `subjectName`,
`quizEligibleQuestionCount`; `thumbnailUrl` is stored but the admin UI doesn't display it),
`ListSessionActivity` (session-scoped audit for the detail Activity tab; mirror `ListStudentActivity`),
`GetMaterialDownloadUrl` (signed URL for the per-material preview/download button — `GetSignedReadUrlAsync`).
DTOs + `SessionMappings.ToDto()` exactly per the frozen contract.

> **Design parity:** all shapes/filters/fields above are driven by `docs/contracts/phase3-sessions.md` §4 (which maps
> each prototype screen to its endpoints). Match the contract field-for-field; the frontend is building to the same file.

### A6 — Application: Question bank CQRS  (`Features/Questions/`)
Commands: `CreateQuestion`, `UpdateQuestion`, `SetQuestionImage`/`ClearQuestionImage`, `DeleteQuestion`,
`AddQuestionVariation`, `UpdateQuestionVariation`, `SetVariationImage`, `RemoveQuestionVariation`.
Queries: `ListQuestions` (paged; **embed signed `imageUrl`** for question + each variation), `GetQuestionById`.
Validators enforce the option/body invariants. DTOs per contract.

### A7 — Api: endpoints
- `Api/Endpoints/SessionEndpoints.cs` (`IEndpointGroup`) — contract rows #1–17 under `/api/sessions`, multipart sub-routes.
- `Api/Endpoints/QuestionEndpoints.cs` — contract rows #18–26 under `/api/sessions/{id}/questions`.
- Every route `RequirePermission(...)` per the contract; `.Produces<>` the documented shapes + `ProblemDetails`.

### A8 — Tests
- **Unit** (`UnitTests/Domain/`): Session lifecycle/status guards, validity 0–365, prerequisite self-block, video
  access-count, question option/variation invariants, quiz-eligibility count rule.
- **Integration** (`IntegrationTests/`, mirror `StudentRegistrationTests` + `SalahBahazadApiFactory`): CRUD happy paths,
  **tenant isolation `NFR-SEC-010`**, multipart upload to the MinIO Testcontainer, soft-delete hides rows, audit entries
  written on mutations, **default-deny** when the permission is absent, prerequisite cycle → 409, quiz over-count → 400.

## Exit criteria
All contract endpoints implemented and returning the documented shapes; `dotnet build -c Release` +
`dotnet test -c Release` green; OpenAPI/Scalar shows the new groups. Hand off to the wiring stream.

## Out of scope (defer)
Enrollment + `enrolledCount` (Phase 4) · real HLS transcode/Hangfire + video playback gate (Phase 5) ·
snapshot-on-edit (consumed at enrollment, Phase 4/5) · public student catalogue projection (`FR-PLAT-SES-008`).
