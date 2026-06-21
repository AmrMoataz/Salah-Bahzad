# Student Portal · S0 — FRONTEND stream (App shell, auth, device-link, design system)

> Status: **Planned — not yet built** · Created 2026-06-21 · The **app half** of foundation phase **S0** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S0). Stands up the **new Angular app `apps/student-portal`**: the
> guarded responsive shell, Firebase sign-in wired to the new student exchange, transparent device-link, silent
> refresh, and the mirrored design system. **No business screens yet** — catalogue/sessions/etc. arrive S2+.
>
> Run in its **own** Claude session, parallel with the backend stream. **File ownership: `frontend/**` only**, plus
> the **one AppHost touch** in §6 (the second `AddNpmApp`). Match the backend doc's **frozen contract**
> (`IMPLEMENTATION-PLAN-student-s0-backend.md` §1) field-for-field.
>
> Satisfies: `FR-STU-AUTH-001`, `FR-STU-DEV-001..003`, `FR-PLAT-AUTH-002/006`, `FR-STU-RWD-001/002` (responsive),
> `FR-STU-A11Y-001` (a11y). Green gate: `npx nx build student-portal` (AOT type-checks templates) +
> `nx test student-portal-feature-shell` + `nx test student-portal-feature-auth`.

---

## Design source of truth (READ THIS — different from the admin portal)

The admin portal follows `.claude/Salah Bahzad Teacher Portal/`. **The student portal follows its own prototype:**

- **Prototype:** **`.claude/Salah Bahzad Student Portal/Student Portal.html`** (siblings `Dropdown.jsx`, `support.js`).
  When anything conflicts, the prototype wins. The screens are delimited by HTML-comment banners
  `<!-- ===== <NAME> ===== -->`. **S0 builds three of them:**
  - **`AUTH: LOGIN`** — *"Welcome back — pick how you'd like to sign in."*, **Continue with Google** + email/password,
    **Forgot password?**, and a **Create account** link (the wizard itself is S1).
  - **`AUTH: REGISTER`** → only its **terminal states** in S0: the **pending** state (*"…then your teacher approves it.
    Once you're in, redeem a code and start learning."* / *"pending approval — Salah will review and approve it"*) and
    a **rejected** state showing the server's `RejectionReason`. *(The full Step-1/Step-2 wizard is S1.)*
  - **`APP`** — the shell: **`Sidebar (tablet/desktop)`**, **`Drawer overlay (mobile)`** + **scrim**, **bottom-nav
    `(mobile)`** with the centre **Redeem** FAB, and the header (`crumb` / `pageTitle` + a **Notifications** bell +
    a user chip). The device one-device copy lives in `PROFILE` (S6) — reuse its wording for the
    `device_not_recognized` status screen.
- **Tokens:** `.claude/Salah Bahzad Student Portal/_ds/salah-bahzad-design-system-1a8c19a8-bb92-4a32-8558-0faf8e57a0f3/`
  → `tokens/{colors,typography,spacing,shadows}.css` + `styles.css`. Mirror into
  **`apps/student-portal/src/styles/_design-tokens.scss`** using the **canonical `--sb-*` names** (identical scheme to
  admin — `--sb-font-sans`, `--sb-body-md-size`, `--sb-timing`, `--sb-primary`, `--sb-space-4`, `--sb-scrim`, …). Do
  **not** reference `docs/tokens.*` (deprecated). Never override token values in component styles.
- **Assets:** `.claude/Salah Bahzad Student Portal/assets/*` → `apps/student-portal/src/assets/` **byte-for-byte**
  (`logo-white/black/small.png`, `salah-mascot.png`, `salah-relaxing.png`, `crown.png`, `super-salah.png`,
  `salah-passed/failed/prerequisite.png`). Fonts: NunitoSans (sans), Caveat (display), Permanent Marker (marker),
  Cascadia Mono (mono) — same families as admin.
- **Icons:** outline only, inline `<svg>` rendered via `DomSanitizer.bypassSecurityTrustHtml` (Angular strips `<svg>`
  from plain `[innerHTML]`) — exactly the admin pattern.

---

## Conventions (mirror `frontend/CLAUDE.md` + master plan §3.2)
- Nx libs at `libs/student-portal/feature-<name>` with `project.json` tags `["scope:student-portal","type:feature"]`,
  `prefix:"sb"`, a `@nx/jest` test target — byte-for-byte the shape of `libs/admin-portal/feature-dashboard`.
- Path aliases in `frontend/tsconfig.base.json`: `@sb/student-portal/feature-<name> → …/src/index.ts`.
- **Module boundaries:** `scope:student-portal` may depend on `scope:shared` **only** — **never** import a
  `scope:admin-portal` lib (and vice-versa). The admin `AuthStore` is **staff-shaped** (it stores `sb_staff`, navigates
  `/dashboard`, calls `/api/auth/exchange`) — so auth is **ported, not cross-imported** (see §3).
- Angular v20+: standalone, `OnPush`, signal `input()/output()/model()`, `computed()/effect()`, `inject()`, native
  control flow, typed reactive forms, `ControlValueAccessor`. Cite `FR-*`/`NFR-*` in tests/commits.

---

## Steps

### F1 — Scaffold `apps/student-portal`
`npx nx g @nx/angular:application student-portal --directory=apps/student-portal --standalone --routing
--style=scss --prefix=sb`. Add `src/styles/_design-tokens.scss` (mirror, above) and `@use 'styles/design-tokens'` from
`styles.scss`; copy the `environment.ts` Firebase config shape from admin; mirror assets (above).

### F2 — Ported auth into a student-owned location (`libs/student-portal/data-access` or `feature-auth`)
The shared `authInterceptor` injects the concrete staff `AuthStore`, so the student app needs its **own** store +
interceptor (ported, not imported):
- **`StudentAuthStore`** (signal-based, mirrors `AuthStore`): `signIn(email,pw)` and `signInWithGoogle()` →
  Firebase → `POST /api/auth/student/exchange` **with `withCredentials:true`** (so `Set-Cookie: sb_device` is stored)
  and an **`X-Device-Fingerprint`** header (§F4). Stores the JWT pair + `StudentInfo` in `sessionStorage`
  (`sb_student`); `refreshAccessToken()` → `/api/auth/refresh` (single-flight `shareReplay`, exactly like admin);
  `signOut()` → Firebase `signOut` + clear + `/login`; `requestPasswordReset()` → Firebase. On a `403` with a machine
  `reason` (`account_pending|account_rejected|account_inactive|device_not_recognized`) it sets a `status` signal the
  router uses to show the **status screen** instead of erroring. `restoreSession()` for reloads.
- **`studentAuthInterceptor`**: attaches the bearer, replays once on `401` via `refreshAccessToken()`, and sets
  **`withCredentials:true` on every `/api` call** so the device cookie rides (device enforcement on future content
  routes). Skips `/api/auth/*`. Models: `StudentInfo`, `StudentAuthResponse`, `StudentStatus` — match contract §1.2.
- Functional **`authGuard`** (default-deny → `/login`) and **`guestGuard`** (signed-in → shell home).

### F3 — `feature-shell` (prototype `APP` banner)
Standalone `ShellComponent` with a `<router-outlet>` and responsive chrome driven by a breakpoint signal
(mobile / tablet / desktop, matching the prototype's `isMobile/isTablet/isDesktop`):
- **Sidebar** (≥ tablet): brand + nav; **mobile drawer** + **scrim** (`--sb-scrim`) toggled by a hamburger, closes on
  nav/scrim-tap (mirror the prototype's `drawerOpen` + `transform:translateX` slide).
- **Bottom-nav** (mobile) with the centre **Redeem** FAB (`feature-catalogue` enroll modal is S2 — in S0 the FAB routes
  to a placeholder `/redeem` or is disabled with a "coming soon" tooltip; keep the visual).
- **Header**: `crumb` (uppercased) + `pageTitle` from a per-route titles map (prototype's `titles` object),
  a **Notifications** bell (visual only — notifications are backlog), and a **user chip** (name + avatar) →
  menu with Sign out. a11y: keyboard-navigable, `aria-label`s, focus trap in the drawer (`FR-STU-A11Y-001`).
- Nav items are gated by what exists; in S0 only **Home** (placeholder) + the user menu are live. Real items
  (Catalogue, My sessions, Profile) are added by their phases.

### F4 — Device-link (transparent)
- A small util builds a **stable client fingerprint**: a random id persisted in `localStorage` (`sb_device_fp`) plus a
  UA/platform summary string; sent as the `X-Device-Fingerprint` header on the exchange. The **authoritative**
  device-token cookie is **server-managed** (HttpOnly) — the SPA never reads or sets it; `withCredentials` carries it.
- Consent is **not** a separate S0 screen — it's the **one-device-policy terms checkbox** at registration (S1). S0
  only (a) sends the fingerprint and (b) renders the **`device_not_recognized`** status screen (reusing the `PROFILE`
  one-device copy + a "contact support to reset" line) when the server refuses an unrecognised device.

### F5 — `feature-auth` screens (prototype `AUTH: LOGIN` + the pending/rejected states)
- **`LoginComponent`** (`guestGuard`): the `AUTH: LOGIN` layout — Continue with Google + email/password, Forgot
  password (→ `StudentAuthStore.requestPasswordReset` once the email is known, else Firebase reset by email),
  Create account → `/register` (S1 placeholder route now). Map Firebase error codes to friendly copy (port the
  admin `#firebaseErrorToMessage`).
- **`StatusComponent`** (or inline states): renders **pending** (mascot + the approve-soon copy), **rejected**
  (`RejectionReason`), **inactive**, and **device_not_recognized** — driven by `StudentAuthStore.status()`. Salah
  mascot art from the mirrored assets.

### F6 — App wiring
- `apps/student-portal/src/app/app.config.ts`: `provideRouter` (+ `withComponentInputBinding`), `provideHttpClient
  (withInterceptors([studentAuthInterceptor]))`, Firebase providers (copy admin), `provideAnimationsAsync`.
- `app.routes.ts`: `/login` (`guestGuard`); `/register` → S1 placeholder; `''` (`authGuard`) → `ShellComponent` with a
  child **placeholder home** (real children land S2/S3/S6); `**` → `''`.
- Expose `window.__SB_API_URL__` the same way admin does (the store reads it for absolute API URLs).

### F7 — Tests (Jest; use `whenStable()`, never `fakeAsync` — the 5B-1 jsdom gotcha)
- `student-auth.store.spec.ts` — exchange maps `StudentAuthResponse` + persists; sets the right `status` for each
  `403 reason`; single-flight refresh hits `/api/auth/refresh` once for concurrent 401s.
- `shell.component.spec.ts` — renders sidebar ≥ tablet, drawer+scrim on mobile (toggle), bottom-nav + Redeem FAB on
  mobile; user-chip Sign out calls the store; crumb/title from the route map.
- `login.component.spec.ts` — Google + email/password paths, error-code mapping, and the pending/rejected/device
  status renders. Driver: `whenStable()`.

## Exit criteria
`apps/student-portal` builds (AOT) and runs; a student signs in via Google/email-password through the new exchange and
lands in the responsive shell on phone/tablet/desktop; anonymous users are bounced to `/login`; `pending`/`rejected`/
`inactive`/`device_not_recognized` render their status screens; refresh is silent. Jest suites green. Hand to wiring.

## Out of scope (defer)
Registration **wizard** (S1 — S0 has only the login + the pending/rejected render), catalogue/sessions/profile screens
(S2/S3/S6), the Redeem enroll modal (S2), real Notifications (backlog), the `cards`/`rail` My-Sessions layouts
(demo-only, dropped per master plan §3.3).

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the FRONTEND stream of Student-Portal phase S0 (new app shell + auth + device-link) for Salah
Bahzad (Angular v20+, Nx). Edit frontend/** ONLY (plus the single AppHost AddNpmApp line — coordinate with wiring).

Read first, in order:
1. frontend/CLAUDE.md (Angular v20+ conventions, tokens, icons)
2. docs/IMPLEMENTATION-PLAN-student-s0-frontend.md — THIS doc. DESIGN SOURCE OF TRUTH =
   .claude/Salah Bahzad Student Portal/Student Portal.html (banners AUTH: LOGIN, AUTH: REGISTER pending/rejected, APP
   shell) + its _ds tokens + assets. NOT the Teacher Portal prototype.
3. docs/IMPLEMENTATION-PLAN-student-s0-backend.md §1 (FROZEN contract: /api/auth/student/exchange, StudentAuthResponse,
   the sb_device HttpOnly cookie, the four 403 reason codes).
4. The admin templates to PORT (not import — module boundaries forbid cross-scope): libs/shared/data-access AuthStore +
   authInterceptor; apps/admin-portal feature-shell + feature-auth + app.config/app.routes.

Build: apps/student-portal (mirror _design-tokens.scss + assets); a ported StudentAuthStore + studentAuthInterceptor
(exchange with withCredentials + X-Device-Fingerprint; single-flight refresh; 403-reason → status screen) in a
student-owned lib; feature-shell (sidebar/tablet+desktop, mobile drawer+scrim, bottom-nav with Redeem FAB, header
crumb/title + notifications bell + user chip, responsive + a11y); feature-auth (LoginComponent: Google + email/pw +
Forgot + Create-account link; status screens pending/rejected(reason)/inactive/device_not_recognized); authGuard +
guestGuard; app.config + app.routes (placeholder home child). Device cookie is server-managed HttpOnly — SPA only
sends the fingerprint + withCredentials.

Jest specs with whenStable() (NOT fakeAsync). Green gate: `npx nx build student-portal` +
`nx test student-portal-feature-shell` + `nx test student-portal-feature-auth`. Report all three.
```
