import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { EmailChangeApi } from '../../core/accounts/email-change-api';
import { AccountShell } from '../../shared/account-shell/account-shell';

@Component({
  selector: 'app-confirm-email-change',
  imports: [RouterLink, AccountShell],
  templateUrl: './confirm-email-change.html',
  styleUrl: './account-form.scss',
})
export class ConfirmEmailChange {
  private readonly api = inject(EmailChangeApi);
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly parameters = inject(ActivatedRoute).snapshot.queryParamMap;
  private readonly userId = this.parameters.get('userId') ?? '';
  private readonly requestId = this.parameters.get('requestId') ?? '';
  private readonly token = this.parameters.get('token') ?? '';
  protected readonly hasLink = !!(this.userId && this.requestId && this.token);
  protected readonly status = signal<'ready' | 'working' | 'success' | 'invalid'>('ready');
  protected readonly error = signal('');

  protected async confirm(): Promise<void> {
    if (!this.hasLink || this.status() !== 'ready') return;
    this.status.set('working');
    this.error.set('');
    try {
      await this.api.confirm(this.userId, this.requestId, this.token);
      if (this.auth.user()?.id === this.userId) this.auth.credentialsChanged();
      this.status.set('success');
      await this.router.navigate([], { replaceUrl: true, queryParams: {} });
    } catch (error) {
      if (
        error instanceof HttpErrorResponse &&
        (error.status === 0 || error.status >= 500 || error.status === 429)
      ) {
        this.status.set('ready');
        this.error.set(
          'The confirmation service is unavailable or busy. Please try again shortly.',
        );
      } else {
        this.status.set('invalid');
        await this.router.navigate([], { replaceUrl: true, queryParams: {} });
      }
    }
  }
}
