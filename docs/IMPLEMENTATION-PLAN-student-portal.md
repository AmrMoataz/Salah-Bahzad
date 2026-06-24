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
| **Personalized Home — a weekly study plan** (KPI roll-up + a current-frontier "what to do next" step list + recently enrolled), derived from existing state and Redis-cached; **net-new** beyond the original "Home = catalogue" (screen #5) | `GET /api/me/plan` | FR-STU-SES-001, FR-PLAT-ENR-003/-007, FR-PLAT-QZ-008 | Home (post-S6) — **Met (wiring DONE 2026-06-22, 10/10 live checks on the Aspire stack, zero drift)** — `docs/contracts/student-home-weekly-plan.md` + `IMPLEMENTATION-PLAN-student-home-{backend,frontend,wiring}.md` |

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
> **Status: ✅ MET (2026-06-21)** — backend (the one new anonymous `GET /api/reference/grades?tenantSlug=`) + frontend
> (the two-step register wizard) built; **wiring proven live on the Aspire stack, all scripted checks green (grades
> 200/400/404 + LIVE cross-tenant isolation & soft-delete exclusion, cascade, register `201` Pending + ID image in
> MinIO + `StudentRegistered` audit, errors 409/404/400/429, and the full register→pending→reject→approve→S0-exchange
> `200`+device-bind loop), ZERO product drift.** The browser visual walkthrough is the only user step (as S0 #9). See
> `docs/IMPLEMENTATION-PLAN-student-s1-{backend,frontend,wiring}.md` + `docs/contracts/student-s1-registration.md`.
> Doc correction logged: terms consent is stored on the student row (`TermsVersion`/`TermsAcceptedAtUtc`), not a
> `terms_acceptances` table. **Grounding correction (planning):** grades are tenant-owned + staff-permissioned
> (`/api/taxonomy/grades`), so S1 added **one** anonymous reference read; the "status read" needed **no** new endpoint
> (S0's exchange `403 { reason }` covers it).

**Goal:** a prospective student completes the wizard and sees the pending state.
- **Frontend:** `feature-auth` register wizard — Step 1 manual (name/email/phone/password) or Google (prefill name/email, ask phone), Step 2 (school, grade, city→region cascade, two parent phones [≥1 required], ID upload ≤5 MB, terms checkbox), submit → success/pending (prototype § AUTH: REGISTER wizard). Dropdowns from `GET /api/reference/*`; submit `multipart/form-data` → `POST /api/students/register`.
- **Backend (one small addition — user-confirmed 2026-06-21):** the register POST + city/region reads **exist**; add **`GET /api/reference/grades?tenantSlug=<slug>`** (anonymous, tenant-scoped, `IgnoreQueryFilters` + explicit `TenantId` filter) so the anonymous wizard can populate its grade dropdown — `/api/taxonomy/grades` is staff-only. **No status-read touch** (S0's `POST /api/auth/student/exchange` already returns `403 account_pending`/`account_rejected`+reason, `FR-STU-REG-009`). No migration.
- **Contract:** `docs/contracts/student-s1-registration.md` (freezes the existing register multipart shape + the new grades read; documents that the status read is S0's exchange `403`).
- **Reqs:** FR-STU-REG-001..009, FR-PLAT-AUTH-003, NFR-PRIV-001/003.
- **Exit:** a new account is created `Pending` with ID image in private R2 + terms recorded + a registration `AuditEntry`; the student cannot sign in until approved; rejection reason renders.

### S2 — Catalogue & enrollment
> **Status: ✅ MET (2026-06-21)** — backend (the one new `GET /api/me/catalogue`) + frontend (`feature-catalogue` +
> enroll modal) built; **wiring proven live on the Aspire stack — all 8 scripted checks green, ZERO contract drift**
> (the 9th, the browser catalogue walkthrough, is the user's visual step, as S0 #9 / S1 #7 — and was in fact partly
> exercised live: the user ran a concurrent admin-mint→student-redeem→card-flip in the browser mid-run). Verified:
> published-only + the exact `CatalogueSessionDto` shape + DESC-by-`CreatedAtUtc` + a thumbnail **signed-URL that
> resolves** (`200 image/png`) + empty-tenant `[]`; the four filters narrow (spec/grade/subject/search, case-insensitive,
> AND-combined); `enrollmentState` NotEnrolled→Enrolled(+expiry)→**Expired (DERIVED via past `ExpiresAtUtc`, `Status`
> left `Active`)**→Refunded; `prerequisiteSatisfied` false→true around completing the prereq assignment (vacuous-true
> when the prereq has no questions); **tenant isolation both directions** (seeded a self-contained tenant B); per-caller
> IDOR (one session reads three different states across three students) + `401` anon/`403` staff/`200` student; the full
> **mint→redeem `201`+`Location`+`EnrollmentDto` → code `Used`+enrollment+2 video-access counters+payment `Completed`+
> attendance shell+assignment(+quiz) snapshot+`CodeRedeemed`/`ActorType=Student` audit → catalogue card flips to
> `Enrolled`** loop, including the **prereq gate end-to-end** (`409` "Complete the prerequisite assignment first." →
> satisfy prereq → `201`); and the redeem error ladder (the **six `409` detail strings verbatim** + two `400`s). One
> non-blocking finding (Phase-4 / cross-cutting, **not** S2): the redeem audit's `Portal` is read from an `X-Portal`
> request header that **neither frontend sends**, so `Portal` is null for redeem (the backend honours it when present —
> proven — and the security-relevant `ActorType=Student` is correct). The catalogue read + reused redeem engine match
> the frozen contract field-for-field. See `IMPLEMENTATION-PLAN-student-s2-{backend,frontend,wiring}.md` +
> `docs/contracts/student-s2-catalogue-enroll.md`.

**Goal:** browse published sessions and enroll by code.
- **Backend:** **new** `GET /api/me/catalogue` (`RequireStudent`) — published, tenant-scoped sessions; filter by grade/subject/specialization; returns price, description, prerequisite badge, and the caller's enrollment state per session. (Redeem already exists.)
- **Frontend:** `feature-catalogue` — header + spec filter + cards grid (`SessionThumb`, Tag, prereq badge, price, enroll CTA) + mascot empty state (prototype § CATALOGUE); **enroll modal** (`CodeInput` segmented + paste → `POST /api/enrollments/redeem`, success → go to session) (§ Enroll modal). The **request-a-spot** modal (§ Request a spot modal) is **not built** (deferred, §3.3).
- **Contract:** `docs/contracts/student-s2-catalogue-enroll.md`.
- **Reqs:** FR-STU-CAT-001..005, FR-PLAT-SES-008, FR-PLAT-ENR-001/006/007.
- **Exit:** catalogue filters live; redeem moves a session to My Sessions, consumes the code, provisions assignment/quiz/video access; every failure (invalid/used/disabled/price-mismatch/prereq-unmet/already-enrolled) shows a specific message.

### S3 — My sessions, session detail & secure video
> **Status: ✅ Met (2026-06-21)** — proven live on the Aspire stack via the `:4300` proxy + a direct Student JWT,
> **9/9 scripted checks green, zero functional drift** (browser walkthrough #10 is the user's step). My-sessions
> list/DESC/thumbnail-resolves; progress **derived** from the gate decrement (Play → `videosWatched` 0→1, `progressPercent`
> 0→100, `Completed`); per-video `lockState` proven across **Playable / QuizLocked / Expired / Exhausted / NotReady** +
> `gateState` Open/QuizRequired/Expired; the full **gate reason ladder** (`not_ready` 409, `not_enrolled` /
> `enrollment_expired` / `quiz_required` / `no_views_remaining` 403) with `AccessRemaining` decrement + a
> `VideoPlaybackStarted`/`Student` audit row; material signed-URL **200 + resolves + not-audited**, **404** when
> refunded/foreign; the 404 IDOR + tenant boundary; 401/403/200 auth; per-caller scoping. NOT committed; run log in
> `IMPLEMENTATION-PLAN-student-s3-wiring.md`. *(Backend had 2 compile blockers fixed pre-run; 3 minor §D.2 detail-string
> copy nits are 5C-gate-owned, rendered verbatim — non-blocking.)* Two new student reads
> (`GET /api/me/sessions`, `GET /api/me/sessions/{id}`) + one material signed-URL read; the **Phase-5C video gate is
> reused as-is** (the browser calls only `POST /api/me/videos/{id}/playback` then deep-links the handoff — redeem/key
> stay the native app's). See `docs/contracts/student-s3-my-sessions-video.md` +
> `docs/IMPLEMENTATION-PLAN-student-s3-{backend,frontend,wiring}.md`.

**Goal:** the enrolled-content hub, including the Play handoff. (Largest slice.)
- **Backend:** **new** `GET /api/me/sessions` (enrolled list + progress + expiry countdown) and `GET /api/me/sessions/{id}` (video playlist with per-video remaining access + lock state, materials with student-gated signed-URL reads, assignment/quiz status, prerequisite + quiz-gate status). Video gate/redeem/key **exist**.
- **Frontend:** `feature-sessions` — **My Sessions** (`spotlight` layout only: summary counts, "jump back in" hero, divided list, expiry chips — prototype § MY SESSIONS: SPOTLIGHT) + **Session detail** (hero band with circular progress, mascot-forward gate banner, video playlist with lock/access badges + Play, materials, assignment/quiz entry cards — § SESSION DETAIL). **Play flow (deep-link only):** `POST /api/me/videos/{id}/playback` → on success open `salah-bahazad://stream?...&handoff=<code>` to hand off to the native/desktop app (which authenticates, calls redeem + key, and plays with OS black-out + watermark); if the app isn't installed, show an **install prompt** (store/download links). **No in-browser HLS player is built** (§8.4); surface the six gate `reason`s as readable failures and show lock/access/expiry on the playlist.
- **Contract:** `docs/contracts/student-s3-my-sessions-video.md` (new reads; references `phase5c-video-gate.md` for the gate).
- **Reqs:** FR-STU-SES-001..004, FR-STU-VID-001..005, FR-PLAT-VID-001..007, FR-PLAT-ENR-003.
- **Exit:** My Sessions shows real progress + expiry; Play fires the gate (decrement + audit) and deep-links to the app, or shows an install prompt when the app is absent; expired/exhausted/locked/quiz-required states show the right reason; assignments stay reachable after expiry. (No in-browser playback — proven via the gate + deep-link, not a browser player.)

### S4 — Assignments (runner + answer-key review)
> **Status: ✅ MET (2026-06-22)** — backend (the one new review read) + frontend (`feature-assessment` runner +
> answer-key review) built; **wiring proven live on the Aspire stack, 9/9 scripted checks green, ZERO drift** (the 10th,
> the browser walkthrough, is the user's visual step, as S0 #9 / S1 #7 / S2 #9 / S3 #10). Verified: engine load with
> **no `isCorrect`**; **answer-through → auto-grade on the last answer** (`System` actor → `Status Completed` +
> `attendance.AssignmentScore` + the S3 card flip, re-answer `409`); behaviour events + accrued `TimeSpentSeconds`
> (`Answered` rejected); the new **`GET /api/me/assignments/{id}/review`** → answer key + score (`percent =
> round(100·marks/max)`, per-option & per-question `isCorrect`, `selectedOptionId` echoed); **`403 assignment_in_progress`**
> on an in-progress assignment; **`404`** IDOR/unknown; **401/403/200** auth; the **`isCorrect` split** (runner hides /
> review exposes) live; review **not audited**; cross-tenant covered by `MyAssignmentReviewApiTests`. Seeded two
> rich-LaTeX sessions for the browser step. See `docs/IMPLEMENTATION-PLAN-student-s4-{backend,frontend,wiring}.md` +
> `docs/contracts/student-s4-assignments.md`. **NOT committed.**
>
> **Grounding correction (planning):** §S4 originally read *"Backend: none."* That was an oversight — the
> student `StudentAssignmentDto` **deliberately hides `isCorrect`** and the only correctness-exposing endpoint is the
> **staff** `GET /api/review/assignments/{enrollmentId}` (`AttendanceRead`-gated), so **`FR-STU-ASG-007`** ("review their
> answers vs. correct answers") had **no** student path. S4 adds **one** new read —
> **`GET /api/me/assignments/{assignmentId}/review`** (`RequireStudent`, gated to the caller's own `Completed` assignment,
> the only student surface that exposes `isCorrect`) — so the slice is the standard **3-stream** split (backend +
> frontend + wiring), exactly like S1's lone anonymous grades read in an otherwise frontend-led slice. The three solving
> routes (`/api/me/assignments`, Phase 5B-1) are **reused as-is**.

**Goal:** do and review homework.
- **Backend (one small addition — user-confirmed 2026-06-21):** **new** `GET /api/me/assignments/{assignmentId}/review`
  (`RequireStudent`) → `StudentAssignmentReviewDto` (per-question/-option `isCorrect` + score), gated to the caller's own
  **`Completed`** assignment (`403 assignment_in_progress` otherwise, `404` IDOR). The three solving routes
  (`/api/me/assignments` — load/answer/events) **exist** (5B-1) and are reused. **No migration.**
- **Frontend:** **new** `feature-assessment` lib — runner (save-as-you-go, accumulated resumable timer, progress, one-
  question-at-a-time MCQ with LaTeX/image render, per-question hint, prev/next, **auto-submit on the last answer** — no
  inline results screen) + **answer-key review** (your vs correct answers + score) (prototype § ASSIGNMENT RUNNER — the
  review screen is **new**; the prototype has none). Replaces S3's dead-end Assignment CTA.
- **Contract:** `docs/contracts/student-s4-assignments.md`.
- **Reqs:** FR-STU-ASG-001..007, FR-PLAT-ASG-002/003/004/006/007/008.
- **Exit:** answers persist incrementally; time accumulates across visits; completing auto-grades and writes attendance;
  the answer-key review renders (your vs correct + score) for the caller's own **completed** assignment.

### S5 — Quizzes (proctored)
> **Status: ✅ MET (2026-06-22)** — backend (the new review read + the additive attempt `id`) + frontend (the quiz
> intro/runner/results + the new per-attempt review + the SignalR hub client) built; **wiring proven live on the Aspire
> stack — S5's own surface with ZERO drift** (intro `id`, start/answer/submit, the `≥`-pass boundary 80==80, best-of=max
> 80→100, focus-not-forfeit, **live hub forfeit-on-disconnect** → `Forfeited`/0, the new `GET …/attempts/{id}/review`
> with per-option + per-question `isCorrect` / `403 quiz_attempt_in_progress` / `404` IDOR / 401-403-200 / not-audited /
> the isCorrect split, and **pass→videos-unlock**). The browser walkthrough (#15) is the user's step. **One pre-existing
> 5B-2 finding, now FIXED (2026-06-22):** the hub **forfeit/timeout wrote no audit row** on a *later* attempt — when
> `RecomputeBest` left `BestPercent`/`Passed` unchanged the `UserQuiz` root was EF-`Unchanged`, and the
> `AuditSaveChangesInterceptor` (which only audited Added/Modified/Deleted) dropped the buffered `System` event. Fixed by
> also auditing `Unchanged` roots carrying an `IAuditableDomainEvent`; +2 regression tests, **63/63** audit+quiz tests
> green. Goes live on the next API rebuild. See `IMPLEMENTATION-PLAN-student-s5-wiring.md` run log. **NOT committed.**

> **Planning note (2026-06-22) — this is a 3-stream slice, not "frontend-only".** Like S4, the master plan's
> *"Backend: none"* was an oversight: **`FR-STU-QZ-009`** ("students SHALL review each attempt's questions, **their
> answers, and the correct answers**") has **no** path — the student quiz shapes deliberately hide `isCorrect`, and the
> **staff** quiz review (`GET /api/review/quizzes/{enrollmentId}` → `QuizReviewDto`) is **attempt-level scores only**.
> So S5 adds **one** new read — **`GET /api/me/quizzes/attempts/{attemptId}/review`** (`RequireStudent`, the only
> student surface that exposes quiz `isCorrect`, gated to the caller's own **terminal** attempt) + **one additive field**
> (`id` on the attempt-summary list, so the intro can deep-link a review) — the standard **3-stream** split (backend +
> frontend + wiring), exactly like S4's review read. **No migration.** **Second grounding correction:** the runner timer
> is **local** (seeded from the start response's `deadlineUtc`/`serverNowUtc`) + **Hangfire-authoritative** — the
> **`QuizHub` pushes nothing** (its sole job is forfeit-on-disconnect), so §4.2's "server-synced via QuizHub" is wrong.
> Contract + 3 streams authored: `docs/contracts/student-s5-quizzes.md` +
> `docs/IMPLEMENTATION-PLAN-student-s5-{backend,frontend,wiring}.md`.

**Goal:** the single-sitting quiz with the live server timer.
- **Backend (one small addition — user-confirmed 2026-06-22):** the five `/api/me/quizzes` engine routes + the `QuizHub`
  **exist** (5B-2) and are reused as-is; add **one** new read **`GET /api/me/quizzes/attempts/{attemptId}/review`** →
  `StudentQuizAttemptReviewDto` (per-question/-option `isCorrect` + the attempt score), gated to the caller's own
  **terminal** attempt (`403 quiz_attempt_in_progress` otherwise, `404` IDOR), **plus** an additive `id` on the
  attempt-summary DTO so the intro list can deep-link a review. **No migration.** The student app connects to the hub
  (JWT via the access-token-on-hub-path scheme, not query-string) **only to arm forfeit-on-disconnect** — the hub pushes
  nothing.
- **Frontend:** `feature-assessment` — quiz intro (time/attempts/best, randomised + pass-mark rules, "one sitting only" alert), runner (a **local** countdown seeded from `deadlineUtc`/`serverNowUtc` [Hangfire is the authoritative auto-submit], question dots, focus-loss detection → telemetry + on-screen warning, **forfeit-on-disconnect/navigation** via `beforeunload` + hub disconnect, leave-quiz confirm modal, auto-submit on timer/manual submit), results (pass/fail mascot, score ring, this-attempt + best-of), **+ the NEW per-attempt answer-key review screen** (your vs correct answers — the prototype has none, like S4's review) (prototype § QUIZ INTRO / QUIZ RUNNER / QUIZ RESULTS + Leave-quiz modal).
- **Contract:** `docs/contracts/student-s5-quizzes.md`.
- **Reqs:** FR-STU-QZ-001..010, FR-PLAT-QZ-001..010 (best-of, `≥` pass, focus-loss-recorded-not-forfeit, forfeit-on-leave, server timer, per-attempt answer-key review).
- **Exit:** an attempt randomises; the timer is authoritative; leaving forfeits with zero (consumes the attempt); focus-loss is logged not forfeited; passing (`≥` min) unlocks the session's videos; best-of is shown; **the per-attempt answer-key review renders (your vs correct + score) for the caller's own terminal attempt**.

### S6 — Profile
> **Status: ✅ MET (2026-06-22)** — backend + frontend built; **wiring proven live on the Aspire stack — all scripted
> checks green, ZERO contract drift** (the `:4300` browser walkthrough is the user's visual step, as in prior phases).
> Verified: `GET /api/me/profile` returns the exact 14-key `StudentProfileDto` (grade/city/region **names** == DB, the
> active `boundDevice` summary+date, **no `email`/`avatar`/`deviceTokenHash`**, `null` when no device); `PUT` persists
> the **seven** writable fields, **leaves `GradeId` unchanged**, **ignores** adversarial `gradeId`/`email` keys, and
> writes **one** `Student` / `ActorType=Student` audit row via the interceptor; the **8-case** `400` validation matrix
> (incl. region-not-in-city); `401` anon / `403` staff on both routes; cross-tenant **404** (EF global filter); and the
> three client-only flows (device-reset INFO modal, Firebase `sendPasswordResetEmail`, sign-out clears the JWT pair)
> fire **no** profile API. Run log in `IMPLEMENTATION-PLAN-student-s6-wiring.md`. *(Planned + adversarially reviewed
> earlier the same day — 3-lens Workflow, 6 fixes incl. a hallucinated `cities."Name"`→`NameEn` and a six-vs-seven
> writable-field fix.)* S6 is the **final vertical slice — it CLOSES the student-portal plan (S0..S6)** (the personalized
> Home/weekly-plan phase is separately planned, post-S6).
>
> **Four user-confirmed decisions (2026-06-22) + grounding corrections to the prototype:** (1) **avatar = initials
> only** — no upload control, **no `Avatar` field, no migration**; real upload deferred (§7). (2) **device reset =
> contact-support only** — the "Reset device" button opens an **informational** modal and calls **no API** (matches
> `FR-STU-DEV-002` + the prototype's no-op confirm); recovery is the existing staff `POST /api/students/{id}/clear-device`
> (`FR-PLAT-DEV-004`). (3) **password = Firebase `sendPasswordResetEmail`** (email reset link) — **no form, no backend**
> (`FR-PLAT-AUTH-009`); the prototype's current/new/confirm form is dropped. (4) **email = read-only Firebase identity**
> — **not** on `Student`, **not** in the GET/PUT DTOs; shown disabled from `firebaseAuth.currentUser.email`. Net backend:
> **two** new `RequireStudent` endpoints + **one** new `Student.UpdateOwnProfile(...)` domain method (grade unchanged),
> **no migration**.

**Goal:** self-service account management.
- **Backend:** **new** `GET /api/me/profile` + `PUT /api/me/profile` (personal info, parent phones, bound-device info; **avatar deferred — initials only**) + a new `Student.UpdateOwnProfile(...)` domain method (grade unchanged), **no migration**. Mirrors the staff `ProfileEndpoints`. Password is Firebase self-service.
- **Frontend:** `feature-profile` — header band, personal-info form (grade + **email disabled**), parent numbers, bound-device card + **contact-support** reset modal, security (change-password → Firebase **email reset link**, sign-out confirm) (prototype § PROFILE + Change-password / Device-reset modals).
- **Contract:** `docs/contracts/student-s6-profile.md`.
- **Reqs:** FR-STU-PRO-001..003, FR-PLAT-AUTH-009, FR-STU-DEV-002/003, FR-PLAT-DEV-004.
- **Exit:** profile reads/saves the seven writable fields; grade + email read-only; bound device + bind date shown; change-password sends a Firebase reset email; reset-device is **contact-support only** (informational modal, no API); sign-out clears the session.

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
`docs/contracts/student-s{1,2,3,4,6}-*.md` (frozen contracts) and `docs/IMPLEMENTATION-PLAN-student-s{0..6}-{backend,frontend,wiring}.md` — same naming and three-stream split as `IMPLEMENTATION-PLAN-phase5c-*`. **S4** reuses the 5B-1 engine (`phase5b1-assignments-attendance.md`) but adds **one** new student review read, so it has its **own** contract (`student-s4-assignments.md`) + all **three** streams. **S5** likewise reuses the existing engine (`phase5b2-quizzes.md`, `phase5c-video-gate.md`) **but**, like S4, adds **one** new student review read (`GET /api/me/quizzes/attempts/{attemptId}/review`, the only student surface exposing quiz `isCorrect`) + one additive attempt-`id` field — so it too has its **own** contract (`student-s5-quizzes.md`) + all **three** streams (`-backend`/`-frontend`/`-wiring`). *(Planning 2026-06-22 revised the earlier "frontend + wiring only" note: `FR-STU-QZ-009`'s per-attempt answer-key review had no backend path.)* **S6** (the final slice) likewise has its **own** contract (`student-s6-profile.md`) + all **three** streams (authored 2026-06-22): it adds **two** new `/api/me/profile` endpoints + one `Student.UpdateOwnProfile(...)` domain method, **no migration**, and **closes** the student-portal plan (S0..S6).
