# FROZEN API CONTRACT — Phase 3 (Sessions, content & question bank)

> **Status: FROZEN.** Single shared source of truth between the backend stream
> (`IMPLEMENTATION-PLAN-phase3-backend.md`) and the frontend stream
> (`IMPLEMENTATION-PLAN-phase3-frontend.md`). **Neither stream edits this file while building.**
> Drift is reconciled in the wiring stream under change control. Created 2026-06-19.
>
> **Design-derived:** every DTO and endpoint here is shaped to the exact prototype screens in
> `.claude/Salah Bahzad Teacher Portal/Admin Portal.dc.html` (`scrSessions`, `scrSessionEdit`,
> `scrSessionDetail`, `scrQuestionEditor`, `scrQuizSettings`). See §4 (Design parity map).
>
> Requirement IDs: `FR-PLAT-SES-001..009`, `FR-PLAT-QB-001..006`, `FR-PLAT-VID-007`,
> `FR-PLAT-AST-001..004`, `FR-ADM-SES-001..011`, `FR-ADM-QB-001..006`, `FR-ADM-QZ-001..002`.

---

## 0. Global conventions

| Concern | Rule |
|---|---|
| Base path | `/api` · Bearer platform JWT on every endpoint · default-deny via `RequirePermission` |
| JSON casing | camelCase over the wire; C# DTOs are PascalCase records |
| Enums | **string names** (`JsonStringEnumConverter`) — frontend models them as string unions |
| Dates | ISO 8601 `DateTimeOffset` UTC, suffix `...AtUtc` |
| Errors | RFC 7807 `ProblemDetails` (400 validation · 403 permission · 404 not found · 409 conflict/illegal-state) |
| Pagination | `PagedResult<T>` (`items/total/page/pageSize/totalPages`), defaults `page=1`, `pageSize=20` |
| Uploads | `multipart/form-data`. Images: `image/jpeg`, `image/png`, `image/webp`. Videos: `video/mp4`, `video/quicktime`, `video/webm` (≤ 2 GB). Materials: `application/pdf`, `text/csv`, `image/png`, `image/jpeg`. Server validates type+size, stores to R2 **private** bucket, DB keeps the **object key** only. |
| Media display | The admin portal renders a **specialization-accent color tile** for sessions (never the thumbnail image) — thumbnails are uploaded/stored for the future student catalogue only. **Question/variation images** auto-render in the editor via a short-lived **signed URL embedded** in the question read model (no click, no audit). **Materials** show a **preview/download button per row** that fetches a short-lived signed URL on click (#18). |
| Permissions | `SessionsRead/Create/Edit/Delete/Publish`, `QuestionsRead/Create/Edit/Delete` — **already exist**. |

### Enums
```
SessionStatus         = "Draft" | "Published" | "Archived"
VideoProcessingStatus = "Pending" | "Processing" | "Ready" | "Failed"
```

---

## 1. DTOs (shaped to the screens)

```jsonc
// SessionListDto — scrSessions table row (cols: Session, Grade, Specialization, State, Qs, Videos, Enrolled)
{
  "id": "guid", "title": "string",
  "gradeName": "string|null", "subjectName": "string|null", "specializationName": "string|null",
  "status": "Draft", "questionCount": 0, "videoCount": 0, "enrolledCount": 0   // enrolledCount=0 until Phase 4
}
// NOTE: no thumbnailUrl/price in the list — the row shows a specialization-accent book-icon tile + title.

// SessionVideoDto — scrSessionEdit/Detail "Videos" (lengthMinutes is admin-entered, e.g. "8:00")
{ "id":"guid","title":"string","order":0,"lengthMinutes":10,"accessCount":3,
  "processingStatus":"Ready","createdAtUtc":"…" }

// SessionMaterialDto — "kind" is the upper-case extension label shown in the UI ("PDF","PNG","CSV")
{ "id":"guid","fileName":"string","kind":"string","sizeBytes":0,"createdAtUtc":"…" }

// QuizSettingDto — scrQuizSettings knobs (minutes!). null when unset.
{ "timeLimitMinutes":15, "questionCount":10, "attemptCount":2, "minPassPercent":60 }

// SessionDetailDto — scrSessionDetail header + Overview "Details" + tab badges
{
  "id":"guid","title":"string","description":"string|null","price":0,"validityDays":90,"status":"Draft",
  "gradeId":"guid","gradeName":"string|null",
  "subjectId":"guid","subjectName":"string|null",                 // derived via Specialization.SubjectId
  "specializationId":"guid","specializationName":"string|null",
  "thumbnailUrl":"string|null",                                   // stored; NOT displayed by the admin UI
  "prerequisiteSessionId":"guid|null","prerequisiteTitle":"string|null",
  "quizSetting": { /* QuizSettingDto */ },                        // or null
  "videos":[ /* SessionVideoDto */ ], "materials":[ /* SessionMaterialDto */ ],
  "questionCount":0, "quizEligibleQuestionCount":0, "enrolledCount":0,
  "createdAtUtc":"…","updatedAtUtc":"string|null"
}

// OptionDto · QuestionVariationDto · QuestionDto — scrQuestionEditor
{ "id":"guid","text":"string","isCorrect":true }                                    // OptionDto
{ "id":"guid","bodyLatex":"string|null","imageUrl":"string|null","options":[/*OptionDto*/] } // variation
{
  "id":"guid","sessionId":"guid",
  "bodyLatex":"string|null","imageUrl":"string|null",             // imageUrl signed+embedded (editor preview)
  "mark":2,"isValidForQuiz":true,"hintUrl":"string|null",
  "options":[ /* OptionDto */ ], "variations":[ /* QuestionVariationDto */ ],
  "createdAtUtc":"…","updatedAtUtc":"string|null"
}

// SessionActivityDto — scrSessionDetail "Activity" tab (mirrors StudentAuditEntryDto)
{ "id":"guid","action":"string","summary":"string|null","actorId":"guid|null",
  "actorRole":"string|null","actorType":"string","ipAddress":"string|null","occurredAtUtc":"…" }

// SignedUrlDto
{ "url":"https://…","expiresAtUtc":"…" }
```

**Field-name mapping (editor ↔ API)** — the frontend models state in the prototype's vocabulary and maps to the API:
`text`(editor)→`bodyLatex` · `quizEligible`→`isValidForQuiz` · `hint`→`hintUrl` ·
`options:string[]`+`correct:index` → `options:[{text,isCorrect}]`.

**Invariants (400 on violation):** `validityDays` 0–365 · `price` ≥ 0 · video `lengthMinutes` ≥ 0 ·
`accessCount` ≥ 0 · quiz `timeLimitMinutes` 5–60, `questionCount` 5–30, `attemptCount` 1–5, `minPassPercent` 40–100 ·
`mark` > 0 · options ≥ 2 with **exactly one** `isCorrect` · question/variation body `bodyLatex` **and/or** image required ·
quiz `questionCount` ≤ `quizEligibleQuestionCount` (client warns per `FR-ADM-QZ-002`; server hard-blocks on publish).

---

## 2. Endpoints — Sessions

| # | Method & path | Perm | Request | Response |
|---|---|---|---|---|
| 1 | `GET /api/sessions` | SessionsRead | query `search? gradeId? subjectId? status? page pageSize` | `PagedResult<SessionListDto>` |
| 2 | `POST /api/sessions` | SessionsCreate | `{ title, description?, price, validityDays, gradeId, specializationId }` | `201 SessionDetailDto` |
| 3 | `GET /api/sessions/{id}` | SessionsRead | — | `SessionDetailDto` / 404 |
| 4 | `PUT /api/sessions/{id}` | SessionsEdit | `{ title, description?, price, validityDays, gradeId, specializationId }` | `SessionDetailDto` |
| 5 | `PUT /api/sessions/{id}/thumbnail` | SessionsEdit | multipart `file` | `SessionDetailDto` |
| 6 | `PUT /api/sessions/{id}/prerequisite` | SessionsEdit | `{ prerequisiteSessionId: guid\|null }` | `SessionDetailDto` / 409 self-or-cycle |
| 7 | `PUT /api/sessions/{id}/quiz-settings` | SessionsEdit | `QuizSettingDto` | `SessionDetailDto` / 400 |
| 8 | `POST /api/sessions/{id}/publish` | SessionsPublish | — | `SessionDetailDto` / 409 |
| 9 | `POST /api/sessions/{id}/archive` | SessionsPublish | — | `SessionDetailDto` / 409 |
| 10 | `DELETE /api/sessions/{id}` | SessionsDelete | — | `204` (soft-delete) |
| 11 | `GET /api/sessions/{id}/activity` | SessionsRead | query `page pageSize` | `PagedResult<SessionActivityDto>` |

### Videos (`scrSessionEdit` / `scrSessionDetail` › Videos)
| # | Method & path | Perm | Request | Response |
|---|---|---|---|---|
| 12 | `POST /api/sessions/{id}/videos` | SessionsEdit | multipart `file`, `title`, `lengthMinutes`, `accessCount` | `201 SessionVideoDto` (status `Pending`/`Processing`) |
| 13 | `PUT /api/sessions/{id}/videos/{videoId}` | SessionsEdit | multipart `title`, `lengthMinutes`, `accessCount`, `file?` (optional replace) | `SessionVideoDto` |
| 14 | `PUT /api/sessions/{id}/videos/reorder` | SessionsEdit | `{ orderedVideoIds: guid[] }` | `SessionVideoDto[]` |
| 15 | `DELETE /api/sessions/{id}/videos/{videoId}` | SessionsEdit | — | `204` |

> Upload stores the source to R2 + enqueues the **transcode seam** (`IVideoProcessingQueue`, stubbed → `Ready`).
> No playback/HLS URL here — that is the Phase 5 video gate (`FR-PLAT-VID-001..006`). `lengthMinutes` is admin-entered.

### Materials (`scrSessionEdit` / `scrSessionDetail` › Materials)
| # | Method & path | Perm | Request | Response |
|---|---|---|---|---|
| 16 | `POST /api/sessions/{id}/materials` | SessionsEdit | multipart `file` | `201 SessionMaterialDto` |
| 17 | `DELETE /api/sessions/{id}/materials/{materialId}` | SessionsEdit | — | `204` |
| 18 | `GET /api/sessions/{id}/materials/{materialId}/url` | SessionsRead | — | `SignedUrlDto` (preview/download button per material) |

## 3. Endpoints — Question bank (`scrQuestionEditor`, `scrSessionDetail` › Question bank)

| # | Method & path | Perm | Request | Response |
|---|---|---|---|---|
| 19 | `GET /api/sessions/{id}/questions` | QuestionsRead | query `page pageSize` | `PagedResult<QuestionDto>` |
| 20 | `POST /api/sessions/{id}/questions` | QuestionsCreate | `{ bodyLatex?, mark, isValidForQuiz, hintUrl?, options:[{text,isCorrect}] }` | `201 QuestionDto` |
| 21 | `PUT /api/sessions/{id}/questions/{questionId}` | QuestionsEdit | `{ bodyLatex?, mark, isValidForQuiz, hintUrl?, options:[{id?,text,isCorrect}] }` | `QuestionDto` |
| 22 | `PUT /api/sessions/{id}/questions/{questionId}/image` | QuestionsEdit | multipart `file` (DELETE same path clears) | `QuestionDto` |
| 23 | `DELETE /api/sessions/{id}/questions/{questionId}` | QuestionsDelete | — | `204` (soft, "Detach") |
| 24 | `POST /api/sessions/{id}/questions/{questionId}/variations` | QuestionsEdit | `{ bodyLatex?, options:[{text,isCorrect}] }` | `201 QuestionVariationDto` |
| 25 | `PUT …/variations/{variationId}` | QuestionsEdit | `{ bodyLatex?, options:[{id?,text,isCorrect}] }` | `QuestionVariationDto` |
| 26 | `PUT …/variations/{variationId}/image` | QuestionsEdit | multipart `file` | `QuestionVariationDto` |
| 27 | `DELETE …/variations/{variationId}` | QuestionsEdit | — | `204` |

---

## 4. Design parity map (prototype screen → contract)

| Prototype screen | Uses |
|---|---|
| `scrSessions` (list) | #1 · `SessionListDto`. Cols Session(accent tile+title+specialization)/Grade/Specialization(Tag)/State(pill)/Qs/Videos/Enrolled. Filters search+grade+subject+status. Accent = `specAccent(specialization)` (frontend-only palette). |
| `scrSessionEdit` (create/edit) | #2/#4 details (title, **description**, grade, subject→filters specialization, price, validity, **thumbnail** #5); Videos #12–15 (drag-reorder, "Add/Edit video" modal: file/title/lengthMinutes/accessCount); Materials #16–18 (add/remove + **preview/download** per row); right rail Publish (status switch #8/#9), Gating (prerequisite #6 + quiz summary→#7), Question bank count→detail. |
| `scrSessionDetail` | header accent tile + status pill + subject Tag + "grade · specialization"; Edit→edit, soft-delete #10. Tabs: Overview (4 tiles + Details kv: grade/subject/specialization/price/prerequisite/quiz) · Videos #3 · Materials #3 + **#18 preview/download** · Question bank #19 (Table: text/mark/variations/quiz-eligible + New question→editor, Quiz settings→#7) · Enrolled *(Phase 4 placeholder)* · Activity #11. |
| `scrQuestionEditor` | #20/#21 (+image #22); variation tabs #24–27; left = LaTeX textarea + image upload + options(radio=correct); right = live preview + Settings(mark, quiz-eligible switch, hint URL). |
| `scrQuizSettings` | #7 · `QuizSettingDto`. Four sliders (time 5–60 min, #questions 5–30, attempts 1–5, pass 40–100%), warning when `questionCount > quizEligibleQuestionCount`, effective-behaviour prose. |

## 5. Notes that bind both sides
- **Snapshot-on-edit (`FR-PLAT-SES-007`) is NOT in Phase 3** (consumed at enrollment, Phase 4/5). The editor still shows
  the prototype's "Edits create a new snapshot…" copy, but the bank is mutable now and no `snapshot` fields exist.
- **`enrolledCount` / Enrolled tab / Unlock / Refund / "Avg. completion" / "Avg. watched"** are Phase 4/5 — `enrolledCount`
  is always `0`; those analytics are placeholders. Fields exist now so shapes stay stable.
- **`description`** is a **Details-card field** (textarea, under Title) in the editor (`FR-PLAT-SES-001`); sent on
  create/update and shown on the detail Overview.
- **Variations are uncapped** on both API and UI (`FR-PLAT-QB-003`) — no 3-variation limit; the editor adds tabs `1…N`.
- Object-key convention (Phase 2 style): `sessions/{tenantId}/{thumbnails|videos|materials}/{guid}.ext`,
  `questions/{tenantId}/images/{guid}.ext`. Keys never leave the backend.
