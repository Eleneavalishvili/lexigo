import { HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { AuthStore } from '../accounts/auth-store';
import { SubscriptionsApi, Subscription } from './subscriptions-api';

export type SubscriptionSetting = 'cancel' | 'resume' | 'renewal' | 'scheduled-plan';
export interface PendingSubscriptionSetting {
  kind: SubscriptionSetting;
  body: Record<string, unknown>;
}

@Injectable()
export class SubscriptionSettingsStore {
  private readonly api = inject(SubscriptionsApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private generation = 0;
  readonly busy = signal(false);
  readonly pending = signal<PendingSubscriptionSetting | null>(null);
  readonly error = signal('');
  readonly notice = signal('');
  constructor() {
    effect(() => {
      if (this.auth.user()?.id !== this.owner) {
        ++this.generation;
        this.pending.set(null);
        this.error.set('');
        this.notice.set('');
        this.busy.set(false);
      }
    });
  }
  async change(
    kind: SubscriptionSetting,
    body: Record<string, unknown>,
  ): Promise<Subscription | null> {
    if (this.busy() || this.pending() || !this.current(this.generation)) return null;
    this.pending.set({ kind, body: { ...body, idempotencyKey: crypto.randomUUID() } });
    return this.retry();
  }
  async retry(): Promise<Subscription | null> {
    const request = this.pending();
    if (!request || this.busy() || !this.current(this.generation)) return null;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      const value = await this.api.manage(request.kind, request.body);
      if (!this.current(generation)) return null;
      this.pending.set(null);
      this.notice.set('Preferences saved. No funds were charged.');
      return value;
    } catch (error) {
      if (!this.current(generation)) return null;
      if (
        error instanceof HttpErrorResponse &&
        error.status >= 400 &&
        error.status < 500 &&
        error.status !== 429
      ) {
        this.pending.set(null);
        this.error.set(
          typeof error.error?.title === 'string'
            ? error.error.title
            : 'This change was rejected. Reload your subscription.',
        );
      } else
        this.error.set(
          'The result is uncertain. Retry the same saved change, or reload to check current settings.',
        );
      return null;
    } finally {
      if (this.current(generation)) this.busy.set(false);
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
