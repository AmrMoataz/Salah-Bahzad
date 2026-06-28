# Salah Bahzad — Deployment Plan (Staging + Production)

> Status: **Planned** · Created 2026-06-28 · Scope: deploy the whole platform — **backend (.NET 10 API), two Angular portals, and the Flutter "secure_player" app** — to a single **Hostinger KVM 2 VPS running Dokploy**, in two isolated environments (production + staging), plus distribution of the app to **Microsoft Store, Apple App Store (iOS + macOS), and Google Play**.
>
> This plan is grounded in the actual repository (file:line citations throughout) and reconciled into **one** architecture. Where the codebase already encodes a decision (env files, entitlements, bundle ids, config keys), this plan follows the code, not assumptions.
>
> **Operator profile:** solo developer. **Box:** 2 vCPU / 8 GB / 100 GB NVMe / 8 TB transfer, Ubuntu 24.04, Frankfurt, IP `72.60.38.211`, host `srv981081.hstgr.cloud`, Dokploy pre-installed (Docker Swarm + Traefik under the hood).

---

## 0. Executive summary — the decisions

| Question you asked | Decision |
|---|---|
| **Kubernetes — right tool?** | **No.** On a single 2-vCPU node, k8s' control plane (etcd/apiserver ≈ 0.5–1 vCPU + 1–1.5 GB) steals the exact resource your ffmpeg transcode needs, and a 1-node cluster gives you none of k8s' HA anyway. **Stay on Dokploy/Swarm.** A safe "learn k8s" path is in §3.1 — never on prod. |
| **How to containerise ffmpeg?** | **Bake the `ffmpeg` apt package into the API runtime image.** The transcoder shells out to the `ffmpeg`/`ffprobe` binaries in-process (`FfmpegMediaTranscoder.cs`); it is **not** a separate microservice. One `apt-get install ffmpeg` + `Transcode__FfmpegPath=/usr/bin/ffmpeg`. |
| **How to deploy staging + prod?** | **Two Dokploy projects** (`Salah Bahzad`, `Salah Bahzad Staging`), each = `postgres` + `redis` + `api` + `admin` + `student`. Network-isolated. Images built in GitHub Actions → GHCR → Dokploy deploys via webhook. |
| **Firebase config?** | **Three Firebase projects already exist in the repo** (`salah-bahzad-development` / `-staging` / `salah-bahzad`). Keep them separate. Server uses `Firebase__ServiceAccountJson` per env; portals/app use the committed per-env client configs (public, safe). §7.1. |
| **Cloudflare R2 config?** | **Separate private bucket + scoped token per env** (`sb-prod-private`, `sb-staging-private`). Path-style, region `auto`, signed URLs only, never public. §7.2. |
| **Env variables?** | Full matrix in §6.3; consolidated per-env `.env` in §7.4; secrets inventory in §7.3. All secrets via Dokploy env — never committed. |
| **App stores + auto-update + releasing a new version?** | All four stores in §9; the **forced-update gate is already built** (`/api/app/version-status` + `426 outdated_app`) and driven by the backend `AppVersions` config; full release runbook in §10. |

**Three launch-blocking code changes** (small, in the repo, must land before real traffic): §2.

**Resource verdict:** KVM 2 is **sufficient for launch** *only after* the §2 code changes and the staging CPU-cap. The headline risk is **CPU** (ffmpeg `libx264` on 2 cores during live quiz traffic), not RAM. Upgrade trigger to KVM 4 defined in §3.2.

---

## 1. Canonical names — the single source of truth

Every section below uses exactly these. The research drafts disagreed on hostnames/service/image names; **this table wins.** **Domain confirmed: `mrsalahbahzad.com`.** The **apex + `www` are reserved for the future marketing landing page** (not yet built); the whole platform lives on the subdomains below.

### 1.1 Public hostnames

| Role | Production | Staging |
|---|---|---|
| Landing page (future, not yet built) | `mrsalahbahzad.com` + `www.mrsalahbahzad.com` | — |
| Admin portal (web) | `admin.mrsalahbahzad.com` | `admin.staging.mrsalahbahzad.com` |
| Student portal (web) | `app.mrsalahbahzad.com` | `app.staging.mrsalahbahzad.com` |
| API + SignalR (public, for the Flutter app) | `api.mrsalahbahzad.com` | `api.staging.mrsalahbahzad.com` |
| Dokploy panel | `panel.mrsalahbahzad.com` (IP-restricted) | — |

> **Landing page:** the apex `mrsalahbahzad.com` (+`www`) is reserved for a marketing landing page that isn't built yet. When it lands, host it cheapest-and-simplest — either **Cloudflare Pages** (free, off-box, zero VPS load — recommended since you already use Cloudflare for R2) or a 6th Dokploy static-nginx Application in the prod project. Either way it never touches the API or shares the portals' auth.

> **Student portal is `app.*`, not `student.*`** — confirmed by the committed Firebase `authDomain`s and `environment.*.ts` (`frontend/apps/student-portal/src/environments/`). The Flutter app's `PORTAL_URL` therefore must be `https://app.mrsalahbahzad.com` (the A4 doc's `student.mrsalahbahzad.com` example is stale — ignore it).

### 1.2 Internal Dokploy service names (per project — networks are isolated, so the same names are safe in both)

| Service | Internal DNS | Port |
|---|---|---|
| API | `api` | `8080` |
| Postgres | `postgres` | `5432` (never published) |
| Redis | `redis` | `6379` (never published) |
| Admin portal nginx | `admin` | `80` |
| Student portal nginx | `student` | `80` |

> ⚠️ **Verify the exact internal DNS name Dokploy assigns.** Dokploy/Swarm may namespace a service (`<project>-<app>-…`). After creating the API service, confirm its resolvable name and set the portal nginx `$api_upstream` and the API's `Host=` connection string to match. Easiest: give each service a stable name/alias in Dokploy and keep prod/staging identical.

### 1.3 Container images (GHCR)

`ghcr.io/<owner>/<repo>/api`, `…/admin`, `…/student` — tags `:prod`, `:staging`, and `:<git-sha>`. **No separate migrator image** — the EF bundle is baked into the `api` image (§6.4).

### 1.4 App identifiers (frozen — never change after first store upload)

| Identifier | Value | Source |
|---|---|---|
| App display name | `Salah Bahzad Secure Player` | choose once, all stores |
| Apple bundle id (iOS + macOS) | `com.salahbahzad.securePlayer` | `app/macos/Runner/Configs/AppInfo.xcconfig`, `app/ios/Runner.xcodeproj/project.pbxproj` |
| Android application id | `com.salahbahzad.secure_player` | `app/android/app/build.gradle.kts:19` |
| Deep-link scheme | `salah-bahazad://stream` (spelling **bahazad**) | iOS/macOS `Info.plist`, Android manifest |
| MSIX identity | *(minted by Microsoft Partner Center)* | §9.1 |

> The Apple (camelCase) vs Android (snake_case) mismatch is **fine** — separate namespaces. **Do not "normalise" them**; the values are already wired into entitlements, keychain groups, Firebase apps, and OAuth clients.

### 1.5 The topology decision (resolves the biggest cross-section conflict)

**Hybrid — and it is not a compromise, it is correct:**

- **Portals are same-origin.** Each portal's nginx **reverse-proxies `/api` and `/hubs`** to the internal `api:8080`. This is exactly what the committed `environment.prod.ts` (`apiUrl: ''`) assumes. Benefits: (a) **no browser CORS**; (b) the student `sb_device` HttpOnly cookie stays **first-party** — a cross-origin `api.*` call would make it a third-party `SameSite=None` cookie that **Safari ITP blocks outright**, silently breaking device-binding enforcement. The student interceptor already sends `withCredentials` (`student-auth.interceptor.ts:9`) and the backend sets the cookie `SameSite=None; Secure` outside Development (`AuthEndpoints.cs:151`) — same-origin keeps it working regardless.
- **The Flutter app uses the public `api.*` route.** The app is **cookie-free** (device-agnostic `app-exchange`, bearer tokens in the OS keystore), so talking to `api.mrsalahbahzad.com` cross-origin is a non-issue — no CORS preflight from a non-browser client, no third-party-cookie exposure.
- **CORS origins are still set** (defense-in-depth) to the portal origins; browser traffic being same-origin means the policy is mostly unused but correct if any direct call happens.

So the API is reachable two ways that hit the same container: `api.*` via Traefik (for the app) and `/api` + `/hubs` proxied internally by each portal (for browsers).

> **Repo fix (recommended):** set `apiUrl: ''` in **both** apps' `environment.staging.ts` so staging exercises the same same-origin path as prod. (They currently point at `https://api.staging.mrsalahbahzad.com` — cross-origin. Keep that only if you deliberately want staging to regression-test the CORS path.)

---

## 2. Code changes required FIRST (in the repo, before production traffic)

These are **code**, not Dokploy config, and the infra plan depends on them. Ship them as a small PR before go-live.

### 2.1 Cap Hangfire concurrency — **launch-blocking before the first prod video**

`AddHangfireServer` is registered with **no `WorkerCount`** (`InfrastructureServiceExtensions.cs:207`) → defaults to `min(20, CPU×5)` = **10 workers** on 2 vCPU. Nothing stops several `libx264` transcodes running at once and pinning both cores while the live API serves quiz SignalR timers. Make it configurable, default **1**:

```csharp
// InfrastructureServiceExtensions.cs ~line 207
services.AddHangfireServer(opts =>
{
    opts.SchedulePollingInterval = TimeSpan.FromSeconds(1);
    opts.WorkerCount = configuration.GetValue("Hangfire:WorkerCount", 1); // was default 10 on 2 vCPU
});
```

`WorkerCount=1` ⇒ at most one transcode at a time. The other jobs (auto-grade, auto-forfeit) are tiny and tolerate serial execution.

### 2.2 Throttle ffmpeg itself — **launch-blocking before the first prod video**

`FfmpegMediaTranscoder.cs:43` runs `libx264` with no thread cap, no preset, no priority. Add to the arg list (right after `"-nostdin", "-y"`):

```csharp
"-threads", "1",            // don't peg both cores from a single transcode
"-preset", "veryfast",      // ~half the CPU-seconds, tolerable size delta on single-rendition VOD
```

Optionally drop priority after `process.Start()`: `process.PriorityClass = ProcessPriorityClass.BelowNormal;` (or wrap the entrypoint in `nice -n 10`) so request threads always win the scheduler.

### 2.3 Partition the auth rate limiter — **fix before real users**

The "auth" limiter is a **single global fixed-window bucket** (`Program.cs:~130`, `AddFixedWindowLimiter`, no partition). `PermitLimit=10/min` is therefore **platform-wide** — at any real scale, 10 logins/min across *all* users is a self-inflicted DoS. Partition by client IP (or username) before shipping the tight limit:

```csharp
options.AddPolicy("auth", httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
```

Do **not** ship `RateLimiting__AuthPermitLimit=10` against the un-partitioned bucket.

### 2.4 (Recommended, fast-follow) Real `/readyz`

`/readyz` is a **stub that always returns ready** (`HealthEndpoints.cs`) — it never probes Postgres/Redis. Until it does, **use `/healthz` for all Traefik/Dokploy probes and the restore smoke test** (this plan does). Optionally wire real `AddCheck`s (Npgsql + Redis) tagged `ready`, or extend `/readyz` to ping the DB. Not launch-blocking; just don't trust `/readyz` to mean "dependencies up".

---

## 3. Infrastructure, Dokploy & Kubernetes

### 3.1 Kubernetes vs Dokploy/Swarm — the honest call

| Factor | Kubernetes on this box | Dokploy/Swarm (current) |
|---|---|---|
| Control-plane cost | etcd + apiserver + scheduler + kubelet ≈ **0.5–1 vCPU + 1–1.5 GB** before any workload (k3s trims to ~0.5 GB, still real) | Traefik + Dokploy agent ≈ 250–350 MB, negligible CPU |
| HA | **Single node = zero HA regardless** — all the complexity, none of the resilience | Same single-node reality, honest about it (snapshots + fast redeploy) |
| Operator surface (solo dev) | ingress, cert-manager, CNI, PVCs, RBAC, Helm, kubectl, version-skew upgrades | add an Application, paste a domain, set env — TLS automatic |
| The actual bottleneck | spends scarce CPU on orchestration | all CPU goes to the ffmpeg transcode that needs it |

**Verdict: stay on Dokploy.** K8s buys nothing on one 2-vCPU node and taxes the one resource you're short on.

**Satisfy the k8s curiosity safely (never on prod):**
- **Cheapest real cluster:** a throwaway second VPS (~$5/mo) + `curl -sfL https://get.k3s.io | sh -`, deploy a copy with Helm, tear down when done.
- **Free + local:** `kind`/`minikube` on your dev machine against the same images you already build.
- **When it's justified:** if you outgrow one box, jump straight to a **managed** control plane (DOKS/Civo/Hetzner+CAPI) so you never hand-run etcd. Multi-node managed is where k8s earns its keep — not before.

### 3.2 Resource budget (both full stacks on 8 GB / 2 vCPU)

**RAM (idle):** OS ~600 MB · Dokploy+Traefik ~300 MB · Dokploy's own Postgres+Redis ~200 MB · prod Postgres 350 + Redis 128 · staging Postgres 200 + Redis 48 · prod API 400 · staging API 300 · 4× nginx 64 ⇒ **≈ 2.6 GB of 8 GB.** RAM is not the constraint.

**CPU is.** A single `libx264` transcode at default preset saturates both vCPUs and ffmpeg transiently uses 300–700 MB. **Mandatory mitigations:**

1. §2.1 Hangfire `WorkerCount=1` + §2.2 ffmpeg `-threads 1 -preset veryfast` (code).
2. **Cap the staging API at `--cpus 0.5`** in Dokploy so staging can never steal cycles from a prod transcode; optionally cap the prod API at `--cpus 1.5` to leave headroom for Postgres/Redis.
3. **Add 4 GB swap** (safety net for transcode/build spikes — you have 100 GB):
   ```bash
   fallocate -l 4G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
   echo '/swapfile none swap sw 0 0' >> /etc/fstab && sysctl -w vm.swappiness=10
   ```
4. **Never build on the box** — Angular/.NET builds run in GitHub Actions (§8).

**Upgrade trigger → KVM 4 (4 vCPU / 16 GB):** when concurrent students × upload frequency means a transcode is *usually* running during peak hours. Until then, KVM 2 + these mitigations is the correct, cost-aware choice.

### 3.3 Dokploy concepts you'll touch

- **Project** → logical group. Create exactly two: `Salah Bahzad`, `Salah Bahzad Staging`.
- **Application** → deployable unit. Source = **Docker image** (GHCR, recommended) or build-from-Dockerfile. We push prebuilt images from CI.
- **Database** → managed Postgres/Redis with a volume, internal-only network, and a **Backups** tab (scheduled `pg_dump` → S3/R2).
- **Domains** → attach a hostname; Dokploy writes Traefik labels + provisions Let's Encrypt automatically (HTTPS-redirect toggle).
- **Environment / shared env** → per-app secrets; project-level shared vars for values common within one project (never shared across projects).

### 3.4 Service topology

```
Project "Salah Bahzad" (prod)        Project "Salah Bahzad Staging"
├─ postgres   (volume, AOF off)      ├─ postgres   (volume, smaller caps)
├─ redis      (appendonly yes)       ├─ redis      (ephemeral ok)
├─ api        (GHCR image + ffmpeg)  ├─ api        (--cpus 0.5)
├─ admin      (nginx, proxies /api)  ├─ admin
└─ student    (nginx, /api + /hubs)  └─ student
```

- **Postgres** backs both EF Core *and* Hangfire. Never publish 5432.
- **Redis** carries the SignalR backplane, quiz connection↔attempt map, playback handoff store, HybridCache L2, **and the JWT revocation list**. Enable light `appendonly yes` on **prod** so revocations survive a restart (staging can be ephemeral). Never publish 6379.
- **Do not deploy the Aspire AppHost or MinIO** — `SalahBahazad.AppHost` is dev-only orchestration; MinIO only emulates R2 locally. Prod/staging use real R2 via `R2__*`.

### 3.5 Domains, TLS, WebSockets, body limits

- **DNS:** A-records for `api/admin/app[.staging].mrsalahbahzad.com` + `panel` → `72.60.38.211`. **Propagate DNS and ensure port 80 is reachable BEFORE attaching a domain in Dokploy** — Traefik uses HTTP-01 (per-host) by default; a missing A-record fails issuance. (A wildcard cert would need DNS-01; not the default.)
- **Per Application:** add the Domain, set container port (API `8080`, nginx `80`), toggle **HTTPS redirect ON** → Dokploy provisions Let's Encrypt + Traefik labels.
- **WebSockets / SignalR (`/hubs/quiz`):** Traefik proxies WS upgrades transparently. The hub reads the JWT from the `access_token` query only on `/hubs/quiz` (`InfrastructureServiceExtensions.cs:164-170`); there is **no auto-reconnect** (a drop *is* the forfeit). The portal nginx `/hubs/` block sets the upgrade headers and a long read timeout (§5.2).
- **Sticky sessions:** **unnecessary at 1 API replica** (the plan). The Redis backplane is wired (`Program.cs:88-91`) so correctness holds if you ever scale; only then add a Traefik sticky cookie on the API service.
- **Upload body limits:** admin uploads source videos (up to **2 GB**, `Program.cs:74-80`) through the **admin portal's nginx** (same-origin) → set `client_max_body_size 2200m;` there (§5.2). Traefik imposes **no** default body cap (it streams), so the edge is fine; the API's own 2 GB cap governs.
- **Forwarded headers:** the Dockerfile sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` so `Request.Scheme` is correct behind Traefik (TLS terminated at the edge) and `UseHttpsRedirection()` (`Program.cs:179`) doesn't misbehave. Do the HTTP→HTTPS upgrade at Traefik (`web`→`websecure`).

### 3.6 Firewall & exposure hardening

| Port | Allow | Notes |
|---|---|---|
| 22 SSH | **your IP(s) only** | key auth, password auth disabled |
| 80 | anywhere | ACME challenge + redirect to 443 |
| 443 | anywhere | all app traffic |

Block everything else inbound — **never publish 5432 / 6379**, MinIO/RedisInsight/pgAdmin (dev-only), or the Dokploy panel port. Postgres/Redis stay on the internal overlay; services reach them by name with no published port. Lock the **Dokploy panel** to `panel.*` behind a Traefik IP-allowlist (or SSH tunnel only) + 2FA + non-default admin. **There is no Hangfire dashboard in the code** (`UseHangfireDashboard` is absent) — keep it that way for launch (one less attack surface); observe jobs via JSON logs + the `hangfire.*` tables. Enable Hostinger's malware scanner + Ubuntu unattended-upgrades.

### 3.7 Backups & disaster recovery

1. **Postgres logical (primary):** Dokploy DB **Backups** tab → daily `pg_dump` of prod (weekly staging) → **Cloudflare R2** (S3 target you already have). Retain 14–30 days.
2. **R2 objects:** videos/HLS live in R2 (Cloudflare-durable). Nothing critical on the VPS disk (transcode scratch in `/tmp` is transient). Optionally enable R2 versioning/lifecycle.
3. **Hostinger VPS snapshots:** whole-box image, 4 retained — the "node died" lever. **Take one before every prod migration.**
4. **Secrets backup (don't skip):** Dokploy stores env on the box — a VPS loss loses your secrets. Export each env to an encrypted store (1Password / `age`-encrypted file in a private repo): `Jwt__Secret`, `Device__SigningKey`, `Firebase__ServiceAccountJson`, R2 keys, DB/Redis passwords.

**Restore drill (run once, record timings):** new box → install Dokploy → recreate 2 projects → recreate DB services → `pg_restore` latest dump from R2 → re-enter env from the encrypted backup → deploy images from GHCR → smoke test `GET https://api.mrsalahbahzad.com/healthz`.

---

## 4. Bring-up runbook (first deploy, in dependency order)

1. **DNS + box:** point all A-records → `72.60.38.211`; lock SSH + firewall (§3.6); add swap (§3.2).
2. **Docker log rotation** (once, so logs don't fill 100 GB): `/etc/docker/daemon.json` → `{"log-driver":"json-file","log-opts":{"max-size":"20m","max-file":"5"}}`, restart docker.
3. **Secure the Dokploy panel** (domain + IP allowlist + 2FA).
4. **Configure GHCR pull credentials in Dokploy** (Registry settings) — **before** any Application points at a private GHCR image, or the first deploy fails on image pull. (Alternatively make the GHCR packages public.)
5. **Create projects** `Salah Bahzad` + `Salah Bahzad Staging`.
6. **Per project:** create **Postgres** + **Redis** Database services (volumes; prod Redis `appendonly yes`). Note internal hostnames; set passwords.
7. **External services** (§7): create R2 buckets + scoped tokens + CORS; per-env Firebase service-account JSON; per-env Google "Desktop app" OAuth client; generate `Jwt__Secret` + `Device__SigningKey`.
8. **Commit repo artifacts:** the §2 code changes, `backend/.dockerignore` + `backend/Dockerfile`, `frontend/.dockerignore` + per-app `frontend/apps/<app>/Dockerfile` + `nginx.conf`, and the CI workflow (§8).
9. **Push `staging` branch** → CI builds + pushes images to GHCR.
10. **Create Applications** (`api`, `admin`, `student`) in each project from the GHCR images; set env (§6.3 / §7.4) — **don't forget `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`**; set the API **Pre-Deploy Command** to run `efbundle` (§6.4); attach domains (HTTPS redirect on).
11. **Deploy staging end-to-end;** verify `/healthz`, an R2 upload→signed-URL round-trip (guards the silent `UnconfiguredFileStorage` fallback), a login (Firebase verify → JWT mint), a SignalR quiz, and a small video transcode (**watch `htop`** — confirm §2 mitigations hold).
12. **Configure Dokploy DB backups → R2;** export secrets to the encrypted store; take a Hostinger snapshot.
13. **Promote `staging` → `main`;** deploy prod (manual trigger + pre-deploy backup — §6.4/§10).

---

## 5. Frontend (two Angular portals)

### 5.1 How config works today (verified)

Both apps feed a `window.__SB_API_URL__` shim at bootstrap from the build-selected `environment.*.ts` (`admin-portal/src/main.ts:7`, `student-portal/src/main.ts:14-16`) and read it everywhere via `#apiUrl()` (e.g. `auth.store.ts:196`, `quiz-hub.client.ts:32`). Build-time env selection is Angular `fileReplacements` (`production`→`environment.prod.ts`, `staging`→`environment.staging.ts`). **Per-env Firebase web config is baked at build** (`app.config.ts:21`) — this is the one value that genuinely differs per env and can't be unified, which is why **staging and prod are different image builds**. `environment.prod.ts` ships `apiUrl: ''` (same-origin). Output is `dist/apps/<app>/browser` (Angular `application` builder). Node pinned by `.nvmrc` (v24.8.0); `.npmrc` sets `legacy-peer-deps=true` (**required** for `@angular/fire@20`).

### 5.2 Dockerfile + nginx (per app)

`frontend/.dockerignore`:
```
node_modules
dist
.nx/cache
**/.angular/cache
**/*.spec.ts
.git
Dockerfile*
```

`frontend/apps/<app>/Dockerfile` (build context = `frontend/`; `APP` ∈ {admin-portal, student-portal}, `NX_CONFIG` ∈ {production, staging}):
```dockerfile
# syntax=docker/dockerfile:1.7
FROM node:24-alpine AS build
WORKDIR /app
ARG APP=admin-portal
ARG NX_CONFIG=production
COPY package.json package-lock.json .npmrc nx.json tsconfig.base.json ./
RUN --mount=type=cache,target=/root/.npm npm ci         # .npmrc carries legacy-peer-deps=true
COPY . .
RUN npx nx build "$APP" --configuration="$NX_CONFIG" \
 && test -f "dist/apps/$APP/browser/index.html"

FROM nginx:1.27-alpine AS runtime
ARG APP=admin-portal
RUN rm -rf /usr/share/nginx/html/*
COPY apps/${APP}/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/apps/${APP}/browser /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

`frontend/apps/<app>/nginx.conf` — **same-origin**: proxies `/api` + `/hubs` to the internal API (the student app needs `/hubs`; harmless on admin). Set the API upstream via a variable + the embedded Docker resolver so nginx starts even if the API is briefly down:
```nginx
map $http_upgrade $connection_upgrade { default upgrade; '' close; }

server {
  listen 80;
  server_name _;
  root /usr/share/nginx/html;
  index index.html;

  resolver 127.0.0.11 valid=10s ipv6=off;
  set $api_upstream http://api:8080;        # ⚠ match the Dokploy internal DNS name (§1.2)

  add_header X-Content-Type-Options "nosniff" always;
  add_header X-Frame-Options "DENY" always;
  add_header Referrer-Policy "strict-origin-when-cross-origin" always;
  add_header Content-Security-Policy "default-src 'self'; img-src 'self' data: blob: https:; font-src 'self' https://fonts.gstatic.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; script-src 'self'; connect-src 'self' https://*.googleapis.com https://securetoken.googleapis.com https://identitytoolkit.googleapis.com wss: https:; frame-src https://*.firebaseapp.com https://accounts.google.com;" always;

  gzip on; gzip_comp_level 6; gzip_min_length 1024; gzip_vary on;
  gzip_types text/plain text/css application/javascript application/json application/manifest+json image/svg+xml application/wasm;

  location /api/ {
    proxy_pass $api_upstream;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_read_timeout 300s;
    client_max_body_size 2200m;             # admin source-video upload (API caps at 2 GB)
  }

  location /hubs/ {                          # SignalR QuizHub — wss end to end
    proxy_pass $api_upstream;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection $connection_upgrade;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_read_timeout 3600s; proxy_send_timeout 3600s; proxy_buffering off;
  }

  location = /healthz { access_log off; return 200 "ok\n"; }

  location ~* \.(?:js|css|woff2?|png|jpe?g|gif|svg|ico|webp|avif)$ {
    expires 1y; add_header Cache-Control "public, immutable"; try_files $uri =404;
  }
  location = /index.html { add_header Cache-Control "no-cache, no-store, must-revalidate"; }
  location / { try_files $uri $uri/ /index.html; }
}
```

> **Brotli:** `nginx:alpine` has gzip only (above). Fine for a solo deployment; Traefik can also compress at the edge.

### 5.3 CORS (set even though same-origin browsers won't use it)

The API has one credentialed CORS policy reading `Cors:AllowedOrigins` (`Program.cs:146-159`); `appsettings.{Production,Staging}.json` ship it **empty**, so set it via env exactly (scheme + host, **no trailing slash** — the #1 silent CORS failure). `AllowCredentials()` forbids `*`.

| Env | `Cors__AllowedOrigins__0` | `Cors__AllowedOrigins__1` |
|---|---|---|
| Production | `https://admin.mrsalahbahzad.com` | `https://app.mrsalahbahzad.com` |
| Staging | `https://admin.staging.mrsalahbahzad.com` | `https://app.staging.mrsalahbahzad.com` |

### 5.4 Post-deploy checks

```bash
curl -I https://admin.mrsalahbahzad.com/students                 # 200 text/html (SPA fallback)
curl -i https://app.mrsalahbahzad.com/api/app/version-status     # 200 JSON via nginx proxy
curl -i -H "Connection: Upgrade" -H "Upgrade: websocket" \
     https://app.mrsalahbahzad.com/hubs/quiz                     # handshake/401, NOT 404/502
```
In the browser: `window.__SB_API_URL__ === ''` in prod, Firebase popup uses the **prod** `authDomain`, and a student login sets a **first-party** `sb_device` cookie on `app.mrsalahbahzad.com`.

---

## 6. Backend (.NET 10 API + ffmpeg)

### 6.1 Verified facts (cited)

| Concern | Finding |
|---|---|
| JWT signing key | `Jwt:Secret` (env `Jwt__Secret`) — **required at boot, throws if missing** (`InfrastructureServiceExtensions.cs:139`, `JwtTokenService.cs:104`) |
| Device key | `Device:SigningKey` — optional, **falls back to `Jwt:Secret`** (`DeviceBindingService.cs:75`) → set a distinct value in prod |
| Health | `/healthz` (live) + `/readyz` (**stub, always ready**) — both anonymous (`HealthEndpoints.cs`). `MapDefaultEndpoints()` is never called. **Probe `/healthz`.** |
| R2 | `ForcePathStyle=true`, region `auto`, checksums `WHEN_REQUIRED`; needs all 4 of Endpoint/AccessKeyId/SecretAccessKey/BucketPrivate or it **silently** uses a throwing stub (`InfrastructureServiceExtensions.cs:226-249`) |
| Firebase | `Firebase__ServiceAccountJson` (else ADC), `Firebase__WebApiKey` **required unless `Firebase__Disable=true`** |
| Serilog | JSON → stdout in prod; OTLP only when `OTEL_EXPORTER_OTLP_ENDPOINT` set (else no-op) |
| Hangfire | Postgres-backed, in-process, **no `WorkerCount`** (→ §2.1); schema self-creates on first boot |
| ffmpeg | shells out to `Transcode:FfmpegPath` (+ derived `ffprobe`); scratch in `/tmp/hls-{videoId}`, cleaned after (`VideoTranscodeJob.cs:35,83`) |
| Uploads | Kestrel cap = 2 GB + 32 MB; override `Uploads__MaxRequestBodyBytes` |
| Migrations | **no `Migrate()` on boot** — gated (`NFR-AVAIL-004`); migrations in the Infrastructure assembly, startup project = Api |

> The AppHost is **not** referenced by Api, so it never compiles into the image. Publishing `SalahBahazad.Api` pulls in Infrastructure/Application/Domain/ServiceDefaults only.

### 6.2 Dockerfile (`backend/Dockerfile`, context = `backend/`)

ffmpeg is installed into the runtime image (**this is "containerising ffmpeg"**); the EF bundle is baked in for the gated migration step (§6.4).

```dockerfile
# syntax=docker/dockerfile:1
# ---- build + publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/SalahBahazad.Api/SalahBahazad.Api.csproj                         src/SalahBahazad.Api/
COPY src/SalahBahazad.Infrastructure/SalahBahazad.Infrastructure.csproj   src/SalahBahazad.Infrastructure/
COPY src/SalahBahazad.Application/SalahBahazad.Application.csproj          src/SalahBahazad.Application/
COPY src/SalahBahazad.Domain/SalahBahazad.Domain.csproj                   src/SalahBahazad.Domain/
COPY src/SalahBahazad.ServiceDefaults/SalahBahazad.ServiceDefaults.csproj src/SalahBahazad.ServiceDefaults/
RUN dotnet restore src/SalahBahazad.Api/SalahBahazad.Api.csproj           # Api graph only — no AppHost
COPY src/ src/
RUN dotnet publish src/SalahBahazad.Api/SalahBahazad.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false
# EF migrations bundle (gated deploy step) — baked into the same image
RUN dotnet tool install --global dotnet-ef --version 10.0.* \
 && export PATH="$PATH:/root/.dotnet/tools" \
 && dotnet ef migrations bundle \
      --project src/SalahBahazad.Infrastructure --startup-project src/SalahBahazad.Api \
      --configuration Release -o /app/publish/efbundle

# ---- runtime (aspnet + ffmpeg) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg \
 && rm -rf /var/lib/apt/lists/* && ffmpeg -version | head -n1
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    Transcode__FfmpegPath=/usr/bin/ffmpeg \
    DOTNET_gcServer=0
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "SalahBahazad.Api.dll"]
```

`backend/.dockerignore`:
```
**/bin/
**/obj/
**/.vs/
**/*.user
tests/
src/SalahBahazad.AppHost/
**/appsettings.Development.json
**/*.md
```

> No Dockerfile `HEALTHCHECK` (the aspnet image has no `curl`) — Traefik/Dokploy probe `/healthz` instead. `DOTNET_gcServer=0` keeps the RAM budget honest. Run as the non-root `app` user; `/tmp` (transcode scratch) is world-writable.

### 6.3 Aspire (dev) → prod env mapping, and the full matrix

The AppHost injects env vars the API already reads; prod supplies the same keys pointing at real services — **no code difference**.

| AppHost (dev) | Env var(s) | Production source |
|---|---|---|
| `AddPostgres(...).AddDatabase("DefaultConnection")` | `ConnectionStrings__DefaultConnection` | Dokploy Postgres service |
| `AddRedis("redis")` | `ConnectionStrings__redis` | Dokploy Redis service |
| MinIO container | `R2__Endpoint/AccessKeyId/SecretAccessKey/BucketPrivate` | real Cloudflare R2 |
| `ResolveFfmpeg()` | `Transcode__FfmpegPath` | baked `/usr/bin/ffmpeg` |
| env name | `ASPNETCORE_ENVIRONMENT` | `Production` / `Staging` |

**Full matrix** (`__`=nesting, `__0`=array; secret 🔒):

| Variable | Prod example | Secret |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` / `Staging` | |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` (in Dockerfile) | |
| `ConnectionStrings__DefaultConnection` | `Host=postgres;Port=5432;Database=salahbahzad;Username=sb_app;Password=…` | 🔒 |
| `ConnectionStrings__redis` | `redis:6379` (+`,password=…` if set) | 🔒 |
| `Jwt__Secret` | `openssl rand -base64 64` | 🔒 |
| `Jwt__Issuer` / `Jwt__Audience` | `salah-bahzad-api` / `salah-bahzad-admin` | |
| `Jwt__AccessTokenMinutes` / `Jwt__RefreshTokenDays` | `15` / `7` | |
| `Device__SigningKey` | distinct `openssl rand -base64 64` | 🔒 |
| `Firebase__ServiceAccountJson` | one-line service-account JSON | 🔒 |
| `Firebase__WebApiKey` | per-env web key | (client) |
| `R2__Endpoint` | `https://<ACCT>.r2.cloudflarestorage.com` | |
| `R2__AccessKeyId` / `R2__SecretAccessKey` | R2 token pair | 🔒 |
| `R2__BucketPrivate` | `sb-prod-private` | |
| `Cors__AllowedOrigins__0/__1` | admin + app origins (§5.3) | |
| `Transcode__FfmpegPath` | `/usr/bin/ffmpeg` | |
| `Hangfire__WorkerCount` | `1` (after §2.1) | |
| `OTEL_EXPORTER_OTLP_ENDPOINT` / `_HEADERS` | unset, or free SaaS | headers 🔒 |
| `AppVersions__Platforms__{android,ios,windows,macos}__{MinVersion,LatestVersion,StoreUrl}` | per release (§9/§10) | |

### 6.4 Migrations — one gated mechanism

The `efbundle` is baked into the API image (§6.2). It is idempotent (applies only pending migrations) and runs on the aspnet runtime.

- **Run it as the Dokploy API "Pre-Deploy Command"** so it executes in the freshly pulled image *before* the new container takes traffic, aborting the deploy on failure:
  ```bash
  /app/efbundle --connection "$ConnectionStrings__DefaultConnection"
  ```
- **Staging:** auto-deploy on push to `staging` — the bundle runs automatically (staging is disposable).
- **Production:** **disable auto-deploy** on `main`. A prod deploy is a **deliberate manual trigger** (honors `NFR-AVAIL-004`); **take a Postgres backup + Hostinger snapshot first** (§3.7), then trigger — the Pre-Deploy bundle runs as part of that deliberate deploy.
- **First deploy / recovery (manual one-off, no SDK on the box):**
  ```bash
  docker run --rm --network <dokploy-prod-network> ghcr.io/<owner>/<repo>/api:prod \
    /app/efbundle --connection "Host=postgres;Port=5432;Database=salahbahzad;Username=sb_app;Password=$PGPASS"
  ```

> Hangfire's own `hangfire.*` tables self-create on first boot (`PrepareSchemaIfNecessary`) — not part of the EF bundle, and idempotent.

### 6.5 Operations

- **Probes:** Traefik/Dokploy HTTP health check → `/healthz` (interval 10s). Do not gate on `/readyz` until §2.4.
- **Graceful shutdown:** transcodes can exceed the 30s default drain. Raise the Dokploy/Swarm `stop-grace-period` to ~60s; interrupted transcodes are safe (Hangfire cancels via the token and retries — fresh `/tmp/hls-{id}` each run).
- **Logs:** Serilog JSON → stdout → Dokploy logs (no extra wiring); rotation set in §4 step 2.
- **OpenTelemetry:** **don't self-host a collector** on this box. Phase 1: leave `OTEL_EXPORTER_OTLP_ENDPOINT` unset (logs only). Phase 2: export to a **free SaaS OTLP tier** (Grafana Cloud / Honeycomb) — no local RAM cost.
- **ffmpeg resourcing:** §2.1 + §2.2 are the controls; cap the API CPU in Dokploy (§3.2); `/tmp` scratch is a few GB at `WorkerCount=1`, trivially within 100 GB.

---

## 7. External services & secrets

### 7.1 Firebase — keep the three projects (already in the repo)

| Env | `projectId` | Evidence |
|---|---|---|
| dev | `salah-bahzad-development` | `admin-portal/src/environments/environment.ts` |
| staging | `salah-bahzad-staging` | `environment.staging.ts` |
| prod | `salah-bahzad` | `environment.prod.ts` |

**Why separate:** Firebase Auth users live inside the project, and every Staff/Student row keys on `FirebaseUid` (`Staff.cs:14`, `Student.cs:17`). A shared pool would let a staging account authenticate against prod data via the same UID. Separate = clean user isolation, independent quotas, one-env blast radius.

**Per env, do this in the Console:**
1. **Auth → Sign-in method:** enable **Email/Password** + **Google**.
2. **Auth → Settings → Authorized domains:** add `admin.mrsalahbahzad.com` + `app.mrsalahbahzad.com` (staging: the `.staging.` variants). `localhost` + `*.firebaseapp.com` are pre-authorized.
3. **Project Settings → Service accounts → Generate new private key** → this JSON is the **server** secret. Minify and inject:
   ```bash
   jq -c . prod-firebase-adminsdk.json    # paste result as Firebase__ServiceAccountJson in Dokploy
   ```
   `GoogleCredential.FromJson` parses the embedded `\n` in `private_key` correctly.
4. **Google Cloud Console → Credentials → Create OAuth client ID → Desktop app** (per env) → records `GOOGLE_DESKTOP_CLIENT_ID` / `_SECRET` for the Windows Flutter build (the "secret" is an installed-app pseudo-secret; PKCE protects it — still keep it in CI secrets, never committed).

**Client config:** the portals' per-env Firebase web config (4 committed `environment.*.ts` files) is **public** and selected at build time. The Flutter app needs **per-env, per-platform** config for store builds: `google-services.json` (Android flavor dirs), `GoogleService-Info.plist` (iOS/macOS), and `firebase_options_<env>.dart` (`flutterfire configure --project=<projectId>`), selected via Flutter **flavors + `--dart-define`** (the app is already `String.fromEnvironment`-driven). Today `app/lib/firebase_options.dart` is hardcoded to **dev** — wire the flavor switch before store releases.

### 7.2 Cloudflare R2

**Separate private bucket + scoped token per env** (one Cloudflare account is fine — per-bucket token scoping isolates the blast radius):

1. **R2 → Create bucket:** `sb-prod-private`, `sb-staging-private` (location hint EU). **Keep private** — no public access, no `r2.dev` URL, no custom domain. The platform issues signed GET URLs (300s TTL); a public bucket is **not** needed.
2. **R2 → Manage API Tokens → Create:** **Object Read & Write**, **scoped to that one bucket**. Yields `R2__AccessKeyId` / `R2__SecretAccessKey` and the endpoint:
   ```
   R2__Endpoint = https://<ACCOUNT_ID>.r2.cloudflarestorage.com    # bucket selected by R2__BucketPrivate (path-style)
   ```
3. **Bucket CORS** (Settings → CORS Policy) — needed for HLS range requests from the portal/player:
   ```json
   [{ "AllowedOrigins": ["https://admin.mrsalahbahzad.com","https://app.mrsalahbahzad.com","salah-bahazad://stream"],
      "AllowedMethods": ["GET","HEAD"],
      "AllowedHeaders": ["Range","If-Range","Authorization"],
      "ExposeHeaders": ["Content-Length","Content-Range","Accept-Ranges","ETag"],
      "MaxAgeSeconds": 3600 }]
   ```
   (staging bucket → `.staging.` origins). Read-only because all writes are server-side.
4. **Lifecycle:** **defer aggressive delete rules** until the transcode pipeline's exact key prefixes are confirmed at wiring time (so a rule never reaps a key the player still needs); start at 30 days or disabled.

> The SDK config (`ForcePathStyle`, region `auto`, checksums `WHEN_REQUIRED`) is already correct in code — **don't change it.** A **missing R2 var is a silent failure** (throwing stub, app still boots) → keep an R2 upload→signed-URL smoke test in the deploy checklist.

### 7.3 Secrets inventory (where each lives)

| Secret / config | 🔒 | Used by | Stored | Injected |
|---|---|---|---|---|
| DB / Redis conn strings | 🔒 | API | Dokploy (api) | env |
| `R2__AccessKeyId` / `SecretAccessKey` | 🔒 | API S3 | Dokploy ← Cloudflare token | env |
| `R2__Endpoint` / `BucketPrivate` | | API S3 | Dokploy | env |
| `Firebase__ServiceAccountJson` | 🔒🔒 | Admin SDK | Dokploy ← Firebase | env (one-line) |
| `Firebase__WebApiKey` | (client) | REST | Dokploy | env |
| `Jwt__Secret` / `Device__SigningKey` | 🔒🔒 | JWT / device HMAC | Dokploy | env (distinct values) |
| Firebase web config (portals) | | SPAs | committed `environment.*.ts` | build-time |
| Android keystore + `key.properties` | 🔒 | CI app build | GitHub Actions secrets | CI only |
| iOS dist cert + profile | 🔒 | CI | GitHub Actions secrets | CI only |
| macOS MAS cert + installer cert + profile + ASC `.p8` | 🔒 | CI | GitHub Actions secrets | CI only |
| `GOOGLE_DESKTOP_CLIENT_ID/_SECRET` | sens. | Win OAuth | GitHub Actions secrets | `--dart-define` |
| Flutter prod `google-services.json` / `.plist` | sens. | app builds | GitHub Actions secrets (base64) | written in CI |
| `SENTRY_DSN` | low | crash | GitHub Actions secrets | `--dart-define` |

**`.gitignore` gaps to close** (none of these should ever be committed): `**/serviceAccount*.json`, `**/*-firebase-adminsdk-*.json`, `*.env*`, `**/key.properties`, `**/*.jks`, `**/*.keystore`, `**/*.p12`, `**/*.pfx`, `**/*.mobileprovision`, `**/*.p8`, and the **prod** Flutter Firebase files (`app/android/app/src/prod/google-services.json`, `app/{ios,macos}/config/prod/GoogleService-Info.plist`). The already-tracked `firebase_options.dart` / `environment*.ts` hold **client** config (public-safe) — leave them, but never add a real service-account JSON / `Jwt__Secret` / R2 secret to them.

**Rotation:** `Jwt__Secret` → new key + redeploy (all tokens invalidate; users re-login) — set `Device__SigningKey` distinctly so it doesn't cascade. R2 token / Firebase SA / DB passwords → create new, update env, redeploy, revoke old.

### 7.4 Consolidated per-env backend env

**Production** (`api` service):
```ini
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=salahbahzad;Username=sb_app;Password=__SET__   # 🔒
ConnectionStrings__redis=redis:6379                                                                                  # 🔒 if password set
R2__Endpoint=https://<ACCOUNT_ID>.r2.cloudflarestorage.com
R2__AccessKeyId=__PROD_R2_KEY__            # 🔒
R2__SecretAccessKey=__PROD_R2_SECRET__     # 🔒
R2__BucketPrivate=sb-prod-private
Firebase__ServiceAccountJson={"type":"service_account","project_id":"salah-bahzad",...}   # 🔒 one line
Firebase__WebApiKey=__PROD_WEB_KEY__       # from environment.prod.ts
Jwt__Issuer=salah-bahzad-api
Jwt__Audience=salah-bahzad-admin
Jwt__AccessTokenMinutes=15
Jwt__RefreshTokenDays=7
Jwt__Secret=__64B__                        # 🔒 openssl rand -base64 64
Device__SigningKey=__DIFFERENT_64B__       # 🔒 distinct
Cors__AllowedOrigins__0=https://admin.mrsalahbahzad.com
Cors__AllowedOrigins__1=https://app.mrsalahbahzad.com
Transcode__FfmpegPath=/usr/bin/ffmpeg
Hangfire__WorkerCount=1
# AppVersions__Platforms__... (see §9/§10)
```
**Staging** — identical shape, different values: `ASPNETCORE_ENVIRONMENT=Staging`, `Database=salahbahzad` (separate instance), `R2__BucketPrivate=sb-staging-private`, `project_id":"salah-bahzad-staging"`, distinct `Jwt__Secret`/`Device__SigningKey`, `.staging.` CORS origins.

---

## 8. CI/CD — build off-box → GHCR → Dokploy

**Branch strategy:** `staging` → Staging project (auto-deploy); `main` → Production project (**manual** deploy + pre-deploy backup). Promote staging→main by PR.

```yaml
# .github/workflows/deploy.yml
name: build-and-deploy
on: { push: { branches: [main, staging] } }
permissions: { contents: read, packages: write }
jobs:
  build:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        include:
          - { name: api,     ctx: backend,  file: backend/Dockerfile,                    args: "" }
          - { name: admin,   ctx: frontend, file: frontend/apps/admin-portal/Dockerfile, args: "APP=admin-portal" }
          - { name: student, ctx: frontend, file: frontend/apps/student-portal/Dockerfile, args: "APP=student-portal" }
    steps:
      - uses: actions/checkout@v4
      - uses: docker/setup-buildx-action@v3
      - uses: docker/login-action@v3
        with: { registry: ghcr.io, username: ${{ github.actor }}, password: ${{ secrets.GITHUB_TOKEN }} }
      - id: cfg
        run: |
          echo "env=${{ github.ref_name == 'main' && 'prod' || 'stg' }}" >> $GITHUB_OUTPUT
          echo "ngcfg=${{ github.ref_name == 'main' && 'production' || 'staging' }}" >> $GITHUB_OUTPUT
      - uses: docker/build-push-action@v6
        with:
          context: ${{ matrix.ctx }}
          file: ${{ matrix.file }}
          push: true
          build-args: |
            ${{ matrix.args }}
            NX_CONFIG=${{ steps.cfg.outputs.ngcfg }}
          tags: |
            ghcr.io/${{ github.repository }}/${{ matrix.name }}:${{ steps.cfg.outputs.env }}
            ghcr.io/${{ github.repository }}/${{ matrix.name }}:${{ github.sha }}
          cache-from: type=gha
          cache-to: type=gha,mode=max
  deploy-staging:                       # prod deploy stays MANUAL (gated backup)
    needs: build
    if: github.ref_name == 'staging'
    runs-on: ubuntu-latest
    steps:
      - run: curl -fsS -X POST "${{ secrets.DOKPLOY_HOOK_STG }}"
```

Each Dokploy Application points at `…/<name>:prod` (or `:stg`). The staging webhook auto-deploys; **prod is triggered by hand** after a backup. (Configure the **GHCR pull secret in Dokploy first** — §4 step 4.)

---

## 9. App store distribution (Microsoft Store, Apple App Store iOS+macOS, Google Play)

Builds on `docs/IMPLEMENTATION-PLAN-native-app-a4-app.md` (already has Android AAB signing, iOS IPA, macOS notarize, Windows installer, and the GitHub Actions skeleton) and **extends it to the four stores**, with the two genuinely new targets called out.

### 9.1 Accounts & one-time prerequisites (USD, ~2026-06)

| Account | Cost | Covers | Lead time / gotchas |
|---|---|---|---|
| **Apple Developer Program** | $99/yr | iOS **and** Mac App Store (one membership) | Individual: same-day–48 h. Org needs D-U-N-S (1–2 wks). |
| **Google Play Developer** | $25 once | Android | ID verification 2–5 days; **new personal accounts need a closed test of ≥12 testers for 14 continuous days** before production — start day 1. |
| **Microsoft Store (Partner Center)** | **$0 for Individual** (Sept 2025 — fee waived, **no credit card**); Company still $99 once | Microsoft Store (MSIX) | **Register via the new individual flow at [storedeveloper.microsoft.com](https://storedeveloper.microsoft.com)** — personal Microsoft account + gov-ID/selfie verification, no payment. **Do NOT use the "Microsoft for your business" / company signup** (that path asks for a card and is the $99 company tier). Pick **Individual**; the type can't be changed in place later. Reserving the app name still **mints your MSIX identity** (`Identity Name`, `Publisher`, `Package Family Name`) — needed before you finalize the manifest. |

Record the frozen identifiers (§1.4) + Apple Team ID + the minted MSIX identity in an `app/README-release.md`.

### 9.2 Versioning — one pubspec, four formats

Single source = `app/pubspec.yaml` `version: <name>+<build>` (today `1.0.0+1`). **Build numbers must strictly increase per store, forever** (a withdrawn upload still burns the number on Apple/Microsoft).

| Store | Marketing | Build | Rule |
|---|---|---|---|
| Google Play | `versionName=1.0.0` | `versionCode=1` | `versionCode` increases each upload |
| App Store (iOS) | `CFBundleShortVersionString=1.0.0` | `CFBundleVersion=1` | increases within a marketing version |
| Mac App Store | same | same | **separate** upload counter from iOS |
| Microsoft Store | `1.0.0` | `1.0.0.0` | MSIX 4-part; **Revision (4th) = 0** |

### 9.3 Google Play (Android) — A4 + console workflow

A4 already builds a Play-App-Signing-ready signed AAB. Add: create app (`com.salahbahzad.secure_player`) → **enrol in Play App Signing** (Google holds the signing key; your A4 keystore is the *upload* key) → **Internal testing** (smoke-test the real artifact: deep-link redeem → playback → the 426 forced-update path) → **Closed test (≥12 testers, 14 days)** → **Production (staged 10→50→100%)**. Block-on forms: **Data safety** (email+name collected & linked for functionality, crash anonymous, no sale), **Content rating**, **Target audience**, **App access** (the app is useless without a backend handoff — **attach reviewer test credentials + a sample deep link**). storeUrl: `https://play.google.com/store/apps/details?id=com.salahbahzad.secure_player`.

### 9.4 Apple App Store (iOS) — A4 IPA + App Store Connect

A4 builds a signed IPA (`method=app-store`). Add: App Store Connect record (`com.salahbahzad.securePlayer`, mints the numeric App ID) → upload via Transporter → **TestFlight**. **The #1 rejection risk (Guideline 2.1):** the reviewer can't get past the splash without a backend handoff — provide a **working `salah-bahazad://stream?…` deep link or a screen recording** + notes explaining it's a B2B companion to an enrolled-student portal. Privacy nutrition labels (email+name linked for functionality; crash not linked). Confirm **`PrivacyInfo.xcprivacy`** declares the Keychain required-reason API for `flutter_secure_storage`. **ATT not needed** — don't add it. storeUrl: `https://apps.apple.com/app/id<IOS_APP_ID>`.

### 9.5 Mac App Store (macOS) — **the extension beyond A4's Developer-ID**

**Good news:** the macOS app is **already App-Sandboxed** (`app/macos/Runner/Release.entitlements`: `app-sandbox`, `network.client`, `keychain-access-groups`) and the capture shim uses the **public** `NSWindow.sharingType = .none` (`MainFlutterWindow.swift:36`) — sandbox-safe, no DRM/private API. So MAS is a **signing + provisioning** job, not a re-architecture.

| Aspect | A4 (Developer-ID, direct) | **Mac App Store** |
|---|---|---|
| Signing | "Developer ID Application" | **"Apple Distribution"** + **"3rd Party Mac Developer Installer"** |
| Provisioning | none | **MAS provisioning profile** embedded |
| Packaging | `.app`/`.dmg`, notarize + staple | **`.pkg`** via `productbuild`, uploaded to App Store Connect (App Review, **no notarization**) |
| Delivery | you host | Apple hosts → `apps.apple.com` |

Changes: create **Apple Distribution** + **MAS installer** certs and a **MAS provisioning profile** for `com.salahbahzad.securePlayer`; keep Release entitlements sandbox-only (the `network.server` entitlement stays debug-only — correct). Use a **separate macOS App Store Connect record** (clean per-platform metadata + its own storeUrl → `AppVersions.macos`). Build/sign/package:
```bash
flutter build macos --release \
  --dart-define=API_BASE_URL=https://api.mrsalahbahzad.com \
  --dart-define=APP_VERSION=1.0.0 --dart-define=PORTAL_URL=https://app.mrsalahbahzad.com \
  --dart-define=SENTRY_DSN="$SENTRY_DSN" --dart-define=SENTRY_ENV=production
APP="build/macos/Build/Products/Release/secure_player.app"
cp "$MAS_PROVISION_PROFILE" "$APP/Contents/embedded.provisionprofile"
codesign --force --deep --options runtime --entitlements macos/Runner/Release.entitlements \
  --sign "Apple Distribution: <NAME> (<TEAMID>)" "$APP"
productbuild --component "$APP" /Applications \
  --sign "3rd Party Mac Developer Installer: <NAME> (<TEAMID>)" build/secure_player_mas.pkg
xcrun altool --upload-app -f build/secure_player_mas.pkg -t macos --apiKey "$ASC_KEY_ID" --apiIssuer "$ASC_ISSUER_ID"
```
In review notes, explain the screen-recording exclusion is legitimate content protection via a public API. storeUrl: `https://apps.apple.com/app/id<MAC_APP_ID>`.

### 9.6 Microsoft Store (Windows) — **the extension beyond A4's Inno Setup**

| Aspect | A4 (Inno Setup, direct) | **Microsoft Store** |
|---|---|---|
| Package | `.exe` installer | **`.msix`** via the `msix` Flutter package |
| Signing | your Authenticode cert + SmartScreen friction | **Store signs it** — no cert to buy/manage |
| Identity | free-form | **Package Family Name from Partner Center** in the manifest |
| URI scheme | installer writes registry keys | **`<uap:Protocol Name="salah-bahazad">`** in the manifest |
| Updates | manual reinstall | **Store auto-update** |

Setup: reserve the name in Partner Center (copy the 4 identity values) → add `msix` dev-dep + config to `pubspec.yaml`:
```yaml
dev_dependencies: { msix: ^3.16.7 }
msix_config:
  display_name: Salah Bahzad Secure Player
  publisher_display_name: <from Partner Center>
  identity_name: <from Partner Center>
  publisher: <CN=... from Partner Center>
  msix_version: 1.0.0.0           # keep in sync with pubspec at every bump
  logo_path: assets/brand/logo-small.png
  capabilities: internetClient
  store: true                     # Store-managed signing — do NOT self-sign
  protocol_activation: salah-bahazad
```
Build: `flutter build windows --release --dart-define=…` then `dart run msix:create --store`. **Verify** the generated `AppxManifest.xml` contains `<uap:Protocol>` and that a test `salah-bahazad://stream?…` link activates the MSIX-installed app (MSIX protocol activation differs from a classic registry handler). Submit in Partner Center (cert hours–3 days). storeUrl: prefer `ms-windows-store://pdp/?productid=<ProductId>` (the in-app Update button lands in the Store app where the update happens).

> **Recommendation:** make MSIX/Microsoft Store the **primary** Windows channel (removes the Authenticode cost + SmartScreen problem); keep A4's Inno Setup as an optional fallback.

### 9.7 CI — extend A4's workflow

Apple targets need `macos-latest`, MSIX needs `windows-latest`, Android stays `ubuntu-latest`. Add: a **`build-msix`** job (`dart run msix:create --store`), a **`build-mas`** job (`productbuild` + `altool --upload-app`, distinct from A4's notarize job), tag trigger `app-v*`, and store-upload steps (`r0adkll/upload-google-play` track `internal`; App Store Connect API key for iOS/macOS). New secrets: `MACOS_MAS_CERT_P12_B64`, `MACOS_MAS_INSTALLER_CERT_P12_B64`, `MAS_PROVISION_PROFILE_B64`, `ASC_KEY_ID`, `ASC_ISSUER_ID`, `ASC_KEY_B64`, `PLAY_SERVICE_ACCOUNT_JSON`. MSIX needs **no** signing secret (Partner Center signs).

### 9.8 Auto-update — two layers (the gate is already built)

- **Layer 1 — store auto-update:** once live, each store updates installed apps in the background. You just publish a higher build number.
- **Layer 2 — in-app version gate (already in the backend):** on launch the app calls `GET /api/app/version-status?platform=&version=` (anonymous, `AppEndpoints.cs:23`). `requested < LatestVersion` → `update_available` → dismissible soft-nudge banner. `requested < MinVersion` → `update_required`, **and** any `redeem` throws **HTTP 426 `outdated_app`** (`UpgradeRequiredException`) with `storeUrl` in `ProblemDetails.Detail` → un-dismissable "Update required" screen.

The control surface is the **`AppVersions` config**: `LatestVersion` drives the nudge, `MinVersion` drives the forced 426. The handler reads `IOptionsMonitor`, but **env-var changes need a container restart** (ASP.NET only file-watches `appsettings.*.json`, not env). Two modes:
- **(A) default:** set `AppVersions__…` env in Dokploy → change = quick API restart (~10–20 s, atomic). Recommended.
- **(B) break-glass:** mount a writable `appsettings.AppVersions.json` as a volume → edit flips the gate live, no restart.

Fill `StoreUrl` once listings exist:
```
android__StoreUrl = https://play.google.com/store/apps/details?id=com.salahbahzad.secure_player
ios__StoreUrl     = https://apps.apple.com/app/id<IOS_APP_ID>
macos__StoreUrl   = https://apps.apple.com/app/id<MAC_APP_ID>
windows__StoreUrl = ms-windows-store://pdp/?productid=<ProductId>
```

---

## 10. Release runbook — "what to do when I release a new version"

**Backend/portal first** (so the contract the app depends on is live), **then the app.**

### 10A. Backend + portals (Dokploy)
```
[ ] 1. PR merged; CI green; images built + pushed to GHCR (tagged :stg/:prod + :sha).
[ ] 2. STAGING (auto-deploys on push to `staging`): Pre-Deploy efbundle runs migrations,
       new api/admin/student roll. Smoke-test: login, redeem→playback, and
       curl '.../api/app/version-status?platform=android&version=0.0.1' → update_required
       curl '.../api/app/version-status?platform=android&version=1.0.0' → ok
[ ] 3. PRODUCTION (MANUAL — NFR-AVAIL-004):
       a. Take a Postgres backup + Hostinger snapshot (rollback point).
       b. Trigger the prod deploy → Pre-Deploy efbundle migrates, then Traefik swaps containers.
       c. Smoke-test prod: /healthz, version-status checks, one real redeem.
```
**Rollback:** redeploy the previous `:sha` images (fast). Destructive migration → restore the pre-deploy snapshot/dump (this is why migrations are a separate, deliberate, backup-gated step).

### 10B. App (stores)
```
[ ] 1. Bump app/pubspec.yaml (e.g. 1.0.1+2); if MSIX, set msix_config.msix_version=1.0.1.0. Commit.
[ ] 2. Tag app-v1.0.1 → CI builds signed artifacts for all 4 stores.
[ ] 3. TEST tracks first: Play Internal, iOS TestFlight, macOS TestFlight/internal, Windows local .msix.
       Smoke-test each: deep-link redeem → playback, soft-nudge banner, forced-update 426.
[ ] 4. Submit: Play Internal→Closed→Production (staged); App Store + Mac App Store review
       (attach the demo deep-link/recording — dodges the 2.1 "can't test" rejection);
       Microsoft Store certification.
[ ] 5. WAIT until each store reports Available / Ready for Sale / In the Store.
[ ] 6. FLIP THE GATE per platform, only after that platform is live:
       set AppVersions__Platforms__<p>__LatestVersion=1.0.1 (+ StoreUrl if unset) → soft nudge.
       Never raise LatestVersion before the store has the build (would nudge to a 404).
       VERIFY: '...&version=1.0.0' → update_available pointing at the right storeUrl.
[ ] 7. ONLY IF MANDATORY (security/breaking), after 100% rollout:
       raise AppVersions__Platforms__<p>__MinVersion=1.0.1 → forces 426 on older builds.
```
**App rollback:** before the gate flip — just halt the staged rollout (the old version keeps working since you didn't raise Min/Latest). After a bad flip — **lower `LatestVersion`/`MinVersion` back** (instant). Bad build already live — stores don't roll back (numbers only go up); ship a fast-follow `1.0.2+3` and use `MinVersion` to skip the bad build once the fix is live.

---

## 11. Risk register

| Risk | Sev | Mitigation |
|---|---|---|
| ffmpeg `libx264` pins both vCPUs during live quiz traffic | **High** | §2.1 `WorkerCount=1` + §2.2 `-threads 1 -preset veryfast` + nice + staging CPU-cap (mandatory before first prod video) |
| Global auth rate-limiter throttles all logins to 10/min | **High** | §2.3 partition by IP before real users |
| Cross-origin would make `sb_device` a third-party cookie → Safari ITP blocks → device-binding silently breaks | **High** | §1.5 same-origin portals (nginx proxy) |
| Migration runs before a backup / on un-migrated schema | **High** | §6.4 prod = manual deploy + pre-deploy snapshot; efbundle as Pre-Deploy gate |
| Missing R2/Firebase var → silent throwing stub, app still boots | Med | §4 step 11 smoke test (upload→signed-URL; login) |
| First deploy fails on private GHCR pull | Med | §4 step 4 configure Dokploy pull secret first |
| Secrets lost with the VPS | Med | §3.7 encrypted off-box secrets backup |
| App Store/Mac App Store 2.1 "can't test without backend" rejection | Med | §9.4/§9.5 attach demo deep-link + notes |
| MSIX protocol activation differs from registry handler → deep link doesn't open | Med | §9.6 verify `<uap:Protocol>` + test activation before submit |
| 8 GB exhausted if you build on the box | Low | §8 build in CI only |

## 12. Confirmed decisions (2026-06-28)

All recommendations accepted by the operator:

1. **Domain = `mrsalahbahzad.com`** — confirmed. Apex + `www` reserved for the **future landing page** (§1.1); platform on subdomains.
2. **Stay on KVM 2** for launch, with the §2/§3.2 mitigations; upgrade to KVM 4 per the defined trigger.
3. **Land the §2 code changes** (Hangfire `WorkerCount`, ffmpeg `-threads/-preset`, partitioned auth rate limiter) before go-live.
4. **Two separate Apple App Store Connect records** (iOS + macOS).
5. **MSIX / Microsoft Store as the primary Windows channel**; Inno Setup demoted to optional fallback.
6. **OTel off** for launch (Serilog JSON logs only) → free SaaS OTLP tier later.
7. **Same-origin portal topology** (§1.5); staging `environment.staging.ts` set to `apiUrl: ''` for parity with prod.

---

### Source of truth & provenance
This plan reconciles five repo-grounded research passes (infra, backend, frontend, external services, app stores) and an adversarial completeness/resource review. It overrides the individual drafts wherever they conflicted (topology, hostnames, service/image names, migration mechanism) per §1. Requirement IDs cited: `NFR-AVAIL-004` (gated migrations), `NFR-SEC-002` (no committed secrets), `NFR-SEC-006` (rate limiting), `NFR-SCAL-002` (SignalR scale), `FR-APP-UPD-001` / `NFR-APP-DIST-*` (app distribution + update gate).
