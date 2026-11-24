import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { BillingOrder, Payment, SubscriptionsApi } from '../../core/billing/subscriptions-api';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

@Component({
  selector: 'app-payment-receipt',
  imports: [RouterLink, CurrencyPipe, DatePipe, BrandLogo],
  templateUrl: './payment-receipt.html',
  styleUrl: './subscription.scss',
})
export class PaymentReceipt {
  protected readonly payment = signal<Payment | null>(null);
  protected readonly order = signal<BillingOrder | null>(null);
  protected readonly loading = signal(false);
  protected readonly loadingOrder = signal(false);
  protected readonly orderError = signal('');
  protected readonly canRetry = signal(false);
  protected readonly error = signal('');
  protected readonly busy = signal(false);
  private readonly api = inject(SubscriptionsApi);
  private readonly auth = inject(AuthStore);
  private readonly owner = this.auth.user()?.id;
  private readonly destroy = inject(DestroyRef);
  private generation = 0;
  private paymentId = '';
  constructor() {
    effect(() => {
      if (this.auth.user()?.id !== this.owner) {
        ++this.generation;
        this.payment.set(null);
        this.order.set(null);
        this.loading.set(false);
        this.loadingOrder.set(false);
        this.orderError.set('');
        this.canRetry.set(false);
        this.paymentId = '';
        this.error.set('');
        this.busy.set(false);
      }
    });
    inject(ActivatedRoute)
      .paramMap.pipe(takeUntilDestroyed())
      .subscribe((params) => {
        this.paymentId = params.get('paymentId') ?? '';
        void this.load(this.paymentId);
      });
  }
  private current(generation = this.generation): boolean {
    return (
      !this.destroy.destroyed &&
      generation === this.generation &&
      this.auth.user()?.id === this.owner
    );
  }
  private async load(id: string): Promise<void> {
    if (!this.current() || !id) return;
    const generation = ++this.generation;
    this.payment.set(null);
    this.order.set(null);
    this.error.set('');
    this.orderError.set('');
    this.canRetry.set(false);
    this.loading.set(true);
    this.loadingOrder.set(false);
    this.busy.set(false);
    try {
      const value = await this.api.payment(id);
      if (!this.current(generation)) return;
      this.payment.set(value);
      this.loading.set(false);
      if (value.orderId) {
        this.loadingOrder.set(true);
        try {
          const order = await this.api.order(value.orderId);
          if (this.current(generation)) this.order.set(order);
        } catch {
          if (this.current(generation))
            this.orderError.set(
              'The saved order details could not load. Your payment record is still shown.',
            );
        } finally {
          if (this.current(generation)) this.loadingOrder.set(false);
        }
      }
    } catch (error) {
      if (!this.current(generation)) return;
      const unavailable =
        error instanceof HttpErrorResponse && [400, 401, 403, 404].includes(error.status);
      this.canRetry.set(!unavailable);
      this.error.set(
        unavailable
          ? 'This receipt is unavailable for your account.'
          : 'Your payment record could not load. Check your connection and try again.',
      );
    } finally {
      if (this.current(generation)) this.loading.set(false);
    }
  }
  protected reload(): void {
    if (this.loading() || this.loadingOrder() || this.busy() || !this.current()) return;
    void this.load(this.paymentId);
  }
  protected async download(): Promise<void> {
    const payment = this.payment();
    if (!payment?.receiptNumber || this.busy() || !this.current()) return;
    this.busy.set(true);
    const generation = this.generation;
    this.error.set('');
    try {
      const blob = await this.api.receipt(payment.id);
      if (!this.current(generation)) return;
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = payment.receiptNumber + '.txt';
      link.click();
      URL.revokeObjectURL(url);
    } catch {
      if (this.current(generation))
        this.error.set('The receipt could not be downloaded. Try again.');
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }
}
