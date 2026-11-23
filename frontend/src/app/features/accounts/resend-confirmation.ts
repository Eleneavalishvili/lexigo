import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { RegistrationApi } from '../../core/accounts/registration-api';
import { AccountShell } from '../../shared/account-shell/account-shell';

@Component({
  selector: 'app-resend-confirmation',
  imports: [ReactiveFormsModule, RouterLink, AccountShell],
  templateUrl: './resend-confirmation.html',
  styleUrl: './account-form.scss',
})
export class ResendConfirmation {
  private readonly api = inject(RegistrationApi);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
  });
  protected readonly working = signal(false);
  protected readonly submitted = signal(false);
  protected readonly error = signal('');

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.working() || this.form.invalid) return;
    this.working.set(true);
    this.error.set('');
    this.api
      .resend(this.form.getRawValue().email)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.working.set(false)),
      )
      .subscribe({
        next: () => this.submitted.set(true),
        error: () => this.error.set('Please wait a little, then try again.'),
      });
  }
}
