import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class EmailChangeApi {
  private readonly http = inject(HttpClient);

  request(
    newEmail: string,
    currentPassword: string,
    authenticatorCode: string,
    recoveryCode: string,
  ): Promise<unknown> {
    return firstValueFrom(
      this.http.put('/api/v1/me/email', {
        newEmail,
        currentPassword,
        authenticatorCode,
        recoveryCode,
      }),
    );
  }

  confirm(userId: string, requestId: string, token: string): Promise<unknown> {
    return firstValueFrom(
      this.http.post('/api/v1/auth/confirm-email-change', { userId, requestId, token }),
    );
  }
}
