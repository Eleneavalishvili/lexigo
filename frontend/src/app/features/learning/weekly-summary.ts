import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { LearningApi } from '../../core/learning/learning-api';
import {
  WeeklyCourseSummary,
  WeeklySummary,
  WeeklySummaryApi,
} from '../../core/learning/weekly-summary-api';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { WeeklyComparisonPanel } from './weekly-comparison';

@Component({
  selector: 'app-weekly-summary',
  imports: [RouterLink, DatePipe, BrandLogo, WeeklyComparisonPanel],
  templateUrl: './weekly-summary.html',
  styleUrl: './weekly-summary.scss',
})
export class WeeklySummaryPage {
  private readonly api = inject(WeeklySummaryApi);
  private readonly learning = inject(LearningApi);
  private readonly auth = inject(AuthStore);
  private readonly owner = this.auth.user()?.id;
  private readonly lifetime = inject(DestroyRef);
  private readonly router = inject(Router);
  private generation = 0;
  protected readonly summary = signal<WeeklySummary | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly weeksAgo = signal(0);

  constructor() {
    effect(() => {
      if (this.auth.user()?.id === this.owner) return;
      ++this.generation;
      this.summary.set(null);
      this.error.set('');
      this.loading.set(false);
      this.busy.set(false);
    });
    void this.load(0);
  }

  protected async load(weeksAgo: number): Promise<void> {
    if (!this.current() || this.loading() || this.busy() || weeksAgo < 0 || weeksAgo > 12) return;
    const generation = ++this.generation;
    this.weeksAgo.set(weeksAgo);
    this.summary.set(null);
    this.loading.set(true);
    this.error.set('');
    try {
      const summary = await this.api.read(weeksAgo);
      if (this.current(generation)) this.summary.set(summary);
    } catch {
      if (this.current(generation))
        this.error.set(
          'Your week could not load. Try again; your learning records are still saved.',
        );
    } finally {
      if (this.current(generation)) this.loading.set(false);
    }
  }

  protected async practise(course: WeeklyCourseSummary): Promise<void> {
    if (!this.current() || this.busy() || this.loading()) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      const session = await this.learning.practiceMistakes(course.courseId);
      if (this.current(generation)) await this.router.navigate(['/learn/session', session.id]);
    } catch (error) {
      if (this.current(generation))
        this.error.set(
          error instanceof HttpErrorResponse && error.status === 409
            ? 'There are no eligible mistakes to practise in this course right now. Try your saved words or open the course.'
            : 'Practice could not open. Try again or return to your learning path.',
        );
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }

  private current(generation = this.generation): boolean {
    return (
      !this.lifetime.destroyed &&
      generation === this.generation &&
      !!this.owner &&
      this.auth.user()?.id === this.owner
    );
  }
}
