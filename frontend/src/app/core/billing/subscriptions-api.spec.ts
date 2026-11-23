import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SubscriptionsApi } from './subscriptions-api';

describe('Subscription HTTP contracts', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('does not send client prices, plan flags or recovery stages to the server', async () => {
    const api = TestBed.inject(SubscriptionsApi);
    const http = TestBed.inject(HttpTestingController);
    const order = api.create('price', 'order-key');
    const quote = http.expectOne('/api/v1/me/billing-orders');
    expect(quote.request.body).toEqual({ planPriceId: 'price', idempotencyKey: 'order-key' });
    quote.flush({ id: 'order' });
    await order;
    const pending = api.pay('order', 'card', 'payment-key', true);
    const payment = http.expectOne('/api/v1/me/billing-orders/order/payments');
    expect(payment.request.body).toEqual({
      testCardId: 'card',
      idempotencyKey: 'payment-key',
      simulateTechnicalFailure: true,
    });
    payment.flush({ status: 'Failed' });
    await pending;
  });
});
