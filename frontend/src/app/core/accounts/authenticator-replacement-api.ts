import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface ReplacementSetup {
  requestId: string;
  sharedKey: string;
  expiresAt: string;
}

@Injectable({ providedIn: 'root' })
export class AuthenticatorReplacementApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/me/two-factor/replacement';

  begin(currentPassword: string): Promise<ReplacementSetup> {
    return firstValueFrom(this.http.post<ReplacementSetup>(this.url, { currentPassword }));
  }

  confirm(
    requestId: string,
    currentPassword: string,
    code: string,
  ): Promise<{ recoveryCodes: string[] }> {
    return firstValueFrom(
      this.http.post<{ recoveryCodes: string[] }>(this.url + '/confirm', {
        requestId,
        currentPassword,
        code,
      }),
    );
  }
}
