# Salah Bahzad — Frontend (Angular)

Nx monorepo of Angular **v20+** apps (admin-portal first). Read this before changing any frontend code.

## Design source of truth

The authoritative design is **`.claude/Salah Bahzad Teacher Portal/`** — use it for everything visual: class names, token values, layout, icons, images/assets, and copy.

- **`Admin Portal.dc.html`** — the visual prototype. When anything conflicts, follow the prototype.
- **`ds/tokens/*.css`** — the canonical design tokens (colors, typography, spacing, shadows, motion).
- **`assets/`** — the brand assets (logos, mascot poses). The app's `apps/admin-portal/src/assets/` mirrors these byte-for-byte.

⚠️ **Deprecated — do NOT reference for design:** `docs/tokens.css`, `docs/tokens.scss`, `docs/tokens.json`, `docs/03-components.md`. They use a divergent, stale token-naming scheme. The frontend no longer imports them.

## Design tokens

Tokens live in **`apps/admin-portal/src/styles/_design-tokens.scss`** — a faithful mirror of `.claude/.../ds/tokens/*.css`, pulled in via `@use 'styles/design-tokens'` from `styles.scss`. If a token value needs to change, change it in the design system and re-mirror; never override token values in component styles.

Use the **canonical token names** everywhere (these are the easy-to-get-wrong ones):

- Fonts: `--sb-font-sans` (all UI text), `--sb-font-display` (Caveat, brand headings only), `--sb-font-marker` (Permanent Marker), `--sb-font-mono`.
- Type sizes: semantic tokens only — `--sb-body-{sm,md,lg}-size`, `--sb-heading-{xs,sm,md,lg,xl}-size`, `--sb-label-{sm,md,lg}-size`, `--sb-display-{md,lg,xl}-size`. (There is **no** `--sb-text-sm` etc.)
- Font weight: raw numbers `400/600/700/800/900` — there are **no** `--sb-weight-*` tokens.
- Motion: `--sb-timing` / `--sb-timing-fast` / `--sb-timing-slow` and `--sb-easing-standard` / `--sb-easing-out` / `--sb-easing-in`. (Not `--sb-dur*` / `--sb-ease-*`.)
- Colors / spacing / radius / shadows / z-index keep their familiar names (`--sb-primary`, `--sb-text-muted`, `--sb-space-4`, `--sb-radius-md`, `--sb-shadow-sm`, …).

## Icons

Outline only — 24×24 grid, ~1.8px stroke, rounded caps/joins, `stroke="currentColor"` (Lucide/Phosphor style). Reuse the prototype's icon paths. **Never** use filled icons.

Render data-driven inline `<svg>` via `DomSanitizer.bypassSecurityTrustHtml` (see `sidebar.component.ts` / `dashboard.component.ts`) — Angular's sanitizer strips `<svg>` from plain `[innerHTML]`, which silently blanks the icon. Accent/KPI chips use `--sb-subject-{x}-bg` + `--sb-subject-{x}-deep`, never white-on-saturated.

## Angular conventions (v20+)

Standalone components, `ChangeDetectionStrategy.OnPush`, signal-based `input()`/`output()`/`model()`, `computed()`/`effect()`, `inject()` (no constructor DI), native control flow (`@if`/`@for`/`@switch` — not `*ngIf`/`*ngFor`), typed reactive forms, `ControlValueAccessor` for custom form controls. The relevant `angular-*` skills cover specifics.

## Intentional non-implementations

The prototype's demo-only "Viewing as Teacher/Assistant" sidebar switcher is **not** built — the real role comes from `AuthStore`. The sidebar collapse-to-icons toggle is replaced by the ≤900px burger + drawer + scrim.

## Build / test

- Build: `npx nx build admin-portal` (AOT build also type-checks templates — use it as the quick gate).
- Tests use Jest and currently require `ts-node` to parse `jest.config.ts`; install it (`npm i -D ts-node`) if the suite won't start.
- Cite requirement IDs (`FR-*` / `NFR-*` from `docs/`) in commits/PRs/tests. `docs/` remains the source of truth for **requirements** — just not for design.
