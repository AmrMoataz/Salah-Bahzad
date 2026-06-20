# Phase 5A — WIRING stream (prove Audit-log browser + Dashboard end-to-end)

> Run this **after** the backend and frontend streams have each gone green on their own gates. Created 2026-06-20.
>
> **Read first:** the **frozen contract** `docs/contracts/phase5a-audit-dashboard.md`, plus the two stream docs
> (`…-phase5a-backend.md`, `…-phase5a-frontend.md`). **Template to mirror:** `IMPLEMENTATION-PLAN-phase4-wiring.md`
> (same shape: bring the slice up on the running Aspire stack, drive a scripted smoke, log the result).
>
> **File ownership:** this stream may touch **both** sides — but only to fix **drift from the frozen contract**
> (a field name/shape mismatch, a wrong status code, a missing filter). Any change must move a side **back onto**
> the contract, never extend it. Record every fix in this doc.

## Goal
Prove, against the live stack, that the audit browser + dashboard work end-to-end with **zero contract drift**:
real hash-chained entries are listed/filtered/drilled-in, Assistant-vs-Teacher sensitive scoping holds, **tenant
isolation holds** (the load-bearing rule for the unfiltered `AuditEntry` table), and dashboard KPIs match seeded
reality. Default-deny verified live.

## Stack & access (from the Phase-4 wiring run — reuse verbatim)
- Bring the stack up via the **Aspire AppHost** (F5 / `dotnet run` on `SalahBahazad.AppHost`): Postgres + pgAdmin,
  API, Angular. The API port is **reassigned by Aspire each run** — drive the API through the Angular dev proxy on
  **`:4200`** (or read the resolved API URL from the AppHost dashboard) rather than hard-coding a port.
- **Direct-JWT smoke technique:** mint platform JWTs directly (the reusable technique from Phase 3/4 wiring) for
  three principals — a **Teacher**, an **Assistant**, and a **second-tenant** staff token — to exercise scoping
  and isolation without the full Firebase login each call. (Student tokens are only needed to *generate* an audit
  row, see below.)

## Pre-seed: make the audit table realistic
The audit trail is only meaningful if real actions exist. Drive a few mutations first (reuse Phase-2/3/4 flows)
so the table has **Staff**, **Student**, and **System** actors and at least one **sensitive** row:
- Approve a pending student (Staff) and **view a student's ID image** (Staff) → produces a `StudentIdImageViewed`
  **sensitive** row.
- Generate a code batch + export CSV (Staff) → `CodeBatchGenerated`, `CodesExported`.
- Redeem a code with a **Student** JWT (#12) → `CodeRedeemed` (Student actor) + the enrollment's side-effect
  rows (System actor where applicable).

## Smoke checklist (script it; assert, don't eyeball) — design-anchored
1. **Feed + filters (#1):** `GET /api/audit` (Teacher) returns the seeded rows newest-first with resolved
   `actorName`, `targetLabel`, and `category`. Filters narrow: `actorId`, `actorType=Student` (the redeem),
   `category=code`, `period=7d/30d`, and `studentId`/`sessionId` (the entity-tab params); paging.
2. **Sensitive scoping (`FR-ADM-AUD-003`):** the **Assistant** feed does **NOT** contain `StudentIdImageViewed`;
   the **Teacher** feed **does**. (In the UI, the Assistant build shows the "Scoped view" alert.)
3. **Drill-in = navigate (no detail endpoint):** every row carries `targetType`+`targetId` so the UI "View" links
   to the right entity (student/session/codes/staff). Assert there is **no** `/api/audit/{id}` and **no**
   `AuditViewed` row is ever written (the prototype has no before/after screen).
4. **Tenant isolation (`NFR-SEC-010`) — the critical assertion:** the **second-tenant** token sees **zero** of
   tenant A's audit rows from `GET /api/audit`. (Proof the explicit `Where(TenantId==…)` is present, since no
   global filter protects `AuditEntry`.)
5. **Dashboard (#2):** `GET /api/dashboard` matches seeded counts — `pendingApprovals`, `activeStudents`,
   `codesUsed`, `codesActive`, `revenueFromCodes` (Σ used-code values); `enrollmentsByDay` buckets sum to
   `enrollmentsTotal` over the default 30-day window **and** a custom `period`; `recentActivity` ≤ 7,
   tenant-scoped, **excludes** sensitive rows.
6. **Default-deny:** anonymous → **401** on both groups; a token without `AuditRead`/`DashboardRead` → **403**.
7. **Frontend render:** load the **Activity log** and **Dashboard** in the running Angular app — feed rows render
   with category icons, the **category filter round-trips**, **"View" navigates** to the entity, the 4 KPI cards +
   the enrollments chart show the right numbers, and the **Assistant** build hides the "Generate codes" quick
   action and shows the scoped alert. Confirm **zero** shape mismatches with the contract (no client-side field
   renames/coercions papering over drift).

## Drift log
Record any contract mismatch found and the one-line fix that returned a side to the contract (mirror the Phase-4
wiring drift log). Target: **zero drift** like Phase 4.

## Exit criteria
All seven checks pass on the live stack; tenant isolation + sensitive scoping proven; dashboard KPIs reconcile to
seeded data; frontend renders with zero drift. Append the dated run log (assertions count + results) to this file,
then mark Phase 5A **Met** in `docs/IMPLEMENTATION-PLAN-admin-portal.md`.

---

## Kickoff prompt (paste into a fresh Claude session at the repo root, after both streams are green)

```
You are running the WIRING stream of Phase 5A (Audit-log browser + Dashboard) for Salah Bahzad. The backend and
frontend streams are already green on their own gates. Your job: prove the slice end-to-end on the running Aspire
stack with ZERO contract drift, and fix only drift (never extend the contract).

Read first:
1. docs/contracts/phase5a-audit-dashboard.md (the FROZEN contract)
2. docs/IMPLEMENTATION-PLAN-phase5a-wiring.md (your run book + smoke checklist)
3. docs/IMPLEMENTATION-PLAN-phase4-wiring.md (the technique to mirror: Aspire AppHost up, drive via :4200 since
   the API port is reassigned, direct-JWT smoke)

The slice is DESIGN-ANCHORED: the activity log is a feed of actor/action/target with a category icon; drill-in
NAVIGATES to the entity (there is NO /api/audit/{id} and NO AuditViewed write). The dashboard has 4 KPIs + an
enrollments-by-day chart + 7 recent-activity rows.

Steps: bring up the AppHost stack; mint Teacher / Assistant / second-tenant JWTs (direct-JWT technique) plus a
Student JWT to seed a redeem; pre-seed real actions (approve student, view ID image [sensitive → StudentIdImageViewed],
generate+export codes, redeem #12). Then run the 7-point smoke: feed+filters (actor/category/period), Assistant-vs-
Teacher sensitive scoping, drill-in = navigate (assert targetType/targetId present, no detail endpoint, no AuditViewed),
TENANT ISOLATION (second tenant sees zero of tenant A's audit — the must-pass check), dashboard KPI + enrollments-
series reconciliation, default-deny (401/403), and frontend render of the Activity log + Dashboard screens.

If you find drift, fix the offending side back ONTO the contract and log it. When done, append a dated run log
(assertion count + pass/fail) to docs/IMPLEMENTATION-PLAN-phase5a-wiring.md and report the tenant-isolation and
sensitive-scoping results explicitly.
```
