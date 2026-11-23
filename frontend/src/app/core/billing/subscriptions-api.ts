import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface PlanFeature {
  code: string;
  description: string;
  limitValue: number | null;
  available: boolean;
}
export interface PlanPrice {
  id: string;
  period: 'Monthly' | 'Annual';
  amount: number;
  currency: string;
}
export interface Plan {
  id: string;
  code: string;
  name: string;
  prices: readonly PlanPrice[];
  features: readonly PlanFeature[];
}
export interface Subscription {
  id: string | null;
  planCode: string;
  status: string;
  period: string | null;
  currentPeriodStart: string | null;
  currentPeriodEnd: string | null;
  cancelAtPeriodEnd: boolean;
  autoRenew: boolean;
  defaultTestCardId: string | null;
  scheduledPlanPriceId: string | null;
  scheduledPlanPrice?: PlanPrice | null;
  renewalProcessingAvailable?: boolean;
  renewalConsentRequired?: boolean;
  renewalTargetSubscriptionId?: string | null;
}
export interface Entitlements {
  planCode: string;
  features: readonly PlanFeature[];
}
export interface BillingOrder {
  id: string;
  planPriceId: string;
  amount: number;
  currency: string;
  period: string;
  status: string;
  createdAt: string;
  expiresAt: string;
  subscriptionId: string | null;
}
export interface Payment {
  id: string;
  orderId: string;
  testCardId: string;
  status: 'Succeeded' | 'Failed' | 'Pending';
  failureCode: string | null;
  amount: number;
  currency: string;
  createdAt: string;
  completedAt: string | null;
  subscriptionId: string | null;
  receiptNumber: string | null;
}

@Injectable({ providedIn: 'root' })
export class SubscriptionsApi {
  private readonly http = inject(HttpClient);
  plans(): Promise<readonly Plan[]> {
    return firstValueFrom(this.http.get<readonly Plan[]>('/api/v1/plans'));
  }
  subscription(): Promise<Subscription> {
    return firstValueFrom(this.http.get<Subscription>('/api/v1/me/subscription'));
  }
  entitlements(): Promise<Entitlements> {
    return firstValueFrom(this.http.get<Entitlements>('/api/v1/me/entitlements'));
  }
  create(priceId: string, key: string): Promise<BillingOrder> {
    return firstValueFrom(
      this.http.post<BillingOrder>('/api/v1/me/billing-orders', {
        planPriceId: priceId,
        idempotencyKey: key,
      }),
    );
  }
  order(id: string): Promise<BillingOrder> {
    return firstValueFrom(
      this.http.get<BillingOrder>(`/api/v1/me/billing-orders/${encodeURIComponent(id)}`),
    );
  }
  cancelOrder(id: string): Promise<BillingOrder> {
    return firstValueFrom(
      this.http.post<BillingOrder>(
        `/api/v1/me/billing-orders/${encodeURIComponent(id)}/cancel`,
        null,
      ),
    );
  }
  orderByRequest(key: string): Promise<BillingOrder> {
    return firstValueFrom(
      this.http.get<BillingOrder>(
        `/api/v1/me/billing-orders/by-request/${encodeURIComponent(key)}`,
      ),
    );
  }
  pay(orderId: string, cardId: string, key: string, technical: boolean): Promise<Payment> {
    return firstValueFrom(
      this.http.post<Payment>(`/api/v1/me/billing-orders/${encodeURIComponent(orderId)}/payments`, {
        testCardId: cardId,
        idempotencyKey: key,
        simulateTechnicalFailure: technical,
      }),
    );
  }
  payment(id: string): Promise<Payment> {
    return firstValueFrom(this.http.get<Payment>(`/api/v1/me/payments/${encodeURIComponent(id)}`));
  }
  paymentByRequest(key: string): Promise<Payment> {
    return firstValueFrom(
      this.http.get<Payment>(`/api/v1/me/payments/by-request/${encodeURIComponent(key)}`),
    );
  }
  manage(
    kind: 'cancel' | 'resume' | 'renewal' | 'scheduled-plan',
    body: Record<string, unknown>,
  ): Promise<Subscription> {
    return firstValueFrom(
      this.http.request<Subscription>(
        kind === 'cancel' || kind === 'resume' ? 'POST' : 'PUT',
        `/api/v1/me/subscription/${kind}`,
        { body },
      ),
    );
  }
  receipt(id: string): Promise<Blob> {
    return firstValueFrom(
      this.http.get(`/api/v1/me/payments/${encodeURIComponent(id)}/receipt`, {
        responseType: 'blob',
      }),
    );
  }
}
