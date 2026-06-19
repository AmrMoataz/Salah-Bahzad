# Phase 3 — WIRING stream (connect backend + frontend)

> Run this **after both** the backend and frontend streams are independently green. Created 2026-06-19.
> Single session, touches both sides. **Prerequisite gates:** backend `dotnet test -c Release` green AND
> frontend `nx build admin-portal` + `nx test feature-sessions` green.

## Goal
Connect the two streams against the running stack, prove the slice end-to-end, and reconcile any drift from the
frozen contract (`docs/contracts/phase3-sessions.md`).

## Steps

1. **Bring up the stack** — run the Aspire AppHost (orchestrates Postgres + pgAdmin + MinIO + API + Angular).
   Apply the gated `AddSessionsAndQuestionBank` migration deliberately (it does not auto-apply).
2. **Point the frontend at the API** — confirm the admin-portal API base URL targets the running API (Aspire service
   discovery / `environment`). No code change if already wired from earlier phases.
3. **End-to-end smoke (the exit demo):** sign in → create a session → upload thumbnail (auto-renders) → add a video
   (access count set; processing status reaches `Ready` via the stub) → add a material (download via signed URL) →
   author a question with LaTeX + image + options + a variation → set quiz settings → set a prerequisite → publish →
   verify it appears in the list with correct stats and in the tabbed detail.
4. **Reconcile contract drift** — the main risk. Walk every endpoint and check, against `docs/contracts/phase3-sessions.md`:
   - enum **string** casing (`JsonStringEnumConverter` ↔ TS string unions),
   - multipart **field names** (`file`, `title`, `accessCount`) and content-type/size limits,
   - `ProblemDetails` error shape surfaced correctly in the UI (400/403/404/409),
   - `PagedResult<T>` envelope (`items/total/page/pageSize/totalPages`),
   - **embedded signed URLs** render without a click (thumbnails, question/variation images) and the
     **material** on-demand signed URL downloads,
   - any field the backend added or renamed vs. the frozen contract.
   Apply fixes to whichever side diverged; if the contract itself was wrong, **amend the frozen contract under change
   control** and note it here.
5. **Audit + security pass** — confirm every state-changing action wrote an `AuditEntry`; confirm default-deny holds
   (an Assistant without `SessionsCreate` is blocked server-side, not just hidden in the UI); confirm private media is
   never a durable public URL.
6. **Docs + close-out** — update OpenAPI/Scalar; tick the Phase 3 exit criteria in
   `docs/IMPLEMENTATION-PLAN-admin-portal.md`; commit the slice (cite the requirement IDs).

## Definition of done
The Phase 3 exit criterion from the master plan: *"author a full session with videos, materials, a question bank,
and a gating quiz config."* Both `dotnet test -c Release` and `nx` gates green; the end-to-end smoke passes against the
running stack; audit + default-deny verified.

## If drift is large
If the two sides diverged significantly, prefer fixing the **implementation** to match the frozen contract rather than
bending the contract — the contract was the agreed interface. Only change the contract when it was genuinely wrong, and
record the reason here so the history is auditable.
