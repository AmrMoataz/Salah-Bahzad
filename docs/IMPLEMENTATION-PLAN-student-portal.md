# Implementation Plan — Student Portal (Frontend + the student backend surface)

> Status: **Planned — not yet built** · Created 2026-06-21 · Scope: the **Salah Bahzad Student Portal** — a new Angular app (`apps/student-portal`) plus the modest set of student-facing backend endpoints the admin engagement deliberately left unbuilt. The shared engines (auth, enrollment, assignments, quizzes, video, audit) already exist from the admin plan's Phases 0–5C and are **reused as-is**.

Written against `docs/01-functional-platform-shared.md`, `docs/03-functional-student-portal.md`, `docs/05-secure-video-streaming-options.md`, `docs/06-database-and-event-sourcing-assessment.md`, and `docs/hls-transcoding-and-streaming.md`. The **design source of truth** is `.claude/Salah Bahzad Student Portal/Student Portal.html` (its sibling `Dropdown.jsx`/`support.js` are part of the prototype) and the shared design-system tokens — **not** `docs/tokens.*` / `docs/03-components.md` (deprecated for design). Requirement IDs (`FR-STU-*`, `FR-PLAT-*`, `NFR-*`) are cited throughout so every phase is traceable.

This plan **mirrors `docs/IMPLEMENTATION-PLAN-admin-portal.md`** in shape (foundation phase → vertical-slice delivery; per-phase frozen contract + 3 parallel-safe streams) and **follows the exact same conventions** as the existing code — see §3, which is the binding contract for "same architecture, backend and frontend."

---

## 1. Context — what already exists (the baseline)

The admin engagement (Phases 0–5C, latest commit `8742138`) built the platform backend **student-first**: the assessment, video, enrollment, registration, and auth engines are already API-driven by a **Student-role JWT** and proven live. The student portal is therefore **mostly a frontend build** on top of a backend that is ~80% there.

> **Assumption:** Phases 0–5C are committed and pushed (the baseline). If any 5C follow-up is uncommitted locally, push it before starting S-phases — this plan builds *on* that baseline, it does not re-do it.

### Already built and reused as-is (verified in `backend/src/SalahBahazad.Api/Endpoints/`)

| Student capability | Endpoint(s) | Built in | Requirement |
|---|---|---|---|
| Sign-in **infrastructure** — Firebase verify + JWT issue/refresh (reused; the *student lookup* path is new — the current handler is **staff-only**, see gaps) | `POST /api/auth/exchange`, `POST /api/auth/refresh` | Phase 0 | FR-PLAT-AUTH-002/006 |
| Silent refresh | `POST /api/auth/refresh` | Phase 0 | FR-PLAT-AUTH-006 |
| **Self-registration wizard submit** | `POST /api/students/register` (**`AllowAnonymous`**, `multipart/form-data`; verifies Firebase, uploads ID image to private R2 ≤5 MB, creates `Pending` student + terms consent, audited, transactional) | Phase 2 | FR-STU-REG-001..008 |
| Reference dropdowns for the wizard (grades / cities / regions cascade) | `GET /api/reference/*` (**`AllowAnonymous`**) | Phase 1 | FR-PLAT-TAX-005 |
| **Enroll by code** | `POST /api/enrollments/redeem` (`RequireStudent`) | Phase 4 | FR-STU-CAT-003/004/005 |
| **Assignment engine** (open, answer-one-at-a-time, resumable timer, hint, submit→auto-grade, review) | `/api/me/assignments` (3 routes, `RequireStudent`) | Phase 5B-1 | FR-STU-ASG-001..007 |
| **Quiz engine** (intro data, attempts, single-sitting, server timer, focus-loss telemetry, best-of) | `/api/me/quizzes` (5 routes, `RequireStudent`) + `QuizHub` (SignalR, JWT-auth, Redis backplane) | Phase 5B-2 | FR-STU-QZ-001..010 |
| **Secure video** (gate → one-time handoff → redeem signed manifest → AES key) | `/api/me/videos` (3 routes, `RequireStudent`) | Phase 5C | FR-STU-VID-001..004 |

### Gaps — the student **read** surface + a few touches (what this plan adds to the backend)

| Student capability | New endpoint (proposed, under `/api/me/*`) | Requirement | Phase |
|---|---|---|---|
| **Student sign-in path** (the current exchange handler is **staff-only** → 401s students) | `POST /api/auth/student/exchange` (new `ExchangeStudentFirebaseToken` command) — Firebase→Student lookup, enforce `Active`, **403 + machine `reason`** (+ rejection reason) for `Pending`/`Rejected`/`Inactive`, issue a Student-role JWT pair | FR-STU-AUTH-001, FR-PLAT-AUTH-005, FR-STU-REG-009 | S0 |
| **Full device binding** — consent-gated, server-issued **HttpOnly signed device-token cookie** + client fingerprint (secondary), one-device enforcement, "my bound device" read | bind/enforce inside the student exchange + bound-device on `GET /api/me/profile`; staff-clear already exists | FR-STU-DEV-001..003, FR-PLAT-DEV-001..006 | S0 |
| Browse the published catalogue (filter by grade/subject/specialization; price; prerequisite badge; per-student enrollment state) | `GET /api/me/catalogue` | FR-STU-CAT-001/002 | S2 |
| My enrolled sessions (progress + expiry countdown) | `GET /api/me/sessions` | FR-STU-SES-001 | S3 |
| One session's detail for the student (video playlist + per-video remaining access + lock state, materials signed reads, assignment/quiz status, prerequisite/quiz-gate status) | `GET /api/me/sessions/{id}` (+ `…/materials/{mid}/url`) | FR-STU-SES-002/003/004 | S3 |
| Student self-service profile (read/update personal info + parent phones + avatar; bound-device info) | `GET /api/me/profile`, `PUT /api/me/profile` | FR-STU-PRO-001/002/003 | S6 |

> Note: `/api/profile` already exists but is **staff-only** (Settings → own profile). The student profile is a **new, separate** `/api/me/profile` group, scoped to the caller's JWT (no IDOR surface), to match the `/api/me/*` family.

---

## 2. Decisions captured

| Decision | Choice |
|---|---|
| Engagement shape | **Frontend-led vertical slices** on top of the existing backend; add only the student *read* surface + two small auth touches |
| New Angular app | `apps/student-portal` in the **same Nx workspace** (`frontend/`), reusing `@sb/shared/ui` + `@sb/shared/data-access` |
| New feature libs | `libs/student-portal/feature-*` tagged `scope:student-portal` / `type:feature` — identical pattern to `libs/admin-portal/feature-*` |
| Design source of truth | `.claude/Salah Bahzad Student Portal/Student Portal.html` + the shared DS tokens (same token names as admin) |
| Local run | The Aspire **AppHost** runs everything on F5 — Postgres + Redis + MinIO + API + **both** Angular apps; `student-portal` is a second `AddNpmApp` on **:4300** (admin stays :4200) |
| Backend additions | New queries under `/api/me/*` (`RequireStudent`), Clean Architecture + CQRS + source-gen Mediator — **identical** conventions to the existing endpoints |
| Video | **Deep-link only — no in-browser playback** (your call, §8.4): **Play** fires the gate then hands off to the native/desktop app via `salah-bahazad://…&handoff=<code>` (real OS black-out everywhere video plays — the existing desktop app already does this); if no app is installed, show an **install prompt**. **Mobile-web video is intentionally blocked until the mobile app ships** — this deliberately supersedes FR-STU-VID-005's watermarked-browser interim in favour of the strongest protection (no AES key ever reaches a browser) |
| Sequencing | One short foundation phase (S0) → vertical slices S1–S6, each demoable and independently shippable |

Open items to confirm are in §8.

---

## 3. Conventions to follow **exactly** (binding — this is the "same architecture" contract)

Every phase MUST conform to the two existing convention docs. Nothing here is new policy; it is the explicit checklist the user asked for so the student portal is indistinguishable in style from the admin portal.

### 3.1 Backend — mirror `backend/CLAUDE.md`

- **Clean Architecture + CQRS** with the source-generated **Mediator** (Artem Shykhermanov). Dependencies point inward: Api → Application → Domain; Infrastructure implements Domain interfaces. No EF/HTTP/Hangfire types in Application or Domain.
- Each query lives in `Application/Features/<Aggregate>/Queries/<Name>/` with its `…Validator` co-located. **Manual** `.ToDto()` mapping — never map in a handler body, never add a mapping library.
- New student endpoints live under **`/api/me/*`** and use the **`RequireStudent()`** endpoint filter (anon → 401, staff → 403) — exactly like `/api/me/assignments|quizzes|videos`. The student id + tenant come from the **JWT**, never a URL id (no IDOR surface, `NFR-SEC-007`).
- **EF global query filter** on `TenantId` does the isolation — never write a per-handler `Where(x => x.TenantId == …)`. Catalogue/my-sessions reads are tenant-scoped automatically; cross-tenant isolation is covered by an integration test (`NFR-SEC-010`).
- Any **state-changing** action emits an `AuditEntry` (the interceptor + explicit events already do this for redeem/assignment/quiz/video). Pure reads (catalogue, my-sessions, profile GET) are not audited; **ID-image / sensitive signed-URL reads are** (mirrors existing material/key behaviour).
- **Scalar/OpenAPI** annotations on every new route (`.WithName/.WithSummary/.Produces<…>`), **xUnit v3 + Testcontainers** integration tests, `FluentAssertions`, naming with no legacy typos (`Specialization`, `Enrollment`).

### 3.2 Frontend — mirror `frontend/CLAUDE.md`

- **Nx libs** at `libs/student-portal/feature-<name>` with `project.json` tags `["scope:student-portal","type:feature"]`, `prefix: "sb"`, and a `@nx/jest` test target — byte-for-byte the shape of `libs/admin-portal/feature-dashboard/project.json`.
- **Path aliases** added to `frontend/tsconfig.base.json`: `@sb/student-portal/feature-<name> → libs/student-portal/feature-<name>/src/index.ts`.
- **Module boundaries** (ESLint `@nx/enforce-module-boundaries`): `scope:student-portal` may depend on `scope:shared` only — **never** import a `scope:admin-portal` lib (and vice-versa). Shared code goes to `@sb/shared/*`; auth/refresh/interceptor patterns are **ported**, not cross-imported.
- **Angular v20+**: standalone components, `ChangeDetectionStrategy.OnPush`, signal `input()`/`output()`/`model()`, `computed()`/`effect()`, `inject()` (no constructor DI), native control flow (`@if`/`@for`/`@switch`), typed reactive forms, `ControlValueAccessor` for custom controls. AOT build (`npx nx build student-portal`) is the quick gate (it type-checks templates).
- **Design**: mirror the shared DS tokens into `apps/student-portal/src/styles/_design-tokens.scss` (same canonical names — `--sb-font-sans`, `--sb-body-md-size`, `--sb-timing`, `--sb-primary`, `--sb-space-4`, …); never override token values in component styles. Outline icons only, rendered via `DomSanitizer.bypassSecurityTrustHtml` for data-driven inline `<svg>` (Angular strips `<svg>` from plain `[innerHTML]`). Assets mirror `.claude/Salah Bahzad Student Portal/assets/` byte-for-byte.
- Cite `FR-*` / `NFR-*` in commits/PRs/tests.

### 3.3 Intentional non-implementations (mirror the admin plan's discipline)

- The prototype's **`sessionLayout` demo prop** (`spotlight | cards | rail`, default `spotlight`) is a designer toggle to show three "My Sessions" treatments. **Confirmed with the user: implement ONLY the `spotlight` layout** — `cards` and `rail` are not built and the enum is dropped, exactly as the admin plan dropped the demo-only "Viewing as Teacher/Assistant" switcher.
- The **"Request a spot"** modal (offline-session code request) is **deferred — not built now** (confirmed): it has no backend and adding one is out of scope for this engagement. The catalogue's only enroll path is code redemption.
- **OS screenshot/recording black-out** and the **in-player watermark** are the native-app engagement (FR-PLAT-VID-004/005); per §8.4 the browser portal ships **no** video player at all (deep-link only), so there is no capturable browser interim and no AES key in the browser.

---

## 4. Workspace & architecture plan (the concrete shape)

### 4.1 Nx additions

```
frontend/
├─ apps/
│   ├─ admin-portal/                         (exists)
│   └─ student-portal/                       NEW  — shell app: routes, app.config, proxy, styles/_design-tokens.scss
├─ libs/
│   ├─ shared/
│   │   ├─ ui/                               (exists — reuse Button, Input, Modal, Tag, Alert, Avatar, Checkbox, …)
│   │   ├─ data-access/                      (exists — reuse AuthStore pattern, http, interceptors, models)
│   │   └─ util/                             (exists)
│   ├─ admin-portal/ …                       (exists — do NOT import from student-portal)
│   └─ student-portal/                       NEW
│       ├─ ui/                               NEW  — student-specific DS pieces (see 4.2)
│       ├─ feature-shell/                    sidebar + mobile drawer + bottom-nav + header (responsive)
│       ├─ feature-auth/                     Firebase sign-in, device-link, status screen, guards
│       ├─ feature-catalogue/                catalogue + enroll/request modals
│       ├─ feature-sessions/                 my-sessions + session detail + video Play flow
│       ├─ feature-assessment/               assignment runner/review + quiz intro/runner/results
│       └─ feature-profile/                  profile + change-password/device-reset/logout modals
```

`apps/student-portal` proxy **forwards `/api` and `/hubs`** to the API (the admin proxy intentionally did *not* forward `/hubs`; the student portal **is** the `QuizHub`'s intended consumer, so it must).

**AppHost / local run:** the Aspire AppHost (`backend/src/SalahBahazad.AppHost/Program.cs`) gets a **second `AddNpmApp`** beside the admin one, so a single **F5 runs both portals** plus Postgres/Redis/MinIO/API:
```csharp
builder.AddNpmApp("student-portal", "../../../frontend", "start:student-portal")
    .WithReference(api)                 // injects services__api__http__0 for the proxy
    .WithEnvironment("BROWSER", "none")
    .WithHttpEndpoint(targetPort: 4300) // admin is 4200; student is 4300
    .ExcludeFromManifest();
```
Add a `start:student-portal` script to `frontend/package.json` (`nx serve student-portal --port 4300`) and a `proxy.conf.js` for the student app that forwards `/api` **and** `/hubs` off Aspire's `services__api__http__0` — mirroring `admin-portal`'s setup exactly.

### 4.2 Design-system reconciliation (S0 task)

Audit `@sb/shared/ui` against the student prototype's component set. **Reuse** the shared ones already built for admin (Button + variants, Input, Checkbox, Modal `size="confirm"`, Tag, Alert, Avatar, Dropdown, FileUpload, Progress). **Add** the student-specific pieces to `libs/student-portal/ui` (or promote to `@sb/shared/ui` if genuinely reusable): `SessionThumb` (the session-thumbnail card — was its own file, now inlined in the prototype), `CodeInput` (segmented 16-char with paste), `Timer` (bar variant, **server-synced via QuizHub**, `warnAt`, `onComplete`), `Chip` (state pill), **circular** `Progress`, and the mobile **bottom-nav** with the center "Redeem" FAB.

### 4.3 Backend additions (Application/Api only — no new aggregates)

All new work is **read queries** + DTOs + endpoints; no schema change, no migration (the entities — `Session`, `Enrollment`, `EnrollmentVideoAccess`, `SessionMaterial`, `UserAssignment`, `UserQuiz`, `Attendance`, `Student`, `StudentDevice` — all exist). Files land in `Application/Features/<Aggregate>/Queries/…` and `Api/Endpoints/MeCatalogueEndpoints.cs` / `MeSessionsEndpoints.cs` / `MeProfileEndpoints.cs`, each an `IEndpointGroup` exactly like the existing ones.

---

## 5. The phased plan

Each phase: **Goal · Backend · Frontend · Design anchor (prototype § section) · Reqs · Exit**. (Anchors cite the prototype's named sections — `CATALOGUE`, `MY SESSIONS`, … — not line numbers, since the source `.html` is re-exported.) S0 is horizontal foundation; S1–S6 are vertical slices. Per the admin pattern, each slice that introduces new endpoints gets a **frozen contract** in `docs/contracts/` and **three parallel-safe streams** (`-backend` / `-frontend` / `-wiring`); frontend-only slices (S4, most of S5) skip the backend stream.

### S0 — Foundation: app shell, auth, device-link, design system
> **Status: ✅ MET (2026-06-21)** — backend + frontend built; wiring proven live on the Aspire stack, **8/9 checks, zero
> contract drift** (the 9th, the browser shell walkthrough, is pending a user visual check). Student sign-in exchange,
> one-device binding (bind/reuse/`device_not_recognized`/staff-clear→re-bind), status gates, role-aware refresh, and the
> "everything is audited" trail all verified. See `IMPLEMENTATION-PLAN-student-s0-{backend,frontend,wiring}.md`.

**Goal:** a student can sign in via Firebase, the guarded responsive shell renders, refresh + device binding work.
- **Frontend:** scaffold `apps/student-portal`; `feature-shell` (sidebar ≥tablet, mobile drawer + scrim, bottom-nav with Redeem FAB, header with crumb/title + notifications bell + user chip — prototype § APP shell: sidebar + drawer + header + bottom-nav); `feature-auth` (Firebase email/password + Google → `POST /api/auth/student/exchange`, send the device fingerprint [the HttpOnly device-token cookie rides automatically], store the JWT pair, port the admin refresh interceptor into shared/data-access, functional route guards, default-deny redirect); device-link consent prompt on first sign-in; **status screen** for `Pending`/`Rejected: <reason>` (prototype § AUTH: REGISTER pending state + AUTH: LOGIN). Mirror tokens; reconcile DS (§4.2). **Run under the AppHost:** add the second `AddNpmApp("student-portal", …, "start:student-portal")` on `:4300` (full snippet in §4.1) so F5 launches it beside the admin app.
- **Backend (real work — the current exchange is staff-only):** add a dedicated **student sign-in path** — `ExchangeStudentFirebaseToken` command + `POST /api/auth/student/exchange` (`AllowAnonymous`, rate-limited, returns a Student-role JWT pair + student info), verifying Firebase → **Student** lookup, enforcing `StudentStatus == Active` and returning a readable **`403 { reason }`** for `Pending`/`Rejected`(+reason)/`Inactive` (`FR-PLAT-AUTH-005`, `FR-STU-REG-009`). Keep the staff exchange untouched (separate surfaces, `FR-PLAT-AUTH-004`). **Full device binding** (`FR-PLAT-DEV-001..006`): on consent-bind, issue a long-lived **HttpOnly, signed device-token cookie** and store a client **fingerprint** as the secondary signal (`StudentDevice` — one active per student, history retained); the JWT carries `deviceId`; sign-in or content access from an unbound/mismatched device is refused with the "device not recognised — contact support" reason; the staff-clear reset already exists.
- **Reqs:** FR-STU-AUTH-001, FR-STU-DEV-001..003, FR-PLAT-AUTH-002/005/006, FR-STU-RWD-001/002, FR-STU-A11Y-001.
- **Exit:** a student signs in via the new student exchange and lands in the shell on phone/tablet/desktop; first sign-in with no bound device prompts consent → an HttpOnly device token binds; a second device is refused with the right message; `Pending`/`Rejected` accounts are blocked with the readable reason; refresh works; guards redirect anonymous users.

### S1 — Registration & onboarding
**Goal:** a prospective student completes the wizard and sees the pending state.
- **Frontend:** `feature-auth` register wizard — Step 1 manual (name/email/phone/password) or Google (prefill name/email, ask phone), Step 2 (school, grade, city→region cascade, two parent phones [≥1 required], ID upload ≤5 MB, terms checkbox), submit → success/pending (prototype § AUTH: REGISTER wizard). Dropdowns from `GET /api/reference/*`; submit `multipart/form-data` → `POST /api/students/register`.
- **Backend:** **exists**; add only the status-read touch if S0 didn't (`FR-STU-REG-009`).
- **Contract:** `docs/contracts/student-s1-registration.md` (documents the existing register multipart shape + the status read).
- **Reqs:** FR-STU-REG-001..009, FR-PLAT-AUTH-003, NFR-PRIV-001/003.
- **Exit:** a new account is created `Pending` with ID image in private R2 + terms recorded + a registration `AuditEntry`; the student cannot sign in until approved; rejection reason renders.

### S2 — Catalogue & enrollment
**Goal:** browse published sessions and enroll by code.
- **Backend:** **new** `GET /api/me/catalogue` (`RequireStudent`) — published, tenant-scoped sessions; filter by grade/subject/specialization; returns price, description, prerequisite badge, and the caller's enrollment state per session. (Redeem already exists.)
- **Frontend:** `feature-catalogue` — header + spec filter + cards grid (`SessionThumb`, Tag, prereq badge, price, enroll CTA) + mascot empty state (prototype § CATALOGUE); **enroll modal** (`CodeInput` segmented + paste → `POST /api/enrollments/redeem`, success → go to session) (§ Enroll modal). The **request-a-spot** modal (§ Request a spot modal) is **not built** (deferred, §3.3).
- **Contract:** `docs/contracts/student-s2-catalogue-enroll.md`.
- **Reqs:** FR-STU-CAT-001..005, FR-PLAT-SES-008, FR-PLAT-ENR-001/006/007.
- **Exit:** catalogue filters live; redeem moves a session to My Sessions, consumes the code, provisions assignment/quiz/video access; every failure (invalid/used/disabled/price-mismatch/prereq-unmet/already-enrolled) shows a specific message.

### S3 — My sessions, session detail & secure video
**Goal:** the enrolled-content hub, including the Play handoff. (Largest slice.)
- **Backend:** **new** `GET /api/me/sessions` (enrolled list + progress + expiry countdown) and `GET /api/me/sessions/{id}` (video playlist with per-video remaining access + lock state, materials with student-gated signed-URL reads, assignment/quiz status, prerequisite + quiz-gate status). Video gate/redeem/key **exist**.
- **Frontend:** `feature-sessions` — **My Sessions** (`spotlight` layout only: summary counts, "jump back in" hero, divided list, expiry chips — prototype § MY SESSIONS: SPOTLIGHT) + **Session detail** (hero band with circular progress, mascot-forward gate banner, video playlist with lock/access badges + Play, materials, assignment/quiz entry cards — § SESSION DETAIL). **Play flow (deep-link only):** `POST /api/me/videos/{id}/playback` → on success open `salah-bahazad://stream?...&handoff=<code>` to hand off to the native/desktop app (which authenticates, calls redeem + key, and plays with OS black-out + watermark); if the app isn't installed, show an **install prompt** (store/download links). **No in-browser HLS player is built** (§8.4); surface the six gate `reason`s as readable failures and show lock/access/expiry on the playlist.
- **Contract:** `docs/contracts/student-s3-my-sessions-video.md` (new reads; references `phase5c-video-gate.md` for the gate).
- **Reqs:** FR-STU-SES-001..004, FR-STU-VID-001..005, FR-PLAT-VID-001..007, FR-PLAT-ENR-003.
- **Exit:** My Sessions shows real progress + expiry; Play fires the gate (decrement + audit) and deep-links to the app, or shows an install prompt when the app is absent; expired/exhausted/locked/quiz-required states show the right reason; assignments stay reachable after expiry. (No in-browser playback — proven via the gate + deep-link, not a browser player.)

### S4 — Assignments (frontend-only)
**Goal:** do and review homework.
- **Backend:** **none** — `/api/me/assignments` exists.
- **Frontend:** `feature-assessment` — runner (save-&-exit, accumulated resumable timer, progress, one-question-at-a-time MCQ with LaTeX/image render, per-question video hint, prev/next, auto-submit on last) + review (your vs correct answers + score) (prototype § ASSIGNMENT RUNNER).
- **Reqs:** FR-STU-ASG-001..007, FR-PLAT-ASG-002/003/004/006/007.
- **Exit:** answers persist incrementally; time accumulates across visits; completing auto-grades and writes attendance; review renders.

### S5 — Quizzes (proctored)
**Goal:** the single-sitting quiz with the live server timer.
- **Backend:** **none** — `/api/me/quizzes` + `QuizHub` exist; the student app connects to the hub (JWT via the access-token-on-hub-path scheme, not query-string).
- **Frontend:** `feature-assessment` — quiz intro (time/attempts/best, randomised + pass-mark rules, "one sitting only" alert), runner (sticky server-synced `Timer` bar `warnAt=60`, question dots, focus-loss detection → telemetry + on-screen warning, **forfeit-on-disconnect/navigation** via `beforeunload` + hub disconnect, leave-quiz confirm modal, auto-submit on timer/manual submit), results (pass/fail mascot, score ring, this-attempt + best-of) (prototype § QUIZ INTRO / QUIZ RUNNER / QUIZ RESULTS + Leave-quiz modal).
- **Reqs:** FR-STU-QZ-001..010, FR-PLAT-QZ-001..010 (best-of, `≥` pass, focus-loss-recorded-not-forfeit, forfeit-on-leave, server timer).
- **Exit:** an attempt randomises; the timer is authoritative; leaving forfeits with zero (consumes the attempt); focus-loss is logged not forfeited; passing (`≥` min) unlocks the session's videos; best-of is shown.

### S6 — Profile
**Goal:** self-service account management.
- **Backend:** **new** `GET /api/me/profile` + `PUT /api/me/profile` (personal info, parent phones, avatar; bound-device info). Password is Firebase self-service.
- **Frontend:** `feature-profile` — header band, personal-info form (grade disabled), parent numbers, bound-device card + reset modal, security (change-password modal → Firebase, sign-out confirm) (prototype § PROFILE + Change-password / Device-reset modals).
- **Contract:** `docs/contracts/student-s6-profile.md`.
- **Reqs:** FR-STU-PRO-001..003, FR-PLAT-AUTH-009, FR-STU-DEV-003.
- **Exit:** profile reads/saves; bound device + bind date shown; change-password defers to Firebase; reset-device requests the staff-clear path; sign-out clears the session.

---

## 6. Cross-cutting (every phase)

- **Responsiveness is first-class** (`FR-STU-RWD-001/002`): phone/tablet/desktop layouts (sidebar↔drawer↔bottom-nav), touch-sized targets — stronger than the admin portal's RWD bar.
- **Accessibility** (`FR-STU-A11Y-001`, `NFR-A11Y-*`): keyboard-navigable, screen-reader-labelled, WCAG 2.1 AA.
- **Audit & tenancy:** every new read is tenant-filtered by the global EF filter; sensitive signed-URL reads are audited; cross-tenant isolation tests on each new endpoint (`NFR-SEC-010`).
- **Tests as features land** (`NFR-MAINT-001`): backend integration tests (Testcontainers) for catalogue/my-sessions/profile + IDOR/tenant; frontend Jest specs per feature lib; AOT build green; OpenAPI current.
- **No drift:** each contract frozen before parallel work; the `-wiring` stream proves the slice live on the Aspire stack (now including `/hubs` for the student app).

## 7. Out of scope / deferred
**In-browser video playback** (the FR-STU-VID-005 watermarked interim) — intentionally **not built** (§8.4); mobile-web video waits for the native app. The native-app OS black-out + in-player watermark (`FR-PLAT-VID-004/005`) are the native engagement. Also: multi-bitrate ABR; Cloudflare CDN in front of R2 (deployment); notifications (`§14` backlog); the "Request a spot" offline-code flow (no backend); the `cards`/`rail` My-Sessions layouts (demo-only). All recorded so nothing is silently dropped.

## 8. Resolved & open items
1. ✅ **Student sign-in is real backend work, not a touch.** Confirmed by reading the handler — `ExchangeFirebaseTokenHandler` looks up `db.Staff` only and 401s everything else. S0 adds a dedicated student exchange (`POST /api/auth/student/exchange`) with the `Active`-status gate + readable `403 { reason }`.
2. ✅ **Full device binding in S0** (confirmed): consent-gated, server-issued **HttpOnly signed device-token cookie** + client fingerprint (secondary signal, `FR-PLAT-DEV-005`), one-device enforcement, staff-clear reset.
3. ✅ **"Request a spot" not built** (confirmed): deferred, no backend.
4. ✅ **Video — Option C, deep-link only** (your call): video plays **only** in the black-out-capable native/desktop app; the portal fires the gate and deep-links (install prompt when the app is absent). **No in-browser player and no browser watermark are built.** This deliberately supersedes FR-STU-VID-005's watermarked-browser interim — trading mobile-web availability for the strongest protection: no AES key ever reaches a browser, and recordings come out **black everywhere video plays**. Mobile-web video unlocks when the mobile app ships. *(Rationale captured: a browser watermark would have been traceable but only casual deterrence — a DOM overlay is strippable and the browser would hold the decrypt key; Option C avoids that surface entirely.)*

---

### Per-phase docs to produce (as each phase starts, mirroring the admin plan)
`docs/contracts/student-s{1,2,3,6}-*.md` (frozen contracts) and `docs/IMPLEMENTATION-PLAN-student-s{0..6}-{backend,frontend,wiring}.md` — same naming and three-stream split as `IMPLEMENTATION-PLAN-phase5c-*`. S4 and S5 reuse the existing engine contracts (`phase5b1-assignments-attendance.md`, `phase5b2-quizzes.md`, `phase5c-video-gate.md`) and need only `-frontend` (+ `-wiring`) streams.
