import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { AccountShell } from '../../shared/account-shell/account-shell';

@Component({
  selector: 'app-teacher-login',
  imports: [AccountShell, ReactiveFormsModule, RouterLink],
  styleUrl: './account-form.scss',
  template: `<app-account-shell>
    <p class="eyebrow">THE TEACHER’S DESK</p>
    <h1>Hello, <em>teacher.</em></h1>
    <p>Use your private six-digit access code and the current code from your authenticator.</p>
    <form [formGroup]="form" (ngSubmit)="submit()" [attr.aria-busy]="working()">
      <div class="field">
        <label for="teacher-access">Teacher access code</label>
        <input
          id="teacher-access"
          type="password"
          inputmode="numeric"
          maxlength="6"
          autocomplete="off"
          formControlName="accessCode"
        />
      </div>
      <div class="field">
        <label for="teacher-authenticator">Authenticator code</label>
        <input
          id="teacher-authenticator"
          inputmode="numeric"
          maxlength="6"
          autocomplete="one-time-code"
          formControlName="authenticatorCode"
        />
      </div>
      @if (error()) {
        <p role="alert" class="form-error">{{ error() }}</p>
      }
      <button class="button button-red" type="submit" [disabled]="working()">
        {{ working() ? 'Opening your desk…' : 'Open my teacher desk' }}
      </button>
    </form>
    <p><a routerLink="/login">Learner or administrator sign-in ↗</a></p>
  </app-account-shell>`,
})
export class TeacherLogin {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  protected readonly form = new FormGroup({
    accessCode: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^[0-9]{6}$/)],
    }),
    authenticatorCode: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^[0-9]{6}$/)],
    }),
  });
  protected readonly working = signal(false);
  protected readonly error = signal('');
  protected async submit(): Promise<void> {
    if (this.working()) return;
    if (this.form.invalid) {
      this.error.set('Enter both six-digit codes.');
      return;
    }
    this.working.set(true);
    this.error.set('');
    try {
      const value = this.form.getRawValue();
      await this.auth.teacherLogin(value.accessCode, value.authenticatorCode);
      this.form.reset();
      await this.router.navigateByUrl('/classes');
    } catch (error) {
      this.error.set(
        error instanceof HttpErrorResponse && error.status === 429
          ? 'Too many attempts. Please wait before trying again.'
          : 'Unable to sign in. Check both codes and try again.',
      );
    } finally {
      this.working.set(false);
    }
  }
}
