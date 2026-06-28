# Backlog — Future Features

Accepted gaps and deferred work that are not in any active implementation plan. Each entry records *why* it was deferred and exactly what would need to be built when it is picked up.

---

## BL-001 — iOS screenshot audit: record screenshot events in the student activity log

**Context:** iOS has no API to block still-screenshots (`NFR-APP-CAP-005`). The app already detects them via `userDidTakeScreenshot` (wired in A2 — `AppDelegate.swift` → `EventChannel('salah_bahzad/secure_surface/events')` → `player_page.dart`) and logs/flags locally, but the event is currently not persisted server-side. The watermark (serial + full name, A1) is the primary deterrent; this feature strengthens accountability by making each screenshot traceable in the admin audit trail, linked to the video and enrollment it was taken from.

**What to build:**

### App (Flutter — `app/`)
- In `player_page.dart`, on receipt of a `userDidTakeScreenshot` event from the `EventChannel` stream, fire `POST /api/me/videos/{videoId}/screenshot-detected` (authenticated, best-effort — do not surface an error to the student if it fails; the local flag/log already fired).
- Pass `enrollmentId` in the request body so the backend can link the event to the session.
- Only on iOS; the channel event is never emitted on other platforms so no platform guard is needed in Dart.

### Backend (.NET — `backend/`)
- New command `RecordVideoScreenshotDetected` + handler under `Features/Videos/` (Clean Architecture, CQRS, `RequireStudent`).
- Endpoint: `POST /api/me/videos/{videoId}/screenshot-detected` body `{ enrollmentId }`.
- Verify the student is enrolled and the videoId belongs to that enrollment (reuse the enrollment-ownership check from the gate handler) — return `403` if not, silently succeed if yes (no state change, audit only).
- Raise a domain event `VideoScreenshotDetectedEvent` with `AuditAction = "VideoScreenshotDetected"`, carrying `StudentId`, `VideoId`, `EnrollmentId`, `TenantId`, `DetectedAtUtc`. Attributed to the **Student** actor (`ActorType = Student`).
- No migration — this is append-only audit data written via the existing `AuditWriter`.

### Admin portal (Angular — `frontend/`)
- Surface `VideoScreenshotDetected` entries in the student's activity log (the audit feed in the admin portal's student detail view, Phase 5A) with a distinct icon and the video name resolved from `VideoId`.
- No new backend read is needed — the audit feed endpoint already returns all audit entries for a student; the frontend just needs to render the new action string.

**Requirement IDs:** `NFR-APP-CAP-005` (the accepted gap this closes), `FR-APP-CAP-002` (detect + report), `FR-PLAT-AUD-001` (audit all student actions).

**Dependencies:** A2 wired (the `userDidTakeScreenshot` EventChannel is already firing) · Phase 5A audit feed in the admin portal (already built).

**Why deferred:** The EventChannel detection is already in place (A2). Persisting it server-side is low-risk, low-urgency work that adds an enforcement paper trail but does not change the user-facing experience. Picked up whenever tighter screenshot accountability is needed.
