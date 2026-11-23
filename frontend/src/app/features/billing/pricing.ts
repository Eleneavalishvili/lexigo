import { Component, DestroyRef, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Plan, SubscriptionsApi } from '../../core/billing/subscriptions-api';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

@Component({
  selector: 'app-pricing',
  imports: [RouterLink, CurrencyPipe, BrandLogo],
  templateUrl: './pricing.html',
  styleUrl: './subscription.scss',
})
export class Pricing {
  protected readonly plans = signal<readonly Plan[]>([]);
  protected readonly error = signal('');
  private readonly api = inject(SubscriptionsApi);
  private readonly destroy = inject(DestroyRef);
  constructor() {
    void this.load();
  }
  protected async load(): Promise<void> {
    this.error.set('');
    try {
      const plans = await this.api.plans();
      if (!this.destroy.destroyed) this.plans.set(plans);
    } catch {
      if (!this.destroy.destroyed)
        this.error.set('The plan catalog could not be loaded. Try again.');
    }
  }
}
