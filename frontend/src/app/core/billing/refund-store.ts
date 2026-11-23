import { HttpErrorResponse } from '@angular/common/http';
import { computed, DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { AuthStore } from '../accounts/auth-store';
import { BillingAdminApi, RefundIntent, RefundResult } from './billing-admin-api';

@Injectable()
export class RefundStore {
  private readonly api = inject(BillingAdminApi);
  private readonly auth = inject(AuthStore);
  private readonly owner = this.auth.user()?.id;
  private readonly destroy = inject(DestroyRef);
  private generation = 0;
  private readonly storageKey = `lexigo.refund.${this.owner}`;
  readonly pending = signal<RefundIntent | null>(this.restore());
  readonly phase = signal<'idle' | 'sending' | 'uncertain' | 'checking' | 'retryReady'>(
    this.pending() ? 'uncertain' : 'idle',
  );
  readonly busy = computed(() => this.phase() === 'sending' || this.phase() === 'checking');
  readonly result = signal<RefundResult | null>(null);
  readonly error = signal('');
  readonly notice = signal('');
  readonly persisted = signal(true);
  constructor() {
    effect(() => {
      if (this.auth.user()?.id === this.owner) return;
      ++this.generation;
      this.pending.set(null);
      this.result.set(null);
      this.phase.set('idle');
      this.error.set('');
      this.notice.set('');
    });
  }
  async submit(
    paymentId: string,
    amount: number,
    reason: string,
    password: string,
  ): Promise<boolean> {
    if (!this.current(this.generation) || this.busy()) return false;
    const pending = this.pending();
    if (pending && (this.phase() !== 'retryReady' || pending.paymentId !== paymentId)) return false;
    const intent = pending ?? { paymentId, key: crypto.randomUUID() };
    this.pending.set(intent);
    this.persist(intent);
    this.result.set(null);
    this.phase.set('sending');
    this.error.set('');
    this.notice.set('');
    const generation = this.generation;
    try {
      const result = await this.api.refund(intent, amount, reason, password);
      if (!this.current(generation)) return false;
      this.complete(result);
      return true;
    } catch (error) {
      if (this.current(generation)) {
        const knownRejection =
          !pending &&
          error instanceof HttpErrorResponse &&
          [400, 401, 403, 409].includes(error.status);
        if (knownRejection) {
          this.pending.set(null);
          this.phase.set('idle');
          try {
            sessionStorage.removeItem(this.storageKey);
          } catch {
            /* No refund was recorded for this new rejected request. */
          }
          this.error.set(
            typeof error.error?.title === 'string'
              ? error.error.title
              : 'The refund was rejected. Check your permission, password and payment details.',
          );
        } else {
          this.phase.set('uncertain');
          this.error.set(
            error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
              ? `${error.error.title} Check the saved result before trying again.`
              : 'The refund result is unconfirmed. Check the saved result before trying again.',
          );
        }
      }
      return false;
    }
  }
  async recover(): Promise<boolean> {
    const intent = this.pending();
    if (!intent || this.busy() || !this.current(this.generation)) return false;
    const generation = this.generation;
    this.phase.set('checking');
    this.error.set('');
    this.notice.set('');
    try {
      const result = await this.api.refundStatus(intent.key);
      if (!this.current(generation)) return false;
      this.complete(result);
      return true;
    } catch (error) {
      if (this.current(generation)) {
        if (error instanceof HttpErrorResponse && error.status === 404) {
          this.phase.set('retryReady');
          this.notice.set(
            'No saved refund was found. You may re-enter the details and password using the same request key; it cannot create a second refund.',
          );
        } else {
          this.phase.set('uncertain');
          this.error.set(
            'The saved result could not be checked. Keep this request pending and try checking again.',
          );
        }
      }
      return false;
    }
  }
  private complete(result: RefundResult): void {
    this.result.set(result);
    this.pending.set(null);
    this.phase.set('idle');
    this.error.set('');
    this.notice.set('The simulated refund is recorded. No real money was used.');
    try {
      sessionStorage.removeItem(this.storageKey);
    } catch {
      /* The recorded server result is authoritative. */
    }
  }
  private restore(): RefundIntent | null {
    if (!this.owner) return null;
    try {
      const value = JSON.parse(sessionStorage.getItem(this.storageKey) ?? 'null');
      const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
      return value && uuid.test(value.paymentId) && uuid.test(value.key)
        ? { paymentId: value.paymentId, key: value.key }
        : null;
    } catch {
      return null;
    }
  }
  private persist(intent: RefundIntent): void {
    // Only resource/request IDs survive reload; never passwords, reasons or card details.
    try {
      sessionStorage.setItem(this.storageKey, JSON.stringify(intent));
      this.persisted.set(true);
    } catch {
      this.persisted.set(false);
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
