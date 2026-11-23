import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../accounts/auth-store';
import { BillingAdminApi, RefundResult } from './billing-admin-api';
import { RefundStore } from './refund-store';

describe('Simulated refund recovery', () => {
  const user = signal({ id: 'actor' });
  const api = { refund: vi.fn(), refundStatus: vi.fn() };
  const paymentId = '11111111-1111-4111-8111-111111111111';
  const result: RefundResult = {
    id: 'refund',
    paymentId,
    amount: 4,
    currency: 'GEL',
    reason: 'Requested refund',
    status: 'Succeeded',
    completedAt: '2026-10-30T00:00:00Z',
    revokedPremium: false,
  };
  beforeEach(() => {
    vi.resetAllMocks();
    sessionStorage.clear();
    user.set({ id: 'actor' });
    TestBed.configureTestingModule({
      providers: [
        RefundStore,
        { provide: AuthStore, useValue: { user } },
        { provide: BillingAdminApi, useValue: api },
      ],
    });
  });
  it('requires a status GET before retrying uncertainty and keeps the same target/key', async () => {
    api.refund
      .mockRejectedValueOnce(new HttpErrorResponse({ status: 0 }))
      .mockResolvedValue(result);
    const store = TestBed.inject(RefundStore);
    await store.submit(paymentId, 4, 'Requested refund', 'Transient password 42!');
    const intent = api.refund.mock.calls[0][0];
    expect(store.phase()).toBe('uncertain');
    const saved = sessionStorage.getItem('lexigo.refund.actor')!;
    expect(JSON.parse(saved)).toEqual(intent);
    expect(saved).not.toContain('Transient');
    expect(saved).not.toContain('Requested refund');
    await store.submit(paymentId, 4, 'Requested refund', 'password');
    expect(api.refund).toHaveBeenCalledOnce();
    api.refundStatus.mockRejectedValueOnce(new HttpErrorResponse({ status: 404 }));
    await store.recover();
    expect(api.refundStatus).toHaveBeenCalledExactlyOnceWith(intent.key);
    expect(store.phase()).toBe('retryReady');
    await store.submit(paymentId, 4, 'Requested refund', 'Transient password 42!');
    expect(api.refund.mock.calls[1][0]).toBe(intent);
    expect(store.result()).toEqual(result);
    expect(store.pending()).toBeNull();
    expect(sessionStorage.getItem('lexigo.refund.actor')).toBeNull();
  });
  it('restores only request IDs and checks without automatically posting a refund', async () => {
    const intent = { paymentId, key: '22222222-2222-4222-8222-222222222222' };
    sessionStorage.setItem('lexigo.refund.actor', JSON.stringify(intent));
    api.refundStatus.mockResolvedValue(result);
    const store = TestBed.inject(RefundStore);
    expect(store.pending()).toEqual(intent);
    expect(api.refund).not.toHaveBeenCalled();
    await store.recover();
    expect(api.refund).not.toHaveBeenCalled();
    expect(store.result()).toEqual(result);
  });
  it('does not label an unavailable status check as absent or enable a retry', async () => {
    api.refund.mockRejectedValue(new HttpErrorResponse({ status: 500 }));
    api.refundStatus.mockRejectedValue(new HttpErrorResponse({ status: 503 }));
    const store = TestBed.inject(RefundStore);
    await store.submit(paymentId, 4, 'Requested refund', 'password');
    await store.recover();
    expect(store.phase()).toBe('uncertain');
    await store.submit(paymentId, 4, 'Requested refund', 'password');
    expect(api.refund).toHaveBeenCalledOnce();
  });
  it('clears a confirmed rejection of a fresh request without showing a refund result', async () => {
    api.refund.mockRejectedValue(
      new HttpErrorResponse({ status: 409, error: { title: 'Fresh MFA required.' } }),
    );
    const store = TestBed.inject(RefundStore);
    await store.submit(paymentId, 4, 'Requested refund', 'password');
    expect(store.pending()).toBeNull();
    expect(store.result()).toBeNull();
    expect(store.error()).toBe('Fresh MFA required.');
  });
  it('does not reveal a previous account refund response after a switch', async () => {
    let finish!: (value: RefundResult) => void;
    api.refund.mockReturnValueOnce(new Promise<RefundResult>((resolve) => (finish = resolve)));
    const store = TestBed.inject(RefundStore);
    const pending = store.submit(paymentId, 4, 'Requested refund', 'password');
    user.set({ id: 'another' });
    finish(result);
    await pending;
    TestBed.tick();
    expect(store.result()).toBeNull();
    expect(store.pending()).toBeNull();
    expect(store.notice()).toBe('');
  });
});
