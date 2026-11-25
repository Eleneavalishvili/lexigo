import { formatDate } from '@angular/common';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { SubscriptionsApi } from '../../core/billing/subscriptions-api';
import { TestCardsApi } from '../../core/billing/test-cards-api';
import { SubscriptionCheckout } from './subscription-checkout';
import { Pricing } from './pricing';

describe('Plan comparison and subscription confirmation', () => {
  const api = {
    plans: vi.fn(),
    subscription: vi.fn(),
    entitlements: vi.fn(),
    create: vi.fn(),
    order: vi.fn(),
    pay: vi.fn(),
  };
  const priceId = '71000000-0000-0000-0000-000000000001';
  beforeEach(() => {
    vi.resetAllMocks();
    sessionStorage.clear();
    api.plans.mockResolvedValue([
      {
        id: 'free',
        code: 'FREE',
        name: 'Free',
        prices: [],
        features: [{ code: 'CoreListening', description: 'Core listening', available: true }],
      },
      {
        id: 'premium',
        code: 'PREMIUM',
        name: 'Premium',
        prices: [
          { id: priceId, amount: 9.99, currency: 'GEL', period: 'Monthly' },
          { id: 'annual', amount: 79.99, currency: 'GEL', period: 'Annual' },
        ],
        features: [
          { code: 'CharacterConversations', description: 'Character calls', available: false },
        ],
      },
    ]);
    api.subscription.mockResolvedValue({
      id: null,
      planCode: 'FREE',
      status: 'Free',
      autoRenew: false,
    });
    api.entitlements.mockResolvedValue({ planCode: 'FREE', features: [] });
    api.create.mockResolvedValue({
      id: 'order',
      planPriceId: priceId,
      amount: 9.99,
      currency: 'GEL',
      period: 'Monthly',
      status: 'Created',
      createdAt: '2026-10-29T00:00:00Z',
      expiresAt: '2026-10-29T00:30:00Z',
    });
    TestBed.configureTestingModule({
      imports: [Pricing, SubscriptionCheckout],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap({ price: priceId }) } },
        },
        { provide: AuthStore, useValue: { user: signal({ id: 'owner' }) } },
        { provide: SubscriptionsApi, useValue: api },
        {
          provide: TestCardsApi,
          useValue: {
            cards: async () => [
              {
                id: 'card',
                nickname: '<img src=x>',
                currency: 'GEL',
                status: 'Active',
                balance: 100,
                isDefault: true,
              },
            ],
          },
        },
      ],
    });
  });
  it('shows the PDF prices and clearly identifies planned features', async () => {
    const fixture = TestBed.createComponent(Pricing);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('GEL9.99');
    expect(host.textContent).toContain('GEL79.99');
    expect(host.textContent).toContain('Planned');
    expect(host.textContent).not.toContain('SIMULATED BILLING ONLY');
  });

  it.each([
    {
      cancelAtPeriodEnd: false,
      expected: formatDate('2026-12-12T12:00:00Z', 'medium', 'en-US'),
    },
    { cancelAtPeriodEnd: true, expected: 'None scheduled' },
  ])(
    'shows the next simulated renewal only when configured',
    async ({ cancelAtPeriodEnd, expected }) => {
      api.subscription.mockResolvedValue({
        id: 'subscription',
        planCode: 'PREMIUM',
        status: 'Active',
        period: 'Monthly',
        currentPeriodStart: '2026-11-12T12:00:00Z',
        currentPeriodEnd: '2026-12-12T12:00:00Z',
        autoRenew: true,
        cancelAtPeriodEnd,
        defaultTestCardId: 'card',
        renewalProcessingAvailable: true,
        renewalConsentRequired: false,
      });
      const fixture = TestBed.createComponent(SubscriptionCheckout);
      await fixture.whenStable();
      const host: HTMLElement = fixture.nativeElement;
      const date = Array.from(host.querySelectorAll('dt')).find(
        (item) => item.textContent?.trim() === 'Next renewal',
      );
      expect(date?.nextElementSibling?.textContent?.trim()).toBe(expected);
      expect(host.querySelector('a[href="/billing/history"]')).not.toBeNull();
    },
  );
  it('requires order review before payment and escapes the test-card name', async () => {
    const fixture = TestBed.createComponent(SubscriptionCheckout);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(api.pay).not.toHaveBeenCalled();
    expect(host.textContent).not.toContain('Confirm payment');
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((item) => item.textContent?.includes('Review order'))!
      .click();
    await fixture.whenStable();
    expect(api.create).toHaveBeenCalledExactlyOnceWith(priceId, expect.any(String));
    expect(api.pay).not.toHaveBeenCalled();
    expect(host.textContent).toContain('Confirm payment');
    expect(host.textContent).toContain('<img src=x>');
    expect(host.querySelector('img')).toBeNull();
    expect(host.textContent).toContain(
      'This is the checkout deadline, not your subscription end date.',
    );
    expect(host.textContent).not.toContain('Quote expires');
  });
  it('removes the checkout deadline after a successful purchase and shows the real period end', async () => {
    const fixture = TestBed.createComponent(SubscriptionCheckout);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((button) => button.textContent?.includes('Review order'))!
      .click();
    await fixture.whenStable();
    api.subscription.mockResolvedValue({
      id: 'subscription',
      planCode: 'PREMIUM',
      status: 'Active',
      period: 'Monthly',
      currentPeriodStart: '2026-11-25T09:00:00Z',
      currentPeriodEnd: '2026-12-25T09:00:00Z',
      autoRenew: false,
    });
    api.pay.mockResolvedValue({
      id: 'payment',
      orderId: 'order',
      status: 'Succeeded',
      amount: 9.99,
      currency: 'GEL',
      receiptNumber: 'LXG-reference',
      subscriptionId: 'subscription',
    });
    const card = host.querySelector<HTMLSelectElement>('#subscription-card')!;
    card.value = 'card';
    card.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((button) => button.textContent?.includes('Confirm payment'))!
      .click();
    await fixture.whenStable();
    expect(host.textContent).toContain('Purchase successful. Premium is active.');
    expect(host.querySelector('#quote-title')).toBeNull();
    const end = [...host.querySelectorAll('dt')].find((row) => row.textContent?.trim() === 'Ends');
    expect(end?.nextElementSibling?.textContent).toContain(
      formatDate('2026-12-25T09:00:00Z', 'medium', 'en-US'),
    );
    expect(host.textContent).toContain('Spam and Promotions');
  });
});
