import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { AuthStore } from '../accounts/auth-store';
import { TestCard, TestCardsApi } from './test-cards-api';
import {
  BillingOrder,
  Entitlements,
  Payment,
  Plan,
  Subscription,
  SubscriptionsApi,
} from './subscriptions-api';
import { PendingSubscription, restoreSubscriptionRequest } from './subscription-recovery';

@Injectable()
export class BillingStore {
  private readonly api = inject(SubscriptionsApi);
  private readonly wallet = inject(TestCardsApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private readonly storageKey = `lexigo.subscription-checkout.${this.owner}`;
  private generation = 0;
  readonly plans = signal<readonly Plan[]>([]);
  readonly subscription = signal<Subscription | null>(null);
  readonly entitlements = signal<Entitlements | null>(null);
  readonly cards = signal<readonly TestCard[]>([]);
  readonly order = signal<BillingOrder | null>(null);
  readonly result = signal<Payment | null>(null);
  readonly pending = signal<PendingSubscription | null>(
    this.owner ? restoreSubscriptionRequest(this.storageKey) : null,
  );
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly ready = signal(false);
  readonly canRetry = signal(false);
  readonly recoverySaved = signal(true);
  readonly error = signal('');

  constructor() {
    effect(() => {
      if (this.auth.user()?.id === this.owner) return;
      ++this.generation;
      this.clearPending();
      this.plans.set([]);
      this.cards.set([]);
      this.subscription.set(null);
      this.entitlements.set(null);
      this.order.set(null);
      this.result.set(null);
      this.ready.set(false);
      this.error.set('');
      this.loading.set(false);
      this.busy.set(false);
    });
  }
  async load(): Promise<void> {
    if (this.busy() || !this.current(this.generation)) return;
    const generation = ++this.generation;
    this.loading.set(true);
    this.error.set('');
    this.ready.set(false);
    try {
      await this.refresh(generation, this.order()?.id);
    } catch {
      if (this.current(generation))
        this.error.set('Your subscription could not be loaded. Please try again.');
    } finally {
      if (this.current(generation)) this.loading.set(false);
    }
    if (this.current(generation) && this.pending()) await this.recover();
  }
  async review(priceId: string): Promise<void> {
    if (
      !this.ready() ||
      this.busy() ||
      this.loading() ||
      this.pending() ||
      !this.current(this.generation)
    )
      return;
    if (this.subscription()?.id) {
      this.error.set('You already have an active subscription.');
      return;
    }
    if (
      !this.plans().some(
        (plan) => plan.code === 'PREMIUM' && plan.prices.some((price) => price.id === priceId),
      )
    ) {
      this.error.set('Choose a current Premium price.');
      return;
    }
    this.result.set(null);
    this.order.set(null);
    this.savePending({ stage: 'Order', priceId, key: crypto.randomUUID() });
    await this.submit();
  }
  async cancelOrder(): Promise<void> {
    const order = this.order();
    const generation = this.generation;
    if (
      order?.status !== 'Created' ||
      !this.ready() ||
      this.busy() ||
      this.loading() ||
      this.pending() ||
      !this.current(generation)
    )
      return;

    this.busy.set(true);
    this.error.set('');
    try {
      const cancelled = await this.api.cancelOrder(order.id);
      if (this.current(generation)) this.order.set(cancelled);
    } catch {
      if (this.current(generation)) this.ready.set(false);
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
    if (this.current(generation) && !this.ready()) await this.load();
    if (this.current(generation) && this.order()?.status === 'Created' && this.ready())
      this.error.set('Checkout is still open. You can try cancelling again.');
  }

  async pay(cardId: string, technical = false): Promise<void> {
    const order = this.order();
    if (
      !order ||
      order.status !== 'Created' ||
      !this.ready() ||
      this.busy() ||
      this.loading() ||
      this.pending() ||
      !this.current(this.generation)
    )
      return;
    if (
      !this.cards().some(
        (card) =>
          card.id === cardId && card.status === 'Active' && card.currency === order.currency,
      )
    ) {
      this.error.set('Choose an active test card in the order currency.');
      return;
    }
    this.result.set(null);
    this.savePending({
      stage: 'Payment',
      orderId: order.id,
      cardId,
      key: crypto.randomUUID(),
      technical,
    });
    await this.submit();
  }
  async recover(): Promise<void> {
    const pending = this.pending();
    if (!pending || this.busy() || this.loading() || !this.current(this.generation)) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    this.canRetry.set(false);
    try {
      if (pending.stage === 'Order') {
        const value = await this.api.orderByRequest(pending.key);
        if (this.current(generation)) {
          this.order.set(value);
          this.clearPending();
        }
      } else {
        const value = await this.api.paymentByRequest(pending.key);
        if (this.current(generation)) await this.accept(value, generation);
      }
    } catch (error) {
      if (!this.current(generation)) return;
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.canRetry.set(true);
        this.error.set('No saved result was found. Retry the same request to continue safely.');
      } else
        this.error.set(
          'The result is still uncertain. Check the request status again before retrying.',
        );
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }
  async retry(): Promise<void> {
    if (
      this.canRetry() &&
      !this.busy() &&
      !this.loading() &&
      this.pending() &&
      this.current(this.generation)
    )
      await this.submit();
  }
  private async submit(): Promise<void> {
    const pending = this.pending();
    if (!pending) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    this.canRetry.set(false);
    try {
      if (pending.stage === 'Order') {
        const order = await this.api.create(pending.priceId, pending.key);
        if (this.current(generation)) {
          this.order.set(order);
          this.clearPending();
        }
      } else {
        const payment = await this.api.pay(
          pending.orderId,
          pending.cardId,
          pending.key,
          pending.technical,
        );
        if (this.current(generation)) await this.accept(payment, generation);
      }
    } catch (error) {
      if (!this.current(generation)) return;
      if (
        error instanceof HttpErrorResponse &&
        error.status >= 400 &&
        error.status < 500 &&
        error.status !== 429
      ) {
        this.clearPending();
        this.ready.set(false);
        this.error.set(
          typeof error.error?.title === 'string'
            ? error.error.title
            : 'The request was rejected. Reload and review the current state.',
        );
      } else
        this.error.set('The result is uncertain. Check the saved request status before retrying.');
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }
  private async accept(payment: Payment, generation: number): Promise<void> {
    this.result.set(payment);
    if (payment.status === 'Pending') {
      this.ready.set(false);
      this.error.set('The payment is still processing. Check its saved status again.');
      return;
    }
    this.clearPending();
    this.ready.set(false);
    try {
      await this.refresh(generation, payment.orderId);
    } catch {
      if (this.current(generation))
        this.error.set(
          'Your payment result is saved. Reload to restore current subscription and card balances.',
        );
    }
  }
  private async refresh(generation: number, orderId?: string): Promise<void> {
    const [plans, subscription, entitlements, cards, order] = await Promise.all([
      this.api.plans(),
      this.api.subscription(),
      this.api.entitlements(),
      this.wallet.cards(),
      orderId ? this.api.order(orderId) : Promise.resolve(null),
    ]);
    if (this.current(generation)) {
      this.plans.set(plans);
      this.subscription.set(subscription);
      this.entitlements.set(entitlements);
      this.cards.set(cards);
      if (order) this.order.set(order);
      this.ready.set(true);
    }
  }
  private current(generation: number): boolean {
    return (
      !this.destroy.destroyed &&
      generation === this.generation &&
      !!this.owner &&
      this.auth.user()?.id === this.owner
    );
  }
  private savePending(request: PendingSubscription): void {
    this.pending.set(request);
    this.canRetry.set(false);
    try {
      sessionStorage.setItem(this.storageKey, JSON.stringify(request));
      this.recoverySaved.set(true);
    } catch {
      this.recoverySaved.set(false);
    }
  }
  private clearPending(): void {
    this.pending.set(null);
    this.canRetry.set(false);
    try {
      sessionStorage.removeItem(this.storageKey);
    } catch {
      /* Non-secret recovery reference only. */
    }
  }
}
