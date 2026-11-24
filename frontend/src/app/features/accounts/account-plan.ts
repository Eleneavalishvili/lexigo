import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { SubscriptionsApi, Subscription } from '../../core/billing/subscriptions-api';

@Component({
  selector: 'app-account-plan',
  imports: [DatePipe, RouterLink],
  template: `<section class="account-plan" aria-label="Your current plan">
    @if (plan(); as current) {
      <p class="eyebrow">YOUR CURRENT PLAN</p>
      <h2>{{ current.planCode === 'PREMIUM' ? 'Premium ✦' : 'Free' }}</h2>
      @if (current.planCode === 'PREMIUM') {
        <p>
          Premium access is saved to your account.
          @if (current.currentPeriodEnd) {
            Active through {{ current.currentPeriodEnd | date: 'mediumDate' }}.
          }
        </p>
        <a routerLink="/classes/extra-study">Find a teacher ↗</a>
      } @else {
        <a routerLink="/pricing">Explore Premium ↗</a>
      }
      <a routerLink="/billing/subscription">Manage your plan ↗</a>
    } @else if (error()) {
      <p role="alert">{{ error() }}</p>
      <button type="button" (click)="load()">Reload your plan</button>
    } @else {
      <p role="status">Opening your current plan…</p>
    }
  </section>`,
  styles: `
    .account-plan {
      margin-block: 1.5rem;
      padding: 1.25rem;
      border: 1px solid var(--line);
      border-radius: 0.75rem;
      background: var(--surface);
    }
    h2 {
      margin-block: 0.4rem;
    }
    a {
      display: inline-block;
      margin: 0.5rem 1rem 0.25rem 0;
    }
  `,
})
export class AccountPlan {
  private readonly api = inject(SubscriptionsApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private generation = 0;
  protected readonly plan = signal<Subscription | null>(null);
  protected readonly error = signal('');
  constructor() {
    effect(() => {
      const owner = this.auth.user()?.id;
      ++this.generation;
      this.plan.set(null);
      this.error.set('');
      if (owner) void this.load();
    });
  }
  protected async load(): Promise<void> {
    const generation = ++this.generation;
    const owner = this.auth.user()?.id;
    this.error.set('');
    try {
      const plan = await this.api.subscription();
      if (
        !this.destroy.destroyed &&
        generation === this.generation &&
        owner === this.auth.user()?.id
      )
        this.plan.set(plan);
    } catch {
      if (!this.destroy.destroyed && generation === this.generation)
        this.error.set('Your plan could not be loaded. Try again.');
    }
  }
}
