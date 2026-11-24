import { Component, DestroyRef, inject, output, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ContentApi, CourseLanguage } from '../../core/content/content-api';

@Component({
  selector: 'app-course-creator',
  imports: [ReactiveFormsModule],
  templateUrl: './course-creator.html',
  styleUrl: './workbench-fields.scss',
})
export class CourseCreator {
  private readonly api = inject(ContentApi);
  private readonly destroy = inject(DestroyRef);
  readonly created = output<void>();
  protected readonly languages = signal<readonly CourseLanguage[]>([]);
  protected readonly opened = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly form = inject(FormBuilder).nonNullable.group({
    sourceLanguageId: [1, [Validators.required, Validators.min(1)]],
    targetLanguageId: [0, [Validators.required, Validators.min(1)]],
    title: ['', [Validators.required, Validators.maxLength(120)]],
    description: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  protected async open(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const languages = await this.api.courseLanguages();
      if (this.destroy.destroyed) return;
      this.languages.set(languages);
      this.opened.set(true);
    } catch {
      if (!this.destroy.destroyed) this.error.set('Could not load languages. Try again.');
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    const course = this.form.getRawValue();
    if (this.busy() || this.form.invalid) return;
    if (course.sourceLanguageId === course.targetLanguageId) {
      this.error.set('Choose two different languages.');
      return;
    }
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.createCourse({
        ...course,
        title: course.title.trim(),
        description: course.description.trim(),
      });
      if (this.destroy.destroyed) return;
      this.form.reset();
      this.opened.set(false);
      this.created.emit();
    } catch (error) {
      if (!this.destroy.destroyed)
        this.error.set(
          error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
            ? error.error.title
            : 'Could not create this course. Try again.',
        );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
}
