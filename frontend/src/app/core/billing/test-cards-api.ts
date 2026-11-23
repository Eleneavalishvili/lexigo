import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface TestCard {
  id: string;
  nickname: string;
  displayLastFour: string;
  currency: 'GEL' | 'USD' | 'EUR';
  balance: number;
  status: 'Active' | 'Blocked';
  version: number;
  createdAt: string;
  isDefault: boolean;
}
export interface CardTransaction {
  paymentId?: string | null;
  heartRefillPurchaseId?: string | null;
  id: string;
  testCardId: string;
  type: 'TopUp' | 'Debit' | 'Refund';
  currency: 'GEL' | 'USD' | 'EUR';
  amount: number;
  balanceAfter: number | null;
  reference: string;
  createdAt: string;
  status: 'Succeeded' | 'Failed' | 'Pending';
  orderId?: string | null;
  failureCode?: string | null;
  refundId?: string | null;
  entryKind?: 'CardTransaction' | 'PaymentAttempt' | 'HeartRefillPurchase';
}
export interface TransactionFilters {
  type?: string;
  status?: string;
  from?: string;
  to?: string;
}
export interface TransactionPage {
  items: readonly CardTransaction[];
  page: number;
  pageSize: number;
  totalCount: number;
}
export interface CardChange {
  card: TestCard;
  transaction: CardTransaction | null;
}
export interface CardOperation {
  method: 'POST' | 'PATCH';
  path: string;
  body: Record<string, unknown>;
}

@Injectable({ providedIn: 'root' })
export class TestCardsApi {
  private readonly http = inject(HttpClient);
  cards(): Promise<readonly TestCard[]> {
    return firstValueFrom(this.http.get<readonly TestCard[]>('/api/v1/me/test-cards'));
  }
  history(cardId = '', page = 1, filters: TransactionFilters = {}): Promise<TransactionPage> {
    let params = new HttpParams().set('page', page);
    if (cardId) params = params.set('cardId', cardId);
    for (const [key, value] of Object.entries(filters)) if (value) params = params.set(key, value);
    return firstValueFrom(this.http.get<TransactionPage>('/api/v1/me/transactions', { params }));
  }
  apply(operation: CardOperation): Promise<CardChange> {
    return firstValueFrom(
      this.http.request<CardChange>(operation.method, operation.path, { body: operation.body }),
    );
  }
}
