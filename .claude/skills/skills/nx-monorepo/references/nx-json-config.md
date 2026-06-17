# nx.json Configuration Reference

## Top-Level Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `plugins` | `Array<string \| Object>` | `[]` | Register Nx plugins that infer tasks automatically |
| `targetDefaults` | `Object` | — | Workspace-wide default options per target/executor |
| `namedInputs` | `Object` | — | Reusable input definitions for cache computation |
| `generators` | `Object` | — | Default options for code generators |
| `parallel` | `number` | `3` | Max targets run in parallel |
| `defaultBase` | `string` | `main` | Base branch for affected calculations |
| `cacheDirectory` | `string` | `.nx/cache` | Local cache location |
| `maxCacheSize` | `string \| number` | 10% disk (max 10GB) | Local cache size limit |
| `nxCloudId` | `string` | — | Nx Cloud connection identifier |
| `nxCloudUrl` | `string` | `https://cloud.nx.app` | Custom Nx Cloud URL |
| `release` | `Object` | — | Version, changelog, and publish config |
| `sync` | `Object` | — | `nx sync` command config |
| `tui` | `Object` | — | Terminal UI behavior |
| `conformance` | `Object` | — | Conformance rules (Nx Cloud) |

## Plugin Configuration

```jsonc
{
  "plugins": [
    "@nx/angular/plugin",                    // String form (no options)
    {
      "plugin": "@nx/eslint/plugin",
      "options": { "targetName": "lint" },   // Customize inferred target names
      "include": ["packages/**/*"],          // Scope to matching projects
      "exclude": ["**/*-e2e/**/*"]           // Exclude matching projects
    }
  ]
}
```

Plugins process in array order. Last plugin wins for identically-named targets.

## Target Defaults

```jsonc
{
  "targetDefaults": {
    "@nx/angular:application": {             // Executor-based key (highest priority)
      "inputs": ["production", "^production"],
      "outputs": ["{projectRoot}/dist"],
      "dependsOn": ["^build"],
      "cache": true
    },
    "build": {                               // Target name key (fallback)
      "inputs": ["default"],
      "cache": true,
      "dependsOn": ["^build"]
    },
    "test": {
      "inputs": ["default", "^default"],
      "cache": true
    },
    "lint": {
      "inputs": ["default", "{workspaceRoot}/.eslintrc.json"],
      "cache": true
    }
  }
}
```

**Key matching order**: executor name > target name > glob patterns.

**Tokens**: `{projectRoot}`, `{workspaceRoot}`, `{projectName}`.

## Named Inputs

```jsonc
{
  "namedInputs": {
    "default": ["{projectRoot}/**/*", "sharedGlobals"],
    "production": [
      "default",
      "!{projectRoot}/**/*.spec.ts",
      "!{projectRoot}/tsconfig.spec.json",
      "!{projectRoot}/.eslintrc.json",
      "!{projectRoot}/jest.config.ts"
    ],
    "sharedGlobals": [
      "{workspaceRoot}/.github/workflows/*"
    ]
  }
}
```

Project-level namedInputs merge with workspace-level: `{...nxJson, ...projectJson}`.

## Generator Defaults

```jsonc
{
  "generators": {
    "@nx/angular:component": {
      "style": "scss",
      "changeDetection": "OnPush",
      "standalone": true
    },
    "@nx/angular:library": {
      "buildable": true,
      "standalone": true
    }
  }
}
```

## Release Configuration

```jsonc
{
  "release": {
    "version": {
      "conventionalCommits": true
    },
    "changelog": {
      "workspaceChangelog": { "createRelease": "github" },
      "projectChangelogs": true,
      "git": { "commit": true, "tag": true }
    }
  }
}
```

## Terminal UI

```jsonc
{
  "tui": {
    "enabled": true,
    "autoExit": true    // true | false | number (seconds)
  }
}
```

## Configuration Precedence (lowest to highest)

1. Inferred configurations from plugins
2. `targetDefaults` in `nx.json`
3. Project-level config in `project.json` / `package.json`
