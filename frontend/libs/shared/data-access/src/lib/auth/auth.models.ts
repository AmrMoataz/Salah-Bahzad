export type StaffRole = 'Assistant' | 'Teacher';

export interface StaffInfo {
  id: string;
  displayName: string;
  email: string;
  role: StaffRole;
  permissions: string[];
}

export interface AuthTokenResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
  staff: StaffInfo;
}

export interface AuthState {
  staff: StaffInfo | null;
  accessToken: string | null;
  refreshToken: string | null;
  isLoading: boolean;
  error: string | null;
}
