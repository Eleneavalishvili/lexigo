import { Component, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthStore } from '../../core/accounts/auth-store';
import { BillingAdminApi, BillingPermission } from '../../core/billing/billing-admin-api';
import { PasswordToggle } from '../../shared/password-toggle/password-toggle';

@Component({
  selector: 'app-billing-permission-editor',
  imports: [ReactiveFormsModule, PasswordToggle],
  templateUrl: './billing-permission-editor.html',
  styleUrl: './billing-permission-editor.scss',
})
export class BillingPermissionEditor {
  readonly userId = input.required<string>();
  private readonly api = inject(BillingAdminApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private readonly builder = inject(FormBuilder).nonNullable;
  private generation = 0;
  private pending: {
    userId: string;
    enabled: boolean;
    reason: string;
    idempotencyKey: string;
  } | null = null;
  protected readonly view = signal<BillingPermission | null>(null);
  protected readonly working = signal(false);
  protected readonly retrying = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly visible = signal(false);
  protected readonly form = this.builder.group({
    enabled: [false],
    reason: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(500)]],
    currentPassword: ['', [Validators.required, Validators.maxLength(128)]],
  });
  constructor() {
    effect(() => {
      const id = this.userId();
      const actor = this.auth.user();
      const generation = ++this.generation;
      this.pending = null;
      this.retrying.set(false);
      this.view.set(null);
      this.error.set('');
      this.notice.set('');
      this.working.set(false);
      this.visible.set(false);
      this.form.reset({ enabled: false, reason: '', currentPassword: '' });
      if (actor && actor.id === this.owner && actor.roles.includes('SuperAdmin'))
        untracked(() => void this.load(id, generation));
    });
  }
  private async load(id: string, generation: number): Promise<void> {
    this.working.set(true);
    try {
      const view = await this.api.permission(id);
      if (this.current(id, generation)) {
        this.view.set(view);
        this.form.controls.enabled.setValue(view.granted);
      }
    } catch {
      if (this.current(id, generation))
        this.error.set('Billing delegation could not load. Check your Super Admin access.');
    } finally {
      if (this.current(id, generation)) this.working.set(false);
    }
  }
  protected async save(): Promise<void> {
    if (this.working() || !this.current(this.userId(), this.generation)) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    const command = this.pending ?? {
      userId: this.userId(),
      enabled: value.enabled,
      reason: value.reason,
      idempotencyKey: crypto.randomUUID(),
    };
    if (command.userId !== this.userId()) return;
    const generation = this.generation;
    this.pending = command;
    this.working.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      await this.api.setPermission(command.userId, {
        enabled: command.enabled,
        reason: command.reason,
        idempotencyKey: command.idempotencyKey,
        currentPassword: value.currentPassword,
      });
      if (!this.current(command.userId, generation)) return;
      this.pending = null;
      this.retrying.set(false);
      this.notice.set('Delegation saved. Changed access ends the recipient’s existing sessions.');
      await this.load(command.userId, generation);
    } catch (error) {
      if (this.current(command.userId, generation)) {
        this.retrying.set(true);
        this.error.set(
          error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
            ? error.error.title
            : 'The change is unconfirmed. Re-enter your password to retry the same saved change.',
        );
      }
    } finally {
      if (this.current(command.userId, generation)) {
        this.form.controls.currentPassword.reset('');
        this.visible.set(false);
        this.working.set(false);
      }
    }
  }
  private current(id: string, generation: number): boolean {
    return (
      !this.destroy.destroyed &&
      generation === this.generation &&
      id === this.userId() &&
      this.auth.user()?.id === this.owner &&
      !!this.auth.user()?.roles.includes('SuperAdmin')
    );
  }
}
