import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from './auth-store';
import { AccountNotification, NotificationPage, NotificationsApi } from './notifications-api';
import { NotificationsStore } from './notifications-store';

describe('Account inbox state', () => {
  const user = signal<{ id: string } | null>({ id: 'owner' });
  const item: AccountNotification = {
    id: 'one',
    type: 'SubscriptionEnded',
    title: 'Your postcard',
    body: 'No funds charged.',
    createdAt: '2026-10-29T00:00:00Z',
    readAt: null,
  };
  const page: NotificationPage = { items: [item], nextBefore: null, nextId: null };
  const api = { page: vi.fn(), read: vi.fn() };
  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'owner' });
    api.page.mockResolvedValue(page);
    TestBed.configureTestingModule({
      providers: [
        NotificationsStore,
        { provide: AuthStore, useValue: { user } },
        { provide: NotificationsApi, useValue: api },
      ],
    });
  });
  async function open() {
    const store = TestBed.inject(NotificationsStore);
    TestBed.tick();
    await Promise.resolve();
    TestBed.tick();
    return store;
  }
  it('loads once, keeps unread state on a failure and lets the same read be retried', async () => {
    const store = await open();
    expect(api.page).toHaveBeenCalledOnce();
    expect(store.items()).toEqual([item]);
    api.read.mockRejectedValueOnce(new Error('offline'));
    await store.read(item.id);
    expect(store.items()[0].readAt).toBeNull();
    expect(store.error()).toContain('could not be saved');
    api.read.mockResolvedValue({ ...item, readAt: '2026-10-29T01:00:00Z' });
    await store.read(item.id);
    expect(store.items()[0].readAt).not.toBeNull();
    expect(store.error()).toBe('');
  });
  it('discards the old account response after an account switch', async () => {
    let resolve!: (value: NotificationPage) => void;
    api.page
      .mockReturnValueOnce(new Promise<NotificationPage>((done) => (resolve = done)))
      .mockResolvedValue({ items: [], nextBefore: null, nextId: null });
    const store = TestBed.inject(NotificationsStore);
    TestBed.tick();
    user.set({ id: 'other' });
    resolve(page);
    await Promise.resolve();
    TestBed.tick();
    await Promise.resolve();
    expect(store.items()).toEqual([]);
    expect(store.error()).toBe('');
  });
  it('continues a server cursor without duplicating visible rows', async () => {
    api.page.mockResolvedValueOnce({ ...page, nextBefore: item.createdAt, nextId: item.id });
    const store = await open();
    expect(store.more()).toBe(true);
    api.page.mockResolvedValue({
      items: [item, { ...item, id: 'two' }],
      nextBefore: null,
      nextId: null,
    });
    await store.load();
    expect(api.page.mock.calls[1]).toEqual([item.createdAt, item.id]);
    expect(store.items().map((value) => value.id)).toEqual(['one', 'two']);
    expect(store.more()).toBe(false);
  });
});
