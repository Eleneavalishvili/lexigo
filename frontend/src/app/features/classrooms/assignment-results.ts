import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthStore } from '../../core/accounts/auth-store';
import { AssignmentApi, AssignmentResultsPage } from '../../core/classrooms/assignment-api';

@Component({
  selector: 'app-assignment-results',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './assignment-results.html',
  styleUrl: './assignment-results.scss',
})
export class AssignmentResults implements OnInit {
  readonly assignmentId = input.required<string>();
  private readonly api = inject(AssignmentApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  protected readonly result = signal<AssignmentResultsPage | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly editing = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly message = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(1000)],
  });

  ngOnInit(): void {
    void this.load(1);
  }

  protected async load(page: number): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || this.loading() || this.saving()) return;
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.api.results(this.assignmentId(), page);
      if (this.current(owner)) this.result.set(result);
    } catch {
      if (this.current(owner)) {
        this.result.set(null);
        this.error.set('Results could not be loaded. Refresh to try again.');
      }
    } finally {
      if (this.current(owner)) this.loading.set(false);
    }
  }

  protected openFeedback(submissionId: string): void {
    this.error.set('');
    this.notice.set('');
    this.message.reset();
    this.editing.set(submissionId);
  }

  protected async postFeedback(): Promise<void> {
    const owner = this.auth.user()?.id;
    const submissionId = this.editing();
    const message = this.message.value.trim();
    if (!owner || !submissionId || this.saving() || this.message.invalid || !message) return;
    this.saving.set(true);
    this.error.set('');
    try {
      await this.api.postFeedback(this.assignmentId(), submissionId, message);
      if (!this.current(owner)) return;
      this.editing.set(null);
      this.notice.set('Feedback posted.');
    } catch (error) {
      if (this.current(owner)) {
        this.error.set(
          error instanceof HttpErrorResponse && error.status === 409
            ? (error.error?.title ?? 'This attempt can no longer receive feedback.')
            : 'Feedback could not be confirmed. Retry with the same message.',
        );
      }
      return;
    } finally {
      if (this.current(owner)) this.saving.set(false);
    }
    await this.load(this.result()?.learners.page ?? 1);
  }

  private current(owner: string): boolean {
    return !this.destroy.destroyed && this.auth.user()?.id === owner;
  }
}
