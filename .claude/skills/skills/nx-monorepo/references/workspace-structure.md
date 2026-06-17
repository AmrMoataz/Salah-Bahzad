# Nx Workspace Structure for Angular

## Recommended Monorepo Layout

```
my-workspace/
├── nx.json                          # Workspace config (plugins, defaults, caching)
├── tsconfig.base.json               # Shared TS config with path mappings
├── package.json                     # Root dependencies
├── .eslintrc.json                   # Root ESLint config
├── apps/
│   ├── my-app/                      # Angular application
│   │   ├── project.json             # Project-specific targets (or inferred)
│   │   ├── src/
│   │   │   ├── app/
│   │   │   │   ├── app.component.ts
│   │   │   │   ├── app.config.ts
│   │   │   │   └── app.routes.ts
│   │   │   ├── main.ts
│   │   │   └── index.html
│   │   └── tsconfig.app.json
│   └── my-app-e2e/                  # E2E tests (Playwright/Cypress)
│       └── project.json
├── libs/
│   ├── feature-auth/                # Feature library
│   │   ├── project.json
│   │   ├── src/
│   │   │   ├── index.ts             # Public API barrel
│   │   │   └── lib/
│   │   └── tsconfig.lib.json
│   ├── shared/
│   │   ├── ui/                      # Shared UI components
│   │   ├── data-access/             # API/state services
│   │   └── util/                    # Pure utility functions
│   └── domain/                      # Domain models/interfaces
└── tools/
    └── generators/                  # Custom workspace generators
```

## Library Classification

Organize libraries by type for clear boundaries:

| Type | Prefix | Purpose | Depends On |
|------|--------|---------|------------|
| **feature** | `feature-` | Smart components, pages, routes | data-access, ui, util, domain |
| **data-access** | `data-access-` | API calls, state management | util, domain |
| **ui** | `ui-` | Presentational components | util, domain |
| **util** | `util-` | Pure functions, helpers | domain |
| **domain** | `domain-` | Interfaces, types, constants | (none) |

### Module Boundary Enforcement

```jsonc
// .eslintrc.json (root)
{
  "overrides": [
    {
      "files": ["*.ts"],
      "rules": {
        "@nx/enforce-module-boundaries": [
          "error",
          {
            "depConstraints": [
              { "sourceTag": "type:feature", "onlyDependOnLibsWithTags": ["type:data-access", "type:ui", "type:util", "type:domain"] },
              { "sourceTag": "type:data-access", "onlyDependOnLibsWithTags": ["type:util", "type:domain"] },
              { "sourceTag": "type:ui", "onlyDependOnLibsWithTags": ["type:util", "type:domain"] },
              { "sourceTag": "type:util", "onlyDependOnLibsWithTags": ["type:domain"] },
              { "sourceTag": "type:domain", "onlyDependOnLibsWithTags": [] }
            ]
          }
        ]
      }
    }
  ]
}
```

### Tag Projects

```jsonc
// libs/feature-auth/project.json
{
  "tags": ["type:feature", "scope:auth"]
}
```

## Creating New Workspace

```bash
npx create-nx-workspace@latest my-workspace \
  --preset=angular-monorepo \
  --appName=my-app \
  --style=scss \
  --nxCloud=yes
```

### Integrated vs Package-Based

| Approach | Use When |
|----------|----------|
| **Integrated** (recommended for Angular) | Single framework, tight coupling, maximum Nx features |
| **Package-based** | Multiple frameworks, independent versioning, gradual adoption |

## Project Configuration (project.json)

With inferred tasks (Project Crystal), `project.json` is minimal:

```jsonc
// apps/my-app/project.json
{
  "name": "my-app",
  "projectType": "application",
  "tags": ["type:app", "scope:main"],
  "targets": {
    // Most targets inferred by plugins. Override only when needed:
    "build": {
      "options": {
        "outputPath": "dist/apps/my-app"
      }
    }
  }
}
```

```jsonc
// libs/shared-ui/project.json
{
  "name": "shared-ui",
  "projectType": "library",
  "tags": ["type:ui", "scope:shared"],
  "targets": {}
}
```

## TypeScript Path Mappings

```jsonc
// tsconfig.base.json
{
  "compilerOptions": {
    "paths": {
      "@myorg/shared-ui": ["libs/shared/ui/src/index.ts"],
      "@myorg/data-access": ["libs/shared/data-access/src/index.ts"],
      "@myorg/feature-auth": ["libs/feature-auth/src/index.ts"],
      "@myorg/domain": ["libs/domain/src/index.ts"],
      "@myorg/util": ["libs/shared/util/src/index.ts"]
    }
  }
}
```

## Common Commands

```bash
# Dependency graph visualization
nx graph

# List all projects
nx show projects

# Show project details
nx show project my-app

# Run task for specific project
nx build my-app
nx test shared-ui
nx lint feature-auth

# Run task for all projects
nx run-many -t build
nx run-many -t test --parallel=5

# Run affected tasks
nx affected -t build test lint

# Migrate to latest Nx
nx migrate latest
nx migrate --run-migrations
```

## Adding to Existing Angular CLI Workspace

```bash
# From existing Angular CLI workspace root
npx nx init
```

This adds:
- `nx.json` with default config
- Nx plugin registrations
- Task caching out of the box
