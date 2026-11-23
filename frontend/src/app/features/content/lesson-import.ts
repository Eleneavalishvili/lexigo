import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { ContentApi, StaffCourse, StaffUnit } from '../../core/content/content-api';

interface ImportReview {
  id: string;
  status: 'Valid' | 'Invalid' | 'Committed';
  exerciseCount: number | null;
  errors: { row: number; message: string }[];
}

@Component({
  selector: 'app-lesson-import',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './lesson-import.html',
  styleUrl: './lesson-import.scss',
})
export class LessonImport {
  private readonly http = inject(HttpClient);
  private readonly content = inject(ContentApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private readonly endpoint = '/api/v1/manage/imports';
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly canView = computed(() => {
    const user = this.auth.user();
    return (
      !!user &&
      user.id === this.owner &&
      user.roles.some((role) => ['ContentAuthor', 'Admin', 'SuperAdmin'].includes(role))
    );
  });
  protected readonly courses = signal<readonly StaffCourse[]>([]);
  protected readonly units = signal<readonly StaffUnit[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly file = signal<File | null>(null);
  protected readonly review = signal<ImportReview | null>(null);
  protected readonly lessonId = signal<string | null>(null);
  protected readonly restored = signal(false);
  protected readonly savedTitle = signal('');
  protected readonly form = inject(FormBuilder).nonNullable.group({
    unitId: ['', Validators.required],
    orderNumber: [1, [Validators.required, Validators.min(1)]],
    title: ['', [Validators.required, Validators.maxLength(160)]],
    objective: ['', [Validators.required, Validators.maxLength(1000)]],
    explanation: ['', [Validators.required, Validators.maxLength(20000)]],
  });

  constructor() {
    void this.reload();
  }

  protected async reload(): Promise<void> {
    await this.run(async () => {
      const courses = await this.content.courses();
      if (this.current()) this.courses.set(courses.filter((course) => course.canAuthor));
      const job = this.route.snapshot.queryParamMap.get('job');
      if (!this.current() || !job) return;
      this.review.set(null);
      this.lessonId.set(null);
      this.savedTitle.set('');
      this.restored.set(true);
      const saved = await firstValueFrom(
        this.http.get<ImportReview & { lessonId: string | null; title: string }>(
          `${this.endpoint}/${encodeURIComponent(job)}`,
        ),
      );
      if (!this.current()) return;
      this.review.set(saved);
      this.lessonId.set(saved.lessonId);
      this.savedTitle.set(saved.title);
      this.restored.set(true);
    });
  }

  protected async chooseCourse(courseId: string): Promise<void> {
    this.units.set([]);
    this.form.controls.unitId.setValue('');
    if (!courseId) return;
    await this.run(async () => {
      const units = await this.content.units(courseId);
      if (this.current()) this.units.set(units);
    });
  }

  protected chooseUnit(unitId: string): void {
    const unit = this.units().find((item) => item.id === unitId);
    this.form.controls.orderNumber.setValue(unit?.nextLessonOrder ?? 1);
  }

  protected chooseFile(input: HTMLInputElement): void {
    const file = input.files?.item(0) ?? null;
    this.error.set('');
    this.file.set(null);
    if (file && file.size > 256_000) {
      this.error.set('Choose a CSV file smaller than 250 KiB.');
      return;
    }
    this.file.set(file);
  }

  protected async validate(): Promise<void> {
    const file = this.file();
    if (!file || this.form.invalid || this.review()) return;
    const fields = this.form.getRawValue();
    await this.run(async () => {
      const csv = await file.text();
      if (!this.current()) return;
      if (csv.length > 64000) {
        this.error.set('The file exceeds 64,000 characters. Split the exercises across drafts.');
        return;
      }
      const result = await firstValueFrom(
        this.http.post<ImportReview>(this.endpoint + '/validate', { ...fields, csv }),
      );
      if (this.current()) {
        this.review.set(result);
        this.savedTitle.set(fields.title);
        await this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { job: result.id },
          queryParamsHandling: 'merge',
          replaceUrl: true,
        });
      }
    });
  }

  protected async commit(): Promise<void> {
    const review = this.review();
    if (!review || review.status !== 'Valid' || this.lessonId()) return;
    await this.run(async () => {
      const result = await firstValueFrom(
        this.http.post<{ id: string }>(`${this.endpoint}/${review.id}/commit`, null),
      );
      if (this.current()) this.lessonId.set(result.id);
    });
  }

  protected edit(): void {
    if (this.busy()) return;
    this.error.set('');
    this.review.set(null);
    this.lessonId.set(null);
    this.restored.set(false);
    this.savedTitle.set('');
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { job: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  private async run(action: () => Promise<void>): Promise<void> {
    if (!this.current() || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await action();
    } catch (error) {
      if (this.current())
        this.error.set(
          error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
            ? error.error.title
            : 'The import request failed. You can retry it.',
        );
    } finally {
      if (this.current()) this.busy.set(false);
    }
  }

  private current(): boolean {
    return !this.destroy.destroyed && this.canView();
  }
}
