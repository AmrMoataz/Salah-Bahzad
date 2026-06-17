# Foundations (Tokens)

The shared primitives every surface draws from. These map 1:1 to [`tokens.css`](tokens.css) / [`tokens.scss`](tokens.scss) / [`tokens.json`](tokens.json). Hex values are eyeballed from the brand assets and meant to be tuned — change them in the token files and everything follows.

## Contents

- [Color — brand](#color--brand)
- [Color — product palette (ramps)](#color--product-palette-ramps)
- [Color — neutrals (warm)](#color--neutrals-warm)
- [Color — semantic](#color--semantic)
- [Color — subject accents](#color--subject-accents)
- [Semantic UI roles](#semantic-ui-roles)
- [Typography](#typography)
- [Spacing](#spacing)
- [Radius](#radius)
- [Shadow & elevation](#shadow--elevation)
- [Motion](#motion)
- [Z-index](#z-index)
- [Breakpoints](#breakpoints)
- [Iconography](#iconography)

---

## Color — brand

The full expressive palette. Use freely on the **brand layer** (posts, marketing, auth, empty states); use sparingly and with intent on the **product layer**.

| Token | Hex | Role |
|---|---|---|
| `--sb-blue` | `#2C6FB3` | Primary brand blue (mascot shirt); the platform's signature colour |
| `--sb-blue-bright` | `#3E8EDE` | Bright blue — banners, formula backgrounds, links/hover |
| `--sb-navy` | `#1E3A5F` | Deep navy — heading blocks, the 4-block thumbnail |
| `--sb-green` | `#46A33E` | Secondary brand green (the blue/green split) |
| `--sb-green-deep` | `#2F7D2A` | Green for text/contrast on light |
| `--sb-red` | `#E23B33` | Brand red — accents, alerts |
| `--sb-mustard` | `#F3C12E` | Mustard yellow — highlights, blocks |
| `--sb-mint` | `#86C7A6` | Soft mint — secondary blocks |
| `--sb-orange` | `#E8743B` | Orange — energy/CTA accents |
| `--sb-purple` | `#6E3FA6` | Purple — topical posts (e.g. statistics) |
| `--sb-pink` | `#E85FA8` | Pink accent (pairs with purple) |
| `--sb-cream` | `#F3F2E0` | Brand "paper" background (posters) |
| `--sb-ink` | `#1A1A1A` | Brand black for lettering/line art |
| `--sb-mascot-skin` | `#E7B488` | Mascot skin |
| `--sb-mascot-beard` | `#3A2A1F` | Mascot beard/hair |

## Color — product palette (ramps)

Disciplined ramps for UI. **Primary = blue** (actions, links, focus). **Accent = green** (secondary actions, positive emphasis).

**Primary (blue)**

| 50 | 100 | 200 | 300 | 400 | 500 | **600** | 700 | 800 | 900 |
|---|---|---|---|---|---|---|---|---|---|
| `#EAF2FB` | `#D2E4F6` | `#A8C9EC` | `#7CADE1` | `#5495D6` | `#3E8EDE` | **`#2C6FB3`** | `#245C95` | `#1E4A78` | `#1E3A5F` |

`--sb-primary-600` is the default action colour; `700` hover, `800` active, `50/100` tints for backgrounds.

**Accent (green)**

| 50 | 100 | 200 | 300 | 400 | 500 | **600** | 700 | 800 | 900 |
|---|---|---|---|---|---|---|---|---|---|
| `#EBF5E9` | `#D4EACF` | `#ABD7A3` | `#7FC375` | `#5BB052` | `#4FA945` | **`#46A33E`** | `#357E30` | `#2A6326` | `#1F491D` |

## Color — neutrals (warm)

Warm-tinted grays (a nod to the cream paper) — the canvas for the product UI.

| Token | Hex | Typical use |
|---|---|---|
| `--sb-neutral-0` | `#FFFFFF` | Cards, surfaces |
| `--sb-neutral-25` | `#FBFBF7` | App background (warm off-white) |
| `--sb-neutral-50` | `#F6F6F0` | Sunken/zebra rows |
| `--sb-neutral-100` | `#ECEBE2` | Subtle fills, dividers |
| `--sb-neutral-200` | `#DAD8CC` | Borders |
| `--sb-neutral-300` | `#BFBDB0` | Strong borders, disabled |
| `--sb-neutral-400` | `#98968A` | Subtle text, placeholders |
| `--sb-neutral-500` | `#6F6E63` | Muted text |
| `--sb-neutral-600` | `#54534A` | Secondary text |
| `--sb-neutral-700` | `#3D3C35` | Body on light (alt) |
| `--sb-neutral-800` | `#2A2924` | Strong text |
| `--sb-neutral-900` | `#1A1A16` | Primary text (ink) |

## Color — semantic

Each maps to a brand colour but is tuned for legible UI (text uses the darker `-fg`).

| Intent | `-fg` (text/icon) | base | `-bg` (tint) | `-border` |
|---|---|---|---|---|
| **Success** | `#2A6326` | `#46A33E` | `#EBF5E9` | `#ABD7A3` |
| **Danger** | `#A52A24` | `#E23B33` | `#FBE8E6` | `#F3B4B0` |
| **Warning** | `#8A6A00` | `#F3C12E` | `#FEF6DD` | `#F5E2A0` |
| **Info** | `#1E4A78` | `#3E8EDE` | `#E8F2FC` | `#A8C9EC` |

> Mustard, mint, and pink **fail** contrast as small text on white — use them as fills/icons/borders, with the `-fg` value for any text.

## Color — subject accents

For colour-coding subjects/specializations (tags, calendar, thumbnails), echoing the timetable's colourful blocks. Use the tint as background + the deep value as text.

| Accent | base | tint (bg) | deep (text) |
|---|---|---|---|
| Blue | `#3E8EDE` | `#E8F2FC` | `#1E4A78` |
| Green | `#46A33E` | `#EBF5E9` | `#2A6326` |
| Red | `#E23B33` | `#FBE8E6` | `#A52A24` |
| Mustard | `#F3C12E` | `#FEF6DD` | `#8A6A00` |
| Mint | `#86C7A6` | `#EAF5EF` | `#2F6B52` |
| Orange | `#E8743B` | `#FCEDE3` | `#A24A1E` |
| Purple | `#6E3FA6` | `#F0E9F8` | `#4A2A72` |
| Pink | `#E85FA8` | `#FCE8F2` | `#9A2E64` |

## Semantic UI roles

Aliases the product UI references (so components never hard-code a ramp step):

| Token | Value | Use |
|---|---|---|
| `--sb-bg` | `neutral-25` | App background |
| `--sb-surface` | `neutral-0` | Cards, sheets, menus |
| `--sb-surface-sunken` | `neutral-50` | Wells, zebra rows |
| `--sb-border` | `neutral-200` | Default borders/dividers |
| `--sb-border-strong` | `neutral-300` | Inputs, emphasis |
| `--sb-text` | `neutral-900` | Primary text |
| `--sb-text-muted` | `neutral-500` | Secondary text |
| `--sb-text-subtle` | `neutral-400` | Placeholders, hints |
| `--sb-primary` / `-hover` / `-active` | `primary-600/700/800` | Primary actions, links |
| `--sb-accent` | `accent-600` | Secondary emphasis |
| `--sb-on-primary` | `#FFFFFF` | Text/icon on primary |
| `--sb-link` / `-hover` | `primary-600 / 700` | Hyperlinks |
| `--sb-focus-ring` | `rgba(62,142,222,.45)` | Focus outline |

## Typography

**Families**

| Token | Stack | Use |
|---|---|---|
| `--sb-font-ui` | `"Nunito Sans", ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif` | **All UI & body text** |
| `--sb-font-display` | `"Permanent Marker", "Nunito Sans", cursive` | Brand marker headings (swap in the real brand font when embedded) |
| `--sb-font-script` | `"Caveat", "Permanent Marker", cursive` | Brush-script accents (brand layer only) |
| `--sb-font-mono` | `ui-monospace, "Cascadia Code", "Fira Code", Menlo, Consolas, monospace` | Codes, serials |

**Scale** (root 16px)

| Token | rem / px | Typical |
|---|---|---|
| `--sb-text-xs` | 0.75 / 12 | Captions, table meta |
| `--sb-text-sm` | 0.875 / 14 | Body-sm, labels, buttons |
| `--sb-text-base` | 1 / 16 | Body |
| `--sb-text-md` | 1.125 / 18 | Lead body, h4 |
| `--sb-text-lg` | 1.25 / 20 | h3 |
| `--sb-text-xl` | 1.5 / 24 | h2 |
| `--sb-text-2xl` | 1.875 / 30 | h1 |
| `--sb-text-3xl` | 2.25 / 36 | Page hero |
| `--sb-text-4xl` | 3 / 48 | Brand display |
| `--sb-text-5xl` | 3.75 / 60 | Brand display |
| `--sb-text-6xl` | 4.5 / 72 | Brand hero (marker) |

**Weights** (Nunito Sans): 400 regular · 600 semibold · 700 bold · 800 extrabold · 900 black.
**Line-heights:** tight 1.15 · snug 1.3 · normal 1.5 · relaxed 1.65.
**Letter-spacing:** tight −0.01em · normal 0 · wide 0.02em · wider 0.06em (eyebrows/labels).

**Roles**

| Role | Font | Size | Weight | LH |
|---|---|---|---|---|
| Brand display | display (marker) | 4xl–6xl | — | tight |
| H1 | ui | 2xl (30) | 800 | snug |
| H2 | ui | xl (24) | 800 | snug |
| H3 | ui | lg (20) | 700 | snug |
| H4 | ui | md (18) | 700 | snug |
| Body | ui | base (16) | 400 | normal |
| Body-sm | ui | sm (14) | 400 | normal |
| Label | ui | sm (14) | 600 | snug |
| Button | ui | sm–base | 700 | 1 |
| Caption | ui | xs (12) | 600 | snug |

## Spacing

4px base unit. `--sb-space-{n}`:

| 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 10 | 12 | 16 | 20 | 24 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 4 | 8 | 12 | 16 | 20 | 24 | 28 | 32 | 40 | 48 | 64 | 80 | 96 |

Defaults: input/button padding `12/16`; card padding `24`; section gap `48–64`.

## Radius

| Token | px | Use |
|---|---|---|
| `--sb-radius-xs` | 4 | Tags, tiny chips |
| `--sb-radius-sm` | 6 | Inputs (compact), checkboxes |
| `--sb-radius-md` | 10 | **Default** — buttons, inputs, cards |
| `--sb-radius-lg` | 14 | Cards, modals |
| `--sb-radius-xl` | 20 | Hero/marketing cards |
| `--sb-radius-2xl` | 28 | Large brand panels |
| `--sb-radius-pill` | 999px | Pills, segmented controls |
| `--sb-radius-circle` | 50% | Avatars, icon buttons |

## Shadow & elevation

Soft, warm-tinted (alpha of ink). Keep elevation low — the brand is friendly, not glossy.

| Token | Value | Use |
|---|---|---|
| `--sb-shadow-xs` | `0 1px 2px rgba(26,26,22,.06)` | Hairline lift |
| `--sb-shadow-sm` | `0 2px 6px rgba(26,26,22,.08)` | **Default** cards, buttons |
| `--sb-shadow-md` | `0 6px 16px rgba(26,26,22,.10)` | Dropdowns, popovers |
| `--sb-shadow-lg` | `0 14px 32px rgba(26,26,22,.12)` | Modals |
| `--sb-shadow-xl` | `0 24px 48px rgba(26,26,22,.16)` | Marketing/feature |
| `--sb-shadow-focus` | `0 0 0 3px var(--sb-focus-ring)` | Focus ring |

## Motion

| Token | Value |
|---|---|
| `--sb-dur-fast` | 150ms (hover, small state) |
| `--sb-dur` | 250ms (default transitions) |
| `--sb-dur-slow` | 400ms (overlays, page) |
| `--sb-ease-standard` | `cubic-bezier(.2,.8,.2,1)` (friendly) |
| `--sb-ease-out` | `cubic-bezier(0,0,.2,1)` |
| `--sb-ease-in` | `cubic-bezier(.4,0,1,1)` |

Respect `prefers-reduced-motion`: drop non-essential transitions.

## Z-index

base `0` · dropdown `1000` · sticky `1100` · overlay `1200` · modal `1300` · toast `1400` · tooltip `1500`.

## Breakpoints

Mobile-first (phone/tablet/desktop are all first-class — see the platform NFRs).

| Token | min-width |
|---|---|
| `--sb-bp-sm` | 480px (large phone) |
| `--sb-bp-md` | 768px (tablet) |
| `--sb-bp-lg` | 1024px (small desktop) |
| `--sb-bp-xl` | 1280px |
| `--sb-bp-2xl` | 1536px |

## Iconography

- **Style:** outline icons, ~1.75px stroke, **rounded** caps & joins (matches the friendly brand), 24px grid (20px dense, 16px inline).
- **Recommended set:** [Lucide](https://lucide.dev) or [Phosphor](https://phosphoricons.com) — both have rounded variants.
- **Brand glyphs** (sun/spiral, crown, apple, megaphone) are illustrative accents for the brand layer — not functional UI icons.
- Icon colour follows text (`currentColor`); use semantic `-fg` colours for status icons.

---

➡️ Next: [01 — Brand layer](01-brand.md) · [03 — Components](03-components.md)
