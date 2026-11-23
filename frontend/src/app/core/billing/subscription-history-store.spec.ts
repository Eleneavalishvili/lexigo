import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../accounts/auth-store';
import { SubscriptionHistoryApi } from './subscription-history-api';
import { SubscriptionHistoryStore } from './subscription-history-store';

describe('Owned subscription history state', () => {
  const user = signal({ id: 'owner' });
  const api = { list: vi.fn(), events: vi.fn() };
  const page = { items: [{ id: 'one' }, { id: 'two' }], page: 1, pageSize: 20, totalCount: 2 };
  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'owner' });
    api.list.mockResolvedValue(page);
    api.events.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    TestBed.configureTestingModule({
      providers: [
        SubscriptionHistoryStore,
        { provide: AuthStore, useValue: { user } },
        { provide: SubscriptionHistoryApi, useValue: api },
      ],
    });
  });
  it('discards updates for a previous selected subscription and never requests an unknown ID', async () => {
    const store = TestBed.inject(SubscriptionHistoryStore);
    await store.load();
    let finish!: (value: unknown) => void;
    api.events.mockReturnValueOnce(new Promise((resolve) => (finish = resolve)));
    const old = store.select('one');
    await store.select('two');
    finish({ items: [{ id: 'stale' }], page: 1, pageSize: 20, totalCount: 1 });
    await old;
    expect(store.selected()).toBe('two');
    expect(store.events()?.items).toEqual([]);
    const calls = api.events.mock.calls.length;
    await store.select('not-owned');
    expect(api.events.mock.calls.length).toBe(calls);
  });
  it('discards private page responses on an account switch and clears both panes', async () => {
    let finish!: (value: unknown) => void;
    api.list.mockReturnValueOnce(new Promise((resolve) => (finish = resolve)));
    const store = TestBed.inject(SubscriptionHistoryStore);
    const pending = store.load();
    user.set({ id: 'another' });
    finish(page);
    await pending;
    TestBed.tick();
    expect(store.subscriptions()).toBeNull();
    expect(store.events()).toBeNull();
    expect(api.events).not.toHaveBeenCalled();
  });
});
