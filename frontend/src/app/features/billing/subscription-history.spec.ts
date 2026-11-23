import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SubscriptionHistoryStore } from '../../core/billing/subscription-history-store';
import { SubscriptionHistory } from './subscription-history';

describe('Subscription paper trail', () => {
  it('shows expired chapters and original paid terms without presenting a renewal action', async () => {
    const store = {
      subscriptions: signal({
        items: [
          {
            id: 'old',
            planCode: 'PREMIUM',
            status: 'Expired',
            period: 'Monthly',
            currentPeriodStart: '2026-01-01T00:00:00Z',
            currentPeriodEnd: '2026-02-01T00:00:00Z',
            isCurrent: false,
            cancelAtPeriodEnd: false,
          },
        ],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      }),
      events: signal({
        items: [
          {
            id: 'event',
            type: 'Activated',
            occurredAt: '2026-01-01T00:00:00Z',
            bySystem: false,
            amount: 9.99,
            currency: 'GEL',
            period: 'Monthly',
            paidPeriodStart: '2026-01-01T00:00:00Z',
            paidPeriodEnd: '2026-02-01T00:00:00Z',
            paymentId: 'owned-payment',
            stopReason: null,
          },
        ],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      }),
      selected: signal('old'),
      loading: signal(false),
      loadingEvents: signal(false),
      error: signal(''),
      eventError: signal(''),
      load: vi.fn(),
      select: vi.fn(),
    };
    TestBed.configureTestingModule({
      imports: [SubscriptionHistory],
      providers: [provideRouter([])],
    });
    TestBed.overrideComponent(SubscriptionHistory, {
      set: { providers: [{ provide: SubscriptionHistoryStore, useValue: store }] },
    });
    const fixture = TestBed.createComponent(SubscriptionHistory);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('PAST CHAPTER');
    expect(host.textContent).toContain('Expired');
    expect(host.textContent).toContain('Paid period:');
    expect(host.querySelector('a[href="/billing/receipt/owned-payment"]')).not.toBeNull();
    expect(host.querySelector('form')).toBeNull();
    expect(host.textContent).not.toContain('Charge card');
  });
});
