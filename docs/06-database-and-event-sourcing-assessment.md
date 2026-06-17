# Database Design & Event-Sourcing Assessment

Two questions you asked:
1. Does the current data design support the features in this spec, and where does it fall short?
2. Should the audit/history requirement be solved with **event sourcing**, or is that overkill?

Short answers: **the current relational design is a good foundation and the right paradigm — keep it.** It needs additive changes (tenancy, device binding, location cascade, terms, code lifecycle, and above all a real audit log). And **no — do not adopt full event sourcing.** Use an append-only, optionally hash-chained audit log on top of the existing model. Reasoning below.

## Contents

- [1. Current design — what's there](#1-current-design--whats-there)
- [2. Fit-by-feature](#2-fit-by-feature)
- [3. Required schema changes](#3-required-schema-changes)
- [4. Event sourcing vs. audit log — the real question](#4-event-sourcing-vs-audit-log--the-real-question)
- [5. Recommended audit architecture](#5-recommended-audit-architecture)
- [6. Migration approach](#6-migration-approach)

---

## 1. Current design — what's there

The backend (`salah-bahazad-backend`) uses EF Core + PostgreSQL with a clean, deliberate model:

- **~17 domain entities** (User, Session, SessionPurchase, SessionVideo, VideoAccess, Code, Question, QuizSetting, UserQuiz, QuizAttempt, UserAssignment, Attendance, SessionActivity, SessionMaterial, Grade, Subject, Topic) with **strongly-typed Guid IDs**.
- **Domain/persistence separation:** domain entities map to separate `*DataModel` classes via AutoMapper; one `IEntityTypeConfiguration` per entity.
- **`EntityBase : IAuditableEntity`** already stamps `CreatedById/At` and `UpdatedById/At` on every entity — a solid audit *baseline*.
- **Owned/embedded snapshot types** (`AssignmentQuestion`, `QuizQuestion`, `QuestionVariation`, `Answer`): a student's generated assignment and each quiz attempt are **frozen copies**, so later edits to a question never rewrite history. This is already a form of immutable historical record.
- **Domain events** drive side-effects (attendance scoring, session-activity creation), and **CQRS** (MediatR) already separates commands from queries.

Three things to know going in, because they shape the recommendations:

- **The generic activity-log mechanism is effectively dead.** `EntityBase` exposes `AddActivityLog`/`GetActivityLogs`, but `AddActivityLog` is called in exactly one place (code creation) and **nothing ever reads or persists the buffer** — there is no audit table and no SaveChanges interceptor flushing it. So "who did what" is *not* actually captured today.
- **The only persisted audit is `SessionActivity`** — session-scoped, five types (`Created/Updated/Opened/ActivatedByCode/Unlocked`), written by four domain-event handlers. No login, code-lifecycle, user-management, assignment/quiz, or device auditing exists.
- **Migrations auto-apply on every boot in every environment** — fine for dev, risky for prod (see `NFR-AVAIL-004`).

## 2. Fit-by-feature

| Capability | Current support | Verdict |
|---|---|---|
| Sessions, videos, materials, prerequisite, quiz settings | Modelled well (incl. self-referencing prerequisite, per-video access cap) | **Good** |
| Enrollment (code + staff unlock), re-enroll/extend, view caps | `SessionPurchase` + `VideoAccess`; counter reset on re-enroll | **Good** |
| Question bank: MCQ, LaTeX/text, image, variations, quiz-eligible flag, hint URL | `Question` has `IsValidForQuiz` + `VideoUrl`; `QuestionVariation` has text/image/answers | **Good** |
| Assignment generation + snapshot + auto-grade | `UserAssignment` + owned `AssignmentQuestion`; event-driven scoring | **Good** |
| Quiz generation, attempts-up-front, best-of, randomisation | `UserQuiz`/`QuizAttempt` with shuffled subsets | **Good** (confirm `>` vs `≥` pass rule) |
| Attendance / grading | `Attendance` per (user, session) | **Good** |
| Rejection reason | Stored in `User.Comment` (required on reject) | **Good** (rename for clarity) |
| Taxonomy: grade/subject/specialization | `Grade`/`Subject`/`Topic`, dynamic + seeded | **Good** (rename Topic→Specialization in UI) |
| **Multi-tenancy** | No `TenantId` anywhere | **Missing** |
| **Comprehensive audit ("everything, with history")** | Only `SessionActivity`; generic mechanism unpersisted | **Missing** — biggest gap |
| **Code disable/enable/delete** | One-directional `Active→Used`; no disable/delete/soft-delete | **Missing** |
| **Device binding** | No device model | **Missing** |
| **City → Region cascade** | `User.City`/`Location` are free-text strings; no Region, no cascade | **Partial/Missing** |
| **Terms acceptance record** | Not captured | **Missing** |
| Firebase identity link | Password-only (`PasswordHash`); no external-IdP linkage | **Missing** |
| **Payments seam (future Paymob)** | None; enrollment hard-couples to Code | **Missing (seam only)** |
| Code batch grouping for re-export | Codes generated but no batch entity | **Partial** |

## 3. Required schema changes

All additive; none require a rewrite.

**Tenancy**
- New `Tenant` table; add `TenantId` (FK) to every root aggregate (User, Session, Code, Grade, Subject, Specialization, Question, SessionPurchase, Attendance, AuditEntry, …).
- Add an EF Core **global query filter** on `TenantId` driven by the current-tenant resolver, so isolation is automatic.

**Audit (see §5 for the full schema)**
- New `AuditEntry` append-only table.
- Keep `SessionActivity` as a *derived* session-scoped read model (a filtered projection of `AuditEntry`), or fold it in with an `EntityType=Session` category — don't maintain two parallel hand-written trails.
- New high-volume `AssessmentEvent` table for in-assignment/in-quiz behaviour telemetry (enter/leave/answer/navigate/focus-loss), kept **separate** from the canonical audit log so thousands of UI events don't bloat it.

**Codes**
- Add `BatchId`, soft-delete (`IsDeleted`, `DeletedById/At`), and a full status set (`Active/Inactive/Used/Deleted`) with `DisabledById/At`. Enforce one-shot redemption while allowing disable/enable on unused codes.

**Device binding**
- New `StudentDevice` table (UserId, DeviceTokenHash, Fingerprint, BoundAt, Status, ClearedById/At, ClearReason) — one active per user, with history retained.

**Location**
- New `City` and `Region` reference tables (`Region.CityId` FK), **seeded with Egypt's cities/regions from an authoritative source and shared across all tenants (global reference data — not tenant-scoped, not staff-managed)**; change `User` to reference `CityId`/`RegionId` (keep `School` as text). Data-migrate existing free-text values.

**Identity & consent**
- Add external-IdP linkage to `User` (e.g. `FirebaseUid`, `AuthProvider`).
- New `TermsAcceptance` (UserId, Version, AcceptedAtUtc) — or columns on User if single-version.

**Payments seam**
- Introduce a `PaymentTransaction` (EnrollmentId, Method=`Code|Gateway`, Amount, ExternalRef, Status) so enrollment depends on "a payment" rather than specifically a code. No gateway yet; the table makes adding Paymob a localized change.

**Media & file storage**
- `SessionVideo` SHALL reference an R2 object key + HLS manifest path (not a YouTube id); `SessionMaterial`, profile images, and ID-verification images store R2 keys too. Originals + HLS renditions live in R2; nothing is stored on the app-server disk. Migrate the current local-`Storage/` approach to R2 + CDN with signed URLs.

## 4. Event sourcing vs. audit log — the real question

You're weighing whether to **event-source** the system (store every change as an immutable event; current state = a fold of events) to satisfy "know who did what on everything, with history," including dispute/abuse evidence.

**Recommendation: don't event-source the system. Add an append-only (optionally hash-chained) audit log instead.** Here's the honest trade-off.

**What event sourcing would buy you**
- A perfect, replayable history and the ability to reconstruct any aggregate's exact past state.
- Temporal queries ("what did this enrollment look like on March 3rd").
- Natural fit with the CQRS you already use.

**What it would cost you**
- A large, permanent complexity tax: event schema versioning and upcasting, snapshots for performance, projection/read-model rebuild tooling, eventual-consistency handling, and replay/debug machinery.
- Every contributor must understand ES to be productive. Your codebase today has **near-zero test coverage** and a small team; ES raises the bar for *every* future change, including trivial CRUD on sessions and taxonomy.
- Ad-hoc relational queries and reporting (which your admin portal leans on heavily — attendance matrices, code registers) get harder, not easier.

**Why an audit log wins here**
- Your actual requirement is **"who did what, when, with history,"** not "reconstruct aggregate state from events." An append-only `AuditEntry` answers the former directly and is trivially queryable for the admin investigation screens.
- You **already have the valuable parts of ES** without its cost: `IAuditableEntity` created/updated stamps, **immutable snapshot** owned-types for assignments and quiz attempts (a student's graded work is frozen by design), and domain events. The graded-evidence that matters most for disputes — *what exactly was on the quiz and what the student answered* — is already permanent.
- For **dispute/abuse defensibility**, append-only + a **hash chain** (each entry stores the hash of the previous entry) makes any deletion or back-dating detectable, which is the property you actually need — at a fraction of ES's complexity.

**The middle path, if you ever need more:** event-source **only** the two or three highest-dispute aggregates (Code redemption, Enrollment, QuizAttempt) while keeping everything else relational. But do this **only** when a concrete need appears (e.g. recurring chargeback-style disputes). Starting there now is premature.

**Bottom line:** full event sourcing is **overkill** for a tutoring platform — even a multi-teacher one — at your team size and maturity. Keep the relational model, add a first-class audit log, and reserve ES for a future, specific, justified aggregate if it ever earns its place.

## 5. Recommended audit architecture

Two complementary mechanisms:

**(a) Automatic change capture — a `SaveChangesInterceptor`.** Hook EF Core's save pipeline; for every inserted/updated/deleted auditable entity, write an `AuditEntry` with before/after in the same transaction (`NFR-AUD-003`). This guarantees *nothing* mutates without a trail and replaces the dead `EntityActivityLog` buffer.

**(b) Explicit business activities — domain/application events.** For semantically rich actions that aren't just a field diff ("rejected student, reason=…", "disabled code", "quiz auto-forfeited on disconnect", "video accessed"), raise an activity from the handler so the log reads in business terms, not column diffs. You already have the domain-event plumbing for this.

Proposed `AuditEntry` shape:

| Column | Purpose |
|---|---|
| `Id` | PK |
| `TenantId` | Isolation |
| `OccurredAtUtc` | When |
| `ActorId`, `ActorRole`, `ActorName` | Who (or `System`) |
| `Action` | e.g. `CodeDisabled`, `StudentApproved`, `VideoAccessed`, `SignInFailed` |
| `EntityType`, `EntityId` | What it acted on |
| `Summary` | Human-readable line for the admin feed |
| `BeforeJson`, `AfterJson` | Field-level context (nullable) |
| `Ip`, `Portal`, `DeviceId` | Where |
| `PrevHash`, `Hash` | Tamper-evidence (hash-chain; optional but recommended) |

Notes:
- **Append-only at the application layer:** no update/delete API; restrict DB privileges so the app role cannot delete from this table (`NFR-AUD-001`).
- Index by `(TenantId, OccurredAtUtc)`, `(EntityType, EntityId)`, and `(ActorId)` to serve the admin filters fast.
- Keep **behavioural telemetry** (assignment navigation, focus-loss pings) in the separate `AssessmentEvent` table — same idea, higher volume, lower retention — so the canonical audit log stays signal-rich.

## 6. Migration approach

1. **Stabilise migrations first:** stop auto-applying on prod boot; make migrations a gated, reviewed deploy step (`NFR-AVAIL-004`).
2. **Tenancy:** add `Tenant` + nullable `TenantId`, backfill the single existing tenant, then make non-nullable and enable the global filter. Add tenant-isolation tests (`NFR-SEC-010`).
3. **Audit:** add `AuditEntry` + the `SaveChangesInterceptor`, wire explicit activities for the high-value actions, and retire the unused `EntityActivityLog` path. Re-point/derive `SessionActivity` from the new log.
4. **Codes, device, location, terms, identity, payments:** additive tables/columns as in §3; data-migrate `User.City`/`Location` free-text into `City`/`Region` references with a reviewed mapping.
5. **Backfill nothing you can't defend:** historical audit entries can't be invented; start the trail at go-live and document that.
6. **Cover the rules with tests** as you go (`NFR-MAINT-001`), prioritising enrollment, code lifecycle, quiz scoring/forfeit, and tenant isolation.

---

➡️ Next: [07 — Implementation gap analysis](07-implementation-gap-analysis.md)
