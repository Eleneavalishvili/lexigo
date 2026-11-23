import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { ExtraStudyApi, ExtraStudySpaceView } from '../../core/classrooms/extra-study-api';
import { CatalogApi, CatalogLesson, CatalogUnit } from '../../core/content/catalog-api';
import { LearningApi } from '../../core/learning/learning-api';

@Component({
  selector: 'app-extra-study-space',
  imports: [RouterLink],
  templateUrl: './extra-study-space.html',
  styleUrl: './classrooms.scss',
})
export class ExtraStudySpace {
  private readonly auth = inject(AuthStore);
  private readonly api = inject(ExtraStudyApi);
  private readonly catalog = inject(CatalogApi);
  private readonly learning = inject(LearningApi);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly params = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  private generation = 0;
  private unitRequest = 0;
  protected readonly space = signal<ExtraStudySpaceView | undefined>(undefined);
  protected readonly units = signal<readonly CatalogUnit[]>([]);
  protected readonly lessons = signal<readonly CatalogLesson[]>([]);
  protected readonly error = signal('');
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);

  constructor() {
    effect(() => {
      const owner = this.auth.user()?.id;
      const id = this.params().get('requestId');
      const generation = ++this.generation;
      this.space.set(undefined);
      this.units.set([]);
      this.lessons.set([]);
      this.error.set('');
      this.busy.set(false);
      this.loading.set(!!owner && !!id);
      if (owner && id) void this.open(id, generation);
    });
  }

  private current(generation: number): boolean {
    return !this.destroy.destroyed && generation === this.generation;
  }

  private async open(id: string, generation: number): Promise<void> {
    try {
      const space = await this.api.space(id);
      if (!this.current(generation)) return;
      this.space.set(space);
      if (!space.canShare) return;
      const units = await this.catalog.units(space.courseId);
      if (this.current(generation)) this.units.set(units);
    } catch (error) {
      if (this.current(generation)) {
        this.space.set(undefined);
        this.error.set(
          error instanceof HttpErrorResponse && error.status === 404
            ? 'This study space is unavailable. Check the request and the learner’s Premium access.'
            : 'The study space could not load. Refresh to try again.',
        );
      }
    } finally {
      if (this.current(generation)) this.loading.set(false);
    }
  }

  protected refresh(): void {
    const id = this.params().get('requestId');
    if (!id || this.busy() || this.loading() || !this.auth.user()) return;
    const generation = ++this.generation;
    this.error.set('');
    this.space.set(undefined);
    this.units.set([]);
    this.lessons.set([]);
    this.loading.set(true);
    void this.open(id, generation);
  }

  protected async chooseUnit(unitId: string): Promise<void> {
    const generation = this.generation;
    const request = ++this.unitRequest;
    this.lessons.set([]);
    if (!unitId) return;
    try {
      const lessons = await this.catalog.lessons(unitId);
      if (this.current(generation) && request === this.unitRequest) this.lessons.set(lessons);
    } catch {
      if (this.current(generation) && request === this.unitRequest)
        this.error.set('The lessons for this unit could not load.');
    }
  }

  protected async share(lessonId: string): Promise<void> {
    const requestId = this.params().get('requestId');
    if (!requestId || !lessonId || this.busy()) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.share(requestId, lessonId);
      if (!this.current(generation)) return;
      const space = await this.api.space(requestId);
      if (this.current(generation)) this.space.set(space);
    } catch (error) {
      if (this.current(generation)) {
        if (error instanceof HttpErrorResponse && error.status === 404) {
          await this.open(requestId, generation);
          if (!this.current(generation) || !this.space()) return;
        }
        this.error.set(
          'The lesson could not be shared. Check that it is still published and the space is available.',
        );
      }
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }

  protected async start(lessonId: string): Promise<void> {
    if (this.busy()) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      const session = await this.learning.start(lessonId);
      if (this.current(generation)) await this.router.navigate(['/learn/session', session.id]);
    } catch {
      if (this.current(generation))
        this.error.set(
          'This lesson could not start. Check your course journey and any earlier lessons you need to finish.',
        );
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }
}
