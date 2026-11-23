import { Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AccountApi } from '../../core/accounts/account-api';
import { AuthStore } from '../../core/accounts/auth-store';
import { AccountShell } from '../../shared/account-shell/account-shell';
import { PasswordToggle } from '../../shared/password-toggle/password-toggle';
import { DeletionResponsibilities } from './deletion-responsibilities';

@Component({
  selector: 'app-delete-account',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    AccountShell,
    PasswordToggle,
    DeletionResponsibilities,
  ],
  templateUrl: './delete-account.html',
  styleUrl: './account-form.scss',
})
export class DeleteAccount {
  private readonly api = inject(AccountApi);
  private readonly auth = inject(AuthStore);
  protected readonly working = signal(false);
  protected readonly done = signal(false);
  protected readonly error = signal('');
  protected readonly showPassword = signal(false);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    currentPassword: ['', [Validators.required, Validators.maxLength(128)]],
    confirmation: ['', [Validators.required, Validators.pattern(/^DELETE$/)]],
    authenticatorCode: ['', Validators.maxLength(32)],
    recoveryCode: ['', Validators.maxLength(32)],
  });

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.working()) return;
    this.working.set(true);
    this.error.set('');
    const value = this.form.getRawValue();
    try {
      await this.api.deleteAccount(value);
      this.auth.credentialsChanged();
      this.form.reset();
      this.done.set(true);
    } catch (error) {
      this.error.set(
        error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
          ? error.error.title
          : 'We could not confirm deletion. Check your connection and try again.',
      );
    } finally {
      this.form.controls.currentPassword.reset();
      this.form.controls.authenticatorCode.reset();
      this.form.controls.recoveryCode.reset();
      this.working.set(false);
    }
  }
}
