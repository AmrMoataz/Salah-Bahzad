# Phase 3 — FRONTEND stream (`feature-sessions`)

> Run this in its **own** Claude session, in parallel with the backend stream. Created 2026-06-19.
>
> **Read first:** `frontend/CLAUDE.md` and the **design source of truth** — the
> `.claude/Salah Bahzad Teacher Portal/` prototype + `apps/admin-portal/src/styles/_design-tokens.scss`
> (NOT `docs/03-components.md`). Plus the **frozen contract** `docs/contracts/phase3-sessions.md`.
> **Template to mirror:** the Phase 2 `libs/admin-portal/feature-students` slice — copy its structure exactly.
>
> **File ownership (do not cross):** this stream edits **`frontend/` only** — specifically the new
> `libs/admin-portal/feature-sessions/`, new shared parts in `libs/shared/ui/`, `app.routes.ts`, and the
> `feature-shell` sidebar. Do not touch `backend/`. Build entirely against the frozen contract with
> **HttpClient mocked** — you do NOT need a running backend.

## Goal
Ship the admin Sessions experience (list, create/edit, tabbed detail, question editor, quiz settings) as an Nx
feature lib, wired to the API shapes in the frozen contract. Green gate: `npx nx build admin-portal` +
`npx nx test feature-sessions`.

## Established patterns to reuse
- Angular v20+ **standalone, signals, OnPush, functional guards**; data via `HttpClient` in a signal-backed service
  (mirror `StudentService`). Enums arrive as **string names**; model them as TS string-union types.
- Nx lib `type:feature`, `scope:admin-portal`. **A `type:feature` lib cannot import another feature**
  (eslint `enforce-module-boundaries`) — read taxonomy via this slice's own service hitting `/api/taxonomy/*`,
  do **not** import `feature-taxonomy`.
- Permissions checked via `AuthStore.hasPermission('SessionsRead' | 'SessionsCreate' | … | 'QuestionsEdit')`.
- Shared DS components live in `@sb/shared/ui` (`libs/shared/ui`). Toasts on every mutation.
- Jest is configured workspace-wide (shared `frontend/jest.preset.js`, per-lib `test-setup.ts` using `setupZoneTestEnv()`).

## Steps

### B1 — Lib scaffold + typed contract
`nx g @nx/angular:library feature-sessions --directory=libs/admin-portal/feature-sessions` (tags `type:feature`,
`scope:admin-portal`). Add:
- `session.models.ts` — TS interfaces + string-union enums **mirroring the frozen contract** (`SessionListDto`,
  `SessionDetailDto`, `SessionVideoDto`, `SessionMaterialDto`, `QuizSettingDto`, `QuestionDto`, `OptionDto`,
  `QuestionVariationDto`, `SignedUrlDto`, `PagedResult<T>`, `SessionStatus`, `VideoProcessingStatus`).
- `SessionService` (signal-backed over `HttpClient`) — one method per contract endpoint (#1–27), plus
  `loadGrades()/loadSubjects()/loadSpecializations()` hitting `/api/taxonomy/*`.

**Two design-parity helpers (frontend-only, match the prototype exactly):**
- **`specAccent(specializationName)`** — replicate `Admin Portal.dc.html`: palette
  `['blue','green','purple','orange','mint','pink','mustard','red']` indexed by the specialization's position in the
  loaded specializations list (`% 8`). Drives the session tile/Tag colors via `--sb-subject-{accent}-bg/-deep`. **Not
  stored server-side.**
- **Editor↔API field map** (component state uses the prototype's names): `text`→`bodyLatex`,
  `quizEligible`→`isValidForQuiz`, `hint`→`hintUrl`, `options:string[]`+`correct:index`→`options:[{text,isCorrect}]`.
  Map on load and before save.

### B2 — Shared DS parts (in `@sb/shared/ui`)
- `file-upload`/dropzone (image + document, shows progress — an untracked `progress/` component already exists).
- `latex-preview` (KaTeX — lighter than MathJax; lazy-load).
- reorderable list (video ordering). Reuse existing `card`, `pagination`, `toast`, `tabs`, table.

### B3 — Sessions list  (`scrSessions`, `FR-ADM-SES-001`)
Filter-bar card (search + grade + **subject** + status — no specialization filter), `sb-table` with columns
**Session** (specialization-**accent tile** with book icon + title + specialization subtitle) / Grade / Specialization
(Tag, accent-colored) / State (pill) / Qs / Videos / Enrolled, pagination + CSV export. Mirror `student-list`.
**No thumbnail image and no price column** — the tile color comes from `specAccent(specialization)` (see note below).

### B4 — Session create/edit  (`scrSessionEdit`, `FR-ADM-SES-002..006`)
2fr/1fr grid exactly per the prototype. **Left:** Details card (title; **description** textarea; Grade/Subject/
Specialization selects where **subject filters specialization**; Price + Validity; **Thumbnail** upload —
uploaded/stored, not re-displayed); Videos card (drag-reorder list showing "{i}. {name}",
"{lengthMinutes}:00 · secure HLS", "Access {n}×"; "Add/Edit video" modal: file + title + **Length (minutes)** +
Allowed access count); Materials card (add via file picker `.pdf,.csv,.png,.jpg,.jpeg`, show "{KIND} · {size}",
**preview/download** button per row → signed URL via #18, remove). **Right rail (sticky):** Publish card (status Switch
draft↔published), Gating card (prerequisite combo excluding self + quiz summary → Configure → quiz-settings),
Question bank card ("{n} questions attached" → Manage → detail).

### B5 — Session detail (tabbed)  (`scrSessionDetail`, `FR-ADM-SES-007/008`)
Header: specialization-**accent tile** (book icon) + title + status pill + subject **Tag** + "{grade} · {specialization}";
actions Unlock *(Phase 4 placeholder)* / Edit / Teacher-only soft-delete. Tabs (with count badges): **Overview**
(4 stat tiles + Details kv: grade/subject/specialization/price/prerequisite/quiz; show **description**) · **Videos**
(table #/Video/Length/Access/Avg-watched*) · **Materials** (cards + **preview/download** per card via #18) ·
**Question bank** (table: Question text/Mark/Variations/Quiz-eligible
pill + New question + Quiz settings) · **Enrolled** *(Phase 4)* · **Activity** (audit from `GET …/activity`).
`:id` via `withComponentInputBinding`. *Avg-watched / Avg-completion are Phase 5 placeholders.*

### B6 — Question editor + Quiz settings  (`scrQuestionEditor` + `scrQuizSettings`, `FR-ADM-QB-001..006`, `FR-ADM-QZ-001..002`)
**Question editor** (2-col): variation tabs ("Variation 1…N" + "+ Add variation", **no cap**); left = LaTeX **textarea** (mono)
+ optional image upload (auto-renders the embedded signed `imageUrl`) + Answer options (radio = correct, single-correct
enforced); right (sticky) = **live KaTeX preview** + Settings card (Mark input, "Eligible for gating quiz" Switch,
"Hint URL (assignments only)"). Keep the prototype's "Edits create a new snapshot…" helper copy.
**Quiz settings screen**: four range **sliders in minutes/counts** — Time limit 5–60 min, Number of questions 5–30,
Attempts 1–5, Minimum pass 40–100% — a warning Alert when `questionCount > quizEligibleQuestionCount`, and the
"Effective behaviour" prose summary.

### B7 — Routes + nav
Add to `apps/admin-portal/src/app/app.routes.ts`: `/sessions`, `/sessions/new`, `/sessions/:id`, `/sessions/:id/edit`
(lazy `loadComponent`, `withComponentInputBinding`). Add a sidebar entry in
`libs/admin-portal/feature-shell/src/lib/shell/shell.component.ts`. Guard visibility with `AuthStore.hasPermission`.

### B8 — Tests
Jest specs: `SessionService` (mocked `HttpClient`, assert URLs/payloads match the contract) + key components
(list filters, create form validation, question single-correct rule). Gate: `nx build admin-portal` +
`nx test feature-sessions` green.

## Working without a backend
Mock `HttpClient` in tests and (optionally) stand up an in-memory fixture for manual `nx serve` review. All shapes
come from the frozen contract — when the wiring stream connects the real API, only the base URL changes. If a real
shape mismatches the contract, **do not silently adapt** — record it for the wiring stream's reconciliation.

## Exit criteria
All five screens built to the contract; `nx build admin-portal` + `nx test feature-sessions` green; design matches
the prototype. Hand off to the wiring stream.

## Out of scope (defer)
Enrolled-students tab data + unlock/refund (Phase 4) · video **playback** (Phase 5; Phase 3 shows processing status
only) · student-facing catalogue.
