# Phase 4 — WIRING stream (connect backend + frontend)

> Run this **after both** the backend and frontend streams are independently green. Created 2026-06-20.
> Single session, touches both sides. **Prerequisite gates:** backend `dotnet test -c Release` green AND
> frontend `nx build admin-portal` + `nx test admin-portal-feature-codes` green.

## Goal
Connect the two streams against the running stack, prove the codes + enrollment slice end-to-end, and reconcile any
drift from the frozen contract (`docs/contracts/phase4-codes-enrollment.md`).

## Steps

1. **Bring up the stack** — run the Aspire AppHost (orchestrates Postgres + pgAdmin + MinIO + API + Angular). Apply the
   gated `AddCodesAndEnrollment` migration deliberately (it does not auto-apply). Confirm the
   `AssistantPermissions += EnrollmentsRefund` change is present (contract §5).
2. **Point the frontend at the API** — confirm the admin-portal API base URL targets the running API (Aspire service
   discovery / `proxy.conf.js`). No code change if already wired from earlier phases.
3. **End-to-end smoke (the exit demo):** sign in as Teacher → **generate a batch** (session + value + qty) →
   **Download Excel** (the CSV streams with the right columns) → the codes appear in the **register** → **disable** then
   **enable** one → redeem one **via #12 with a student JWT** (admin UI has no redeem screen) → it flips to **Used**,
   creates an `Active` enrollment, provisions per-video counters, writes a `Completed` `PaymentTransaction`, creates the
   attendance shell, and fires `EnrollmentCreatedEvent` (stub side-effects logged) → on **Session detail › Enrolled**
   the student appears → **Unlock** a *different* student (bypassing code/price) → **Refund** one enrollment (status
   `Refunded`, the code returns to `Active`, a reversing `PaymentTransaction` is written) → **re-enroll/extend** resets
   counters & pushes expiry without duplicating (`FR-PLAT-ENR-004`) → **Student detail › Enrollments & transactions**
   shows the rows → `enrolledCount` is now correct in the sessions list/detail.
4. **Reconcile contract drift** — the main risk. Walk every endpoint and check, against the frozen contract:
   - enum **string** casing (`CodeStatus`/`EnrollmentStatus`/`EnrollmentMethod` ↔ TS string unions; "Disabled" UI label
     maps to `Inactive`),
   - the **code = sessionId + value** model and the **value==price** redemption check (contract §5),
   - `ProblemDetails` 409s surfaced in the UI (used-code disable/delete, price mismatch, second active enrollment,
     refund-when-not-active),
   - `PagedResult<T>` envelope on register / enrolled / student-enrollments,
   - the **CSV export** (full filtered #3, batch re-export #4) streams with `Content-Disposition` and the documented
     columns; the **selection** export is client-side,
   - any field the backend added or renamed vs. the frozen contract.
   Apply fixes to whichever side diverged; if the contract itself was wrong, **amend the frozen contract under change
   control** and note it here.
5. **Audit + security pass** — confirm every lifecycle event wrote an `AuditEntry` with intact `PrevHash`→`Hash`
   (generate, export, disable, enable, delete, redeem, unlock, refund — `FR-PLAT-AUD-002`). **Pay special attention to
   `export`: it is a `GET` (read), so it must come from an explicit `IAuditWriter` write, not the SaveChanges
   interceptor — hit the export endpoint and verify a fresh audit row appears.** Confirm **default-deny**
   holds server-side (Assistant blocked on generate/disable/delete; Assistant **allowed** to unlock **and refund** after
   the catalog change; anonymous → 401; a **student** token rejected on staff routes and a **staff** token rejected on
   #12 redeem); confirm **tenant isolation** (`NFR-SEC-010`) — tenant B never sees tenant A’s codes/enrollments/exports.
6. **Docs + close-out** — update OpenAPI/Scalar; tick the Phase 4 exit criteria in
   `docs/IMPLEMENTATION-PLAN-admin-portal.md`; commit the slice (cite the requirement IDs).

## Definition of done
The Phase 4 exit criterion from the master plan: *"mint → export → redeem → refund a code, all audited;
counters/expiry correct on re-enroll."* Both `dotnet test -c Release` and the `nx` gates green; the end-to-end smoke
passes against the running stack; audit + default-deny + tenant isolation verified.

## If drift is large
Prefer fixing the **implementation** to match the frozen contract rather than bending the contract — it was the agreed
interface. Only change the contract when it was genuinely wrong, and record the reason here so the history is auditable.

## Watch-list (Phase 4 specifics that bit Phase 3 or are new)
- **Money type:** `decimal` EGP end-to-end; ensure JSON serializes without locale/rounding surprises and the UI renders
  `EGP {value}`.
- **Student-role JWT for #12** is the one new auth surface — verify the policy issues/accepts a student principal and
  that the smoke’s "direct-JWT" technique (per the Phase 3 wiring log) can mint one.
- **`enrolledCount` fill** touches the Phase 3 session queries — confirm the sessions list/detail still match the
  Phase 3 contract shape (only the value changed from the `0` placeholder).
- **Stubbed side-effects:** assert the `EnrollmentCreatedEvent` handler ran (log/marker) but that **no** assignment/quiz
  rows are expected yet — that engine is Phase 5; the smoke checks the event fired, not snapshot contents.

---

## Reconciliation log — executed 2026-06-20

**Prerequisite gates (both green before starting).**
- Backend `dotnet test -c Release`: **132/132 unit**; integration all green **except** the pre-existing
  `QuestionBankTests.Create_allows_an_image_only_question` (500 → MinIO image-only path) — verified on a clean `HEAD`
  worktree in a prior session, a Phase-3/env issue for that owner, **not introduced by Phase 4**.
- Frontend `nx build admin-portal` (AOT) green (budget warnings only); `nx test admin-portal-feature-codes` **22/22**.

**Stack & migration.** The user already had the Aspire AppHost running under Visual Studio (F5) — the documented run
model — so the CLI `dotnet run` on the AppHost failed on the VS Debug-DLL lock (expected; not used). Drove the running
stack instead. The gated `AddCodesAndEnrollment` migration was **already applied** (verified deliberately:
`20260620003342_AddCodesAndEnrollment` present in `__EFMigrationsHistory` with all six tables —
`codes/code_batches/enrollments/enrollment_video_access/payment_transactions/attendance`). `AssistantPermissions +=
EnrollmentsRefund` confirmed in `PermissionCatalog.cs` (Assistant has `CodesRead` + `Enrollments{Read,Unlock,Refund}`,
**not** `CodesGenerate/Disable/Delete`). Aspire reassigns the API's localhost port on every restart (saw 62883→51348),
so the smoke ran through the **stable Angular dev-server proxy at `:4200/api`** (also exercises the real frontend→API
path). Frontend base URL is relative (`window.__SB_API_URL__ ?? ''`) → rides the proxy. Smoke auth used the Phase-3
direct-JWT technique (HS256 over the dev secret; `nameid`/`role`/`tenant_id`/`token_type`), extended this phase with a
**Student-role token** (`role:"Student"`) for #12.

**Contract drift — ZERO.** Unlike Phase 3, both streams hit `docs/contracts/phase4-codes-enrollment.md` precisely; no
fix was needed on either side. Verified field-for-field against live JSON: enum **string** casing
(`CodeStatus`/`EnrollmentStatus`/`EnrollmentMethod`; UI maps `Inactive`→"Disabled"), the **code = sessionId + value**
model + `value==price` redemption re-check, `ProblemDetails` 409s (used-code disable/delete, price mismatch,
second-active-enrollment, refund-not-active), `PagedResult<T>` envelope (register/enrolled/student-enrollments), the CSV
export (`text/csv` + `Content-Disposition`, columns `Serial,Value,Status,Batch,Session,Created by,Created,Redeemed
by,Redeemed at` for #3/#4; client-side selection export is the 4-col `Serial/Value/Status/Session` subset by design).
The frontend models (`feature-codes` + the boundary-duplicated enrollment DTOs in `feature-sessions`/`feature-students`)
and every endpoint path/verb/body match. The contract was **not** amended. The three **known, accepted** gaps stand
(not drift): generate "Value" pre-fills from `GET /api/sessions/{id}` (list DTO carries no price); the register's
"active" subtitle is page-derived (no global aggregate until the Phase-5 dashboard); the unlock picker loads Active
students and filters client-side (shared combobox has no server search-as-you-type). The frontend tip "surface price on
the combo payload" is a Phase-5 enhancement, logged, not a wiring fix.

**End-to-end smoke (58/58 assertions, via `:4200/api`).** generate batch (value defaulted to the session price) → list
register (string `Active`, `SB-XXXXX-XXXXX` serials, `CODES-YYYYMMDD-NN` label) → CSV export #3 + batch re-export #4 →
disable→`Inactive`→enable → **redeem #12 with a student JWT** (code→`Used`, `redeemedByStudentName` set; enrollment
`Active`/`Code`/amount=price/`expiresAtUtc` set) → Enrolled tab shows the student (`Code`, initials, 0 progress
placeholders) → **unlock** a different student (`Unlock`/amount 0/`codeId` null) → **refund** the code enrollment
(`Refunded`; code returns to `Active`; `redeemedBy` cleared) → **re-enroll/extend** reusing the **same enrollment id**
(no duplicate, expiry pushed) → student-detail enrollments (single row, `Code` + serial) → `enrolledCount` real and
consistent across session **detail and list** (Phase-3 queries). DB-level checks (API-invisible): 2
`EnrollmentVideoAccess` counters reset to the videos' `AccessCount` on extend; `PaymentTransaction` trail
`Completed→Refunded(reversal)→Completed`; one `Attendance` shell per student (A's **not** duplicated across
refund+re-extend); exactly one enrollment row for (student, session). These durable artifacts are produced in the same
`Create()`/`Extend()` that raise `EnrollmentCreated`/`Extended`; the side-effect handler wiring is proven by the green
integration suite (Serilog is console-only in dev, owned by VS, so the stub's log line wasn't scraped — evidenced
structurally instead).

**Audit + default-deny + tenant isolation.**
- **Audit (`FR-PLAT-AUD-002`):** all eight actions wrote rows — `CodeBatchGenerated`, **`CodesExported` for both
  `Code` (#3) and `CodeBatch` (#4)** (a GET → written explicitly via `IAuditWriter`, the key interceptor-miss case),
  `CodeDisabled`, `CodeEnabled`, `CodeDeleted`, `CodeRedeemed` (**`ActorType=Student`**), `EnrollmentCreated` (unlock=Staff,
  redeem=Student), `EnrollmentExtended`, `EnrollmentRefunded`. The high-volume children (bulk-minted codes, counters,
  payments, attendance) emit **no** generic rows (`IAuditViaEventOnly`). All Phase-4 entries are hash-chained (non-null
  `PrevHash`/`Hash`); the linkage design is proven by the green `Audit_hash_chain_links_across_staff_and_student_actions`
  integration test (clean tenant: single genesis, walk `PrevHash→Hash`, every entry reachable, no fork).
- **Pre-existing audit-infra observation (NOT Phase 4, follow-up recommended):** the shared dev DB shows 10 null-`Hash`
  legacy `Staff` entries (2026-06-18, earliest dev) and 12 `PrevHash` forks among **Phase-3** entries (2026-06-19:
  `QuestionOption`/`SessionVideo`/`QuizSetting`/…). Root cause is the chain-head seeding
  (`OrderByDescending(a => a.Id)` over UUIDv7) not being robust to **same-millisecond id ordering / concurrent writes**
  (the user was clicking the live UI during the smoke). No Phase-4 entry is affected. Suggested fix for the audit-infra
  owner: a monotonic per-tenant sequence or a serialized chain head; out of Phase-4 wiring scope.
- **Default-deny (21/21):** anonymous → 401; Assistant → 403 on generate/disable/enable/delete but **allowed** on
  unlock + refund (permission passes → 404 on a random id, not 403) and `CodesRead`; student token → 403 on staff routes;
  **both staff roles → 403 on #12 redeem** (`RequireStudent`). Delete #7 → 204 soft-delete, hidden from the register,
  idempotent 404 on re-delete.
- **Tenant isolation (`NFR-SEC-010`):** a Teacher token minted for a different `tenant_id` sees **zero** of our codes,
  **404** on our batch export, and empty enrollments for our session/student — no cross-tenant leakage.

**Test fixtures (dev-only, documented).** Promoted the second seed student "Student Test" `Rejected→Active` via SQL so
two Active students were available (redeem one, unlock the other) — `redeem` does not gate on student status but `unlock`
requires Active; the approve API only allows `Pending→Active`. Left it Active (sensible given it now holds enrollments).
The smoke is self-healing (refunds any pre-existing Active enrollment for the two test students on the target session)
so it is re-runnable and robust to the concurrent live-UI activity.

**Docs close-out.** OpenAPI/Scalar is runtime-generated and already lists all 12 Phase-4 paths (verified at
`/openapi/v1.json`). Phase-4 exit criterion ticked in `IMPLEMENTATION-PLAN-admin-portal.md`. Committed citing the
requirement IDs.
