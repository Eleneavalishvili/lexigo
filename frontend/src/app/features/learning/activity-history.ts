import { DatePipe } from '@angular/common';
import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { ActivityApi, ActivityPage } from '../../core/learning/activity-api';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

@Component({
  selector: 'app-activity-history',
  imports: [BrandLogo, DatePipe, ReactiveFormsModule, RouterLink],
  templateUrl: './activity-history.html',
  styleUrl: './activity-history.scss',
})
export class ActivityHistory {
  private readonly api = inject(ActivityApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private request?: Subscription;
  private applied = { from: '', to: '' };
  protected readonly days = signal<ActivityPage | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal('');
  protected readonly range = new FormGroup({
    from: new FormControl('', { nonNullable: true, validators: Validators.required }),
    to: new FormControl('', { nonNullable: true, validators: Validators.required }),
  });

  constructor() {
    const today = new Date();
    const from = new Date(today.getFullYear(), today.getMonth(), today.getDate() - 29);
    const date = (value: Date) =>
      `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
    this.range.setValue({ from: date(from), to: date(today) });
    effect(() => {
      const owner = this.auth.user()?.id;
      this.request?.unsubscribe();
      this.days.set(null);
      this.loading.set(false);
      this.error.set('');
      if (owner) this.apply();
    });
  }

  protected apply(): void {
    if (this.range.invalid) return;
    const range = this.range.getRawValue();
    if (range.from > range.to) {
      this.error.set('The end date must be on or after the start date.');
      return;
    }
    this.applied = range;
    this.load(1);
  }

  protected load(page: number): void {
    const owner = this.auth.user()?.id;
    if (!owner || page < 1) return;
    this.request?.unsubscribe();
    this.days.set(null);
    this.loading.set(true);
    this.error.set('');
    this.request = this.api
      .list(this.applied.from, this.applied.to, page)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (days) => {
          if (this.auth.user()?.id !== owner) return;
          this.days.set(days);
          this.loading.set(false);
        },
        error: (error) => {
          if (this.auth.user()?.id !== owner) return;
          this.loading.set(false);
          this.error.set(
            error.status === 400
              ? 'Choose a date range of up to 366 days.'
              : 'We couldn’t load your activity. Try again.',
          );
        },
      });
  }
}
