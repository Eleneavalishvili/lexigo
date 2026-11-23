import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AccountApi } from '../../core/accounts/account-api';
import { AccountShell } from '../../shared/account-shell/account-shell';

@Component({
  selector: 'app-forgot-password',
  imports: [ReactiveFormsModule, RouterLink, AccountShell],
  templateUrl: './forgot-password.html',
  styleUrl: './account-form.scss',
})
export class ForgotPassword {
  private readonly api = inject(AccountApi);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
  });
  protected readonly working = signal(false);
  protected readonly sent = signal(false);
  protected readonly error = signal('');

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.working() || this.form.invalid) return;
    this.working.set(true);
    this.error.set('');
    try {
      await this.api.forgot(this.form.getRawValue().email);
      this.sent.set(true);
    } catch {
      this.error.set('We couldn’t send that request. Wait a little and try again.');
    } finally {
      this.working.set(false);
    }
  }
}
