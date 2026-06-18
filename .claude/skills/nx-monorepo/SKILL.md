---
name: nx-monorepo
description: "Senior Nx monorepo architect guidance for Angular workspaces. Covers workspace setup, nx.json configuration, project structure, code generation with @nx/angular generators, build optimization, task caching, affected commands, CI pipelines, module boundaries, and custom generators. Use when creating or configuring Nx workspaces, generating Angular apps/libraries/components in Nx, optimizing builds with caching and affected commands, setting up CI with Nx Cloud, enforcing module boundaries, writing custom generators, or migrating to Nx. Do not use for single-project Angular CLI apps without Nx."
---

# Nx Monorepo for Angular

Expert guidance for Nx 20+ monorepos with Angular. Prefer inferred tasks (Project Crystal) and minimal `project.json` configuration.

## Decision Guide

| Task | Action |
|------|--------|
| New workspace | `npx create-nx-workspace --preset=angular-monorepo` |
| Add app | `nx g @nx/angular:application` |
| Add shared library | `nx g @nx/angular:library --buildable` |
| Add publishable library | `nx g @nx/angular:library --publishable --importPath=@scope/name` |
| Generate component/service/pipe | `nx g @nx/angular:component\|service\|pipe --project=name` |
| Visualize deps | `nx graph` |
| Run affected tasks | `nx affected -t build test lint` |
| Migrate Nx | `nx migrate latest && nx migrate --run-migrations` |
| Add Nx to Angular CLI project | `npx nx init` |

## Reference Guide

| Topic | Reference | Load When |
|-------|-----------|-----------|
| nx.json full config | `references/nx-json-config.md` | Configuring plugins, targetDefaults, namedInputs, generators, caching |
| @nx/angular generators | `references/angular-generators.md` | Generating apps, libs, components, custom generators |
| Caching, affected, CI | `references/caching-and-ci.md` | Build optimization, remote cache, affected commands, CI setup |
| Workspace structure | `references/workspace-structure.md` | Project layout, library types, module boundaries, tags |

Refer to `references/nx-json-config.md` for full nx.json configuration options.
Refer to `references/angular-generators.md` for @nx/angular generator commands and options.
Refer to `references/caching-and-ci.md` for build caching, affected commands, and CI pipeline setup.
Refer to `references/workspace-structure.md` for project layout, library types, and module boundary enforcement.

## Core Principles

- Use **inferred tasks** (Project Crystal) over explicit executor config in `project.json`
- Keep `project.json` minimal; let plugins infer build/serve/test/lint targets
- Organize libs by type: `feature`, `data-access`, `ui`, `util`, `domain`
- Enforce module boundaries with `@nx/enforce-module-boundaries` ESLint rule
- Use `nx affected` in CI, never `run-many` for PRs
- Enable caching for all deterministic tasks (`build`, `test`, `lint`)
- Exclude test/config files from `production` named input to avoid unnecessary cache busts
- Prefer buildable libs for incremental builds in large workspaces

## Quick Start: New Angular Monorepo

First, create the workspace with `create-nx-workspace`. Next, generate shared libraries for UI, features, and data access. Finally, verify the dependency graph with `nx graph`.

```bash
npx create-nx-workspace@latest my-org \
  --preset=angular-monorepo \
  --appName=web-app \
  --style=scss \
  --nxCloud=yes

cd my-org

# Add a shared UI library
nx g @nx/angular:library shared/ui --buildable --prefix=ui

# Add a feature library
nx g @nx/angular:library feature-auth --buildable --prefix=auth

# Add a data-access library
nx g @nx/angular:library shared/data-access --buildable

# Verify dependency graph
nx graph
```

## Constraints

### MUST DO
- Tag all projects (`type:feature`, `type:ui`, etc.) for boundary enforcement
- Use `--buildable` for libraries in workspaces with >5 libs
- Configure `namedInputs.production` to exclude test files
- Use `nx affected` in CI pipelines
- Keep barrel files (`index.ts`) as the only public API for libraries

### MUST NOT DO
- Commit `.nx/cache` to version control
- Import library internals bypassing the barrel file
- Use `run-many` for PR checks (use `affected` instead)
- Put business logic in `apps/`; extract to `libs/` for reuse and testability
- Skip `fetch-depth: 0` in CI checkout (breaks affected calculation)
