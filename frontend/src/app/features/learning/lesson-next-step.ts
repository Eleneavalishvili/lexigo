import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import {
  CourseProgress,
  LearningApi,
  LessonMapItem,
  SessionView,
} from '../../core/learning/learning-api';

@Component({
  selector: 'app-lesson-next-step',
  imports: [RouterLink],
  templateUrl: './lesson-next-step.html',
  styleUrl: './lesson-next-step.scss',
})
export class LessonNextStep {
  readonly session = input.required<SessionView>();
  private readonly api = inject(LearningApi);
  private readonly auth = inject(AuthStore);
  private readonly owner = this.auth.user()?.id;
  private readonly router = inject(Router);
  private readonly lifetime = inject(DestroyRef);
  private generation = 0;
  protected readonly nextLesson = signal<LessonMapItem | null>(null);
  protected readonly loading = signal(false);
  protected readonly ready = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  constructor() {
    effect(() => {
      const session = this.session();
      const owner = this.auth.user()?.id;
      ++this.generation;
      this.nextLesson.set(null);
      this.ready.set(false);
      this.busy.set(false);
      this.loading.set(false);
      this.error.set('');
      if (
        owner === this.owner &&
        owner &&
        session.completedAt &&
        session.passed &&
        session.mode === 'Lesson' &&
        !session.isPlacement
      )
        void this.load();
    });
  }

  protected async load(): Promise<void> {
    const generation = ++this.generation;
    const session = this.session();
    const owner = this.auth.user()?.id;
    if (!owner || owner !== this.owner) return;
    this.loading.set(true);
    this.ready.set(false);
    this.nextLesson.set(null);
    this.error.set('');
    try {
      const course = await this.api.progress(session.courseId);
      if (!this.current(generation, owner)) return;
      if (!course.isEnrolled) throw new Error('Course unavailable');
      this.nextLesson.set(this.chooseNext(course, session.lessonId));
      this.ready.set(true);
    } catch {
      if (this.current(generation, owner))
        this.error.set('Your next step could not load. Refresh it or return to your journey.');
    } finally {
      if (this.current(generation, owner)) this.loading.set(false);
    }
  }

  protected async open(kind: 'next' | 'retry' | 'practice'): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || owner !== this.owner || this.busy()) return;
    const next = this.nextLesson();
    if (kind === 'next' && !next) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      let sessionId: string;
      if (kind === 'next' && next?.activeSessionId) {
        sessionId = next.activeSessionId;
      } else if (kind === 'practice') {
        const lessonId = this.session().lessonId;
        const { courseId, practiceSkillId } = this.session();
        const practice =
          lessonId && lessonId !== '00000000-0000-0000-0000-000000000000'
            ? this.api.practice(lessonId)
            : practiceSkillId
              ? this.api.practiceMistakes(courseId, practiceSkillId)
              : this.api.practiceMistakes(courseId);
        sessionId = (await practice).id;
      } else {
        sessionId = (await this.api.start(kind === 'next' ? next!.id : this.session().lessonId)).id;
      }
      if (this.current(generation, owner))
        await this.router.navigate(['/learn/session', sessionId]);
    } catch (error) {
      if (this.current(generation, owner))
        this.error.set(
          error instanceof HttpErrorResponse && error.status === 409
            ? 'This step is not available right now. Return to your journey to check the current lessons, practice and hearts.'
            : 'The lesson could not open. Your saved result is safe. Try again.',
        );
    } finally {
      if (this.current(generation, owner)) this.busy.set(false);
    }
  }

  private chooseNext(course: CourseProgress, currentId: string): LessonMapItem | null {
    const unitOrder = new Map(course.units.map((unit) => [unit.id, unit.orderNumber]));
    const ordered = [...course.lessons].sort(
      (left, right) =>
        (unitOrder.get(left.unitId) ?? 0) - (unitOrder.get(right.unitId) ?? 0) ||
        left.orderNumber - right.orderNumber,
    );
    const position = ordered.findIndex((item) => item.id === currentId);
    const unfinished = (item: LessonMapItem): boolean =>
      item.id !== currentId && item.isUnlocked && item.status !== 'Completed';
    return ordered.slice(position + 1).find(unfinished) ?? ordered.find(unfinished) ?? null;
  }

  private current(generation: number, owner: string): boolean {
    return (
      !this.lifetime.destroyed && generation === this.generation && this.auth.user()?.id === owner
    );
  }
}
