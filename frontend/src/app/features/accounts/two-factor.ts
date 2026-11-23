import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import {
  AuthenticatorSetup,
  TwoFactorApi,
  TwoFactorStatus,
} from '../../core/accounts/two-factor-api';
import { AccountShell } from '../../shared/account-shell/account-shell';
import { PasswordToggle } from '../../shared/password-toggle/password-toggle';

@Component({
  selector: 'app-two-factor',
  imports: [RouterLink, ReactiveFormsModule, AccountShell, PasswordToggle],
  templateUrl: './two-factor.html',
  styleUrls: ['./account-form.scss', './two-factor.scss'],
})
export class TwoFactor {
  private readonly api = inject(TwoFactorApi);
  protected readonly auth = inject(AuthStore);
  protected readonly status = signal<TwoFactorStatus | null>(null);
  protected readonly setup = signal<AuthenticatorSetup | null>(null);
  protected readonly recoveryCodes = signal<readonly string[]>([]);
  protected readonly complete = signal(false);
  protected readonly working = signal(false);
  protected readonly error = signal('');
  protected readonly showPassword = signal(false);
  protected readonly useRecovery = signal(false);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    currentPassword: ['', [Validators.required, Validators.maxLength(128)]],
    code: [''],
    recoveryCode: [''],
  });
  protected readonly privileged =
    this.auth.user()?.roles.some((role) => role !== 'Learner') ?? false;

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.error.set('');
    try {
      this.status.set(await this.api.status());
    } catch {
      this.error.set('We couldn’t load your security settings. Please try again.');
    }
  }

  protected async begin(): Promise<void> {
    if (!this.valid(false)) return;
    await this.perform(async () => {
      this.setup.set(await this.api.setup(this.form.getRawValue().currentPassword));
    });
  }

  protected async enable(): Promise<void> {
    if (!this.valid(true)) return;
    await this.perform(async () => {
      const value = this.form.getRawValue();
      this.finish((await this.api.enable(value.currentPassword, value.code)).recoveryCodes);
    });
  }

  protected async regenerate(): Promise<void> {
    if (!this.valid(true)) return;
    await this.perform(async () => {
      const value = this.form.getRawValue();
      this.finish((await this.api.regenerate(value.currentPassword, value.code)).recoveryCodes);
    });
  }

  protected async disable(): Promise<void> {
    if (!this.valid(!this.useRecovery(), this.useRecovery())) return;
    await this.perform(async () => {
      const value = this.form.getRawValue();
      await this.api.disable(
        value.currentPassword,
        this.useRecovery() ? undefined : value.code,
        this.useRecovery() ? value.recoveryCode : undefined,
      );
      this.finish([]);
    });
  }

  private finish(codes: readonly string[]): void {
    this.recoveryCodes.set(codes);
    this.setup.set(null);
    this.form.reset();
    this.complete.set(true);
    this.auth.credentialsChanged();
  }

  private valid(code: boolean, recovery = false): boolean {
    this.form.controls.code.setValidators(
      code ? [Validators.required, Validators.pattern(/^\d{6}$/)] : [],
    );
    this.form.controls.recoveryCode.setValidators(
      recovery ? [Validators.required, Validators.maxLength(32)] : [],
    );
    this.form.controls.code.updateValueAndValidity();
    this.form.controls.recoveryCode.updateValueAndValidity();
    this.form.markAllAsTouched();
    return this.form.valid;
  }

  private async perform(operation: () => Promise<void>): Promise<void> {
    if (this.working()) return;
    this.working.set(true);
    this.error.set('');
    try {
      await operation();
    } catch {
      this.error.set(
        'Unable to update security. Check your password and use a fresh code. Too many attempts can temporarily lock sign-in.',
      );
    } finally {
      this.working.set(false);
    }
  }
}
