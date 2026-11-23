import { Component, DestroyRef, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  AuthenticatorReplacementApi,
  ReplacementSetup,
} from '../../core/accounts/authenticator-replacement-api';
import { AuthStore } from '../../core/accounts/auth-store';
import { TwoFactorApi } from '../../core/accounts/two-factor-api';
import { AccountShell } from '../../shared/account-shell/account-shell';
import { PasswordToggle } from '../../shared/password-toggle/password-toggle';

@Component({
  selector: 'app-replace-authenticator',
  imports: [ReactiveFormsModule, RouterLink, DatePipe, AccountShell, PasswordToggle],
  templateUrl: './replace-authenticator.html',
  styleUrls: ['./account-form.scss', './two-factor.scss'],
})
export class ReplaceAuthenticator {
  private readonly api = inject(AuthenticatorReplacementApi);
  private readonly auth = inject(AuthStore);
  private readonly security = inject(TwoFactorApi);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly setup = signal<ReplacementSetup | null>(null);
  protected readonly enabled = signal<boolean | null>(null);
  protected readonly loading = signal(true);
  protected readonly recoveryCodes = signal<readonly string[]>([]);
  protected readonly working = signal(false);
  protected readonly error = signal('');
  protected readonly showPassword = signal(false);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    currentPassword: ['', [Validators.required, Validators.maxLength(128)]],
    code: [''],
  });

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const status = await this.security.status();
      if (!this.destroyRef.destroyed) this.enabled.set(status.enabled);
    } catch {
      if (!this.destroyRef.destroyed)
        this.error.set('We couldn’t load your security settings. Please try again.');
    } finally {
      if (!this.destroyRef.destroyed) this.loading.set(false);
    }
  }

  protected async submit(): Promise<void> {
    if (this.working() || !this.enabled()) return;
    const setup = this.setup();
    this.form.controls.code.setValidators(
      setup ? [Validators.required, Validators.pattern(/^\d{6}$/)] : [],
    );
    this.form.controls.code.updateValueAndValidity();
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.working.set(true);
    this.error.set('');
    try {
      const value = this.form.getRawValue();
      if (setup) {
        const result = await this.api.confirm(setup.requestId, value.currentPassword, value.code);
        if (this.destroyRef.destroyed) return;
        this.recoveryCodes.set(result.recoveryCodes);
        this.setup.set(null);
        this.form.reset();
        this.auth.credentialsChanged();
      } else {
        const result = await this.api.begin(value.currentPassword);
        if (!this.destroyRef.destroyed) this.setup.set(result);
      }
    } catch {
      if (!this.destroyRef.destroyed)
        this.error.set(
          'We couldn’t complete that step. Check your password and new app code. Start within five minutes of a two-factor sign-in; finish setup within ten minutes.',
        );
    } finally {
      if (!this.destroyRef.destroyed) this.working.set(false);
    }
  }
}
