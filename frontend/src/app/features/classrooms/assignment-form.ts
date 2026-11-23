import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, input, OnInit, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthStore } from '../../core/accounts/auth-store';
import { AssignmentApi, AssignmentDetail } from '../../core/classrooms/assignment-api';
import { ClassroomView } from '../../core/classrooms/classroom-api';
import { CatalogApi, CatalogLesson, CatalogUnit } from '../../core/content/catalog-api';

@Component({
  selector: 'app-assignment-form',
  imports: [ReactiveFormsModule],
  templateUrl: './assignment-form.html',
  styleUrl: './assignment-form.scss',
})
export class AssignmentForm implements OnInit {
  readonly classroom = input.required<ClassroomView>();
  readonly existing = input<AssignmentDetail | null>(null);
  readonly saved = output<string>();
  readonly cancelled = output<void>();
  private readonly api = inject(AssignmentApi);
  private readonly catalog = inject(CatalogApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private readonly requestKey = crypto.randomUUID();
  private readonly builder = inject(FormBuilder).nonNullable;
  protected readonly form = this.builder.group({
    title: ['', [Validators.required, Validators.maxLength(120)]],
    dueAt: ['', Validators.required],
    minimumScore: [80, [Validators.required, Validators.min(0), Validators.max(100)]],
  });
  protected readonly units = signal<readonly CatalogUnit[]>([]);
  protected readonly lessons = signal<readonly CatalogLesson[]>([]);
  protected readonly selected = signal<readonly Pick<CatalogLesson, 'id' | 'title'>[]>([]);
  protected readonly unit = signal('');
  protected readonly catalogLoading = signal(false);
  protected readonly catalogError = signal('');
  protected readonly saving = signal(false);
  protected readonly error = signal('');
  private initialLocalDeadline = '';

  ngOnInit(): void {
    const assignment = this.existing();
    if (assignment) {
      const deadline = new Date(assignment.dueAt);
      const local = new Date(deadline.getTime() - deadline.getTimezoneOffset() * 60_000);
      this.initialLocalDeadline = local.toISOString().slice(0, 16);
      this.form.reset({
        title: assignment.title,
        dueAt: this.initialLocalDeadline,
        minimumScore: assignment.minimumScore,
      });
      this.selected.set(assignment.lessons);
    }
    void this.loadUnits();
  }

  protected async loadUnits(): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || this.catalogLoading()) return;
    this.catalogLoading.set(true);
    this.catalogError.set('');
    try {
      const units = await this.catalog.units(this.classroom().courseId);
      if (this.current(owner)) this.units.set(units);
    } catch {
      if (this.current(owner)) this.catalogError.set('The course units could not be loaded.');
    } finally {
      if (this.current(owner)) this.catalogLoading.set(false);
    }
  }

  protected async loadLessons(unitId: string): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || this.catalogLoading()) return;
    this.unit.set(unitId);
    this.lessons.set([]);
    this.catalogError.set('');
    if (!unitId) return;
    this.catalogLoading.set(true);
    try {
      const lessons = await this.catalog.lessons(unitId);
      if (this.current(owner)) this.lessons.set(lessons);
    } catch {
      if (this.current(owner))
        this.catalogError.set('The lessons in this unit could not be loaded.');
    } finally {
      if (this.current(owner)) this.catalogLoading.set(false);
    }
  }

  protected contains(id: string): boolean {
    return this.selected().some((lesson) => lesson.id === id);
  }

  protected toggle(lesson: Pick<CatalogLesson, 'id' | 'title'>): void {
    if (this.saving()) return;
    if (this.contains(lesson.id)) {
      this.selected.update((items) => items.filter((item) => item.id !== lesson.id));
    } else if (this.selected().length < 20) {
      this.selected.update((items) => [...items, lesson]);
    }
  }

  protected async save(): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || this.saving()) return;
    this.form.markAllAsTouched();
    const input = this.form.getRawValue();
    const title = input.title.trim();
    const deadline = new Date(input.dueAt);
    if (
      this.form.invalid ||
      !title ||
      !Number.isInteger(input.minimumScore) ||
      !Number.isFinite(deadline.getTime()) ||
      this.selected().length === 0
    ) {
      this.error.set(
        'Enter a title, a valid deadline, a whole-number score, and at least one lesson.',
      );
      return;
    }

    this.error.set('');
    this.saving.set(true);
    try {
      const existing = this.existing();
      const details = {
        title,
        dueAt:
          existing && input.dueAt === this.initialLocalDeadline
            ? existing.dueAt
            : deadline.toISOString(),
        minimumScore: input.minimumScore,
        lessonIds: this.selected().map((lesson) => lesson.id),
      };
      const result = existing
        ? await this.api.edit(existing.id, { ...details, revision: existing.revision })
        : await this.api.create(this.classroom().id, { ...details, requestKey: this.requestKey });
      if (this.current(owner)) this.saved.emit(result.id);
    } catch (error) {
      if (this.current(owner)) {
        this.error.set(
          error instanceof HttpErrorResponse && error.status === 409
            ? (error.error?.title ??
                'The assignment could not be saved. Check the details and retry.')
            : this.existing()
              ? 'The edit could not be confirmed. Close this form and refresh the assignment.'
              : 'The result could not be confirmed. Retry with the same details to avoid creating a duplicate.',
        );
      }
    } finally {
      if (this.current(owner)) this.saving.set(false);
    }
  }

  private current(owner: string): boolean {
    return !this.destroy.destroyed && this.auth.user()?.id === owner;
  }
}
