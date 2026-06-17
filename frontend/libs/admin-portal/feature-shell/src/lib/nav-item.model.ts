export interface NavItem {
  label: string;
  icon: string;
  route: string;
  permission?: string;
  badgeSignal?: () => number | null;
}
