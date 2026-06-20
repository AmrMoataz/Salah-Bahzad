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

## Reconciliation log — (fill in when the wiring stream executes)

*(Mirror the Phase 3 wiring log: record the gate results, the stack/migration state, which side drifted and how it was
reconciled, the end-to-end smoke assertions, the audit/default-deny/tenant-isolation results, and the docs close-out.)*
