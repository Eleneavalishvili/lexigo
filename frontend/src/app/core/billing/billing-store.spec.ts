import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../accounts/auth-store';
import { BillingStore } from './billing-store';
import { SubscriptionsApi } from './subscriptions-api';
import { TestCardsApi } from './test-cards-api';

describe('Subscription billing state', () => {
  const priceId = '71000000-0000-0000-0000-000000000001';
  const cardId = '72000000-0000-0000-0000-000000000001';
  const orderId = '73000000-0000-0000-0000-000000000001';
  const user = signal<{ id: string } | null>({ id: 'owner' });
  const api = {
    plans: vi.fn(),
    subscription: vi.fn(),
    entitlements: vi.fn(),
    create: vi.fn(),
    order: vi.fn(),
    orderByRequest: vi.fn(),
    pay: vi.fn(),
    paymentByRequest: vi.fn(),
  };
  const wallet = { cards: vi.fn() };
  const order = {
    id: orderId,
    planPriceId: priceId,
    amount: 9.99,
    currency: 'GEL',
    period: 'Monthly',
    status: 'Created',
    createdAt: '2026-10-29T00:00:00Z',
    expiresAt: '2026-10-29T00:30:00Z',
    subscriptionId: null,
  };
  const paid = {
    id: 'payment',
    orderId,
    testCardId: cardId,
    status: 'Succeeded',
    failureCode: null,
    amount: 9.99,
    currency: 'GEL',
    createdAt: '2026-10-29T00:00:00Z',
    completedAt: '2026-10-29T00:00:00Z',
    subscriptionId: 'subscription',
    receiptNumber: 'TEST-fixture',
  };
  beforeEach(() => {
    vi.resetAllMocks();
    sessionStorage.clear();
    user.set({ id: 'owner' });
    api.plans.mockResolvedValue([
      {
        id: 'plan',
        code: 'PREMIUM',
        name: 'Premium',
        prices: [{ id: priceId, amount: 9.99, currency: 'GEL', period: 'Monthly' }],
        features: [],
      },
    ]);
    api.subscription.mockResolvedValue({
      id: null,
      planCode: 'FREE',
      status: 'Free',
      autoRenew: false,
    });
    api.entitlements.mockResolvedValue({ planCode: 'FREE', features: [] });
    wallet.cards.mockResolvedValue([
      { id: cardId, currency: 'GEL', status: 'Active', balance: 100, isDefault: true },
    ]);
    api.create.mockResolvedValue(order);
    api.orderByRequest.mockResolvedValue(order);
    api.pay.mockResolvedValue(paid);
    api.paymentByRequest.mockResolvedValue(paid);
    api.order.mockResolvedValue({ ...order, status: 'Paid', subscriptionId: 'subscription' });
    TestBed.configureTestingModule({
      providers: [
        BillingStore,
        { provide: AuthStore, useValue: { user } },
        { provide: SubscriptionsApi, useValue: api },
        { provide: TestCardsApi, useValue: wallet },
      ],
    });
  });
  it('quotes without granting Premium and sends only card/key/scenario on explicit payment', async () => {
    const store = TestBed.inject(BillingStore);
    await store.load();
    await store.review(priceId);
    expect(api.create).toHaveBeenCalledExactlyOnceWith(priceId, expect.any(String));
    expect(api.pay).not.toHaveBeenCalled();
    expect(store.subscription()?.planCode).toBe('FREE');
    api.subscription.mockResolvedValue({
      id: 'subscription',
      planCode: 'PREMIUM',
      status: 'Active',
    });
    api.entitlements.mockResolvedValue({ planCode: 'PREMIUM', features: [] });
    await store.pay(cardId, false);
    expect(api.pay).toHaveBeenCalledExactlyOnceWith(orderId, cardId, expect.any(String), false);
    expect(store.subscription()?.planCode).toBe('PREMIUM');
    expect(store.result()?.receiptNumber).toBe('TEST-fixture');
    expect(store.pending()).toBeNull();
  });
  it('requires status lookup and retains the same order key after an interrupted quote', async () => {
    api.create.mockRejectedValueOnce(new HttpErrorResponse({ status: 0 }));
    const store = TestBed.inject(BillingStore);
    await store.load();
    await store.review(priceId);
    const key = api.create.mock.calls[0][1];
    await store.retry();
    expect(api.create).toHaveBeenCalledOnce();
    api.orderByRequest.mockRejectedValueOnce(new HttpErrorResponse({ status: 404 }));
    await store.recover();
    await store.retry();
    expect(api.create).toHaveBeenLastCalledWith(priceId, key);
    expect(store.order()?.id).toBe(orderId);
  });
  it('recovers a saved successful payment without sending another charge', async () => {
    const store = TestBed.inject(BillingStore);
    await store.load();
    await store.review(priceId);
    api.pay.mockRejectedValueOnce(new HttpErrorResponse({ status: 500 }));
    await store.pay(cardId);
    const key = api.pay.mock.calls[0][2];
    await store.retry();
    expect(api.pay).toHaveBeenCalledOnce();
    await store.recover();
    expect(api.paymentByRequest).toHaveBeenCalledExactlyOnceWith(key);
    expect(api.pay).toHaveBeenCalledOnce();
    expect(store.result()?.status).toBe('Succeeded');
  });
  it('does not clear or retry a payment whose server status is still Pending', async () => {
    const store = TestBed.inject(BillingStore);
    await store.load();
    await store.review(priceId);
    api.pay.mockRejectedValueOnce(new HttpErrorResponse({ status: 0 }));
    await store.pay(cardId);
    api.paymentByRequest.mockResolvedValue({ ...paid, status: 'Pending', receiptNumber: null });
    await store.recover();
    await store.retry();
    expect(store.pending()?.stage).toBe('Payment');
    expect(store.canRetry()).toBe(false);
    expect(api.pay).toHaveBeenCalledOnce();
  });
  it('restores a payment request on reload and queries status without auto-paying', async () => {
    const key = '74000000-0000-0000-0000-000000000001';
    sessionStorage.setItem(
      'lexigo.subscription-checkout.owner',
      JSON.stringify({ stage: 'Payment', orderId, cardId, key, technical: false }),
    );
    const store = TestBed.inject(BillingStore);
    await store.load();
    expect(api.paymentByRequest).toHaveBeenCalledExactlyOnceWith(key);
    expect(api.pay).not.toHaveBeenCalled();
    expect(store.result()?.status).toBe('Succeeded');
  });
  it('does not allow another payment after a saved result whose reload failed', async () => {
    const store = TestBed.inject(BillingStore);
    await store.load();
    await store.review(priceId);
    api.subscription.mockRejectedValueOnce(new Error('offline'));
    await store.pay(cardId);
    expect(store.result()?.status).toBe('Succeeded');
    expect(store.ready()).toBe(false);
    await store.pay(cardId);
    expect(api.pay).toHaveBeenCalledOnce();
  });
  it('clears private order, payment and recovery references after account changes', async () => {
    const store = TestBed.inject(BillingStore);
    await store.load();
    await store.review(priceId);
    TestBed.tick();
    let finish!: (value: unknown) => void;
    api.pay.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    );
    const pending = store.pay(cardId);
    user.set({ id: 'other' });
    TestBed.tick();
    finish(paid);
    await pending;
    expect(store.result()).toBeNull();
    expect(store.order()).toBeNull();
    expect(store.cards()).toEqual([]);
    expect(store.subscription()).toBeNull();
    expect(sessionStorage.length).toBe(0);
  });
});
