import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface RegistrationRequest {
  email: string;
  displayName: string;
  password: string;
}

@Injectable({ providedIn: 'root' })
export class RegistrationApi {
  private readonly http = inject(HttpClient);

  register(request: RegistrationRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>('/api/v1/auth/register', request);
  }

  resend(email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>('/api/v1/auth/resend-verification', { email });
  }

  confirmEmail(userId: string, token: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>('/api/v1/auth/verify-email', { userId, token });
  }
}
