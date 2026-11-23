import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { HeartStatus } from '../learning/hearts-api';

export interface HeartOffer {
  offerCode: string;
  amount: number;
  currency: string;
  maximumHearts: number;
  hearts: HeartStatus;
}
export interface HeartPurchaseInput {
  testCardId: string;
  offerCode: string;
  idempotencyKey: string;
}
export interface HeartPurchase {
  id: string;
  testCardId: string;
  offerCode: string;
  status: 'Succeeded' | 'Failed';
  failureCode: string | null;
  amount: number;
  chargedAmount: number;
  currency: string;
  reference: string;
  heartsBefore: number;
  heartsAfter: number;
  createdAt: string;
}
export interface HeartPurchasePage {
  items: readonly HeartPurchase[];
  page: number;
  pageSize: number;
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class HeartPurchasesApi {
  private readonly http = inject(HttpClient);
  offer(): Promise<HeartOffer> {
    return firstValueFrom(this.http.get<HeartOffer>('/api/v1/me/hearts/refill-offer'));
  }
  purchase(input: HeartPurchaseInput): Promise<HeartPurchase> {
    return firstValueFrom(this.http.post<HeartPurchase>('/api/v1/me/hearts/purchases', input));
  }
  byRequest(key: string): Promise<HeartPurchase> {
    return firstValueFrom(
      this.http.get<HeartPurchase>(
        `/api/v1/me/hearts/purchases/by-request/${encodeURIComponent(key)}`,
      ),
    );
  }
  history(page = 1): Promise<HeartPurchasePage> {
    return firstValueFrom(
      this.http.get<HeartPurchasePage>('/api/v1/me/hearts/purchases', { params: { page } }),
    );
  }
}
