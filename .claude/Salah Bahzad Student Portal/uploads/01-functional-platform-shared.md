# Functional Requirements — Platform / Shared

Cross-cutting capabilities that live in the backend/domain and are consumed by **both** the admin and student portals. Portal-specific behaviour is in [02 — Admin](02-functional-admin-portal.md) and [03 — Student](03-functional-student-portal.md). Conventions and the role matrix are in the [overview](README.md).

## Contents

- [1. Multi-tenancy & tenant context](#1-multi-tenancy--tenant-context)
- [2. Identity & authentication](#2-identity--authentication)
- [3. Authorization & roles](#3-authorization--roles)
- [4. Device binding](#4-device-binding)
- [5. Taxonomy & reference data](#5-taxonomy--reference-data)
- [6. Sessions (the product)](#6-sessions-the-product)
- [7. Enrollment, codes & payments](#7-enrollment-codes--payments)
- [8. Question bank](#8-question-bank)
- [9. Assignments engine](#9-assignments-engine)
- [10. Quizzes engine](#10-quizzes-engine)
- [11. Secure video service](#11-secure-video-service)
- [12. Attendance & grading](#12-attendance--grading)
- [13. Audit & activity logging](#13-audit--activity-logging)
- [14. Notifications](#14-notifications)

---

## 1. Multi-tenancy & tenant context

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-TEN-001 | Every tenant-owned entity SHALL carry a `TenantId`; all reads and writes SHALL be implicitly scoped to the current tenant. | Single tenant exists today; the scoping must still be present. |
| FR-PLAT-TEN-002 | The system SHALL resolve the current tenant from a single source (host/subdomain or token claim) before any business logic runs, and reject requests that cannot be attributed to a tenant. | One resolver, one place. |
| FR-PLAT-TEN-003 | No query, report, or export SHALL be able to return data from more than one tenant. | Enforced centrally (global query filter), not per-handler. |
| FR-PLAT-TEN-004 | A `Tenant` aggregate SHALL exist (id, display name, status, locale defaults) even though tenant onboarding/billing is out of scope this revision. | Seam for future SaaS. |
| FR-PLAT-TEN-005 | Staff accounts, students, and all catalogue/taxonomy data SHALL belong to exactly one tenant. | A student of tenant A can never see tenant B. |

## 2. Identity & authentication

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-AUTH-001 | Students SHALL authenticate via Firebase Authentication, supporting at minimum email/password and at least one social provider (Google). | Firebase is the student IdP. |
| FR-PLAT-AUTH-002 | The backend SHALL verify the Firebase ID token server-side and exchange it for a platform session (JWT or equivalent) carrying `userId`, `tenantId`, `role`, and `deviceId`. | Firebase proves identity; the platform issues authorization. |
| FR-PLAT-AUTH-003 | On first authentication, if no platform account exists for the Firebase identity, the system SHALL begin the student registration flow (see `FR-STU-REG-*`) and pre-fill name, email, photo, and phone where the provider supplies them. | Firebase → profile bootstrap. |
| FR-PLAT-AUTH-004 | Staff (Teacher/Assistant) SHALL also authenticate via Firebase (email/password); staff accounts SHALL be provisioned by a Teacher (no self-registration) with their Firebase identity linked at creation. The platform SHALL store no staff passwords. | One IdP for everyone; staff and students still use separate login surfaces. |
| FR-PLAT-AUTH-005 | A student account SHALL NOT be able to sign in while its status is `Pending`, `Rejected`, or `Inactive`; only `Active` accounts may obtain a session. | Approval gate enforced at token issuance. |
| FR-PLAT-AUTH-006 | Sessions SHALL expire; the system SHALL support refresh without forcing re-login, and SHALL allow staff to revoke a user's active sessions. | Add refresh + revocation (absent today). |
| FR-PLAT-AUTH-007 | Authorization SHALL be expressed as granular permissions grouped into the `Teacher`/`Assistant`/`Student` roles, so the Assistant/Teacher split can be reconfigured without code changes. | Role = bundle of permissions. |
| FR-PLAT-AUTH-008 | Every privileged endpoint SHALL enforce role/permission **server-side**; the UI hiding an action SHALL never be the only control. | Closes current gaps where code/user endpoints lacked checks. |
| FR-PLAT-AUTH-009 | Password reset SHALL be delegated entirely to Firebase's built-in self-service flow for **all** users (staff and students); the platform SHALL NOT build its own reset logic or send its own emails. | Avoids app-side email/reset — Firebase sends the emails. |

## 3. Authorization & roles

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-ROLE-001 | The system SHALL implement the default [role/permission matrix](README.md#role--permission-matrix-authoritative-summary). | Source of truth for both portals. |
| FR-PLAT-ROLE-002 | A Teacher SHALL NOT be able to create or elevate a staff account to a role higher than their own. | Prevents privilege escalation. |
| FR-PLAT-ROLE-003 | Code generation, code disable/delete, staff management, taxonomy management, deletions, and tenant settings SHALL be restricted to `Teacher`. | Assistants are operational, not administrative. |
| FR-PLAT-ROLE-004 | Destructive actions (delete user/session/code) SHALL require explicit confirmation and SHALL be soft-deletes where the data participates in audit, attendance, or financial history. | Preserve history. |

## 4. Device binding

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-DEV-001 | Each student account SHALL be bindable to exactly one trusted device. | One device per student. |
| FR-PLAT-DEV-002 | On first sign-in with no bound device, the system SHALL present a consent prompt; on acceptance it SHALL bind a stable device fingerprint to the account. | Explicit opt-in. |
| FR-PLAT-DEV-003 | Sign-in or content access from a device other than the bound one SHALL be refused with a clear "device not recognised — contact support" message. | Anti-sharing control. |
| FR-PLAT-DEV-004 | Staff SHALL be able to clear a student's bound device, after which the next device may be re-bound; each clear SHALL be audited (who, when, reason). | Recovery path. |
| FR-PLAT-DEV-005 | Device identity SHALL combine a server-issued, HttpOnly device token with client signals; it SHALL NOT rely on a single spoofable header. | Robust fingerprinting. |
| FR-PLAT-DEV-006 | The bound device and its binding date SHALL be visible to staff on the student's detail screen. | Support visibility. |

> **Suggested improvement over pure fingerprinting:** issue a long-lived, HttpOnly, signed *device cookie/token* on binding and treat that as the device identity, with a fingerprint as a secondary signal. It is far more stable than fingerprint-only (which breaks on browser updates) and harder to clone than a header. For a future native app, the OS keystore holds the device token. This keeps one-device-per-student enforceable without locking legitimate students out every browser update.

## 5. Taxonomy & reference data

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-TAX-001 | Teachers SHALL manage **Grades**, **Subjects**, and **Specializations** (CRUD), all tenant-scoped and dynamic. | "Specialization" = Topic today. |
| FR-PLAT-TAX-002 | A Specialization SHALL belong to exactly one Subject; a Session SHALL reference one Grade and one Specialization (and thereby its Subject). | Hierarchy: Subject → Specialization → Session. |
| FR-PLAT-TAX-003 | Cities and Regions SHALL be a fixed, system-provided **reference dataset for Egypt** (cities/governorates and their dependent regions), seeded and shared across all tenants — **not** staff-managed. Regions SHALL depend on the selected City (cascading). | Global lookup; seeded from an authoritative source (see note below). |
| FR-PLAT-TAX-004 | Taxonomy items in use SHALL NOT be hard-deleted; the system SHALL block deletion or offer archival when references exist. | Referential safety. |
| FR-PLAT-TAX-005 | Taxonomy SHALL be queryable for dropdowns by both portals, including an anonymous read of Grades/Cities/Regions for the public registration form. | Public sign-up needs lists pre-auth. |

> **Cities & Regions are reference data, not staff content.** Egypt's cities and their regions SHALL be seeded from an authoritative public dataset and maintained as a **global, read-only lookup shared by every tenant** — there is no staff CRUD screen for them. Changes happen via a re-seed/migration when the source data changes, not through the portal. This is why FR-PLAT-TAX-001 (Teacher-managed taxonomy) covers only Grades, Subjects, and Specializations, and why Cities/Regions carry no `TenantId`.

## 6. Sessions (the product)

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-SES-001 | A Session SHALL have: title, description, price, thumbnail, validity window (days until enrollment expiry, 0–365), grade, specialization, and status (draft/published/archived). | Core attributes. |
| FR-PLAT-SES-002 | A Session SHALL contain an ordered list of **videos**, each with its own per-enrollment **allowed access (view) count**. | View cap is per video. |
| FR-PLAT-SES-003 | A Session SHALL contain unlimited **materials** (readable attachments: PDF/CSV/PNG/JPG) for download/reading. | Reading resources. |
| FR-PLAT-SES-004 | A Session MAY reference one **prerequisite** Session; enrollment SHALL be blocked until the prerequisite is satisfied (see `FR-PLAT-ENR-007`). | Self-referencing chain. |
| FR-PLAT-SES-005 | A Session SHALL have a **question bank** (see §8) used to generate its assignment and to generate the quiz that gates any session for which it is the prerequisite. | Question bank drives assessment. |
| FR-PLAT-SES-006 | A Session MAY have **quiz settings** (time, number of questions, attempts, minimum pass %); these apply to the quiz that gates the *next* session. | Configured per session. |
| FR-PLAT-SES-007 | Editing a Session's questions after students have enrolled SHALL NOT retroactively alter already-generated assignments or quizzes. | Grading fairness via snapshotting. |
| FR-PLAT-SES-008 | The system SHALL expose a published catalogue view (student) and an administrative view (staff) of sessions, both paginated and filterable by grade/subject/specialization. | Two projections. |
| FR-PLAT-SES-009 | Session create/update/delete and content changes SHALL be audited. | See §13. |

## 7. Enrollment, codes & payments

### Codes

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-COD-001 | A Teacher SHALL generate codes in batches; each Code SHALL have a unique serial, a monetary **value**, a status, and creation metadata (who/when/batch). | `Teacher` only. |
| FR-PLAT-COD-002 | On generation, the batch SHALL be exportable as an Excel/CSV file for offline distribution. | Offline sale workflow. |
| FR-PLAT-COD-003 | A Code SHALL be redeemable exactly once, only while `Active`, and only for a session whose price equals the code's value. | One-shot, value-matched. |
| FR-PLAT-COD-004 | A Teacher SHALL be able to **disable** (`Active`→`Inactive`) and **re-enable** a code that has not been used, and SHALL be able to **delete** a code (soft-delete preserving history). | New lifecycle vs. one-directional today. |
| FR-PLAT-COD-005 | The code register SHALL show, per code: status, value, who created it, when, and — if used — which student redeemed it, for which session, and when. | Full traceability. |
| FR-PLAT-COD-006 | Every code lifecycle event (create, export, disable, enable, delete, redeem) SHALL be audited with actor and timestamp. | Dispute evidence. |

### Enrollment

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-ENR-001 | A student SHALL enroll in a session by redeeming a valid Code for that session. | Code path. |
| FR-PLAT-ENR-002 | Staff SHALL be able to **unlock** a session for a student directly, bypassing code and price checks. | Manual grant. |
| FR-PLAT-ENR-003 | An enrollment SHALL define an expiry = enrollment date + the session's validity window; after expiry the student SHALL lose access to the session's **videos** (and its gating quiz, which exists only to unlock those videos) but SHALL **retain access to the assignment**. | Time-boxed access; assignment stays open. |
| FR-PLAT-ENR-004 | Re-enrolling/extending SHALL reset a student's per-video access counters and push the expiry forward rather than duplicating the enrollment. | Idempotent re-grant. |
| FR-PLAT-ENR-005 | On enrollment the system SHALL auto-generate the session's assignment (see §9), provision per-video access counters, and — if the session has a prerequisite with questions — generate the prerequisite quiz (see §10). | Enrollment side-effects. |
| FR-PLAT-ENR-006 | A student SHALL NOT hold more than one active enrollment for the same session. | Dedupe. |
| FR-PLAT-ENR-007 | If a session has a prerequisite, the student SHALL be allowed to enroll only after **completing the prerequisite session's assignment**. The session's own gating quiz is taken *after* enrollment and unlocks its videos (see §10). | Enrollment gate = prerequisite's assignment. |
| FR-PLAT-ENR-008 | Staff SHALL be able to **refund/revoke** an enrollment, returning the code (where applicable) and removing access, with the action audited. | Reversal path. |

### Payments (future-ready, not enabled)

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-PAY-001 | The enrollment model SHALL treat "code redemption" as one payment method behind a payment abstraction, so an online gateway (e.g. Paymob) can be added later without reworking enrollment. | Seam only. |
| FR-PLAT-PAY-002 | Online payment processing SHALL NOT be enabled in this revision; the abstraction and data model SHALL nonetheless accommodate a transaction/receipt record. | Deferred by regulation. |

## 8. Question bank

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-QB-001 | All questions SHALL be multiple-choice (MCQ) with a defined set of answer options and one correct answer, and a configurable mark/weight. | MCQ only. |
| FR-PLAT-QB-002 | A question SHALL support a **LaTeX** body (for math) **and/or** an uploaded **image** of the question, so authors need not retype complex content. | Math + image input. |
| FR-PLAT-QB-003 | A question SHALL support multiple **variations** (alternate wordings/images with their own correct answer) used to randomise quizzes. | Anti-cheating variety. |
| FR-PLAT-QB-004 | A question SHALL carry a flag indicating whether it is eligible for use in **quizzes**; ineligible questions SHALL be usable in assignments only. | `IsValidForQuiz`. |
| FR-PLAT-QB-005 | A question MAY carry a hint resource (e.g. a YouTube explainer URL) that SHALL be shown **only in the assignment context**, never in a quiz. | Hint in homework, not exams. |
| FR-PLAT-QB-006 | Questions SHALL belong to a session's bank; staff SHALL add, edit, and remove questions and variations. | Authoring. |

## 9. Assignments engine

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-ASG-001 | On enrollment, the system SHALL generate one assignment per enrollment by snapshotting one variation of every question in the session bank. | Same assignment is acceptable across students. |
| FR-PLAT-ASG-002 | An assignment SHALL be **open-access**: the student may enter, leave, and resume freely with no single-sitting constraint. | Homework, not exam. |
| FR-PLAT-ASG-003 | The student SHALL answer questions incrementally (one at a time), with answers persisted as they go. | Resumable. |
| FR-PLAT-ASG-004 | The system SHALL accumulate **time spent** in the assignment across sessions and resume the timer on re-entry. | Time tracking. |
| FR-PLAT-ASG-005 | The system SHALL log the student's behaviour within the assignment (enter, leave, answer, navigate) for staff visibility. | Behaviour trail. |
| FR-PLAT-ASG-006 | When every question is answered, the assignment SHALL be auto-graded and the score written to the student's attendance for that session. | Auto-grade → attendance. |
| FR-PLAT-ASG-007 | The hint resource (`FR-PLAT-QB-005`) SHALL be available per question while solving the assignment. | — |
| FR-PLAT-ASG-008 | A completed assignment SHALL be reviewable (questions, submitted answers, correct answers, score). | Review mode. |

## 10. Quizzes engine

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-QZ-001 | A quiz SHALL be generated for an enrollment only when the session has a prerequisite **and** that prerequisite has quiz-eligible questions; the quiz is built from the **prerequisite** session's bank. | Gating assessment. |
| FR-PLAT-QZ-002 | Quiz generation SHALL respect the prerequisite session's quiz settings: total **time**, **number of questions**, **number of attempts**, and **minimum pass %**. | Per-session config. |
| FR-PLAT-QZ-003 | Each attempt SHALL draw an independently randomised subset of questions, selecting among each question's variations, so attempts differ. | Randomisation. |
| FR-PLAT-QZ-004 | A quiz SHALL be a **single sitting**: leaving the page, closing the browser, or losing connection SHALL forfeit the active attempt with a **zero** score, consuming that attempt. | Strict proctoring. |
| FR-PLAT-QZ-005 | A countdown timer SHALL be enforced **server-side**; when it reaches zero the attempt SHALL auto-submit with whatever has been answered. | Authoritative timer. |
| FR-PLAT-QZ-006 | The system SHALL detect tab/window switches and other focus-loss during an attempt and record each occurrence (count, timestamps, duration) as monitoring data for staff to review and judge; these events SHALL **not** auto-forfeit the attempt. Capturing *what* gained focus MAY be added later (best-effort). | Monitored for staff review, not auto-forfeit. |
| FR-PLAT-QZ-007 | A student's quiz grade SHALL be the **maximum** score across attempts; all attempt scores SHALL remain visible to the student and staff. | Best-of scoring, full history. |
| FR-PLAT-QZ-008 | Passing the quiz (score **≥** minimum pass %) SHALL unlock this session's **videos**; until it is passed (where a quiz exists), the session's videos SHALL remain locked. | Quiz gates this session's content. Pass rule is **`≥`** (current code uses strict `>` — to be fixed). |
| FR-PLAT-QZ-009 | An attempt, once submitted or forfeited, SHALL NOT be re-opened; a new attempt may be started while attempts remain. | Immutable attempts. |
| FR-PLAT-QZ-010 | All quiz events (start, submit, forfeit, timer-expiry, focus-loss) SHALL be audited. | Dispute evidence. |

## 11. Secure video service

> Decided architecture (Cloudflare R2 + CDN, HLS + AES-128, signed URLs, in-player watermark, native-app black-out, browser→app handoff): [05 — Secure video & asset storage](05-secure-video-streaming-options.md). The access/enrollment requirements here stay storage-agnostic; the app specifics are in [08](08-functional-app.md)/[09](09-non-functional-app.md).

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-VID-001 | Video playback SHALL be gated by a server check that the caller has an active, unexpired enrollment with remaining access count for that specific video, **and — where the session has a gating quiz — that the quiz has been passed**. | Server is the gate. |
| FR-PLAT-VID-002 | Each successful playback start SHALL decrement/record the per-video access count and SHALL be audited (who watched what, when). | View accounting + audit. |
| FR-PLAT-VID-003 | Video SHALL be delivered as **HLS** (segmented, AES-128 encrypted) from object storage (Cloudflare R2) via a CDN, using **short-lived signed URLs** that cannot be replayed or shared. | No durable hotlink; no single downloadable file. |
| FR-PLAT-VID-004 | The player SHALL display a **dynamic visible watermark** (student serial/phone) over the video and disable casual-capture affordances (right-click/download/PiP). | Traceability + deterrence. |
| FR-PLAT-VID-005 | The screenshot/screen-recording **black-out** SHALL be delivered by the **native apps** via OS secure-surface flags (see [08](08-functional-app.md)/[09](09-non-functional-app.md)); the platform SHALL NOT use DRM. *Play* SHALL therefore hand the video off to the device's app via a device-aware deep link, passing a short-lived **one-time handoff code** (never the raw token). | Black-out = OS flags, not DRM. |
| FR-PLAT-VID-006 | When the access count is exhausted or the enrollment is expired/forbidden, playback SHALL fail with a specific, user-readable reason. | Clear failure states. |
| FR-PLAT-VID-007 | Source videos and their HLS renditions SHALL live in object storage (R2), never on the app server's disk or web root; the database SHALL store only the object key / manifest reference. | Durable, off-disk. |

### File & asset storage (images, documents, uploads)

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-AST-001 | All uploaded files (session thumbnails, question images, materials, ID-verification images, profile images) SHALL be stored in object storage (Cloudflare R2), never on the app server's disk or web root. | One store for all assets. |
| FR-PLAT-AST-002 | Public, low-sensitivity assets (thumbnails, question images) MAY be served public-read via the CDN. | Cheap public delivery. |
| FR-PLAT-AST-003 | Private/sensitive assets (ID-verification images, paid materials/PDFs) SHALL live in a private bucket, be encrypted at rest, and be retrieved only via short-lived signed URLs after an auth/enrollment check; ID-image access SHALL be audited. | Minors' PII; gated content. |
| FR-PLAT-AST-004 | The database SHALL store only object keys/references, never file bytes or public absolute URLs for private assets. | Keys, not blobs. |

## 12. Attendance & grading

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-ATT-001 | The system SHALL maintain one attendance record per (student, session) capturing assignment score, best quiz score, videos watched, and progress. | Per-enrollment progress. |
| FR-PLAT-ATT-002 | Attendance scores SHALL be updated automatically as assignments are graded and quizzes are attempted. | Event-driven. |
| FR-PLAT-ATT-003 | Attendance data SHALL be queryable per student and per session to power the reporting screens (see `FR-ADM-ATT-*`). | Reporting source. |

## 13. Audit & activity logging

> The owner's requirement: **"know who did what, on everything, with history,"** usable for dispute/abuse evidence and operational visibility. This is a first-class platform capability, not a per-feature afterthought.

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-AUD-001 | The system SHALL record an append-only activity entry for every state-changing action, capturing **who** (actor + role), **what** (action + entity type + entity id), **when** (UTC), **where** (portal/IP/device), and **context** (before/after or a human-readable summary). | Comprehensive, uniform shape. |
| FR-PLAT-AUD-002 | Audited actions SHALL include at minimum: sign-in/out and failed sign-ins; registration, approval, rejection (with reason); enrollment by code; staff unlock; refund/revoke; code create/export/disable/enable/delete/redeem; session and content create/update/delete; question changes; quiz start/submit/forfeit; video access; device bind/clear; staff create/update/delete; role/permission changes; taxonomy changes; password resets. | The explicit "everything" list. |
| FR-PLAT-AUD-003 | Activity entries SHALL be **immutable** — no update or delete path — and retained per the retention policy (`NFR-AUD-*`). | Tamper-evidence. |
| FR-PLAT-AUD-004 | The log SHALL be queryable and filterable by actor, subject entity, action type, date range, and student/session, and SHALL paginate. | Investigation UX. |
| FR-PLAT-AUD-005 | Automated actions (auto-grade, auto-forfeit, scheduled jobs) SHALL be attributed to a `System` actor. | No anonymous mutations. |
| FR-PLAT-AUD-006 | Reading the audit log SHALL be permission-gated (Teacher: all; Assistant: scoped) and SHALL itself be audited for sensitive views. | Watch the watchers. |

## 14. Notifications

> **Deferred — not a priority for this phase.** Captured for completeness only; build after the core flows ship. Treat all of §14 as optional/backlog.

| ID | Requirement | Notes |
|---|---|---|
| FR-PLAT-NOT-001 | The system SHOULD notify a student when their registration is approved or rejected (with reason). | Channel TBD (email/SMS/in-app). |
| FR-PLAT-NOT-002 | The system SHOULD notify staff of pending registrations awaiting review. | Operational nudge. |
| FR-PLAT-NOT-003 | Notification dispatch SHALL be abstracted behind a provider interface so channels can be added later. | Seam. |

---

➡️ Next: [02 — Admin portal](02-functional-admin-portal.md) · [03 — Student portal](03-functional-student-portal.md)
