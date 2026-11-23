import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface SubscriptionHistoryItem {
  id: string;
  planCode: string;
  status: string;
  period: string;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  isCurrent: boolean;
  cancelAtPeriodEnd: boolean;
}
export interface SubscriptionEventItem {
  id: string;
  type: string;
  occurredAt: string;
  bySystem: boolean;
  paymentId: string | null;
  amount: number | null;
  currency: string | null;
  period: string | null;
  paidPeriodStart: string | null;
  paidPeriodEnd: string | null;
  stopReason: string | null;
}
export interface HistoryPage<T> {
  items: readonly T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
@Injectable({ providedIn: 'root' })
export class SubscriptionHistoryApi {
  private readonly http = inject(HttpClient);
  list(page = 1): Promise<HistoryPage<SubscriptionHistoryItem>> {
    return firstValueFrom(
      this.http.get<HistoryPage<SubscriptionHistoryItem>>('/api/v1/me/subscriptions', {
        params: new HttpParams().set('page', page),
      }),
    );
  }
  events(id: string, page = 1): Promise<HistoryPage<SubscriptionEventItem>> {
    return firstValueFrom(
      this.http.get<HistoryPage<SubscriptionEventItem>>(`/api/v1/me/subscriptions/${id}/events`, {
        params: new HttpParams().set('page', page),
      }),
    );
  }
}
