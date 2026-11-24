import { Component, effect, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TestCard } from '../../core/billing/test-cards-api';
import { calendarRange } from '../../core/billing/calendar-range';
import { TestCardsStore } from '../../core/billing/test-cards-store';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { AuthStore } from '../../core/accounts/auth-store';

@Component({
  selector: 'app-test-cards',
  imports: [RouterLink, ReactiveFormsModule, CurrencyPipe, DatePipe, BrandLogo],
  providers: [TestCardsStore],
  templateUrl: './test-cards.html',
  styleUrl: './test-cards.scss',
})
export class TestCards {
  protected readonly store = inject(TestCardsStore);
  private readonly builder = inject(FormBuilder).nonNullable;
  protected readonly editing = signal<string | null>(null);
  protected readonly filterError = signal('');
  protected readonly filterForm = this.builder.group({
    type: [''],
    status: [''],
    from: [''],
    to: [''],
  });
  protected readonly createForm = this.builder.group({
    nickname: ['', [Validators.required, Validators.maxLength(40)]],
    currency: ['GEL', Validators.required],
    cardNumber: ['', [Validators.pattern(/^[0-9 ]{4,23}$/)]],
  });
  protected readonly editForm = this.builder.group({
    nickname: ['', [Validators.required, Validators.maxLength(40)]],
    makeDefault: [false],
  });
  protected readonly topUpForm = this.builder.group({
    cardId: ['', Validators.required],
    amount: [
      10,
      [
        Validators.required,
        Validators.min(1),
        Validators.max(1000),
        Validators.pattern(/^\d+(\.\d{1,2})?$/),
      ],
    ],
  });
  constructor() {
    const auth = inject(AuthStore);
    const owner = auth.user()?.id;
    effect(() => {
      if (auth.user()?.id === owner) return;
      this.editing.set(null);
      this.filterForm.reset({ type: '', status: '', from: '', to: '' });
      this.filterError.set('');
      this.createForm.reset({ nickname: '', currency: 'GEL', cardNumber: '' });
      this.editForm.reset({ nickname: '', makeDefault: false });
      this.topUpForm.reset({ cardId: '', amount: 10 });
    });
    void this.store.load();
  }
  protected async applyFilters(): Promise<void> {
    const value = this.filterForm.getRawValue();
    this.filterError.set('');
    try {
      await this.store.filter({
        type: value.type || undefined,
        status: value.status || undefined,
        ...calendarRange(value.from, value.to),
      });
    } catch (error) {
      this.filterError.set(
        error instanceof Error ? error.message : 'Choose valid start and end dates.',
      );
    }
  }
  protected unavailable(): boolean {
    return this.store.loading() || this.store.working() || !!this.store.pending();
  }
  protected create(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }
    const { cardNumber, ...card } = this.createForm.getRawValue();
    const digits = cardNumber.replaceAll(' ', '');
    if (digits && digits.length < 4) return;
    this.createForm.controls.cardNumber.reset('');
    void this.store.change({
      method: 'POST',
      path: '/api/v1/me/test-cards',
      body: { ...card, ...(digits ? { displayLastFour: digits.slice(-4) } : {}) },
    });
  }
  protected select(card: TestCard): void {
    this.editing.set(card.id);
    this.editForm.setValue({ nickname: card.nickname, makeDefault: card.isDefault });
  }
  protected save(): void {
    const id = this.editing();
    if (!id || this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      return;
    }
    void this.store.change({
      method: 'PATCH',
      path: `/api/v1/me/test-cards/${id}`,
      body: this.editForm.getRawValue(),
    });
  }
  protected block(card: TestCard): void {
    void this.store.change({
      method: 'POST',
      path: `/api/v1/me/test-cards/${card.id}/${card.status === 'Active' ? 'block' : 'unblock'}`,
      body: {},
    });
  }
  protected filterHistory(event: Event): void {
    void this.store.load((event.target as HTMLSelectElement).value, 1);
  }
  protected topUp(): void {
    if (this.topUpForm.invalid) {
      this.topUpForm.markAllAsTouched();
      return;
    }
    const value = this.topUpForm.getRawValue();
    void this.store.change({
      method: 'POST',
      path: `/api/v1/me/test-cards/${value.cardId}/top-ups`,
      body: { amount: value.amount },
    });
  }
}
