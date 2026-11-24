import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../accounts/auth-store';
import { CardOperation, TestCardsApi } from './test-cards-api';
import { TestCardsStore } from './test-cards-store';

describe('Test wallet state', () => {
  const user = signal<{ id: string } | null>({ id: 'owner' });
  const api = { cards: vi.fn(), history: vi.fn(), apply: vi.fn() };
  const operation: CardOperation = {
    method: 'POST',
    path: '/api/v1/me/test-cards/card/top-ups',
    body: { amount: 10 },
  };
  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'owner' });
    api.cards.mockResolvedValue([]);
    api.history.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    api.apply.mockResolvedValue({});
    TestBed.configureTestingModule({
      providers: [
        TestCardsStore,
        { provide: AuthStore, useValue: { user } },
        { provide: TestCardsApi, useValue: api },
      ],
    });
  });
  it('retains the exact top-up request after an uncertain result and blocks another mutation', async () => {
    const store = TestBed.inject(TestCardsStore);
    await store.load();
    api.apply.mockRejectedValueOnce(new HttpErrorResponse({ status: 0 }));
    await store.change(operation);
    const original = api.apply.mock.calls[0][0];
    expect(original.body.idempotencyKey).toEqual(expect.any(String));
    expect(store.pending()).toBe(original);
    await store.change({ ...operation, body: { amount: 30 } });
    expect(api.apply).toHaveBeenCalledOnce();
    await store.retry();
    expect(api.apply.mock.calls[1][0]).toBe(original);
    expect(store.pending()).toBeNull();
    expect(store.notice()).toContain('Your wallet is updated.');
  });
  it('shows a known rejection and does not retain a pending operation', async () => {
    api.apply.mockRejectedValue(
      new HttpErrorResponse({ status: 409, error: { title: 'Unblock this card first.' } }),
    );
    const store = TestBed.inject(TestCardsStore);
    await store.change(operation);
    expect(store.error()).toContain('Unblock this card');
    expect(store.pending()).toBeNull();
    expect(store.notice()).toBe('');
  });
  it('does not retry a successful mutation when only the following reload fails', async () => {
    const store = TestBed.inject(TestCardsStore);
    await store.load();
    api.cards.mockRejectedValueOnce(new Error('offline'));
    await store.change(operation);
    expect(store.pending()).toBeNull();
    expect(store.error()).toContain('change is saved');
    await store.retry();
    expect(api.apply).toHaveBeenCalledOnce();
  });
  it('clears pending/private state and ignores late responses after an account change', async () => {
    const store = TestBed.inject(TestCardsStore);
    await store.load();
    TestBed.tick();
    let finish!: (value: unknown) => void;
    api.apply.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    );
    const pending = store.change(operation);
    user.set({ id: 'other' });
    TestBed.tick();
    finish({});
    await pending;
    expect(store.cards()).toEqual([]);
    expect(store.history()).toBeNull();
    expect(store.pending()).toBeNull();
    expect(store.notice()).toBe('');
    expect(store.working()).toBe(false);
    await store.change(operation);
    expect(api.apply).toHaveBeenCalledOnce();
  });
  it('ignores an older history load when the card filter changes', async () => {
    let finish!: (value: unknown) => void;
    api.cards.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    );
    const store = TestBed.inject(TestCardsStore);
    const old = store.load('old');
    await store.load('new');
    finish([{ id: 'stale' }]);
    await old;
    expect(store.cards()).toEqual([]);
    expect(store.selectedCard()).toBe('new');
    expect(api.history).toHaveBeenLastCalledWith('new', 1, {});
  });
  it('retains applied result/type/date filters across pages and clears them on account change', async () => {
    const store = TestBed.inject(TestCardsStore);
    await store.load('card');
    const filters = { status: 'Failed', type: 'Debit', from: '2026-10-01T00:00:00Z' };
    await store.filter(filters);
    expect(api.history).toHaveBeenLastCalledWith('card', 1, filters);
    await store.load('card', 2);
    expect(api.history).toHaveBeenLastCalledWith('card', 2, filters);
    user.set({ id: 'another' });
    TestBed.tick();
    expect(store.filters()).toEqual({});
    expect(store.history()).toBeNull();
  });
});
