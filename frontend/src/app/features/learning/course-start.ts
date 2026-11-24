import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthStore } from '../../core/accounts/auth-store';
import { PreferenceStore } from '../../core/accounts/preference-store';
import { LearningApi } from '../../core/learning/learning-api';
import { CharacterCue, courseCompanion } from '../../shared/character-cue/character-cue';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-course-start',
  imports: [RouterLink, CharacterCue, BrandLogo],
  templateUrl: './course-start.html',
  styleUrl: './course-start.scss',
})
export class CourseStart {
  private readonly api = inject(LearningApi);
  private readonly preferences = inject(PreferenceStore);
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private generation = 0;
  protected readonly courseId = signal('');
  protected readonly title = signal('your language');
  protected readonly step = signal(0);
  protected readonly goal = signal<number | null>(null);
  protected readonly start = signal<'scratch' | 'placement'>('scratch');
  protected readonly busy = signal(true);
  protected readonly courseReady = signal(false);
  protected readonly error = signal('');
  protected readonly goals = [
    { xp: 20, label: 'A little every day' },
    { xp: 40, label: 'Build a habit' },
    { xp: 60, label: 'Make steady progress' },
    { xp: 80, label: 'Go a little further' },
  ];
  protected readonly companion = computed(() => courseCompanion(this.courseId()));
  constructor() {
    effect(() => {
      if (this.auth.user()?.id === this.owner) return;
      ++this.generation;
      this.title.set('your language');
      void this.router.navigate(['/login']);
    });
    inject(ActivatedRoute)
      .paramMap.pipe(takeUntilDestroyed())
      .subscribe((params) => {
        this.courseId.set(params.get('courseId') ?? '');
        this.title.set('your language');
        this.step.set(0);
        this.goal.set(null);
        this.start.set('scratch');
        void this.load();
      });
  }
  private async load(): Promise<void> {
    const generation = ++this.generation;
    this.busy.set(true);
    this.courseReady.set(false);
    this.error.set('');
    try {
      const course = await this.api.progress(this.courseId());
      if (this.current(generation)) {
        this.title.set(course.title);
        this.courseReady.set(true);
      }
    } catch {
      if (this.current(generation))
        this.error.set('This course could not be opened. Try again or return to your languages.');
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }
  protected async retryCourse(): Promise<void> {
    if (!this.busy()) await this.load();
  }
  protected async begin(): Promise<void> {
    const goal = this.goal();
    if (this.busy() || !this.courseReady() || goal === null) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.preferences.setStudyGoal(goal);
      if (!this.current(generation)) return;
      await this.api.enroll(this.courseId(), this.start() === 'scratch' ? 'A1' : undefined);
      if (!this.current(generation)) return;
      if (this.start() === 'placement') {
        const session = await this.api.placement(this.courseId());
        if (this.current(generation)) await this.router.navigate(['/learn/session', session.id]);
      } else {
        const course = await this.api.progress(this.courseId());
        if (!this.current(generation)) return;
        const first = course.lessons.find((lesson) => lesson.isUnlocked);
        if (!first) throw new Error('No beginner lesson is available');
        const session = await this.api.start(first.id);
        if (this.current(generation)) await this.router.navigate(['/learn/session', session.id]);
      }
    } catch (error) {
      if (this.current(generation))
        this.error.set(
          error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
            ? error.error.title + ' Your saved progress is kept.'
            : 'We couldn’t open your first step. Your saved progress is kept; try again.',
        );
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }
  private current(generation: number): boolean {
    return (
      generation === this.generation &&
      !this.destroy.destroyed &&
      this.auth.user()?.id === this.owner
    );
  }
}
