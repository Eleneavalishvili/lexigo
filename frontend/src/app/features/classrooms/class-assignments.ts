import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import {
  AssignedLesson,
  AssignmentApi,
  AssignmentAttempt,
  AssignmentDetail,
  AssignmentSummary,
} from '../../core/classrooms/assignment-api';
import { ClassPage, ClassroomView } from '../../core/classrooms/classroom-api';
import { LearningApi } from '../../core/learning/learning-api';
import { AssignmentForm } from './assignment-form';
import { AssignmentResults } from './assignment-results';

@Component({
  selector: 'app-class-assignments',
  imports: [DatePipe, AssignmentForm, AssignmentResults],
  templateUrl: './class-assignments.html',
  styleUrl: './class-assignments.scss',
})
export class ClassAssignments implements OnInit {
  readonly classroom = input.required<ClassroomView>();
  private readonly api = inject(AssignmentApi);
  private readonly learning = inject(LearningApi);
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  protected readonly page = signal<ClassPage<AssignmentSummary> | null>(null);
  protected readonly assignment = signal<AssignmentDetail | null>(null);
  protected readonly loading = signal(false);
  protected readonly opening = signal(false);
  protected readonly starting = signal(false);
  protected readonly submitting = signal(false);
  protected readonly busy = computed(
    () => this.loading() || this.opening() || this.starting() || this.submitting(),
  );
  protected readonly formMode = signal<'create' | 'edit' | null>(null);
  protected readonly showResults = signal(false);
  protected readonly canManage = computed(() => {
    const classroom = this.classroom();
    return classroom.isTeacher && !classroom.isArchived && classroom.coursePublished;
  });
  protected readonly error = signal('');
  protected readonly notice = signal('');

  ngOnInit(): void {
    void this.load(1);
  }

  protected async load(page: number): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || this.busy()) return;
    this.loading.set(true);
    this.assignment.set(null);
    this.showResults.set(false);
    this.error.set('');
    try {
      const result = await this.api.list(this.classroom().id, page);
      if (this.current(owner)) this.page.set(result);
    } catch {
      if (this.current(owner)) {
        this.page.set(null);
        this.assignment.set(null);
        this.error.set('Assignments could not be loaded. Refresh the list to try again.');
      }
    } finally {
      if (this.current(owner)) this.loading.set(false);
    }
  }

  protected async open(id: string): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || this.busy()) return;
    this.opening.set(true);
    this.assignment.set(null);
    this.showResults.set(false);
    this.error.set('');
    try {
      const result = await this.api.detail(id);
      if (this.current(owner) && result.classroomId === this.classroom().id)
        this.assignment.set(result);
    } catch {
      if (this.current(owner)) this.error.set('This assignment is no longer available.');
    } finally {
      if (this.current(owner)) this.opening.set(false);
    }
  }

  protected async start(lesson: AssignedLesson): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || this.classroom().isTeacher || !lesson.isPublished || this.busy()) return;
    this.starting.set(true);
    this.error.set('');
    try {
      const result = await this.learning.start(lesson.id);
      if (this.current(owner)) await this.router.navigate(['/learn/session', result.id]);
    } catch {
      if (this.current(owner)) {
        this.error.set(
          'This lesson could not be opened. Check the prerequisites and available hearts on your course path.',
        );
      }
    } finally {
      if (this.current(owner)) this.starting.set(false);
    }
  }

  protected async submit(attempt: AssignmentAttempt): Promise<void> {
    const owner = this.auth.user()?.id;
    const assignment = this.assignment();
    if (!owner || !assignment || this.busy()) return;
    this.submitting.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      await this.api.submit(assignment.id, attempt.sessionId);
      if (!this.current(owner)) return;
      this.notice.set('Attempt submitted to your class.');
    } catch (error) {
      if (this.current(owner)) {
        this.error.set(
          error instanceof HttpErrorResponse && error.status === 409
            ? (error.error?.title ?? 'This attempt is no longer eligible.')
            : 'Submission could not be confirmed. You can safely retry this attempt.',
        );
      }
      return;
    } finally {
      if (this.current(owner)) this.submitting.set(false);
    }
    await this.open(assignment.id);
  }

  protected async saved(id: string): Promise<void> {
    this.notice.set(this.formMode() === 'edit' ? 'Assignment updated.' : 'Assignment created.');
    this.formMode.set(null);
    await this.load(this.page()?.page ?? 1);
    await this.open(id);
  }

  private current(owner: string): boolean {
    return !this.destroy.destroyed && this.auth.user()?.id === owner;
  }
}
