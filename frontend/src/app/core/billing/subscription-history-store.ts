import { DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { AuthStore } from '../accounts/auth-store';
import {
  HistoryPage,
  SubscriptionEventItem,
  SubscriptionHistoryApi,
  SubscriptionHistoryItem,
} from './subscription-history-api';

@Injectable()
export class SubscriptionHistoryStore {
  private readonly api = inject(SubscriptionHistoryApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private generation = 0;
  private eventRequest = 0;
  readonly subscriptions = signal<HistoryPage<SubscriptionHistoryItem> | null>(null);
  readonly events = signal<HistoryPage<SubscriptionEventItem> | null>(null);
  readonly selected = signal<string | null>(null);
  readonly loading = signal(false);
  readonly loadingEvents = signal(false);
  readonly error = signal('');
  readonly eventError = signal('');
  constructor() {
    effect(() => {
      if (this.auth.user()?.id === this.owner) return;
      ++this.generation;
      ++this.eventRequest;
      this.subscriptions.set(null);
      this.events.set(null);
      this.selected.set(null);
      this.loading.set(false);
      this.loadingEvents.set(false);
      this.error.set('');
      this.eventError.set('');
    });
  }
  async load(page = 1): Promise<void> {
    if (!this.current(this.generation)) return;
    const generation = ++this.generation;
    ++this.eventRequest;
    this.loading.set(true);
    this.loadingEvents.set(false);
    this.error.set('');
    this.eventError.set('');
    this.subscriptions.set(null);
    this.events.set(null);
    this.selected.set(null);
    try {
      const response = await this.api.list(page);
      if (!this.current(generation)) return;
      this.subscriptions.set(response);
      if (response.items.length) await this.select(response.items[0].id);
    } catch {
      if (this.current(generation))
        this.error.set('Your subscription history could not load. Please refresh to try again.');
    } finally {
      if (this.current(generation)) this.loading.set(false);
    }
  }
  async select(id: string, page = 1): Promise<void> {
    if (
      !this.current(this.generation) ||
      !this.subscriptions()?.items.some((item) => item.id === id)
    )
      return;
    const generation = this.generation;
    const request = ++this.eventRequest;
    this.selected.set(id);
    this.events.set(null);
    this.loadingEvents.set(true);
    this.eventError.set('');
    try {
      const response = await this.api.events(id, page);
      if (this.current(generation) && request === this.eventRequest) this.events.set(response);
    } catch {
      if (this.current(generation) && request === this.eventRequest)
        this.eventError.set('These subscription updates could not load. Please try again.');
    } finally {
      if (this.current(generation) && request === this.eventRequest) this.loadingEvents.set(false);
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
