import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface AccountNotification {
  id: string;
  type: string;
  title: string;
  body: string;
  createdAt: string;
  readAt: string | null;
}
export interface NotificationPage {
  items: readonly AccountNotification[];
  nextBefore: string | null;
  nextId: string | null;
}
@Injectable({ providedIn: 'root' })
export class NotificationsApi {
  private readonly http = inject(HttpClient);
  page(before?: string | null, beforeId?: string | null): Promise<NotificationPage> {
    let params = new HttpParams().set('limit', 30);
    if (before && beforeId) params = params.set('before', before).set('beforeId', beforeId);
    return firstValueFrom(this.http.get<NotificationPage>('/api/v1/me/notifications', { params }));
  }
  read(id: string): Promise<AccountNotification> {
    return firstValueFrom(
      this.http.patch<AccountNotification>(`/api/v1/me/notifications/${id}/read`, {}),
    );
  }
}
