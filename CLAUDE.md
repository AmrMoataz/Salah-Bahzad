# Salah Bahzad — Monorepo Root

This is the repo root. Work happens in three areas:

- **`backend/`** — .NET 10 API (Clean Architecture + CQRS). **Read `backend/CLAUDE.md`** for all backend conventions, domain model, and business rules before changing backend code. Run `dotnet`/the .NET Claude Kit from inside `backend/`.
- **`frontend/`** — Angular v20+ apps (admin-portal first). **Read `frontend/CLAUDE.md`** before changing frontend code. The authoritative design is the **`.claude/Salah Bahzad Teacher Portal/`** prototype + its `ds/tokens/*` (mirrored into `apps/admin-portal/src/styles/_design-tokens.scss`) — NOT `docs/tokens.css` or `docs/03-components.md`, which are deprecated for design.
- **`docs/`** — authoritative **requirements** (`FR-*`/`NFR-*`) and the phased build plan (`docs/IMPLEMENTATION-PLAN-admin-portal.md`); cite requirement IDs in commits/PRs/tests. Source of truth for requirements only — the design source of truth is the `.claude` design system above.

The **.NET Claude Kit** plugin is enabled via `.claude/settings.json` at this root. Tenancy stance: every tenant-owned entity carries `TenantId` with an EF global query filter (single tenant today, multi later). Audit ("who did what, on everything, with history") is a first-class, append-only requirement.
