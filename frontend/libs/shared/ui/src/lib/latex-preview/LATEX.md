# LaTeX Authoring Reference — `sb-latex-preview`

This documents exactly what the homegrown LaTeX previewer (`latex-preview.component.ts`)
supports. It is **not** a full math engine (no KaTeX/MathJax) — it is a dependency-free
string-substitution pass used for an at-a-glance authoring preview (e.g. the question
editor body field). Everything below reflects what the code actually does; anything it
doesn't recognize **passes through as raw text without erroring**.

## 1. Mental model

The previewer does three things:

1. Splits text into **prose** and **math** based on `$` delimiters.
2. Leaves prose untouched (newlines preserved via `white-space: pre-wrap`).
3. Runs math through a fixed list of find-and-replace rules + Unicode super/subscript mapping.

## 2. Delimiters

| Syntax       | Result                              | Notes                                  |
| ------------ | ----------------------------------- | -------------------------------------- |
| `$ ... $`    | **Inline** chip (flows with text)   | Content cannot contain another `$`     |
| `$$ ... $$`  | **Block** chip (centered, own line) | Content **can span multiple lines**    |
| *(no `$`)*   | Plain prose                         | Newlines preserved, no math processing |

- An **unclosed `$`** is left as a literal `$` — nothing breaks, but no math renders.
- There is **no way to escape a `$`** — avoid lone dollar signs in prose (e.g. prices).
- Inside math, **runs of spaces collapse to one** and leading/trailing spaces are trimmed.

## 3. Symbols & operators

| Command                 | Output | Command                | Output |
| ----------------------- | ------ | ---------------------- | ------ |
| `\times`                | ×      | `\pm`                  | ±      |
| `\cdot`                 | ·      | `\mp`                  | ∓      |
| `\div`                  | ÷      | `\approx`              | ≈      |
| `\leq` *(or `\le`)*     | ≤      | `\neq` *(or `\ne`)*    | ≠      |
| `\geq` *(or `\ge`)*     | ≥      | `\infty`               | ∞      |
| `\sum`                  | ∑      | `\int`                 | ∫      |
| `\to` *(or `\rightarrow`)* | →   | `\degree` *(or `\circ`)* | °    |

## 4. Greek letters

| Command   | Output | Command   | Output | Command   | Output |
| --------- | ------ | --------- | ------ | --------- | ------ |
| `\alpha`  | α      | `\beta`   | β      | `\gamma`  | γ      |
| `\delta`  | δ      | `\Delta`  | Δ      | `\theta`  | θ      |
| `\lambda` | λ      | `\mu`     | μ      | `\pi`     | π      |
| `\phi`    | φ      | `\omega`  | ω      | `\Omega`  | Ω      |

Only `\Delta` and `\Omega` are supported as capitals.

## 5. Superscripts & subscripts

Two forms: **single character** (`x^2`) or **braced group** (`x^{12}`).

```
x^2        ->  x²
x^{12}     ->  x¹²
x^{n+1}    ->  xⁿ⁺¹
a_1        ->  a₁
a_{12}     ->  a₁₂
```

**Critical limitation:** mapping only works for characters that have a Unicode
super/subscript. If **any** character in the group is unsupported, the whole group falls
back to literal text (`x^a` stays `x^a`).

- **Superscript supports:** `0–9  +  -  =  (  )  n  i`
- **Subscript supports:** `0–9  +  -  =  (  )  a  e  x  o`

So `v_x` -> `vₓ` works, but `v_i` -> stays `v_i` (no subscript "i").

## 6. Fractions & roots

| You type             | You get   |
| -------------------- | --------- |
| `\frac{a}{b}`        | `(a)/(b)` |
| `\sqrt{x}`           | `√(x)`    |
| `\sqrt` *(no braces)*| `√`       |

**Limitation:** `\frac` and `\sqrt{...}` only read a **single, flat brace group**. Nested
braces break them:

- `\frac{1}{2}` -> `(1)/(2)` ✅
- `\frac{x^{2}}{y}` -> not converted ❌ (nested `{}` defeats the matcher)

Workaround: prefer the single-char power form so braces aren't needed
(`\frac{x^2}{y}` works because `x^2` needs no braces).

## 7. Text & units

| You type            | You get | Use for          |
| ------------------- | ------- | ---------------- |
| `\mathrm{m/s}`      | `m/s`   | Upright units    |
| `\text{if } x>0`    | `if x>0`| Words inside math|
| `\mathbf{v}`        | `v`     | (unwraps only — no bold) |
| `\mathit` `\mathsf` `\operatorname` | unwrapped | — |

These commands **strip the wrapper and keep the contents** — no visual styling is applied.
They mainly keep your LaTeX valid for a future real renderer.

## 8. Spacing

| You type                              | You get  |
| ------------------------------------- | -------- |
| `\,`  `\;`  `\:`  `\!`  `\ ` (backslash-space) | a space |

Handy for units: `4\,\mathrm{m/s^2}` -> `4 m/s²`.

## 9. Worked examples

```
A car accelerates from rest at $a = 4\,\mathrm{m/s^2}$. Find its speed after $t = 5\,\mathrm{s}$.
-> ...at  a = 4 m/s²  ...after  t = 5 s.

Solve $$x = \frac{-b \pm \sqrt{b^2 - 4ac}}{2a}$$
-> block chip:  x = (-b ± √(b²-4ac))/(2a)

Given $\theta = 30\degree$ and $\omega \approx 2\pi$, find the period.
-> θ = 30°  ...  ω ≈ 2π

The sum $\sum a_n$ converges as $n \to \infty$.
-> ∑ aₙ  ...  n → ∞
```

## 10. Things that silently DON'T work

The previewer never errors, so these look fine to the author but render as raw text:

- **Unsupported super/subscript chars** — `x^a`, `v_i`, `^\theta`.
- **Nested braces** in `\frac`/`\sqrt` — `\frac{x^{2}}{y}`.
- **Anything not in the tables above** — matrices, `\binom`, `\lim`, `\cases`,
  `\overline`, `\vec`, `\hat`, integrals with limits, aligned environments, etc.
- **Visual stacking** — fractions are always inline `(a)/(b)`; roots are `√(x)`.
- **Prefix collisions** — commands starting with `\le`, `\ge`, `\ne` get partially eaten
  (e.g. `\leftarrow` becomes `≤ftarrow`). Use `\to`/`\rightarrow` for arrows; avoid
  `\leftarrow`, `\left`, `\right`.
- **Bold/italic/fonts** — `\mathbf`, `\mathit` etc. unwrap but apply no styling.
- **Literal `$`** — no escape; don't put stray dollar signs in prose.

## 11. One-page cheat sheet

```
DELIMITERS   $inline$            $$block$$
FRACTIONS    \frac{a}{b}         \sqrt{x}        (flat braces only)
POWERS       x^2  x^{n+1}        a_1  a_{12}     (limited char set)
GREEK        \alpha \beta \theta \pi \mu \lambda \omega \phi
             \Delta \Omega
OPERATORS    \times \cdot \div \pm \mp
             \leq \geq \neq \approx
             \sum \int \infty \to \degree
UNITS/TEXT   \mathrm{m/s}  \text{...}           (unwrap only)
SPACING      \,  \;  \:                          (-> space)
AVOID        \leftarrow \left \right  nested {}  lone $  x^a  v_i
```

---

> If full LaTeX fidelity (matrices, stacked fractions, arbitrary super/subscripts) is ever
> required — especially on the student-facing render path — replace this component with
> **KaTeX** (preferred over MathJax for live preview). This file documents the current,
> intentionally-lightweight behavior only.
