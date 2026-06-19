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

---

## Reconciliation log — 2026-06-19 (wiring stream executed)

**Gates (step 1):** backend `dotnet test -c Release` → 113 unit + 48 integration green; frontend
`nx build admin-portal` green and `nx test admin-portal-feature-sessions` → 25 green. *(Note: the Nx project name
is `admin-portal-feature-sessions`, not `feature-sessions`.)*

**Stack + migration (step 2):** the Aspire AppHost was already running (Postgres + pgAdmin + MinIO containers + API +
Angular as local processes); the gated `AddSessionsAndQuestionBank` migration was already applied to the dev DB
(all six migrations present, tables created). No action needed.

**Frontend → API (step 3):** already wired — the Angular dev-server proxy (`proxy.conf.js`) reads Aspire's injected
`services__api__http__0`; `GET http://localhost:4200/api/sessions` proxies to the API (401 unauthenticated). No code change.

**Drift reconciled (step 4) — the backend matched the frozen contract faithfully; the *frontend* had diverged.**
The contract was **not** amended (it was correct). Frontend fixes (all in `libs/admin-portal/feature-sessions`):
- `SessionListDto` carried `thumbnailUrl/price/validityDays/gradeId/specializationId/createdAtUtc/updatedAtUtc` and
  lacked `subjectName` → replaced with the contract shape (`gradeName/subjectName/specializationName` + counts; accent
  tile, never a thumbnail).
- `SessionVideoDto.durationSeconds` → `lengthMinutes` (admin-entered; contract §1).
- `SessionMaterialDto.contentType` → `kind` (server-computed "PDF"/"PNG"/"CSV").
- `QuizSettingDto.timeLimitSeconds` → `timeLimitMinutes` (contract is minutes; removed the ×60 conversion in the
  quiz-settings + summary code).
- `SessionDetailDto` gained `subjectId`/`subjectName`; `description` made nullable.
- `SessionService.addVideo` was missing the contract-required `lengthMinutes` multipart field (would 400).
- `SessionService.setVideoAccessCount` hit a non-existent `PUT …/videos/{id}/access-count` → replaced with the
  contract's multipart `updateVideo` (`PUT …/videos/{id}`, #13); the video dialog now edits title/length/access.
- List filter was a (server-ignored) *specialization* filter → switched to the contract's **subject** filter
  (`subjectId`; §1 query + §4 design parity).
Specs updated to match; build + 25 tests green afterward. Verified field-for-field against **live API JSON**
(enum casing `"Draft"`, `ProblemDetails`, `PagedResult` envelope, embedded signed image URLs, material download).

**End-to-end smoke (step 5):** passed against the running stack (35 assertions) — create → thumbnail → video
(reached `Ready` via the stub) → material (signed URL downloaded) → question (LaTeX + image + 3 options + 1 variation)
→ 4 more eligible questions → quiz settings → prerequisite (seeded "Session 1") → **publish blocked at 409 while the
quiz over-counted the eligible bank (10 > 5), then 200 after fixing to 5** → list + tabbed detail verified.

**Audit + security (step 6):** the smoke wrote **39 `AuditEntry` rows**, every one with `PrevHash` + `Hash`
(chain intact); both the field-diff interceptor and the explicit rich-action handlers fired; the rolled-back 409
publish wrote no `SessionPublished` row. Default-deny verified live (Assistant → 403 on publish/delete, anonymous →
401) and by `SessionPermissionTests`. Private media is persisted as R2 object keys only
(`sessions/{tenant}/…`, `questions/{tenant}/images/…`); zero durable URLs in the DB — URLs are short-lived signed reads.

**Docs (step 6):** OpenAPI/Scalar is auto-generated and already current (all 20 session/question paths present); the
Phase 3 exit criterion in `IMPLEMENTATION-PLAN-admin-portal.md` is ticked.
