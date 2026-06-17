# Implementation Gap Analysis

You asked separately: *what is implemented, is it implemented the right way, and what is missing?* The requirement docs (01–04) deliberately describe the **target state and ignore the code**. This document is the bridge back to reality: it maps each capability to what exists in the **new** stack (`salah-bahazad-backend` + `salah-bahazad-student-portal`) today, then lists the correctness/security issues and a prioritised roadmap.

> Scope note: the assessment targets the **new** backend and student portal. `salah-education` (old .NET) and `salah-education-admin` (old Angular 15) are treated as legacy; the admin portal is being rebuilt. The Flutter desktop app is the basis of the **chosen native-app video path** (to be reworked and extended to mobile) — see [05](05-secure-video-streaming-options.md)/[08](08-functional-app.md)/[09](09-non-functional-app.md).

**Legend:** ✅ Implemented · 🟡 Partial / needs work · ❌ Missing

## Identity, authorization & tenancy

| Capability | Status | Notes |
|---|---|---|
| Staff password auth | 🟡 | BCrypt today; target moves staff to Firebase too (no platform-stored passwords). |
| Pending-approval gate (only Active can sign in) | ✅ | Enforced in `User.ValidatePasswordForAuthentication`. |
| **Firebase** auth for all users (students + staff) | ❌ | Currently BCrypt for both; no external IdP; reset/email delegated to Firebase in target. |
| Refresh token + session revocation | ❌ | No refresh flow; expiry caught only on next failed call. |
| Granular permissions (configurable Assistant/Teacher split) | ❌ | Roles are a `UserType` enum only. |
| **Server-side authz on every privileged endpoint** | ❌ | `POST /api/Codes` has *no* auth; `UsersController` has *no* role checks anywhere. |
| **Multi-tenancy** (`TenantId`, isolation) | ❌ | Single teacher hard-baked; no tenant concept. |

## Taxonomy & reference data

| Capability | Status | Notes |
|---|---|---|
| Grades CRUD | ✅ | `GET/POST/DELETE`. |
| Subjects / Specializations management | 🟡 | API exposes reads/filters; full CRUD lives partly in the old admin. |
| **City → Region cascade** | ❌ | `User.City`/`Location` are free-text; no Region, no dependency. |
| Block deletion of in-use taxonomy | 🟡 | Not verified/consistent. |

## Sessions, enrollment & codes

| Capability | Status | Notes |
|---|---|---|
| Session core (title/desc/price/days-valid 0–365/thumbnail/grade/specialization) | ✅ | — |
| Videos with per-video access cap | ✅ | `SessionVideo` + `VideoAccess`. |
| Materials (PDF/CSV/PNG/JPG) | ✅ | `SessionMaterial`. |
| Prerequisite chaining | ✅ | Self-referencing + `CanEnrollUser` gate. |
| Quiz settings per session | ✅ | time / #questions / #attempts / min-pass. |
| Snapshot-on-edit (don't rewrite history) | ✅ | Owned-type copies for assignment/quiz. |
| Session publish status (draft/published/archived) | 🟡 | No explicit lifecycle state observed. |
| Enroll by code (value == price, one-shot) | ✅ | `ActivateSessionWithCode`. |
| Staff **unlock** (bypass code) | ✅ | `UnlockSessionForStudent`. |
| Re-enroll resets counters / extends expiry | ✅ | `PrepareVideoAccess` / `ExtendSessionExpiry`. |
| **Refund / revoke enrollment** | 🟡 | Present in old admin; not confirmed in new backend. |
| Code generate + export | ✅ | CSV in backend; Excel in old admin (`xlsx`). |
| Code **disable / enable / delete** (soft-delete) | ❌ | One-directional `Active→Used` only. |
| Code register: who used it, for what, when | 🟡 | Paginated list + status filter; full usage join partial. |
| **Payments seam** (future Paymob) | ❌ | Enrollment hard-coupled to Code. |

## Question bank & assessment

| Capability | Status | Notes |
|---|---|---|
| MCQ with mark | ✅ | — |
| LaTeX / text body | ✅ | MathJax in portals. |
| **Image upload** for a question | ✅ | `QuestionVariation.ImageUrl`. |
| Multiple **variations** | ✅ | For randomisation. |
| **Quiz-eligible** flag | ✅ | `Question.IsValidForQuiz`. |
| **Hint URL**, assignment-only | 🟡 | `Question.VideoUrl` exists; "assignment-only" display not enforced server-side. |
| Edit a question (not just add/remove) | 🟡 | Create + remove-from-session only; no update endpoint. |
| Assignment: auto-generate, open-access, resumable, one-at-a-time | ✅ | — |
| Assignment time-tracking + resume | ✅ | SignalR assignment hub. |
| Assignment **behaviour logging** | 🟡 | Hub tracks time/access, not full enter/leave/navigate behaviour. |
| Assignment auto-grade → attendance | ✅ | Domain-event handler. |
| Quiz: generate from prerequisite, respect settings, randomise | ✅ | Attempts materialised up-front. |
| Quiz **single-sitting forfeit on disconnect** | ✅ | Quiz hub auto-forfeits (zero score). |
| Quiz **server-side timer auto-submit** | 🟡 | Countdown is client-side in the portal; server only forfeits on disconnect. |
| Tab/window-switch detection | 🟡 | Detected client-side today; target records it for staff review (not auto-forfeit). |
| Best-of scoring, all attempts visible | ✅ | `QuizMaxScoreAttempt`. |
| Pass threshold `≥` | ❌ | Code uses strict `>` — scoring exactly the minimum **fails**. Confirmed fix: use `≥`. |
| Quiz events audited (start/submit/forfeit/timeout) | ❌ | Not audited. |

## Secure video

| Capability | Status | Notes |
|---|---|---|
| Server gate: active enrollment + remaining access | ✅ | `SessionVideo.AccessVideo`. |
| Record/decrement view + "Opened" activity | 🟡 | Session-scoped activity only. |
| **Short-lived signed HLS URLs** | ❌ | Today YouTube + `yt-dlp`, raw JWT in the deep link; target: signed AES-128 HLS from Cloudflare R2/CDN + one-time handoff code. |
| **Dynamic watermark + black-out** | ❌ | Target: watermark painted in the app player; black-out via native-app OS flags (desktop app already does this; mobile app needed). |
| Black-out approach (decided: native apps, no DRM) | 🟡 | Desktop app already blacks out via OS flags; mobile app still needed; current app is fragile/ToS-violating (yt-dlp) and must be reworked. |
| Durable media/asset storage (R2) | ❌ | Today local disk under `Storage/`, served at `/Storage`; target Cloudflare R2 + CDN with signed URLs. |
| Specific failure states | ✅ | Desktop app surfaces six. |

## Attendance & audit

| Capability | Status | Notes |
|---|---|---|
| Attendance per (student, session): assignment + quiz scores | ✅ | `Attendance`. |
| Auto-update on grade/attempt | ✅ | Event-driven. |
| Easy cross-student/session reporting + export | 🟡 | Data present; the navigable reporting UX is a rebuild target. |
| **Audit: who/what/when/where on everything** | ❌ | Only `SessionActivity` (5 session-scoped types). |
| Generic activity-log mechanism | ❌ | `EntityActivityLog` buffer exists but is **never persisted or read** — effectively dead. |
| Login / code-lifecycle / user-mgmt / device / quiz auditing | ❌ | None of these are recorded. |
| Append-only immutability + tamper-evidence | 🟡 | No enforced immutability; no hash-chain. |

## Registration & device

| Capability | Status | Notes |
|---|---|---|
| Multi-step registration (school, grade, parents×2, ID image) | ✅ | Captured in `RegisterStudent` + ID upload. |
| **City → Region** in registration | 🟡 | City free-text; no Region cascade. |
| **Terms & conditions** acceptance record | ❌ | Not captured. |
| Rejection **reason** | ✅ | `User.Comment`, required when rejecting. |
| **Device binding** (one device, consent, staff-clear) | ❌ | No device model at all. |

## Correctness & "is it done the right way?" — issues to fix

Severity: 🔴 high · 🟠 medium · 🟡 low.

| # | Issue | Sev | Where |
|---|---|---|---|
| 1 | `POST /api/Codes` has **no authorization** — anyone reaching the API can mint redeemable codes. | 🔴 | backend `CodesController` |
| 2 | `UsersController` has **no role checks** — staff-management endpoints are reachable by any authenticated user (domain re-checks are partial/untraced). | 🔴 | backend `UsersController` |
| 3 | **Secrets committed in plaintext** (JWT key, DB strings w/ passwords, Sentry/Datadog/NewRelic keys) across `appsettings.*`. | 🔴 | backend config |
| 4 | **Migrations auto-apply on every boot, every env** — a bad migration crashes prod on container start. | 🔴 | backend startup |
| 5 | **No audit** of logins, code lifecycle, user management, quiz events, or device changes (the core requirement). | 🔴 | backend |
| 6 | SignalR hubs authenticate via **query-string params, not JWT**, and hold **per-process in-memory** state (no backplane) — insecure and breaks under horizontal scale (auto-forfeit/time-tracking silently fail). | 🟠 | backend hubs |
| 7 | Quiz pass uses strict `>` not `≥` — exact-minimum score fails. **Confirmed: change to `≥`.** | 🟠 | backend `QuizAttempt` |
| 8 | **No business-rule tests** — enrollment, grading, quiz scoring/forfeit, codes have zero regression coverage. | 🟠 | backend |
| 9 | **No CI/CD**; Render deploy references a non-existent `docker-compose.yml`; admin Docker staging/prod build calls non-existent npm scripts. | 🟠 | backend, old admin |
| 10 | **No responsive design** — the student portal targets desktop browsers; phone/tablet breakpoints and a responsive layout are not established. | 🟠 | student portal |
| 11 | Raw JWT passed to the desktop app via `salah-bahzad://…&token=…` (can leak via process lists/history); **TLS validation disabled in desktop prod**; **bearer tokens logged in plaintext**. | 🟠 | desktop app |
| 12 | Several domain exceptions named like 401/403 actually return **HTTP 400**. | 🟡 | backend exceptions |
| 13 | `PurchaseStatus.Completed` appears to have **no code path** that sets it. | 🟡 | backend domain |
| 14 | Dead code: `EntityActivityLog` path, `Salah.Bahazad.Client`, old-admin `VimeoService`/`tus-js-client`, desktop `TestScreen`/duplicate providers. | 🟡 | multiple |

## Suggested priority order

**P0 — security & integrity (do first, mostly small):** issues 1, 2, 3, 4; add the audit log + interceptor (issue 5).

**P1 — close the feature gaps that the business depends on:** code disable/delete + lifecycle audit; device binding; comprehensive audit coverage; tenant-readiness (`TenantId` + global filter); fix the SignalR auth/backplane (issue 6); server-side quiz timer; the `≥` pass rule (issue 7).

**P2 — productise & de-risk:** Firebase auth for all users (students + staff); City/Region cascade; terms acceptance; payments seam; secure-video migration to R2 + HLS + native apps ([05](05-secure-video-streaming-options.md)); business-rule tests + CI/CD; responsive phone/tablet/desktop support; desktop hardening or retirement.

---

⬅️ Back to the [overview](README.md).
