export interface NavItem {
  label: string;
  /** Raw inline SVG markup (rendered via a trusted-HTML bypass in the sidebar). */
  icon: string;
  route: string;
  /** When set, the item is hidden unless the signed-in staff holds this permission. */
  permission?: string;
  /** Optional live badge count (e.g. pending approvals). Hidden when null/0. */
  badgeSignal?: () => number | null;
}

export interface NavGroup {
  title: string;
  items: NavItem[];
}
