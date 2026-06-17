# Brand Layer

The expressive side of Salah Bahzad — used on social posts, marketing/landing pages, auth & splash screens, empty states, and thumbnails. It's loud on purpose. (For functional screens, switch to the [product-UI layer](03-components.md).) All colours/fonts referenced here are defined in [Foundations](02-foundations.md).

## Contents

- [Brand personality & voice](#brand-personality--voice)
- [Logo system](#logo-system)
- [The SB monogram](#the-sb-monogram)
- [Motifs](#motifs)
- [The mascot](#the-mascot)
- [Social-post archetypes](#social-post-archetypes)
- [Brand layout & color use](#brand-layout--color-use)

---

## Brand personality & voice

Salah Bahzad is a real person — an electrical engineer turned mathematics tutor (8+ years) for secondary students. The brand should feel like him: **confident, approachable, energetic, and a little playful**, but always *clear* (he teaches hard maths simply).

- **Voice:** friendly and direct, encouraging, lightly humorous. Speaks *to* the student ("you've got this"), not down at them.
- **Tone by context:** upbeat for announcements/marketing; calm and plain for instructions, grades, and errors.
- **Avoid:** stiff corporate copy, heavy jargon, anything that feels like a generic ed-tech template.

## Logo system

Four expressions of the mark — pick by context:

| Mark | Description | Use when |
|---|---|---|
| **Primary wordmark** | "SALAH ᴹ BAHZAD" stacked, hand-drawn **marker** lettering | Default brand signature — posts, headers, footers |
| **Sun lockup** | "SALAH ☼ BAHZAD" with the sun/spiral glyph between the words | Hero/marketing where there's room for the horizontal lockup |
| **Monogram (SB + crown)** | "SB" ligature with a small crown | Avatars, favicon, app icon, tight spaces |
| **Mascot + wordmark** | The bearded character beside the wordmark | Friendly/marketing contexts, intro banners |

**Colour variants:** ink (`--sb-ink`) on light, **white** on colour/photo, and a **navy** (`--sb-navy`) variant. Always use a provided variant — don't recolour the lettering arbitrarily.

**Usage rules**

- **Clear space:** keep padding around the mark equal to the cap-height of the "S" (≈ the crown's height for the monogram). Nothing crowds the logo.
- **Minimum size:** wordmark ≥ 120px wide on screen; monogram ≥ 24px.
- **Backgrounds:** ensure contrast — white mark on `--sb-blue`/`--sb-green`/photos; ink mark on `--sb-cream`/white.
- **Don't:** stretch/skew, add drop shadows or outlines, recolour to non-brand colours, re-typeset the wordmark in a different font, or place the ink mark on a busy/low-contrast background.

## The SB monogram

The "SB + crown" is the compact identity. Use it for: the **app icon**, **favicon**, **avatar fallback** (when a student/staff has no photo — see `Avatar` in components), and any square/circular placement. Keep the crown intact; don't separate it from the letters.

## Motifs

Supporting graphic elements that signal "Salah Bahzad" without the full logo:

- **Sun / spiral** — the optimistic signature glyph; use as a small accent, bullet, or divider.
- **Crown** — pairs with SB; sparingly, for "premium/achievement" moments (e.g. top score).
- **Chalk math formulas** — faint white equations on a blue field (as in the profile art); great for hero/auth backgrounds. Keep low-contrast so it never competes with foreground text.
- **Apple, megaphone, hand-drawn arrows/squiggles** — playful spot illustrations for education/announcement contexts.

Use **one** dominant motif per composition; motifs are seasoning.

## The mascot

The bearded character (blue tee, navy trousers, white sneakers) is a full **character system**, not a single image. Observed poses: running, pointing up, sitting/relaxing (sunglasses), waving, surprised, falling, thumbs-up, painting. Build/maintain a small library so the right emotion is always available.

**When to use which:**

| Mood | Pose | Use |
|---|---|---|
| Welcoming | waving / pointing | Onboarding, hero, sign-up |
| Positive | thumbs-up / running | Success, completion, "apply now" |
| Thinking/teaching | pointing up | Tips, explanations, lesson intros |
| Empty/quiet | sitting/relaxing | Empty states ("nothing here yet") |
| Oops | surprised / falling | Errors, 404, failed actions |

**Rules:** one mascot per view; give it room (don't crop awkwardly unless peeking from an edge — the "peeking from behind a block" treatment is on-brand); keep it on a calm background so it reads; never stretch or recolour the character.

## Social-post archetypes

Reusable templates the brand already uses — keep these consistent:

1. **4-block thumbnail** — a 2×2 grid of `--sb-navy` / `--sb-red` / `--sb-green` / `--sb-mustard`, a big marker heading (e.g. "Assignment 1", "Introduction"), a subtitle (e.g. "Calculus [3rd Sec]"), and the mascot peeking from a corner. For lessons, assignments, series.
2. **Cream typographic poster** — `--sb-cream` background, oversized marker word(s) in 2–3 brand colours ("WE ARE HIRING", "APPLY NOW", "ANNOUNCEMENT"), small wordmark bottom-corner, optional mascot. For announcements/recruiting.
3. **Diagonal brand split** — blue/green diagonal, intro copy on one side, big lockup + mascot on the other (the "Who is Salah Bahzad?" banner). For about/intro.
4. **Mascot-in-circle on formula field** — bright-blue chalk-formula background, mascot in a `--sb-green` circle inside a white square frame. For profile/avatar/feature.
5. **Script highlight** — purple/colour field with flowing **script** ("Thursday", "Statistics") + marker details. For schedule/teasers.

## Brand layout & color use

- Lead with **one or two** brand colours per piece; let the rest support. Blue+green is the core pairing; red/mustard/orange/purple are energy accents.
- Big **marker** headline + **rounded-sans** supporting text — never the reverse.
- Keep generous margins; let the colour blocks and the mascot breathe.
- Maintain contrast: white or ink text on saturated blocks (test mustard/mint — they need ink, not white).

---

➡️ Next: [03 — Components (product UI)](03-components.md) · [02 — Foundations](02-foundations.md)
