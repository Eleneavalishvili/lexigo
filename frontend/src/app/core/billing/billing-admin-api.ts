import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Page } from '../administration/administration-api';

export interface ManagedPlan {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  createdAt: string;
  prices: readonly {
    id: string;
    period: string;
    amount: number;
    currency: string;
    validFrom: string;
    validTo: string | null;
  }[];
  features: readonly {
    featureId: number;
    code: string;
    description: string;
    limitValue: number | null;
    available: boolean;
  }[];
}

export interface PlanMetadataChange {
  name: string;
  reason: string;
  idempotencyKey: string;
  currentPassword: string;
}
export type PlanFeatureSelection = Pick<
  ManagedPlan['features'][number],
  'featureId' | 'limitValue'
>;
export interface ManagedPayment {
  id: string;
  userId: string;
  customer: string;
  orderId: string;
  testCardId: string;
  cardStatus: string;
  status: string;
  failureCode: string | null;
  amount: number;
  currency: string;
  refundedAmount: number;
  createdAt: string;
}
export interface ManagedSubscription {
  id: string;
  userId: string;
  customer: string;
  planCode: string;
  status: string;
  period: string;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  cancelAtPeriodEnd: boolean;
}
export interface BillingAudit {
  id: string;
  actorId: string | null;
  action: string;
  entityType: string;
  entityId: string;
  reason: string | null;
  traceId: string;
  occurredAt: string;
}
export interface AdminBillingFilters {
  userId?: string;
  paymentId?: string;
  status?: string;
  from?: string;
  to?: string;
}
export interface RefundResult {
  id: string;
  paymentId: string;
  amount: number;
  currency: string;
  reason: string;
  status: string;
  completedAt: string | null;
  revokedPremium: boolean;
}
export interface RefundIntent {
  paymentId: string;
  key: string;
}
export interface BillingPermission {
  userId: string;
  granted: boolean;
  eligible: boolean;
  inherited: boolean;
}
@Injectable({ providedIn: 'root' })
export class BillingAdminApi {
  private readonly http = inject(HttpClient);
  private params(page: number, filters: AdminBillingFilters): HttpParams {
    let params = new HttpParams().set('page', page);
    for (const [key, value] of Object.entries(filters)) if (value) params = params.set(key, value);
    return params;
  }
  plans(page = 1, active?: boolean): Promise<Page<ManagedPlan>> {
    let params = new HttpParams().set('page', page);
    if (active !== undefined) params = params.set('active', active);
    return firstValueFrom(this.http.get<Page<ManagedPlan>>('/api/v1/admin/plans', { params }));
  }
  schedulePrice(
    priceId: string,
    body: {
      amount: number;
      startsAt: string;
      reason: string;
      currentPassword: string;
      idempotencyKey: string;
    },
  ): Promise<ManagedPlan['prices'][number]> {
    return firstValueFrom(
      this.http.post<ManagedPlan['prices'][number]>(
        `/api/v1/admin/plans/prices/${encodeURIComponent(priceId)}/schedule`,
        body,
      ),
    );
  }
  createPlan(body: PlanMetadataChange & { code: string }): Promise<void> {
    return firstValueFrom(this.http.post<void>('/api/v1/admin/plans', body));
  }
  planFeatures(): Promise<ManagedPlan['features']> {
    return firstValueFrom(this.http.get<ManagedPlan['features']>('/api/v1/admin/plans/features'));
  }
  renamePlan(
    id: string,
    body: PlanMetadataChange & {
      expectedName: string;
      features?: PlanFeatureSelection[];
      expectedFeatures?: PlanFeatureSelection[];
      isActive?: boolean;
      expectedIsActive?: boolean;
    },
  ): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(`/api/v1/admin/plans/${encodeURIComponent(id)}`, body),
    );
  }
  payments(page = 1, filters: AdminBillingFilters = {}): Promise<Page<ManagedPayment>> {
    return firstValueFrom(
      this.http.get<Page<ManagedPayment>>('/api/v1/admin/payments', {
        params: this.params(page, filters),
      }),
    );
  }
  subscriptions(page = 1, filters: AdminBillingFilters = {}): Promise<Page<ManagedSubscription>> {
    return firstValueFrom(
      this.http.get<Page<ManagedSubscription>>('/api/v1/admin/subscriptions', {
        params: this.params(page, filters),
      }),
    );
  }
  audit(page = 1, filters: AdminBillingFilters = {}): Promise<Page<BillingAudit>> {
    return firstValueFrom(
      this.http.get<Page<BillingAudit>>('/api/v1/admin/billing-audit', {
        params: this.params(page, { from: filters.from, to: filters.to }),
      }),
    );
  }
  refund(
    intent: RefundIntent,
    amount: number,
    reason: string,
    currentPassword: string,
  ): Promise<RefundResult> {
    return firstValueFrom(
      this.http.post<RefundResult>(`/api/v1/admin/payments/${intent.paymentId}/refunds`, {
        amount,
        reason,
        currentPassword,
        idempotencyKey: intent.key,
      }),
    );
  }
  refundStatus(key: string): Promise<RefundResult> {
    return firstValueFrom(this.http.get<RefundResult>(`/api/v1/admin/refunds/by-request/${key}`));
  }
  permission(userId: string): Promise<BillingPermission> {
    return firstValueFrom(
      this.http.get<BillingPermission>(`/api/v1/admin/users/${userId}/billing-permission`),
    );
  }
  setPermission(
    userId: string,
    body: { enabled: boolean; reason: string; currentPassword: string; idempotencyKey: string },
  ): Promise<BillingPermission> {
    return firstValueFrom(
      this.http.put<BillingPermission>(`/api/v1/admin/users/${userId}/billing-permission`, body),
    );
  }
}
