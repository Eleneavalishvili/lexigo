import { newPasswordValidators, passwordGuidance } from '../../core/accounts/password-rules';
import { PasswordToggle } from '../../shared/password-toggle/password-toggle';
import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AccountApi } from '../../core/accounts/account-api';
import { ActiveSession } from '../../core/accounts/auth-models';
import { AuthStore } from '../../core/accounts/auth-store';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

@Component({
  selector: 'app-account',
  imports: [PasswordToggle, RouterLink, DatePipe, ReactiveFormsModule, BrandLogo],
  templateUrl: './account.html',
  styleUrl: './account.scss',
})
export class Account {
  protected readonly passwordGuidance = passwordGuidance;
  protected readonly auth = inject(AuthStore);
  private readonly api = inject(AccountApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly sessions = signal<readonly ActiveSession[]>([]);
  protected readonly loading = signal(true);
  protected readonly working = signal(false);
  protected readonly error = signal('');
  protected readonly showCurrentPassword = signal(false);
  protected readonly showNewPassword = signal(false);
  protected readonly passwordForm = inject(FormBuilder).nonNullable.group({
    currentPassword: ['', [Validators.required, Validators.maxLength(128)]],
    newPassword: ['', newPasswordValidators],
  });

  constructor() {
    effect(() => {
      if (!this.auth.user()) void this.router.navigate(['/login']);
    });
    void this.loadSessions();
  }

  private async loadSessions(): Promise<void> {
    this.loading.set(true);
    try {
      const sessions = await this.api.sessions();
      if (!this.destroyRef.destroyed) this.sessions.set(sessions);
    } catch {
      if (!this.destroyRef.destroyed)
        this.error.set('We couldn’t load your sessions. Please try again.');
    } finally {
      if (!this.destroyRef.destroyed) this.loading.set(false);
    }
  }

  protected async signOut(): Promise<void> {
    await this.perform(async () => {
      await this.auth.logout();
      await this.router.navigate(['/login']);
    });
  }

  protected async revoke(session: ActiveSession): Promise<void> {
    await this.perform(async () => {
      await this.api.revoke(session.id);
      if (session.isCurrent) {
        this.auth.credentialsChanged();
        await this.router.navigate(['/login']);
      } else await this.loadSessions();
    });
  }

  protected async signOutEverywhere(): Promise<void> {
    await this.perform(async () => {
      await this.api.revokeAll();
      this.auth.credentialsChanged();
      await this.router.navigate(['/login']);
    });
  }

  protected async changePassword(): Promise<void> {
    this.passwordForm.markAllAsTouched();
    if (this.passwordForm.invalid) return;
    await this.perform(async () => {
      const value = this.passwordForm.getRawValue();
      await this.api.changePassword(value.currentPassword, value.newPassword);
      this.passwordForm.reset();
      this.auth.credentialsChanged();
      await this.router.navigate(['/login']);
    });
  }

  private async perform(operation: () => Promise<void>): Promise<void> {
    if (this.working()) return;
    this.working.set(true);
    this.error.set('');
    try {
      await operation();
    } catch {
      this.error.set('That change could not be completed. Check your details and try again.');
    } finally {
      this.working.set(false);
    }
  }
}
