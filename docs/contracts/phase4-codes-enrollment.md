# FROZEN API CONTRACT — Phase 4 (Enrollment, codes & payments seam)

> **Status: FROZEN.** Single shared source of truth between the backend stream
> (`IMPLEMENTATION-PLAN-phase4-backend.md`) and the frontend stream
> (`IMPLEMENTATION-PLAN-phase4-frontend.md`). **Neither stream edits this file while building.**
> Drift is reconciled in the wiring stream under change control. Created 2026-06-20.
>
> **Design-derived:** every DTO and endpoint here is shaped to the exact prototype screens in
> `.claude/Salah Bahzad Teacher Portal/Admin Portal.dc.html` (`scrCodes`, `scrCodesGenerate`,
> `scrSessionDetail` › Unlock/Enrolled, `scrStudentDetail` › Enrollments). See §4 (Design parity map).
>
> Requirement IDs: `FR-PLAT-COD-001..006`, `FR-PLAT-ENR-001..008`, `FR-PLAT-PAY-001..002`,
> `FR-ADM-COD-001..005`, `FR-ADM-SES-009/010`, `FR-ADM-STU-008`, `FR-PLAT-AUD-002`.

---

## 0. Global conventions

Identical to Phase 3 (`docs/contracts/phase3-sessions.md` §0) — reproduced here so this file stands alone.

| Concern | Rule |
|---|---|
| Base path | `/api` · Bearer platform JWT on every endpoint · default-deny via `RequirePermission` |
| JSON casing | camelCase over the wire; C# DTOs are PascalCase records |
| Enums | **string names** (`JsonStringEnumConverter`) — frontend models them as string unions |
| Dates | ISO 8601 `DateTimeOffset` UTC, suffix `...AtUtc` |
| Money | `decimal` EGP, rendered `EGP {value}` by the UI |
| Errors | RFC 7807 `ProblemDetails` (400 validation · 403 permission · 404 not found · 409 conflict/illegal-state) |
| Pagination | `PagedResult<T>` (`items/total/page/pageSize/totalPages`), defaults `page=1`, `pageSize=20` |
| File exports | `text/csv; charset=utf-8` stream + `Content-Disposition: attachment; filename="…"`. Excel opens CSV natively (FR-PLAT-COD-002 says "Excel/CSV"); a true `.xlsx` via ClosedXML is a drop-in upgrade later. |
| Permissions | `CodesRead/Generate/Disable/Delete`, `EnrollmentsRead/Unlock/Refund` — **already exist** in `Domain/Enums/Permission.cs` and are **already bundled** in `Application/Features/Auth/PermissionCatalog.cs` (see §5 for the one Assistant-refund reconciliation). |

### Enums
```
CodeStatus       = "Active" | "Inactive" | "Used"      // UI label: Inactive → "Disabled". Soft-deleted codes are hidden, not a status.
EnrollmentStatus = "Active" | "Expired" | "Refunded"
EnrollmentMethod = "Code" | "Unlock"
PaymentMethod    = "CodeRedemption" | "Unlock"          // future: "Gateway" (FR-PLAT-PAY-001)
PaymentStatus    = "Completed" | "Refunded"
```

---

## 1. DTOs (shaped to the screens)

```jsonc
// CodeListDto — scrCodes table row (cols: Serial, Value, Status, Batch, Session, Redeemed by, Created)
{
  "id": "guid", "serial": "string", "value": 0, "status": "Active",
  "batchId": "guid", "batchLabel": "string",
  "sessionId": "guid", "sessionTitle": "string",
  "redeemedByStudentId": "guid|null", "redeemedByStudentName": "string|null", "redeemedAtUtc": "…|null",
  "createdByName": "string|null", "createdAtUtc": "…"
}

// CodeBatchDto — scrCodesGenerate "Batch ready" panel (no inline code list — the Excel comes from #4)
{
  "batchId": "guid", "label": "string",
  "sessionId": "guid", "sessionTitle": "string",
  "value": 0, "quantity": 0, "createdAtUtc": "…"
}

// EnrollmentDto — result of unlock (#9) / refund (#10) / redeem (#12)
{
  "id": "guid",
  "studentId": "guid", "studentName": "string",
  "sessionId": "guid", "sessionTitle": "string",
  "status": "Active", "method": "Unlock", "amount": 0,
  "codeId": "guid|null", "codeSerial": "string|null",
  "enrolledAtUtc": "…", "expiresAtUtc": "…|null"            // null when session validityDays == 0 (no expiry)
}

// EnrollmentListDto — scrSessionDetail "Enrolled students" tab row (Student, Progress, Quiz best, actions)
{
  "enrollmentId": "guid", "studentId": "guid", "studentName": "string", "studentInitials": "string",
  "method": "Code", "status": "Active", "enrolledAtUtc": "…",
  "quizBestPercent": 0, "videosWatched": 0, "videosTotal": 0   // progress fields are Phase 5 placeholders (always 0 now)
}

// StudentEnrollmentDto — scrStudentDetail "Enrollments & transactions" tab row (Session, Method, Amount, When)
{
  "enrollmentId": "guid", "sessionId": "guid", "sessionTitle": "string",
  "method": "Code", "status": "Active", "amount": 0, "enrolledAtUtc": "…", "codeSerial": "string|null"
}
```

**The unlock modal’s student picker reuses Phase 2** — `GET /api/students?status=Active&search={q}` (`StudentListDto`
carries `name`+`phone`). `feature-sessions` calls it from its own service (no `feature-students` TS import — module
boundary), mapping rows to `{ value: id, label: name, description: phone }` exactly like `scrSessionDetail.unlockBody()`.

**Invariants (400/409 on violation):**
- generate (#2): `quantity` 1–1000 · `value` ≥ 0 · `sessionId` must exist. `value` **defaults to the session’s current
  price** (the modal pre-fills it); a mismatch is allowed at mint but blocks redemption (see §5).
- redeem (#12): code exists + `Active` + not soft-deleted → else **409**; `code.value == session.price` → else **409**
  (price mismatch); student holds no other `Active` enrollment for the session (`FR-PLAT-ENR-006`) → else **409**.
- unlock (#9): `studentId` is an `Active` student; no existing `Active` enrollment for the session → else **409**.
- refund (#10): enrollment is `Active` → else **409**.
- disable/enable/delete (#5/#6/#7): code status **must not be `Used`** → else **409** (the register hides these actions
  for used codes; the server still enforces it).

---

## 2. Endpoints — Codes (`/api/codes`)

| # | Method & path | Perm | Request | Response |
|---|---|---|---|---|
| 1 | `GET /api/codes` | CodesRead | query `search? status? batchId? sessionId? page pageSize` | `PagedResult<CodeListDto>` |
| 2 | `POST /api/codes/batches` | CodesGenerate | `{ sessionId, value, quantity }` | `201 CodeBatchDto` |
| 3 | `GET /api/codes/export` | CodesRead | query `search? status? batchId? sessionId?` (same filters as #1) | `text/csv` file |
| 4 | `GET /api/codes/batches/{batchId}/export` | CodesRead | — | `text/csv` file (re-export a batch, `FR-ADM-COD-005`) |
| 5 | `POST /api/codes/{id}/disable` | CodesDisable | — | `CodeListDto` / 409 if `Used` |
| 6 | `POST /api/codes/{id}/enable` | CodesDisable | — | `CodeListDto` / 409 if `Used` |
| 7 | `DELETE /api/codes/{id}` | CodesDelete | — | `204` (soft-delete) / 409 if `Used` |

> **Bulk actions are client-side fan-out.** The register’s "Disable" bulk button loops #5 over the selected serials
> (matches `scrCodes` `sel.forEach(setCodeStatus)`), and "Export selection" builds a CSV from the already-loaded rows
> in the browser. #3 is the **server** export of the whole filtered set; #4 re-exports one batch (the Generate
> screen’s "Download Excel" button calls #4 with the just-minted `batchId`).
>
> CSV columns (#3/#4): `Serial, Value, Status, Batch, Session, Created by, Created, Redeemed by, Redeemed at`
> (the `downloadCSV` column set in `scrCodes`).

## 3. Endpoints — Enrollment

| # | Method & path | Perm | Request | Response |
|---|---|---|---|---|
| 8 | `GET /api/sessions/{id}/enrollments` | EnrollmentsRead | query `search? page pageSize` | `PagedResult<EnrollmentListDto>` (scrSessionDetail › Enrolled) |
| 9 | `POST /api/sessions/{id}/unlock` | EnrollmentsUnlock | `{ studentId }` | `201 EnrollmentDto` / 409 already-active / 404 |
| 10 | `POST /api/enrollments/{id}/refund` | EnrollmentsRefund | `{ reason? }` | `EnrollmentDto` (status `Refunded`) / 409 not-active |
| 11 | `GET /api/students/{id}/enrollments` | EnrollmentsRead | query `page pageSize` | `PagedResult<StudentEnrollmentDto>` (scrStudentDetail › Enrollments) |
| 12 | `POST /api/enrollments/redeem` | **student (self)** | `{ serial }` | `201 EnrollmentDto` / 400 / 409 — **backend-only this phase; no admin UI** |

> **#12 redeem is the student-portal path** (`FR-PLAT-ENR-001`). The admin portal never calls it, but the engine is in
> Phase 4 backend scope ("enrollment by code, value==price, one-shot"). It is gated by an **authenticated Student-role
> principal** (a minimal new policy — the one auth touch this phase) and proven by integration tests that issue a
> student JWT. It shares the enrollment side-effect path with #9 (see §5).
>
> **Refund returns the code** (`FR-PLAT-ENR-008`): when the enrollment’s `method == Code`, #10 flips that code
> `Used → Active` so it can be redeemed again, and writes a reversing `PaymentTransaction` (`PaymentStatus.Refunded`).

---

## 4. Design parity map (prototype screen → contract)

| Prototype screen | Uses |
|---|---|
| `scrCodes` (register) | #1 · `CodeListDto`. Cols Serial(mono)/Value(`EGP n`)/Status(pill active·used·**disabled**=Inactive)/Batch/Session/Redeemed-by(+date)/Created. Filters: search(serial\|student)+status+session. Teacher-only: select-col (disabled for `Used`) → bulk **Disable** (loop #5) / **Export selection** (client CSV); per-row **Enable↔Disable** (#5/#6) + **Delete** (#7, confirm). Header **Export** (#3) + **Generate codes** (→ generate). Assistant → read-only Alert. |
| `scrCodesGenerate` (Teacher-only) | Left *Batch settings* card: Session combo + Value(EGP) + Quantity → **Generate** (#2). Right *Preview* → on success *Batch ready* (check, summary) → **Download Excel** (#4). Role-gated otherwise. |
| `scrSessionDetail` › **Unlock for student** button | #9. Modal `unlockBody()`: SBCombobox over `GET /api/students?status=Active&search=` ("Search by name or phone…"), "Grant access bypassing code & price" → confirm "Unlock session". |
| `scrSessionDetail` › **Enrolled** tab | #8 · `EnrollmentListDto` (Student cell, Progress bar*, Quiz best*, actions **Review**[Phase 5]/**Refund** → #10 "Refund & revoke", danger, audited). *Progress/Quiz-best are 0 placeholders until Phase 5.* |
| `scrStudentDetail` › **Enrollments & transactions** tab | #11 · `StudentEnrollmentDto` (Session / Method pill `Code`·`Unlock` / Amount `EGP n`\|`Free` / When). |
| `scrDashboard` codes & revenue | "Codes used / active" + "Revenue (by code)" stats + "Generate codes" quick action + code/unlock/refund items in recent-activity — read aggregates of #1 / the audit feed. **The dashboard screen itself is Phase 5;** only the data seam lands now. |
| (future student portal) redeem | #12 — built + tested in the backend, no Phase 4 screen. |

## 5. Notes that bind both sides

- **Codes are session-bound + value-matched.** Reconciling `FR-PLAT-COD-003` ("redeemable only for a session whose
  price equals the code’s value") with the prototype (the register has a **Session** column; Generate picks a session):
  a code carries **both** `sessionId` and `value`. Generation defaults `value` to that session’s current price; #12
  redeems a code only for **its** session and re-checks `value == session.price` at redemption (so a later price change
  safely blocks stale codes).
- **`CodeStatus` has three values** (`Active`/`Inactive`/`Used`). The prototype’s **"Disabled"** label = `Inactive`.
  **Delete is soft** (`ISoftDeletable`) — deleted codes drop out of the register via the global query filter; "Deleted"
  is not a queryable status.
- **Assistant refund reconciliation.** The README role matrix **and** the prototype show **Refund** to Assistants, but
  the forward-declared `PermissionCatalog.AssistantPermissions` lists only `EnrollmentsUnlock` (not `EnrollmentsRefund`).
  Phase 4 **adds `EnrollmentsRefund` to the Assistant bundle** to match the matrix + design (it is not in the
  `FR-PLAT-ROLE-003` Teacher-only list). If the owner wants refund to be Teacher-only, flip this one line — the
  contract’s perm column (`EnrollmentsRefund`) stays the same either way.
- **Enrollment side-effects are partly a stubbed seam this phase** (mirrors how Phase 3 stubbed `IVideoProcessingQueue`):
  on enroll (#9/#12) the backend **really** provisions per-video access counters from the session’s videos
  (`FR-PLAT-ENR-005`), writes a `PaymentTransaction` (`FR-PLAT-PAY-001/002`), and creates the `Attendance` shell
  (`FR-PLAT-ATT-001`). It raises an `EnrollmentCreated` domain event whose handler will **generate the assignment +
  prerequisite-quiz snapshots** (`FR-PLAT-ASG-001`, `FR-PLAT-QZ-001`) — **stubbed in Phase 4, real in Phase 5** when the
  assignment/quiz engines that consume them land. Likewise the **prerequisite-assignment-completion gate**
  (`FR-PLAT-ENR-007`) is enforced in Phase 5; Phase 4 redemption does not yet block on it (documented deferral).
- **Re-enroll / extend is idempotent** (`FR-PLAT-ENR-004`): redeeming/unlocking again for a session the student already
  has resets the per-video counters and pushes `expiresAtUtc` forward on the **existing** `Active` enrollment rather
  than creating a second row (so #9/#12’s "already-active" 409 applies only when the caller is not explicitly extending;
  the extend path updates in place — see the backend plan A-steps).
- **`enrolledCount` becomes real.** Phase 3 shipped `SessionListDto.enrolledCount` / `SessionDetailDto.enrolledCount`
  as `0` placeholders; Phase 4 fills them with the count of `Active` enrollments (a small change to the Phase 3 session
  queries — owned by the backend stream, the contract shape is unchanged).
- **Serials** are unique **per tenant**, opaque, and human-keyable (format `SB-XXXXX-XXXXX`, Crockford base32, no
  ambiguous chars). **Batch `label`** is a server-generated readable string (e.g. `NEW-20260620-01`). Neither leaks
  across tenants (`FR-ADM-COD-005`, tenant global query filter).
- **No file uploads in Phase 4** — the only binary is the CSV export the server streams out.
- **Audit is mandatory for all eight actions** (`FR-PLAT-AUD-002`): generate, **export**, disable, enable, delete,
  redeem, unlock, refund. Backend-only concern, but note for the frontend stream: triggering **export** (#3/#4) and
  **redeem** (#12) are audited server-side, so they are not "free" reads — don’t batch/duplicate the calls. (Export is a
  `GET`, so the backend audits it explicitly — see the backend plan’s audit step.)
