# Non-Functional Requirements

Quality attributes and constraints that apply across both portals and the backend. Each is testable; targets are stated where meaningful. Portal-specific notes are called out inline.

> **Context that drives several of these:** the user base is predominantly **minors** (high-school students), and the platform stores **sensitive PII** — identification images, parent/guardian phone numbers, school, and location. Privacy and security requirements are sized accordingly.

## Contents

- [Security](#security)
- [Privacy & data protection](#privacy--data-protection)
- [Performance](#performance)
- [Scalability & multi-tenant readiness](#scalability--multi-tenant-readiness)
- [Availability, backup & disaster recovery](#availability-backup--disaster-recovery)
- [Observability & audit retention](#observability--audit-retention)
- [Compatibility](#compatibility)
- [Accessibility](#accessibility)
- [Maintainability, quality & delivery](#maintainability-quality--delivery)

---

## Security

| ID | Requirement | Target / Notes |
|---|---|---|
| NFR-SEC-001 | All traffic SHALL be HTTPS/TLS 1.2+; HSTS enabled. | No plaintext. |
| NFR-SEC-002 | Secrets (DB connection strings, signing keys, provider keys, DSNs) SHALL NOT be committed to source; they SHALL come from environment/secret store per environment. | Closes the current committed-secrets finding. |
| NFR-SEC-003 | Authorization SHALL be enforced server-side on every endpoint; no privileged action SHALL rely on UI gating alone. | Default-deny. |
| NFR-SEC-004 | User credentials SHALL be managed by Firebase Authentication — the platform SHALL store no passwords; platform session tokens SHALL be signed, short-lived, and support refresh + revocation. | Credential storage/reset delegated to Firebase. |
| NFR-SEC-005 | Real-time (SignalR) connections SHALL authenticate with the same token scheme as the REST API and SHALL authorise resource ownership; query-string credentials SHALL NOT be used. | Fixes hub auth inconsistency. |
| NFR-SEC-006 | The API SHALL apply rate limiting and lockout/backoff on auth, code-redemption, and video-access endpoints to resist brute force and abuse. | Anti-abuse. |
| NFR-SEC-007 | Input SHALL be validated server-side; the app SHALL be hardened against the OWASP Top 10 (injection, XSS, IDOR, SSRF, etc.). | IDOR is acute given Guid IDs in URLs. |
| NFR-SEC-008 | File uploads (ID images, thumbnails, materials, question images) SHALL be type/size-validated, stored in object storage (Cloudflare R2) — not on the app server disk/web root — served via authenticated short-lived signed URLs, and scanned where feasible. | No public hotlinking of PII. |
| NFR-SEC-009 | Video SHALL be delivered as signed, short-lived, AES-128-encrypted **HLS** from R2/CDN (no single downloadable file); the screenshot/recording **black-out** is delivered by the native apps via OS flags, **not DRM**. | See [05](05-secure-video-streaming-options.md) / [09](09-non-functional-app.md). |
| NFR-SEC-010 | Tenant isolation SHALL be verified by automated tests (a user of tenant A can never read/write tenant B). | Multi-tenant safety. |
| NFR-SEC-011 | Dependencies SHALL be scanned for known vulnerabilities in CI; the TLS-bypass override in the desktop client SHALL be removed. | Supply-chain + the prod cert-bypass finding. |

## Privacy & data protection

| ID | Requirement | Target / Notes |
|---|---|---|
| NFR-PRIV-001 | Collection SHALL be limited to what each feature needs; sensitive PII (ID image, parent numbers, location) SHALL be access-restricted to staff with a need and SHALL be audited when viewed. | Data minimisation. |
| NFR-PRIV-002 | PII SHALL be encrypted at rest; ID-verification images SHALL be stored in a restricted bucket, never publicly addressable. | Minors' data. |
| NFR-PRIV-003 | Terms acceptance SHALL be recorded with version + timestamp; a privacy policy appropriate to minors' data SHALL be presented at registration. | Consent provenance. |
| NFR-PRIV-004 | A data-retention policy SHALL define how long PII, audit logs, and assessment data are kept, and SHALL support deletion/anonymisation requests consistent with that policy and audit-integrity needs. | Retention vs. evidence balance. |
| NFR-PRIV-005 | Logs and crash reports SHALL NOT contain bearer tokens, passwords, or raw PII. | Fixes token-in-logs findings. |
| NFR-PRIV-006 | Data residency/processing-location expectations SHALL be documented and honoured (confirm jurisdiction; relevant given Egypt/Paymob context). | Confirm with owner. |

## Performance

| ID | Requirement | Target / Notes |
|---|---|---|
| NFR-PERF-001 | Interactive API reads (catalogue, lists, detail) SHALL respond < 300 ms p95 under expected load. | Server-side. |
| NFR-PERF-002 | Portal first-meaningful-paint SHALL be < 2.5 s on a typical broadband desktop; route transitions < 500 ms. | Perceived speed. |
| NFR-PERF-003 | The quiz countdown and assignment timer SHALL stay accurate to within ±1 s of server time, surviving brief network blips without unfair forfeits. | Fairness-critical. |
| NFR-PERF-004 | Video start (authorise → first frame) SHALL be < 4 s p95 on broadband. | Streaming UX. |
| NFR-PERF-005 | List endpoints SHALL paginate and SHALL avoid N+1 queries; heavy reports SHALL stream/export asynchronously. | — |

## Scalability & multi-tenant readiness

| ID | Requirement | Target / Notes |
|---|---|---|
| NFR-SCAL-001 | The architecture SHALL scale to many tenants and to the largest single tenant's peak (e.g. an exam-night spike of concurrent quizzes) without redesign. | Tenant-ready. |
| NFR-SCAL-002 | The API SHALL be horizontally scalable; any real-time/in-memory state (quiz session maps, timers) SHALL use a shared backplane (e.g. Redis) so multiple instances are correct. | Fixes per-process hub state. |
| NFR-SCAL-003 | Per-tenant data growth SHALL be isolated by `TenantId` indexing so one large tenant does not degrade others. | Index strategy. |
| NFR-SCAL-004 | Background work (auto-grade, notifications, exports) SHALL run outside the request path and be retry-safe. | Async jobs. |

## Availability, backup & disaster recovery

| ID | Requirement | Target / Notes |
|---|---|---|
| NFR-AVAIL-001 | Target availability SHALL be ≥ 99.5% monthly for the student-facing portal during business/study hours. | Set per business need. |
| NFR-AVAIL-002 | The database SHALL have automated daily backups with point-in-time recovery; restore SHALL be tested periodically. | Data safety. |
| NFR-AVAIL-003 | Defined RPO/RTO SHALL be documented (suggested RPO ≤ 24 h, RTO ≤ 4 h) and validated. | DR objectives. |
| NFR-AVAIL-004 | Database migrations SHALL be gated/reviewed and reversible; they SHALL NOT auto-apply unsupervised on every production boot. | Fixes migrate-on-boot risk. |
| NFR-AVAIL-005 | Uploaded files SHALL live on durable object storage (Cloudflare R2), not ephemeral container disk; the app server SHALL NOT be the file store. | Asset durability. |

## Observability & audit retention

| ID | Requirement | Target / Notes |
|---|---|---|
| NFR-OBS-001 | Structured logging, metrics, and distributed tracing SHALL be enabled in all deployed environments (not production-only). | Diagnosability. |
| NFR-OBS-002 | Errors SHALL be reported to a monitoring tool with environment/tenant context (no PII/secrets). | — |
| NFR-AUD-001 | The audit/activity log SHALL be **append-only and immutable** at the application layer (no update/delete API). | Tamper-evidence. |
| NFR-AUD-002 | Audit entries SHALL be retained for a defined minimum (suggested ≥ 24 months, longer for financial/code events) consistent with the privacy policy. | Dispute window. |
| NFR-AUD-003 | Audit writes SHALL be reliable: an audited action and its log entry SHALL commit atomically (or the action fails). | No silent gaps. |
| NFR-AUD-004 | Health/readiness endpoints SHALL exist in every environment for orchestration. | Ops. |

## Compatibility

| ID | Requirement | Target / Notes |
|---|---|---|
| NFR-COMPAT-001 | Both portals SHALL support current Chrome, Edge, Firefox, and Safari (latest two major versions). | Evergreen. |
| NFR-COMPAT-002 | Both portals SHALL be fully responsive and usable across phone, tablet, and desktop viewports; no feature SHALL be desktop-only. | Phone/tablet/desktop are first-class. |
| NFR-COMPAT-003 | The OS versions where protected playback (the app's capture black-out) is supported SHALL be documented (see [09](09-non-functional-app.md)); unsupported combinations SHALL show a clear message rather than play unprotected. | Black-out depends on OS support. |

## Accessibility

| ID | Requirement | Target / Notes |
|---|---|---|
| NFR-A11Y-001 | Both portals SHOULD target WCAG 2.1 AA: keyboard operability, focus order, colour contrast, labels/ARIA, error identification. | Inclusive. |
| NFR-A11Y-002 | Math/LaTeX content SHALL render with accessible markup where the rendering library supports it. | MathML/alt. |

## Maintainability, quality & delivery

| ID | Requirement | Target / Notes |
|---|---|---|
| NFR-MAINT-001 | Business rules SHALL have automated test coverage (unit + integration), especially enrollment, grading, quiz scoring/forfeit, code lifecycle, and tenant isolation. | Closes near-zero-coverage gap. |
| NFR-MAINT-002 | CI SHALL build, test, and scan on every change; CD SHALL deploy via a repeatable, reviewable pipeline. | No manual-only releases. |
| NFR-MAINT-003 | Configuration SHALL be environment-driven; no environment-specific values hard-coded in source. | 12-factor. |
| NFR-MAINT-004 | Code SHALL follow the established Clean Architecture/CQRS conventions; new naming SHALL avoid perpetuating existing typos (`Infrustructure`, `Activites`, `Pagtination`) in fresh modules. | Consistency. |
| NFR-MAINT-005 | Public APIs SHALL be documented (OpenAPI) in non-production environments; the admin rebuild SHALL ship with a component/design-system baseline. | Onboarding + the redesign. |

---

➡️ Next: [05 — Secure video streaming](05-secure-video-streaming-options.md)
