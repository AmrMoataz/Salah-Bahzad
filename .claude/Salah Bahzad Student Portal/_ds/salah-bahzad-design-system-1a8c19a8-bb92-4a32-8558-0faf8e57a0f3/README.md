# Salah Bahzad Design System

The shared visual language for everything Salah Bahzad — from the playful, mascot-driven social/marketing material to the calm, functional admin and student portals. This design system enables consistent, on-brand experiences across all surfaces while respecting the distinct personalities of brand marketing (expressive, mascot-forward) and product UI (calm, functional).

## Overview

Salah Bahzad is a real person — an electrical engineer turned mathematics tutor with 8+ years of experience teaching secondary students. The brand reflects his core values: **confident, approachable, energetic, and clear**. The design system is deliberately split into two complementary layers:

| Layer | Context | Character |
|---|---|---|
| **Brand layer** | Social posts, landing/marketing pages, auth/splash screens, empty states, thumbnails | Vibrant, hand-drawn, mascot-forward, expressive marker/script type, full saturated palette |
| **Product-UI layer** | Admin portal, student portal, functional app screens | Calm, legible, disciplined color use (one primary blue + green accent), warm neutrals, clean rounded sans |

Both layers share the same **foundations** (tokens) but apply them with different emphasis and intent.

## Contents

- **[tokens/](tokens/)** — CSS custom properties for colors, typography, spacing, shadows, motion (194 tokens total)
  - `colors.css` — Brand palette, primary/accent ramps, neutrals, semantic colors, subject accents (8 subject-specific colors)
  - `typography.css` — Font families (Nunito Sans, Cascadia Mono, Caveat, Permanent Marker), type scale, weights, line heights
  - `spacing.css` — Spacing scale (4px base), border radii, z-index, breakpoints, motion timing & easing
  - `shadows.css` — Elevation shadows, focus ring
- **[guidelines/](guidelines/)** — foundation specimen cards (colors, type, spacing, shadows, Student Portal UI samples)
- **[components/](components/)** — 30 reusable product-UI components, organized by concern
  - `buttons/` — Button (primary, accent, secondary, ghost, danger, danger-ghost; sm/md/lg; loading, icon-only)
  - `forms/` — Input, Select, Checkbox, Radio, Switch, CodeInput (segmented enrollment code), SearchBar, DatePicker, FileUpload
  - `feedback/` — Alert, Toast, Badge, Chip, Tag, Avatar, Progress (linear + score ring), Tooltip, Skeleton, Modal, Drawer, Timer (quiz countdown)
  - `navigation/` — Tabs, Breadcrumb, Pagination, Stepper
  - `layout/` — Card, StatCard (KPI), EmptyState (mascot)
  - `data/` — Table (sortable, zebra, custom cells)
- **[ui_kits/](ui_kits/)** — Full-screen interactive UI kit samples for each product surface
- **[assets/](assets/)** — Brand assets (mascot, logos, crown, cover imagery)

## Key Principles

1. **Friendly, not childish.** Rounded shapes, warm neutrals, and confident blue provide the foundation; playful touches (mascot, marker headings) are seasoning, not the meal.
2. **Marker/script type is for brand layer only.** UI text is always the rounded sans (`--sb-font-ui`). Never set body copy, form labels, or table data in marker or script.
3. **Saturated colors are accents, not canvases.** In product UI, brand colors carry meaning (primary action, success, danger, subject tags); large surfaces stay neutral/warm-white.
4. **The mascot has a job.** Use it to add warmth in appropriate contexts (onboarding, empty states, success, errors) — never on dense functional screens.
5. **Accessible by default.** Text meets WCAG AA contrast; mustard/mint/pink work as backgrounds/icons but never as small text on white.
6. **One source of truth.** Components reference tokens; tokens live in the token files. Change a value once, everywhere updates.

## Content Fundamentals

**Voice & tone:**
- Friendly and direct, encouraging, lightly humorous
- Speaks **to** the student ("you've got this"), never down at them
- Upbeat for announcements/marketing; calm and plain for instructions, grades, errors
- Avoids stiff corporate copy, heavy jargon, generic ed-tech templates
- Salah is a real, approachable person — the brand reflects his authentic personality

**Copy guidelines:**
- Use first person ("I've got this lesson") and second person ("You've completed...") to build connection
- Keep sentences short and punchy; clarity over cleverness
- Use encouraging language for struggles ("Let's try again" vs. "Error")
- Capitalize titles consistently; use sentence case for body copy
- Emoji: reserved for the brand layer (posts, auth screens, empty states) — never in functional UI
- Numbers: spell out in marketing copy, use numerals in UI/data contexts

## Visual Foundations

**Colors:**
- **Primary blue** (`--sb-primary-600`, `#2C6FB3`) — the platform's signature color, mascot's shirt. Use for primary actions, links, focus states.
- **Accent green** (`--sb-accent-600`, `#46A33E`) — secondary actions, positive emphasis, success states.
- **Full brand palette** (red, mustard, mint, orange, purple, pink) — reserved for brand-layer contexts (posters, social, marketing). Never use on functional UI unless semantic (danger = red, warning = mustard).
- **Warm neutrals** (`neutral-25` to `neutral-900`) — the canvas for product UI. Off-white backgrounds (`neutral-25`), white surfaces (`neutral-0`), text gradations for hierarchy.
- **Subject accents** (8 color-coded options) — for tagging subjects/specializations, timetable blocks, calendar views. Each has a base color, tinted background, and deep text value.

**Typography:**
- **UI font:** Nunito Sans, 400/600/700/800 weights. Clean, rounded, friendly without being playful. Used for all functional UI.
- **Display (brand only):** Permanent Marker (hand-drawn marker lettering) for brand headings, social posts, hero sections. Never on functional UI.
- **Script (brand only):** Caveat (brush script) for elegant accents on brand-layer pieces (e.g. "Thursday" on schedule teasers).
- **Mono:** Cascadia Code / Fira Code for codes, serials, enrollment tokens.
- **Scale:** 12px minimum (captions); 16px base (body); 24–30px for UI headings; 48–72px for brand display.

**Spacing & layout:**
- 4px base unit. Defaults: 12–16px padding on inputs/buttons; 24px on cards; 48–64px section gaps.
- Generous margins; let color blocks and imagery breathe.
- Desktop-first *and* mobile-friendly — responsive from 480px (large phone) to 1536px+ (large desktop).

**Radius:**
- `--sb-radius-md` (10px) — default for buttons, inputs, cards
- `--sb-radius-lg` (14px) — modals, larger cards
- `--sb-radius-pill` (999px) — pills, segmented controls, tags
- `--sb-radius-circle` (50%) — avatars, icon buttons

**Shadows & elevation:**
- Soft, warm-tinted shadows (using ink alpha). Keep elevation low — friendly, not glossy.
- `shadow-sm` (default for cards/buttons) → `shadow-xl` (modals, marketing features)
- Focus ring: 3px `--sb-focus-ring` (soft blue, visible on both light and dark)

**Motion:**
- Transitions default to 250ms with a friendly easing (`cubic-bezier(0.2, 0.8, 0.2, 1)`)
- Fast 150ms for hover/small state changes; slow 400ms for overlays/page transitions
- Always respect `prefers-reduced-motion: reduce` — drop non-essential animations

**Backgrounds & imagery:**
- Product UI: warm off-white (`neutral-25`) as app bg; white (`neutral-0`) for cards/surfaces
- Brand layer: full-bleed photos/color blocks with overlaid text, chalk formulas on blue fields, mascot illustrations
- Imagery tone: warm, approachable, educational (photos of real tutoring, student success, learning moments)
- Never use: harsh filters, over-saturated stock photos, sterile corporate imagery

**Hover/interaction states:**
- Hover: darken slightly (e.g. `primary-700` on `primary-600` button); lift with shadow
- Active/pressed: additional darkening + `translateY(1px)` for tactile feel
- Disabled: 45% opacity + `not-allowed` cursor
- Focus: always visible focus ring; keyboard navigation is first-class
- Loading: spinner overlay or inline spinner + label, preserve button width

## Iconography

**Style & approach:**
- Outline icons, ~1.75px stroke, rounded caps & joins (matches the friendly brand aesthetic)
- 24px grid for standard icons; 20px for dense UI; 16px for inline
- Source: **Lucide** or **Phosphor** rounded variants recommended
- Icon color follows text (`currentColor`); use semantic `-fg` colors for status indicators

**Brand glyphs:**
- Sun/spiral (☼) — optimistic signature glyph, use as accents, bullets, dividers
- Crown — pairs with SB monogram for premium/achievement moments
- Chalk math formulas — faint white equations on blue (brand backgrounds only)
- Apple, megaphone, hand-drawn arrows/squiggles — playful spot illustrations for education/announcements

Use **one** dominant graphic motif per composition; glyphs are seasoning.

## Logo system & usage

**Four expressions:**
1. **Primary wordmark** — "SALAH ᴹ BAHZAD" stacked in hand-drawn marker lettering (default for posts, headers, footers)
2. **Sun lockup** — "SALAH ☼ BAHZAD" with sun glyph (horizontal format for hero/marketing)
3. **Monogram** — "SB" ligature + crown (avatars, favicon, app icon, tight spaces)
4. **Mascot + wordmark** — the bearded character beside wordmark (friendly/marketing contexts)

**Color variants:** ink (`--sb-ink`) on light backgrounds; white on color/photo; navy (`--sb-navy`) variant available.

**Rules:**
- Clear space = cap-height of "S" (≈ crown height for monogram)
- Minimum size: wordmark ≥ 120px wide; monogram ≥ 24px
- Never: stretch/skew, add shadows/outlines, recolor arbitrarily, reposition elements, relinquish

## The mascot

A full character system (not a single image) — observed poses include running, pointing up, sitting/relaxing (sunglasses), waving, surprised, falling, thumbs-up, painting.

**When to use which mood:**
- **Welcoming** (waving/pointing) — onboarding, hero, sign-up
- **Positive** (thumbs-up/running) — success, completion, "apply now"
- **Thinking/teaching** (pointing up) — tips, explanations, lesson intros
- **Empty/quiet** (sitting/relaxing) — empty states ("nothing here yet")
- **Oops** (surprised/falling) — errors, 404, failed actions

**Rules:** One mascot per view; give it room; keep on calm background; never stretch or recolor.

## How to use this system

**For product UI (portals/app screens):**
- Reference tokens from `tokens/*.css` for colors, type, spacing
- Build from the component library (reusable, tested pieces)
- Follow the product-UI layer guidelines (calm, legible, one primary + accent)
- Use [guidelines/](guidelines/) cards to review specifications

**For brand/marketing (posts, landing, auth, hero):**
- Add brand layer guidelines (use the full palette, marker headings, mascot)
- Reference brand glyphs, logos, illustration poses from [assets/](assets/)
- Pair marker display with rounded-sans supporting text
- Keep generous margins; lead with one or two colors per piece

**Distributing to teams:**
- Export `styles.css` (which imports all tokens) to consuming projects
- Provide component source files for implementation
- Share this README + guideline cards for design reference

## File structure

```
salah-design-system/
├── styles.css                       # Global entry point (imports all tokens)
├── tokens/
│   ├── colors.css                  # Color palette, ramps, semantic roles
│   ├── typography.css              # Fonts, sizes, weights, line heights
│   ├── spacing.css                 # Space scale, radius, z-index, breakpoints
│   └── shadows.css                 # Shadows, motion, focus ring
├── guidelines/                      # Foundation specimen cards
│   ├── colors-primary.card.html    # Primary blue ramp
│   ├── colors-accent.card.html     # Accent green ramp
│   ├── colors-neutral.card.html    # Warm-tinted grays
│   ├── colors-semantic.card.html   # Success, danger, warning, info
│   ├── colors-brand.card.html      # Full expressive palette
│   ├── colors-subject.card.html    # 8 subject-accent colors
│   ├── typography-scale.card.html  # Type scale (xs to 4xl)
│   ├── typography-weights.card.html# Font weights
│   ├── spacing-scale.card.html     # Spacing tokens
│   ├── radius.card.html            # Border radius variants
│   └── shadows.card.html           # Shadow elevations
├── components/
│   ├── buttons/
│   │   ├── Button.jsx              # Primary button component
│   │   ├── Button.d.ts             # Props interface
│   │   ├── Button.prompt.md        # Usage guide
│   │   └── buttons.card.html       # Button gallery & states
│   ├── forms/
│   │   ├── Input.jsx               # Text input component
│   │   ├── Input.d.ts              # Props interface
│   │   ├── Input.prompt.md         # Usage guide
│   │   └── forms.card.html         # Input specimens
│   ├── layout/
│   │   ├── Card.jsx                # Surface/container component
│   │   ├── Card.d.ts               # Props interface
│   │   ├── Card.prompt.md          # Usage guide
│   │   └── layout.card.html        # Card specimens
│   └── feedback/
│       ├── Badge.jsx               # Count badge component
│       ├── Badge.d.ts              # Props interface
│       ├── Badge.prompt.md         # Usage guide
│       ├── Tag.jsx                 # Subject tag component
│       ├── Tag.d.ts                # Props interface
│       ├── Tag.prompt.md           # Usage guide
│       └── feedback.card.html      # Tags & badges gallery
├── ui_kits/
│   ├── student_portal/
│   │   ├── Dashboard.html          # Student home screen
│   │   └── Session.html            # Active tutoring session
│   └── marketing/
│       ├── Hero.html               # Landing page hero
│       └── Features.html           # Feature cards layout
├── SKILL.md                        # Skill documentation for Claude Code
└── README.md                       # (this file)
```

## Manifest

### Foundation Cards (11 cards, Design System tab)

| Name | Group | Content |
|---|---|---|
| Primary Blue | Colors | 10-step blue ramp, 600 as default |
| Accent Green | Colors | 10-step green ramp, 600 as default |
| Neutral Grays | Colors | Warm-tinted 0–900 scale |
| Semantic Colors | Colors | Success, danger, warning, info |
| Brand Palette | Colors | 10 expressive brand colors |
| Subject Accents | Colors | 8 subject color systems |
| Type Scale | Type | Sizes xs (12px) to 4xl (48px) |
| Font Weights | Type | 400–900 weights |
| Spacing Scale | Spacing | 4px base unit tokens |
| Border Radius | Spacing | xs (4px) to pill (999px) |
| Shadows | Spacing | xs to xl elevations |

### Components (30 across 6 groups)

| Group | Components |
|---|---|
| buttons | Button (primary, accent, secondary, ghost, danger, danger-ghost × sm/md/lg; loading; icon-only) |
| forms | Input, Select, Checkbox, Radio, Switch, CodeInput, SearchBar, DatePicker, FileUpload |
| feedback | Alert, Toast, Badge, Chip, Tag, Avatar, Progress (linear + circular score ring), Tooltip, Skeleton, Modal, Drawer, Timer |
| navigation | Tabs, Breadcrumb, Pagination, Stepper |
| layout | Card, StatCard, EmptyState |
| data | Table |

Each component reads only the canonical tokens in `tokens/*.css` (e.g. `--sb-text`, `--sb-text-muted`, `--sb-space-4`, `--sb-font-sans`, `--sb-success-bg`). All `@dsCard` galleries load React + the compiled `_ds_bundle.js` and read components from `window.SalahBahzadDesignSystem_<hash>`.

### UI Kits (4 screens, Design System tab)

| Name | Path | Type |
|---|---|---|
| Dashboard | `ui_kits/student_portal/Dashboard.html` | Student home screen |
| Session | `ui_kits/student_portal/Session.html` | Active tutoring session |
| Hero | `ui_kits/marketing/Hero.html` | Landing page hero |
| Features | `ui_kits/marketing/Features.html` | Feature cards grid |

## How to iterate

**Edit tokens:** Update values in `tokens/*.css` and they cascade to all components and cards automatically.

**Add components:** Create `ComponentName.jsx` + `ComponentName.d.ts` + `ComponentName.prompt.md` in a `components/<group>/` folder. The compiler will auto-bundle it.

**Create cards:** Add `*.card.html` files with `<!-- @dsCard group="..." -->` on line 1. They appear in the Design System tab.

**UI kits:** Build full-screen mockups in `ui_kits/<product>/` and tag with `<!-- @dsCard group="..." -->` to index them.

## Caveats & next steps

- **Font files:** This system references Google Font fallbacks (Nunito Sans, Permanent Marker, Caveat, Cascadia Code). For production use, embed the actual brand fonts and update `@font-face` rules in `tokens/typography.css`.
- **Icon system:** Lucide or Phosphor icons recommended (rounded variants). Copy icon sets into `assets/icons/` if using a proprietary icon font.
- **Component coverage:** The full product-UI set from `03-components.md` is implemented — buttons, form controls (incl. the segmented enrollment CodeInput), cards/KPIs, tables, tags/badges/status pills, tabs/breadcrumb/pagination/stepper, modal/drawer, alerts/toasts, avatar, progress (linear + score ring), tooltip, skeleton, and the mascot empty state. Navigation shells (sidebar/topbar) live as ui_kit layouts rather than single components.
- **Mascot library:** Brand layer empty states, onboarding, and hero sections should use mascot poses (waving, thumbs-up, sitting, surprised). Illustrations available in `assets/` (or create/source as needed).

---

*Design system source:* Extracted from Salah Bahzad's brand assets and documentation (logos, mascot, social posts, profile art). Values are approximate and tunable — adjust hex/font values in token files and they cascade everywhere.

**To use with consuming projects:** Export `styles.css` (which imports all tokens) and the compiled `_ds_bundle.js` to any project that needs on-brand components.
