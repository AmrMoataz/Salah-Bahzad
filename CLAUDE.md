# Salah Bahzad — Monorepo Root

This is the repo root. Work happens in three areas:

- **`backend/`** — .NET 10 API (Clean Architecture + CQRS). **Read `backend/CLAUDE.md`** for all backend conventions, domain model, and business rules before changing backend code. Run `dotnet`/the .NET Claude Kit from inside `backend/`.
- **`frontend/`** — Angular apps (admin-portal first). Use the Angular v20+ conventions: standalone components, signals, OnPush, native `@if`/`@for`, typed reactive forms. Design tokens come from `docs/tokens.css` and the component specs in `docs/03-components.md`.
- **`docs/`** — authoritative requirements (`FR-*`/`NFR-*`), design system, and the phased build plan (`docs/IMPLEMENTATION-PLAN-admin-portal.md`). Treat these as the source of truth; cite requirement IDs in commits/PRs/tests.

The **.NET Claude Kit** plugin is enabled via `.claude/settings.json` at this root. Tenancy stance: every tenant-owned entity carries `TenantId` with an EF global query filter (single tenant today, multi later). Audit ("who did what, on everything, with history") is a first-class, append-only requirement.
