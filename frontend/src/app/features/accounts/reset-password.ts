import { newPasswordValidators, passwordGuidance } from '../../core/accounts/password-rules';
import { PasswordToggle } from '../../shared/password-toggle/password-toggle';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AccountApi } from '../../core/accounts/account-api';
import { AuthStore } from '../../core/accounts/auth-store';
import { AccountShell } from '../../shared/account-shell/account-shell';

@Component({
  selector: 'app-reset-password',
  imports: [PasswordToggle, ReactiveFormsModule, RouterLink, AccountShell],
  templateUrl: './reset-password.html',
  styleUrl: './account-form.scss',
})
export class ResetPassword {
  protected readonly passwordGuidance = passwordGuidance;
  private readonly api = inject(AccountApi);
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly parameters = inject(ActivatedRoute).snapshot.queryParamMap;
  private readonly userId = this.parameters.get('userId') ?? '';
  private readonly token = this.parameters.get('token') ?? '';
  protected readonly hasLink = this.userId.length > 0 && this.token.length > 0;
  protected readonly form = inject(FormBuilder).nonNullable.group({
    newPassword: ['', newPasswordValidators],
  });
  protected readonly working = signal(false);
  protected readonly done = signal(false);
  protected readonly showPassword = signal(false);
  protected readonly error = signal('');

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.working() || this.form.invalid || !this.hasLink) return;
    this.working.set(true);
    this.error.set('');
    try {
      await this.api.reset(this.userId, this.token, this.form.getRawValue().newPassword);
      this.auth.credentialsChanged();
      this.form.reset();
      this.done.set(true);
      await this.router.navigate([], { replaceUrl: true, queryParams: {} });
    } catch {
      this.error.set(
        'This reset request is invalid or has expired. Request a new link and try again.',
      );
    } finally {
      this.working.set(false);
    }
  }
}
