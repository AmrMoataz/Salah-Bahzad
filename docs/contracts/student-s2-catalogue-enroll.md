# FROZEN CONTRACT — Student Portal · S2 · Catalogue & enrollment

> Status: **Frozen** · Created 2026-06-21 · Slice: Student-Portal **S2** in
> `docs/IMPLEMENTATION-PLAN-student-portal.md` (§S2). **Design anchor:** the prototype's **`CATALOGUE`** section +
> its **`Enroll modal`** (and the deliberately-unbuilt **`Request a spot modal`**) in
> `.claude/Salah Bahzad Student Portal/Student Portal.html`. Behaviour authority is `FR-STU-CAT-001..005`,
> `FR-PLAT-SES-008` (publish-to-catalogue), `FR-PLAT-ENR-001/004/005/006/007` (redeem, extend-in-place, side-effects,
> one-active, prerequisite gate).
>
> Satisfies: browse the published catalogue filtered by grade/subject/specialization with price + prerequisite badge
> (`FR-STU-CAT-001/002`); enroll by code in a guided modal (`FR-STU-CAT-003`); on success the session moves to My
> Sessions, the code is consumed, and assignment/quiz/video access is provisioned (`FR-STU-CAT-004`); every failure
> shows a specific message (`FR-STU-CAT-005`). **Half already built** — the **redeem engine** shipped in Phase 4
> (`POST /api/enrollments/redeem`, `RequireStudent`, proven live). This contract **freezes that engine as-is** and adds
> **one** new student read — **`GET /api/me/catalogue`**. Frontend + wiring cite this file field-for-field.
> **Change this file first if anything moves.**

## 0. Ground rules

- **Backend = one new read + the frozen redeem engine.** The deliverable screen is `feature-catalogue`; the backend adds
  **only** `GET /api/me/catalogue` (the discovery read). Redeem (`POST /api/enrollments/redeem`) is **reused verbatim**
  from Phase 4 — no signature change. **No new aggregate, no migration.**
- **Authenticated student surface.** Both endpoints use **`RequireStudent()`** (anon → 401, staff → 403) — identical to
  `/api/me/assignments|quizzes|videos` and the existing `/api/enrollments/redeem`. **The student id + tenant come from
  the JWT** (`ICurrentUserResolver.UserId` / `.TenantId`), never a URL id — no IDOR surface (`NFR-SEC-007`).
- **Tenant isolation is automatic.** The EF global query filter scopes `Sessions`/`Enrollments` to the caller's tenant
  and excludes soft-deleted rows. **Never** write a per-handler `Where(x => x.TenantId == …)`. Cross-tenant isolation is
  covered by an integration test on the new read (`NFR-SEC-010`).
- **Catalogue = published only.** `GET /api/me/catalogue` returns sessions with **`Status == Published`** (`FR-PLAT-SES-008`).
  `Draft` and `Archived` sessions never appear. The read is **not paginated** (a tenant's published set is small — a
  scrollable cards grid, not a table; see `§A.2` rationale).
- **Reads are not audited; redeem is.** `GET /api/me/catalogue` is a pure read — **not** audited (consistent with the
  other `/api/me/*` reads). `POST /api/enrollments/redeem` **is** audited (Phase-4 `CodeRedeemed`, already implemented).
- **Money & enums over the wire.** `decimal` EGP rendered `EGP {value}` by the UI; enums are **string names**
  (`JsonStringEnumConverter`) — the frontend models them as string unions. Dates are ISO-8601 `…AtUtc`.

## A. Catalogue — `GET /api/me/catalogue` (NEW · `RequireStudent`)

`RequireStudent` · `200 IReadOnlyList<CatalogueSessionDto>`. Returns the tenant's **published** sessions, each carrying
display fields, a **prerequisite** badge + satisfied flag, and **the caller's own enrollment state** for that session.

### A.1 Query parameters (all optional — narrowing filters, `FR-STU-CAT-001`)

| Param | Type | Notes |
|---|---|---|
| `gradeId` | guid? | Restrict to one grade. |
| `subjectId` | guid? | Restrict to one subject (matched via the session's specialization → `Specialization.SubjectId`, `FR-PLAT-TAX-002`). |
| `specializationId` | guid? | Restrict to one specialization. |
| `search` | string? | Case-insensitive substring over `Title`. |

> The **frontend's primary control is a specialization chip-bar** built client-side from the distinct specializations in
> the returned set (the prototype's `CATALOGUE` filter) — so the happy path calls this endpoint **with no params** and
> filters in the browser. The server params exist for completeness/future server-side narrowing and **must** be honoured
> (and are the cheap way to write the filter tests). The student's grade is **not** auto-applied server-side (S6 profile
> is not built yet); browsing all published sessions + client spec filtering matches the prototype.

### A.2 Result — `CatalogueSessionDto` (shaped to the prototype's `SessionThumb` card)

```jsonc
// 200 · IReadOnlyList<CatalogueSessionDto> — ordered by CreatedAtUtc DESC (newest first)
{
  "id": "guid",
  "title": "string",
  "description": "string|null",
  "price": 0,                          // decimal EGP; 0 == "Free"
  "thumbnailUrl": "string|null",       // short-lived signed R2 URL (same pattern as SessionDetailDto.thumbnailUrl); null if no thumbnail
  "gradeId": "guid",   "gradeName": "string|null",
  "subjectId": "guid", "subjectName": "string|null",        // derived via the specialization
  "specializationId": "guid", "specializationName": "string|null",
  "videoCount": 0,                     // for the card's "N videos" line
  "validityDays": 0,                   // 0 == "no expiry"; else "access for N days"
  // prerequisite (FR-STU-CAT-002)
  "prerequisiteSessionId": "guid|null",
  "prerequisiteTitle": "string|null",
  "prerequisiteSatisfied": true,       // see §C.2 — vacuously true when there is no prerequisite
  // the CALLER's state for this session (FR-STU-CAT-004) — see §C.1
  "enrollmentState": "NotEnrolled",    // "NotEnrolled" | "Enrolled" | "Expired" | "Refunded"
  "enrolledExpiresAtUtc": "…|null"     // when Enrolled: the active enrollment's expiry (null == no-expiry session); else null
}
```

> **Rationale for a flat (non-paged) list:** the catalogue is a per-tenant **published** set (tens of sessions, not the
> admin's full draft+archived corpus), rendered as a single scrollable cards grid with client-side spec filtering — so a
> `PagedResult` would only add ceremony. If a tenant's published set ever grows large this is a drop-in upgrade to
> `PagedResult<CatalogueSessionDto>` with no DTO change (documented so it isn't a silent gap).

### A.3 Error modes — ProblemDetails

| Status | When |
|---|---|
| `401` | No bearer (anonymous). |
| `403` | A **staff** JWT (the `RequireStudent` filter rejects non-students). |
| `200` | Always otherwise — an empty `[]` when the tenant has no published sessions (the UI shows the mascot empty state). |

## B. Redeem — `POST /api/enrollments/redeem` (EXISTS — frozen as-is, Phase 4 #12)

`RequireStudent` · body `{ "serial": "string" }` · returns `201 Created` `EnrollmentDto`. **No change in S2** —
documented so the frontend builds to it exactly. The student + tenant come from the JWT.

### B.1 Request

```jsonc
{ "serial": "SB-XXXXX-XXXXX" }   // trimmed + upper-cased server-side; the segmented CodeInput emits the 16-char serial
```

### B.2 Result — `EnrollmentDto` (contract reused from `docs/contracts/phase4-codes-enrollment.md` §1)

```jsonc
// 201 Created · Location: /api/enrollments/{id}
{
  "id": "guid",
  "studentId": "guid", "studentName": "string|null",   // the caller's own — no IDOR
  "sessionId": "guid", "sessionTitle": "string|null",
  "status": "Active", "method": "Code", "amount": 0,
  "codeId": "guid|null", "codeSerial": "string|null",
  "enrolledAtUtc": "…", "expiresAtUtc": "…|null"        // null when the session's validityDays == 0
}
```

### B.3 Error modes (frozen — from `RedeemCodeHandler` + `RedeemCodeValidator`) — each maps to a specific UI message (`FR-STU-CAT-005`)

| Status | Server `detail` (verbatim — render it) | Cause |
|---|---|---|
| `400` | FluentValidation message | `serial` empty or > 20 chars (`RedeemCodeValidator`). |
| `409` | `"This code is invalid or no longer available."` | Serial not found / soft-deleted / wrong tenant. |
| `409` | `"This code is not available for redemption."` | Code exists but `Status != Active` (disabled or already **Used**). |
| `409` | `"This code's value no longer matches the session price."` | Price changed since the code was minted (`FR-PLAT-COD-003`). |
| `409` | `"This student already has an active enrollment for this session."` | One-active rule (`FR-PLAT-ENR-006`). |
| `409` | `"Complete the prerequisite assignment first."` | Prerequisite gate (`FR-PLAT-ENR-007`). |

> **Frozen:** the path, the `{ serial }` body, the `EnrollmentDto` shape, and the `400`/`409` statuses. The six 409
> `detail` strings above are **the server's** — the frontend renders `problem.detail` (already user-safe + specific), so
> "each failure shows a specific message" (`FR-STU-CAT-005`) is satisfied without the frontend re-deriving copy. A
> `404` is **not** part of redeem (a missing code is a `409` by design — Phase-4 decision, do not expect 404).

### B.4 Server behaviour (reference — already implemented, do NOT rebuild)

Resolve code by serial (`409` if missing/inactive) → load its session (+ videos) → re-check `code.Value == session.Price`
(`409`) → `EnrollmentWorkflow.EnrollOrExtendAsync` (enforces the **prerequisite gate** `409` and **one-active** `409`;
**provisions per-video access counters, the payment, the attendance shell, and raises `EnrollmentCreated`** →
assignment/quiz snapshots, `FR-PLAT-ENR-005`) → `code.MarkRedeemed` → save (transactional) → `CodeRedeemed` audit.
**Re-enroll/extend is in place** (`FR-PLAT-ENR-004`): redeeming a fresh code for a session whose enrollment is
`Expired`/`Refunded` re-activates the existing row (resets counters, pushes expiry) rather than 409-ing.

## C. Per-caller computation (frozen rules the new read must implement)

### C.1 `enrollmentState` + `enrolledExpiresAtUtc` (FR-STU-CAT-004)

A student holds **at most one** enrollment row per session (`FR-PLAT-ENR-006`). Load the caller's enrollment for each
catalogue session (`db.Enrollments.Where(e => e.StudentId == currentUser.UserId && sessionIds.Contains(e.SessionId))`,
tenant + soft-delete filtered automatically), then:

| Row | `enrollmentState` | `enrolledExpiresAtUtc` | Card CTA |
|---|---|---|---|
| none | `NotEnrolled` | `null` | **Enroll** (opens the code modal) |
| `Status == Active` and (`ExpiresAtUtc == null` **or** `ExpiresAtUtc > now`) | `Enrolled` | `ExpiresAtUtc` | **Open** (go to the session — S3) |
| `Status == Active` and `ExpiresAtUtc <= now` | `Expired` | `null` | **Enroll** (re-redeem extends in place) |
| `Status == Refunded` | `Refunded` | `null` | **Enroll** (re-redeem extends in place) |

> **Important:** expiry is **derived from `ExpiresAtUtc` vs `now`** — the domain never flips `Status` to `Expired`
> (`Enrollment` has only `Create`/`Extend`/`Refund`/`SoftDelete`; the `EnrollmentStatus.Expired` enum value is unused by
> the writer). Compute `Expired` in the projection, do **not** read `Status == Expired`. Use the handler's injected
> `TimeProvider` for `now`.

### C.2 `prerequisiteSatisfied` (FR-STU-CAT-002) — mirror `EnforcePrerequisiteGateAsync` exactly

- No `PrerequisiteSessionId` → **`true`** (vacuous).
- Prerequisite session has **no questions** (`!db.Questions.Any(q => q.SessionId == prereqId)`) → **`true`** (nothing to
  complete — same vacuous pass the enroll gate uses).
- Else → `true` **iff** the caller has a `UserAssignment` for the prerequisite with `Status == Completed`.

This is **the same predicate the server enforces at redeem** (`§B.4`) — the catalogue surfaces it so the card can show a
prerequisite badge and a **disabled Enroll CTA** ("Complete *{prerequisiteTitle}* first") **before** the student wastes a
code. The server remains authoritative (a forced redeem still 409s) — the flag is UX, not the security boundary.

## D. Audit (`FR-PLAT-AUD-002`)

- `GET /api/me/catalogue` — **pure read, not audited** (parity with the other `/api/me/*` reads; the signed thumbnail URL
  is a low-sensitivity catalogue image, unlike the audited private ID-image / paid-material reads).
- `POST /api/enrollments/redeem` — **audited** (`CodeRedeemed`, `ActorType=Student`, `Portal=student`) — **already
  implemented in Phase 4**; the frontend must treat redeem as a state change (don't double-fire it).

## E. Request-a-spot — **NOT built** (deferred; master plan §3.3 / §8.3)

The prototype's **`Request a spot modal`** (an offline-session code *request*) has **no backend** and is **out of scope**
for this engagement. The catalogue's **only** enroll path is **code redemption** (`§B`). The frontend does **not** build
the request-a-spot modal. Recorded here so it isn't silently dropped.

## F. Frozen vs. stream-owned

- **Frozen (this file):** the `GET /api/me/catalogue` path, `RequireStudent`, the four optional query params (`§A.1`),
  the `CatalogueSessionDto` field names/types + DESC ordering + flat-list shape (`§A.2`), published-only + tenant-auto
  scoping (`§0`), the `enrollmentState`/`enrolledExpiresAtUtc` derivation (`§C.1`) and `prerequisiteSatisfied` predicate
  (`§C.2`); the **redeem** path/body/`EnrollmentDto`/`400`+`409` statuses + the six 409 `detail` strings (`§B`); "reads
  not audited, redeem audited" (`§D`); "request-a-spot not built" (`§E`).
- **Backend owns:** the query's folder/name (`Features/Sessions/Queries/ListCatalogue/` or
  `Features/Enrollment/Queries/…` — implementer's call, keep the route + DTO frozen), the `CatalogueSessionDto` +
  `.ToCatalogueDto()` mapping location, the `MeCatalogueEndpoints : IEndpointGroup` wiring, the name-resolution joins
  (mirror `ListSessionsHandler`: `IgnoreQueryFilters` on grade/subject/spec **names** only), and the integration tests.
- **Frontend owns:** the cards grid + spec chip-bar + mascot empty state, the `SessionThumb`/`CodeInput` UI pieces, the
  enroll modal UX (segmented 16-char input + paste), the redeem call + error rendering, post-success refresh + nav, and
  the Jest specs.
- **Wiring owns:** proving the slice live on the Aspire stack — catalogue returns the tenant's published set with correct
  per-caller state + prereq flags (tenant-isolated), and the full **redeem → consumed code → enrollment + side-effects →
  catalogue card flips to `Enrolled`** loop, with every redeem error surfaced.
