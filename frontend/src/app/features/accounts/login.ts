import { PasswordToggle } from '../../shared/password-toggle/password-toggle';
import { Component, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { AccountShell } from '../../shared/account-shell/account-shell';

@Component({
  selector: 'app-login',
  imports: [PasswordToggle, ReactiveFormsModule, RouterLink, AccountShell],
  templateUrl: './login.html',
  styleUrl: './account-form.scss',
})
export class Login {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    password: ['', [Validators.required, Validators.maxLength(128)]],
    authenticatorCode: [''],
    recoveryCode: [''],
  });
  protected readonly working = signal(false);
  protected readonly needsCode = signal(false);
  protected readonly useRecovery = signal(false);
  protected readonly showPassword = signal(false);
  protected readonly error = signal(
    inject(ActivatedRoute).snapshot.queryParamMap.has('unavailable')
      ? 'We couldn’t reach the server to restore your session. Please try again.'
      : '',
  );

  protected async submit(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.working() || this.form.invalid) return;
    this.working.set(true);
    this.error.set('');
    try {
      const value = this.form.getRawValue();
      const response = await this.auth.login(
        value.email,
        value.password,
        this.useRecovery() ? undefined : value.authenticatorCode || undefined,
        this.useRecovery() ? value.recoveryCode || undefined : undefined,
      );
      if ('requiresTwoFactor' in response) {
        this.needsCode.set(true);
        this.setCodeMode(this.useRecovery());
      } else {
        this.form.controls.password.reset();
        const destination = this.route.snapshot.queryParamMap.get('returnUrl') ?? '';
        await this.router.navigateByUrl(
          /^\/(?:learn|classes|account|billing)(?:\/|$|\?)/.test(destination) &&
            !destination.includes('\\')
            ? destination
            : '/learn',
        );
      }
    } catch (error) {
      this.error.set(
        error instanceof HttpErrorResponse && error.status === 403
          ? 'This account needs additional security setup before it can sign in.'
          : error instanceof HttpErrorResponse && error.status === 429
            ? 'Too many attempts. Wait a little before trying again.'
            : 'Unable to sign in. Check your details and make sure your email is confirmed.',
      );
    } finally {
      this.working.set(false);
    }
  }

  protected setCodeMode(recovery: boolean): void {
    this.useRecovery.set(recovery);
    this.form.controls.authenticatorCode.setValidators(
      recovery ? [] : [Validators.required, Validators.pattern(/^\d{6}$/)],
    );
    this.form.controls.recoveryCode.setValidators(
      recovery ? [Validators.required, Validators.maxLength(32)] : [],
    );
    this.form.controls.authenticatorCode.updateValueAndValidity();
    this.form.controls.recoveryCode.updateValueAndValidity();
  }
}
