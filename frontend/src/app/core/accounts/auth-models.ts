export interface CurrentAccount {
  id: string;
  email: string;
  displayName: string;
  emailConfirmed: boolean;
  roles: readonly string[];
  permissions?: readonly string[];
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: CurrentAccount;
}

export interface TwoFactorResponse {
  requiresTwoFactor: true;
}

export interface ActiveSession {
  id: string;
  deviceLabel: string;
  createdAt: string;
  expiresAt: string;
  isCurrent: boolean;
}
