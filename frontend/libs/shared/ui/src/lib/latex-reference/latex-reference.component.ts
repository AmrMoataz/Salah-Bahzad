import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { ClipboardService } from '../clipboard/clipboard.service';
import { DrawerComponent } from '../drawer/drawer.component';
import { LatexPreviewComponent } from '../latex-preview/latex-preview.component';

/** [command, rendered glyph] — a click-to-copy symbol tile. */
type Glyph = readonly [cmd: string, out: string];
/** [command, rendered, note?] — a click-to-copy input→output row. */
type Io = readonly [cmd: string, out: string, note?: string];
/** [you-type, preview-source] — a worked example (preview is rendered live). */
type Example = readonly [raw: string, preview: string];

/**
 * LaTeX reference drawer (design `latexRefModal`, FR-ADM-QB-001). A right-side sheet documenting the
 * lightweight previewer's supported syntax. Every symbol tile and input→output row is click-to-copy,
 * and the worked examples copy their raw source — all via {@link ClipboardService}. Pairs with the
 * "Formatting help" button on the question editor's body card.
 */
@Component({
  selector: 'sb-latex-reference',
  standalone: true,
  imports: [DrawerComponent, LatexPreviewComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <sb-drawer [open]="open()" title="LaTeX reference" [width]="660" (close)="close.emit()">
      <div class="lr">
        <!-- Intro -->
        <div class="lr__intro">
          <span class="lr__intro-icon" aria-hidden="true">☼</span>
          <span>
            A <strong>lightweight previewer</strong> — not full LaTeX. Anything it doesn’t recognise
            renders as plain text. Tap any tile to copy it.
          </span>
        </div>

        <!-- Operators -->
        <section class="lr__block">
          <div class="lr__head"><span class="lr__dot" aria-hidden="true"></span><h3 class="lr__h">Operators</h3></div>
          <div class="lr__glyphs">
            @for (g of operators; track g[0]) {
              <button type="button" class="sb-glyph" (click)="copy(g[0])">
                <span class="sb-glyph__out">{{ g[1] }}</span>
                <span class="sb-glyph__cmd">{{ g[0] }}</span>
              </button>
            }
          </div>
        </section>

        <!-- Greek -->
        <section class="lr__block">
          <div class="lr__head"><span class="lr__dot" aria-hidden="true"></span><h3 class="lr__h">Greek</h3></div>
          <div class="lr__glyphs">
            @for (g of greek; track g[0]) {
              <button type="button" class="sb-glyph" (click)="copy(g[0])">
                <span class="sb-glyph__out">{{ g[1] }}</span>
                <span class="sb-glyph__cmd">{{ g[0] }}</span>
              </button>
            }
          </div>
        </section>

        <!-- Delimiters -->
        <section class="lr__block">
          <div class="lr__head"><span class="lr__dot" aria-hidden="true"></span><h3 class="lr__h">Delimiters</h3></div>
          <div class="lr__rows">
            @for (r of delimiters; track r[0]) {
              <button type="button" class="sb-ltx-row" (click)="copy(r[0])">
                <code class="sb-ltx-row__cmd">{{ r[0] }}</code>
                <span class="sb-ltx-row__arrow" aria-hidden="true">→</span>
                <span class="sb-ltx-row__out">{{ r[1] }}</span>
                @if (r[2]) { <span class="sb-ltx-row__note">{{ r[2] }}</span> }
              </button>
            }
          </div>
        </section>

        <!-- Powers / Fractions (two columns) -->
        <section class="lr__block lr__cols">
          <div>
            <div class="lr__head"><span class="lr__dot" aria-hidden="true"></span><h3 class="lr__h">Powers &amp; subscripts</h3></div>
            <div class="lr__rows">
              @for (r of powers; track r[0]) {
                <button type="button" class="sb-ltx-row" (click)="copy(r[0])">
                  <code class="sb-ltx-row__cmd">{{ r[0] }}</code>
                  <span class="sb-ltx-row__arrow" aria-hidden="true">→</span>
                  <span class="sb-ltx-row__out">{{ r[1] }}</span>
                  @if (r[2]) { <span class="sb-ltx-row__note">{{ r[2] }}</span> }
                </button>
              }
            </div>
          </div>
          <div>
            <div class="lr__head"><span class="lr__dot" aria-hidden="true"></span><h3 class="lr__h">Fractions &amp; roots</h3></div>
            <div class="lr__rows">
              @for (r of roots; track r[0]) {
                <button type="button" class="sb-ltx-row" (click)="copy(r[0])">
                  <code class="sb-ltx-row__cmd">{{ r[0] }}</code>
                  <span class="sb-ltx-row__arrow" aria-hidden="true">→</span>
                  <span class="sb-ltx-row__out">{{ r[1] }}</span>
                  @if (r[2]) { <span class="sb-ltx-row__note">{{ r[2] }}</span> }
                </button>
              }
            </div>
          </div>
        </section>

        <!-- Text, units & spacing -->
        <section class="lr__block">
          <div class="lr__head"><span class="lr__dot" aria-hidden="true"></span><h3 class="lr__h">Text, units &amp; spacing</h3></div>
          <div class="lr__rows">
            @for (r of textUnits; track r[0]) {
              <button type="button" class="sb-ltx-row" (click)="copy(r[0])">
                <code class="sb-ltx-row__cmd">{{ r[0] }}</code>
                <span class="sb-ltx-row__arrow" aria-hidden="true">→</span>
                <span class="sb-ltx-row__out">{{ r[1] }}</span>
                @if (r[2]) { <span class="sb-ltx-row__note">{{ r[2] }}</span> }
              </button>
            }
          </div>
        </section>

        <!-- Worked examples -->
        <section class="lr__block">
          <div class="lr__head"><span class="lr__dot" aria-hidden="true"></span><h3 class="lr__h">Worked examples</h3></div>
          <div class="lr__examples">
            @for (ex of examples; track ex[0]) {
              <button type="button" class="lr__example" (click)="copy(ex[0], 'Copied example')">
                <div class="lr__example-type">
                  <div class="lr__example-label">You type</div>
                  <code class="lr__example-raw">{{ ex[0] }}</code>
                </div>
                <div class="lr__example-preview">
                  <div class="lr__example-label lr__example-label--accent">Preview</div>
                  <sb-latex-preview [latex]="ex[1]" />
                </div>
              </button>
            }
          </div>
        </section>

        <!-- Avoid -->
        <section class="lr__avoid">
          <div class="lr__avoid-head">
            <span class="lr__avoid-icon" aria-hidden="true">⚠</span>
            <h3 class="lr__h">Avoid — renders as raw text</h3>
          </div>
          <div class="lr__chips">
            @for (c of avoidChips; track c) {
              <code class="lr__chip">{{ c }}</code>
            }
          </div>
          <p class="lr__avoid-text">
            Super/subscripts only map a fixed character set (e.g. no <code>x^a</code> or <code>v_i</code>).
            Nested braces defeat <code>\\frac</code>/<code>\\sqrt</code>. There is no escape for <code>$</code> —
            keep stray dollar signs out of prose.
          </p>
        </section>
      </div>
    </sb-drawer>
  `,
  styles: [`
    :host { display: contents; }

    .lr__intro {
      display: flex;
      align-items: center;
      gap: var(--sb-space-3);
      font-size: var(--sb-body-md-size);
      line-height: 1.5;
      margin-bottom: var(--sb-space-6);
      padding: 11px 14px;
      background: var(--sb-info-bg);
      border: 1px solid var(--sb-info-border);
      border-radius: var(--sb-radius-md);
      color: var(--sb-text);
    }
    .lr__intro-icon { flex-shrink: 0; font-size: 16px; }

    .lr__block { margin-bottom: var(--sb-space-6); }
    .lr__cols { display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: var(--sb-space-5); }

    .lr__head { display: flex; align-items: baseline; gap: 10px; margin-bottom: var(--sb-space-3); }
    .lr__dot { width: 7px; height: 7px; border-radius: 50%; background: var(--sb-primary); flex-shrink: 0; }
    .lr__h {
      margin: 0;
      font-size: 12.5px;
      font-weight: 800;
      text-transform: uppercase;
      letter-spacing: 0.07em;
      color: var(--sb-text);
    }

    /* Symbol tiles */
    .lr__glyphs { display: grid; grid-template-columns: repeat(auto-fill, minmax(78px, 1fr)); gap: 8px; }
    .sb-glyph {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 7px;
      padding: 13px 6px;
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-md);
      cursor: pointer;
      font-family: var(--sb-font-sans);
      transition: border-color var(--sb-timing-fast) var(--sb-easing-standard),
                  background var(--sb-timing-fast) var(--sb-easing-standard),
                  transform var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .sb-glyph:hover { border-color: var(--sb-primary); background: var(--sb-primary-50); transform: translateY(-1px); }
    .sb-glyph:active { transform: translateY(0); }
    .sb-glyph:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }
    .sb-glyph__out { font-size: 23px; font-weight: 700; color: var(--sb-text); line-height: 1; }
    .sb-glyph__cmd { font-size: 11px; font-family: var(--sb-font-mono); color: var(--sb-primary-700); white-space: nowrap; }

    /* Input → output rows */
    .lr__rows { display: flex; flex-direction: column; gap: 7px; }
    .sb-ltx-row {
      display: flex;
      align-items: center;
      gap: 12px;
      width: 100%;
      text-align: left;
      padding: 9px 12px;
      background: var(--sb-surface);
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-md);
      cursor: pointer;
      font-family: var(--sb-font-sans);
      transition: border-color var(--sb-timing-fast) var(--sb-easing-standard),
                  background var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .sb-ltx-row:hover { border-color: var(--sb-primary); background: var(--sb-primary-50); }
    .sb-ltx-row:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }
    .sb-ltx-row__cmd { font-family: var(--sb-font-mono); font-size: 13px; color: var(--sb-primary-700); flex-shrink: 0; min-width: 88px; }
    .sb-ltx-row__arrow { color: var(--sb-text-subtle); flex-shrink: 0; }
    .sb-ltx-row__out { font-size: 16px; font-weight: 700; color: var(--sb-text); min-width: 44px; }
    .sb-ltx-row__note { font-size: 11.5px; color: var(--sb-text-subtle); margin-left: auto; text-align: right; }

    /* Worked examples */
    .lr__examples { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 12px; }
    .lr__example {
      display: block;
      width: 100%;
      text-align: left;
      padding: 0;
      border: 1px solid var(--sb-border);
      border-radius: var(--sb-radius-md);
      overflow: hidden;
      background: var(--sb-surface);
      cursor: pointer;
      font-family: var(--sb-font-sans);
      transition: border-color var(--sb-timing-fast) var(--sb-easing-standard);
    }
    .lr__example:hover { border-color: var(--sb-primary); }
    .lr__example:focus-visible { outline: none; box-shadow: var(--sb-shadow-focus); }
    .lr__example-type { padding: 11px 14px; background: var(--sb-surface-sunken); border-bottom: 1px solid var(--sb-border); }
    .lr__example-preview { padding: 11px 14px; }
    .lr__example-label {
      font-size: 10px;
      font-weight: 800;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      color: var(--sb-text-subtle);
      margin-bottom: 6px;
    }
    .lr__example-label--accent { color: var(--sb-primary-700); }
    .lr__example-raw { display: block; font-family: var(--sb-font-mono); font-size: 12.5px; line-height: 1.6; color: var(--sb-text); white-space: pre-wrap; }

    /* Avoid */
    .lr__avoid { padding: 14px 16px; background: var(--sb-warning-bg); border: 1px solid var(--sb-warning-border); border-radius: var(--sb-radius-md); }
    .lr__avoid-head { display: flex; align-items: center; gap: 8px; margin-bottom: 10px; }
    .lr__avoid-icon { color: var(--sb-warning-fg); font-size: 15px; }
    .lr__chips { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 10px; }
    .lr__chip { font-family: var(--sb-font-mono); font-size: 12.5px; background: var(--sb-surface); border: 1px solid var(--sb-warning-border); border-radius: var(--sb-radius-sm); padding: 3px 8px; color: var(--sb-text); }
    .lr__avoid-text { margin: 0; font-size: 12.5px; line-height: 1.6; color: var(--sb-text-muted); }
    .lr__avoid-text code { font-family: var(--sb-font-mono); }
  `],
})
export class LatexReferenceComponent {
  readonly #clipboard = inject(ClipboardService);

  readonly open = input<boolean>(false);
  readonly close = output<void>();

  copy(text: string, message?: string): void {
    void this.#clipboard.copy(text, message ?? `Copied  ${text}`);
  }

  readonly operators: readonly Glyph[] = [
    ['\\times', '×'], ['\\cdot', '·'], ['\\div', '÷'], ['\\pm', '±'], ['\\mp', '∓'],
    ['\\approx', '≈'], ['\\leq', '≤'], ['\\geq', '≥'], ['\\neq', '≠'], ['\\infty', '∞'],
    ['\\sum', '∑'], ['\\int', '∫'], ['\\to', '→'], ['\\degree', '°'],
  ];

  readonly greek: readonly Glyph[] = [
    ['\\alpha', 'α'], ['\\beta', 'β'], ['\\gamma', 'γ'], ['\\delta', 'δ'], ['\\Delta', 'Δ'],
    ['\\theta', 'θ'], ['\\lambda', 'λ'], ['\\mu', 'μ'], ['\\pi', 'π'], ['\\phi', 'φ'],
    ['\\omega', 'ω'], ['\\Omega', 'Ω'],
  ];

  readonly delimiters: readonly Io[] = [
    ['$ … $', 'inline', 'flows with text — no nested $'],
    ['$$ … $$', 'block', 'centered, own line — can span lines'],
  ];

  readonly powers: readonly Io[] = [
    ['x^2', 'x²', 'single char'], ['x^{12}', 'x¹²', 'braced group'],
    ['x^{n+1}', 'xⁿ⁺¹'], ['a_1', 'a₁', 'single char'], ['a_{12}', 'a₁₂', 'braced group'],
  ];

  readonly roots: readonly Io[] = [
    ['\\frac{a}{b}', '(a)/(b)', 'flat braces only'],
    ['\\sqrt{x}', '√(x)'], ['\\sqrt', '√'],
  ];

  readonly textUnits: readonly Io[] = [
    ['\\mathrm{m/s}', 'm/s', 'upright units'],
    ['\\text{if } x>0', 'if x>0', 'words inside math'],
    ['\\,  \\;  \\:', '␣', 'thin spaces — 4\\,\\mathrm{m/s}'],
  ];

  readonly examples: readonly Example[] = [
    ['A car starts at $a = 4\\,\\mathrm{m/s^2}$.\nSpeed after $t = 5\\,\\mathrm{s}$?', 'A car starts at $a = 4\\,\\mathrm{m/s^2}$. Speed after $t = 5\\,\\mathrm{s}$?'],
    ['$$x = \\frac{-b \\pm \\sqrt{b^2-4ac}}{2a}$$', '$$x = \\frac{-b \\pm \\sqrt{b^2-4ac}}{2a}$$'],
    ['Given $\\theta = 30\\degree$, $\\omega \\approx 2\\pi$.', 'Given $\\theta = 30\\degree$, $\\omega \\approx 2\\pi$.'],
    ['The sum $\\sum a_n$ as $n \\to \\infty$.', 'The sum $\\sum a_n$ as $n \\to \\infty$.'],
  ];

  readonly avoidChips: readonly string[] = ['x^a', 'v_i', '\\frac{x^{2}}{y}', '\\leftarrow', '\\left', '\\right', 'lone $'];
}
