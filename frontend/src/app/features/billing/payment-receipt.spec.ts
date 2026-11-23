import { signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { SubscriptionsApi } from '../../core/billing/subscriptions-api';
import { PaymentReceipt } from './payment-receipt';

describe('Private simulated payment receipt', () => {
  const user = signal({ id: 'owner' });
  const params = new BehaviorSubject(convertToParamMap({ paymentId: 'first' }));
  const api = { payment: vi.fn(), receipt: vi.fn(), order: vi.fn() };
  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'owner' });
    params.next(convertToParamMap({ paymentId: 'first' }));
    api.payment.mockResolvedValue({
      id: 'first',
      orderId: 'order',
      status: 'Succeeded',
      receiptNumber: 'TEST-first',
      amount: 9.99,
      currency: 'GEL',
      completedAt: '2026-10-29T00:00:00Z',
      createdAt: '2026-10-28T23:59:00Z',
    });
    api.receipt.mockResolvedValue(new Blob(['SIMULATED RECEIPT'], { type: 'text/plain' }));
    api.order.mockResolvedValue({ id: 'order', amount: 9.99, currency: 'GEL', period: 'Monthly' });
    TestBed.configureTestingModule({
      imports: [PaymentReceipt],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: params } },
        { provide: AuthStore, useValue: { user } },
        { provide: SubscriptionsApi, useValue: api },
      ],
    });
  });
  it('clears private receipt details and prevents download after account changes', async () => {
    const fixture = TestBed.createComponent(PaymentReceipt);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('TEST-first');
    user.set({ id: 'other' });
    await fixture.whenStable();
    expect(host.textContent).not.toContain('TEST-first');
    expect(host.querySelector('button')).toBeNull();
    expect(api.receipt).not.toHaveBeenCalled();
  });
  it('ignores an older route response after another receipt is selected', async () => {
    let finish!: (value: unknown) => void;
    api.payment.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    );
    const fixture = TestBed.createComponent(PaymentReceipt);
    fixture.detectChanges();
    api.payment.mockResolvedValue({
      id: 'second',
      status: 'Succeeded',
      receiptNumber: 'TEST-second',
      amount: 79.99,
      currency: 'GEL',
    });
    params.next(convertToParamMap({ paymentId: 'second' }));
    await vi.waitFor(() => expect(api.payment).toHaveBeenCalledTimes(2));
    finish({
      id: 'first',
      status: 'Succeeded',
      receiptNumber: 'TEST-old',
      amount: 9.99,
      currency: 'GEL',
    });
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('TEST-old');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('TEST-second');
  });

  function button(host: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(host.querySelectorAll<HTMLButtonElement>('button')).find((item) =>
      item.textContent?.includes(text),
    );
  }

  it('can retry a temporary payment read failure without making another payment', async () => {
    api.payment.mockRejectedValueOnce(new HttpErrorResponse({ status: 0 }));
    const fixture = TestBed.createComponent(PaymentReceipt);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Check your connection');
    expect(host.textContent).not.toContain('unavailable for your account');
    button(host, 'Try loading again')!.click();
    await fixture.whenStable();
    expect(api.payment).toHaveBeenCalledTimes(2);
    expect(api.order).toHaveBeenCalledExactlyOnceWith('order');
    expect(host.textContent).toContain('TEST-first');
    expect(host.textContent).toContain('Monthly');
    expect(host.querySelector('[role=alert]')).toBeNull();
  });

  it('does not offer retries or show private details for a missing or foreign payment', async () => {
    api.payment.mockRejectedValueOnce(new HttpErrorResponse({ status: 404 }));
    const fixture = TestBed.createComponent(PaymentReceipt);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('unavailable for your account');
    expect(button(host, 'Try loading again')).toBeUndefined();
    expect(host.textContent).not.toContain('TEST-first');
    expect(api.order).not.toHaveBeenCalled();
  });

  it('keeps the payment and download available when optional order details fail', async () => {
    api.order.mockRejectedValueOnce(new HttpErrorResponse({ status: 503 }));
    const fixture = TestBed.createComponent(PaymentReceipt);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('TEST-first');
    expect(button(host, 'Download test receipt')?.disabled).toBe(false);
    expect(host.textContent).toContain('saved order details could not load');
    button(host, 'Retry order details')!.click();
    await fixture.whenStable();
    expect(host.textContent).toContain('Monthly');
    expect(host.textContent).not.toContain('saved order details could not load');
    expect(api.order).toHaveBeenCalledTimes(2);
  });

  it('discards old order details after selecting a different payment', async () => {
    let finish!: (value: unknown) => void;
    api.order.mockReturnValueOnce(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    const fixture = TestBed.createComponent(PaymentReceipt);
    fixture.detectChanges();
    await vi.waitFor(() => expect(api.order).toHaveBeenCalledOnce());
    api.payment.mockResolvedValue({
      id: 'second',
      orderId: 'second-order',
      status: 'Succeeded',
      receiptNumber: 'TEST-second',
      amount: 79.99,
      currency: 'GEL',
    });
    api.order.mockResolvedValue({
      id: 'second-order',
      period: 'Annual',
      amount: 79.99,
      currency: 'GEL',
    });
    params.next(convertToParamMap({ paymentId: 'second' }));
    await fixture.whenStable();
    finish({ id: 'old-order', period: 'Monthly', amount: 9.99, currency: 'GEL' });
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Annual');
    expect(host.textContent).not.toContain('Monthly');
    expect(host.textContent).not.toContain('TEST-first');
  });

  it('ignores late order details and performs no new reads after account changes', async () => {
    let finish!: (value: unknown) => void;
    api.order.mockReturnValueOnce(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    const fixture = TestBed.createComponent(PaymentReceipt);
    fixture.detectChanges();
    await vi.waitFor(() => expect(api.order).toHaveBeenCalledOnce());
    user.set({ id: 'other' });
    params.next(convertToParamMap({ paymentId: 'different' }));
    finish({ id: 'old-order', period: 'Monthly', amount: 9.99, currency: 'GEL' });
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).not.toContain('Monthly');
    expect(host.textContent).not.toContain('TEST-first');
    expect(api.payment).toHaveBeenCalledOnce();
  });

  it('refreshes a pending result using reads and reveals the receipt only after success', async () => {
    api.payment.mockResolvedValueOnce({
      id: 'first',
      orderId: 'order',
      status: 'Pending',
      receiptNumber: null,
      amount: 9.99,
      currency: 'GEL',
    });
    const fixture = TestBed.createComponent(PaymentReceipt);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(button(host, 'Download test receipt')).toBeUndefined();
    button(host, 'Refresh payment status')!.click();
    await fixture.whenStable();
    expect(button(host, 'Download test receipt')).toBeDefined();
    expect(host.textContent).toContain('TEST-first');
    expect(api.payment).toHaveBeenCalledTimes(2);
    expect(api.receipt).not.toHaveBeenCalled();
  });
});
