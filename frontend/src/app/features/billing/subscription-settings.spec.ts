import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../../core/accounts/auth-store';
import { SubscriptionsApi } from '../../core/billing/subscriptions-api';
import { SubscriptionSettings } from './subscription-settings';

describe('Subscription management controls', () => {
  const user = signal({ id: 'owner' });
  const api = { manage: vi.fn() };
  const subscription = {
    id: 'subscription',
    planCode: 'PREMIUM',
    status: 'Active',
    period: 'Monthly',
    currentPeriodStart: '2026-10-29T00:00:00Z',
    currentPeriodEnd: '2026-11-29T00:00:00Z',
    cancelAtPeriodEnd: false,
    autoRenew: false,
    defaultTestCardId: null,
    scheduledPlanPriceId: null,
    renewalProcessingAvailable: false,
  };
  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'owner' });
    api.manage.mockResolvedValue(subscription);
    TestBed.configureTestingModule({
      imports: [SubscriptionSettings],
      providers: [
        { provide: AuthStore, useValue: { user } },
        { provide: SubscriptionsApi, useValue: api },
      ],
    });
  });
  async function open(value = subscription) {
    const fixture = TestBed.createComponent(SubscriptionSettings);
    fixture.componentRef.setInput('subscription', value);
    fixture.componentRef.setInput('cards', [
      { id: 'card', nickname: '<img src=x>', currency: 'GEL', status: 'Active' },
    ]);
    await fixture.whenStable();
    return fixture;
  }
  it('clearly labels saved preferences while processing is unavailable and sends no automatic changes', async () => {
    const fixture = await open();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Automatic renewal processing is not available yet');
    expect(host.textContent).toContain('<img src=x>');
    expect(host.querySelector('img')).toBeNull();
    expect(api.manage).not.toHaveBeenCalled();
  });
  it('cancels only the displayed subscription and requests an authoritative reload', async () => {
    const fixture = await open();
    const changed = vi.fn();
    fixture.componentInstance.changed.subscribe(changed);
    const host: HTMLElement = fixture.nativeElement;
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((item) => item.textContent?.includes('Cancel at period end'))!
      .click();
    await fixture.whenStable();
    expect(api.manage).toHaveBeenCalledExactlyOnceWith('cancel', {
      subscriptionId: 'subscription',
      idempotencyKey: expect.any(String),
    });
    expect(changed).toHaveBeenCalledOnce();
  });
  it('offers resume while showing that paid access lasts until the saved end date', async () => {
    const fixture = await open({ ...subscription, cancelAtPeriodEnd: true });
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Premium stays available until then');
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((item) => item.textContent?.includes('Resume subscription'))!
      .click();
    await fixture.whenStable();
    expect(api.manage.mock.calls[0][0]).toBe('resume');
  });
  it('requires fresh confirmation for older preferences and does not enable renewal by rendering', async () => {
    const fixture = TestBed.createComponent(SubscriptionSettings);
    fixture.componentRef.setInput('subscription', {
      ...subscription,
      autoRenew: true,
      renewalProcessingAvailable: true,
      renewalConsentRequired: true,
    });
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('cannot renew until you confirm');
    expect(fixture.nativeElement.textContent).toContain('authorizes a simulated debit');
    expect(api.manage).not.toHaveBeenCalled();
  });
  it('allows an expired Free account to stop the explicitly targeted pending renewal', async () => {
    const fixture = TestBed.createComponent(SubscriptionSettings);
    fixture.componentRef.setInput('subscription', {
      ...subscription,
      id: null,
      planCode: 'FREE',
      autoRenew: true,
      renewalTargetSubscriptionId: 'ended',
    });
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('form')).toBeNull();
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((value) => value.textContent?.includes('Turn off pending renewal'))!
      .click();
    await fixture.whenStable();
    expect(api.manage).toHaveBeenCalledExactlyOnceWith('renewal', {
      subscriptionId: 'ended',
      autoRenew: false,
      defaultTestCardId: null,
      idempotencyKey: expect.any(String),
    });
  });
});
