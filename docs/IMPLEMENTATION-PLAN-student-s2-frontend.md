# Student Portal · S2 — FRONTEND stream (Catalogue & enroll-by-code)

> Status: **Planned — not yet built** · Created 2026-06-21 · The **app half** of slice **S2** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S2). Builds the **catalogue** discovery screen + the **enroll-by-code
> modal** into a **new** `libs/student-portal/feature-catalogue` lib. The S0 shell already renders a guarded layout with
> a sidebar/bottom-nav (incl. the centre **Redeem** FAB) and an authenticated student session; S2 fills the catalogue
> route the shell links to and wires the Redeem FAB to the enroll modal.
>
> Run in its **own** Claude session, parallel-safe with the backend stream. **File ownership: `frontend/**` only.**
> Match the **frozen contract** (`docs/contracts/student-s2-catalogue-enroll.md`) field-for-field — the
> `CatalogueSessionDto` shape (§A.2), the `enrollmentState`/`prerequisiteSatisfied` semantics (§C), and the redeem body
> + the six 409 `detail` strings (§B).
>
> Satisfies: `FR-STU-CAT-001..005`, `FR-STU-RWD-001/002` (responsive), `FR-STU-A11Y-001` (a11y). Green gate:
> `npx nx build student-portal` (AOT type-checks templates) + `nx test student-portal-feature-catalogue`.

---

## Design source of truth (the student prototype, NOT the Teacher portal)

- **Prototype:** `.claude/Salah Bahzad Student Portal/Student Portal.html` (siblings `Dropdown.jsx`, `support.js`).
  The banners are **`<!-- ===== CATALOGUE ===== -->`** and the **`Enroll modal`** (and the unbuilt **`Request a spot
  modal`**). S2 builds:
  - the **catalogue**: a header, a **specialization filter** (chip-bar), a **cards grid** of `SessionThumb` tiles
    (thumbnail, title, grade/subject/spec, price `EGP n` / **Free**, **prerequisite badge**, an **Enroll** or **Open**
    CTA), and a **mascot empty state** when there are no sessions / the filter matches none;
  - the **enroll modal**: a guided **segmented 16-char code input with paste support** (`CodeInput`), inline validation,
    a redeem action, and a success state.
- **Tokens / assets / icons / fonts:** already mirrored into `apps/student-portal` by S0 — **reuse**, don't re-mirror.
  Mascot art for the empty/success states: the same `assets/salah-*.png` S0/S1 use. Outline icons inline via
  `DomSanitizer.bypassSecurityTrustHtml` (the admin/S0 pattern; Angular strips `<svg>` from plain `[innerHTML]`).
- When prototype and this doc conflict, **the prototype wins** on layout/copy; **the contract wins** on field names,
  the redeem body/errors, and the enrollment/prerequisite semantics.

---

## Conventions (mirror `frontend/CLAUDE.md` + master plan §3.2)
- **New lib** `libs/student-portal/feature-catalogue` — `project.json` tags `["scope:student-portal","type:feature"]`,
  `prefix:"sb"`, `@nx/jest` test target (byte-for-byte the shape of the S0 `feature-shell`/`feature-auth` project.json).
  **You must also** add the `@sb/student-portal/feature-catalogue` path alias to `frontend/tsconfig.base.json`, the route
  entry to `apps/student-portal/src/app/app.routes.ts`, **and** the nav item to the shell — an unrouted lib still builds
  green (the S1-wiring "unrouted feature-attendance" gotcha); prove the route resolves at `:4300/catalogue`.
- **Module boundaries:** `scope:student-portal` → `scope:shared` only. Reuse `@sb/shared/ui`; **never** import an
  admin-portal lib (the admin codes screens are staff-shaped — port patterns, don't cross-import). The shell's **Redeem
  FAB** lives in `feature-shell`, which **cannot** import `feature-catalogue` — so the FAB **navigates** (routing) to a
  catalogue redeem route that opens the modal; it does not import the modal component.
- Angular v20+: standalone, `OnPush`, signal `input()/output()/model()`, `computed()/effect()`, `inject()`, native
  control flow, **typed reactive forms**, **`ControlValueAccessor`** for the `CodeInput`. Cite `FR-*`/`NFR-*` in tests.
- **Reuse the shared UI:** `Button` (+ variants), `Modal` (`size="confirm"`), `Tag`/`Chip`, `Alert`, `Input`. Add the
  **student-specific** `CodeInput` (segmented) + `SessionThumb` (card) to a new `libs/student-portal/ui` (master plan
  §4.2) — or keep them inside `feature-catalogue` if not yet reused elsewhere (S3 will reuse `SessionThumb`, so promoting
  to `libs/student-portal/ui` now is the cleaner call).

---

## Steps

### F1 — Lib scaffold + wiring (avoid the unrouted-lib trap)
- `nx g @nx/angular:library feature-catalogue --directory=libs/student-portal/feature-catalogue` (or copy a sibling's
  `project.json`); confirm the **tags**, `prefix:"sb"`, and the `@nx/jest` target.
- Add `@sb/student-portal/feature-catalogue → libs/student-portal/feature-catalogue/src/index.ts` to
  `frontend/tsconfig.base.json`.
- Add a **lazy route** `{ path: 'catalogue', loadComponent: … }` (the shell's default/home landing) to
  `app.routes.ts`, under the **authenticated shell** + student guard (reuse S0's guard). Add the **"Catalogue"/"Browse"**
  nav item (sidebar + bottom-nav) pointing at it. Confirm `:4300/catalogue` resolves (not just that the build is green).

### F2 — Data access: `CatalogueService` (authenticated — bearer + refresh apply)
In `libs/student-portal/data-access` (beside the S0/S1 student services), add a `CatalogueService`. **Unlike S1's
anonymous reads, these are authenticated** — they ride the existing `studentAuthInterceptor` (bearer attached, 401→refresh
replay). **Do not** exempt them.
- `catalogue(filters?): Observable<CatalogueSession[]>` → `GET /api/me/catalogue` (+ optional `gradeId/subjectId/
  specializationId/search` query params, contract §A.1) → the `CatalogueSessionDto[]` shape (§A.2). Model
  `enrollmentState` as the string union `'NotEnrolled' | 'Enrolled' | 'Expired' | 'Refunded'`.
- `redeem(serial): Observable<Enrollment>` → `POST /api/enrollments/redeem` with body `{ serial }` (contract §B.1) →
  `EnrollmentDto` (§B.2). **No `Content-Type` fuss** — it's plain JSON.
- Map both DTOs to TS models in the data-access lib; export them from its barrel.

### F3 — `CodeInput` (segmented 16-char, paste) — `ControlValueAccessor`
A reusable control: **segmented boxes** for the `SB-XXXXX-XXXXX` serial (the prototype's stepped entry), auto-advance on
type, **backspace** to the previous box, and **paste** that distributes a pasted serial across the boxes (strip dashes/
spaces, upper-case). Emits the assembled serial string. Implement `ControlValueAccessor` so the enroll form binds it like
any input. a11y: each box labelled, the group has an accessible name, paste works via keyboard. *(This is the only net-new
custom control in S2; everything else is shared UI.)*

### F4 — `SessionThumb` (the catalogue card)
A presentational `OnPush` card from a `CatalogueSession`: thumbnail (`thumbnailUrl`, fallback to a tinted placeholder when
null), title, grade/subject/specialization line, **price** (`EGP n` or **Free** when `price === 0`), a **video count** +
**validity** hint (`access N days` / `no expiry`), a **prerequisite badge** when `prerequisiteSessionId != null`, and a
CTA driven by `enrollmentState` (§C.1):
- `NotEnrolled` / `Expired` / `Refunded` → **Enroll** → opens the enroll modal (F5).
- `Enrolled` → **Open** → routes to the session detail (S3; until S3 ships, route to a `/sessions` placeholder or disable
  with a "Coming soon" note — **do not** block the build on S3).
- **Prerequisite unmet** (`prerequisiteSatisfied === false`): the **Enroll CTA is disabled** with a "Complete
  *{prerequisiteTitle}* first" hint (`FR-STU-CAT-002`). The server still enforces it (§B.3) — this is UX, not the gate.

### F5 — `EnrollModal` (the guided code redemption) — `FR-STU-CAT-003/004/005`
A `Modal` (`size="confirm"`) containing the `CodeInput` + a redeem button + inline error area. Opened **two ways**: from a
card's **Enroll** CTA (pre-scoped to that session — optional `sessionId` context for the success nav) and from the shell's
**Redeem FAB** (general — redeem whatever code, then land on the resulting session). Behaviour:
- **Submit** → `redeem(serial)`.
- **`201`** → success state; **refresh the catalogue** (the just-enrolled card flips `NotEnrolled → Enrolled`, CTA →
  Open); offer **Go to session** (routes to S3 when built, else the catalogue). The code is now consumed server-side
  (`FR-STU-CAT-004`).
- **Errors (`FR-STU-CAT-005`)** — **render the server's `problem.detail` verbatim** (the six §B.3 strings are already
  specific + user-safe: invalid/used/disabled code, price mismatch, already-enrolled, prerequisite unmet); show it inline
  under the `CodeInput`. A **`400`** (empty/too-long serial) → inline "Enter a valid code." (mirror the validator). Keep
  the entered serial on error (don't clear the boxes). Disable the button + show a spinner while in flight.

### F6 — `CatalogueComponent` (the screen)
A standalone `OnPush` component under the shell + student guard:
- On init, `catalogue()` → render the `SessionThumb` grid. Loading skeleton; **mascot empty state** when the list is
  empty (`FR-STU-CAT-001`).
- **Specialization filter (chip-bar):** derive the distinct `{ specializationId, specializationName }` from the loaded
  set and render selectable chips (an **All** chip + one per spec); filtering is **client-side** over the loaded list (no
  re-fetch needed — the published set is small, contract §A.2). *(A search box / grade filter is optional polish; the
  endpoint supports them — wire them only if the prototype shows them.)*
- **Responsive (`FR-STU-RWD-001/002`):** a multi-column grid on desktop/tablet → single column on phone; comfortable
  touch targets; the filter chips wrap/scroll on narrow widths. **a11y (`FR-STU-A11Y-001`):** the grid is a labelled
  list, chips are toggle buttons with `aria-pressed`, the modal traps focus + restores it on close, CTAs are reachable.

### F7 — Wire the shell's Redeem FAB (routing, not import)
The S0 shell's centre **Redeem** FAB should open the enroll modal. Respecting the module boundary (shell can't import
`feature-catalogue`), the FAB **navigates** to a catalogue redeem entry — e.g. route to `/catalogue` with a `redeem`
query/flag (or a dedicated `/catalogue/redeem` child route) that the `CatalogueComponent` reads to auto-open the
`EnrollModal`. Confirm the FAB opens the modal from anywhere in the shell.

### F8 — Tests (Jest; `whenStable()`, never `fakeAsync` — the 5B-1 jsdom gotcha)
- `code-input.component.spec.ts`: typing advances boxes; backspace retreats; **paste** of `SB-ABCDE-FGHIJ` (and a
  dash-less / lower-case variant) fills the boxes and emits the normalized serial; `ControlValueAccessor` round-trips a
  value.
- `catalogue.component.spec.ts`: renders a `SessionThumb` per item; **empty list → mascot empty state**; the **spec
  chip-bar** filters client-side (selecting a spec hides non-matching cards; **All** restores); an `Enrolled` card shows
  **Open**, a `NotEnrolled` shows **Enroll**, and a `prerequisiteSatisfied:false` card shows a **disabled** Enroll + the
  prereq hint.
- `enroll-modal.component.spec.ts`: submit → `POST /api/enrollments/redeem` with `{ serial }` (exact body); **`201`** →
  success + a catalogue refresh requested + the card flips to `Enrolled`; **`409`** with each `detail` string → that
  string rendered inline (drive a couple: invalid code, already-enrolled, prerequisite unmet); **`400`** → "Enter a valid
  code"; the serial is preserved on error. Driver: `whenStable()`.
- `catalogue.service.spec.ts`: `catalogue()` hits `/api/me/catalogue` (with params when given) **with** a bearer (not
  exempted); `redeem()` posts `{ serial }`; both map the DTOs correctly.

## Exit criteria
A signed-in student opens **Catalogue**, sees the tenant's published sessions as cards (price/prereq badge/CTA), filters
by specialization, and enrolls via the segmented code modal (from a card **or** the Redeem FAB); on success the card flips
to **Enrolled/Open** and the code is consumed; a prerequisite-unmet card disables Enroll with the right hint; every redeem
error (invalid/used/disabled/price-mismatch/already-enrolled/prereq-unmet + the 400) renders a specific message; the
screen is responsive + a11y-clean on phone/tablet/desktop. `npx nx build student-portal` (AOT) +
`nx test student-portal-feature-catalogue` green. Hand to wiring.

## Out of scope (defer)
The **"Request a spot"** offline-code request modal (deferred, master plan §3.3 / contract §E — **not built**); the
**session detail / My Sessions / video** screens (S3 — the `Open` CTA routes there once it lands; a placeholder until
then); profile (S6); any change to the **redeem** engine (reused as-is); server-side pagination (the flat list is frozen,
contract §A.2); a grade/subject server filter UI beyond what the prototype shows (the endpoint supports them, but the spec
chip-bar is the deliverable).

---

## Kickoff prompt (paste into a fresh Claude session at the repo root)

```
You are implementing the FRONTEND stream of Student-Portal phase S2 (the catalogue + enroll-by-code modal) for Salah
Bahzad (Angular v20+, Nx). Edit frontend/** ONLY. The app, shell (with a Redeem FAB), auth, and student session already
exist from S0/S1 — you add a NEW libs/student-portal/feature-catalogue lib.

Read first, in order:
1. frontend/CLAUDE.md (Angular v20+ conventions, tokens, icons, module boundaries).
2. docs/IMPLEMENTATION-PLAN-student-s2-frontend.md — THIS doc. DESIGN SOURCE OF TRUTH =
   .claude/Salah Bahzad Student Portal/Student Portal.html banners CATALOGUE + Enroll modal (Request-a-spot is NOT built).
3. docs/contracts/student-s2-catalogue-enroll.md — the FROZEN contract: §A (GET /api/me/catalogue + CatalogueSessionDto),
   §C (enrollmentState / prerequisiteSatisfied semantics), §B (POST /api/enrollments/redeem body {serial} + the six 409
   detail strings + 400). Render the server's problem.detail verbatim for redeem errors.
4. The S0/S1 code to reuse/port: libs/student-portal/feature-shell (the Redeem FAB + nav), the studentAuthInterceptor +
   StudentAuthStore (catalogue + redeem are AUTHENTICATED — bearer + refresh apply, do NOT exempt them), and the
   app.routes.ts + tsconfig.base.json alias pattern.

Build: scaffold libs/student-portal/feature-catalogue (tags scope:student-portal/type:feature, prefix sb, @nx/jest) AND
wire its tsconfig alias + app.routes.ts route + shell nav item (an unrouted lib still builds green — prove /catalogue
resolves at :4300). A CatalogueService (catalogue() + redeem({serial}), authenticated). A CodeInput segmented 16-char
ControlValueAccessor with paste. A SessionThumb card (price EGP/Free, prereq badge, Enroll/Open CTA by enrollmentState;
disabled Enroll + hint when prerequisiteSatisfied=false). An EnrollModal (CodeInput -> redeem -> 201 refresh + card flips
to Enrolled; render problem.detail on 409; "Enter a valid code" on 400; keep serial on error). A CatalogueComponent
(cards grid + client-side specialization chip-bar + mascot empty state). Wire the shell Redeem FAB to open the modal via
ROUTING (not import — module boundary).

Jest with whenStable() (NOT fakeAsync): CodeInput type/backspace/paste + CVA; catalogue render + empty state + spec
filter + CTA-by-state + disabled-on-unmet-prereq; enroll modal exact {serial} body + 201 flip + each 409 detail rendered
+ 400 + serial preserved; service hits the right paths WITH a bearer. Responsive + a11y. Green gate:
`npx nx build student-portal` + `nx test student-portal-feature-catalogue`. Report both.
```
