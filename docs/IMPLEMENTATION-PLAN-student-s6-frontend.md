# Student Portal · S6 — FRONTEND stream (self-service Profile)

> Status: **Planned — not yet built** · Created 2026-06-22 · The **app half** of slice **S6** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S6 — Profile). Builds the student's own **Profile** screen — the
> header band, the editable personal-info form, the bound-device card, and the security card — into a **new**
> `libs/student-portal/feature-profile` lib, and flips the shell's disabled **Profile** nav item live. **S6 CLOSES
> the student-portal plan (S0..S6)** — this is the final vertical slice (the personalized Home / weekly-plan phase is
> separately planned, post-S6).
>
> Run in its **own** Claude session, parallel-safe with the backend stream. **File ownership: `frontend/**` only.**
> Match the **frozen contract** (`docs/contracts/student-s6-profile.md`) field-for-field — the two new endpoints
> `GET` + `PUT /api/me/profile` (§A), the `StudentProfileDto` shape incl. nested `boundDevice` and the **absence** of
> `email`/`avatar` (§A.1), the `UpdateMyStudentProfileRequest` **seven writable fields** with `gradeId`/email excluded
> (§A.2), the error modes (`401`/`403`/`400`, **no 404-self**, **no 409**, §B), and the screen + three-modal behaviour
> (§D).
>
> **Four user-confirmed decisions (2026-06-22), binding — every screen must be consistent with them:**
> (1) **Avatar = initials only** — no upload control, no `Avatar` field, no DTO field, no storage; display via the shared
> `sb-avatar` (size `xl`, initials). (2) **Device reset = contact-support only** — the "Reset device" button opens an
> **informational** modal that calls **no API**. (3) **Password = Firebase email reset link** — `sendPasswordResetEmail`
> client-side, no form, no backend. (4) **Email = read-only Firebase identity** — not on `Student`, not in either DTO,
> shown disabled from `firebaseAuth.currentUser.email`.
>
> Satisfies: `FR-STU-PRO-001/002/003`, `FR-PLAT-AUTH-009` (password delegated entirely to Firebase),
> `FR-STU-DEV-002/003`, `FR-PLAT-DEV-004` (staff clear is the recovery path), `FR-STU-RWD-001/002` (responsive),
> `FR-STU-A11Y-001` + `NFR-A11Y-001/002` (a11y). `NFR-PRIV-001` (audited-PII reads) applies to **staff** viewing a
> student, **not** the data subject viewing their own profile — the GET is not audited. Green gate:
> `npx nx build student-portal` (AOT type-checks templates) + `nx test student-portal-feature-profile`.

---

## Design source of truth (the student prototype, NOT the Teacher portal)

- **Prototype:** `.claude/Salah Bahzad Student Portal/Student Portal.html` (siblings `Dropdown.jsx`, `support.js`).
  S6 builds the **`PROFILE`** section + its **`Change-password`**, **`Device-reset`**, and **`Sign-out`** modals:
  - **Header band:** gradient `135deg #EAF2FB → #EBF5E9`, the shared **Avatar** (size **`xl`**, **initials only**,
    status badge — **no image, no upload**, decision 1), the name (bold 24px), a sub-line
    `"{gradeName} · {track} · {cityName}"` (prototype copy — render from the resolved names), and a **success Chip
    "Active"** from `status`. Two-column grid desktop (`1.6fr 1fr`) → single column on mobile (`FR-STU-RWD-001/002`).
  - **Left card "Personal information":** **Full name** (editable), **Email** (prototype shows an editable `<input>` →
    **we render it read-only/disabled**, decision 4 / §C.2), **School** (editable), **Grade** (**disabled** — §C.1),
    **City** (dropdown), **Region** (dropdown, cascades from City). A **"Save changes"** primary button. Subsection
    **"Parent / guardian numbers":** **Primary** (required) + **Secondary** (optional) (`FR-STU-PRO-002`).
  - **Right column "Bound device" card:** smartphone icon, device name = `boundDevice.summary` (generic fallback when
    `null`), `"Bound {date}"`, helper *"Only one device can access content. To switch devices, contact support to reset
    the binding."*, a **"Reset device"** secondary-sm button → the **device-reset INFO modal** (§D.2). **"Security"
    card:** **"Change password"** secondary → the **Firebase reset-email modal** (§D.3); **"Sign out"** danger-ghost →
    the **sign-out confirm modal** (§D.4).
  - Mascots (`assets/salah-*.png`) are available for a friendly modal state (e.g. the sign-out modal). Outline icons
    inline via `DomSanitizer.bypassSecurityTrustHtml` (the admin/S0..S5 pattern — Angular strips `<svg>` from plain
    `[innerHTML]`).
- Tokens / assets / icons / fonts are already mirrored into `apps/student-portal` by S0 — **reuse**, don't re-mirror.
- When prototype and this doc conflict, **the prototype wins** on layout/copy; **the contract wins** on the data + calls,
  the DTO field names, and the three modal behaviours. **Grounding corrections to honour** (the prototype is wrong on
  these, the contract is right): Email is **read-only** not editable (§C.2); the device-reset confirm is a **no-op
  contact-support** action (§D.2); the change-password 3-field form is **replaced** by a Firebase reset-email
  confirm/success modal (§D.3).

---

## Conventions (mirror `frontend/CLAUDE.md` + master plan §3.2)
- **New lib** `libs/student-portal/feature-profile` — `project.json` `name: "student-portal-feature-profile"`,
  tags `["scope:student-portal","type:feature"]`, `prefix:"sb"`, `@nx/jest` test target (byte-for-byte the shape of
  S2's `feature-catalogue` / S4's `feature-assessment` `project.json`). You **must also** add the
  `@sb/student-portal/feature-profile` path alias to `frontend/tsconfig.base.json`, the **one** `profile` route to
  `apps/student-portal/src/app/app.routes.ts`, **and** export `ProfileComponent` from the lib barrel — an unrouted lib
  still builds green (the recurring "unrouted feature" trap from S1/5B-1 wiring); prove `/profile` resolves at `:4300`.
- **Module boundaries:** `scope:student-portal` → `scope:shared` only. Reuse `@sb/shared/ui`; **never** import an
  admin-portal lib. `feature-profile` may consume `@sb/student-portal/data-access`. It does **not** import another
  `feature-*` lib.
- Angular v20+: standalone, `OnPush`, signal `input()/output()/model()`, `computed()/effect()`, `inject()` (no
  constructor DI), native control flow (`@if`/`@for`/`@switch`), **typed reactive forms** for the personal-info form.
  Cite `FR-*`/`NFR-*` in tests.
- **Reuse the shared UI** (`@sb/shared/ui` barrel): `Avatar` (size `xl`, initials, `status="active"`),
  `StatusPill`/`Chip` (variant `success` — the "Active" chip), `Card`, `Button` (variants
  `primary`/`secondary`/`ghost`/`danger`-ghost via the available variants, sizes `sm`/`md`/`lg`), `Select` (the
  `ControlValueAccessor` dropdown — for City/Region), `FormField`, `Alert` (warning — the device-reset modal),
  `Modal` (`size="confirm"`, `modalFooter` slot, `(close)` output), `ToastService` (Save-changes success toast). **Do
  not** use `FileUpload` — there is no avatar upload (decision 1).
- **No new reference read.** The City→Region cascade **reuses the existing anonymous `/api/reference/*` reads** already
  shipped by S1 — `RegistrationService.cities()` (`GET /api/reference/cities`) + `RegistrationService.regions(cityId)`
  (`GET /api/reference/cities/{cityId}/regions`), which return `CityRef`/`RegionRef` (`{ id, nameEn, nameAr }` /
  `{ id, cityId, nameEn, nameAr }`). The `studentAuthInterceptor` skips `/api/reference/` (it is in `ANONYMOUS_PATHS`),
  so they need no bearer. **Grade is read-only → no grades read is needed in S6.**

> **Reuse, don't widen.** The runner-vs-review separation discipline from S4/S5 applies here too: model the **GET**
> result (`StudentProfile`, with `gradeName`/`boundDevice`) and the **PUT** body (`UpdateMyStudentProfile`, the seven
> writable fields) as **separate** wire-exact interfaces — the PUT body must **not** carry `gradeId`/`gradeName`/`email`/
> `status`/`boundDevice`. Use `| null` (the contract's nullables), never optional `?`, on the wire models.

---

## Steps

### F1 — Lib scaffold + routing + shell nav (avoid the unrouted-lib trap)
- `nx g @nx/angular:library feature-profile --directory=libs/student-portal/feature-profile` (or copy
  `feature-catalogue`'s `project.json`); confirm `name: "student-portal-feature-profile"`, the **tags**
  (`scope:student-portal`/`type:feature`), `prefix:"sb"`, and the `@nx/jest` target (jestConfig
  `libs/student-portal/feature-profile/jest.config.ts`). Add a `test-setup.ts` mirroring `feature-catalogue`'s.
- Add `"@sb/student-portal/feature-profile": ["libs/student-portal/feature-profile/src/index.ts"]` to
  `frontend/tsconfig.base.json` (beside the existing `feature-catalogue`/`feature-sessions`/`feature-assessment`
  entries); export `ProfileComponent` from the lib barrel `src/index.ts`.
- Add **one lazy route** under the **authenticated shell** in `apps/student-portal/src/app/app.routes.ts` (a child of
  the `''` `authGuard` shell route, beside the `sessions` / `catalogue` children):
  ```ts
  {
    // S6 — the student self-service Profile screen.
    path: 'profile',
    loadComponent: () =>
      import('@sb/student-portal/feature-profile').then((m) => m.ProfileComponent),
  },
  ```
  Confirm `/profile` resolves at `:4300` (not just a green build).
- **Flip the shell Profile nav item live** in `libs/student-portal/feature-shell/src/lib/shell/shell.component.ts` —
  set `disabled: true → false` on the Profile item in **BOTH** `NAV_ITEMS` **and** `BOTTOM_ITEMS` (the `route: '/profile'`
  item with `icon: ICON.profile`; `ROUTE_META.profile = ['Account','Profile']` is already correct). Update the
  S0/S6 comment (`Profile stays disabled ("Soon") until S6`) to note it is now live. This is the **only**
  `feature-shell` touch.

### F2 — Data access: `ProfileService` + wire-exact models (authenticated — bearer + refresh apply)
In `libs/student-portal/data-access`, add a `lib/profile/` folder (beside `catalogue/` / `sessions/` /
`assignments/`): `profile.service.ts` + `profile.models.ts`. The two profile calls are **authenticated** — they ride
the existing `studentAuthInterceptor` (bearer attached, 401→refresh-replay, `sb_device` cookie via
`withCredentials`). **Do not** add `/api/me/profile` to `ANONYMOUS_PATHS`. Use the same `__SB_API_URL__` shim as
`CatalogueService`/`MySessionsService`.

- **Models** (`profile.models.ts`) — wire-exact to contract §A.1/§A.2, `| null` not `?`:
  ```ts
  /** Nested in StudentProfile — the caller's active StudentDevice (contract §A.1 / §C.5). Token hash never sent. */
  export interface BoundDevice {
    summary: string | null;       // StudentDevice.FingerprintSummary, e.g. "Windows / Chrome"; null → generic label
    boundAtUtc: string;           // StudentDevice.BoundAtUtc (ISO-8601) → "Bound {date}"
  }

  /** GET /api/me/profile result (contract §A.1). NO email field — email is shown from Firebase (§C.2). NO avatar. */
  export interface StudentProfile {
    id: string;
    fullName: string;
    phoneNumber: string;
    parentPhonePrimary: string;
    parentPhoneSecondary: string | null;
    schoolName: string;
    gradeId: string;
    gradeName: string | null;     // tenant-owned taxonomy display name — DISABLED field + header sub-line
    cityId: string;
    cityName: string | null;
    regionId: string;
    regionName: string | null;
    status: StudentStatus;        // string union ('Active' for a signed-in student) → success Chip
    boundDevice: BoundDevice | null; // null when no active device bound
  }

  /** PUT /api/me/profile body (contract §A.2) — the SEVEN writable fields only. NO gradeId, NO email, NO status. */
  export interface UpdateMyStudentProfile {
    fullName: string;
    phoneNumber: string;
    schoolName: string;
    cityId: string;
    regionId: string;
    parentPhonePrimary: string;
    parentPhoneSecondary: string | null;
  }
  ```
  Reuse the existing `StudentStatus` string union (`../auth/student-auth.models`) — do **not** redefine it. Export
  `ProfileService`, `StudentProfile`, `BoundDevice`, `UpdateMyStudentProfile` from
  `libs/student-portal/data-access/src/index.ts`.
- **Service** (`profile.service.ts`) — `@Injectable({ providedIn: 'root' })`, `inject(HttpClient)`, the `#apiUrl()`
  shim:
  - `getProfile(): Observable<StudentProfile>` → `GET /api/me/profile` (§A #1). The wire shape **equals**
    `StudentProfile` field-for-field, so the response is the model (no mapping).
  - `updateProfile(body: UpdateMyStudentProfile): Observable<StudentProfile>` → `PUT /api/me/profile` (§A #2) with the
    seven-field body → the **re-read** `StudentProfile`. A `400` (validation: empty/too-long/unknown-or-mismatched
    city/region) surfaces as an `HttpErrorResponse` for the component to render. There is **no 404-self / no 409**
    (§B) — the caller is the JWT subject.

### F3 — `ProfileComponent` (the screen) — `FR-STU-PRO-001/002`, contract §D.0
A standalone `OnPush` screen at `/profile`. On init it loads `getProfile()` (and the City list) and renders the header
band + the two-column body.

- **Load:** call `ProfileService.getProfile()` → a `profile = signal<StudentProfile | null>(null)`. While `null`, show a
  light loading state. There is no 404 path (the subject always exists); a transient error toasts + retries.
- **Header band** (gradient `135deg #EAF2FB → #EBF5E9`):
  - `sb-avatar` `size="xl"`, `status="active"`, `[initials]="initials()"` where `initials()` is a `computed` from
    `profile().fullName` (first letters of the first two words, upper-cased) — **initials only, no image, no upload**
    (decision 1; `FR-STU-PRO-001`'s avatar is **deferred**, §Out-of-scope).
  - Name (bold 24px) = `profile().fullName`.
  - Sub-line `"{gradeName} · {track} · {cityName}"` — render `gradeName` + `cityName` from the resolved names; the
    prototype's "track" string has no DTO field, so render gracefully (drop the segment / use `gradeName` only when
    there's no track). `gradeName`/`cityName` may be `null` — fall back without dangling separators.
  - **Success Chip "Active"** from `status` (`sb-status-pill variant="success"` or the DS Chip).
- **Left card "Personal information"** — a **typed reactive form** (`FormGroup` via `inject(NonNullableFormBuilder)`):
  - **Full name** — editable `FormControl<string>`, `Validators.required` + `Validators.maxLength(200)`.
  - **Email** — **read-only / disabled** input (decision 4 / §C.2): **not** a form control bound to the DTO; sourced
    client-side from `inject(StudentAuthStore)`'s Firebase auth → `firebaseAuth.currentUser?.email` (see F5 for how the
    store exposes it). Render it disabled with a hint *"Managed by your sign-in provider."* It is **never** sent on PUT.
  - **School** — editable `FormControl<string>`, required + `maxLength(200)`.
  - **Grade** — a **disabled** `sb-select` (or a static read-only field) showing `gradeName` (§C.1). It is **not** in
    the form value sent on PUT — a student cannot change their own grade (staff-managed, `FR-ADM-STU-005`).
  - **City** — `sb-select` `FormControl<string>` (required), options from `RegistrationService.cities()` mapped to
    `SelectOption { value: id, label: nameEn }` (or `nameAr` per locale).
  - **Region** — `sb-select` `FormControl<string>` (required), **cascades from City** (§C.3/§C.4): on a City value
    change, call `RegistrationService.regions(cityId)`, reset the Region control, and repopulate options. Seed the
    region options on load by fetching `regions(profile().cityId)` so the current region renders selected. A mismatched
    pair is rejected server-side (`400`, §B).
  - **"Save changes"** primary button → builds `UpdateMyStudentProfile` from the form value + the parent-phone
    subsection (NOT email, NOT grade), calls `ProfileService.updateProfile(body)`, on **`200`** re-seeds the `profile`
    signal + the form from the returned DTO and fires a **success toast** ("Profile updated."); on **`400`** renders the
    FluentValidation `detail` inline (e.g. under the relevant field / a form-level error). Disable the button +
    show a spinner while in flight; disable when the form is invalid/pristine.
- **Subsection "Parent / guardian numbers"** (`FR-STU-PRO-002`):
  - **Primary** — `FormControl<string>`, **required** + `maxLength(32)`.
  - **Secondary** — `FormControl<string>`, **optional**, `maxLength(32)`; sent as `null` (not `''`) when blank.
- **Right column "Bound device" card** (`FR-STU-DEV-003`) — **display-only**, see F4.
- **"Security" card** — Change-password + Sign-out buttons, see F4.
- **Responsive** (`FR-STU-RWD-001/002`): the `1.6fr 1fr` grid collapses to one column on phone; touch-sized targets.
  **A11y** (`FR-STU-A11Y-001`, `NFR-A11Y-001/002`): every field has a `<label>`/`sb-form-field`; the disabled
  Email/Grade carry `aria-disabled`; the success Chip's meaning isn't colour-alone (the text "Active"); modals trap
  focus + `Esc`-close (the shared `sb-modal` already does this).

### F4 — The bound-device card + the three modals (contract §D.1–§D.4) — all `sb-modal size="confirm"`
Three modals, each driven by a `signal<boolean>` open flag. Reuse the confirm-modal pattern from `feature-catalogue`'s
`EnrollModalComponent` (`<sb-modal [open]="…" size="confirm" (close)="…">` + projected body + a `modalFooter` slot).

- **Bound-device card** (display-only): a smartphone icon, the device name = `boundDevice.summary` with a **generic
  fallback** ("This device" / "Your current device") when `null`; the line `"Bound {boundDevice.boundAtUtc}"` (format
  the ISO date for display); the helper *"Only one device can access content. To switch devices, contact support to
  reset the binding."*; a **"Reset device"** secondary-**sm** button → opens the device-reset INFO modal. When
  `boundDevice == null`, show a muted "No device bound yet." and keep the Reset button (it's informational either way).
  **The token hash is never present** in the model (§C.5) — there is nothing to hide client-side.

- **D.2 — Device-reset modal — INFO only, NO API** (decision 2 · `FR-STU-DEV-002` · grounding correction):
  Title **"Reset bound device?"**, a **warning `sb-alert`** *"One device only … contact support to reset … a limited
  number of times"*, and a footer with **Cancel** + an **informational "Request reset"** action. The "Request reset"
  click **just closes the modal (and may toast a "Contact support to reset your device." info)** — it calls **NO API**.
  The recovery path is the **existing staff** clear `POST /api/students/{id}/clear-device`
  (`Permission.StudentsDeviceClear`, `FR-PLAT-DEV-004`), invoked by staff out-of-band — the student modal merely tells
  them to contact support. **No new student device-reset endpoint exists** (§Out-of-scope). (Grounding correction: the
  prototype's confirm button is a no-op `closeModal` — keep it purely informational.)

- **D.3 — Change-password modal — Firebase email reset link** (decision 3 · `FR-STU-PRO-003`, `FR-PLAT-AUTH-009` ·
  grounding correction): triggered by the Security card's **"Change password"** secondary button. A confirm/info modal
  **"Send a password reset link to {email}?"** (email from `firebaseAuth.currentUser.email`, via the store, F5) →
  the action calls **`StudentAuthStore.requestPasswordReset(email)`** (the store already implements it via
  `sendPasswordResetEmail(auth, email)`, but it **requires the email argument** — it does **not** self-source the
  address; it was written for the pre-sign-in login form, so S6 must **pass in** the signed-in email from the new
  `currentEmail`/`getCurrentEmail()` store accessor, F5) → on resolve, flip the modal to a **success state "Check your
  inbox."** with a Close button. **No backend, no platform reset logic** — password is delegated **entirely** to Firebase
  (`FR-PLAT-AUTH-009`). (Grounding correction: the prototype's 3-field current/new/confirm form is **replaced** by this
  confirm/success modal — do not build a password form.)

- **D.4 — Sign-out modal:** triggered by the Security card's **"Sign out"** danger-ghost button. Title **"Sign out?"** +
  mascot (optional) + *"You will need to sign in again … your progress is saved"* + footer **"Stay signed in"** (close)
  / **"Sign out"**. **"Sign out"** → `inject(StudentAuthStore).signOut()` (Firebase `signOut` + clear the JWT pair +
  redirect `/login`) — reuse the store path, do not re-implement.

### F5 — Email + Firebase plumbing (decisions 3 + 4)
Email and password both come from Firebase via the **existing** `StudentAuthStore`, not from the API:
- **Email (read-only display, §C.2):** the component needs the signed-in Firebase email. The `StudentAuthStore` holds
  the `Auth` instance (`#firebaseAuth`). Expose a tiny read accessor on the store for the current user's email (e.g. a
  `currentEmail = computed(() => this.#firebaseAuth.currentUser?.email ?? null)` or a `getCurrentEmail()` method) and
  consume it in `ProfileComponent` — **do not** inject `Auth` directly into the feature lib (keep Firebase access behind
  the store seam, the S0 pattern). If a store accessor is undesirable, inject `Auth` from `@angular/fire/auth` in the
  component, but the store accessor is preferred for testability (the spec can stub the store). The email is **never**
  put in a form control bound to the DTO and **never** sent on PUT.
- **Password reset (§D.3):** call the store's existing `requestPasswordReset(email)` — it already wraps
  `sendPasswordResetEmail(this.#firebaseAuth, email)`, **but it requires the email argument** (it does **not**
  self-source the address — it was written for the pre-sign-in login form, where the email came from the login form).
  S6 is a **signed-in** caller, so **pass in** the value from the same `currentEmail`/`getCurrentEmail()` accessor above
  (`firebaseAuth.currentUser.email`).
- **Sign-out (§D.4):** call the store's existing `signOut()`.

> Keep Firebase behind the **`StudentAuthStore` seam** so the Jest specs can mock the store (the email accessor, the
> reset method, the sign-out method) without a real Firebase `Auth` — the S0/S2 testing pattern.

### F6 — Tests (Jest; `whenStable()`, never `fakeAsync`)
Set up like `feature-catalogue`'s component specs — `TestBed`, `fixture.componentRef.setInput(...)` where needed,
**`await fixture.whenStable()`** (not `fakeAsync`), and **mock the data-access barrel** via
`jest.mock('@sb/student-portal/data-access', …)` (the ESM-fire gotcha — re-export the `ProfileService` +
`RegistrationService` + `StudentAuthStore` stubs **and** any consts the component imports, e.g. the `StudentStatus`
type usage). Stub `ToastService` from `@sb/shared/ui`.
- `profile.service.spec.ts`: `getProfile()` hits `GET /api/me/profile` **WITH a bearer** (not exempted) and maps the
  `StudentProfile` shape incl. the nested `boundDevice` + `null` boundDevice; `updateProfile(body)` hits
  `PUT /api/me/profile` with **exactly the seven writable fields** (assert the body has **no** `gradeId`/`email`/
  `status`/`boundDevice` keys) and maps the returned DTO; a `400` flows through as an `HttpErrorResponse`; the
  `status` string-union maps correctly.
- `profile.component.spec.ts`:
  - renders the **header** (initials avatar, name, sub-line from `gradeName`/`cityName`, the **"Active" Chip**);
  - the **personal-info form** pre-fills from the loaded profile; **Email is disabled** and shows the Firebase email
    (from the stubbed store), **Grade is disabled** and shows `gradeName`;
  - changing **City** triggers a `regions(cityId)` fetch and **resets/repopulates Region** (assert `regions()` called
    with the new city id);
  - **"Save changes"** calls `updateProfile()` with the seven-field body (**no** email/grade), and on `200` re-seeds the
    form + **fires the success toast**; a `400` renders the server `detail` inline;
  - **parent phones:** Primary required (Save disabled when blank), Secondary optional (sent as `null` when blank);
  - the **bound-device card** shows `summary` + "Bound {date}", and a **generic label** when `boundDevice` is `null`;
  - the **device-reset modal** opens on "Reset device" and its confirm action **calls NO service method** (assert the
    `ProfileService`/HTTP stub is **not** touched) and just closes;
  - **"Change password"** opens the modal and its action calls **`StudentAuthStore.requestPasswordReset`** with the
    Firebase email, then shows the **"Check your inbox"** success state;
  - **"Sign out"** opens the confirm modal and its confirm calls **`StudentAuthStore.signOut`**.

## Exit criteria
A signed-in student opens **Profile** from the now-live shell nav and sees their header band (initials avatar +
name + grade/city sub-line + **"Active"** chip), edits **Full name / School / City→Region / parent phones** in a typed
reactive form with **Email and Grade disabled**, and **"Save changes"** PUTs the **seven writable fields** (grade +
email never sent), re-renders from the returned DTO, and toasts success; a `400` on a bad/mismatched city/region renders
inline. The **bound-device** card shows the active device summary + bind date (generic label when none) and the **"Reset
device"** button opens an **informational contact-support modal that calls no API**; **"Change password"** fires
Firebase `sendPasswordResetEmail` (no form, no backend) and shows "Check your inbox"; **"Sign out"** confirms then runs
the store sign-out (clears the JWT pair + redirects `/login`). The screen is responsive + a11y-clean on
phone/tablet/desktop. `npx nx build student-portal` (AOT) + `nx test student-portal-feature-profile` green. Hand to
wiring — the **final** S6 wiring **closes the student-portal plan (S0..S6)**.

## Out of scope (defer / NOT built)
- **Real avatar upload (decision 1).** S6 ships **initials-only** via `sb-avatar` — **no upload control, no `Avatar`
  field on `Student`, no DTO field, no `FileUpload`, no `IFileStorage` use, no migration, no storage work.**
  `FR-STU-PRO-001` mentions an avatar; the real avatar upload is **DEFERRED** (a future slice would add an avatar object
  key + signed read URL + a migration). Recorded so it isn't silently dropped (contract §F).
- **No student device-reset endpoint / no device-reset API call (decision 2).** The "Reset device" button is
  contact-support-only; the recovery path is the existing **staff** `POST /api/students/{id}/clear-device`
  (`FR-PLAT-DEV-004`), invoked out-of-band. No new student-facing reset endpoint, no student `IDeviceBindingService`
  route (contract §F).
- **No password form / no password backend (decision 3).** Change-password is `sendPasswordResetEmail` client-side; the
  prototype's 3-field form is replaced. The platform builds **no** reset email/logic (`FR-PLAT-AUTH-009`).
- **Email not editable (decision 4).** Email is read-only Firebase identity, not stored on `Student`, not in either
  DTO; changing it is a Firebase identity operation, out of scope.
- **No new reference endpoint** — the City/Region dropdowns **reuse** the existing anon `/api/reference/*` reads via
  `RegistrationService` (§C.4). **No grades read** — grade is read-only.
- **No new shell screen beyond Profile**, and **no change to other student screens** — the only `feature-shell` touch is
  flipping the Profile nav `disabled` in `NAV_ITEMS` + `BOTTOM_ITEMS` (F1).
- **No backend in this stream** — `frontend/**` only. The two new endpoints + the new `Student.UpdateOwnProfile(...)`
  domain method are the **backend** stream's work (contract §G).

## Frozen vs. stream-owned
- **Frozen (the contract, `docs/contracts/student-s6-profile.md`):** the two routes `GET` + `PUT /api/me/profile` +
  `RequireStudent`; the `StudentProfileDto` field names/types incl. nested `boundDevice` and the **absence** of
  `email`/`avatar` (§A.1); the `UpdateMyStudentProfileRequest` **seven** writable fields + the **exclusion** of
  `gradeId`/email (§A.2); the validation rules (§A.2); the error modes (`401`/`403`/`400`, **no 404-self**, **no 409**,
  §B); the writable-vs-read-only split (§C.1); email read-only-from-Firebase (§C.2); the city/region cascade +
  existence `400` + reference-reuse (§C.3/§C.4); the bound-device read shape with the token hash never exposed (§C.5);
  the four decisions + the three modal behaviours (§D); "GET not audited / PUT audited via interceptor" (§E); the
  deferred set (§F).
- **Backend owns (separate stream):** the new `MeProfileEndpoints : IEndpointGroup`, the `GetMyStudentProfile` query +
  `UpdateMyStudentProfile` command, the `StudentProfileDto` + `.ToProfileDto()` mapping, the new
  `Student.UpdateOwnProfile(...)` domain method (leaves grade unchanged), the validator + city/region existence `400`,
  the grade/city/region name joins, the active-`StudentDevice` projection, and the integration tests.
- **Frontend owns (THIS stream):** the new `libs/student-portal/feature-profile` lib (`student-portal-feature-profile`,
  tags + prefix + `@nx/jest`, barrel exports `ProfileComponent`) + the `@sb/student-portal/feature-profile` alias + the
  `profile` route + flipping the shell Profile nav `disabled: false` in **both** `NAV_ITEMS` and `BOTTOM_ITEMS`; the
  `ProfileService` + wire-exact `StudentProfile`/`BoundDevice`/`UpdateMyStudentProfile` models in `data-access`; the
  `ProfileComponent` (header band + initials Avatar + personal-info typed reactive form with **Email disabled** +
  **Grade disabled** + City→Region cascade reusing the reference data-access + parent-phone subsection + Save→PUT→
  refresh+toast) and the three `sb-modal size="confirm"` modals (device-reset **INFO/no-API**, Firebase change-password,
  sign-out) wired to the `StudentAuthStore` (`requestPasswordReset` / `signOut`) + the read-only email accessor; the
  Jest specs (`whenStable()`, mock the data-access barrel).
- **Wiring owns (separate stream, FINAL — closes S0..S6):** proving the slice live on the Aspire stack — `GET` returns
  the caller's data + resolved grade/city/region names + the active bound-device summary (tenant-isolated, no token
  hash); `PUT` updates the seven writable fields, **leaves grade unchanged**, ignores any email/grade in the body,
  writes a `Student` audit row via the interceptor, and re-reads correctly; `400` on a bad/mismatched city/region;
  `401` anon / `403` staff; the contact-support device modal fires **no** API; change-password fires Firebase
  `sendPasswordResetEmail`; sign-out clears the JWT pair + redirects.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the FRONTEND stream of Student-Portal phase S6 (self-service Profile) for Salah Bahzad
(Angular v20+, Nx). Edit frontend/** ONLY. This is the FINAL student-portal slice — it CLOSES the plan (S0..S6).
The app, shell, auth, catalogue, sessions, assignments, and quizzes already exist from S0..S5 — you add a NEW
libs/student-portal/feature-profile lib and flip the shell's disabled Profile nav item live.

Read first, in order:
1. frontend/CLAUDE.md (Angular v20+ conventions, tokens, icons, module boundaries).
2. docs/IMPLEMENTATION-PLAN-student-s6-frontend.md — THIS doc. DESIGN SOURCE OF TRUTH =
   .claude/Salah Bahzad Student Portal/Student Portal.html, the PROFILE section + its Change-password / Device-reset /
   Sign-out modals (header band with an INITIALS-ONLY xl avatar + name + grade/city sub-line + "Active" chip; a
   Personal information card with Full name [edit], Email [READ-ONLY/disabled — prototype is wrong, it's not editable
   and not stored], School [edit], Grade [DISABLED], City + Region dropdowns; a Parent/guardian numbers subsection
   [Primary required, Secondary optional]; "Save changes"; a Bound device card; a Security card with Change password +
   Sign out). GROUNDING CORRECTIONS vs the prototype: Email is read-only; the device-reset confirm is a no-op
   contact-support action [NO API]; the 3-field change-password form is REPLACED by a Firebase reset-email modal.
3. docs/contracts/student-s6-profile.md — the FROZEN contract: §A (the TWO new endpoints — GET /api/me/profile ->
   StudentProfileDto [NO email, NO avatar; nested boundDevice {summary,boundAtUtc}; gradeName/cityName/regionName
   display names; status string], PUT /api/me/profile {fullName, phoneNumber, schoolName, cityId, regionId,
   parentPhonePrimary, parentPhoneSecondary} -> updated StudentProfileDto [grade + email NOT updatable]); §B (errors:
   401 anon / 403 staff / 400 validation incl. unknown-or-mismatched city/region; NO 404-self, NO 409); §C (writable
   vs read-only; email read-only from Firebase; city->region cascade reusing /api/reference/*; bound-device read,
   token hash NEVER exposed); §D (the screen + the THREE modals — device-reset INFO/no-API, Firebase change-password,
   sign-out confirm). Four binding decisions: avatar=initials-only, device-reset=contact-support-only/no-API,
   password=Firebase sendPasswordResetEmail, email=read-only Firebase identity.
4. The code to reuse/port: libs/student-portal/data-access (CatalogueService is the ProfileService pattern — these are
   AUTHENTICATED, ride studentAuthInterceptor, do NOT add /api/me/profile to ANONYMOUS_PATHS; RegistrationService
   cities()/regions(cityId) are the EXISTING anon /api/reference reads to REUSE for City->Region; StudentAuthStore
   already exposes requestPasswordReset(email) [sendPasswordResetEmail — REQUIRES the email argument, does NOT
   self-source it; S6 must pass currentUser.email via a new currentEmail/getCurrentEmail() store accessor] + signOut()
   and holds the Firebase Auth for the read-only email); libs/student-portal/feature-catalogue (project.json shape, enroll-modal.component.ts is the
   sb-modal size="confirm" confirm-modal pattern, the component spec is the Jest setup template);
   libs/student-portal/feature-shell/.../shell.component.ts (flip the Profile item disabled:true->false in BOTH
   NAV_ITEMS and BOTTOM_ITEMS); the app.routes.ts + tsconfig.base.json alias pattern; @sb/shared/ui
   (Avatar[size xl, initials, status="active"] / StatusPill[variant success] / Card / Button / Select / FormField /
   Alert / Modal[size="confirm", modalFooter slot] / ToastService). Do NOT use FileUpload (no avatar upload). NEVER
   import an admin-portal lib.

Build: scaffold libs/student-portal/feature-profile (name student-portal-feature-profile, tags
scope:student-portal/type:feature, prefix sb, @nx/jest) AND wire its tsconfig alias + the /profile route under the
authenticated shell AND flip the shell Profile nav disabled:false in both NAV_ITEMS + BOTTOM_ITEMS (an unrouted lib
still builds green — prove /profile resolves at :4300). A ProfileService (getProfile(), updateProfile(body) —
authenticated; wire-exact StudentProfile [incl. boundDevice|null, gradeName, NO email/avatar] vs UpdateMyStudentProfile
[the seven writable fields, NO gradeId/email/status]; use |null not ?). A ProfileComponent (gradient header band +
INITIALS-ONLY xl Avatar + "Active" chip + grade/city sub-line; a typed reactive form — Full name/School editable, Email
+ Grade DISABLED, City->Region cascade dropdowns from the reused /api/reference reads, parent Primary required /
Secondary optional; "Save changes" -> PUT then re-seed + success toast; 400 -> inline detail) + a Bound device card
(summary + "Bound {date}", generic label when null, "Reset device" -> INFO modal that calls NO API) + a Security card
(Change password -> StudentAuthStore.requestPasswordReset(email) confirm/"Check your inbox" modal; Sign out -> confirm
-> StudentAuthStore.signOut()). Email read-only from firebaseAuth.currentUser.email via a StudentAuthStore accessor
(keep Firebase behind the store seam). NO avatar upload, NO device-reset API, NO password form/backend, email not
sent on PUT.

Jest with whenStable() (NOT fakeAsync; mock the data-access barrel via jest.mock; setup like
catalogue.component.spec.ts): the service hits GET/PUT /api/me/profile WITH a bearer and the PUT body has exactly the
seven writable fields (NO gradeId/email); the component pre-fills the form, Email + Grade are disabled, changing City
re-fetches Region, Save calls updateProfile with the seven-field body + toasts, 400 renders inline, parent Primary
required + Secondary optional(null), the bound-device card + generic fallback, the device-reset modal calls NO service,
Change password calls requestPasswordReset + shows "Check your inbox", Sign out calls signOut. Responsive
(FR-STU-RWD-001/002) + a11y (FR-STU-A11Y-001, NFR-A11Y-001/002). Green gate: `npx nx build student-portal` +
`nx test student-portal-feature-profile`. Report both.
```
