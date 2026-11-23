import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ActiveSession } from './auth-models';

export interface DeletionResponsibility {
  code: string;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class AccountApi {
  private readonly http = inject(HttpClient);
  deletionResponsibilities(): Promise<DeletionResponsibility[]> {
    return firstValueFrom(
      this.http.get<DeletionResponsibility[]>('/api/v1/me/deletion-responsibilities'),
    );
  }
  exportLearningData(vocabularyOnly: boolean): Promise<Blob> {
    const path = vocabularyOnly ? '/api/v1/me/export/vocabulary' : '/api/v1/me/export';
    return firstValueFrom(this.http.get(path, { responseType: 'blob' }));
  }
  async deleteAccount(body: {
    currentPassword: string;
    confirmation: string;
    authenticatorCode: string;
    recoveryCode: string;
  }): Promise<void> {
    const csrf = await firstValueFrom(
      this.http.get<{ requestToken: string }>('/api/v1/me/deletion-verification'),
    );
    await firstValueFrom(
      this.http.delete<void>('/api/v1/me', {
        body,
        headers: { 'X-XSRF-TOKEN': csrf.requestToken },
      }),
    );
  }
  sessions(): Promise<readonly ActiveSession[]> {
    return firstValueFrom(this.http.get<ActiveSession[]>('/api/v1/me/sessions'));
  }
  revoke(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>('/api/v1/me/sessions/' + encodeURIComponent(id)));
  }
  revokeAll(): Promise<void> {
    return firstValueFrom(this.http.delete<void>('/api/v1/me/sessions'));
  }
  changePassword(currentPassword: string, newPassword: string): Promise<unknown> {
    return firstValueFrom(this.http.put('/api/v1/me/password', { currentPassword, newPassword }));
  }
  forgot(email: string): Promise<unknown> {
    return firstValueFrom(this.http.post('/api/v1/auth/forgot-password', { email }));
  }
  reset(userId: string, token: string, newPassword: string): Promise<unknown> {
    return firstValueFrom(
      this.http.post('/api/v1/auth/reset-password', { userId, token, newPassword }),
    );
  }
}
