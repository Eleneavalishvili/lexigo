import { Component, effect, inject, signal, untracked } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { BillingAdminStore, BillingTab } from '../../core/billing/billing-admin-store';
import { ManagedPayment } from '../../core/billing/billing-admin-api';
import { RefundStore } from '../../core/billing/refund-store';
import { calendarRange } from '../../core/billing/calendar-range';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { PasswordToggle } from '../../shared/password-toggle/password-toggle';

@Component({
  selector: 'app-billing-administration',
  imports: [RouterLink, ReactiveFormsModule, CurrencyPipe, DatePipe, BrandLogo, PasswordToggle],
  providers: [BillingAdminStore, RefundStore],
  templateUrl: './billing-administration.html',
  styleUrls: ['./subscription.scss', './billing-administration.scss'],
})
export class BillingAdministration {
  protected readonly auth = inject(AuthStore);
  protected readonly store = inject(BillingAdminStore);
  protected readonly refunds = inject(RefundStore);
  private readonly builder = inject(FormBuilder).nonNullable;
  protected readonly showPassword = signal(false);
  protected readonly formError = signal('');
  protected readonly filters = this.builder.group({
    userId: [''],
    status: [''],
    from: [''],
    to: [''],
  });
  protected readonly refundForm = this.builder.group({
    amount: [0, [Validators.required, Validators.min(0.01)]],
    reason: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(500)]],
    currentPassword: ['', [Validators.required, Validators.maxLength(128)]],
    confirm: [false, Validators.requiredTrue],
  });
  constructor() {
    const owner = this.auth.user()?.id;
    effect(() => {
      if (this.auth.user()?.id === owner) return;
      this.refundForm.reset();
      this.filters.reset();
      this.showPassword.set(false);
      this.formError.set('');
    });
    effect(() => {
      const pending = this.refunds.pending();
      if (pending && this.store.selected()?.id !== pending.paymentId)
        untracked(() => void this.store.recoverSelection(pending.paymentId));
    });
    effect(() => {
      const selected = this.store.selected();
      untracked(() => {
        const maximum = selected ? this.remaining(selected) : 0;
        this.refundForm.controls.amount.setValidators([
          Validators.required,
          Validators.min(0.01),
          Validators.max(maximum),
        ]);
        this.refundForm.reset({ amount: maximum, reason: '', currentPassword: '', confirm: false });
        this.showPassword.set(false);
      });
    });
    void this.store.load();
    if (this.refunds.pending()) void this.recover();
  }
  protected remaining(payment: ManagedPayment): number {
    return Number((payment.amount - payment.refundedAmount).toFixed(2));
  }
  protected select(payment: ManagedPayment): void {
    if (!this.refunds.pending() && !this.refunds.busy()) this.store.choose(payment);
  }
  protected switchTab(tab: BillingTab): void {
    this.filters.reset();
    this.formError.set('');
    void this.store.load(tab, 1, {});
  }
  protected async filter(): Promise<void> {
    const value = this.filters.getRawValue();
    this.formError.set('');
    if (
      value.userId &&
      !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value.userId)
    ) {
      this.formError.set('Use a valid customer UUID.');
      return;
    }
    try {
      await this.store.load(this.store.tab(), 1, {
        userId: value.userId || undefined,
        status: value.status || undefined,
        ...calendarRange(value.from, value.to),
      });
    } catch (error) {
      this.formError.set(error instanceof Error ? error.message : 'Check the filters.');
    }
  }
  protected async submitRefund(): Promise<void> {
    const payment = this.store.selected();
    if (!payment || this.refundForm.invalid) {
      this.refundForm.markAllAsTouched();
      return;
    }
    const value = this.refundForm.getRawValue();
    const saved = await this.refunds.submit(
      payment.id,
      value.amount,
      value.reason,
      value.currentPassword,
    );
    this.refundForm.controls.currentPassword.reset('');
    this.showPassword.set(false);
    if (saved) {
      this.store.choose(null);
      await this.store.load();
    }
  }
  protected async recover(): Promise<void> {
    if (await this.refunds.recover()) {
      this.store.choose(null);
      await this.store.load();
    }
  }
}
