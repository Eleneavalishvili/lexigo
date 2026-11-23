import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthStore } from '../../core/accounts/auth-store';
import { BillingAdminApi } from '../../core/billing/billing-admin-api';

interface PriceChange {
  amount: number;
  startsAt: string;
  reason: string;
  idempotencyKey: string;
}

@Component({
  selector: 'app-plan-price-schedule',
  imports: [ReactiveFormsModule],
  templateUrl: './plan-price-schedule.html',
  styleUrl: './plan-price-schedule.scss',
})
export class PlanPriceSchedule {
  readonly priceId = input.required<string>();
  readonly saved = output<void>();
  private readonly api = inject(BillingAdminApi);
  private readonly auth = inject(AuthStore);
  private readonly lifetime = inject(DestroyRef);
  private readonly builder = inject(FormBuilder).nonNullable;
  protected readonly form = this.builder.group({
    amount: [0, [Validators.required, Validators.min(0.01), Validators.max(10000)]],
    startsAt: ['', Validators.required],
    reason: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(500)]],
    currentPassword: ['', [Validators.required, Validators.maxLength(128)]],
  });
  protected readonly busy = signal(false);
  protected readonly pending = signal<PriceChange | null>(null);
  protected readonly message = signal('');

  protected async schedule(): Promise<void> {
    if (this.busy() || this.form.invalid) return;
    const owner = this.auth.user()?.id;
    if (!owner) return;
    const { amount, startsAt, reason, currentPassword } = this.form.getRawValue();
    const time = new Date(startsAt);
    if (Number.isNaN(time.getTime())) return;
    const change = this.pending() ?? {
      amount,
      startsAt: time.toISOString(),
      reason,
      idempotencyKey: crypto.randomUUID(),
    };
    this.pending.set(change);
    this.busy.set(true);
    this.message.set('');
    this.form.controls.currentPassword.reset();
    const current = () => !this.lifetime.destroyed && this.auth.user()?.id === owner;
    try {
      await this.api.schedulePrice(this.priceId(), { ...change, currentPassword });
      if (current()) this.saved.emit();
    } catch (error) {
      if (!current()) return;
      if (error instanceof HttpErrorResponse && [400, 403, 404, 409].includes(error.status)) {
        this.pending.set(null);
        this.message.set(
          error.error?.title || 'The price could not be scheduled. Reload the catalogue.',
        );
      } else {
        this.message.set('The result is unknown. Enter your password and retry the same change.');
      }
    } finally {
      if (current()) this.busy.set(false);
    }
  }
}
