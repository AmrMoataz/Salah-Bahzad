/**
 * Combobox — dropdown with autocomplete (typeahead filtering)
 *
 * A text input that filters a list of options as you type. Keyboard
 * navigable, clears with ×, and falls back to a "no matches" row.
 *
 * @startingPoint section="Forms" subtitle="Autocomplete" viewport="360x140"
 */
function Combobox({ label, value, onChange, options = [], placeholder = 'Type to search…', disabled, error, required, emptyText = 'No matches' }) {
  const [open, setOpen] = React.useState(false);
  const [query, setQuery] = React.useState('');
  const [active, setActive] = React.useState(0);
  const rootRef = React.useRef(null);
  const selected = options.find((o) => o.value === value);

  // Show selected label in the field when closed; the live query while open.
  const display = open ? query : (selected ? selected.label : '');
  const filtered = React.useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!open || !q) return options;
    return options.filter((o) => o.label.toLowerCase().includes(q));
  }, [query, options, open]);

  React.useEffect(() => {
    if (!open) return;
    const onDoc = (e) => { if (rootRef.current && !rootRef.current.contains(e.target)) close(); };
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, [open, query]);

  const close = () => { setOpen(false); setQuery(''); };
  const choose = (o) => { if (o.disabled) return; onChange?.(o.value); close(); };

  const onKey = (e) => {
    if (e.key === 'ArrowDown') { e.preventDefault(); setOpen(true); setActive((i) => Math.min(filtered.length - 1, i + 1)); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActive((i) => Math.max(0, i - 1)); }
    else if (e.key === 'Enter') { e.preventDefault(); if (open && filtered[active]) choose(filtered[active]); }
    else if (e.key === 'Escape') { close(); }
  };

  const borderColor = error ? 'var(--sb-danger)' : open ? 'var(--sb-primary)' : 'var(--sb-border-strong)';

  return (
    <div ref={rootRef} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--sb-space-1)', fontFamily: 'var(--sb-font-sans)', position: 'relative' }}>
      {label && (
        <label style={{ fontSize: 'var(--sb-label-lg-size)', fontWeight: 600, color: 'var(--sb-text)' }}>
          {label}{required && <span style={{ color: 'var(--sb-danger)' }}> *</span>}
        </label>
      )}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 'var(--sb-space-2)', height: 40, padding: '0 12px',
        border: `1px solid ${borderColor}`, borderRadius: 'var(--sb-radius-md)',
        background: disabled ? 'var(--sb-surface-sunken)' : 'var(--sb-surface)',
        boxShadow: open ? 'var(--sb-shadow-focus)' : 'none', opacity: disabled ? 0.55 : 1,
        transition: 'border-color var(--sb-timing-fast), box-shadow var(--sb-timing-fast)',
      }}>
        <span aria-hidden style={{ color: 'var(--sb-text-subtle)', fontSize: 15, lineHeight: 1 }}>⌕</span>
        <input
          type="text" value={display} placeholder={placeholder} disabled={disabled}
          onChange={(e) => { setQuery(e.target.value); setOpen(true); setActive(0); }}
          onFocus={() => setOpen(true)} onKeyDown={onKey} role="combobox" aria-expanded={open} aria-autocomplete="list"
          style={{
            flex: 1, minWidth: 0, border: 'none', outline: 'none', background: 'transparent',
            fontFamily: 'inherit', fontSize: 'var(--sb-body-md-size)', color: 'var(--sb-text)',
            cursor: disabled ? 'not-allowed' : 'text',
          }}
        />
        {selected && !disabled && (
          <button type="button" aria-label="Clear" onMouseDown={(e) => e.preventDefault()} onClick={() => { onChange?.(''); close(); }}
            style={{ border: 'none', background: 'var(--sb-neutral-100)', color: 'var(--sb-text-muted)', width: 20, height: 20, borderRadius: '50%', cursor: 'pointer', fontSize: 13, lineHeight: 1, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>×</button>
        )}
        <span aria-hidden style={{ color: 'var(--sb-text-muted)', fontSize: 12, transform: open ? 'rotate(180deg)' : 'none', transition: 'transform var(--sb-timing-fast)' }}>▾</span>
      </div>

      {open && (
        <ul role="listbox" style={{
          position: 'absolute', top: 'calc(100% + 6px)', left: 0, right: 0, zIndex: 50, margin: 0,
          listStyle: 'none', padding: 'var(--sb-space-1)', maxHeight: 240, overflowY: 'auto',
          background: 'var(--sb-surface)', border: '1px solid var(--sb-border)',
          borderRadius: 'var(--sb-radius-md)', boxShadow: 'var(--sb-shadow-lg)',
        }}>
          {filtered.length === 0 && (
            <li style={{ padding: '10px', fontSize: 'var(--sb-body-md-size)', color: 'var(--sb-text-subtle)', textAlign: 'center' }}>{emptyText}</li>
          )}
          {filtered.map((o, i) => {
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

window.SBCombobox = Combobox;
