import { Component, effect, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HeartCheckoutStore } from '../../core/billing/heart-checkout-store';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { CharacterCue } from '../../shared/character-cue/character-cue';
import { HeartRecovery } from '../learning/heart-recovery';
import { AuthStore } from '../../core/accounts/auth-store';

@Component({
  selector: 'app-heart-checkout',
  imports: [RouterLink, CurrencyPipe, DatePipe, BrandLogo, CharacterCue, HeartRecovery],
  providers: [HeartCheckoutStore],
  templateUrl: './heart-checkout.html',
  styleUrl: './heart-checkout.scss',
})
export class HeartCheckout {
  protected readonly store = inject(HeartCheckoutStore);
  protected readonly selected = signal('');
  protected readonly returnTo: string;
  constructor() {
    const lesson = inject(ActivatedRoute).snapshot.queryParamMap.get('lesson') ?? '';
    this.returnTo = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(lesson)
      ? `/learn/session/${lesson}`
      : '/learn';
    const auth = inject(AuthStore);
    const owner = auth.user()?.id;
    effect(() => {
      if (auth.user()?.id !== owner) this.selected.set('');
    });
    effect(() => {
      const cards = this.store.cards();
      if (cards.some((card) => card.id === this.selected())) return;
      this.selected.set(
        cards.find(
          (card) =>
            card.isDefault &&
            card.status === 'Active' &&
            card.currency === this.store.offer()?.currency,
        )?.id ?? '',
      );
    });
    void this.store.load();
  }
  protected choose(event: Event): void {
    this.selected.set((event.target as HTMLSelectElement).value);
  }
  protected reason(code: string | null): string {
    return (
      (
        {
          CardBlocked: 'The test card is blocked.',
          CurrencyMismatch: 'Choose a GEL test card.',
          InsufficientBalance: 'Add simulated funds to your test card.',
          HeartsAlreadyFull: 'Your hearts are already full. No refill was needed.',
        } as Record<string, string>
      )[code ?? ''] ?? 'The purchase did not complete.'
    );
  }
}
