import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { TestCardsApi } from '../../core/billing/test-cards-api';
import { TestCards } from './test-cards';

describe('Test wallet screen', () => {
  it('labels declined amounts as requests and never offers a receipt or balance-after value', async () => {
    api.history.mockResolvedValue({
      items: [
        {
          id: 'declined',
          testCardId: 'card',
          type: 'Debit',
          amount: 9.99,
          balanceAfter: null,
          currency: 'GEL',
          reference: '<script>reference</script>',
          createdAt: '2026-10-29T00:00:00Z',
          status: 'Failed',
          paymentId: 'failed',
          orderId: 'quote',
          entryKind: 'PaymentAttempt',
        },
      ],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });
    const fixture = TestBed.createComponent(TestCards);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const history = host.querySelector('.transaction-list')!;
    expect(history.textContent).toContain('No funds moved');
    expect(history.textContent).toContain('Requested:');
    expect(history.textContent).toContain('Order: quote');
    expect(history.textContent).not.toContain('Balance after:');
    expect(history.querySelector('a')).toBeNull();
    expect(history.querySelector('script')).toBeNull();
  });
  it('rejects a reversed date range before sending another history request', async () => {
    const fixture = TestBed.createComponent(TestCards);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const from = host.querySelector<HTMLInputElement>('#history-from')!;
    from.value = '2026-10-30';
    from.dispatchEvent(new Event('input'));
    const to = host.querySelector<HTMLInputElement>('#history-to')!;
    to.value = '2026-10-29';
    to.dispatchEvent(new Event('input'));
    const previous = api.history.mock.calls.length;
    host
      .querySelector<HTMLFormElement>('.history-filters')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(host.textContent).toContain('end date must be on or after');
    expect(api.history.mock.calls.length).toBe(previous);
  });
  const user = signal({ id: 'owner' });
  const api = { cards: vi.fn(), history: vi.fn(), apply: vi.fn() };
  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'owner' });
    api.cards.mockResolvedValue([
      {
        id: 'card',
        nickname: '<img src=x>',
        displayLastFour: '1234',
        currency: 'GEL',
        balance: 10,
        status: 'Active',
        version: 1,
        createdAt: '2026-10-29T00:00:00Z',
        isDefault: true,
      },
    ]);
    api.history.mockResolvedValue({
      items: [
        {
          id: 'entry',
          testCardId: 'card',
          type: 'TopUp',
          amount: 10,
          balanceAfter: 10,
          currency: 'GEL',
          reference: 'TEST-fixture',
          createdAt: '2026-10-29T00:00:00Z',
          status: 'Succeeded',
        },
      ],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });
    api.apply.mockResolvedValue({});
    TestBed.configureTestingModule({
      imports: [TestCards],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user } },
        { provide: TestCardsApi, useValue: api },
      ],
    });
  });
  it('clearly labels simulated balances, escapes card names and shows currency-aware history', async () => {
    const fixture = TestBed.createComponent(TestCards);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('SIMULATION ONLY');
    expect(host.textContent).toContain('No real bank card');
    expect(host.textContent).toContain('<img src=x>');
    expect(host.querySelector('img')).toBeNull();
    expect(host.textContent).toContain('GEL');
    expect(host.textContent).toContain('TEST-fixture');
    expect(host.querySelector('input[autocomplete="cc-number"]')).toBeNull();
  });
  it('clears editable private names when the account changes', async () => {
    const fixture = TestBed.createComponent(TestCards);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const nickname = host.querySelector<HTMLInputElement>('#card-name')!;
    nickname.value = 'Private name';
    nickname.dispatchEvent(new Event('input'));
    user.set({ id: 'other' });
    await fixture.whenStable();
    expect(nickname.value).toBe('');
    expect(host.textContent).not.toContain('<img src=x>');
  });
  it('labels a heart debit with a minus sign rather than as a top-up', async () => {
    api.history.mockResolvedValue({
      items: [
        {
          id: 'debit',
          testCardId: 'card',
          type: 'Debit',
          amount: 5,
          balanceAfter: 5,
          currency: 'GEL',
          reference: 'HEART-fixture',
          createdAt: '2026-10-29T00:00:00Z',
          status: 'Succeeded',
        },
      ],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });
    const fixture = TestBed.createComponent(TestCards);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Heart refill');
    expect(host.textContent).toContain('−GEL5.00');
    expect(host.querySelector('.transaction-list')!.textContent).not.toContain('Test top-up');
  });
  it('does not submit an invalid top-up and sends only a card path, amount and request key when valid', async () => {
    const fixture = TestBed.createComponent(TestCards);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const select = host.querySelector<HTMLSelectElement>('#topup-card')!;
    select.value = 'card';
    select.dispatchEvent(new Event('change'));
    const amount = host.querySelector<HTMLInputElement>('#topup-amount')!;
    amount.value = '1.001';
    amount.dispatchEvent(new Event('input'));
    const form = amount.closest('form')!;
    form.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();
    expect(api.apply).not.toHaveBeenCalled();
    amount.value = '12.50';
    amount.dispatchEvent(new Event('input'));
    form.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();
    expect(api.apply).toHaveBeenCalledExactlyOnceWith({
      method: 'POST',
      path: '/api/v1/me/test-cards/card/top-ups',
      body: { amount: 12.5, idempotencyKey: expect.any(String) },
    });
  });
});
