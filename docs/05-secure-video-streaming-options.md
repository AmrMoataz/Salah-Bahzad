# Secure Video & Asset Storage — Architecture

The decided approach for storing and delivering lesson videos and all other uploaded files (images, documents), and for meeting the hard requirement that **screenshots and screen recordings of video come out black** (phone, Windows, macOS) — while keeping the client's running cost as low as possible.

> **Decisions captured (with Amr):**
> 1. **No DRM.** The screenshot/recording black-out is delivered by the **native apps** via OS secure-surface flags (free), not by paid DRM in the browser (which can't black out Chrome/Firefox on desktop anyway).
> 2. **Apps carry video.** Playback happens in the native apps (Windows/macOS/iOS/Android). Clicking *Play* in the web portal **deep-links into the device's app**; the app **authenticates** the student and plays there. See [App functional](08-functional-app.md) and [App non-functional](09-non-functional-app.md) requirements.
> 3. **Cloudflare R2 + CDN** stores and delivers everything (video, HLS, images, documents), chosen because **egress is free** — the dominant cost for video, and worse for a MENA audience on most CDNs.
> 4. **HLS + AES-128 + short-lived signed URLs + in-player watermark**; no single downloadable file.
> 5. Phone-camera re-filming is **accepted**; the watermark makes any leak traceable.

## 1. Why this shape (the reality in one paragraph)

The Netflix-style black-out is a property of **DRM + the OS protected-media path**, and in a browser it only works on Safari (FairPlay) and Edge (PlayReady) — **not Chrome/Firefox on Windows/macOS**, which is most of your audience. The same black-out is **free** in a native app through OS flags (`FLAG_SECURE` on Android, capture detection + FairPlay/AVPlayer on iOS, `SetWindowDisplayAffinity` on Windows, `NSWindow.sharingType` on macOS) — your existing Flutter desktop app already does this. Since a mobile app is on your roadmap anyway (offline attendance), routing video through the apps gives you the requirement for the price of app-store fees (~$99/yr Apple + ~$25 once Google) and lets you skip a recurring DRM bill that wouldn't even cover your biggest segment. What you still pay for — cheaply — is storage and delivery of the encrypted HLS, and that's where R2 wins.

## 2. Storage & delivery — Cloudflare R2 + CDN

**The cost driver is egress (delivery), not storage.** Storage is pennies; bandwidth is what makes a video bill balloon, and MENA delivery is priced higher on most CDNs. Cloudflare R2 charges **$0 egress**, so the client's bill stays near just the (tiny) storage cost no matter how much students watch.

| Concern | Choice | Why |
|---|---|---|
| Object storage | **Cloudflare R2** (~$0.015/GB-month, **$0 egress**) | Egress-free is the single biggest cost lever for video, especially to MENA. |
| CDN | **Cloudflare CDN** in front of R2 | Native R2 integration; global PoPs incl. MENA; cached delivery is cheap/free. |
| Transcode | **ffmpeg** (one-time per video, on a worker/the backend) | Produces multi-bitrate HLS + AES-128 keys. No per-minute encode fee. |
| Player | **The native app's player** (+ hls.js in the interim browser) | You own it, so no managed-video-platform fee. |
| Cheaper-storage variant | **Backblaze B2** (~$0.006/GB) + Cloudflare (free egress via Bandwidth Alliance) | If raw storage cost ever matters more than R2's simplicity. |

Avoid **AWS S3 + CloudFront** for this workload — egress at ~$0.08–0.09/GB is the expensive trap.

Rough client cost for one teacher: a ~100 GB catalogue is ~$1.50/month of storage on R2, and because egress is free, delivery adds ~nothing — so the monthly media bill is effectively a couple of dollars plus negligible operation fees. (Indicative — verify current pricing.)

## 3. The end-to-end pipeline (upload → play)

```
Staff upload (admin portal)
   │  original file → backend → R2 (private "originals" bucket)
   ▼
ffmpeg transcode → multi-bitrate HLS (.m3u8 + segments) + AES-128 key
   │  written to R2 ("hls" bucket); key released only to an authorised player
   ▼
Cloudflare CDN (in front of R2; cached segments, $0 egress)
   ▲
   │  ── student clicks "Play" in the web portal ──
   │  portal opens a DEVICE-AWARE deep link into the native app:
   │     Win/macOS → salah-bahazad://stream?session=…&video=…&handoff=<one-time code>
   │     iOS/Android → universal / app link to the same
   │  (app not installed → prompt to install)
   ▼
Native app opens and AUTHENTICATES the student
   │  exchanges the short-lived ONE-TIME handoff code for its own
   │  authenticated session (or prompts Firebase sign-in).
   │  ⚠ the raw JWT is never placed in the URL (fixes today's smell)
   ▼
App requests a SHORT-LIVED SIGNED HLS URL from the backend
   │  gate: active enrollment + quiz passed (if any) + access remaining
   │  → records view + audit → returns the signed URL (valid minutes)
   ▼
App plays HLS (decrypts AES-128) with:
   • dynamic watermark (student serial/phone) painted over the frame
   • OS black-out flag ON for the duration  ← the screenshot/record protection
```

## 4. Authentication & the browser→app handoff

- The **app is a first-class authenticated client**, not a dumb player: it establishes its own authenticated session (Firebase identity → platform session), so a leaked deep link alone can never play anything.
- The portal's *Play* action **detects the OS** and opens the matching app via its registered scheme (`salah-bahazad://`) / iOS Universal Link / Android App Link. If the app isn't installed, the portal shows an install prompt with store links.
- The deep link carries a **short-lived, single-use handoff code** (and the session/video ids), **never the raw bearer token**. The app exchanges that code for its session; if the code is missing/expired, the app falls back to its own sign-in. This both fixes the current "raw JWT in a custom-protocol URL" finding and lets the app enforce **device binding** (`FR-PLAT-DEV-*`) at exchange time.

## 5. Protection layers (defence in depth)

| Layer | What it stops | Where |
|---|---|---|
| Server enrollment gate | Access without a valid, paid, unexpired enrollment | Backend (`FR-PLAT-VID-001`) |
| App authentication + device binding | A leaked deep link / shared account playing video | App + backend (`FR-PLAT-DEV-*`) |
| Short-lived signed URLs | Link sharing / hotlinking / dev-tools grabbing a durable URL | Backend + CDN |
| HLS segmentation + AES-128 | Downloading a single playable file; naive rippers | ffmpeg + R2 |
| **OS black-out flag** | **Screenshots & screen recording (OBS, phone, built-in)** | **Native app (per-OS) — [09](09-non-functional-app.md)** |
| Dynamic watermark | Makes any phone-camera leak traceable to the student | App player overlay |
| Accepted residual | Phone camera pointed at the screen | — (watermark traces it) |

## 6. Images & documents (everything that isn't video)

These don't need HLS or the app — but they need the same storage discipline, and some are sensitive.

- **Do not store uploads on the app server's disk.** On container hosts the local disk is wiped on every redeploy and isn't backed up or shared across instances — uploads silently vanish. (The current backend writes to a local `Storage/` folder and serves it at `/Storage`; that's both a durability and a security problem.)
- **Public, low-sensitivity** (session thumbnails, question images) → R2 public bucket behind the CDN. Pennies.
- **Private / sensitive** (ID-verification images, paid PDFs/materials) → R2 **private** bucket, encrypted at rest, fetched via short-lived **signed URLs after an auth/enrollment check** — never a public link. ID images are minors' PII, so this is mandatory and access is audited (`NFR-PRIV-001/002`).

Everything lives in one R2 account, separated by bucket/prefix; the database stores only object keys/manifest paths.

## 7. Threat model — what we stop vs accept

| Threat | Outcome |
|---|---|
| Right-click/save, dev-tools URL grab, link sharing | **Stopped** — signed short-lived HLS, no durable/single-file URL. |
| `yt-dlp` / stream rippers | **Stopped in practice** — encrypted segmented HLS + signed access (and we leave YouTube entirely). |
| Screenshot / screen recording (phone, Windows, macOS) | **Black** — in the native app, via OS flags. |
| Same, in a desktop browser (interim only) | Not blocked — watermarked + capturable until the user is on the app. |
| Phone camera filming the screen | **Accepted** — watermark makes it traceable. |
| Account sharing / leaked deep link | Handled by app auth + device binding, not the video layer alone. |

## 8. Interim before the mobile app ships

Desktop is already covered by your existing desktop app. Until the **mobile** app ships, mobile students would watch in the mobile browser with watermark + signed HLS but **no black-out** (capturable). Given you accept camera capture and expect some redistribution, that's a reasonable temporary gap — or a reason to sequence the mobile app early. Browsing, enrollment, assignments, and quizzes all stay in the responsive web portal throughout; only *video* is handed to the app.

## 9. How this maps to the build

1. **Where to upload:** staff upload via the admin portal → backend → **R2** (private originals); a transcode job writes HLS to R2. The DB stores the R2 keys/manifest path on `SessionVideo`, not a YouTube id.
2. **How to stream:** *Play* deep-links into the device's app → app authenticates → app asks the backend, which checks the gate (`FR-PLAT-VID-001`), records the view + audit (`FR-PLAT-VID-002`), and returns a **short-lived signed HLS URL** (`FR-PLAT-VID-003`).
3. **Where the black-out comes from:** the **app**, via OS flags — see [App NFRs](09-non-functional-app.md). Not DRM.
4. **Watermark:** painted by the app player with the student's serial/phone (`FR-PLAT-VID-004`).
5. **Assets:** images/docs in R2 (public vs private-signed) per §6.

---

➡️ Next: [06 — Database & event-sourcing assessment](06-database-and-event-sourcing-assessment.md) · [08 — App functional requirements](08-functional-app.md) · [09 — App non-functional requirements](09-non-functional-app.md)
