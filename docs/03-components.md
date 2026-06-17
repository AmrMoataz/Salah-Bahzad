# Product-UI Components

The functional component set for the admin and student portals (and the app's functional screens). Brand-tinted but **calm and legible** — UI text is always `--sb-font-ui` (never marker/script), surfaces are warm-white, and saturated colour carries meaning. Everything references [Foundations](02-foundations.md) tokens.

General rules for every component: visible **focus ring** (`--sb-shadow-focus`), `disabled` at ~45% with `not-allowed`, hit targets ≥ 40px on touch, transitions `--sb-dur` `--sb-ease-standard`, respect `prefers-reduced-motion`.

## Contents

- [Buttons](#buttons) · [Links](#links)
- [Inputs & form fields](#inputs--form-fields) · [Selection controls](#selection-controls) · [Code input](#code-input)
- [Cards](#cards) · [Stat / KPI card](#stat--kpi-card)
- [Tags, badges & status pills](#tags-badges--status-pills)
- [Tables](#tables) · [Pagination](#pagination)
- [Navigation shells](#navigation-shells) · [Tabs](#tabs) · [Breadcrumb](#breadcrumb)
- [Modal / dialog](#modal--dialog) · [Drawer](#drawer)
- [Alerts & toasts](#alerts--toasts)
- [Avatar](#avatar) · [Progress](#progress) · [Stepper](#stepper)
- [Empty states](#empty-states) · [Skeleton](#skeleton) · [Tooltip](#tooltip)

---

## Buttons

Radius `--sb-radius-md`; weight 700; height **md 40px** (default), **sm 32px**, **lg 48px**; padding `0 16px` (md). Icon+label gap `8px`.

| Variant | Fill / text | Hover | Use |
|---|---|---|---|
| **Primary** | `--sb-primary` bg / white | `--sb-primary-hover` | The one main action per view |
| **Accent** | `--sb-accent` bg / white | `accent-700` | Positive secondary (e.g. "Approve") |
| **Secondary (outline)** | transparent / `--sb-text`; `1px --sb-border-strong` | `--sb-surface-sunken` bg | Secondary actions |
| **Ghost** | transparent / `--sb-text` | `--sb-surface-sunken` | Toolbar/low-emphasis |
| **Danger** | `--sb-danger` bg / white | `#C7322B` | Destructive (delete, reject) |
| **Danger-ghost** | transparent / `--sb-danger-fg` | `--sb-danger-bg` | Destructive, low-emphasis |

States: `:hover` darken, `:active` `800`/translateY(1px), `:focus-visible` ring, `disabled` 45%, `loading` spinner + label, keep width. **Icon-only** buttons are square (`--sb-radius-md` or circle), need `aria-label`.

## Links

`--sb-link`, no underline at rest, underline on hover; visited inherits. In body copy, underline always for clarity.

## Inputs & form fields

**Input/textarea/select:** height 40px (md), padding `0 12px`, `1px --sb-border-strong`, radius `--sb-radius-md`, bg `--sb-surface`, text `--sb-text`, placeholder `--sb-text-subtle`.

States: `:hover` border `neutral-400`; `:focus` border `--sb-primary` + `--sb-shadow-focus`; `error` border `--sb-danger` + `--sb-danger` helper text; `disabled` `--sb-surface-sunken`; readonly no border emphasis.

**Field anatomy:** Label (`label` role, 14/600) → control → hint (`xs`, `--sb-text-muted`) or error (`xs`, `--sb-danger-fg`). Required marked with `*` in `--sb-danger`. Always associate `<label for>`; errors via `aria-describedby` + `aria-invalid`.

## Selection controls

- **Checkbox / radio:** 18px box, radius `sm` (checkbox) / circle (radio), checked = `--sb-primary`; focus ring; label clickable.
- **Switch:** 36×20 track, `--sb-neutral-300` off → `--sb-primary` on, white knob; `role="switch"`.

## Code input

For redeeming enrollment codes (and any segmented token): 3 groups of segmented boxes (matches the `XXXX-XXXX-XXXX` serial), `--sb-radius-md`, monospaced, auto-advance, **paste fills all**, focus ring per box. Invalid → danger border + message.

## Cards

`--sb-surface`, radius `--sb-radius-lg`, `--sb-shadow-sm`, padding `--sb-space-6` (24). Optional header (title + actions), body, footer. Interactive cards: hover `--sb-shadow-md` + slight lift; whole-card link needs a focusable element.

## Stat / KPI card

Dashboard metric: big number (`2xl`/800), label (`sm`/`--sb-text-muted`), optional delta (success/danger `-fg`), optional sparkline/icon in a tinted circle (subject/semantic). Used for "pending approvals", "active students", "codes remaining".

## Tags, badges & status pills

- **Tag (subject/specialization):** subject-accent **tint bg** + **deep text** (see Foundations → subject accents), radius `--sb-radius-pill`, `xs`/600, padding `2px 10px`.
- **Badge (count):** small circular/pill, `--sb-primary` or `--sb-danger` (notifications).
- **Status pill:** semantic tint + `-fg`. Canonical mappings:

| Domain | Value → pill |
|---|---|
| Student status | Pending → warning · Active → success · Rejected → danger · Inactive → neutral |
| Code status | Active → success · Inactive → neutral · Used → info · Deleted → danger |
| Quiz state | Passed → success · Failed → danger · Not started → neutral |
| Enrollment | In-progress → info · Completed → success · Expired → neutral · Locked → warning |

## Tables

The admin workhorse — optimised for density and scanning.

- Header row: `--sb-surface-sunken`, `xs`/700/`wider` letter-spacing, `--sb-text-muted`, sticky on scroll.
- Rows: 48px (comfortable) / 40px (compact); `1px --sb-border` bottom; **zebra** optional (`neutral-50`); hover `primary-50`.
- Cells: 12–16px padding; numbers right-aligned + `tabular-nums`.
- **Row actions:** ghost icon buttons revealed on hover / always on touch; destructive in danger-ghost.
- Selection: leading checkbox column; selected row `primary-50`.
- Sort: header click, arrow indicator; one active sort.
- **Responsive:** below `--sb-bp-md`, collapse to stacked "label: value" cards (don't horizontally scroll dense admin tables on phones).
- States: loading → skeleton rows; empty → [empty state](#empty-states); error → inline alert + retry.

## Pagination

Page size select + range text ("1–20 of 240") + prev/next + numbered pages (truncated with ellipsis). Current page `--sb-primary` fill; others ghost. Keyboard accessible; `aria-current="page"`.

## Navigation shells

**Sidebar (admin):** fixed left, `--sb-surface`, `1px --sb-border` right; logo/monogram top; grouped nav items (icon + label, 40px, radius `md`); active item `primary-50` bg + `--sb-primary` text + 3px left accent bar; collapsible to icons; becomes a drawer below `--sb-bp-lg`.

**Topbar:** `--sb-surface`, `--sb-shadow-xs`, height 60px; left = page title/breadcrumb, right = search, notifications, avatar menu. On mobile, hamburger toggles the sidebar drawer.

**Student shell:** lighter — topbar + optional left nav; on phone, a bottom tab bar (Home, My Sessions, Assignments, Profile) is acceptable.

## Tabs

Underline style: row of labels, active = `--sb-primary` text + 2px underline; hover `--sb-text`. `role="tablist"`; arrow-key navigation; panels `role="tabpanel"`.

## Breadcrumb

`sm`/`--sb-text-muted`, `/` or chevron separators, last item `--sb-text` (current, not a link).

## Modal / dialog

Center sheet, `--sb-surface`, radius `--sb-radius-lg`, `--sb-shadow-lg`, max-width 480 (confirm) / 640 (form); scrim `rgba(26,26,22,.45)`. Header (title + close), body, footer (right-aligned: secondary + primary). **Destructive confirms** use a danger primary and name the object ("Delete code ABCD-… ?"). Trap focus, `Esc` closes, return focus to trigger, `role="dialog"` + `aria-modal`.

## Drawer

Edge sheet (right for detail panels, left for mobile nav). Same surface/scrim as modal; slides with `--sb-ease-standard`. Use for the admin "student detail" side panel and mobile navigation.

## Alerts & toasts

- **Inline alert / banner:** semantic `-bg` + `-border` + `-fg`, icon, radius `--sb-radius-md`. For form-level errors, page notices.
- **Toast:** bottom (mobile) / top-right (desktop), `--sb-surface` + left accent bar in semantic colour, `--sb-shadow-md`, auto-dismiss ~5s (errors persist), max ~3 stacked. `role="status"` (polite) / `alert` (errors).

## Avatar

Circle (`--sb-radius-circle`); photo when available, else initials on a subject-accent tint, else the **SB monogram**. Sizes 24/32/40/64. Status dot (online/approved) optional. Group = overlapping with "+N".

## Progress

- **Linear:** 8px track `--sb-neutral-100`, fill `--sb-primary`; for video-watch / completion.
- **Circular (score):** ring showing assignment/quiz **score %** (the brand's circle-progress motif). Colour by result: pass → `--sb-accent`, fail → `--sb-danger`, in-progress → `--sb-primary`. Center = `%` in `lg`/800. Provide `aria-valuenow`.

## Stepper

Horizontal (desktop) / vertical (mobile) numbered steps for the **registration wizard** and **enroll modal**. Completed = `--sb-accent` check; current = `--sb-primary` ring; upcoming = `--sb-neutral-300`. Connector line tints as you progress.

## Empty states

Centered: a **mascot** pose (sitting/relaxing for "nothing yet", surprised for errors), a short headline (`lg`/700), one line of guidance (`--sb-text-muted`), and a primary action. Keep it warm and brief — this is where the brand personality is welcome in the product UI.

## Skeleton

Shimmer placeholders (`--sb-neutral-100` → `50` sweep) matching final layout (text bars, avatars, table rows). Prefer skeletons over spinners for content; use a spinner only inside buttons/inline.

## Tooltip

Dark (`--sb-neutral-800`) bubble, white text, `xs`, `--sb-radius-sm`, `--sb-shadow-md`, 8px offset; show on hover + focus; never put essential-only info in a tooltip (touch can't hover).

---

➡️ Back to [README](README.md) · [02 — Foundations](02-foundations.md) · [01 — Brand](01-brand.md)
