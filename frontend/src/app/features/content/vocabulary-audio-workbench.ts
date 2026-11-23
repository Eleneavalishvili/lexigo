import { Component, DestroyRef, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthStore } from '../../core/accounts/auth-store';
import { ContentApi, StaffCourse, StaffWord } from '../../core/content/content-api';
import { ReviewedAudioPicker } from './reviewed-audio-picker';

@Component({
  selector: 'app-vocabulary-audio-workbench',
  imports: [RouterLink, ReviewedAudioPicker],
  templateUrl: './vocabulary-audio-workbench.html',
  styles: `
    main {
      max-width: 52rem;
      margin: auto;
      padding: 2rem 1rem;
    }
    nav,
    .actions {
      display: flex;
      gap: 0.75rem;
      flex-wrap: wrap;
      align-items: center;
    }
    article {
      border-block-end: 1px solid var(--muted);
      padding-block: 1rem;
    }
    select {
      display: block;
      margin-block: 0.5rem;
      max-width: 100%;
    }
  `,
})
export class VocabularyAudioWorkbench {
  private readonly api = inject(ContentApi);
  private readonly auth = inject(AuthStore);
  private readonly owner = this.auth.user()?.id;
  private readonly destroy = inject(DestroyRef);
  protected readonly courses = signal<readonly StaffCourse[]>([]);
  protected readonly course = signal<StaffCourse | undefined>(undefined);
  protected readonly rows = signal<StaffWord[]>([]);
  protected readonly selected = signal<StaffWord | undefined>(undefined);
  protected readonly audioId = signal<string | null>(null);
  protected readonly page = signal(1);
  protected readonly total = signal(0);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly notice = signal('');
  protected readonly error = signal('');

  constructor() {
    void this.loadCourses();
  }

  private async loadCourses(): Promise<void> {
    try {
      const courses = await this.api.courses();
      if (this.current()) this.courses.set(courses);
    } catch {
      if (this.current()) this.error.set('Courses could not be loaded.');
    }
  }

  protected chooseCourse(id: string): void {
    this.notice.set('');
    this.course.set(this.courses().find((item) => item.id === id));
    void this.load(1);
  }

  protected async load(page: number): Promise<void> {
    const course = this.course();
    this.error.set('');
    this.selected.set(undefined);
    this.rows.set([]);
    this.total.set(0);
    if (!course) return;
    this.loading.set(true);
    try {
      const result = await this.api.words(course.id, page);
      if (!this.current()) return;
      this.rows.set(result.items);
      this.total.set(result.total);
      this.page.set(page);
    } catch {
      if (this.current()) this.error.set('Words could not be loaded. Refresh the list to retry.');
    } finally {
      if (this.current()) this.loading.set(false);
    }
  }

  protected edit(word: StaffWord): void {
    this.notice.set('');
    this.error.set('');
    this.selected.set(word);
    this.audioId.set(word.audioMediaId);
  }

  protected async save(word: StaffWord): Promise<void> {
    if (this.saving() || this.audioId() === word.audioMediaId) return;
    this.saving.set(true);
    this.error.set('');
    try {
      await this.api.attachWordAudio(word, this.audioId());
      if (!this.current()) return;
      this.notice.set('Recording updated.');
      await this.load(this.page());
    } catch (error) {
      if (this.current()) {
        this.error.set(
          error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
            ? error.error.title
            : 'The recording could not be updated.',
        );
      }
    } finally {
      if (this.current()) this.saving.set(false);
    }
  }

  private current(): boolean {
    return !this.destroy.destroyed && this.owner === this.auth.user()?.id;
  }
}
