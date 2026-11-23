import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../accounts/auth-store';
import { HeartCheckoutStore } from './heart-checkout-store';
import { HeartOffer, HeartPurchase, HeartPurchasesApi } from './heart-purchases-api';
import { TestCardsApi } from './test-cards-api';

describe('Simulated heart checkout state', () => {
  const cardId = '10000000-0000-0000-0000-000000000001';
  const user = signal<{ id: string } | null>({ id: 'owner' });
  const api = { offer: vi.fn(), purchase: vi.fn(), byRequest: vi.fn(), history: vi.fn() };
  const wallet = { cards: vi.fn() };
  const offer: HeartOffer = {
    offerCode: 'full-hearts-gel-v1',
    amount: 5,
    currency: 'GEL',
    maximumHearts: 5,
    hearts: { count: 2, maximum: 5, refillAt: null, serverNow: '2026-10-29T00:00:00Z' },
  };
  const result: HeartPurchase = {
    id: 'purchase',
    testCardId: cardId,
    offerCode: offer.offerCode,
    status: 'Succeeded',
    failureCode: null,
    amount: 5,
    chargedAmount: 5,
    currency: 'GEL',
    reference: 'HEART-fixture',
    heartsBefore: 2,
    heartsAfter: 5,
    createdAt: '2026-10-29T00:00:00Z',
  };
  beforeEach(() => {
    vi.resetAllMocks();
    sessionStorage.clear();
    user.set({ id: 'owner' });
    api.offer.mockResolvedValue(offer);
    api.purchase.mockResolvedValue(result);
    api.byRequest.mockResolvedValue(result);
    api.history.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    wallet.cards.mockResolvedValue([
      { id: cardId, status: 'Active', currency: 'GEL', balance: 10, isDefault: true },
    ]);
    TestBed.configureTestingModule({
      providers: [
        HeartCheckoutStore,
        { provide: AuthStore, useValue: { user } },
        { provide: HeartPurchasesApi, useValue: api },
        { provide: TestCardsApi, useValue: wallet },
      ],
    });
  });
  it('submits only the selected card, reviewed offer code and generated request key', async () => {
    const store = TestBed.inject(HeartCheckoutStore);
    await store.load();
    await store.pay(cardId);
    expect(api.purchase).toHaveBeenCalledExactlyOnceWith({
      testCardId: cardId,
      offerCode: offer.offerCode,
      idempotencyKey: expect.any(String),
    });
    expect(store.result()).toEqual(result);
    expect(store.pending()).toBeNull();
    expect(sessionStorage.length).toBe(0);
  });
  it('checks status before allowing a same-key retry after an unknown outcome', async () => {
    api.purchase.mockRejectedValueOnce(new HttpErrorResponse({ status: 0 }));
    const store = TestBed.inject(HeartCheckoutStore);
    await store.load();
    await store.pay(cardId);
    const request = api.purchase.mock.calls[0][0];
    await store.retry();
    await store.pay(cardId);
    expect(api.purchase).toHaveBeenCalledOnce();
    api.byRequest.mockRejectedValueOnce(new HttpErrorResponse({ status: 404 }));
    await store.recover();
    expect(api.byRequest).toHaveBeenCalledExactlyOnceWith(request.idempotencyKey);
    expect(store.canRetry()).toBe(true);
    await store.retry();
    expect(api.purchase.mock.calls[1][0]).toBe(request);
    expect(store.pending()).toBeNull();
  });
  it('recovers a completed purchase without another POST and blocks retry while status is uncertain', async () => {
    api.purchase.mockRejectedValueOnce(new HttpErrorResponse({ status: 500 }));
    const store = TestBed.inject(HeartCheckoutStore);
    await store.load();
    await store.pay(cardId);
    api.byRequest.mockRejectedValueOnce(new HttpErrorResponse({ status: 0 }));
    await store.recover();
    await store.retry();
    expect(api.purchase).toHaveBeenCalledOnce();
    expect(store.canRetry()).toBe(false);
    await store.recover();
    expect(store.result()).toEqual(result);
    expect(api.purchase).toHaveBeenCalledOnce();
  });
  it('restores a pending reference after reload and only performs a status lookup automatically', async () => {
    const saved = {
      testCardId: cardId,
      offerCode: offer.offerCode,
      idempotencyKey: '20000000-0000-0000-0000-000000000002',
    };
    sessionStorage.setItem('lexigo.heart-checkout.owner', JSON.stringify(saved));
    const store = TestBed.inject(HeartCheckoutStore);
    await store.load();
    expect(api.byRequest).toHaveBeenCalledExactlyOnceWith(saved.idempotencyKey);
    expect(api.purchase).not.toHaveBeenCalled();
    expect(store.result()).toEqual(result);
    expect(sessionStorage.length).toBe(0);
  });
  it('retains a known decline as an uncharged result and clears recovery state', async () => {
    api.purchase.mockResolvedValue({
      ...result,
      status: 'Failed',
      failureCode: 'InsufficientBalance',
      chargedAmount: 0,
      heartsAfter: 2,
    });
    const store = TestBed.inject(HeartCheckoutStore);
    await store.load();
    await store.pay(cardId);
    expect(store.result()?.chargedAmount).toBe(0);
    expect(store.result()?.failureCode).toBe('InsufficientBalance');
    expect(store.pending()).toBeNull();
    expect(store.canRetry()).toBe(false);
  });
  it('preserves a saved result and blocks a new purchase when its balance reload fails', async () => {
    const store = TestBed.inject(HeartCheckoutStore);
    await store.load();
    api.offer.mockRejectedValueOnce(new Error('offline'));
    await store.pay(cardId);
    expect(store.result()).toEqual(result);
    expect(store.offer()).toBeNull();
    await store.pay(cardId);
    expect(api.purchase).toHaveBeenCalledOnce();
    expect(store.error()).toContain('result is saved');
  });
  it('clears pending storage/private state and ignores a late response after account change', async () => {
    const store = TestBed.inject(HeartCheckoutStore);
    await store.load();
    TestBed.tick();
    let finish!: (value: HeartPurchase) => void;
    api.purchase.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    );
    const pending = store.pay(cardId);
    expect(sessionStorage.length).toBe(1);
    user.set({ id: 'other' });
    TestBed.tick();
    finish(result);
    await pending;
    expect(store.result()).toBeNull();
    expect(store.cards()).toEqual([]);
    expect(store.offer()).toBeNull();
    expect(store.pending()).toBeNull();
    expect(sessionStorage.length).toBe(0);
  });
  it('rejects full-heart purchases and malformed saved requests without sending a payment', async () => {
    sessionStorage.setItem(
      'lexigo.heart-checkout.owner',
      JSON.stringify({ testCardId: cardId, idempotencyKey: 'invalid', offerCode: offer.offerCode }),
    );
    api.offer.mockResolvedValue({ ...offer, hearts: { ...offer.hearts, count: 5 } });
    const store = TestBed.inject(HeartCheckoutStore);
    await store.load();
    await store.pay(cardId);
    expect(api.purchase).not.toHaveBeenCalled();
    expect(api.byRequest).not.toHaveBeenCalled();
    expect(store.error()).toContain('already full');
  });
});
