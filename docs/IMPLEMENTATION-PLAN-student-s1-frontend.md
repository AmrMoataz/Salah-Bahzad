# Student Portal · S1 — FRONTEND stream (Registration & onboarding wizard)

> Status: **Planned — not yet built** · Created 2026-06-21 · The **app half** of slice **S1** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S1). Builds the **two-step self-registration wizard** into the
> **existing** `libs/student-portal/feature-auth` (S0 stood up the app, the login screen, the status screens, and a
> **`/register` placeholder route** — S1 fills it). On submit the student lands in the **pending** state and **cannot
> sign in until a teacher approves** (S0's exchange enforces this).
>
> Run in its **own** Claude session, parallel-safe with the backend stream. **File ownership: `frontend/**` only.**
> Match the **frozen contract** (`docs/contracts/student-s1-registration.md`) field-for-field — especially the
> multipart field names (§A.1), the validation rules (§D), and the `tenantSlug`/`termsVersion` client constants (§F).
>
> Satisfies: `FR-STU-REG-001..009`, `FR-PLAT-AUTH-003` (Google sign-up), `NFR-PRIV-001/003` (ID-image PII),
> `FR-STU-RWD-001/002` (responsive), `FR-STU-A11Y-001` (a11y). Green gate: `npx nx build student-portal` (AOT
> type-checks templates) + `nx test student-portal-feature-auth`.

---

## Design source of truth (the student prototype, NOT the Teacher portal)

- **Prototype:** `.claude/Salah Bahzad Student Portal/Student Portal.html` (siblings `Dropdown.jsx`, `support.js`).
  The banner is **`<!-- ===== AUTH: REGISTER ===== -->`**. S1 builds:
  - the **two-step wizard** — **Step 1** (how to sign up: **Continue with Google** or **manual** name/email/phone/
    password) and **Step 2** (school, grade, city→region, parent phones, ID upload, terms);
  - the **success / pending** terminal state (*"…then your teacher approves it. Once you're in, redeem a code and start
    learning."* / *"pending approval — Salah will review and approve it"*) — **reuse S0's pending render**;
  - a **Back to login** affordance + the **"Already have an account? Sign in"** link.
  The login screen's **"Create account"** link (built in S0) routes here.
- **Tokens / assets / icons / fonts:** already mirrored into `apps/student-portal` by S0 — **reuse**, do not re-mirror.
  Mascot art for the pending state: `assets/salah-relaxing.png` / `salah-mascot.png` (the same ones S0's status screens
  use). Outline icons inline via `DomSanitizer.bypassSecurityTrustHtml` (the admin/S0 pattern).
- When prototype and this doc conflict, **the prototype wins** on layout/copy; **the contract wins** on field names,
  validation, and tenant/terms constants.

---

## Conventions (mirror `frontend/CLAUDE.md` + master plan §3.2)
- Work inside the **existing** `libs/student-portal/feature-auth` (tags `["scope:student-portal","type:feature"]`,
  `prefix:"sb"`, `@nx/jest`). No new lib needed.
- **Module boundaries:** `scope:student-portal` → `scope:shared` only. Reuse `@sb/shared/ui` controls; **never** import
  an admin-portal lib. The admin register wizard (if any) is staff-shaped — **port patterns, don't cross-import**.
- Angular v20+: standalone, `OnPush`, signal `input()/output()/model()`, `computed()/effect()`, `inject()`, native
  control flow, **typed reactive forms**, `ControlValueAccessor` for any custom control. Cite `FR-*`/`NFR-*` in tests.
- **Reuse the shared UI** (already used by admin, per master plan §4.2): `Button` (+ variants), `Input`, `Checkbox`,
  `Select`/`Dropdown`, `FileUpload` (the ID-image picker), `Alert`. Add a student-specific **stepper/progress** to
  `libs/student-portal/ui` only if the prototype's step indicator isn't expressible with shared pieces.

---

## Steps

### F1 — Config constants (`tenantSlug`, `termsVersion`) — contract §F
- Add `tenantSlug` and `termsVersion` to `apps/student-portal/src/environments/environment*.ts` (defaults
  `'salah-bahzad'` / `'v1'` — confirm the real slug against the seeded tenant during wiring). Read `tenantSlug` with the
  same runtime-override shim S0 uses for the API URL: **`window.__SB_TENANT__ ?? environment.tenantSlug`**. Expose a
  tiny `RegistrationConfig` (or extend the existing env accessor) so the wizard + the grades fetch share one source.

### F2 — Data access: registration service + reference reads (student-owned)
In `libs/student-portal/data-access` (or `feature-auth`'s data layer), add a `RegistrationService` (or extend the
existing student data-access), all **anonymous** (no bearer; these run before the student can sign in):
- `grades(): Observable<GradeRef[]>` → `GET /api/reference/grades?tenantSlug=<slug>` (contract §B#3) →
  `{ id, name }[]`.
- `cities(): Observable<CityRef[]>` → `GET /api/reference/cities` (`{ id, nameEn, nameAr }`).
- `regions(cityId): Observable<RegionRef[]>` → `GET /api/reference/cities/{cityId}/regions`.
- `register(form: RegisterForm): Observable<{ studentId: string; status: StudentStatus }>` → **builds `FormData`**
  with the **exact** contract §A.1 field names (`firebaseIdToken, tenantSlug, fullName, phoneNumber,
  parentPhonePrimary, parentPhoneSecondary?, gradeId, cityId, regionId, schoolName, termsVersion, idImage`) →
  `POST /api/students/register`. **Do not set `Content-Type`** (let the browser set the multipart boundary).
- Ensure the **studentAuthInterceptor skips `/api/students/register` + `/api/reference/*`** (no bearer, no refresh
  replay) — they're anonymous; verify S0's interceptor `/api/auth/*` skip is widened or these paths are otherwise
  exempt so a missing token doesn't trigger a refresh loop.

### F3 — Firebase account creation (the identity half) — `FR-STU-REG-002`, `FR-PLAT-AUTH-003`
The wizard mints the **Firebase ID token** the register form carries; it must **not** call the student exchange (there
is no student yet). Add to the student auth layer (port the admin Firebase usage; reuse S0's `#firebaseErrorToMessage`):
- **Manual:** at **final submit**, `createUserWithEmailAndPassword(email, password)` → hold the `UserCredential` →
  `user.getIdToken()` for a fresh token. *(Create at submit, not Step 1, so the short-lived token is fresh when posted.)*
- **Google:** at **Step 1**, `signInWithPopup(GoogleAuthProvider)` → prefill `fullName`/`email` (read-only email),
  keep the signed-in Firebase user; at submit call `user.getIdToken()` for a fresh token.
- **Edge cases (map to friendly copy):** `auth/email-already-in-use` → "You already have an account — sign in instead"
  (link to `/login`); weak-password / invalid-email → inline field errors; popup-closed/blocked → non-blocking notice.
- **Cleanup nuance:** if `POST /register` returns **`409`** (already registered) after a successful Firebase create,
  the Firebase account now exists but has no student — surface "Account already registered, please sign in" and route to
  `/login` (do **not** loop). *(Deleting the orphaned Firebase user is out of scope; the existing account is harmless.)*

### F4 — `RegisterComponent` — the two-step wizard (prototype `AUTH: REGISTER`)
A standalone `OnPush` component under `guestGuard`, replacing S0's `/register` placeholder. One **typed reactive form**
split across two steps with a step indicator:
- **Step 1 — account:** a **Google** button and a **manual** sub-form (`fullName`, `email`, `password`); choosing
  Google runs F3's popup, prefills name + (read-only) email, and **still asks `phoneNumber`**. `phoneNumber` lives here
  (asked in both paths). "Next" validates Step 1 only.
- **Step 2 — details:** `schoolName`; **grade** `Select` (F2 grades); **city** `Select` → on change load + reset
  **region** `Select` (the cascade, F2); **parentPhonePrimary** (required) + **parentPhoneSecondary** (optional) — **≥ 1
  parent phone enforced**; **ID image** via shared `FileUpload` with a **client-side guard** (`image/jpeg|png|webp`,
  **≤ 5 MB** — contract §D) and a thumbnail/preview; a **terms** `Checkbox` group whose label links the terms and
  **includes the one-device-policy acknowledgement** (contract §F) — submit is disabled until checked.
- **Validation:** typed validators mirroring contract §D for inline UX; the **server is authoritative** — on `400`,
  map FluentValidation field errors back onto the controls; on `404` (bad grade/city/region/slug) show a top `Alert`;
  on `429` show "Too many attempts, try again shortly." Keep entered values on error (don't reset the form).
- **Submit:** assemble `RegisterForm` (token from F3 + the `tenantSlug`/`termsVersion` constants from F1) → `register()`
  → on `201` go to the **pending** state (F5). Disable the submit button + show a spinner while in flight.
- **a11y (`FR-STU-A11Y-001`):** labelled inputs, `aria-invalid` + error text wired, step changes announced, focus moves
  to the first control / first error, the file input keyboard-reachable. **Responsive (`FR-STU-RWD-001/002`):**
  single-column on phone, comfortable touch targets, matches the prototype across phone/tablet/desktop.

### F5 — Success / pending + rejected reconciliation (reuse S0)
- On `201 { status:"Pending" }` render the **pending** success state — **reuse S0's pending render** (the
  `StatusComponent` pending case / its mascot + copy) so there's no duplication; add the *"redeem a code once you're
  in"* line from the prototype. Offer **Back to login**.
- A **rejected** student doesn't see a screen here (they're rejected *after* review) — that state is S0's: when they
  later sign in, the exchange returns `403 account_rejected` (+ `RejectionReason`) and S0's status screen renders it.
  S1 adds nothing for rejected beyond making sure the pending copy sets the expectation. *(Contract §C.)*

### F6 — Routing
- Replace the S0 `/register` **placeholder** with `RegisterComponent` under **`guestGuard`** (a signed-in student is
  bounced to the shell home). Keep `/login` ↔ `/register` links working both ways. No shell chrome on these auth routes
  (full-screen auth layout, as S0's login).

### F7 — Tests (Jest; `whenStable()`, never `fakeAsync` — the 5B-1 jsdom gotcha)
- `register.component.spec.ts`:
  - **Step gating:** Step 1 invalid blocks "Next"; valid advances. ≥ 1 parent phone enforced; terms unchecked blocks
    submit; ID image > 5 MB or wrong type is rejected client-side.
  - **City→region cascade:** selecting a city loads regions and resets a stale region.
  - **Manual submit:** builds `FormData` with the **exact** contract field names + the configured `tenantSlug`/
    `termsVersion`; calls Firebase create → `getIdToken` → `POST /register`; `201` → pending state shown.
  - **Google path:** popup prefills name/email (email read-only), still requires phone; submit uses the held user's
    fresh token.
  - **Error mapping:** `400` → field errors restored; `409` → "already registered → sign in"; `429` → throttle notice;
    Firebase `email-already-in-use` → sign-in hint. Driver: `whenStable()`.
- `registration.service.spec.ts`: `FormData` keys/values exactly match §A.1; `grades()` sends `?tenantSlug=`;
  region fetch hits the city path; **no bearer** on any of these (anonymous), no refresh replay on a 401.

## Exit criteria
A prospective student opens `/register`, completes Step 1 (Google **or** manual) + Step 2, uploads an ID image, accepts
the terms (incl. one-device policy), and submits; a `Pending` student is created and the pending state renders with the
redeem-after-approval copy. Inline + server validation behave; Google prefills name/email and asks phone; every server
error (`400`/`404`/`409`/`429`) and Firebase error maps to readable copy; the flow is responsive + a11y-clean on phone/
tablet/desktop. `npx nx build student-portal` (AOT) + `nx test student-portal-feature-auth` green. Hand to wiring.

## Out of scope (defer)
Catalogue / sessions / profile screens (S2/S3/S6); the Redeem enroll modal (S2); the **"Request a spot"** offline-code
request (deferred, master plan §3.3 — **not built**); any new status-read endpoint (driven by S0's exchange `403`,
contract §C); deleting orphaned Firebase users on a post-create `409` (harmless, out of scope); multi-tenant host→slug
resolution (single-tenant constant for now, §F).

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the FRONTEND stream of Student-Portal phase S1 (the self-registration wizard) for Salah Bahzad
(Angular v20+, Nx). Edit frontend/** ONLY. The app, login, status screens, and a /register PLACEHOLDER already exist
from S0 — you fill the wizard into the existing libs/student-portal/feature-auth.

Read first, in order:
1. frontend/CLAUDE.md (Angular v20+ conventions, tokens, icons).
2. docs/IMPLEMENTATION-PLAN-student-s1-frontend.md — THIS doc. DESIGN SOURCE OF TRUTH =
   .claude/Salah Bahzad Student Portal/Student Portal.html banner AUTH: REGISTER (two-step wizard + pending state).
3. docs/contracts/student-s1-registration.md — the FROZEN contract: POST /api/students/register multipart field names
   (§A.1), validation (§D), the new GET /api/reference/grades?tenantSlug= (§B#3), tenantSlug/termsVersion constants
   (§F), and "no new status endpoint — reuse S0's exchange 403" (§C).
4. The S0 code to reuse/port: libs/student-portal/feature-auth (LoginComponent, status screens, firebase error map),
   the studentAuthInterceptor + StudentAuthStore, and S0's window.__SB_*__ runtime-override shim.

Build: config constants tenantSlug (window.__SB_TENANT__ ?? env) + termsVersion; a RegistrationService (anonymous —
grades?tenantSlug=, cities, regions cascade, and a FormData POST /register with the EXACT contract field names, no
Content-Type header); Firebase account creation (createUserWithEmailAndPassword for manual, signInWithPopup(Google)
for social — getIdToken() at submit; it must NOT call the student exchange); a RegisterComponent two-step wizard
(Step 1 Google/manual + phone; Step 2 school/grade/city->region/parent phones[>=1]/ID upload<=5MB jpeg|png|webp via
shared FileUpload/terms incl one-device policy) under guestGuard replacing the placeholder; on 201 reuse S0's pending
render. Map 400/404/409/429 + Firebase errors to readable copy; keep form values on error. Responsive + a11y.

Jest with whenStable() (NOT fakeAsync): step gating, >=1 parent phone, terms gate, ID client guard, city->region
cascade, exact FormData keys + tenantSlug/termsVersion, Google prefill, error mapping. Green gate:
`npx nx build student-portal` + `nx test student-portal-feature-auth`. Report both.
```
