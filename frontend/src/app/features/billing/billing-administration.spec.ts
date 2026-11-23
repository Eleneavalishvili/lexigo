import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { BillingAdminApi } from '../../core/billing/billing-admin-api';
import { BillingAdministration } from './billing-administration';

describe('Billing desk safety', () => {
  it('opens read-only records and requires separate selection and confirmation before a refund', async () => {
    sessionStorage.clear();
    const api = {
      payments: vi.fn().mockResolvedValue({
        items: [
          {
            id: 'payment',
            userId: 'customer',
            customer: '<img src=x>',
            orderId: 'order',
            testCardId: 'card',
            cardStatus: 'Blocked',
            status: 'Succeeded',
            failureCode: null,
            amount: 9.99,
            currency: 'GEL',
            refundedAmount: 0,
            createdAt: '2026-10-30T00:00:00Z',
          },
        ],
        total: 1,
        page: 1,
        pageSize: 20,
      }),
      refund: vi.fn(),
    };
    TestBed.configureTestingModule({
      imports: [BillingAdministration],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user: signal({ id: 'actor', roles: ['SuperAdmin'] }) } },
        { provide: BillingAdminApi, useValue: api },
      ],
    });
    const fixture = TestBed.createComponent(BillingAdministration);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(api.refund).not.toHaveBeenCalled();
    expect(host.querySelector('img')).toBeNull();
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((item) => item.textContent?.includes('Review refund'))!
      .click();
    await fixture.whenStable();
    expect(api.refund).not.toHaveBeenCalled();
    expect(host.querySelector('#refund-password')).not.toBeNull();
    host
      .querySelector<HTMLInputElement>('#refund-password')!
      .closest('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(api.refund).not.toHaveBeenCalled();
    expect(host.textContent).toContain('confirmation');
  });
});
