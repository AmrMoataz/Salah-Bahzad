# Salah Bahzad Platform

Monorepo for the Salah Bahzad online private-tutoring platform — a tenant-ready (single-tenant now) system for an Egyptian secondary-school maths tutor.

## Repository layout

```
.
├─ backend/    .NET 10 API — Clean Architecture + CQRS (see backend/CLAUDE.md)
│   ├─ src/    SalahBahazad.Domain · .Application · .Infrastructure · .Api
│   └─ tests/  SalahBahazad.UnitTests · .IntegrationTests
├─ frontend/   Angular apps — admin-portal (this engagement); student-portal later
├─ docs/       Requirements & architecture specs (FR/NFR), design system, tokens, build plan
└─ .claude/    Claude Code config (the .NET Claude Kit plugin is enabled here)
```

## Where to start

- **Requirements:** `docs/README.md` (overview, roles, glossary) → `docs/01`–`docs/09`.
- **Design system & tokens:** `docs/01-brand.md`, `docs/02-foundations.md`, `docs/03-components.md`, `docs/tokens.*`.
- **Build plan:** `docs/IMPLEMENTATION-PLAN-admin-portal.md`.
- **Backend conventions:** `backend/CLAUDE.md` (generated & tuned via the .NET Claude Kit).

## Running

- **Backend:** `cd backend && dotnet run --project src/SalahBahazad.Api` (PostgreSQL + Redis required — see backend/CLAUDE.md).
- **Frontend:** added in the foundation phase — see `frontend/README.md`.

## Scope note

Current focus: the **Teacher/Admin portal** (backend + Angular frontend). The student portal frontend and the Flutter native video app are planned later; the shared backend engines are built to serve them.
