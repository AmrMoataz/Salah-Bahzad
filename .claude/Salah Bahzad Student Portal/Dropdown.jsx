/**
 * Dropdown — custom popover single-select (richer than native Select)
 *
 * Supports per-option icon + description, keyboard navigation, and a
 * selected check. Use when options need more than a plain text label.
 *
 * @startingPoint section="Forms" subtitle="Dropdown menu" viewport="360x140"
 */
function Dropdown({ label, value, onChange, options = [], placeholder = 'Select an option', disabled, error, required }) {
  const [open, setOpen] = React.useState(false);
  const [active, setActive] = React.useState(-1);
  const rootRef = React.useRef(null);
  const selected = options.find((o) => o.value === value);

  React.useEffect(() => {
    if (!open) return;
    const onDoc = (e) => { if (rootRef.current && !rootRef.current.contains(e.target)) setOpen(false); };
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, [open]);

  React.useEffect(() => {
    if (open) setActive(options.findIndex((o) => o.value === value));
  }, [open]);

  const choose = (o) => { if (o.disabled) return; onChange?.(o.value); setOpen(false); };

  const onKey = (e) => {
    if (disabled) return;
    if (e.key === 'ArrowDown' || (e.key === 'Enter' && !open)) {
      e.preventDefault();
      if (!open) { setOpen(true); return; }
      setActive((i) => Math.min(options.length - 1, i + 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault(); setActive((i) => Math.max(0, i - 1));
    } else if (e.key === 'Enter' && open) {
      e.preventDefault(); if (options[active]) choose(options[active]);
    } else if (e.key === 'Escape') { setOpen(false); }
  };

  const borderColor = error ? 'var(--sb-danger)' : open ? 'var(--sb-primary)' : 'var(--sb-border-strong)';

  return (
    <div ref={rootRef} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--sb-space-1)', fontFamily: 'var(--sb-font-sans)', position: 'relative' }}>
      {label && (
        <label style={{ fontSize: 'var(--sb-label-lg-size)', fontWeight: 600, color: 'var(--sb-text)' }}>
          {label}{required && <span style={{ color: 'var(--sb-danger)' }}> *</span>}
        </label>
      )}
      <button
        type="button" disabled={disabled} onClick={() => !disabled && setOpen((v) => !v)} onKeyDown={onKey}
        aria-haspopup="listbox" aria-expanded={open}
        style={{
          display: 'flex', alignItems: 'center', gap: 'var(--sb-space-2)', width: '100%', height: 40,
          padding: '0 12px', textAlign: 'left', fontFamily: 'inherit', fontSize: 'var(--sb-body-md-size)',
          color: selected ? 'var(--sb-text)' : 'var(--sb-text-subtle)',
          border: `1px solid ${borderColor}`, borderRadius: 'var(--sb-radius-md)',
          background: disabled ? 'var(--sb-surface-sunken)' : 'var(--sb-surface)',
          boxShadow: open ? 'var(--sb-shadow-focus)' : 'none',
          cursor: disabled ? 'not-allowed' : 'pointer', opacity: disabled ? 0.55 : 1, outline: 'none',
          transition: 'border-color var(--sb-timing-fast), box-shadow var(--sb-timing-fast)',
        }}
      >
        {selected?.icon && <span aria-hidden style={{ fontSize: 16, lineHeight: 1 }}>{selected.icon}</span>}
        <span style={{ flex: 1, minWidth: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {selected ? selected.label : placeholder}
        </span>
        <span aria-hidden style={{ color: 'var(--sb-text-muted)', fontSize: 12, transform: open ? 'rotate(180deg)' : 'none', transition: 'transform var(--sb-timing-fast)' }}>▾</span>
      </button>

      {open && (
        <ul role="listbox" style={{
          position: 'absolute', top: 'calc(100% + 6px)', left: 0, right: 0, zIndex: 50, margin: 0,
          listStyle: 'none', padding: 'var(--sb-space-1)', maxHeight: 240, overflowY: 'auto',
          background: 'var(--sb-surface)', border: '1px solid var(--sb-border)',
          borderRadius: 'var(--sb-radius-md)', boxShadow: 'var(--sb-shadow-lg)',
        }}>
          {options.map((o, i) => {
            const isSel = o.value === value, isAct = i === active;
            return (
              <li
                key={o.value} role="option" aria-selected={isSel}
                onMouseEnter={() => setActive(i)} onMouseDown={(e) => e.preventDefault()} onClick={() => choose(o)}
                style={{
                  display: 'flex', alignItems: 'center', gap: 'var(--sb-space-2)', padding: '8px 10px',
                  borderRadius: 'var(--sb-radius-sm)', cursor: o.disabled ? 'not-allowed' : 'pointer',
                  background: isAct && !o.disabled ? 'var(--sb-primary-50)' : 'transparent',
                  color: o.disabled ? 'var(--sb-text-subtle)' : 'var(--sb-text)', opacity: o.disabled ? 0.6 : 1,
                }}
              >
                {o.icon && <span aria-hidden style={{ fontSize: 16, lineHeight: 1, flexShrink: 0 }}>{o.icon}</span>}
                <span style={{ flex: 1, minWidth: 0 }}>
                  <span style={{ display: 'block', fontSize: 'var(--sb-body-md-size)', fontWeight: isSel ? 700 : 500, lineHeight: 1.3 }}>{o.label}</span>
                  {o.description && <span style={{ display: 'block', fontSize: 'var(--sb-body-sm-size)', color: 'var(--sb-text-muted)', lineHeight: 1.3 }}>{o.description}</span>}
                </span>
                {isSel && <span aria-hidden style={{ color: 'var(--sb-primary)', fontSize: 13, flexShrink: 0 }}>✓</span>}
              </li>
            );
          })}
        </ul>
      )}
      {error && <span style={{ fontSize: 'var(--sb-body-sm-size)', color: 'var(--sb-danger-fg)' }}>{error}</span>}
    </div>
  );
}

window.Dropdown = Dropdown;
if (typeof module !== 'undefined') module.exports = { Dropdown };
