import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../accounts/auth-store';
import { SubscriptionsApi } from './subscriptions-api';
import { SubscriptionSettingsStore } from './subscription-settings-store';

describe('Subscription preference recovery', () => {
  const user = signal({ id: 'owner' });
  const api = { manage: vi.fn() };
  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'owner' });
    api.manage.mockResolvedValue({ id: 'subscription', cancelAtPeriodEnd: true });
    TestBed.configureTestingModule({
      providers: [
        SubscriptionSettingsStore,
        { provide: AuthStore, useValue: { user } },
        { provide: SubscriptionsApi, useValue: api },
      ],
    });
  });
  it('retains the exact target/key and prevents another edit after an uncertain result', async () => {
    api.manage.mockRejectedValueOnce(new HttpErrorResponse({ status: 0 }));
    const store = TestBed.inject(SubscriptionSettingsStore);
    await store.change('cancel', { subscriptionId: 'subscription' });
    const request = api.manage.mock.calls[0][1];
    expect(request).toEqual({ subscriptionId: 'subscription', idempotencyKey: expect.any(String) });
    await store.change('resume', { subscriptionId: 'another' });
    expect(api.manage).toHaveBeenCalledOnce();
    await store.retry();
    expect(api.manage.mock.calls[1][1]).toBe(request);
    expect(store.pending()).toBeNull();
    expect(store.notice()).toContain('No funds');
  });
  it('clears a known rejection and never displays it as saved', async () => {
    api.manage.mockRejectedValue(
      new HttpErrorResponse({ status: 409, error: { title: 'Resume this subscription first.' } }),
    );
    const store = TestBed.inject(SubscriptionSettingsStore);
    await store.change('renewal', { subscriptionId: 'subscription', autoRenew: true });
    expect(store.pending()).toBeNull();
    expect(store.error()).toContain('Resume this subscription');
    expect(store.notice()).toBe('');
  });
  it('clears pending private preferences and ignores late responses on account change', async () => {
    const store = TestBed.inject(SubscriptionSettingsStore);
    TestBed.tick();
    let finish!: (value: unknown) => void;
    api.manage.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    );
    const pending = store.change('cancel', { subscriptionId: 'private-subscription' });
    user.set({ id: 'other' });
    TestBed.tick();
    finish({ id: 'private-subscription' });
    expect(await pending).toBeNull();
    expect(store.pending()).toBeNull();
    expect(store.notice()).toBe('');
    expect(store.busy()).toBe(false);
  });
});
