import { Component, effect, inject, input, output } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { AuthStore } from '../../core/accounts/auth-store';
import { Plan, Subscription } from '../../core/billing/subscriptions-api';
import { TestCard } from '../../core/billing/test-cards-api';
import {
  SubscriptionSetting,
  SubscriptionSettingsStore,
} from '../../core/billing/subscription-settings-store';

@Component({
  selector: 'app-subscription-settings',
  imports: [ReactiveFormsModule, CurrencyPipe, DatePipe],
  providers: [SubscriptionSettingsStore],
  templateUrl: './subscription-settings.html',
  styleUrl: './subscription.scss',
})
export class SubscriptionSettings {
  readonly subscription = input.required<Subscription>();
  readonly plans = input<readonly Plan[]>([]);
  readonly cards = input<readonly TestCard[]>([]);
  readonly disabled = input(false);
  readonly changed = output<void>();
  protected readonly store = inject(SubscriptionSettingsStore);
  private readonly builder = inject(FormBuilder).nonNullable;
  protected readonly renewal = this.builder.group({ autoRenew: [false], cardId: [''] });
  protected readonly schedule = this.builder.group({ priceId: [''] });
  constructor() {
    const auth = inject(AuthStore);
    const owner = auth.user()?.id;
    effect(() => {
      if (auth.user()?.id !== owner) {
        this.renewal.reset({ autoRenew: false, cardId: '' });
        this.schedule.reset({ priceId: '' });
        return;
      }
      const value = this.subscription();
      this.renewal.setValue({ autoRenew: value.autoRenew, cardId: value.defaultTestCardId ?? '' });
      this.schedule.setValue({ priceId: value.scheduledPlanPriceId ?? '' });
    });
  }
  protected locked(): boolean {
    return this.disabled() || this.store.busy() || !!this.store.pending();
  }
  protected async cancel(cancel: boolean): Promise<void> {
    await this.send(cancel ? 'cancel' : 'resume', { subscriptionId: this.subscription().id });
  }
  protected async saveRenewal(): Promise<void> {
    const value = this.renewal.getRawValue();
    await this.send('renewal', {
      subscriptionId: this.subscription().id,
      autoRenew: value.autoRenew,
      defaultTestCardId: value.cardId || null,
    });
  }
  protected async stopRenewal(): Promise<void> {
    await this.send('renewal', {
      subscriptionId: this.subscription().renewalTargetSubscriptionId,
      autoRenew: false,
      defaultTestCardId: this.subscription().defaultTestCardId,
    });
  }
  protected async saveSchedule(): Promise<void> {
    await this.send('scheduled-plan', {
      subscriptionId: this.subscription().id,
      planPriceId: this.schedule.controls.priceId.value || null,
    });
  }
  protected async retry(): Promise<void> {
    if (await this.store.retry()) this.changed.emit();
  }
  private async send(kind: SubscriptionSetting, body: Record<string, unknown>): Promise<void> {
    if (this.disabled()) return;
    if (await this.store.change(kind, body)) this.changed.emit();
  }
}
