import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { AuthStore } from '../accounts/auth-store';
import { TestCard, TestCardsApi } from './test-cards-api';
import {
  HeartOffer,
  HeartPurchase,
  HeartPurchaseInput,
  HeartPurchasePage,
  HeartPurchasesApi,
} from './heart-purchases-api';

@Injectable()
export class HeartCheckoutStore {
  private readonly api = inject(HeartPurchasesApi);
  private readonly wallet = inject(TestCardsApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private readonly storageKey = `lexigo.heart-checkout.${this.owner}`;
  private generation = 0;
  readonly offer = signal<HeartOffer | null>(null);
  readonly cards = signal<readonly TestCard[]>([]);
  readonly history = signal<HeartPurchasePage | null>(null);
  readonly result = signal<HeartPurchase | null>(null);
  readonly pending = signal<HeartPurchaseInput | null>(this.restore());
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly checking = signal(false);
  readonly canRetry = signal(false);
  readonly recoverySaved = signal(true);
  readonly error = signal('');

  constructor() {
    effect(() => {
      if (this.auth.user()?.id === this.owner) return;
      ++this.generation;
      this.forget();
      this.offer.set(null);
      this.cards.set([]);
      this.history.set(null);
      this.result.set(null);
      this.error.set('');
      this.loading.set(false);
      this.busy.set(false);
    });
  }

  async load(page = this.history()?.page ?? 1): Promise<void> {
    if (this.busy() || !this.current(this.generation)) return;
    const generation = ++this.generation;
    this.loading.set(true);
    this.error.set('');
    try {
      await this.refresh(generation, page);
    } catch {
      if (this.current(generation))
        this.error.set('Checkout could not be loaded. Please try again.');
    } finally {
      if (this.current(generation)) this.loading.set(false);
    }
    if (this.current(generation) && this.pending()) await this.recover();
  }

  async pay(cardId: string): Promise<void> {
    const offer = this.offer();
    if (!offer || this.busy() || this.loading() || this.pending() || !this.current(this.generation))
      return;
    const card = this.cards().find((item) => item.id === cardId);
    if (offer.hearts.count >= offer.maximumHearts) {
      this.error.set('Your hearts are already full. No purchase is needed.');
      return;
    }
    if (!card || card.status !== 'Active' || card.currency !== offer.currency) {
      this.error.set('Choose an active test card in the offer currency.');
      return;
    }
    const request = {
      testCardId: cardId,
      offerCode: offer.offerCode,
      idempotencyKey: crypto.randomUUID(),
    };
    this.pending.set(request);
    this.persist(request);
    this.result.set(null);
    this.canRetry.set(false);
    await this.submit();
  }

  async recover(): Promise<void> {
    const request = this.pending();
    if (!request || this.busy() || this.loading() || !this.current(this.generation)) return;
    const generation = this.generation;
    this.busy.set(true);
    this.checking.set(true);
    this.canRetry.set(false);
    this.error.set('');
    try {
      const result = await this.api.byRequest(request.idempotencyKey);
      if (this.current(generation)) await this.accept(result, generation);
    } catch (error) {
      if (!this.current(generation)) return;
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.canRetry.set(true);
        this.error.set('No completed purchase was found. You may retry the same saved request.');
      } else this.error.set('The purchase status is still uncertain. Check again before retrying.');
    } finally {
      if (this.current(generation)) {
        this.busy.set(false);
        this.checking.set(false);
      }
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
    const request = this.pending();
    if (!request) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    this.canRetry.set(false);
    try {
      const result = await this.api.purchase(request);
      if (this.current(generation)) await this.accept(result, generation);
    } catch (error) {
      if (!this.current(generation)) return;
      if (
        error instanceof HttpErrorResponse &&
        error.status >= 400 &&
        error.status < 500 &&
        error.status !== 429
      ) {
        this.forget();
        this.offer.set(null);
        this.error.set(
          typeof error.error?.title === 'string'
            ? error.error.title
            : 'The purchase was rejected. Reload checkout before trying again.',
        );
      } else
        this.error.set('The result is uncertain. Check the saved purchase status before retrying.');
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }

  private async accept(result: HeartPurchase, generation: number): Promise<void> {
    this.result.set(result);
    this.forget();
    this.offer.set(null);
    try {
      await this.refresh(generation, 1);
    } catch {
      if (this.current(generation))
        this.error.set(
          'The purchase result is saved. Reload to see your current hearts and test-card balance.',
        );
    }
  }
  private async refresh(generation: number, page: number): Promise<void> {
    const [offer, cards, history] = await Promise.all([
      this.api.offer(),
      this.wallet.cards(),
      this.api.history(page),
    ]);
    if (this.current(generation)) {
      this.offer.set(offer);
      this.cards.set(cards);
      this.history.set(history);
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
  private restore(): HeartPurchaseInput | null {
    if (!this.owner) return null;
    try {
      const raw = sessionStorage.getItem(this.storageKey);
      if (!raw) return null;
      const value: unknown = JSON.parse(raw);
      if (
        typeof value === 'object' &&
        value !== null &&
        Object.keys(value).sort().join(',') === 'idempotencyKey,offerCode,testCardId'
      ) {
        const candidate = value as HeartPurchaseInput;
        const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
        if (
          typeof candidate.offerCode === 'string' &&
          candidate.offerCode.length <= 50 &&
          guid.test(candidate.testCardId) &&
          guid.test(candidate.idempotencyKey)
        )
          return candidate;
      }
      sessionStorage.removeItem(this.storageKey);
    } catch {
      try {
        sessionStorage.removeItem(this.storageKey);
      } catch {
        /* Storage may be unavailable. */
      }
    }
    return null;
  }
  private persist(request: HeartPurchaseInput): void {
    try {
      sessionStorage.setItem(this.storageKey, JSON.stringify(request));
      this.recoverySaved.set(true);
    } catch {
      this.recoverySaved.set(false);
    }
  }
  private forget(): void {
    this.pending.set(null);
    this.canRetry.set(false);
    try {
      sessionStorage.removeItem(this.storageKey);
    } catch {
      /* Nothing sensitive is stored when browser storage is unavailable. */
    }
  }
}
