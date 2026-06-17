# Salah Bahazad Platform — Requirements & Architecture Specification

A clean, target-state specification for the online private-tutoring platform: what the system **shall** do, written independently of the current implementation. It is modular so each portal can be handed to design and built in isolation. A separate [gap analysis](07-implementation-gap-analysis.md) maps this target state back to what exists in the code today.

> Scope decisions for this revision were confirmed with the product owner (Amr):
> 1. **Tenant-ready, single-tenant now** — design clean tenant seams; do not build tenant CRUD or billing yet.
> 2. **Minimise video-security cost** — optimise streaming options for the lowest sustainable spend, with an honest account of the trade-offs.
> 3. **Clean target-state requirements** — the requirement docs describe the desired system, not the current code.
> 4. **Comprehensive, dispute-grade audit** — "who did what, on everything, with history," usable as evidence for abuse/disputes and for operational visibility.

---

## Document index

| # | Document | Audience |
|---|---|---|
| — | This overview — scope, roles, conventions, glossary | Everyone |
| 01 | [Functional requirements — Platform / shared](01-functional-platform-shared.md) | Backend, both portals |
| 02 | [Functional requirements — Admin portal](02-functional-admin-portal.md) | **Design hand-off**, admin frontend, backend |
| 03 | [Functional requirements — Student portal](03-functional-student-portal.md) | Student frontend, backend |
| 04 | [Non-functional requirements](04-non-functional-requirements.md) | Architecture, security, ops |
| 05 | [Secure video & asset storage — architecture](05-secure-video-streaming-options.md) | Architecture, product |
| 06 | [Database design & event-sourcing assessment](06-database-and-event-sourcing-assessment.md) | Architecture, backend |
| 07 | [Implementation gap analysis](07-implementation-gap-analysis.md) | Planning, backend |
| 08 | [Functional requirements — Native app (Win/macOS/iOS/Android)](08-functional-app.md) | App (Flutter), backend |
| 09 | [Non-functional requirements — Native app](09-non-functional-app.md) | App, architecture, security |

## How to read a requirement

Each functional requirement has a stable ID so it can be referenced from designs, tickets, and tests:

```
FR-<SCOPE>-<NNN>
```

| Scope | Meaning |
|---|---|
| `PLAT` | Platform / shared capability consumed by both portals (lives in the backend / domain) |
| `ADM` | Admin portal (Teacher + Assistant facing) |
| `STU` | Student portal (student facing) |
| `APP` | Native app — Windows/macOS/iOS/Android (video playback) |

Non-functional requirements use `NFR-<CATEGORY>-<NNN>` (e.g. `NFR-SEC-003`). Requirements use **SHALL** (mandatory), **SHOULD** (recommended), **MAY** (optional). IDs are never reused; deprecated requirements are struck through, not deleted.

## Actors & roles

| Actor | Description | Primary surface |
|---|---|---|
| **Student** | An enrolled or prospective learner. Self-registers, is approved by staff, buys/redeems access to sessions, studies. | Student portal + native app (video) |
| **Assistant** | Staff member who helps run day-to-day operations (approvals, unlocks, content, attendance) but cannot perform privileged/destructive platform actions. | Admin portal |
| **Teacher** | The tenant's owner/operator — the subscribing teacher. Everything an Assistant can do, plus code generation, staff/role management, deletions, taxonomy, and tenant configuration. (Named `Admin` in the current code.) | Admin portal |
| **Admin / System Owner** | *(Future, SaaS)* The platform operator who owns and administers all tenants, subscriptions, and cross-tenant concerns — not a tenant-level role. | Platform console (future) |
| **System** | Automated actors: scheduled jobs, domain-event handlers, the auto-grader, the auto-forfeit timer. Recorded in the audit trail as `System`. | — |

### Role / permission matrix (authoritative summary)

`✓` = allowed, `—` = not allowed, `self` = only on the actor's own record. The backend is the enforcement point; the portals only *reflect* these rules.

| Capability | Student | Assistant | Teacher |
|---|---|---|---|
| Register / manage own profile | self | self | self |
| Browse catalogue, redeem code, study | ✓ | — | — |
| Approve / reject student registrations | — | ✓ | ✓ |
| Unlock a session for a student (bypass code) | — | ✓ | ✓ |
| Refund / revoke an enrollment | — | ✓ | ✓ |
| Create / edit sessions, questions, materials | — | ✓ | ✓ |
| Configure quiz settings per session | — | ✓ | ✓ |
| **Generate / disable / delete codes** | — | — | ✓ |
| View code register & usage | — | ✓ | ✓ |
| Clear a student's bound device | — | ✓ | ✓ |
| Manage taxonomy (grades, subjects, specializations) | — | — | ✓ |
| Create / edit / delete staff (assistants & teachers) | — | — | ✓ |
| View attendance & reports | — | ✓ | ✓ |
| View full audit / activity log | — | ✓ (scoped) | ✓ (all) |
| Tenant settings & subscription | — | — | ✓ |

> The exact split between Assistant and Teacher is a configuration target, not a hard-coded constant — see `FR-PLAT-AUTH-007` (granular permissions). The matrix above is the default policy.

## Glossary

| Term | Definition |
|---|---|
| **Tenant** | One subscribing teacher/organisation and all its isolated data (students, sessions, codes, staff). The unit of future billing. |
| **Subject** | Top-level taxonomy, e.g. *Math*, *English*. Tenant-configurable. |
| **Specialization** | Sub-taxonomy under a subject, e.g. *Calculus*, *Algebra*. (Implemented today as *Topic*.) Tenant-configurable. |
| **Grade** | School year a session/student belongs to. Tenant-configurable. |
| **Session** | The sellable product — a "course": title, description, price, videos, materials, validity window, prerequisite, quiz, question bank. |
| **Enrollment** | One student's access to one session (purchased by code or unlocked by staff). Owns that student's video-access counters, assignment, and quiz. (Implemented today as *SessionPurchase*.) |
| **Code** | A prepaid voucher with a monetary value, redeemable once for a session of equal price. Generated by a Teacher, distributed offline. |
| **Question Bank** | The set of MCQ questions attached to a session; the source from which assignments and prerequisite quizzes are generated. |
| **Variation** | An alternate wording of the same question (LaTeX or image), used to randomise quizzes. |
| **Assignment** | Homework auto-generated for a session at enrollment; open-book, resumable, time-tracked. |
| **Quiz** | A timed, single-sitting, proctored assessment generated from a **prerequisite** session's question bank to gate the next session. |
| **Attendance** | The per-student, per-session record of progress and scores (assignment score, best quiz score, videos watched). |
| **Activity / Audit log** | Append-only record of *who* did *what*, *when*, against which entity, with before/after context. |
| **Device binding** | The single trusted device tied to a student's account to deter account sharing. |

## Tenant-readiness — what "single now, multi later" means concretely

This is a cross-cutting constraint that shapes every other document. For this revision:

- Every tenant-owned root entity (User, Session, Code, Grade, Subject, Specialization, Question, Enrollment, Attendance, Activity) **shall carry a `TenantId`**, even though exactly one tenant row exists today.
- A **current-tenant resolver** (e.g. from host/subdomain or a claim) **shall** be the single place that decides "which tenant is this request for," so no query is written assuming a global dataset.
- Authentication, authorization, audit, and taxonomy **shall** be tenant-scoped by design.
- **Not in scope now:** tenant self-service onboarding, subscription plans, billing, per-tenant theming/branding, cross-tenant admin console. These are explicitly deferred but must not be *contradicted* by current choices.

See [06 — Database & event-sourcing assessment](06-database-and-event-sourcing-assessment.md#multi-tenancy) for the data-layer implications.
