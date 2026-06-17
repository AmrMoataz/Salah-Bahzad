/* @ds-bundle: {"format":3,"namespace":"SalahBahzadDesignSystem_1a8c19","components":[{"name":"Button","sourcePath":"components/buttons/Button.jsx"},{"name":"Table","sourcePath":"components/data/Table.jsx"},{"name":"Alert","sourcePath":"components/feedback/Alert.jsx"},{"name":"Avatar","sourcePath":"components/feedback/Avatar.jsx"},{"name":"Badge","sourcePath":"components/feedback/Badge.jsx"},{"name":"Chip","sourcePath":"components/feedback/Chip.jsx"},{"name":"Drawer","sourcePath":"components/feedback/Drawer.jsx"},{"name":"Modal","sourcePath":"components/feedback/Modal.jsx"},{"name":"Progress","sourcePath":"components/feedback/Progress.jsx"},{"name":"Skeleton","sourcePath":"components/feedback/Skeleton.jsx"},{"name":"Tag","sourcePath":"components/feedback/Tag.jsx"},{"name":"Timer","sourcePath":"components/feedback/Timer.jsx"},{"name":"Toast","sourcePath":"components/feedback/Toast.jsx"},{"name":"Tooltip","sourcePath":"components/feedback/Tooltip.jsx"},{"name":"Checkbox","sourcePath":"components/forms/Checkbox.jsx"},{"name":"CodeInput","sourcePath":"components/forms/CodeInput.jsx"},{"name":"DatePicker","sourcePath":"components/forms/DatePicker.jsx"},{"name":"FileUpload","sourcePath":"components/forms/FileUpload.jsx"},{"name":"Input","sourcePath":"components/forms/Input.jsx"},{"name":"Radio","sourcePath":"components/forms/Radio.jsx"},{"name":"SearchBar","sourcePath":"components/forms/SearchBar.jsx"},{"name":"Select","sourcePath":"components/forms/Select.jsx"},{"name":"Switch","sourcePath":"components/forms/Switch.jsx"},{"name":"Card","sourcePath":"components/layout/Card.jsx"},{"name":"EmptyState","sourcePath":"components/layout/EmptyState.jsx"},{"name":"StatCard","sourcePath":"components/layout/StatCard.jsx"},{"name":"Breadcrumb","sourcePath":"components/navigation/Breadcrumb.jsx"},{"name":"Pagination","sourcePath":"components/navigation/Pagination.jsx"},{"name":"Stepper","sourcePath":"components/navigation/Stepper.jsx"},{"name":"Tabs","sourcePath":"components/navigation/Tabs.jsx"}],"sourceHashes":{"components/buttons/Button.jsx":"98fe415331ae","components/data/Table.jsx":"f63519cf88be","components/feedback/Alert.jsx":"3d374b5524e2","components/feedback/Avatar.jsx":"92c87292bb94","components/feedback/Badge.jsx":"6ca3bf4ee4c8","components/feedback/Chip.jsx":"a6f1f79dd73d","components/feedback/Drawer.jsx":"5c311b7e52de","components/feedback/Modal.jsx":"874be7abc066","components/feedback/Progress.jsx":"05328ed2307c","components/feedback/Skeleton.jsx":"62de62b2d4f0","components/feedback/Tag.jsx":"95da0c1c4a22","components/feedback/Timer.jsx":"b3657dab368a","components/feedback/Toast.jsx":"16fca7b46ca9","components/feedback/Tooltip.jsx":"f57499cf2951","components/forms/Checkbox.jsx":"381958faca1f","components/forms/CodeInput.jsx":"a79ffe685436","components/forms/DatePicker.jsx":"45e151e01c03","components/forms/FileUpload.jsx":"b84205ba9249","components/forms/Input.jsx":"c345dfb07eef","components/forms/Radio.jsx":"44851f485d1c","components/forms/SearchBar.jsx":"e3b52f1c58df","components/forms/Select.jsx":"9381d736440a","components/forms/Switch.jsx":"9bddd2a5ebbc","components/layout/Card.jsx":"a7a6e9372581","components/layout/EmptyState.jsx":"899644e8d92e","components/layout/StatCard.jsx":"a09d6d103724","components/navigation/Breadcrumb.jsx":"ff628a6a3380","components/navigation/Pagination.jsx":"575c1dcc41c3","components/navigation/Stepper.jsx":"ee74dda3ecd7","components/navigation/Tabs.jsx":"4d7b53df58bf"},"inlinedExternals":[],"unexposedExports":[]} */

(() => {

const __ds_ns = (window.SalahBahzadDesignSystem_1a8c19 = window.SalahBahzadDesignSystem_1a8c19 || {});

const __ds_scope = {};

(__ds_ns.__errors = __ds_ns.__errors || []);

// components/buttons/Button.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/**
 * Button — product UI layer
 *
 * @startingPoint section="Buttons" subtitle="Primary action button" viewport="480x120"
 */
function Button({
  variant = 'primary',
  size = 'md',
  disabled = false,
  loading = false,
  iconOnly = false,
  children,
  style,
  ...props
}) {
  const [hover, setHover] = React.useState(false);
  const [active, setActive] = React.useState(false);
  const sizes = {
    sm: {
      height: 32,
      padding: iconOnly ? 0 : '0 12px',
      width: iconOnly ? 32 : undefined,
      font: 'var(--sb-body-md-size)'
    },
    md: {
      height: 40,
      padding: iconOnly ? 0 : '0 16px',
      width: iconOnly ? 40 : undefined,
      font: 'var(--sb-body-md-size)'
    },
    lg: {
      height: 48,
      padding: iconOnly ? 0 : '0 20px',
      width: iconOnly ? 48 : undefined,
      font: 'var(--sb-body-lg-size)'
    }
  }[size];
  const palette = {
    primary: {
      bg: 'var(--sb-primary)',
      hover: 'var(--sb-primary-hover)',
      activeBg: 'var(--sb-primary-active)',
      color: 'var(--sb-on-primary)',
      border: 'transparent',
      shadow: 'var(--sb-shadow-sm)'
    },
    accent: {
      bg: 'var(--sb-accent)',
      hover: 'var(--sb-accent-700)',
      activeBg: 'var(--sb-accent-800)',
      color: '#fff',
      border: 'transparent',
      shadow: 'var(--sb-shadow-sm)'
    },
    secondary: {
      bg: 'var(--sb-surface)',
      hover: 'var(--sb-surface-sunken)',
      activeBg: 'var(--sb-neutral-100)',
      color: 'var(--sb-text)',
      border: 'var(--sb-border-strong)',
      shadow: 'none'
    },
    ghost: {
      bg: 'transparent',
      hover: 'var(--sb-surface-sunken)',
      activeBg: 'var(--sb-neutral-100)',
      color: 'var(--sb-text)',
      border: 'transparent',
      shadow: 'none'
    },
    danger: {
      bg: 'var(--sb-danger)',
      hover: '#C7322B',
      activeBg: '#A52A24',
      color: '#fff',
      border: 'transparent',
      shadow: 'var(--sb-shadow-sm)'
    },
    'danger-ghost': {
      bg: 'transparent',
      hover: 'var(--sb-danger-bg)',
      activeBg: 'var(--sb-danger-bg)',
      color: 'var(--sb-danger-fg)',
      border: 'transparent',
      shadow: 'none'
    }
  }[variant];
  const bg = disabled ? palette.bg : active ? palette.activeBg : hover ? palette.hover : palette.bg;
  return /*#__PURE__*/React.createElement("button", _extends({
    disabled: disabled || loading,
    onMouseEnter: () => setHover(true),
    onMouseLeave: () => {
      setHover(false);
      setActive(false);
    },
    onMouseDown: () => setActive(true),
    onMouseUp: () => setActive(false),
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 'var(--sb-space-2)',
      fontFamily: 'var(--sb-font-sans)',
      fontWeight: 700,
      fontSize: sizes.font,
      lineHeight: 1,
      height: sizes.height,
      padding: sizes.padding,
      width: sizes.width,
      borderRadius: 'var(--sb-radius-md)',
      border: `1px solid ${palette.border}`,
      background: bg,
      color: palette.color,
      boxShadow: hover ? 'none' : palette.shadow,
      cursor: disabled || loading ? 'not-allowed' : 'pointer',
      opacity: disabled ? 0.45 : 1,
      transform: active && !disabled ? 'translateY(1px)' : 'none',
      transition: 'background var(--sb-timing-fast) var(--sb-easing-standard), transform var(--sb-timing-fast) var(--sb-easing-standard)',
      whiteSpace: 'nowrap',
      ...style
    }
  }, props), loading && /*#__PURE__*/React.createElement("span", {
    style: {
      width: 14,
      height: 14,
      borderRadius: '50%',
      border: '2px solid currentColor',
      borderTopColor: 'transparent',
      display: 'inline-block',
      animation: 'sb-spin 0.7s linear infinite'
    }
  }), children, /*#__PURE__*/React.createElement("style", null, '@keyframes sb-spin{to{transform:rotate(360deg)}}'));
}
Object.assign(__ds_scope, { Button });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/buttons/Button.jsx", error: String((e && e.message) || e) }); }

// components/data/Table.jsx
try { (() => {
/**
 * Table — admin data table with zebra rows, sort, and row hover
 *
 * @startingPoint section="Data" subtitle="Data table" viewport="720x320"
 */
function Table({
  columns = [],
  rows = [],
  zebra = true,
  sortKey,
  sortDir = 'asc',
  onSort,
  getRowKey
}) {
  const [hoverRow, setHoverRow] = React.useState(null);
  return /*#__PURE__*/React.createElement("div", {
    style: {
      border: '1px solid var(--sb-border)',
      borderRadius: 'var(--sb-radius-lg)',
      overflow: 'hidden',
      fontFamily: 'var(--sb-font-sans)',
      background: 'var(--sb-surface)'
    }
  }, /*#__PURE__*/React.createElement("table", {
    style: {
      width: '100%',
      borderCollapse: 'collapse'
    }
  }, /*#__PURE__*/React.createElement("thead", null, /*#__PURE__*/React.createElement("tr", {
    style: {
      background: 'var(--sb-surface-sunken)'
    }
  }, columns.map(col => {
    const sorted = sortKey === col.key;
    return /*#__PURE__*/React.createElement("th", {
      key: col.key,
      onClick: () => col.sortable && onSort?.(col.key),
      style: {
        textAlign: col.align || 'left',
        padding: '10px 16px',
        fontSize: 'var(--sb-label-sm-size)',
        fontWeight: 700,
        letterSpacing: '0.06em',
        textTransform: 'uppercase',
        color: 'var(--sb-text-muted)',
        borderBottom: '1px solid var(--sb-border)',
        whiteSpace: 'nowrap',
        cursor: col.sortable ? 'pointer' : 'default',
        userSelect: 'none'
      }
    }, col.label, col.sortable && /*#__PURE__*/React.createElement("span", {
      style: {
        marginLeft: 6,
        opacity: sorted ? 1 : 0.3
      }
    }, sorted && sortDir === 'desc' ? '▾' : '▴'));
  }))), /*#__PURE__*/React.createElement("tbody", null, rows.map((row, ri) => {
    const key = getRowKey ? getRowKey(row, ri) : ri;
    const hovered = hoverRow === key;
    const bg = hovered ? 'var(--sb-primary-50)' : zebra && ri % 2 ? 'var(--sb-neutral-50)' : 'var(--sb-surface)';
    return /*#__PURE__*/React.createElement("tr", {
      key: key,
      onMouseEnter: () => setHoverRow(key),
      onMouseLeave: () => setHoverRow(null),
      style: {
        background: bg,
        transition: 'background var(--sb-timing-fast)'
      }
    }, columns.map(col => /*#__PURE__*/React.createElement("td", {
      key: col.key,
      style: {
        textAlign: col.align || 'left',
        padding: '12px 16px',
        fontSize: 'var(--sb-body-md-size)',
        color: 'var(--sb-text)',
        borderBottom: ri === rows.length - 1 ? 'none' : '1px solid var(--sb-border)',
        fontVariantNumeric: col.align === 'right' ? 'tabular-nums' : 'normal'
      }
    }, col.render ? col.render(row[col.key], row) : row[col.key])));
  }))));
}
Object.assign(__ds_scope, { Table });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/data/Table.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Alert.jsx
try { (() => {
/**
 * Alert — inline banner for form-level and page notices
 *
 * @startingPoint section="Feedback" subtitle="Inline alert banner" viewport="520x110"
 */
function Alert({
  variant = 'info',
  title,
  children,
  onClose
}) {
  const c = {
    success: {
      fg: 'var(--sb-success-fg)',
      bg: 'var(--sb-success-bg)',
      border: 'var(--sb-success-border)',
      icon: '✓'
    },
    danger: {
      fg: 'var(--sb-danger-fg)',
      bg: 'var(--sb-danger-bg)',
      border: 'var(--sb-danger-border)',
      icon: '!'
    },
    warning: {
      fg: 'var(--sb-warning-fg)',
      bg: 'var(--sb-warning-bg)',
      border: 'var(--sb-warning-border)',
      icon: '!'
    },
    info: {
      fg: 'var(--sb-info-fg)',
      bg: 'var(--sb-info-bg)',
      border: 'var(--sb-info-border)',
      icon: 'i'
    }
  }[variant];
  return /*#__PURE__*/React.createElement("div", {
    role: "alert",
    style: {
      display: 'flex',
      gap: 'var(--sb-space-3)',
      alignItems: 'flex-start',
      padding: 'var(--sb-space-3) var(--sb-space-4)',
      background: c.bg,
      border: `1px solid ${c.border}`,
      borderRadius: 'var(--sb-radius-md)',
      fontFamily: 'var(--sb-font-sans)',
      color: c.fg
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      flexShrink: 0,
      width: 20,
      height: 20,
      borderRadius: '50%',
      background: c.fg,
      color: c.bg,
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 12,
      fontWeight: 800,
      marginTop: 1
    }
  }, c.icon), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      fontSize: 'var(--sb-body-md-size)',
      lineHeight: 1.5
    }
  }, title && /*#__PURE__*/React.createElement("div", {
    style: {
      fontWeight: 700,
      color: c.fg,
      marginBottom: children ? 2 : 0
    }
  }, title), children && /*#__PURE__*/React.createElement("div", {
    style: {
      color: 'var(--sb-text)'
    }
  }, children)), onClose && /*#__PURE__*/React.createElement("button", {
    onClick: onClose,
    "aria-label": "Dismiss",
    style: {
      border: 'none',
      background: 'none',
      color: c.fg,
      cursor: 'pointer',
      fontSize: 18,
      lineHeight: 1,
      padding: 0
    }
  }, "\xD7"));
}
Object.assign(__ds_scope, { Alert });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Alert.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Avatar.jsx
try { (() => {
/**
 * Avatar — photo, initials, or SB monogram fallback
 *
 * @startingPoint section="Feedback" subtitle="User avatar" viewport="320x90"
 */
function Avatar({
  size = 'md',
  src,
  alt,
  initials,
  subject = 'blue',
  status
}) {
  const dim = {
    xs: 24,
    sm: 32,
    md: 40,
    lg: 48,
    xl: 64
  }[size] || 40;
  const tint = `var(--sb-subject-${subject}-bg)`;
  const deep = `var(--sb-subject-${subject}-deep)`;
  const statusColor = {
    online: 'var(--sb-success)',
    approved: 'var(--sb-success)',
    pending: 'var(--sb-warning)',
    offline: 'var(--sb-neutral-300)'
  }[status];
  return /*#__PURE__*/React.createElement("span", {
    style: {
      position: 'relative',
      display: 'inline-flex'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      width: dim,
      height: dim,
      borderRadius: '50%',
      overflow: 'hidden',
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      background: src ? 'var(--sb-surface-sunken)' : tint,
      color: deep,
      fontFamily: 'var(--sb-font-sans)',
      fontWeight: 800,
      fontSize: dim * 0.4,
      border: '1px solid var(--sb-border)'
    }
  }, src ? /*#__PURE__*/React.createElement("img", {
    src: src,
    alt: alt || '',
    style: {
      width: '100%',
      height: '100%',
      objectFit: 'cover'
    }
  }) : initials || 'SB'), status && /*#__PURE__*/React.createElement("span", {
    style: {
      position: 'absolute',
      right: -1,
      bottom: -1,
      width: dim * 0.28,
      height: dim * 0.28,
      minWidth: 8,
      minHeight: 8,
      borderRadius: '50%',
      background: statusColor,
      border: '2px solid var(--sb-surface)'
    }
  }));
}
Object.assign(__ds_scope, { Avatar });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Avatar.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Badge.jsx
try { (() => {
/**
 * Badge — count / notification indicator
 *
 * @startingPoint section="Feedback" subtitle="Count badge" viewport="300x80"
 */
function Badge({
  count,
  variant = 'danger',
  dot = false,
  max = 99
}) {
  const bg = {
    primary: 'var(--sb-primary)',
    danger: 'var(--sb-danger)',
    success: 'var(--sb-success)',
    neutral: 'var(--sb-neutral-400)'
  }[variant];
  if (dot) {
    return /*#__PURE__*/React.createElement("span", {
      style: {
        display: 'inline-block',
        width: 8,
        height: 8,
        borderRadius: '50%',
        background: bg
      }
    });
  }
  const label = typeof count === 'number' && count > max ? `${max}+` : count;
  return /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      minWidth: 20,
      height: 20,
      padding: '0 6px',
      borderRadius: 'var(--sb-radius-pill)',
      background: bg,
      color: '#fff',
      fontFamily: 'var(--sb-font-sans)',
      fontSize: 'var(--sb-label-sm-size)',
      fontWeight: 700,
      lineHeight: 1
    }
  }, label);
}
Object.assign(__ds_scope, { Badge });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Badge.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Chip.jsx
try { (() => {
/**
 * Chip — removable filter / selection token
 *
 * @startingPoint section="Feedback" subtitle="Chip / filter token" viewport="360x70"
 */
function Chip({
  variant = 'default',
  children,
  onRemove,
  selected
}) {
  const c = {
    default: {
      bg: 'var(--sb-neutral-100)',
      fg: 'var(--sb-text)'
    },
    primary: {
      bg: 'var(--sb-primary-100)',
      fg: 'var(--sb-primary-800)'
    },
    success: {
      bg: 'var(--sb-success-bg)',
      fg: 'var(--sb-success-fg)'
    },
    warning: {
      bg: 'var(--sb-warning-bg)',
      fg: 'var(--sb-warning-fg)'
    },
    danger: {
      bg: 'var(--sb-danger-bg)',
      fg: 'var(--sb-danger-fg)'
    }
  }[variant];
  return /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: 'var(--sb-space-2)',
      padding: '4px 10px',
      borderRadius: 'var(--sb-radius-pill)',
      background: c.bg,
      color: c.fg,
      border: selected ? `1px solid ${c.fg}` : '1px solid transparent',
      fontFamily: 'var(--sb-font-sans)',
      fontSize: 'var(--sb-body-sm-size)',
      fontWeight: 600
    }
  }, children, onRemove && /*#__PURE__*/React.createElement("button", {
    onClick: onRemove,
    "aria-label": "Remove",
    style: {
      border: 'none',
      background: 'none',
      color: 'inherit',
      cursor: 'pointer',
      padding: 0,
      fontSize: 15,
      lineHeight: 1,
      opacity: 0.7
    }
  }, "\xD7"));
}
Object.assign(__ds_scope, { Chip });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Chip.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Drawer.jsx
try { (() => {
/**
 * Drawer — edge sheet for detail panels and mobile nav
 *
 * @startingPoint section="Feedback" subtitle="Drawer / side sheet" viewport="640x420"
 */
function Drawer({
  open,
  onClose,
  side = 'right',
  title,
  children,
  footer,
  width = 400
}) {
  if (!open) return null;
  const isRight = side === 'right';
  return /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'fixed',
      inset: 0,
      zIndex: 1300,
      fontFamily: 'var(--sb-font-sans)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    onClick: onClose,
    style: {
      position: 'absolute',
      inset: 0,
      background: 'var(--sb-scrim)'
    }
  }), /*#__PURE__*/React.createElement("div", {
    role: "dialog",
    "aria-modal": "true",
    style: {
      position: 'absolute',
      top: 0,
      bottom: 0,
      [isRight ? 'right' : 'left']: 0,
      width: '92%',
      maxWidth: width,
      background: 'var(--sb-surface)',
      boxShadow: 'var(--sb-shadow-lg)',
      display: 'flex',
      flexDirection: 'column'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      padding: 'var(--sb-space-4) var(--sb-space-5)',
      borderBottom: '1px solid var(--sb-border)'
    }
  }, /*#__PURE__*/React.createElement("h3", {
    style: {
      margin: 0,
      fontSize: 'var(--sb-heading-sm-size)',
      fontWeight: 700,
      color: 'var(--sb-text)'
    }
  }, title), /*#__PURE__*/React.createElement("button", {
    onClick: onClose,
    "aria-label": "Close",
    style: {
      border: 'none',
      background: 'none',
      cursor: 'pointer',
      fontSize: 22,
      lineHeight: 1,
      color: 'var(--sb-text-muted)',
      padding: 0
    }
  }, "\xD7")), /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1,
      overflow: 'auto',
      padding: 'var(--sb-space-5)',
      fontSize: 'var(--sb-body-md-size)',
      color: 'var(--sb-text)'
    }
  }, children), footer && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'flex-end',
      gap: 'var(--sb-space-3)',
      padding: 'var(--sb-space-4) var(--sb-space-5)',
      borderTop: '1px solid var(--sb-border)'
    }
  }, footer)));
}
Object.assign(__ds_scope, { Drawer });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Drawer.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Modal.jsx
try { (() => {
/**
 * Modal — centered dialog with scrim
 *
 * @startingPoint section="Feedback" subtitle="Dialog / modal" viewport="640x400"
 */
function Modal({
  open,
  onClose,
  title,
  children,
  footer,
  size = 'confirm'
}) {
  if (!open) return null;
  const maxWidth = size === 'form' ? 640 : 480;
  return /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'fixed',
      inset: 0,
      zIndex: 1300,
      fontFamily: 'var(--sb-font-sans)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    onClick: onClose,
    style: {
      position: 'absolute',
      inset: 0,
      background: 'var(--sb-scrim)'
    }
  }), /*#__PURE__*/React.createElement("div", {
    role: "dialog",
    "aria-modal": "true",
    style: {
      position: 'absolute',
      top: '50%',
      left: '50%',
      transform: 'translate(-50%, -50%)',
      width: '92%',
      maxWidth,
      maxHeight: '88vh',
      overflow: 'auto',
      background: 'var(--sb-surface)',
      borderRadius: 'var(--sb-radius-lg)',
      boxShadow: 'var(--sb-shadow-lg)'
    }
  }, title && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      padding: 'var(--sb-space-5) var(--sb-space-6)',
      borderBottom: '1px solid var(--sb-border)'
    }
  }, /*#__PURE__*/React.createElement("h3", {
    style: {
      margin: 0,
      fontSize: 'var(--sb-heading-sm-size)',
      fontWeight: 700,
      color: 'var(--sb-text)'
    }
  }, title), /*#__PURE__*/React.createElement("button", {
    onClick: onClose,
    "aria-label": "Close",
    style: {
      border: 'none',
      background: 'none',
      cursor: 'pointer',
      fontSize: 22,
      lineHeight: 1,
      color: 'var(--sb-text-muted)',
      padding: 0
    }
  }, "\xD7")), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: 'var(--sb-space-6)',
      fontSize: 'var(--sb-body-md-size)',
      color: 'var(--sb-text)',
      lineHeight: 1.5
    }
  }, children), footer && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'flex-end',
      gap: 'var(--sb-space-3)',
      padding: 'var(--sb-space-4) var(--sb-space-6)',
      borderTop: '1px solid var(--sb-border)'
    }
  }, footer)));
}
Object.assign(__ds_scope, { Modal });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Modal.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Progress.jsx
try { (() => {
/**
 * Progress — linear bar or circular score ring
 *
 * @startingPoint section="Feedback" subtitle="Progress bar & score ring" viewport="420x140"
 */
function Progress({
  value = 0,
  variant = 'primary',
  label,
  showValue,
  circular = false,
  size = 96,
  height = 8
}) {
  const v = Math.min(Math.max(value, 0), 100);
  const color = {
    primary: 'var(--sb-primary)',
    success: 'var(--sb-accent)',
    danger: 'var(--sb-danger)',
    warning: 'var(--sb-warning)',
    info: 'var(--sb-info)'
  }[variant] || 'var(--sb-primary)';
  if (circular) {
    const stroke = Math.max(6, size * 0.1);
    return /*#__PURE__*/React.createElement("div", {
      role: "progressbar",
      "aria-valuenow": v,
      "aria-valuemin": 0,
      "aria-valuemax": 100,
      style: {
        position: 'relative',
        width: size,
        height: size,
        fontFamily: 'var(--sb-font-sans)'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        width: '100%',
        height: '100%',
        borderRadius: '50%',
        background: `conic-gradient(${color} ${v * 3.6}deg, var(--sb-neutral-100) 0deg)`
      }
    }), /*#__PURE__*/React.createElement("div", {
      style: {
        position: 'absolute',
        inset: stroke,
        borderRadius: '50%',
        background: 'var(--sb-surface)',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center'
      }
    }, /*#__PURE__*/React.createElement("span", {
      style: {
        fontSize: size * 0.26,
        fontWeight: 800,
        color: 'var(--sb-text)'
      }
    }, v, "%"), label && /*#__PURE__*/React.createElement("span", {
      style: {
        fontSize: 'var(--sb-label-sm-size)',
        color: 'var(--sb-text-muted)'
      }
    }, label)));
  }
  return /*#__PURE__*/React.createElement("div", {
    style: {
      width: '100%',
      fontFamily: 'var(--sb-font-sans)'
    }
  }, (label || showValue) && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      justifyContent: 'space-between',
      marginBottom: 'var(--sb-space-2)',
      fontSize: 'var(--sb-body-sm-size)',
      color: 'var(--sb-text-muted)'
    }
  }, label && /*#__PURE__*/React.createElement("span", null, label), showValue && /*#__PURE__*/React.createElement("span", {
    style: {
      fontWeight: 700,
      color: 'var(--sb-text)'
    }
  }, v, "%")), /*#__PURE__*/React.createElement("div", {
    style: {
      width: '100%',
      height,
      background: 'var(--sb-neutral-100)',
      borderRadius: 'var(--sb-radius-pill)',
      overflow: 'hidden'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      height: '100%',
      width: v + '%',
      background: color,
      borderRadius: 'var(--sb-radius-pill)',
      transition: 'width var(--sb-timing) var(--sb-easing-standard)'
    }
  })));
}
Object.assign(__ds_scope, { Progress });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Progress.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Skeleton.jsx
try { (() => {
/**
 * Skeleton — shimmer placeholder
 *
 * @startingPoint section="Feedback" subtitle="Loading skeleton" viewport="360x140"
 */
function Skeleton({
  variant = 'text',
  width,
  height,
  lines = 1
}) {
  const base = {
    background: 'linear-gradient(90deg, var(--sb-neutral-100) 25%, var(--sb-neutral-50) 37%, var(--sb-neutral-100) 63%)',
    backgroundSize: '400% 100%',
    animation: 'sb-shimmer 1.4s ease infinite'
  };
  const keyframes = /*#__PURE__*/React.createElement("style", null, '@keyframes sb-shimmer{0%{background-position:100% 0}100%{background-position:-100% 0}}');
  if (variant === 'circle') {
    const d = width || height || 40;
    return /*#__PURE__*/React.createElement("span", {
      style: {
        display: 'inline-block',
        width: d,
        height: d,
        borderRadius: '50%',
        ...base
      }
    }, keyframes);
  }
  if (variant === 'rect') {
    return /*#__PURE__*/React.createElement("span", {
      style: {
        display: 'block',
        width: width || '100%',
        height: height || 120,
        borderRadius: 'var(--sb-radius-md)',
        ...base
      }
    }, keyframes);
  }
  return /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 'var(--sb-space-2)',
      width: width || '100%'
    }
  }, keyframes, Array.from({
    length: lines
  }).map((_, i) => /*#__PURE__*/React.createElement("span", {
    key: i,
    style: {
      display: 'block',
      height: height || 12,
      borderRadius: 'var(--sb-radius-xs)',
      width: i === lines - 1 && lines > 1 ? '70%' : '100%',
      ...base
    }
  })));
}
Object.assign(__ds_scope, { Skeleton });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Skeleton.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Tag.jsx
try { (() => {
/**
 * Tag — subject / specialization label (tint bg + deep text)
 *
 * @startingPoint section="Feedback" subtitle="Subject tag" viewport="360x70"
 */
function Tag({
  label,
  subject = 'blue'
}) {
  return /*#__PURE__*/React.createElement("span", {
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      padding: '2px 10px',
      borderRadius: 'var(--sb-radius-pill)',
      background: `var(--sb-subject-${subject}-bg)`,
      color: `var(--sb-subject-${subject}-deep)`,
      fontFamily: 'var(--sb-font-sans)',
      fontSize: 'var(--sb-label-md-size)',
      fontWeight: 700,
      letterSpacing: '0.01em'
    }
  }, label);
}
Object.assign(__ds_scope, { Tag });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Tag.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Timer.jsx
try { (() => {
/**
 * Timer — countdown for quizzes and timed assignments, with an
 * optional progress ring and low-time urgency state
 *
 * @startingPoint section="Feedback" subtitle="Quiz countdown timer" viewport="320x160"
 */
function Timer({
  seconds = 0,
  total,
  running = true,
  onComplete,
  warnAt = 60,
  variant = 'ring'
}) {
  const [remaining, setRemaining] = React.useState(seconds);
  React.useEffect(() => setRemaining(seconds), [seconds]);
  React.useEffect(() => {
    if (!running) return;
    if (remaining <= 0) {
      onComplete?.();
      return;
    }
    const id = setTimeout(() => setRemaining(r => r - 1), 1000);
    return () => clearTimeout(id);
  }, [remaining, running]);
  const mm = String(Math.floor(Math.max(remaining, 0) / 60)).padStart(2, '0');
  const ss = String(Math.max(remaining, 0) % 60).padStart(2, '0');
  const urgent = remaining <= warnAt;
  const color = urgent ? 'var(--sb-danger)' : 'var(--sb-primary)';
  const tot = total || seconds || 1;
  const pct = Math.max(0, Math.min(100, remaining / tot * 100));
  if (variant === 'bar') {
    return /*#__PURE__*/React.createElement("div", {
      style: {
        fontFamily: 'var(--sb-font-sans)',
        width: '100%'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'baseline',
        marginBottom: 'var(--sb-space-2)'
      }
    }, /*#__PURE__*/React.createElement("span", {
      style: {
        fontSize: 'var(--sb-label-lg-size)',
        fontWeight: 600,
        color: 'var(--sb-text-muted)'
      }
    }, "Time remaining"), /*#__PURE__*/React.createElement("span", {
      style: {
        fontFamily: 'var(--sb-font-mono)',
        fontSize: 'var(--sb-heading-sm-size)',
        fontWeight: 800,
        color,
        fontVariantNumeric: 'tabular-nums'
      }
    }, mm, ":", ss)), /*#__PURE__*/React.createElement("div", {
      style: {
        height: 8,
        background: 'var(--sb-neutral-100)',
        borderRadius: 'var(--sb-radius-pill)',
        overflow: 'hidden'
      }
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        height: '100%',
        width: pct + '%',
        background: color,
        borderRadius: 'var(--sb-radius-pill)',
        transition: 'width 1s linear, background var(--sb-timing)'
      }
    })));
  }
  const size = 120,
    stroke = 10;
  return /*#__PURE__*/React.createElement("div", {
    role: "timer",
    "aria-live": "off",
    style: {
      position: 'relative',
      width: size,
      height: size,
      fontFamily: 'var(--sb-font-sans)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      width: '100%',
      height: '100%',
      borderRadius: '50%',
      background: `conic-gradient(${color} ${pct * 3.6}deg, var(--sb-neutral-100) 0deg)`,
      transition: 'background 1s linear',
      animation: urgent && running ? 'sb-timer-pulse 1s ease-in-out infinite' : 'none'
    }
  }), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      inset: stroke,
      borderRadius: '50%',
      background: 'var(--sb-surface)',
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontFamily: 'var(--sb-font-mono)',
      fontSize: 28,
      fontWeight: 800,
      color,
      lineHeight: 1,
      fontVariantNumeric: 'tabular-nums'
    }
  }, mm, ":", ss), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--sb-label-sm-size)',
      color: 'var(--sb-text-muted)',
      marginTop: 2
    }
  }, urgent ? 'Hurry!' : 'remaining')), /*#__PURE__*/React.createElement("style", null, '@keyframes sb-timer-pulse{0%,100%{opacity:1}50%{opacity:.55}}'));
}
Object.assign(__ds_scope, { Timer });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Timer.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Toast.jsx
try { (() => {
/**
 * Toast — transient notification with semantic left accent
 *
 * @startingPoint section="Feedback" subtitle="Toast notification" viewport="380x100"
 */
function Toast({
  variant = 'info',
  title,
  message,
  onClose
}) {
  const accent = {
    success: 'var(--sb-success)',
    error: 'var(--sb-danger)',
    danger: 'var(--sb-danger)',
    warning: 'var(--sb-warning)',
    info: 'var(--sb-info)'
  }[variant] || 'var(--sb-info)';
  return /*#__PURE__*/React.createElement("div", {
    role: "status",
    style: {
      display: 'flex',
      alignItems: 'flex-start',
      gap: 'var(--sb-space-3)',
      minWidth: 280,
      maxWidth: 360,
      padding: 'var(--sb-space-3) var(--sb-space-4)',
      background: 'var(--sb-surface)',
      borderRadius: 'var(--sb-radius-md)',
      borderLeft: `4px solid ${accent}`,
      boxShadow: 'var(--sb-shadow-md)',
      fontFamily: 'var(--sb-font-sans)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      flex: 1
    }
  }, title && /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 'var(--sb-body-md-size)',
      fontWeight: 700,
      color: 'var(--sb-text)'
    }
  }, title), message && /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 'var(--sb-body-sm-size)',
      color: 'var(--sb-text-muted)',
      marginTop: title ? 2 : 0
    }
  }, message)), onClose && /*#__PURE__*/React.createElement("button", {
    onClick: onClose,
    "aria-label": "Dismiss",
    style: {
      border: 'none',
      background: 'none',
      cursor: 'pointer',
      color: 'var(--sb-text-subtle)',
      fontSize: 18,
      lineHeight: 1,
      padding: 0
    }
  }, "\xD7"));
}
Object.assign(__ds_scope, { Toast });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Toast.jsx", error: String((e && e.message) || e) }); }

// components/feedback/Tooltip.jsx
try { (() => {
/**
 * Tooltip — hover/focus hint bubble
 *
 * @startingPoint section="Feedback" subtitle="Tooltip" viewport="300x120"
 */
function Tooltip({
  content,
  placement = 'top',
  children
}) {
  const [show, setShow] = React.useState(false);
  const pos = {
    top: {
      bottom: '100%',
      left: '50%',
      transform: 'translateX(-50%)',
      marginBottom: 8
    },
    bottom: {
      top: '100%',
      left: '50%',
      transform: 'translateX(-50%)',
      marginTop: 8
    },
    left: {
      right: '100%',
      top: '50%',
      transform: 'translateY(-50%)',
      marginRight: 8
    },
    right: {
      left: '100%',
      top: '50%',
      transform: 'translateY(-50%)',
      marginLeft: 8
    }
  }[placement];
  return /*#__PURE__*/React.createElement("span", {
    style: {
      position: 'relative',
      display: 'inline-flex'
    },
    onMouseEnter: () => setShow(true),
    onMouseLeave: () => setShow(false),
    onFocus: () => setShow(true),
    onBlur: () => setShow(false)
  }, children, show && /*#__PURE__*/React.createElement("span", {
    role: "tooltip",
    style: {
      position: 'absolute',
      ...pos,
      zIndex: 1500,
      whiteSpace: 'nowrap',
      background: 'var(--sb-neutral-800)',
      color: '#fff',
      fontFamily: 'var(--sb-font-sans)',
      fontSize: 'var(--sb-body-sm-size)',
      padding: '6px 10px',
      borderRadius: 'var(--sb-radius-sm)',
      boxShadow: 'var(--sb-shadow-md)'
    }
  }, content));
}
Object.assign(__ds_scope, { Tooltip });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/Tooltip.jsx", error: String((e && e.message) || e) }); }

// components/forms/Checkbox.jsx
try { (() => {
/**
 * Checkbox — selection control
 *
 * @startingPoint section="Forms" subtitle="Checkbox" viewport="280x60"
 */
function Checkbox({
  checked,
  onChange,
  disabled,
  label
}) {
  return /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: 'var(--sb-space-2)',
      cursor: disabled ? 'not-allowed' : 'pointer',
      userSelect: 'none',
      fontFamily: 'var(--sb-font-sans)',
      opacity: disabled ? 0.45 : 1
    }
  }, /*#__PURE__*/React.createElement("input", {
    type: "checkbox",
    checked: !!checked,
    onChange: e => onChange?.(e.target.checked),
    disabled: disabled,
    style: {
      width: 18,
      height: 18,
      margin: 0,
      accentColor: 'var(--sb-primary)',
      borderRadius: 'var(--sb-radius-sm)',
      cursor: 'inherit'
    }
  }), label && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--sb-body-md-size)',
      color: 'var(--sb-text)'
    }
  }, label));
}
Object.assign(__ds_scope, { Checkbox });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Checkbox.jsx", error: String((e && e.message) || e) }); }

// components/forms/CodeInput.jsx
try { (() => {
/**
 * CodeInput — segmented enrollment-code entry (XXXX-XXXX-XXXX)
 *
 * @startingPoint section="Forms" subtitle="Enrollment code input" viewport="420x120"
 */
function CodeInput({
  groups = 3,
  groupLength = 4,
  value = '',
  onChange,
  error,
  label = 'Enrollment code'
}) {
  const refs = React.useRef([]);
  const total = groups;
  const parts = React.useMemo(() => {
    const clean = (value || '').replace(/[^A-Za-z0-9]/g, '').toUpperCase();
    const out = [];
    for (let i = 0; i < groups; i++) out.push(clean.slice(i * groupLength, (i + 1) * groupLength));
    return out;
  }, [value, groups, groupLength]);
  const emit = arr => onChange?.(arr.join('-'));
  const setPart = (i, v) => {
    const clean = v.replace(/[^A-Za-z0-9]/g, '').toUpperCase().slice(0, groupLength);
    const next = [...parts];
    next[i] = clean;
    emit(next);
    if (clean.length === groupLength && i < total - 1) refs.current[i + 1]?.focus();
  };
  const onPaste = e => {
    e.preventDefault();
    const clean = (e.clipboardData.getData('text') || '').replace(/[^A-Za-z0-9]/g, '').toUpperCase();
    const arr = [];
    for (let i = 0; i < groups; i++) arr.push(clean.slice(i * groupLength, (i + 1) * groupLength));
    emit(arr);
  };
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 'var(--sb-space-2)',
      fontFamily: 'var(--sb-font-sans)'
    }
  }, label && /*#__PURE__*/React.createElement("label", {
    style: {
      fontSize: 'var(--sb-label-lg-size)',
      fontWeight: 600,
      color: 'var(--sb-text)'
    }
  }, label), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 'var(--sb-space-2)'
    }
  }, parts.map((p, i) => /*#__PURE__*/React.createElement(React.Fragment, {
    key: i
  }, /*#__PURE__*/React.createElement("input", {
    ref: el => refs.current[i] = el,
    value: p,
    onChange: e => setPart(i, e.target.value),
    onPaste: onPaste,
    onKeyDown: e => {
      if (e.key === 'Backspace' && !p && i > 0) refs.current[i - 1]?.focus();
    },
    maxLength: groupLength,
    "aria-label": `Code group ${i + 1}`,
    style: {
      width: groupLength * 18 + 16,
      height: 44,
      textAlign: 'center',
      fontFamily: 'var(--sb-font-mono)',
      fontSize: 'var(--sb-body-lg-size)',
      letterSpacing: '0.18em',
      textTransform: 'uppercase',
      color: 'var(--sb-text)',
      border: `1px solid ${error ? 'var(--sb-danger)' : 'var(--sb-border-strong)'}`,
      borderRadius: 'var(--sb-radius-md)',
      background: 'var(--sb-surface)',
      outline: 'none'
    }
  }), i < total - 1 && /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--sb-text-subtle)',
      fontWeight: 700
    }
  }, "\u2013")))), error && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--sb-body-sm-size)',
      color: 'var(--sb-danger-fg)'
    }
  }, error));
}
Object.assign(__ds_scope, { CodeInput });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/CodeInput.jsx", error: String((e && e.message) || e) }); }

// components/forms/DatePicker.jsx
try { (() => {
/**
 * DatePicker — text field + drop-down month calendar for session
 * validity windows and date filters
 *
 * @startingPoint section="Forms" subtitle="Date picker" viewport="320x120"
 */
function DatePicker({
  value,
  onChange,
  label,
  placeholder = 'Select a date',
  min,
  max
}) {
  const [open, setOpen] = React.useState(false);
  const selected = value ? new Date(value) : null;
  const [view, setView] = React.useState(selected || new Date());
  const fmt = d => d ? d.toLocaleDateString(undefined, {
    day: '2-digit',
    month: 'short',
    year: 'numeric'
  }) : '';
  const iso = d => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  const sameDay = (a, b) => a && b && a.toDateString() === b.toDateString();
  const year = view.getFullYear(),
    month = view.getMonth();
  const firstDow = new Date(year, month, 1).getDay();
  const daysIn = new Date(year, month + 1, 0).getDate();
  const cells = [];
  for (let i = 0; i < firstDow; i++) cells.push(null);
  for (let d = 1; d <= daysIn; d++) cells.push(new Date(year, month, d));
  const minD = min ? new Date(min) : null,
    maxD = max ? new Date(max) : null;
  const disabled = d => minD && d < minD || maxD && d > maxD;
  return /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative',
      fontFamily: 'var(--sb-font-sans)',
      width: 240
    }
  }, label && /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'block',
      fontSize: 'var(--sb-label-lg-size)',
      fontWeight: 600,
      color: 'var(--sb-text)',
      marginBottom: 'var(--sb-space-1)'
    }
  }, label), /*#__PURE__*/React.createElement("button", {
    type: "button",
    onClick: () => setOpen(!open),
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      width: '100%',
      height: 40,
      padding: '0 12px',
      border: `1px solid ${open ? 'var(--sb-primary)' : 'var(--sb-border-strong)'}`,
      borderRadius: 'var(--sb-radius-md)',
      background: 'var(--sb-surface)',
      cursor: 'pointer',
      boxShadow: open ? 'var(--sb-shadow-focus)' : 'none',
      fontFamily: 'inherit',
      fontSize: 'var(--sb-body-md-size)',
      color: selected ? 'var(--sb-text)' : 'var(--sb-text-subtle)'
    }
  }, /*#__PURE__*/React.createElement("span", null, selected ? fmt(selected) : placeholder), /*#__PURE__*/React.createElement("span", {
    "aria-hidden": true,
    style: {
      color: 'var(--sb-text-muted)'
    }
  }, "\u25A6")), open && /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'absolute',
      top: 'calc(100% + 6px)',
      left: 0,
      zIndex: 1400,
      width: 252,
      background: 'var(--sb-surface)',
      border: '1px solid var(--sb-border)',
      borderRadius: 'var(--sb-radius-lg)',
      boxShadow: 'var(--sb-shadow-lg)',
      padding: 'var(--sb-space-3)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      marginBottom: 'var(--sb-space-2)'
    }
  }, /*#__PURE__*/React.createElement("button", {
    type: "button",
    onClick: () => setView(new Date(year, month - 1, 1)),
    style: navBtn
  }, "\u2039"), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--sb-body-md-size)',
      fontWeight: 700,
      color: 'var(--sb-text)'
    }
  }, view.toLocaleDateString(undefined, {
    month: 'long',
    year: 'numeric'
  })), /*#__PURE__*/React.createElement("button", {
    type: "button",
    onClick: () => setView(new Date(year, month + 1, 1)),
    style: navBtn
  }, "\u203A")), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'grid',
      gridTemplateColumns: 'repeat(7, 1fr)',
      gap: 2
    }
  }, ['S', 'M', 'T', 'W', 'T', 'F', 'S'].map((d, i) => /*#__PURE__*/React.createElement("span", {
    key: i,
    style: {
      textAlign: 'center',
      fontSize: 'var(--sb-label-sm-size)',
      fontWeight: 700,
      color: 'var(--sb-text-subtle)',
      padding: '4px 0'
    }
  }, d)), cells.map((d, i) => {
    if (!d) return /*#__PURE__*/React.createElement("span", {
      key: i
    });
    const isSel = sameDay(d, selected);
    const isToday = sameDay(d, new Date());
    const off = disabled(d);
    return /*#__PURE__*/React.createElement("button", {
      key: i,
      type: "button",
      disabled: off,
      onClick: () => {
        onChange?.(iso(d));
        setOpen(false);
      },
      style: {
        height: 30,
        border: 'none',
        borderRadius: 'var(--sb-radius-sm)',
        cursor: off ? 'not-allowed' : 'pointer',
        background: isSel ? 'var(--sb-primary)' : 'transparent',
        color: off ? 'var(--sb-text-subtle)' : isSel ? 'var(--sb-on-primary)' : 'var(--sb-text)',
        fontFamily: 'inherit',
        fontSize: 'var(--sb-body-sm-size)',
        fontWeight: isSel || isToday ? 700 : 500,
        boxShadow: isToday && !isSel ? 'inset 0 0 0 1px var(--sb-primary-200)' : 'none',
        opacity: off ? 0.4 : 1
      }
    }, d.getDate());
  }))));
}
const navBtn = {
  border: 'none',
  background: 'var(--sb-surface-sunken)',
  borderRadius: 'var(--sb-radius-sm)',
  width: 28,
  height: 28,
  cursor: 'pointer',
  color: 'var(--sb-text)',
  fontSize: 16,
  lineHeight: 1
};
Object.assign(__ds_scope, { DatePicker });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/DatePicker.jsx", error: String((e && e.message) || e) }); }

// components/forms/FileUpload.jsx
try { (() => {
/**
 * FileUpload — drag-and-drop dropzone for ID images, materials,
 * videos, and question attachments
 *
 * @startingPoint section="Forms" subtitle="File upload dropzone" viewport="460x240"
 */
function FileUpload({
  label,
  accept,
  multiple = false,
  hint = 'PNG, JPG, PDF or MP4 · up to 50 MB',
  files = [],
  onFiles,
  onRemove
}) {
  const [drag, setDrag] = React.useState(false);
  const inputRef = React.useRef(null);
  const pick = list => {
    if (list && list.length) onFiles?.(Array.from(list));
  };
  return /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: 'var(--sb-font-sans)',
      width: '100%'
    }
  }, label && /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'block',
      fontSize: 'var(--sb-label-lg-size)',
      fontWeight: 600,
      color: 'var(--sb-text)',
      marginBottom: 'var(--sb-space-2)'
    }
  }, label), /*#__PURE__*/React.createElement("div", {
    role: "button",
    tabIndex: 0,
    onClick: () => inputRef.current?.click(),
    onKeyDown: e => {
      if (e.key === 'Enter' || e.key === ' ') inputRef.current?.click();
    },
    onDragOver: e => {
      e.preventDefault();
      setDrag(true);
    },
    onDragLeave: () => setDrag(false),
    onDrop: e => {
      e.preventDefault();
      setDrag(false);
      pick(e.dataTransfer.files);
    },
    style: {
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      justifyContent: 'center',
      gap: 'var(--sb-space-2)',
      padding: 'var(--sb-space-8) var(--sb-space-6)',
      textAlign: 'center',
      cursor: 'pointer',
      border: `2px dashed ${drag ? 'var(--sb-primary)' : 'var(--sb-border-strong)'}`,
      borderRadius: 'var(--sb-radius-lg)',
      background: drag ? 'var(--sb-primary-50)' : 'var(--sb-surface-sunken)',
      transition: 'border-color var(--sb-timing-fast), background var(--sb-timing-fast)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    "aria-hidden": true,
    style: {
      width: 40,
      height: 40,
      borderRadius: '50%',
      background: 'var(--sb-primary-100)',
      color: 'var(--sb-primary-700)',
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 20
    }
  }, "\u2191"), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--sb-body-md-size)',
      color: 'var(--sb-text)'
    }
  }, /*#__PURE__*/React.createElement("b", {
    style: {
      color: 'var(--sb-primary-700)'
    }
  }, "Click to upload"), " or drag & drop"), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--sb-body-sm-size)',
      color: 'var(--sb-text-muted)'
    }
  }, hint), /*#__PURE__*/React.createElement("input", {
    ref: inputRef,
    type: "file",
    accept: accept,
    multiple: multiple,
    onChange: e => pick(e.target.files),
    style: {
      display: 'none'
    }
  })), files.length > 0 && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 'var(--sb-space-2)',
      marginTop: 'var(--sb-space-3)'
    }
  }, files.map((f, i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 'var(--sb-space-3)',
      padding: 'var(--sb-space-2) var(--sb-space-3)',
      background: 'var(--sb-surface)',
      border: '1px solid var(--sb-border)',
      borderRadius: 'var(--sb-radius-md)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    "aria-hidden": true,
    style: {
      width: 28,
      height: 28,
      borderRadius: 'var(--sb-radius-sm)',
      flexShrink: 0,
      background: 'var(--sb-subject-blue-bg)',
      color: 'var(--sb-subject-blue-deep)',
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 13,
      fontWeight: 800
    }
  }, (f.name || 'file').split('.').pop().slice(0, 3).toUpperCase()), /*#__PURE__*/React.createElement("span", {
    style: {
      flex: 1,
      minWidth: 0,
      fontSize: 'var(--sb-body-sm-size)',
      color: 'var(--sb-text)',
      overflow: 'hidden',
      textOverflow: 'ellipsis',
      whiteSpace: 'nowrap'
    }
  }, f.name || 'Untitled'), f.size != null && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--sb-label-sm-size)',
      color: 'var(--sb-text-muted)',
      flexShrink: 0
    }
  }, (f.size / 1048576).toFixed(1), " MB"), onRemove && /*#__PURE__*/React.createElement("button", {
    type: "button",
    onClick: () => onRemove(i),
    "aria-label": "Remove",
    style: {
      border: 'none',
      background: 'none',
      color: 'var(--sb-text-subtle)',
      cursor: 'pointer',
      fontSize: 17,
      lineHeight: 1,
      padding: 0
    }
  }, "\xD7")))));
}
Object.assign(__ds_scope, { FileUpload });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/FileUpload.jsx", error: String((e && e.message) || e) }); }

// components/forms/Input.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/**
 * Input — text field with label, hint, and error states
 *
 * @startingPoint section="Forms" subtitle="Text input field" viewport="360x140"
 */
function Input({
  size = 'md',
  disabled = false,
  error = false,
  required = false,
  helperText,
  label,
  placeholder,
  leadingIcon,
  style,
  ...props
}) {
  const [focus, setFocus] = React.useState(false);
  const [hover, setHover] = React.useState(false);
  const height = size === 'sm' ? 32 : size === 'lg' ? 48 : 40;
  const borderColor = error ? 'var(--sb-danger)' : focus ? 'var(--sb-primary)' : hover ? 'var(--sb-neutral-400)' : 'var(--sb-border-strong)';
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 'var(--sb-space-1)',
      fontFamily: 'var(--sb-font-sans)',
      ...style
    }
  }, label && /*#__PURE__*/React.createElement("label", {
    style: {
      fontSize: 'var(--sb-label-lg-size)',
      fontWeight: 600,
      color: 'var(--sb-text)'
    }
  }, label, required && /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--sb-danger)'
    }
  }, " *")), /*#__PURE__*/React.createElement("div", {
    onMouseEnter: () => setHover(true),
    onMouseLeave: () => setHover(false),
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 'var(--sb-space-2)',
      height,
      padding: '0 12px',
      border: `1px solid ${borderColor}`,
      borderRadius: 'var(--sb-radius-md)',
      background: disabled ? 'var(--sb-surface-sunken)' : 'var(--sb-surface)',
      boxShadow: focus ? 'var(--sb-shadow-focus)' : 'none',
      opacity: disabled ? 0.55 : 1,
      transition: 'border-color var(--sb-timing-fast), box-shadow var(--sb-timing-fast)'
    }
  }, leadingIcon && /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--sb-text-subtle)',
      display: 'inline-flex'
    }
  }, leadingIcon), /*#__PURE__*/React.createElement("input", _extends({
    placeholder: placeholder,
    disabled: disabled,
    "aria-invalid": error || undefined,
    onFocus: () => setFocus(true),
    onBlur: () => setFocus(false),
    style: {
      flex: 1,
      minWidth: 0,
      border: 'none',
      outline: 'none',
      background: 'transparent',
      fontFamily: 'inherit',
      fontSize: 'var(--sb-body-md-size)',
      color: 'var(--sb-text)',
      height: '100%'
    }
  }, props))), helperText && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--sb-body-sm-size)',
      color: error ? 'var(--sb-danger-fg)' : 'var(--sb-text-muted)'
    }
  }, helperText));
}
Object.assign(__ds_scope, { Input });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Input.jsx", error: String((e && e.message) || e) }); }

// components/forms/Radio.jsx
try { (() => {
/**
 * Radio — selection control
 *
 * @startingPoint section="Forms" subtitle="Radio button" viewport="280x60"
 */
function Radio({
  checked,
  onChange,
  disabled,
  label,
  name,
  value
}) {
  return /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: 'var(--sb-space-2)',
      cursor: disabled ? 'not-allowed' : 'pointer',
      userSelect: 'none',
      fontFamily: 'var(--sb-font-sans)',
      opacity: disabled ? 0.45 : 1
    }
  }, /*#__PURE__*/React.createElement("input", {
    type: "radio",
    name: name,
    value: value,
    checked: !!checked,
    onChange: e => onChange?.(e.target.checked),
    disabled: disabled,
    style: {
      width: 18,
      height: 18,
      margin: 0,
      accentColor: 'var(--sb-primary)',
      cursor: 'inherit'
    }
  }), label && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--sb-body-md-size)',
      color: 'var(--sb-text)'
    }
  }, label));
}
Object.assign(__ds_scope, { Radio });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Radio.jsx", error: String((e && e.message) || e) }); }

// components/forms/SearchBar.jsx
try { (() => {
/**
 * SearchBar — student / session / code search with clear
 *
 * @startingPoint section="Forms" subtitle="Search field" viewport="420x100"
 */
function SearchBar({
  value,
  onChange,
  onSubmit,
  placeholder = 'Search students, sessions, codes…',
  size = 'md'
}) {
  const [focus, setFocus] = React.useState(false);
  const height = size === 'sm' ? 36 : size === 'lg' ? 48 : 42;
  return /*#__PURE__*/React.createElement("form", {
    onSubmit: e => {
      e.preventDefault();
      onSubmit?.(value);
    },
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 'var(--sb-space-2)',
      height,
      padding: '0 12px',
      width: '100%',
      background: 'var(--sb-surface)',
      borderRadius: 'var(--sb-radius-pill)',
      border: `1px solid ${focus ? 'var(--sb-primary)' : 'var(--sb-border-strong)'}`,
      boxShadow: focus ? 'var(--sb-shadow-focus)' : 'none',
      fontFamily: 'var(--sb-font-sans)',
      transition: 'border-color var(--sb-timing-fast), box-shadow var(--sb-timing-fast)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    "aria-hidden": true,
    style: {
      color: 'var(--sb-text-subtle)',
      fontSize: 16,
      lineHeight: 1
    }
  }, "\u2315"), /*#__PURE__*/React.createElement("input", {
    type: "search",
    value: value,
    placeholder: placeholder,
    onChange: e => onChange?.(e.target.value),
    onFocus: () => setFocus(true),
    onBlur: () => setFocus(false),
    style: {
      flex: 1,
      minWidth: 0,
      border: 'none',
      outline: 'none',
      background: 'transparent',
      fontFamily: 'inherit',
      fontSize: 'var(--sb-body-md-size)',
      color: 'var(--sb-text)'
    }
  }), value && /*#__PURE__*/React.createElement("button", {
    type: "button",
    onClick: () => onChange?.(''),
    "aria-label": "Clear",
    style: {
      border: 'none',
      background: 'var(--sb-neutral-100)',
      color: 'var(--sb-text-muted)',
      width: 20,
      height: 20,
      borderRadius: '50%',
      cursor: 'pointer',
      fontSize: 13,
      lineHeight: 1,
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      flexShrink: 0
    }
  }, "\xD7"));
}
Object.assign(__ds_scope, { SearchBar });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/SearchBar.jsx", error: String((e && e.message) || e) }); }

// components/forms/Select.jsx
try { (() => {
/**
 * Select — dropdown field
 *
 * @startingPoint section="Forms" subtitle="Select dropdown" viewport="360x120"
 */
function Select({
  label,
  value,
  onChange,
  options = [],
  disabled,
  error,
  placeholder,
  required
}) {
  const [focus, setFocus] = React.useState(false);
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      gap: 'var(--sb-space-1)',
      fontFamily: 'var(--sb-font-sans)'
    }
  }, label && /*#__PURE__*/React.createElement("label", {
    style: {
      fontSize: 'var(--sb-label-lg-size)',
      fontWeight: 600,
      color: 'var(--sb-text)'
    }
  }, label, required && /*#__PURE__*/React.createElement("span", {
    style: {
      color: 'var(--sb-danger)'
    }
  }, " *")), /*#__PURE__*/React.createElement("div", {
    style: {
      position: 'relative'
    }
  }, /*#__PURE__*/React.createElement("select", {
    value: value,
    onChange: e => onChange?.(e.target.value),
    disabled: disabled,
    onFocus: () => setFocus(true),
    onBlur: () => setFocus(false),
    style: {
      width: '100%',
      height: 40,
      padding: '0 36px 0 12px',
      appearance: 'none',
      fontFamily: 'inherit',
      fontSize: 'var(--sb-body-md-size)',
      color: value ? 'var(--sb-text)' : 'var(--sb-text-subtle)',
      border: `1px solid ${error ? 'var(--sb-danger)' : focus ? 'var(--sb-primary)' : 'var(--sb-border-strong)'}`,
      borderRadius: 'var(--sb-radius-md)',
      background: disabled ? 'var(--sb-surface-sunken)' : 'var(--sb-surface)',
      boxShadow: focus ? 'var(--sb-shadow-focus)' : 'none',
      cursor: disabled ? 'not-allowed' : 'pointer',
      opacity: disabled ? 0.55 : 1,
      outline: 'none'
    }
  }, placeholder && /*#__PURE__*/React.createElement("option", {
    value: "",
    disabled: true
  }, placeholder), options.map(o => /*#__PURE__*/React.createElement("option", {
    key: o.value,
    value: o.value
  }, o.label))), /*#__PURE__*/React.createElement("span", {
    style: {
      position: 'absolute',
      right: 12,
      top: '50%',
      transform: 'translateY(-50%)',
      pointerEvents: 'none',
      color: 'var(--sb-text-muted)',
      fontSize: 12
    }
  }, "\u25BE")), error && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--sb-body-sm-size)',
      color: 'var(--sb-danger-fg)'
    }
  }, error));
}
Object.assign(__ds_scope, { Select });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Select.jsx", error: String((e && e.message) || e) }); }

// components/forms/Switch.jsx
try { (() => {
/**
 * Switch — on/off toggle
 *
 * @startingPoint section="Forms" subtitle="Toggle switch" viewport="280x60"
 */
function Switch({
  checked,
  onChange,
  disabled,
  label
}) {
  return /*#__PURE__*/React.createElement("label", {
    style: {
      display: 'inline-flex',
      alignItems: 'center',
      gap: 'var(--sb-space-3)',
      cursor: disabled ? 'not-allowed' : 'pointer',
      userSelect: 'none',
      fontFamily: 'var(--sb-font-sans)',
      opacity: disabled ? 0.45 : 1
    }
  }, /*#__PURE__*/React.createElement("span", {
    role: "switch",
    "aria-checked": !!checked,
    onClick: () => !disabled && onChange?.(!checked),
    style: {
      position: 'relative',
      width: 36,
      height: 20,
      flexShrink: 0,
      borderRadius: 'var(--sb-radius-pill)',
      background: checked ? 'var(--sb-primary)' : 'var(--sb-neutral-300)',
      transition: 'background var(--sb-timing) var(--sb-easing-standard)'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      position: 'absolute',
      top: 2,
      left: checked ? 18 : 2,
      width: 16,
      height: 16,
      borderRadius: '50%',
      background: '#fff',
      boxShadow: 'var(--sb-shadow-xs)',
      transition: 'left var(--sb-timing) var(--sb-easing-standard)'
    }
  })), label && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--sb-body-md-size)',
      color: 'var(--sb-text)'
    }
  }, label));
}
Object.assign(__ds_scope, { Switch });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/forms/Switch.jsx", error: String((e && e.message) || e) }); }

// components/layout/Card.jsx
try { (() => {
/**
 * Card — surface container with optional header / footer
 *
 * @startingPoint section="Layout" subtitle="Content card" viewport="420x240"
 */
function Card({
  title,
  actions,
  footer,
  children,
  shadow = 'sm',
  padding = true,
  interactive = false,
  style
}) {
  const [hover, setHover] = React.useState(false);
  return /*#__PURE__*/React.createElement("div", {
    onMouseEnter: () => interactive && setHover(true),
    onMouseLeave: () => interactive && setHover(false),
    style: {
      background: 'var(--sb-surface)',
      borderRadius: 'var(--sb-radius-lg)',
      border: '1px solid var(--sb-border)',
      boxShadow: interactive && hover ? 'var(--sb-shadow-md)' : `var(--sb-shadow-${shadow})`,
      transform: interactive && hover ? 'translateY(-2px)' : 'none',
      transition: 'box-shadow var(--sb-timing) var(--sb-easing-standard), transform var(--sb-timing) var(--sb-easing-standard)',
      fontFamily: 'var(--sb-font-sans)',
      overflow: 'hidden',
      cursor: interactive ? 'pointer' : 'default',
      ...style
    }
  }, (title || actions) && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      padding: 'var(--sb-space-4) var(--sb-space-6)',
      borderBottom: '1px solid var(--sb-border)'
    }
  }, /*#__PURE__*/React.createElement("h3", {
    style: {
      margin: 0,
      fontSize: 'var(--sb-heading-sm-size)',
      fontWeight: 700,
      color: 'var(--sb-text)'
    }
  }, title), actions && /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      gap: 'var(--sb-space-2)'
    }
  }, actions)), /*#__PURE__*/React.createElement("div", {
    style: {
      padding: padding ? 'var(--sb-space-6)' : 0,
      color: 'var(--sb-text)',
      fontSize: 'var(--sb-body-md-size)'
    }
  }, children), footer && /*#__PURE__*/React.createElement("div", {
    style: {
      padding: 'var(--sb-space-4) var(--sb-space-6)',
      borderTop: '1px solid var(--sb-border)'
    }
  }, footer));
}
Object.assign(__ds_scope, { Card });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/layout/Card.jsx", error: String((e && e.message) || e) }); }

// components/layout/EmptyState.jsx
try { (() => {
/**
 * EmptyState — friendly empty / error placeholder with mascot
 *
 * @startingPoint section="Layout" subtitle="Empty state with mascot" viewport="420x340"
 */
function EmptyState({
  image,
  headline,
  description,
  action
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: 'column',
      alignItems: 'center',
      textAlign: 'center',
      gap: 'var(--sb-space-3)',
      padding: 'var(--sb-space-10) var(--sb-space-6)',
      fontFamily: 'var(--sb-font-sans)',
      maxWidth: 380,
      margin: '0 auto'
    }
  }, image && /*#__PURE__*/React.createElement("img", {
    src: image,
    alt: "",
    style: {
      width: 140,
      height: 'auto',
      marginBottom: 'var(--sb-space-2)'
    }
  }), headline && /*#__PURE__*/React.createElement("h3", {
    style: {
      margin: 0,
      fontSize: 'var(--sb-heading-md-size)',
      fontWeight: 700,
      color: 'var(--sb-text)'
    }
  }, headline), description && /*#__PURE__*/React.createElement("p", {
    style: {
      margin: 0,
      fontSize: 'var(--sb-body-md-size)',
      color: 'var(--sb-text-muted)',
      lineHeight: 1.5
    }
  }, description), action && /*#__PURE__*/React.createElement("div", {
    style: {
      marginTop: 'var(--sb-space-2)'
    }
  }, action));
}
Object.assign(__ds_scope, { EmptyState });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/layout/EmptyState.jsx", error: String((e && e.message) || e) }); }

// components/layout/StatCard.jsx
try { (() => {
/**
 * StatCard — dashboard KPI metric
 *
 * @startingPoint section="Layout" subtitle="KPI / stat card" viewport="280x150"
 */
function StatCard({
  label,
  value,
  delta,
  deltaDirection = 'up',
  icon,
  accent = 'blue'
}) {
  const positive = deltaDirection === 'up';
  return /*#__PURE__*/React.createElement("div", {
    style: {
      background: 'var(--sb-surface)',
      border: '1px solid var(--sb-border)',
      borderRadius: 'var(--sb-radius-lg)',
      boxShadow: 'var(--sb-shadow-sm)',
      padding: 'var(--sb-space-5)',
      fontFamily: 'var(--sb-font-sans)',
      display: 'flex',
      flexDirection: 'column',
      gap: 'var(--sb-space-3)',
      minWidth: 200
    }
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between'
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--sb-label-lg-size)',
      fontWeight: 600,
      color: 'var(--sb-text-muted)'
    }
  }, label), icon && /*#__PURE__*/React.createElement("span", {
    style: {
      width: 36,
      height: 36,
      borderRadius: '50%',
      background: `var(--sb-subject-${accent}-bg)`,
      color: `var(--sb-subject-${accent}-deep)`,
      display: 'inline-flex',
      alignItems: 'center',
      justifyContent: 'center',
      fontSize: 18
    }
  }, icon)), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 'var(--sb-heading-lg-size)',
      fontWeight: 800,
      color: 'var(--sb-text)',
      lineHeight: 1
    }
  }, value), delta != null && /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 'var(--sb-body-sm-size)',
      fontWeight: 700,
      color: positive ? 'var(--sb-success-fg)' : 'var(--sb-danger-fg)'
    }
  }, positive ? '▲' : '▼', " ", delta));
}
Object.assign(__ds_scope, { StatCard });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/layout/StatCard.jsx", error: String((e && e.message) || e) }); }

// components/navigation/Breadcrumb.jsx
try { (() => {
/**
 * Breadcrumb — hierarchical path
 *
 * @startingPoint section="Navigation" subtitle="Breadcrumb trail" viewport="480x60"
 */
function Breadcrumb({
  items = []
}) {
  return /*#__PURE__*/React.createElement("nav", {
    "aria-label": "Breadcrumb",
    style: {
      fontFamily: 'var(--sb-font-sans)'
    }
  }, /*#__PURE__*/React.createElement("ol", {
    style: {
      display: 'flex',
      flexWrap: 'wrap',
      alignItems: 'center',
      gap: 'var(--sb-space-2)',
      margin: 0,
      padding: 0,
      listStyle: 'none'
    }
  }, items.map((item, i) => {
    const last = i === items.length - 1;
    return /*#__PURE__*/React.createElement("li", {
      key: i,
      style: {
        display: 'inline-flex',
        alignItems: 'center',
        gap: 'var(--sb-space-2)'
      }
    }, last ? /*#__PURE__*/React.createElement("span", {
      "aria-current": "page",
      style: {
        fontSize: 'var(--sb-body-md-size)',
        fontWeight: 600,
        color: 'var(--sb-text)'
      }
    }, item.label) : /*#__PURE__*/React.createElement("a", {
      href: item.href || '#',
      style: {
        fontSize: 'var(--sb-body-md-size)',
        color: 'var(--sb-text-muted)',
        textDecoration: 'none'
      }
    }, item.label), !last && /*#__PURE__*/React.createElement("span", {
      style: {
        color: 'var(--sb-text-subtle)',
        fontSize: 12
      }
    }, "\u203A"));
  })));
}
Object.assign(__ds_scope, { Breadcrumb });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/navigation/Breadcrumb.jsx", error: String((e && e.message) || e) }); }

// components/navigation/Pagination.jsx
try { (() => {
/**
 * Pagination — page navigation with numbers + range
 *
 * @startingPoint section="Navigation" subtitle="Pagination control" viewport="520x70"
 */
function Pagination({
  page = 1,
  pageCount = 1,
  onChange,
  total,
  pageSize
}) {
  const go = p => {
    if (p >= 1 && p <= pageCount && p !== page) onChange?.(p);
  };
  const pages = React.useMemo(() => {
    const out = [];
    const add = p => out.push(p);
    if (pageCount <= 7) {
      for (let i = 1; i <= pageCount; i++) add(i);
      return out;
    }
    add(1);
    if (page > 3) add('…');
    for (let i = Math.max(2, page - 1); i <= Math.min(pageCount - 1, page + 1); i++) add(i);
    if (page < pageCount - 2) add('…');
    add(pageCount);
    return out;
  }, [page, pageCount]);
  const btn = (active, disabled) => ({
    minWidth: 34,
    height: 34,
    padding: '0 8px',
    borderRadius: 'var(--sb-radius-md)',
    border: `1px solid ${active ? 'var(--sb-primary)' : 'transparent'}`,
    background: active ? 'var(--sb-primary)' : 'transparent',
    color: active ? 'var(--sb-on-primary)' : disabled ? 'var(--sb-text-subtle)' : 'var(--sb-text)',
    fontFamily: 'var(--sb-font-sans)',
    fontSize: 'var(--sb-body-md-size)',
    fontWeight: 700,
    cursor: disabled ? 'not-allowed' : 'pointer'
  });
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      gap: 'var(--sb-space-4)',
      fontFamily: 'var(--sb-font-sans)',
      flexWrap: 'wrap'
    }
  }, total != null && pageSize != null && /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 'var(--sb-body-sm-size)',
      color: 'var(--sb-text-muted)'
    }
  }, (page - 1) * pageSize + 1, "\u2013", Math.min(page * pageSize, total), " of ", total), /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 'var(--sb-space-1)'
    }
  }, /*#__PURE__*/React.createElement("button", {
    onClick: () => go(page - 1),
    disabled: page <= 1,
    style: btn(false, page <= 1),
    "aria-label": "Previous"
  }, "\u2039"), pages.map((p, i) => p === '…' ? /*#__PURE__*/React.createElement("span", {
    key: i,
    style: {
      minWidth: 24,
      textAlign: 'center',
      color: 'var(--sb-text-subtle)'
    }
  }, "\u2026") : /*#__PURE__*/React.createElement("button", {
    key: i,
    onClick: () => go(p),
    "aria-current": p === page ? 'page' : undefined,
    style: btn(p === page, false)
  }, p)), /*#__PURE__*/React.createElement("button", {
    onClick: () => go(page + 1),
    disabled: page >= pageCount,
    style: btn(false, page >= pageCount),
    "aria-label": "Next"
  }, "\u203A")));
}
Object.assign(__ds_scope, { Pagination });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/navigation/Pagination.jsx", error: String((e && e.message) || e) }); }

// components/navigation/Stepper.jsx
try { (() => {
/**
 * Stepper — numbered progress through a wizard
 *
 * @startingPoint section="Navigation" subtitle="Wizard stepper" viewport="560x120"
 */
function Stepper({
  steps = [],
  current = 0,
  orientation = 'horizontal'
}) {
  const vertical = orientation === 'vertical';
  return /*#__PURE__*/React.createElement("div", {
    style: {
      display: 'flex',
      flexDirection: vertical ? 'column' : 'row',
      alignItems: vertical ? 'flex-start' : 'center',
      fontFamily: 'var(--sb-font-sans)',
      gap: 0
    }
  }, steps.map((step, i) => {
    const done = i < current;
    const active = i === current;
    const circle = done ? 'var(--sb-accent)' : active ? 'var(--sb-primary)' : 'var(--sb-neutral-100)';
    const ring = active ? '0 0 0 4px var(--sb-primary-100)' : 'none';
    const textColor = done || active ? 'var(--sb-text)' : 'var(--sb-text-muted)';
    const last = i === steps.length - 1;
    return /*#__PURE__*/React.createElement(React.Fragment, {
      key: i
    }, /*#__PURE__*/React.createElement("div", {
      style: {
        display: 'flex',
        flexDirection: vertical ? 'row' : 'column',
        alignItems: 'center',
        gap: 'var(--sb-space-2)',
        textAlign: 'center'
      }
    }, /*#__PURE__*/React.createElement("span", {
      style: {
        width: 28,
        height: 28,
        borderRadius: '50%',
        flexShrink: 0,
        background: circle,
        boxShadow: ring,
        color: done ? '#fff' : active ? '#fff' : 'var(--sb-text-muted)',
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: 'var(--sb-body-sm-size)',
        fontWeight: 800
      }
    }, done ? '✓' : i + 1), /*#__PURE__*/React.createElement("span", {
      style: {
        fontSize: 'var(--sb-body-sm-size)',
        fontWeight: active ? 700 : 600,
        color: textColor,
        whiteSpace: 'nowrap'
      }
    }, step.label)), !last && /*#__PURE__*/React.createElement("span", {
      style: {
        background: done ? 'var(--sb-accent)' : 'var(--sb-border)',
        ...(vertical ? {
          width: 2,
          height: 24,
          marginLeft: 13
        } : {
          height: 2,
          flex: 1,
          minWidth: 24,
          margin: '0 var(--sb-space-2)',
          marginBottom: 22
        })
      }
    }));
  }));
}
Object.assign(__ds_scope, { Stepper });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/navigation/Stepper.jsx", error: String((e && e.message) || e) }); }

// components/navigation/Tabs.jsx
try { (() => {
/**
 * Tabs — underline tab navigation
 *
 * @startingPoint section="Navigation" subtitle="Underline tabs" viewport="480x140"
 */
function Tabs({
  tabs = [],
  active,
  onChange,
  children
}) {
  return /*#__PURE__*/React.createElement("div", {
    style: {
      fontFamily: 'var(--sb-font-sans)'
    }
  }, /*#__PURE__*/React.createElement("div", {
    role: "tablist",
    style: {
      display: 'flex',
      gap: 'var(--sb-space-1)',
      borderBottom: '1px solid var(--sb-border)'
    }
  }, tabs.map(tab => {
    const isActive = active === tab.id;
    return /*#__PURE__*/React.createElement("button", {
      key: tab.id,
      role: "tab",
      "aria-selected": isActive,
      onClick: () => onChange?.(tab.id),
      style: {
        display: 'inline-flex',
        alignItems: 'center',
        gap: 'var(--sb-space-2)',
        padding: '10px 14px',
        border: 'none',
        background: 'none',
        cursor: 'pointer',
        fontFamily: 'inherit',
        fontSize: 'var(--sb-body-md-size)',
        fontWeight: isActive ? 700 : 600,
        color: isActive ? 'var(--sb-primary)' : 'var(--sb-text-muted)',
        borderBottom: `2px solid ${isActive ? 'var(--sb-primary)' : 'transparent'}`,
        marginBottom: -1,
        transition: 'color var(--sb-timing-fast)'
      }
    }, tab.label, tab.badge != null && /*#__PURE__*/React.createElement("span", {
      style: {
        fontSize: 'var(--sb-label-sm-size)',
        fontWeight: 700,
        padding: '1px 7px',
        borderRadius: 'var(--sb-radius-pill)',
        background: isActive ? 'var(--sb-primary-100)' : 'var(--sb-neutral-100)',
        color: isActive ? 'var(--sb-primary-800)' : 'var(--sb-text-muted)'
      }
    }, tab.badge));
  })), children && /*#__PURE__*/React.createElement("div", {
    role: "tabpanel",
    style: {
      padding: 'var(--sb-space-4) 0',
      color: 'var(--sb-text)'
    }
  }, children));
}
Object.assign(__ds_scope, { Tabs });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/navigation/Tabs.jsx", error: String((e && e.message) || e) }); }

__ds_ns.Button = __ds_scope.Button;

__ds_ns.Table = __ds_scope.Table;

__ds_ns.Alert = __ds_scope.Alert;

__ds_ns.Avatar = __ds_scope.Avatar;

__ds_ns.Badge = __ds_scope.Badge;

__ds_ns.Chip = __ds_scope.Chip;

__ds_ns.Drawer = __ds_scope.Drawer;

__ds_ns.Modal = __ds_scope.Modal;

__ds_ns.Progress = __ds_scope.Progress;

__ds_ns.Skeleton = __ds_scope.Skeleton;

__ds_ns.Tag = __ds_scope.Tag;

__ds_ns.Timer = __ds_scope.Timer;

__ds_ns.Toast = __ds_scope.Toast;

__ds_ns.Tooltip = __ds_scope.Tooltip;

__ds_ns.Checkbox = __ds_scope.Checkbox;

__ds_ns.CodeInput = __ds_scope.CodeInput;

__ds_ns.DatePicker = __ds_scope.DatePicker;

__ds_ns.FileUpload = __ds_scope.FileUpload;

__ds_ns.Input = __ds_scope.Input;

__ds_ns.Radio = __ds_scope.Radio;

__ds_ns.SearchBar = __ds_scope.SearchBar;

__ds_ns.Select = __ds_scope.Select;

__ds_ns.Switch = __ds_scope.Switch;

__ds_ns.Card = __ds_scope.Card;

__ds_ns.EmptyState = __ds_scope.EmptyState;

__ds_ns.StatCard = __ds_scope.StatCard;

__ds_ns.Breadcrumb = __ds_scope.Breadcrumb;

__ds_ns.Pagination = __ds_scope.Pagination;

__ds_ns.Stepper = __ds_scope.Stepper;

__ds_ns.Tabs = __ds_scope.Tabs;

})();
