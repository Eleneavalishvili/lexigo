import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { WeeklyComparison, WeeklySummaryApi } from '../../core/learning/weekly-summary-api';

@Component({
  selector: 'app-weekly-comparison',
  imports: [DatePipe, RouterLink],
  templateUrl: './weekly-comparison.html',
  styleUrl: './weekly-comparison.scss',
})
export class WeeklyComparisonPanel {
  readonly weeksAgo = input.required<number>();
  private readonly api = inject(WeeklySummaryApi);
  private readonly lifetime = inject(DestroyRef);
  private readonly auth = inject(AuthStore);
  protected readonly comparison = signal<WeeklyComparison | null>(null);
  protected readonly state = signal<'idle' | 'loading' | 'locked' | 'error'>('idle');

  protected async load(): Promise<void> {
    if (this.state() === 'loading') return;
    const owner = this.auth.user()?.id;
    const week = this.weeksAgo();
    const isCurrent = () =>
      !this.lifetime.destroyed && this.auth.user()?.id === owner && this.weeksAgo() === week;
    this.state.set('loading');
    try {
      const comparison = await this.api.compare(week);
      if (isCurrent()) {
        this.comparison.set(comparison);
        this.state.set('idle');
      }
    } catch (error) {
      if (isCurrent()) {
        this.state.set(
          error instanceof HttpErrorResponse && error.status === 403 ? 'locked' : 'error',
        );
      }
    }
  }
}
