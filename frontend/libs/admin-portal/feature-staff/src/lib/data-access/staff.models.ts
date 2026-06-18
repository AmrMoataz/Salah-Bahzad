import { StaffRole } from '@sb/shared/data-access';

/** A staff member as returned by the API (list row and single record share one shape). */
export interface StaffListItem {
  id: string;
  displayName: string;
  email: string;
  role: StaffRole;
  isActive: boolean;
  lastSeenAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export type StaffDetail = StaffListItem;

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface CreateStaffRequest {
  displayName: string;
  email: string;
  role: StaffRole;
}

export type UpdateStaffRequest = CreateStaffRequest;

export interface StaffListQuery {
  search?: string;
  role?: StaffRole | null;
  isActive?: boolean | null;
  page?: number;
  pageSize?: number;
}

export interface PasswordResetResponse {
  /** The address Firebase was asked to email the reset link to. */
  email: string;
}
