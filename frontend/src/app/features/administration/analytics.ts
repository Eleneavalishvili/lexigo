import { DecimalPipe } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { ContentApi, StaffCourse } from '../../core/content/content-api';
import { AnalyticsChart } from './analytics-chart';

interface FormatCounts {
  format: string;
  answers: number;
  errors: number;
}

interface AnalyticsReport {
  activeLearners: number;
  courses: {
    courseId: string;
    course: string;
    learners: number;
    started: number;
    finished: number;
  }[];
  difficulty: FormatCounts[];
  commonErrors: (FormatCounts & { exerciseId: string })[];
}

@Component({
  selector: 'app-learning-analytics',
  imports: [AnalyticsChart, DecimalPipe, ReactiveFormsModule, RouterLink],
  templateUrl: './analytics.html',
  styleUrl: './analytics.scss',
})
export class Analytics {
  private readonly http = inject(HttpClient);
  private readonly content = inject(ContentApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private readonly endpoint = '/api/v1/admin/analytics';
  private readonly today = new Date();
  protected readonly ownsView = computed(() => !!this.owner && this.auth.user()?.id === this.owner);
  protected readonly filters = inject(FormBuilder).nonNullable.group({
    from: [
      new Date(this.today.getTime() - 29 * 86_400_000).toISOString().slice(0, 10),
      Validators.required,
    ],
    through: [this.today.toISOString().slice(0, 10), Validators.required],
    courseId: [''],
  });
  protected readonly courses = signal<readonly StaffCourse[]>([]);
  protected readonly report = signal<AnalyticsReport | null>(null);
  protected readonly courseChart = computed(() => {
    const rows = this.report()?.courses ?? [];
    return {
      labels: rows.map((item) => item.course),
      series: [
        { label: 'Sessions started', values: rows.map((item) => item.started) },
        { label: 'Lessons finished', values: rows.map((item) => item.finished) },
      ],
    };
  });
  protected readonly difficultyChart = computed(() => {
    const rows = this.report()?.difficulty ?? [];
    return {
      labels: rows.map((item) => item.format),
      series: [
        {
          label: 'Error rate',
          values: rows.map((item) => Math.round((item.errors / item.answers) * 1000) / 10),
        },
      ],
    };
  });
  protected readonly error = signal('');
  protected readonly courseError = signal(false);
  protected readonly busy = signal(false);
  private applied: HttpParams | null = null;

  constructor() {
    void this.loadCourses();
    void this.load();
  }

  protected async load(): Promise<void> {
    const { from, through, courseId } = this.filters.getRawValue();
    const days = (Date.parse(through) - Date.parse(from)) / 86_400_000;
    if (this.filters.invalid || !Number.isFinite(days) || days < 0 || days >= 90) {
      this.error.set('Choose a date range of 1 to 90 days.');
      return;
    }
    let params = new HttpParams().set('from', from).set('through', through);
    if (courseId) params = params.set('courseId', courseId);
    await this.run(async () => {
      this.report.set(null);
      this.applied = null;
      const report = await firstValueFrom(
        this.http.get<AnalyticsReport>(this.endpoint, { params }),
      );
      if (!this.current()) return;
      this.applied = params;
      this.report.set(report);
    });
  }

  protected async exportCsv(): Promise<void> {
    const params = this.applied?.set('csv', true);
    if (!params) return;
    await this.run(async () => {
      const blob = await firstValueFrom(
        this.http.get(this.endpoint, { params, responseType: 'blob' }),
      );
      if (!this.current()) return;
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'lexigo-analytics.csv';
      link.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    });
  }

  private async loadCourses(): Promise<void> {
    try {
      const courses = await this.content.courses();
      if (this.current()) this.courses.set(courses);
    } catch {
      if (this.current()) this.courseError.set(true);
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    if (!this.current() || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await action();
    } catch {
      if (this.current())
        this.error.set('Analytics could not be loaded. Check your access and try again.');
    } finally {
      if (this.current()) this.busy.set(false);
    }
  }

  private current(): boolean {
    return this.ownsView() && !this.destroy.destroyed;
  }
}
