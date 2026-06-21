export interface NavItem {
  label: string;
  /** Raw inline SVG markup (rendered via a trusted-HTML bypass — Angular strips `<svg>` otherwise). */
  icon: string;
  /** Router link for the item. */
  route: string;
  /** Exact-match active highlighting (the home `''`/`/` item needs this). */
  exact?: boolean;
  /**
   * When true the item is a placeholder for a screen that doesn't exist yet (S0 gates everything
   * but Home off). It renders greyed with a "Soon" tag and does not navigate. Its phase flips this.
   */
  disabled?: boolean;
}
