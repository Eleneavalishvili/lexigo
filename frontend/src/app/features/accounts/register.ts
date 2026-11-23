import { newPasswordValidators, passwordGuidance } from '../../core/accounts/password-rules';
import { PasswordToggle } from '../../shared/password-toggle/password-toggle';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { RegistrationApi } from '../../core/accounts/registration-api';
import { AccountShell } from '../../shared/account-shell/account-shell';

@Component({
  selector: 'app-register',
  imports: [PasswordToggle, ReactiveFormsModule, RouterLink, AccountShell],
  templateUrl: './register.html',
  styleUrl: './account-form.scss',
})
export class Register {
  protected readonly passwordGuidance = passwordGuidance;
  private readonly api = inject(RegistrationApi);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    displayName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(80)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    password: ['', newPasswordValidators],
  });
  protected readonly working = signal(false);
  protected readonly submitted = signal(false);
  protected readonly error = signal('');
  protected readonly showPassword = signal(false);

  protected submit(): void {
    if (this.working()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.error.set('');
    this.working.set(true);
    this.api
      .register(this.form.getRawValue())
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.working.set(false)),
      )
      .subscribe({
        next: () => {
          this.submitted.set(true);
          this.form.controls.password.reset();
        },
        error: (response: { status?: number }) =>
          this.error.set(
            response.status === 429
              ? 'A few too many attempts. Please wait a little before trying again.'
              : 'We could not complete that request. Check your details and try again.',
          ),
      });
  }
}
