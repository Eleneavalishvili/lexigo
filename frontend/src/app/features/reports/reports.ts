import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';

interface Report {
  id: string;
  resourceType: string;
  resourceId: string;
  versionId: string | null;
  reason: string;
  comment: string;
  status: 'New' | 'InReview' | 'Resolved' | 'Rejected';
  resolution: string | null;
  createdAt: string;
}

@Component({
  selector: 'app-reports',
  imports: [DatePipe, ReactiveFormsModule, RouterLink],
  templateUrl: './reports.html',
  styleUrl: './reports.scss',
})
export class Reports {
  private readonly route = inject(ActivatedRoute);
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  protected readonly ownsView = computed(() => !!this.owner && this.auth.user()?.id === this.owner);
  protected readonly staff = this.route.snapshot.data['staff'] === true;
  protected readonly exerciseId = this.route.snapshot.queryParamMap.get('exercise');
  protected readonly sessionId = this.route.snapshot.queryParamMap.get('session');
  protected readonly cardCode = this.route.snapshot.queryParamMap.get('card');
  protected readonly rankingCourse = this.route.snapshot.queryParamMap.get('rankingCourse');
  protected readonly reports = signal<Report[]>([]);
  protected readonly page = signal(1);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly selected = signal<Report | null>(null);
  protected readonly reason = new FormControl('Exercise', { nonNullable: true });
  protected readonly comment = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(1000)],
  });
  protected readonly decision = new FormControl('InReview', { nonNullable: true });
  protected readonly resolution = new FormControl('', { nonNullable: true });

  constructor() {
    void this.load(1);
  }

  protected async load(page: number): Promise<void> {
    if (this.busy()) return;
    await this.run(async () => {
      const rows = await firstValueFrom(
        this.http.get<Report[]>(this.staff ? '/api/v1/admin/reports' : '/api/v1/me/reports', {
          params: { page },
        }),
      );
      if (!this.current()) return;
      this.reports.set(rows);
      this.page.set(page);
      this.selected.set(null);
    });
  }

  protected async submit(): Promise<void> {
    if (
      (!this.exerciseId && !this.cardCode && !this.rankingCourse) ||
      this.comment.invalid ||
      !this.comment.value.trim()
    )
      return;
    await this.run(async () => {
      const comment = this.comment.value.trim();
      const request = this.rankingCourse
        ? this.http.post<Report>('/api/v1/reports/ranking-names', {
            courseId: this.rankingCourse,
            week: this.route.snapshot.queryParamMap.get('week'),
            displayName: this.route.snapshot.queryParamMap.get('name'),
            reportToken: this.route.snapshot.queryParamMap.get('reportToken'),
            comment,
          })
        : this.cardCode
          ? this.http.post<Report>('/api/v1/reports/public-names', {
              cardCode: this.cardCode,
              comment,
            })
          : this.http.post<Report>('/api/v1/reports', {
              sessionExerciseId: this.exerciseId,
              reason: this.reason.value,
              comment,
            });
      const report = await firstValueFrom(request);
      if (!this.current()) return;
      this.notice.set('Report received.');
      this.comment.reset();
      this.reports.update((rows) =>
        [report, ...rows.filter((item) => item.id !== report.id)].slice(0, 20),
      );
    });
  }

  protected select(report: Report): void {
    this.selected.set(report);
    this.decision.setValue(report.status === 'InReview' ? 'Resolved' : 'InReview');
    this.resolution.reset();
  }

  protected async review(): Promise<void> {
    const report = this.selected();
    const status = this.decision.value;
    const resolution = this.resolution.value.trim();
    if (!report || (status !== 'InReview' && !resolution)) return;
    await this.run(async () => {
      const updated = await firstValueFrom(
        this.http.patch<Report>(`/api/v1/admin/reports/${report.id}`, { status, resolution }),
      );
      if (!this.current()) return;
      this.reports.update((rows) => rows.map((item) => (item.id === updated.id ? updated : item)));
      this.selected.set(null);
      this.notice.set('Review saved.');
    });
  }

  private async run(action: () => Promise<void>): Promise<void> {
    if (!this.current() || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await action();
    } catch (error) {
      if (this.current()) {
        this.error.set(
          error instanceof HttpErrorResponse && error.status === 409
            ? (error.error?.title ?? 'The report could not be saved.')
            : 'The report could not be opened or saved. Try refreshing.',
        );
      }
    } finally {
      if (this.current()) this.busy.set(false);
    }
  }

  private current(): boolean {
    return !this.destroy.destroyed && this.ownsView();
  }
}
