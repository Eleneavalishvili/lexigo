import { Component, effect, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SubscriptionSettings } from './subscription-settings';
import { BillingStore } from '../../core/billing/billing-store';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { CharacterCue } from '../../shared/character-cue/character-cue';
import { AuthStore } from '../../core/accounts/auth-store';

@Component({
  selector: 'app-subscription-checkout',
  imports: [RouterLink, CurrencyPipe, DatePipe, BrandLogo, CharacterCue, SubscriptionSettings],
  providers: [BillingStore],
  templateUrl: './subscription-checkout.html',
  styleUrl: './subscription.scss',
})
export class SubscriptionCheckout {
  protected readonly store = inject(BillingStore);
  protected readonly selectedPrice = signal('');
  protected readonly selectedCard = signal('');
  constructor() {
    const price = inject(ActivatedRoute).snapshot.queryParamMap.get('price') ?? '';
    const auth = inject(AuthStore);
    const owner = auth.user()?.id;
    effect(() => {
      if (auth.user()?.id !== owner) {
        this.selectedPrice.set('');
        this.selectedCard.set('');
      }
    });
    effect(() => {
      const prices = this.store.plans().find((plan) => plan.code === 'PREMIUM')?.prices ?? [];
      if (!prices.some((item) => item.id === this.selectedPrice()))
        this.selectedPrice.set(prices.find((item) => item.id === price)?.id ?? prices[0]?.id ?? '');
    });
    effect(() => {
      const cards = this.store.cards();
      if (!cards.some((item) => item.id === this.selectedCard()))
        this.selectedCard.set(
          cards.find(
            (item) =>
              item.isDefault &&
              item.status === 'Active' &&
              item.currency === this.store.order()?.currency,
          )?.id ?? '',
        );
    });
    void this.store.load();
  }
  protected choosePrice(event: Event): void {
    this.selectedPrice.set((event.target as HTMLSelectElement).value);
  }
  protected chooseCard(event: Event): void {
    this.selectedCard.set((event.target as HTMLSelectElement).value);
  }
  protected failure(code: string | null): string {
    return (
      (
        {
          CardBlocked: 'The card is blocked.',
          CurrencyMismatch: 'The card currency does not match this order.',
          InsufficientBalance: 'Add card balance to this card.',
          TechnicalFailure: 'The payment could not be processed. Your balance was not charged.',
          AlreadySubscribed: 'You already have an active subscription.',
          OrderExpired: 'This quote expired. Review a fresh order.',
        } as Record<string, string>
      )[code ?? ''] ?? 'The payment did not complete.'
    );
  }
}
