import { DestroyRef, Injectable, computed, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthStore } from '../accounts/auth-store';
import { CourseProgress, EnrollmentPage, LearningApi, LessonMapItem } from './learning-api';

@Injectable()
export class CourseStore {
  readonly selectedLevel = signal('');
  private readonly api = inject(LearningApi);
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private generation = 0;
  private courseId = '';
  readonly progress = signal<CourseProgress | null>(null);
  readonly nextLesson = computed<LessonMapItem | undefined>(() => {
    const course = this.progress();
    const startingOrder =
      course?.units.find((unit) => unit.id === course.recommendedUnitId)?.orderNumber ?? 0;
    const remaining =
      course?.lessons.filter((lesson) => lesson.isUnlocked && lesson.status !== 'Completed') ?? [];
    return (
      remaining.find(
        (lesson) =>
          (course?.units.find((unit) => unit.id === lesson.unitId)?.orderNumber ?? 0) >=
          startingOrder,
      ) ?? remaining[0]
    );
  });
  readonly enrollments = signal<EnrollmentPage | null>(null);
  readonly busy = signal(false);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly page = signal(1);
  constructor() {
    effect(() => {
      if (this.auth.user()?.id === this.owner) return;
      ++this.generation;
      this.progress.set(null);
      this.enrollments.set(null);
      this.error.set('');
      void this.router.navigate(['/login']);
    });
  }

  async selectCourse(courseId: string): Promise<void> {
    this.courseId = courseId;
    this.page.set(1);
    this.selectedLevel.set('');
    this.progress.set(null);
    this.enrollments.set(null);
    await this.load();
  }
  async load(): Promise<void> {
    const generation = ++this.generation;
    this.loading.set(true);
    this.error.set('');
    try {
      if (this.courseId) {
        const value = await this.api.progress(this.courseId);
        if (this.current(generation)) this.progress.set(value);
      } else {
        const value = await this.api.enrollments(this.page());
        if (this.current(generation)) this.enrollments.set(value);
      }
    } catch {
      if (this.current(generation))
        this.error.set('We couldn’t open your journey. Please try again.');
    } finally {
      if (this.current(generation)) this.loading.set(false);
    }
  }
  async join(): Promise<void> {
    if (this.busy()) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      if (this.selectedLevel()) await this.api.enroll(this.courseId, this.selectedLevel());
      else await this.api.enroll(this.courseId);
      if (this.current(generation)) await this.load();
    } catch {
      if (this.current(generation))
        this.error.set('We couldn’t join this course. Please try again.');
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  async checkStartingPoint(): Promise<void> {
    if (this.busy()) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      if (!this.progress()?.isEnrolled) await this.api.enroll(this.courseId);
      if (!this.current(generation)) return;
      const session = await this.api.placement(this.courseId);
      if (this.current(generation)) await this.router.navigate(['/learn/session', session.id]);
    } catch (error) {
      if (this.current(generation))
        this.error.set(
          error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
            ? error.error.title
            : 'We couldn’t open the placement check. Try again.',
        );
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }
  async start(lesson: LessonMapItem, practice = false): Promise<void> {
    if (this.busy() || !lesson.isUnlocked) return;
    if (this.progress()?.startingLevel === null) {
      this.error.set('Choose and save your starting level before beginning a new lesson.');
      return;
    }
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      const value = practice ? await this.api.practice(lesson.id) : await this.api.start(lesson.id);
      if (this.current(generation)) await this.router.navigate(['/learn/session', value.id]);
    } catch (error) {
      if (this.current(generation))
        this.error.set(
          error instanceof HttpErrorResponse && error.status === 409
            ? 'This lesson isn’t available yet. Refresh your journey to check its prerequisites.'
            : 'We couldn’t start the lesson. Please try again.',
        );
    } finally {
      if (!this.destroy.destroyed) this.busy.set(false);
    }
  }
  lessons(unitId: string): readonly LessonMapItem[] {
    return this.progress()?.lessons.filter((item) => item.unitId === unitId) ?? [];
  }
  completedCount(): number {
    return this.progress()?.lessons.filter((item) => item.status === 'Completed').length ?? 0;
  }
  unitCompleted(unitId: string): boolean {
    const lessons = this.lessons(unitId);
    return lessons.length > 0 && lessons.every((lesson) => lesson.status === 'Completed');
  }
  lessonTitle(id: string): string | undefined {
    return this.progress()?.lessons.find((lesson) => lesson.id === id)?.title;
  }
  async changePage(step: number): Promise<void> {
    this.page.update((page) => page + step);
    await this.load();
  }
  private current(generation: number): boolean {
    return (
      !this.destroy.destroyed &&
      generation === this.generation &&
      this.auth.user()?.id === this.owner
    );
  }
}
