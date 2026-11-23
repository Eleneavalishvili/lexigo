import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../accounts/auth-store';
import { BillingAdminApi } from './billing-admin-api';
import { BillingAdminStore } from './billing-admin-store';

describe('Private billing administration reads', () => {
  const user = signal({ id: 'actor' });
  const api = { payments: vi.fn(), subscriptions: vi.fn(), audit: vi.fn() };
  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'actor' });
    api.payments.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20 });
    api.audit.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20 });
    TestBed.configureTestingModule({
      providers: [
        BillingAdminStore,
        { provide: AuthStore, useValue: { user } },
        { provide: BillingAdminApi, useValue: api },
      ],
    });
  });
  it('discards a payment response from the earlier tab and retains current filter paging', async () => {
    let finish!: (value: unknown) => void;
    api.payments.mockReturnValueOnce(new Promise((resolve) => (finish = resolve)));
    const store = TestBed.inject(BillingAdminStore);
    const old = store.load();
    await store.load('audit', 2, { from: '2026-10-01T00:00:00Z' });
    finish({ items: [{ id: 'stale' }], total: 1, page: 1, pageSize: 20 });
    await old;
    expect(store.payments()).toBeNull();
    expect(store.tab()).toBe('audit');
    expect(api.audit).toHaveBeenLastCalledWith(2, { from: '2026-10-01T00:00:00Z' });
  });
  it('clears financial panes across accounts and cannot restore an old selection', async () => {
    const store = TestBed.inject(BillingAdminStore);
    await store.load();
    user.set({ id: 'another' });
    TestBed.tick();
    await store.recoverSelection('old');
    expect(api.payments).toHaveBeenCalledOnce();
    expect(store.selected()).toBeNull();
    expect(store.payments()).toBeNull();
  });
});
