import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { AuthStore } from '../accounts/auth-store';
import {
  CardOperation,
  TestCard,
  TestCardsApi,
  TransactionPage,
  TransactionFilters,
} from './test-cards-api';

@Injectable()
export class TestCardsStore {
  private readonly api = inject(TestCardsApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private generation = 0;
  readonly cards = signal<readonly TestCard[]>([]);
  readonly history = signal<TransactionPage | null>(null);
  readonly selectedCard = signal('');
  readonly filters = signal<TransactionFilters>({});
  readonly page = signal(1);
  readonly loading = signal(false);
  readonly working = signal(false);
  readonly pending = signal<CardOperation | null>(null);
  readonly error = signal('');
  readonly notice = signal('');

  constructor() {
    effect(() => {
      if (this.auth.user()?.id === this.owner) return;
      ++this.generation;
      this.cards.set([]);
      this.history.set(null);
      this.pending.set(null);
      this.selectedCard.set('');
      this.filters.set({});
      this.page.set(1);
      this.error.set('');
      this.notice.set('');
      this.loading.set(false);
      this.working.set(false);
    });
  }

  async load(cardId = this.selectedCard(), page = this.page()): Promise<void> {
    if (this.working() || !this.current(this.generation)) return;
    const generation = ++this.generation;
    this.selectedCard.set(cardId);
    this.page.set(page);
    this.loading.set(true);
    this.history.set(null);
    this.error.set('');
    try {
      await this.refresh(generation);
    } catch {
      if (this.current(generation)) this.error.set('Your wallet could not be loaded. Try again.');
    } finally {
      if (this.current(generation)) this.loading.set(false);
    }
  }

  async filter(filters: TransactionFilters): Promise<void> {
    if (this.loading() || this.working() || !this.current(this.generation)) return;
    this.filters.set({ ...filters });
    await this.load(this.selectedCard(), 1);
  }

  async change(operation: CardOperation): Promise<void> {
    if (this.pending() || this.loading() || this.working() || !this.current(this.generation))
      return;
    this.pending.set({
      ...operation,
      body: { ...operation.body, idempotencyKey: crypto.randomUUID() },
    });
    await this.retry();
  }

  async retry(): Promise<void> {
    const operation = this.pending();
    if (!operation || this.working() || this.loading() || !this.current(this.generation)) return;
    const generation = this.generation;
    this.working.set(true);
    this.error.set('');
    this.notice.set('');
    let saved = false;
    try {
      await this.api.apply(operation);
      if (!this.current(generation)) return;
      saved = true;
      this.pending.set(null);
      this.notice.set('Your wallet is updated. Review your card balance and transaction history.');
      await this.refresh(generation);
    } catch (error) {
      if (!this.current(generation)) return;
      if (saved)
        this.error.set('Your change is saved. Reload the wallet to see its latest balance.');
      else if (
        error instanceof HttpErrorResponse &&
        error.status >= 400 &&
        error.status < 500 &&
        error.status !== 429
      ) {
        this.pending.set(null);
        this.error.set(
          typeof error.error?.title === 'string'
            ? error.error.title
            : 'This test-card change was rejected. Reload and try again.',
        );
      } else
        this.error.set(
          'The result is uncertain. Retry the same request to recover it safely; it cannot add the balance twice.',
        );
    } finally {
      if (this.current(generation)) this.working.set(false);
    }
  }

  private async refresh(generation: number): Promise<void> {
    const [cards, history] = await Promise.all([
      this.api.cards(),
      this.api.history(this.selectedCard(), this.page(), this.filters()),
    ]);
    if (this.current(generation)) {
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
}
