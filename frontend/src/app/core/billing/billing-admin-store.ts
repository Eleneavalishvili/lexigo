import { DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { AuthStore } from '../accounts/auth-store';
import { Page } from '../administration/administration-api';
import {
  AdminBillingFilters,
  BillingAdminApi,
  BillingAudit,
  ManagedPayment,
  ManagedSubscription,
} from './billing-admin-api';

export type BillingTab = 'payments' | 'subscriptions' | 'audit';
@Injectable()
export class BillingAdminStore {
  private readonly api = inject(BillingAdminApi);
  private readonly auth = inject(AuthStore);
  private readonly owner = this.auth.user()?.id;
  private readonly destroy = inject(DestroyRef);
  private generation = 0;
  private selectionRequest = 0;
  readonly tab = signal<BillingTab>('payments');
  readonly payments = signal<Page<ManagedPayment> | null>(null);
  readonly subscriptions = signal<Page<ManagedSubscription> | null>(null);
  readonly audit = signal<Page<BillingAudit> | null>(null);
  readonly selected = signal<ManagedPayment | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly filters = signal<AdminBillingFilters>({});
  constructor() {
    effect(() => {
      if (this.auth.user()?.id === this.owner) return;
      ++this.generation;
      ++this.selectionRequest;
      this.payments.set(null);
      this.subscriptions.set(null);
      this.audit.set(null);
      this.selected.set(null);
      this.error.set('');
      this.loading.set(false);
    });
  }
  async load(tab = this.tab(), page = 1, filters = this.filters()): Promise<void> {
    if (!this.current(this.generation)) return;
    const generation = ++this.generation;
    this.tab.set(tab);
    this.filters.set({ ...filters });
    this.loading.set(true);
    this.error.set('');
    this.payments.set(null);
    this.subscriptions.set(null);
    this.audit.set(null);
    try {
      if (tab === 'payments') {
        const rows = await this.api.payments(page, filters);
        if (this.current(generation)) this.payments.set(rows);
      }
      if (tab === 'subscriptions') {
        const rows = await this.api.subscriptions(page, filters);
        if (this.current(generation)) this.subscriptions.set(rows);
      }
      if (tab === 'audit') {
        const rows = await this.api.audit(page, filters);
        if (this.current(generation)) this.audit.set(rows);
      }
    } catch {
      if (this.current(generation))
        this.error.set(
          'Billing records could not load. Check your current billing permission and try again.',
        );
    } finally {
      if (this.current(generation)) this.loading.set(false);
    }
  }
  choose(payment: ManagedPayment | null): void {
    if (!this.current(this.generation)) return;
    ++this.selectionRequest;
    this.selected.set(payment);
  }
  async recoverSelection(id: string): Promise<void> {
    if (!this.current(this.generation)) return;
    const request = ++this.selectionRequest;
    const generation = this.generation;
    try {
      const page = await this.api.payments(1, { paymentId: id });
      if (this.current(generation) && request === this.selectionRequest)
        this.selected.set(page.items[0] ?? null);
    } catch {
      if (this.current(generation) && request === this.selectionRequest)
        this.error.set(
          'The original payment could not load. Check your permission before continuing.',
        );
    }
  }
  private current(generation: number): boolean {
    return (
      !this.destroy.destroyed &&
      generation === this.generation &&
      !!this.owner &&
      this.auth.user()?.id === this.owner
    );
  }
}
