import { Component, DestroyRef, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { ContentApi, StaffCourse } from '../../core/content/content-api';
import {
  VocabularyAction,
  VocabularyRevision,
  VocabularyRevisionApi,
} from '../../core/content/vocabulary-revision-api';
import { VocabularyApi, vocabularyTopics, WordView } from '../../core/vocabulary/vocabulary-api';

@Component({
  selector: 'app-vocabulary-revision-workbench',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './vocabulary-revision-workbench.html',
  styles: `
    main {
      max-width: 54rem;
      margin: auto;
      padding: 2rem 1rem;
    }
    article,
    form {
      border-block-end: 1px solid var(--muted);
      padding-block: 1rem;
    }
    label {
      display: block;
      margin-block: 0.5rem;
    }
    input,
    select,
    textarea {
      display: block;
      max-width: 100%;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
    }
  `,
})
export class VocabularyRevisionWorkbench {
  private readonly api = inject(VocabularyRevisionApi);
  private readonly content = inject(ContentApi);
  private readonly vocabulary = inject(VocabularyApi);
  private readonly auth = inject(AuthStore);
  private readonly owner = this.auth.user()?.id;
  private readonly destroy = inject(DestroyRef);
  protected readonly courses = signal<readonly StaffCourse[]>([]);
  protected readonly course = signal<StaffCourse | undefined>(undefined);
  protected readonly revisions = signal<VocabularyRevision[]>([]);
  protected readonly matches = signal<readonly WordView[]>([]);
  protected readonly editing = signal<VocabularyRevision | undefined>(undefined);
  protected readonly existingWord = signal<WordView | undefined>(undefined);
  protected readonly reviewing = signal<string | null>(null);
  protected readonly withdrawing = signal<string | null>(null);
  protected readonly page = signal(1);
  protected readonly busy = signal(false);
  protected readonly notice = signal('');
  protected readonly error = signal('');
  protected readonly topics = vocabularyTopics;
  protected readonly form = new FormGroup({
    word: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    englishTranslation: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    example: new FormControl('', { nonNullable: true }),
    partOfSpeech: new FormControl('', { nonNullable: true }),
    topic: new FormControl('', { nonNullable: true }),
  });

  constructor() {
    void this.run(async () => {
      const courses = await this.content.courses();
      if (this.active()) this.courses.set(courses);
    });
  }

  protected selectCourse(id: string): void {
    this.course.set(this.courses().find((item) => item.id === id));
    this.resetForm();
    this.matches.set([]);
    this.revisions.set([]);
    this.page.set(1);
    void this.refresh(1);
  }

  protected refresh(page: number): Promise<void> {
    return this.run(() => this.load(page));
  }

  protected search(search: string): Promise<void> {
    const course = this.course();
    if (!course || !search.trim()) return Promise.resolve();
    return this.run(async () => {
      const result = await this.vocabulary.list({
        courseId: course.id,
        search,
        savedOnly: false,
        status: 'all',
        page: 1,
      });
      if (this.active()) {
        this.matches.set(result.items);
        if (!result.items.length) this.notice.set('No published words matched your search.');
      }
    });
  }

  protected chooseWord(word: WordView): void {
    this.resetForm();
    this.existingWord.set(word);
    this.form.reset({
      word: word.word,
      englishTranslation: word.englishTranslation,
      example: word.example,
      partOfSpeech: word.partOfSpeech,
      topic: word.topic ?? '',
    });
  }

  protected edit(revision: VocabularyRevision): void {
    this.resetForm();
    this.editing.set(revision);
    this.form.reset({ ...revision.text, topic: revision.text.topic ?? '' });
  }

  protected resetForm(): void {
    this.editing.set(undefined);
    this.existingWord.set(undefined);
    this.form.reset();
  }

  protected save(): Promise<void> {
    const course = this.course();
    const revision = this.editing();
    const values = this.form.getRawValue();
    if (!course || this.form.invalid || !values.word.trim() || !values.englishTranslation.trim()) {
      this.error.set('Enter the word and its English translation.');
      return Promise.resolve();
    }
    const text = { ...values, topic: values.topic || null };
    return this.run(async () => {
      if (revision) await this.api.change(revision, 'edit', text);
      else await this.api.create(course.id, this.existingWord()?.id ?? null, text);
      if (!this.active()) return;
      this.resetForm();
      this.notice.set('Draft saved.');
      await this.load(revision ? this.page() : 1);
    });
  }

  protected change(
    revision: VocabularyRevision,
    action: VocabularyAction,
    comment?: string,
  ): Promise<void> {
    return this.run(async () => {
      await this.api.change(revision, action, null, comment ?? null);
      if (!this.active()) return;
      if (this.editing()?.id === revision.id) this.resetForm();
      this.notice.set('Revision updated.');
      await this.load(this.page());
    });
  }

  protected owns(revision: VocabularyRevision): boolean {
    return this.auth.user()?.id === revision.authorId;
  }

  protected admin(): boolean {
    return !!this.auth.user()?.roles.some((role) => role === 'Admin' || role === 'SuperAdmin');
  }

  private async load(page: number): Promise<void> {
    const course = this.course();
    if (!course) return;
    const revisions = await this.api.list(course.id, page);
    if (!this.active()) return;
    this.revisions.set(revisions);
    this.reviewing.set(null);
    this.withdrawing.set(null);
    this.page.set(page);
  }

  private async run(action: () => Promise<void>): Promise<void> {
    if (this.busy() || !this.active()) return;
    this.busy.set(true);
    this.notice.set('');
    this.error.set('');
    try {
      await action();
    } catch (error) {
      if (this.active())
        this.error.set(
          error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
            ? error.error.title
            : 'The vocabulary workspace could not finish this request.',
        );
    } finally {
      if (this.active()) this.busy.set(false);
    }
  }

  private active(): boolean {
    return !this.destroy.destroyed && !!this.owner && this.owner === this.auth.user()?.id;
  }
}
