import { Injectable, inject } from '@angular/core';
import { ToastService } from '../toast/toast.service';

/**
 * Copy-to-clipboard helper (mirrors the prototype's `copyLatex`/`fallbackCopy`). Uses the async
 * Clipboard API where available and falls back to a hidden `<textarea>` + `execCommand('copy')` for
 * older/insecure contexts. On success it shows an info toast; on total failure it nudges the user to
 * press Ctrl/Cmd+C. Returns whether the copy succeeded.
 */
@Injectable({ providedIn: 'root' })
export class ClipboardService {
  readonly #toast = inject(ToastService);

  async copy(text: string, toastMessage?: string): Promise<boolean> {
    const ok = await this.#write(text);
    if (ok) {
      this.#toast.info(toastMessage ?? `Copied  ${text}`, 'Copied');
    } else {
      this.#toast.info('Press Ctrl/Cmd+C to copy', 'Copy');
    }
    return ok;
  }

  async #write(text: string): Promise<boolean> {
    if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
      try {
        await navigator.clipboard.writeText(text);
        return true;
      } catch {
        // Permission denied / insecure context — fall through to the legacy path.
      }
    }
    return this.#fallbackCopy(text);
  }

  #fallbackCopy(text: string): boolean {
    if (typeof document === 'undefined') return false;
    try {
      const ta = document.createElement('textarea');
      ta.value = text;
      ta.setAttribute('readonly', '');
      ta.style.position = 'fixed';
      ta.style.top = '0';
      ta.style.left = '-9999px';
      ta.style.opacity = '0';
      document.body.appendChild(ta);
      ta.select();
      ta.setSelectionRange(0, text.length);
      const ok = document.execCommand('copy');
      document.body.removeChild(ta);
      return ok;
    } catch {
      return false;
    }
  }
}
