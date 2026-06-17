# Functional Requirements — Admin Portal

The back-office used by **Teachers** and **Assistants** to run a tenant: students, sessions, assessment content, codes, taxonomy, staff, attendance, and audit. (The product name **"Admin Portal"** denotes this staff back-office — not the future platform-owner *Admin* role; see the [role table](README.md#actors--roles).) This portal is slated for a **ground-up rebuild and redesign**; this document is written to be handed to design, so it leads with an information architecture and screen inventory, then the detailed requirements.

Shared rules (auth, roles, tenancy, audit, the domain engines) live in [01 — Platform/shared](01-functional-platform-shared.md). The [role matrix](README.md#role--permission-matrix-authoritative-summary) governs which actions each role sees.

## Contents

- [A. Information architecture & screen inventory (design hand-off)](#a-information-architecture--screen-inventory-design-hand-off)
- [B. Shell, navigation & dashboard](#b-shell-navigation--dashboard)
- [C. Student management](#c-student-management)
- [D. Session management](#d-session-management)
- [E. Question bank authoring](#e-question-bank-authoring)
- [F. Quiz configuration](#f-quiz-configuration)
- [G. Assignment & quiz review](#g-assignment--quiz-review)
- [H. Code management](#h-code-management)
- [I. Taxonomy management](#i-taxonomy-management)
- [J. Staff & role management](#j-staff--role-management)
- [K. Attendance & reporting](#k-attendance--reporting)
- [L. Audit / activity log](#l-audit--activity-log)

---

## A. Information architecture & screen inventory (design hand-off)

A single authenticated shell (top bar + collapsible left nav) hosts the feature areas below. Every list is paginated, searchable, and filterable; every destructive action is confirmed. Target: responsive across phone, tablet, and desktop (dense management tables optimise for larger screens but stay usable on smaller ones).

| # | Screen | Purpose | Key elements | Satisfies |
|---|---|---|---|---|
| 1 | **Login** | Staff sign-in | Email/username, password, error states | FR-ADM-AUTH-001 |
| 2 | **Dashboard** | At-a-glance operations | KPI cards (pending approvals, active students, codes used/remaining, revenue-by-code), recent activity feed, quick actions | FR-ADM-DASH-001..003 |
| 3 | **Students — list** | Find & triage students | Filter by status/grade, search, status chips, row actions | FR-ADM-STU-001 |
| 4 | **Student — detail** | Full student record & actions | Profile, ID image, parents, grade, **device panel**, enrollments, attendance, **history tabs** (logins, enrollments/transactions, activity), approve/reject, deactivate | FR-ADM-STU-002..010 |
| 5 | **Approvals queue** | Review pending registrations | Card/list of pending students, inline approve / reject-with-reason | FR-ADM-STU-003..004 |
| 6 | **Sessions — list** | Catalogue management | Filter by grade/subject/specialization/status, stats (questions/videos/enrolled), row actions | FR-ADM-SES-001 |
| 7 | **Session — create/edit** | Author a session | Details, thumbnail, **videos (+ per-video access count)**, materials, grade, specialization, **prerequisite picker**, **quiz settings**, publish state | FR-ADM-SES-002..006 |
| 8 | **Session — detail** | Manage one session | Tabs: overview/stats, videos, materials, **question bank**, enrolled students, **activity**; actions: unlock-for-student, refund | FR-ADM-SES-007..011 |
| 9 | **Question editor** | Author MCQ questions | LaTeX editor + live preview, **image upload**, options/correct answer, mark, **variations**, **quiz-eligible toggle**, **hint URL** | FR-ADM-QB-001..006 |
| 10 | **Quiz settings** | Configure gating quiz | Time, #questions, #attempts, min pass %, preview of effective behaviour | FR-ADM-QZ-001..002 |
| 11 | **Assignment/quiz review** | Inspect a student's work | Questions, submitted vs correct, score, attempt history, time spent, behaviour log | FR-ADM-REV-001..003 |
| 12 | **Codes — list** | Code register | Filter by status/batch/session, columns (serial, value, status, created-by, used-by, session, dates), bulk actions | FR-ADM-COD-002..005 |
| 13 | **Codes — generate** | Mint a batch | Session/value, quantity, **Excel export on success** | FR-ADM-COD-001 |
| 14 | **Taxonomy** | Manage reference data | Tabs: Grades, Subjects, Specializations (Cities/Regions are seeded Egypt reference data — no editor) | FR-ADM-TAX-001, FR-ADM-TAX-003 |
| 15 | **Staff** | Teacher/assistant accounts | List, create/edit with role, deactivate/delete, reset password | FR-ADM-STAFF-001..004 |
| 16 | **Attendance** | Cross-student progress | Pick session → matrix of students × (videos watched, assignment score, quiz best/attempts); pick student → per-session breakdown; export | FR-ADM-ATT-001..004 |
| 17 | **Activity log** | Audit investigation | Global filterable feed (actor, action, entity, date, student/session); drill-in | FR-ADM-AUD-001..002 |
| 18 | **Settings** | Tenant/profile prefs | Own profile, password; *(tenant settings placeholder for future)* | FR-ADM-SET-001 |

## B. Shell, navigation & dashboard

| ID | Requirement | Notes |
|---|---|---|
| FR-ADM-AUTH-001 | Staff SHALL sign in via Firebase (email/password); non-staff accounts SHALL be rejected by this portal. | Separate surface; same IdP. |
| FR-ADM-AUTH-002 | The portal SHALL enforce route-level guards reflecting the signed-in role; unauthorised areas SHALL be hidden and blocked. | UI + server. |
| FR-ADM-DASH-001 | The dashboard SHALL surface counts of pending approvals, active students, codes (used/active/remaining), and enrollments over a selectable period. | Operational KPIs. |
| FR-ADM-DASH-002 | The dashboard SHALL show a recent-activity feed sourced from the audit log. | Quick pulse. |
| FR-ADM-DASH-003 | The dashboard SHALL offer quick actions (review approvals, generate codes, create session) gated by role. | Shortcuts. |

## C. Student management

| ID | Requirement | Notes |
|---|---|---|
| FR-ADM-STU-001 | Staff SHALL list/search students filtered by status (pending/active/rejected/inactive) and grade. | Triage. |
| FR-ADM-STU-002 | Staff SHALL view a student's full record: profile, ID-verification image, parent numbers, grade, school, city/region, bound device, enrollments, attendance. | 360° view. |
| FR-ADM-STU-003 | Staff SHALL **approve** a pending student, transitioning them to active and enabling sign-in. | Approval. |
| FR-ADM-STU-004 | Staff SHALL **reject** a pending student and SHALL be required to supply a reason, which is stored and shown in history (and to the student per `FR-PLAT-NOT-001`). | Reason mandatory. |
| FR-ADM-STU-005 | Staff SHALL edit a student's grade and parent contact numbers. | Corrections. |
| FR-ADM-STU-006 | Staff SHALL **deactivate/reactivate** a student account. | Lifecycle. |
| FR-ADM-STU-007 | Staff SHALL **clear the student's bound device**, with a reason, after which the student may re-bind. | Anti-sharing recovery. |
| FR-ADM-STU-008 | Staff SHALL view the student's **login history**, **enrollment/transaction history**, and **activity history** as distinct, paginated views. | History tabs. |
| FR-ADM-STU-009 | Staff SHALL view the student's attendance/grades across sessions from the detail screen. | Cross-link to §K. |
| FR-ADM-STU-010 | All student-management actions SHALL be audited (actor, action, reason, timestamp). | §13 platform. |

## D. Session management

| ID | Requirement | Notes |
|---|---|---|
| FR-ADM-SES-001 | Staff SHALL list/filter sessions by grade/subject/specialization/status with per-session stats (questions, videos, enrolled). | Catalogue. |
| FR-ADM-SES-002 | Staff SHALL create/edit a session: details, price, validity window, thumbnail upload, grade, specialization, publish state. | Core authoring. |
| FR-ADM-SES-003 | Staff SHALL add/reorder/remove **videos**, each with its **allowed access count**, and upload/replace each video via the secure pipeline. | Video mgmt. |
| FR-ADM-SES-004 | Staff SHALL add/remove **materials** (PDF/CSV/PNG/JPG). | Readings. |
| FR-ADM-SES-005 | Staff SHALL set/clear a **prerequisite** session from eligible candidates (excluding self/cycles). | Gating. |
| FR-ADM-SES-006 | Staff SHALL configure the session's **quiz settings** (see §F). | Gating quiz. |
| FR-ADM-SES-007 | Staff SHALL attach/detach questions to the session's **question bank** (see §E). | Bank wiring. |
| FR-ADM-SES-008 | Staff SHALL view enrolled students and per-session activity from the session detail. | Visibility. |
| FR-ADM-SES-009 | Staff SHALL **unlock** a session for a specific student (by phone/serial/search), bypassing code & price. | Manual grant. |
| FR-ADM-SES-010 | Staff SHALL **refund/revoke** an enrollment. | Reversal. |
| FR-ADM-SES-011 | Staff SHALL delete a session (soft-delete if enrollments/history exist), with confirmation and audit. | Safe delete. |

## E. Question bank authoring

| ID | Requirement | Notes |
|---|---|---|
| FR-ADM-QB-001 | Staff SHALL author MCQ questions with answer options, a correct answer, and a mark. | MCQ. |
| FR-ADM-QB-002 | The editor SHALL support **LaTeX** with a live rendered preview. | Math authoring. |
| FR-ADM-QB-003 | The editor SHALL allow **uploading an image** of the question instead of (or with) typed text. | Image questions. |
| FR-ADM-QB-004 | Staff SHALL add multiple **variations** per question, each with its own text/image and correct answer. | Randomisation source. |
| FR-ADM-QB-005 | Staff SHALL toggle a question's **quiz eligibility** and SHALL set an optional **hint URL** (shown only in assignments). | Flags. |
| FR-ADM-QB-006 | Staff SHALL edit/remove questions and variations, with the snapshot rule (`FR-PLAT-SES-007`) protecting existing students. | Safe edits. |

## F. Quiz configuration

| ID | Requirement | Notes |
|---|---|---|
| FR-ADM-QZ-001 | Staff SHALL set, per session, the gating quiz's **time**, **number of questions**, **number of attempts**, and **minimum pass %**. | The four knobs. |
| FR-ADM-QZ-002 | The UI SHALL validate settings against the bank (e.g. requested #questions ≤ quiz-eligible questions available) and warn otherwise. | Guardrail. |

## G. Assignment & quiz review

| ID | Requirement | Notes |
|---|---|---|
| FR-ADM-REV-001 | Staff SHALL review any student's assignment: questions, submitted answers, correct answers, score, and accumulated time. | Inspect homework. |
| FR-ADM-REV-002 | Staff SHALL review any student's quiz attempts: per-attempt questions/answers/score, best score, and forfeit/timeout flags. | Inspect exams. |
| FR-ADM-REV-003 | Staff SHALL view the student's in-assessment behaviour log (enter/leave/answer, focus-loss events). | Anti-cheat insight. |

## H. Code management

| ID | Requirement | Notes |
|---|---|---|
| FR-ADM-COD-001 | **Teachers** SHALL generate a batch of codes for a session/value and quantity, and the system SHALL produce a **downloadable Excel** of the batch. | Teacher-only mint + export. |
| FR-ADM-COD-002 | Staff SHALL view the code register filtered by status, batch, and session. | Register. |
| FR-ADM-COD-003 | The register SHALL show, per code: serial, value, status, created-by/when, and (if used) redeemed-by student, session, and when. | Traceability. |
| FR-ADM-COD-004 | **Teachers** SHALL **disable/enable** unused codes and **delete** codes (soft-delete), each action audited. | Lifecycle controls. |
| FR-ADM-COD-005 | The register SHALL support re-exporting a batch and SHALL never expose another tenant's codes. | Export + isolation. |

## I. Taxonomy management

| ID | Requirement | Notes |
|---|---|---|
| FR-ADM-TAX-001 | **Teachers** SHALL CRUD Grades, Subjects, and Specializations (Specialization under a Subject). | Dynamic taxonomy. |
| FR-ADM-TAX-002 | Cities and Regions SHALL **not** be staff-managed; they are a seeded, Egypt-wide reference dataset (see `FR-PLAT-TAX-003`). The portal MAY surface them read-only for reference. | No CRUD editor to build. |
| FR-ADM-TAX-003 | The UI SHALL prevent deletion of in-use taxonomy or offer archival, surfacing where it is referenced. | Referential safety. |

## J. Staff & role management

| ID | Requirement | Notes |
|---|---|---|
| FR-ADM-STAFF-001 | **Teachers** SHALL list staff and create staff (assistants or other teachers), assigning a role no higher than their own. | No escalation. |
| FR-ADM-STAFF-002 | **Teachers** SHALL edit a staff member's details, role, and status; credential/password reset is delegated to Firebase self-service (see `FR-PLAT-AUTH-009`). | Maintenance. |
| FR-ADM-STAFF-003 | **Teachers** SHALL deactivate/delete staff (soft-delete preserving audit attribution). | Lifecycle. |
| FR-ADM-STAFF-004 | All staff-management actions SHALL be audited. | §13. |

## K. Attendance & reporting

| ID | Requirement | Notes |
|---|---|---|
| FR-ADM-ATT-001 | Staff SHALL open an attendance view **per session** showing every enrolled student with: videos watched (vs total), assignment score, best quiz score, and attempt count. | Session matrix. |
| FR-ADM-ATT-002 | Staff SHALL open an attendance view **per student** showing every enrolled session with the same metrics. | Student rollup. |
| FR-ADM-ATT-003 | The attendance views SHALL be easy to navigate (filter, sort, search) and SHALL drill into a student's assignment/quiz review. | Navigability is an explicit goal. |
| FR-ADM-ATT-004 | Attendance views SHALL be exportable (Excel/CSV). | Offline reporting. |

## L. Audit / activity log

| ID | Requirement | Notes |
|---|---|---|
| FR-ADM-AUD-001 | Staff SHALL browse the activity log filtered by actor, action type, entity, date range, student, and session. | Investigation. |
| FR-ADM-AUD-002 | The log SHALL present each entry as who/what/when/where with a readable summary and drill-in to the affected entity. | Evidence-grade. |
| FR-ADM-AUD-003 | Assistants SHALL see a scoped subset; **Admins** SHALL see all, including sensitive views (e.g. who read what). | Tiered access. |

## M. Settings

| ID | Requirement | Notes |
|---|---|---|
| FR-ADM-SET-001 | Each staff user SHALL manage their own profile and password; a tenant-settings area SHALL exist as a placeholder for future SaaS configuration. | Future seam. |

---

➡️ Next: [03 — Student portal](03-functional-student-portal.md) · [04 — Non-functional](04-non-functional-requirements.md)
