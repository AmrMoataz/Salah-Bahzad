# @nx/angular Generators

## Application & Library

### `nx g @nx/angular:application`

Create an Angular application.

```bash
nx g @nx/angular:application my-app --routing --style=scss --ssr
```

| Option | Default | Description |
|--------|---------|-------------|
| `--bundler` | `esbuild` | `esbuild` \| `webpack` |
| `--style` | `css` | `css` \| `scss` \| `sass` \| `less` |
| `--routing` | `true` | Add router configuration |
| `--standalone` | `true` | Standalone components |
| `--ssr` | `false` | Server-side rendering |
| `--strict` | `true` | Strict type checking |
| `--prefix` | `app` | Component selector prefix |
| `--e2eTestRunner` | `playwright` | `playwright` \| `cypress` \| `none` |
| `--port` | `4200` | Dev server port |

### `nx g @nx/angular:library`

Create a shared Angular library.

```bash
nx g @nx/angular:library shared/ui --buildable --importPath=@myorg/shared-ui
```

| Option | Default | Description |
|--------|---------|-------------|
| `--buildable` | `false` | Add build target for incremental builds |
| `--publishable` | `false` | Generate for npm publishing (requires `--importPath`) |
| `--importPath` | — | Custom import path (`@myorg/lib-name`) |
| `--standalone` | `true` | Standalone components |
| `--routing` | `false` | Add router config |
| `--lazy` | `false` | Add lazy-loaded route |
| `--prefix` | — | Component selector prefix |
| `--directory` | — | Subdirectory within `libs/` |

### Library Types Guide

| Need | Type | Flag |
|------|------|------|
| Internal shared code | Default | (none) |
| Incremental builds in monorepo | Buildable | `--buildable` |
| Publish to npm | Publishable | `--publishable --importPath=@scope/name` |

## Component, Directive, Pipe, Service

### `nx g @nx/angular:component`

```bash
nx g @nx/angular:component my-component --project=my-app --changeDetection=OnPush
```

| Option | Default | Description |
|--------|---------|-------------|
| `--standalone` | `true` | Standalone component |
| `--style` | `css` | Style extension |
| `--inlineStyle` | `false` | Inline styles |
| `--inlineTemplate` | `false` | Inline template |
| `--changeDetection` | `Default` | `Default` \| `OnPush` |
| `--viewEncapsulation` | `Emulated` | `Emulated` \| `None` \| `ShadowDom` |
| `--skipTests` | `false` | Skip test file |
| `--export` | `false` | Export from library barrel |

### `nx g @nx/angular:directive`

```bash
nx g @nx/angular:directive highlight --project=shared-ui --export
```

### `nx g @nx/angular:pipe`

```bash
nx g @nx/angular:pipe format-date --project=shared-utils --export
```

### `nx g @nx/angular:service`

```bash
nx g @nx/angular:service data-access --project=my-app
```

## Advanced Generators

### `nx g @nx/angular:setup-ssr`

Add SSR to an existing application.

```bash
nx g @nx/angular:setup-ssr --project=my-app
```

### `nx g @nx/angular:setup-mf`

Configure Module Federation for micro-frontends.

```bash
nx g @nx/angular:setup-mf --project=shell --mfType=host
nx g @nx/angular:setup-mf --project=remote1 --mfType=remote --host=shell --port=4201
```

### `nx g @nx/angular:ngrx`

Add NgRx state management.

```bash
nx g @nx/angular:ngrx users --project=my-app --module=app.module.ts
```

### `nx g @nx/angular:storybook-configuration`

Set up Storybook for a project.

```bash
nx g @nx/angular:storybook-configuration my-lib
```

## Custom Workspace Generators

Create project-specific generators in `tools/generators/`:

```bash
nx g @nx/workspace:generator my-generator
```

This creates:

```
tools/generators/my-generator/
├── index.ts        # Generator entry point
├── schema.json     # Options schema
└── schema.d.ts     # TypeScript types for options
```

### schema.json — Defining Generator Options

`schema.json` is a JSON Schema file that defines every option the generator accepts.
It drives CLI prompts, validation, defaults, and `--help` output.

```json
{
  "$schema": "http://json-schema.org/schema",
  "$id": "MyFeatureGenerator",
  "title": "Generate a Feature",
  "description": "Scaffolds a feature component, service, and routes",
  "type": "object",

  "properties": {

    "name": {
      "type": "string",
      "description": "Feature name, e.g. user-profile",
      "$default": { "$source": "argv", "index": 0 },
      "x-prompt": "What is the feature name?",
      "pattern": "^[a-z][a-z0-9-]*$"
    },

    "project": {
      "type": "string",
      "description": "Target Nx project",
      "x-prompt": "Which project should this feature belong to?",
      "$default": { "$source": "projectName" }
    },

    "style": {
      "type": "string",
      "description": "Stylesheet format",
      "enum": ["css", "scss", "sass", "less"],
      "default": "scss",
      "x-prompt": {
        "message": "Which stylesheet format?",
        "type": "list",
        "items": [
          { "value": "css",  "label": "CSS"  },
          { "value": "scss", "label": "SCSS" },
          { "value": "sass", "label": "SASS" },
          { "value": "less", "label": "LESS" }
        ]
      }
    },

    "withService": {
      "type": "boolean",
      "description": "Also generate a data-access service",
      "default": true,
      "x-prompt": "Generate a data-access service?"
    },

    "changeDetection": {
      "type": "string",
      "description": "Change detection strategy",
      "enum": ["OnPush", "Default"],
      "default": "OnPush"
    },

    "directory": {
      "type": "string",
      "description": "Subdirectory within the project source",
      "default": "features"
    },

    "skipTests": {
      "type": "boolean",
      "description": "Skip generating spec files",
      "default": false
    }
  },

  "required": ["name", "project"]
}
```

#### schema.json Options Reference

| Field | Purpose | Example |
|-------|---------|---------|
| `"type"` | JSON type of the value | `"string"`, `"boolean"`, `"number"` |
| `"default"` | Value used when not supplied | `"scss"`, `true` |
| `"enum"` | Restrict to a fixed set of values | `["OnPush", "Default"]` |
| `"required"` | Options that must always be provided | `["name", "project"]` |
| `"x-prompt"` | String → simple text prompt; Object → interactive list | `"What is the name?"` |
| `"$default.$source": "argv"` | Maps first positional CLI arg to this option | `nx g my-gen my-name` |
| `"$default.$source": "projectName"` | Auto-infers value from current project context | — |
| `"pattern"` | Regex validation on a string value | `"^[a-z][a-z0-9-]*$"` |

#### schema.d.ts — Matching TypeScript Interface

Every property in `schema.json` maps directly to a field here.
Keep both files in sync manually — Nx does not auto-generate one from the other.

```typescript
// tools/generators/my-generator/schema.d.ts
export interface MyGeneratorSchema {
  name: string;
  project: string;
  style: 'css' | 'scss' | 'sass' | 'less';
  withService: boolean;
  changeDetection: 'OnPush' | 'Default';
  directory: string;
  skipTests: boolean;
}
```

### Generator Implementation

```typescript
import { Tree, formatFiles, generateFiles, joinPathFragments } from '@nx/devkit';

interface MyGeneratorSchema {
  name: string;
  directory?: string;
}

export default async function (tree: Tree, options: MyGeneratorSchema) {
  const projectRoot = `libs/${options.directory ?? options.name}`;
  generateFiles(tree, joinPathFragments(__dirname, 'files'), projectRoot, {
    ...options,
    tmpl: '',  // Strip __tmpl__ from filenames
  });
  await formatFiles(tree);
}
```

### Run Custom Generator

```bash
nx g my-generator my-lib
# or with workspace prefix
nx g @myorg/tools:my-generator my-lib
```
