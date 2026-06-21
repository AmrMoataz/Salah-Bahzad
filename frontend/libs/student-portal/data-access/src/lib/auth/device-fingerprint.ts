// Builds the secondary device signal sent as `X-Device-Fingerprint` on the student exchange
// (FR-STU-DEV-001..003 / FR-PLAT-DEV-006). It is intentionally *not* the authoritative binding:
// the real device token is the server-managed, HttpOnly `sb_device` cookie (§1.3) that this SPA
// never reads or writes. The fingerprint is a stable, low-entropy client id + a UA/platform summary
// for staff visibility ("Android · Chrome"-style) and a soft second factor.

const FP_KEY = 'sb_device_fp';

/** A random, stable per-browser id, persisted in localStorage (survives reloads, not incognito). */
function stableClientId(): string {
  try {
    let id = localStorage.getItem(FP_KEY);
    if (!id) {
      id =
        typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
          ? crypto.randomUUID()
          : `fp-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
      localStorage.setItem(FP_KEY, id);
    }
    return id;
  } catch {
    // Storage blocked (private mode / disabled) — fall back to an ephemeral id.
    return 'fp-unavailable';
  }
}

/** A coarse, human-readable OS + browser guess from the UA string (best-effort, never throws). */
function uaSummary(): string {
  const ua = (typeof navigator !== 'undefined' && navigator.userAgent) || 'unknown';

  const os =
    /Windows/i.test(ua) ? 'Windows' :
    /Android/i.test(ua) ? 'Android' :
    /iPhone|iPad|iPod/i.test(ua) ? 'iOS' :
    /Mac OS X|Macintosh/i.test(ua) ? 'macOS' :
    /Linux/i.test(ua) ? 'Linux' : 'Unknown OS';

  const browser =
    /Edg\//i.test(ua) ? 'Edge' :
    /OPR\/|Opera/i.test(ua) ? 'Opera' :
    /Chrome\//i.test(ua) ? 'Chrome' :
    /Firefox\//i.test(ua) ? 'Firefox' :
    /Safari\//i.test(ua) ? 'Safari' : 'Browser';

  return `${os} - ${browser}`;
}

/**
 * The value for the `X-Device-Fingerprint` header on the exchange: a stable client id joined
 * with the UA/platform summary. The backend persists this as `StudentDevice.FingerprintSummary`.
 */
export function getDeviceFingerprint(): string {
  // HTTP header values must be ASCII (RFC 9110 field-value); Kestrel rejects non-ASCII request
  // headers with an empty-body 400 *before* the endpoint runs. Use an ASCII separator and strip
  // anything outside printable ASCII so the header is always valid (caught in S0 wiring).
  const raw = `${stableClientId()} - ${uaSummary()}`;
  return raw.replace(/[^\x20-\x7E]/g, '');
}
