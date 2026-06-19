import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

interface Segment {
  kind: 'text' | 'inline' | 'block';
  value: string;
}

const SYMBOLS: ReadonlyArray<readonly [RegExp, string]> = [
  [/\\times/g, '×'], [/\\cdot/g, '·'], [/\\div/g, '÷'],
  [/\\pm/g, '±'], [/\\mp/g, '∓'],
  [/\\leq?/g, '≤'], [/\\geq?/g, '≥'], [/\\neq?/g, '≠'], [/\\approx/g, '≈'],
  [/\\infty/g, '∞'], [/\\to|\\rightarrow/g, '→'], [/\\leftarrow/g, '←'],
  [/\\Delta/g, 'Δ'], [/\\delta/g, 'δ'], [/\\alpha/g, 'α'], [/\\beta/g, 'β'],
  [/\\gamma/g, 'γ'], [/\\theta/g, 'θ'], [/\\lambda/g, 'λ'], [/\\mu/g, 'μ'],
  [/\\pi/g, 'π'], [/\\omega/g, 'ω'], [/\\Omega/g, 'Ω'], [/\\phi/g, 'φ'],
  [/\\sum/g, '∑'], [/\\int/g, '∫'], [/\\sqrt/g, '√'],
  [/\\degree|\\circ/g, '°'],
];

const SUP: Record<string, string> = {
  '0': '⁰', '1': '¹', '2': '²', '3': '³', '4': '⁴', '5': '⁵', '6': '⁶',
  '7': '⁷', '8': '⁸', '9': '⁹', '+': '⁺', '-': '⁻', '=': '⁼', '(': '⁽',
  ')': '⁾', 'n': 'ⁿ', 'i': 'ⁱ',
};
const SUB: Record<string, string> = {
  '0': '₀', '1': '₁', '2': '₂', '3': '₃', '4': '₄', '5': '₅', '6': '₆',
  '7': '₇', '8': '₈', '9': '₉', '+': '₊', '-': '₋', '=': '₌', '(': '₍',
  ')': '₎', 'a': 'ₐ', 'e': 'ₑ', 'x': 'ₓ', 'o': 'ₒ',
};

function mapScript(body: string, table: Record<string, string>): string | null {
  let out = '';
  for (const ch of body) {
    if (table[ch] === undefined) return null;
    out += table[ch];
  }
  return out;
}

/** Best-effort LaTeX → readable text. Not a full engine — enough for an at-a-glance preview. */
function cleanMath(input: string): string {
  let s = input;
  s = s.replace(/\\[,;:!]|\\ /g, ' ');
  s = s.replace(/\\(?:mathrm|mathbf|mathit|text|mathsf|operatorname)\s*\{([^}]*)\}/g, '$1');
  s = s.replace(/\\frac\s*\{([^}]*)\}\s*\{([^}]*)\}/g, '($1)/($2)');
  s = s.replace(/\\sqrt\s*\{([^}]*)\}/g, '√($1)');
  for (const [re, rep] of SYMBOLS) s = s.replace(re, rep);
  s = s.replace(/\^\{([^}]*)\}|\^(\S)/g, (_m, braced, single) => {
    const body = braced ?? single ?? '';
    return mapScript(body, SUP) ?? `^${body}`;
  });
  s = s.replace(/_\{([^}]*)\}|_(\S)/g, (_m, braced, single) => {
    const body = braced ?? single ?? '';
    return mapScript(body, SUB) ?? `_${body}`;
  });
  s = s.replace(/[{}]/g, '').replace(/\s+/g, ' ').trim();
  return s;
}

/**
 * Lightweight LaTeX preview (design-system question preview). Renders <code>$inline$</code> and
 * <code>$$block$$</code> math as the prototype's monospace "math chips" with best-effort symbol
 * cleanup, and plain prose as text. Intentionally dependency-free (no KaTeX/MathJax bundle) so it
 * stays build-safe and lazy-friendly; the chip styling mirrors the prototype's live preview exactly.
 */
@Component({
  selector: 'sb-latex-preview',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (segments().length === 0) {
      <span class="lx__empty">{{ placeholder() }}</span>
    } @else {
      @for (seg of segments(); track $index) {
        @switch (seg.kind) {
          @case ('text') { <span class="lx__text">{{ seg.value }}</span> }
          @case ('inline') { <code class="lx__math">{{ seg.value }}</code> }
          @case ('block') { <code class="lx__math lx__math--block">{{ seg.value }}</code> }
        }
      }
    }
  `,
  styles: [`
    :host { display: block; font-size: 15px; line-height: 1.7; color: var(--sb-text); }

    .lx__empty { color: var(--sb-text-subtle); font-size: var(--sb-body-md-size); }
    .lx__text { white-space: pre-wrap; }

    .lx__math {
      font-family: var(--sb-font-mono);
      background: var(--sb-primary-50);
      color: var(--sb-primary-800);
      padding: 1px 6px;
      border-radius: var(--sb-radius-xs);
      white-space: pre-wrap;
    }
    .lx__math--block {
      display: block;
      margin: var(--sb-space-2) 0;
      padding: var(--sb-space-3);
      text-align: center;
      border-radius: var(--sb-radius-md);
    }
  `],
})
export class LatexPreviewComponent {
  readonly latex = input<string>('');
  readonly placeholder = input<string>('Nothing to preview yet.');

  readonly segments = computed<Segment[]>(() => this.#parse(this.latex() ?? ''));

  #parse(src: string): Segment[] {
    const text = src.trim();
    if (!text) return [];
    const out: Segment[] = [];
    // Split on $$...$$ (block) and $...$ (inline); keep the delimiters' contents.
    const re = /\$\$([\s\S]+?)\$\$|\$([^$]+?)\$/g;
    let last = 0;
    let m: RegExpExecArray | null;
    while ((m = re.exec(text)) !== null) {
      if (m.index > last) out.push({ kind: 'text', value: text.slice(last, m.index) });
      if (m[1] != null) out.push({ kind: 'block', value: cleanMath(m[1]) });
      else out.push({ kind: 'inline', value: cleanMath(m[2]) });
      last = re.lastIndex;
    }
    if (last < text.length) out.push({ kind: 'text', value: text.slice(last) });
    return out;
  }
}
