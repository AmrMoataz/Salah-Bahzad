# Native App · A3 — WIRING stream (7-state live drive + sign-out secret purge)

> Status: **Planned** · Created 2026-06-27 · Proves phase **A3** end-to-end on the
> running Aspire stack + on-device. Run **after** the A3 app stream is compiled.
> **No backend changes. No contract changes.** The backend is unchanged from 5C.

---

## Pre-flight

- A3 app stream compiled: `flutter analyze` clean + `flutter test` green.
- `flutter build apk --debug` (Android) or `flutter run -d windows` (Windows) — at
  least one platform with the A3 code live.
- Aspire AppHost running: `dotnet run --project backend/src/SalahBahazad.AppHost`
  (F5). Confirms API at `http://localhost:5080`, MinIO at `:9000`, Postgres + Redis.
- A student account in **Tenant A** with a **Ready** AES-128 HLS `SessionVideo`
  (reuse the A1/5C fixture: one `enrollment_video_access` row with `access_remaining ≥ 1`).
- `GOOGLE_DESKTOP_CLIENT_ID` / `GOOGLE_DESKTOP_CLIENT_SECRET` in the run env if
  testing Google sign-in on Windows.

---

## Fixtures and helpers

All DB edits below are `psql` commands against the dev Postgres (port 5432, db
`salahbahazad_dev`, user `postgres`). The API's Aspire endpoint is
`http://localhost:5080`.

**Mint a Student JWT:**
```bash
# Exchange a valid Firebase ID token for a Student-role JWT via app-exchange
curl -s -X POST http://localhost:5080/api/auth/student/app-exchange \
  -H "Content-Type: application/json" \
  -d '{"firebaseIdToken":"<FIREBASE_ID_TOKEN>"}' | jq .
# → { "accessToken": "...", "refreshToken": "...", "student": { ... } }
```
Store `accessToken` as `$STUDENT_JWT`.

**Mint a Play handoff (gate call):**
```bash
# First, find the videoId from the fixture session video
# Then call the gate with a valid Student JWT to mint a handoff
curl -s -X POST http://localhost:5080/api/me/videos/<VIDEO_ID>/playback \
  -H "Authorization: Bearer $STUDENT_JWT" | jq .
# → { "handoff": "<CODE>", "videoId": "...", "sessionId": "..." }
```
The portal's deep-link format: `salah-bahazad://stream?videoId=…&sessionId=…&handoff=…`

---

## Live checks (7 failure states + sign-out)

Run against the app on-device. Each check: trigger the condition → verify the
correct §H screen renders (title verbatim, right primary action) → take note.

---

### Check 1 — `unauthorized` ("Your session expired")

**Trigger:** fire a deep link with a **forged / expired Bearer** — the app stores
it, calls `POST /api/me/videos/playback/redeem` → server returns `401`.

```bash
# Overwrite the app's stored JWT with a garbage value — on Windows dev:
# flutter_secure_storage writes to the Windows Credential Manager.
# Easiest: sign in normally, then in the DB expire the student's session
# (or just manually break the token in the secure storage via the app's own
# "sign out + sign in with a wrong password" then retry, or
# temporarily set the API's JWT secret to a different value).
```

Simpler approach: in `ApiClient` add a test flag `--dart-define=FORCE_401=true`
temporarily, or just use an old JWT that has genuinely expired (wait for the
access-token lifetime) and observe the silent-refresh fail path.

**Expected:** Player screen shows **"Your session expired"** + **"Sign in again"**.
Tapping "Sign in again" navigates to `/signin`. ✅

---

### Check 2 — `forbidden` ("You're not enrolled in this")

**Trigger:** call the gate for a video the student is **not enrolled in**. Either use
a different session's video ID in the deep link, or delete/refund the enrollment:
```sql
UPDATE enrollments SET status = 'Refunded' WHERE student_id = '<STUDENT_ID>'
  AND id = '<ENROLLMENT_ID>';
```
Then fire the deep link.

**Expected:** Player shows **"You're not enrolled in this"** + **"Open the portal"**.
Tapping opens the student portal URL in the system browser. ✅

---

### Check 3 — `maxviews` ("No views left for this lesson")

**Trigger:** set `access_remaining = 0` for the fixture enrollment + video:
```sql
UPDATE enrollment_video_accesses SET access_remaining = 0
  WHERE enrollment_id = '<ENROLLMENT_ID>' AND session_video_id = '<VIDEO_ID>';
```
Then fire the gate → `403 no_views_remaining`.

**Expected:** Player shows **"No views left for this lesson"** + **"Back to portal"**.
Tapping goes to Idle (which has the "Open the student portal" button). ✅

---

### Check 4 — `expired` ("Your enrollment expired")

**Trigger:** set the enrollment's expiry to the past:
```sql
UPDATE enrollments SET expires_at_utc = NOW() - INTERVAL '1 day'
  WHERE id = '<ENROLLMENT_ID>';
```
Then fire the gate → `403 enrollment_expired`.

**Expected:** Player shows **"Your enrollment expired"** + **"Open the portal"**. ✅

---

### Check 5a — `notfound` via 404 ("We can't find this lesson")

**Trigger:** fire a deep link with a **non-existent / wrong-tenant video ID** (e.g.
a random UUID). The `redeem` step returns `404`.

**Expected:** Player shows **"We can't find this lesson"** + **"Back to portal"**.
Message: "The link may be old, or the lesson was moved. Head back and try again." ✅

---

### Check 5b — `notfound` via 410 handoff expired

**Trigger:** mint a real handoff (gate call → gets `handoff` code), then **wait
60+ seconds** (the Redis TTL) before firing the deep link. `redeem` → `410
handoff_expired`.

**Expected:** Player shows **"We can't find this lesson"** + **"Back to portal"**.
Message: **"This play link expired. Press Play again in the portal…"** ✅

---

### Check 6 — `offline` ("You're offline")

**Trigger A — redeem-time offline:** disable the network adapter (or enable Flight
Mode), then fire a Play deep link. The app calls `redeem` → network error →
`ApiException.isNetwork = true` → `offline` state.

**Trigger B — mid-playback offline:** start a lesson successfully (video playing),
then disable the network while the buffer is live. The connectivity listener fires →
engine pauses immediately. Watch the buffer drain. When libmpv exhausts segments
and errors → `_onEngineError` → connectivity check → still offline → `offline` state
shows over the dark stage.

**Re-enable network:** connectivity listener fires → `online` & status is `error` →
no auto-resume (correct). User taps **"Try again"** → `retry()` reuses the manifest
→ engine re-opens → playback resumes without spending another view. ✅

**Trigger C — mid-playback short outage:** same as B but re-enable network **before**
the buffer drains (within ~10 s). Connectivity listener fires → `_onConnectivityChanged`
sees `online` + status is `paused` → calls `_engine.play()` → auto-resumes.
No error screen, no view spent. ✅

---

### Check 7 — `server` ("Something went wrong")

**Trigger:** stop the API process while a lesson is playing (`Ctrl-C` in the
AppHost terminal) OR between the gate call and the redeem. `redeem` or an engine
segment fetch → `5xx` (connection refused maps to a DioException with no HTTP
status → falls through to the `_engineError()` path). Connectivity is **on**, so
`_onEngineError` → online → `_engineError()`.

**Expected:** Player shows **"Something went wrong"** + **"Try again"**. Restart
the API, tap "Try again" → retries (within manifest TTL) or shows
`_handoffExpired()` if TTL elapsed. ✅

---

### Check 8 — Sign-out purges secrets

1. Sign in → land on Idle.
2. Tap **Sign out**.
3. **Verify keystore cleared:** sign back in and confirm the app goes through the
   full Firebase flow (not cached). On Windows: Credential Manager should no longer
   show the `secure_player_*` entries after sign-out.
4. **Verify in-memory secrets:** navigate to the Player during a session, then sign
   out from Idle (on desktop, use the window chrome's account area). The player is
   torn down → `_teardown()` runs → `_proxy.stop()` → `_manifest = null`. The
   signed URLs and AES key loader are GC'd. (No visible test — confirmed by code
   audit: `_teardown` and provider auto-dispose guarantee this.) ✅

---

## Sentry smoke (production build only)

Build with a test DSN:
```
flutter build apk --debug \
  --dart-define=SENTRY_DSN=https://…@sentry.io/… \
  --dart-define=SENTRY_ENV=staging
```

1. Trigger Check 7 (stop the API) → the player logs a `warning` breadcrumb.
2. Force an uncaught exception (temporarily throw in `_boot`) → Sentry Issues
   receives the event.
3. Inspect the Sentry payload: confirm **no** tokens, handoffs, signed URLs, or
   student PII in `message`, `breadcrumbs`, or `contexts`.

---

## Exit criteria

All 8 checks (7 §H states + sign-out) pass. No second view is spent on any retry
path (proven by querying `enrollment_video_accesses.access_remaining` before and
after "Try again"). Sentry smoke confirms payloads carry zero PII.

**A3 closes.** Next: **A4** — distribution, signing, CI, min-version.
