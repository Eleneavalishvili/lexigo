import { Component, inject } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { SubscriptionHistoryStore } from '../../core/billing/subscription-history-store';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

@Component({
  selector: 'app-subscription-history',
  imports: [RouterLink, CurrencyPipe, DatePipe, BrandLogo],
  providers: [SubscriptionHistoryStore],
  templateUrl: './subscription-history.html',
  styleUrls: ['./subscription.scss', './subscription-history.scss'],
})
export class SubscriptionHistory {
  protected readonly store = inject(SubscriptionHistoryStore);
  constructor() {
    void this.store.load();
  }
  protected eventLabel(type: string): string {
    const labels: Record<string, string> = {
      Refunded: 'Paid period fully refunded',
      PartiallyRefunded: 'Partial refund credited',
      PriorPeriodRefunded: 'Earlier payment fully refunded',
      Activated: 'Premium activated',
      Renewed: 'Premium renewed',
      Expired: 'Premium period ended',
      CancellationRequested: 'Period-end cancellation requested',
      Resumed: 'Subscription resumed',
      RenewalPreferenceChanged: 'Renewal preference saved',
      PlanChangeScheduled: 'Next-period choice saved',
      RenewalDisabledForNewPurchase: 'New purchase started with renewal off',
    };
    return labels[type] ?? 'Subscription update';
  }
}
