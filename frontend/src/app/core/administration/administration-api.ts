import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface Page<T> {
  total: number;
  page: number;
  pageSize: number;
  items: readonly T[];
}
export interface ManagedUser {
  id: string;
  email: string;
  displayName: string;
  status: string;
  emailConfirmed: boolean;
  twoFactorEnabled: boolean;
  roles: readonly string[];
  languageIds: readonly number[];
}
export interface AuditEntry {
  id: string;
  actorId: string | null;
  action: string;
  entityType: string;
  entityId: string;
  reason: string | null;
  occurredAt: string;
  traceId: string;
}

@Injectable({ providedIn: 'root' })
export class AdministrationApi {
  private readonly http = inject(HttpClient);
  users(search: string, role: string, status: string, page: number): Promise<Page<ManagedUser>> {
    let params = new HttpParams().set('page', page);
    if (search) params = params.set('search', search);
    if (role) params = params.set('role', role);
    if (status) params = params.set('status', status);
    return firstValueFrom(this.http.get<Page<ManagedUser>>('/api/v1/admin/users', { params }));
  }
  languages(): Promise<readonly { id: number; englishName: string }[]> {
    return firstValueFrom(
      this.http.get<{ id: number; englishName: string }[]>('/api/v1/admin/languages'),
    );
  }
  history(userId: string | null, page: number): Promise<Page<AuditEntry>> {
    let params = new HttpParams().set('page', page);
    if (userId) params = params.set('userId', userId);
    return firstValueFrom(this.http.get<Page<AuditEntry>>('/api/v1/admin/audit-logs', { params }));
  }
  status(id: string, status: string, reason: string, currentPassword: string): Promise<void> {
    return firstValueFrom(
      this.http.patch<void>('/api/v1/admin/users/' + encodeURIComponent(id) + '/status', {
        status,
        reason,
        currentPassword,
      }),
    );
  }
  roles(
    id: string,
    roles: readonly string[],
    languageIds: readonly number[],
    reason: string,
    currentPassword: string,
  ): Promise<void> {
    return firstValueFrom(
      this.http.put<void>('/api/v1/admin/users/' + encodeURIComponent(id) + '/roles', {
        roles,
        languageIds,
        reason,
        currentPassword,
      }),
    );
  }
}
