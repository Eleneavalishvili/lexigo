import { DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { AuthStore } from '../accounts/auth-store';
import { Page } from '../administration/administration-api';
import { BillingAdminApi, ManagedPlan } from './billing-admin-api';

@Injectable()
export class PlanCatalogueStore {
  private readonly api = inject(BillingAdminApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private generation = 0;
  readonly result = signal<Page<ManagedPlan> | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly active = signal<boolean | undefined>(undefined);

  constructor() {
    effect(() => {
      if (this.auth.user()?.id === this.owner) return;
      ++this.generation;
      this.result.set(null);
      this.error.set('');
      this.loading.set(false);
    });
  }

  async load(page = 1, active = this.active()): Promise<void> {
    if (!this.current(this.generation)) return;
    const generation = ++this.generation;
    this.active.set(active);
    this.loading.set(true);
    this.result.set(null);
    this.error.set('');
    try {
      const result = await this.api.plans(page, active);
      if (this.current(generation)) this.result.set(result);
    } catch {
      if (this.current(generation))
        this.error.set('The catalogue could not load. Check your billing access and try again.');
    } finally {
      if (this.current(generation)) this.loading.set(false);
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
