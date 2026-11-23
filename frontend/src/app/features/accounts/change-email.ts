import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EmailChangeApi } from '../../core/accounts/email-change-api';
import { AccountShell } from '../../shared/account-shell/account-shell';
import { PasswordToggle } from '../../shared/password-toggle/password-toggle';

@Component({
  selector: 'app-change-email',
  imports: [ReactiveFormsModule, RouterLink, AccountShell, PasswordToggle],
  templateUrl: './change-email.html',
  styleUrl: './account-form.scss',
})
export class ChangeEmail {
  private readonly api = inject(EmailChangeApi);
  protected readonly working = signal(false);
  protected readonly done = signal(false);
  protected readonly error = signal('');
  protected readonly showPassword = signal(false);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    newEmail: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    currentPassword: ['', [Validators.required, Validators.maxLength(128)]],
    authenticatorCode: ['', Validators.maxLength(32)],
    recoveryCode: ['', Validators.maxLength(32)],
  });

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.working()) return;
    this.working.set(true);
    this.error.set('');
    try {
      const value = this.form.getRawValue();
      await this.api.request(
        value.newEmail,
        value.currentPassword,
        value.authenticatorCode,
        value.recoveryCode,
      );
      this.form.reset();
      this.done.set(true);
    } catch {
      this.error.set(
        'We couldn’t request that change. Check your password and security code, then try again.',
      );
    } finally {
      this.working.set(false);
    }
  }
}
