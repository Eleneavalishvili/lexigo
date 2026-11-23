import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../accounts/auth-store';
import { BillingAdminApi } from './billing-admin-api';
import { PlanCatalogueStore } from './plan-catalogue-store';

describe('Private plan catalogue state', () => {
  const user = signal({ id: 'staff' });
  const plans = vi.fn();
  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'staff' });
    TestBed.configureTestingModule({
      providers: [
        PlanCatalogueStore,
        { provide: AuthStore, useValue: { user } },
        { provide: BillingAdminApi, useValue: { plans } },
      ],
    });
  });
  it('keeps the newest filtered page when an earlier request finishes later', async () => {
    let finish!: (value: unknown) => void;
    plans.mockReturnValueOnce(new Promise((resolve) => (finish = resolve)));
    const store = TestBed.inject(PlanCatalogueStore);
    const earlier = store.load(1, true);
    const filtered = { items: [{ id: 'retired' }], page: 2, pageSize: 20, total: 21 };
    plans.mockResolvedValueOnce(filtered);
    await store.load(2, false);
    finish({ items: [{ id: 'active' }], page: 1, pageSize: 20, total: 1 });
    await earlier;
    expect(store.result()).toEqual(filtered);
    expect(store.active()).toBe(false);
    expect(plans).toHaveBeenLastCalledWith(2, false);
  });
  it('discards private catalogue data and in-flight responses when the account changes', async () => {
    let finish!: (value: unknown) => void;
    plans.mockReturnValueOnce(new Promise((resolve) => (finish = resolve)));
    const store = TestBed.inject(PlanCatalogueStore);
    const pending = store.load();
    user.set({ id: 'another' });
    TestBed.tick();
    finish({ items: [{ id: 'private' }], page: 1, pageSize: 20, total: 1 });
    await pending;
    expect(store.result()).toBeNull();
    await store.load();
    expect(plans).toHaveBeenCalledOnce();
  });
});
