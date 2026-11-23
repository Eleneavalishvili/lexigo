import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface TwoFactorStatus {
  enabled: boolean;
  recoveryCodesRemaining: number;
}
export interface AuthenticatorSetup {
  sharedKey: string;
  authenticatorUri: string;
}

@Injectable({ providedIn: 'root' })
export class TwoFactorApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/me/two-factor';
  status(): Promise<TwoFactorStatus> {
    return firstValueFrom(this.http.get<TwoFactorStatus>(this.url));
  }
  setup(currentPassword: string): Promise<AuthenticatorSetup> {
    return firstValueFrom(
      this.http.post<AuthenticatorSetup>(this.url + '/setup', { currentPassword }),
    );
  }
  enable(currentPassword: string, code: string): Promise<{ recoveryCodes: string[] }> {
    return firstValueFrom(
      this.http.post<{ recoveryCodes: string[] }>(this.url + '/enable', { currentPassword, code }),
    );
  }
  regenerate(currentPassword: string, code: string): Promise<{ recoveryCodes: string[] }> {
    return firstValueFrom(
      this.http.post<{ recoveryCodes: string[] }>(this.url + '/recovery-codes', {
        currentPassword,
        code,
      }),
    );
  }
  disable(currentPassword: string, code?: string, recoveryCode?: string): Promise<unknown> {
    return firstValueFrom(
      this.http.post(this.url + '/disable', { currentPassword, code, recoveryCode }),
    );
  }
}
