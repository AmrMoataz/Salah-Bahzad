# Caching, Affected, and CI Optimization

## How Caching Works

Nx computes a **hash** for each task based on inputs. If the hash matches a previous run, Nx restores cached outputs and replays terminal output instead of re-executing.

### Hash Inputs

- Source files in the project (filtered by `inputs` config)
- Dependencies (other projects this project depends on)
- Runtime environment (Node version, OS, env vars if configured)
- Task options/flags
- External dependencies (`package-lock.json`, etc.)

### Cache Configuration

```jsonc
// nx.json
{
  "targetDefaults": {
    "build": {
      "cache": true,
      "inputs": ["production", "^production"],
      "outputs": ["{projectRoot}/dist"]
    },
    "test": {
      "cache": true,
      "inputs": ["default", "^default"],
      "outputs": ["{projectRoot}/coverage"]
    },
    "lint": {
      "cache": true,
      "inputs": ["default", "{workspaceRoot}/.eslintrc.json"]
    }
  }
}
```

### Cache Invalidation

Cache busts when any input changes. Fine-tune with `namedInputs`:

```jsonc
{
  "namedInputs": {
    "default": ["{projectRoot}/**/*", "sharedGlobals"],
    "production": [
      "default",
      "!{projectRoot}/**/*.spec.ts",        // Exclude test files
      "!{projectRoot}/tsconfig.spec.json",
      "!{projectRoot}/.eslintrc.json",
      "!{projectRoot}/jest.config.ts",
      "!{projectRoot}/**/*.stories.ts"       // Exclude storybook
    ],
    "sharedGlobals": []
  }
}
```

### Cache Rules

- Cacheable tasks MUST be side-effect free (same inputs = same outputs)
- Do NOT cache tasks with side effects (deploy, publish, e2e with external deps)
- Use `"^production"` prefix to include dependency inputs

## Affected Commands

Run tasks only on projects affected by changes since `defaultBase`:

```bash
# Run affected builds
nx affected -t build

# Run affected tests
nx affected -t test

# Run affected lint
nx affected -t lint

# Custom base/head
nx affected -t build --base=main --head=HEAD

# List affected projects (no task execution)
nx show projects --affected

# Visualize affected graph
nx affected --graph
```

### How Affected Works

1. Nx computes file changes between `base` and `head`
2. Maps changed files to projects
3. Walks the project dependency graph to find all downstream dependents
4. Runs the specified task only on affected projects

### Optimize Affected Accuracy

Flatten your project graph. More granular libraries = more precise affected:

```
# Bad: one big app (any change rebuilds everything)
apps/my-app/

# Good: split into focused libraries
libs/feature-auth/
libs/feature-dashboard/
libs/shared-ui/
libs/data-access/
```

## Task Pipelines

Define task dependencies in `targetDefaults`:

```jsonc
{
  "targetDefaults": {
    "build": {
      "dependsOn": ["^build"]         // Build deps first (topological)
    },
    "test": {
      "dependsOn": ["build"]          // Build self before testing
    },
    "deploy": {
      "dependsOn": ["build", "test"]  // Build + test before deploy
    }
  }
}
```

- `"^build"` = run `build` on all dependencies first (topological order)
- `"build"` = run `build` on the same project first
- `"project-a:build"` = run `build` on a specific project first

## CI Configuration

### GitHub Actions with Nx

```yaml
name: CI
on: [push, pull_request]

jobs:
  main:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0   # Full history for affected

      - uses: actions/setup-node@v4
        with:
          node-version: 20

      - run: npm ci

      - uses: nrwl/nx-set-shas@v4   # Set NX_BASE and NX_HEAD

      - run: npx nx affected -t lint test build
```

### Nx Cloud (Remote Cache + DTE)

```bash
# Connect workspace to Nx Cloud
npx nx connect
```

```jsonc
// nx.json
{
  "nxCloudId": "your-cloud-id"
}
```

### Distributed Task Execution (Nx Agents)

```yaml
# .nx/workflows/ci.yml (Nx Cloud managed)
distribute-on:
  small-linux: 3    # 3 small agents
  medium-linux: 1   # 1 medium agent
```

Nx Agents automatically:
- Distribute tasks across machines based on historical run times
- Respect task dependencies and ordering
- Share cache across agents

## Performance Tips

| Strategy | Impact | How |
|----------|--------|-----|
| Remote caching | High | `npx nx connect` for Nx Cloud |
| Granular libraries | High | Split large apps into focused libs |
| Parallel execution | Medium | Increase `parallel` in nx.json |
| Fine-tune inputs | Medium | Exclude non-essential files from cache hash |
| Affected commands | High | Use `nx affected` in CI instead of `run-many` |
| Distributed execution | Very High | Nx Agents for large workspaces |
