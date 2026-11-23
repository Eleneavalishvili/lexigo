import { DestroyRef, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { AuthStore } from './auth-store';
import { AccountNotification, NotificationPage, NotificationsApi } from './notifications-api';

@Injectable()
export class NotificationsStore {
  private readonly api = inject(NotificationsApi);
  private readonly auth = inject(AuthStore);
  readonly items = signal<readonly AccountNotification[]>([]);
  readonly loading = signal(false);
  readonly reading = signal<string | null>(null);
  readonly error = signal('');
  readonly more = signal(false);
  private cursor: NotificationPage | null = null;
  private generation = 0;
  private owner: string | undefined;
  constructor() {
    effect(() => {
      this.owner = this.auth.user()?.id;
      this.generation++;
      this.items.set([]);
      this.cursor = null;
      this.more.set(false);
      this.loading.set(false);
      this.reading.set(null);
      this.error.set('');
      if (this.owner) untracked(() => void this.load());
    });
    inject(DestroyRef).onDestroy(() => this.generation++);
  }
  async load(reset = false): Promise<void> {
    if (!this.owner || this.loading()) return;
    const generation = this.generation;
    const owner = this.owner;
    this.loading.set(true);
    this.error.set('');
    try {
      const cursor = reset ? null : this.cursor;
      const page = await this.api.page(cursor?.nextBefore, cursor?.nextId);
      if (!this.current(generation, owner)) return;
      this.cursor = page;
      this.items.update((items) =>
        reset
          ? page.items
          : [
              ...items,
              ...page.items.filter((item) => !items.some((saved) => saved.id === item.id)),
            ],
      );
      this.more.set(!!page.nextBefore && !!page.nextId);
    } catch {
      if (this.current(generation, owner))
        this.error.set('Your inbox could not load. Please try again.');
    } finally {
      if (this.current(generation, owner)) this.loading.set(false);
    }
  }
  async read(id: string): Promise<void> {
    if (this.reading() || !this.items().some((item) => item.id === id)) return;
    const generation = this.generation;
    const owner = this.owner;
    this.reading.set(id);
    this.error.set('');
    try {
      const value = await this.api.read(id);
      if (this.current(generation, owner))
        this.items.update((items) => items.map((item) => (item.id === value.id ? value : item)));
    } catch {
      if (this.current(generation, owner))
        this.error.set('The read status could not be saved. Please try again.');
    } finally {
      if (this.current(generation, owner)) this.reading.set(null);
    }
  }
  private current(generation: number, owner: string | undefined): boolean {
    return generation === this.generation && this.auth.user()?.id === owner;
  }
}
