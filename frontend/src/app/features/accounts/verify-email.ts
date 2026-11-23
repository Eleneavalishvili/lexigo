import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { RegistrationApi } from '../../core/accounts/registration-api';
import { HttpErrorResponse } from '@angular/common/http';
import { AccountShell } from '../../shared/account-shell/account-shell';

@Component({
  selector: 'app-verify-email',
  imports: [RouterLink, AccountShell],
  templateUrl: './verify-email.html',
  styleUrl: './account-form.scss',
})
export class VerifyEmail {
  private readonly api = inject(RegistrationApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly parameters = inject(ActivatedRoute).snapshot.queryParamMap;
  private readonly userId = this.parameters.get('userId') ?? '';
  private readonly token = this.parameters.get('token') ?? '';
  protected readonly status = signal<
    'ready' | 'working' | 'success' | 'already-confirmed' | 'missing' | 'error'
  >(this.userId && this.token ? 'ready' : 'missing');
  protected readonly retryMessage = signal('');

  protected confirm(): void {
    if (this.status() !== 'ready') return;
    this.retryMessage.set('');
    this.status.set('working');
    this.api
      .confirmEmail(this.userId, this.token)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.status.set('success');
          void this.router.navigate([], { replaceUrl: true, queryParams: {} });
        },
        error: (error: unknown) => {
          if (
            error instanceof HttpErrorResponse &&
            (error.status === 0 || error.status >= 500 || error.status === 429)
          ) {
            this.retryMessage.set(
              error.status === 429
                ? 'Too many attempts. Wait a little, then try again.'
                : 'We couldn’t reach the confirmation service. Please try again.',
            );
            this.status.set('ready');
            return;
          }
          this.status.set(
            error instanceof HttpErrorResponse &&
              error.status === 400 &&
              error.error?.code === 'email_already_confirmed'
              ? 'already-confirmed'
              : 'error',
          );
          void this.router.navigate([], { replaceUrl: true, queryParams: {} });
        },
      });
  }
}
