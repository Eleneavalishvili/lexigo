import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { HeartPurchasesApi } from '../../core/billing/heart-purchases-api';
import { TestCardsApi } from '../../core/billing/test-cards-api';
import { HeartCheckout } from './heart-checkout';

describe('Heart checkout screen', () => {
  const user = signal({ id: 'owner' });
  const api = { offer: vi.fn(), purchase: vi.fn(), byRequest: vi.fn(), history: vi.fn() };
  const wallet = { cards: vi.fn() };
  beforeEach(() => {
    vi.resetAllMocks();
    sessionStorage.clear();
    user.set({ id: 'owner' });
    api.offer.mockResolvedValue({
      offerCode: 'full-hearts-gel-v1',
      amount: 5,
      currency: 'GEL',
      maximumHearts: 5,
      hearts: { count: 2, maximum: 5, refillAt: null, serverNow: '2026-10-29T00:00:00Z' },
    });
    wallet.cards.mockResolvedValue([
      {
        id: 'card',
        nickname: '<img src=x>',
        balance: 10,
        status: 'Active',
        currency: 'GEL',
        isDefault: true,
      },
    ]);
    api.history.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    api.purchase.mockResolvedValue({
      id: 'purchase',
      status: 'Failed',
      failureCode: 'InsufficientBalance',
      amount: 5,
      chargedAmount: 0,
      currency: 'GEL',
      reference: 'HEART-test',
      createdAt: '2026-10-29T00:00:00Z',
    });
    TestBed.configureTestingModule({
      imports: [HeartCheckout],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: convertToParamMap({ lesson: 'not-a-safe-return-path' }) },
          },
        },
        { provide: AuthStore, useValue: { user } },
        { provide: HeartPurchasesApi, useValue: api },
        { provide: TestCardsApi, useValue: wallet },
      ],
    });
  });
  it('shows the approved simulated price, current hearts, escaped card name and safe learning return', async () => {
    const fixture = TestBed.createComponent(HeartCheckout);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).not.toContain('SIMULATED CHECKOUT ONLY');
    expect(host.textContent).toContain('GEL5.00');
    expect(host.textContent).toContain('2 / 5');
    expect(host.textContent).toContain('<img src=x>');
    expect(host.querySelector('img')).toBeNull();
    expect(host.querySelector('header>a:last-child')?.getAttribute('href')).toBe('/learn');
  });
  it('requires explicit confirmation and shows a declined charge as zero', async () => {
    const fixture = TestBed.createComponent(HeartCheckout);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(api.purchase).not.toHaveBeenCalled();
    const button = Array.from(host.querySelectorAll<HTMLButtonElement>('button')).find((item) =>
      item.textContent?.includes('Confirm purchase'),
    )!;
    button.click();
    await fixture.whenStable();
    expect(api.purchase).toHaveBeenCalledOnce();
    expect(host.textContent).toContain('NO FUNDS MOVED');
    expect(host.textContent).toContain('Add card balance');
    expect(host.textContent).toContain('GEL0.00');
  });
});
