import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CatalogApi, CatalogCourse } from '../../core/content/catalog-api';
import { VocabularyStore } from '../../core/vocabulary/vocabulary-store';
import {
  VocabularyApi,
  vocabularyTopics,
  WordCollectionView,
  WordReviewResult,
  WordView,
} from '../../core/vocabulary/vocabulary-api';
import { AuthStore } from '../../core/accounts/auth-store';
import { HttpErrorResponse } from '@angular/common/http';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { WordRecording } from './word-recording';
import { VocabularyReference } from './vocabulary-reference';

@Component({
  selector: 'app-vocabulary',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    DatePipe,
    BrandLogo,
    WordRecording,
    VocabularyReference,
  ],
  providers: [VocabularyStore],
  templateUrl: './vocabulary.html',
  styleUrl: './vocabulary.scss',
})
export class Vocabulary {
  protected readonly store = inject(VocabularyStore);
  private readonly api = inject(VocabularyApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  protected readonly review = signal<{ word: WordView; key: string; owner: string } | undefined>(
    undefined,
  );
  protected readonly reviewAnswer = new FormControl('', { nonNullable: true });
  protected readonly reviewResult = signal<WordReviewResult | undefined>(undefined);
  protected readonly reviewError = signal('');
  protected readonly reviewing = signal(false);
  protected readonly collections = signal<WordCollectionView[]>([]);
  protected readonly collectionError = signal('');
  protected readonly collectionBusy = signal(false);
  protected readonly collectionId = signal('');
  protected readonly collectionsOpen = signal(false);
  protected readonly confirmingDelete = signal(false);
  protected readonly courses = signal<readonly CatalogCourse[]>([]);
  protected readonly topics = vocabularyTopics;
  protected readonly topicLabels = new Map<string, string>(
    vocabularyTopics.map(({ id, label }) => [id, label]),
  );
  protected readonly form = new FormGroup({
    search: new FormControl('', { nonNullable: true }),
    courseId: new FormControl('', { nonNullable: true }),
    topic: new FormControl('', { nonNullable: true }),
    status: new FormControl<'all' | 'due' | 'scheduled'>('all', { nonNullable: true }),
  });
  constructor() {
    effect(() => {
      void this.auth.user()?.id;
      this.collections.set([]);
      this.collectionId.set('');
      this.collectionsOpen.set(false);
      this.confirmingDelete.set(false);
    });
    const destroy = inject(DestroyRef);
    void inject(CatalogApi)
      .courses()
      .then((value) => {
        if (!destroy.destroyed) this.courses.set(value);
      })
      .catch(() => {
        /* All-language searching remains available when the course menu is unavailable. */
      });
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed(destroy))
      .subscribe((params) => {
        const requestedCourse = params.get('courseId') ?? '';
        const courseId = /^[\da-f]{8}(?:-[\da-f]{4}){3}-[\da-f]{12}$/i.test(requestedCourse)
          ? requestedCourse
          : '';
        const status = params.get('status') === 'due' ? 'due' : 'all';
        this.form.reset({ courseId, status, search: '', topic: '' });
        this.collectionId.set('');
        this.review.set(undefined);
        void this.store.load({
          ...this.form.getRawValue(),
          savedOnly: true,
          collectionId: '',
          page: 1,
        });
      });
  }
  protected async loadCollections(): Promise<void> {
    const owner = this.auth.user()?.id;
    try {
      const items = await this.api.collections();
      if (!this.destroy.destroyed && this.auth.user()?.id === owner) this.collections.set(items);
    } catch {
      if (!this.destroy.destroyed && this.auth.user()?.id === owner)
        this.collectionError.set('Collections could not load.');
    }
  }

  protected selectCollection(id: string): void {
    this.review.set(undefined);
    this.collectionId.set(id);
    this.confirmingDelete.set(false);
    if (id) this.form.controls.courseId.setValue('');
    void this.store.load({
      collectionId: id,
      courseId: this.form.controls.courseId.value,
      page: 1,
    });
  }

  protected async toggleSavedWord(word: WordView): Promise<void> {
    await this.store.toggle(word);
    if (!this.destroy.destroyed && word.savedWordId && this.collectionsOpen()) {
      await this.loadCollections();
    }
  }

  protected saveCollection(courseId: string, name: string): Promise<void> {
    const id = this.collectionId();
    if (!name.trim() || (!id && !courseId)) {
      this.collectionError.set(
        id ? 'Enter a new collection name.' : 'Choose a course and enter a collection name.',
      );
      return Promise.resolve();
    }
    const owner = this.auth.user()?.id;
    return this.changeCollection(async () => {
      if (id) await this.api.renameCollection(id, name);
      else if (courseId) {
        const created = await this.api.createCollection(courseId, name);
        if (!this.destroy.destroyed && this.auth.user()?.id === owner)
          this.selectCollection(created.id);
      }
    });
  }

  protected removeCollection(): Promise<void> {
    const id = this.collectionId();
    const owner = this.auth.user()?.id;
    return this.changeCollection(async () => {
      if (!id) return;
      await this.api.deleteCollection(id);
      if (!this.destroy.destroyed && this.auth.user()?.id === owner && this.collectionId() === id)
        this.selectCollection('');
    });
  }

  protected setCollectionWord(collectionId: string, wordId: string, add: boolean): Promise<void> {
    return this.changeCollection(() => this.api.setCollectionWord(collectionId, wordId, add));
  }

  private async changeCollection(action: () => Promise<unknown>): Promise<void> {
    if (this.collectionBusy()) return;
    const owner = this.auth.user()?.id;
    this.collectionBusy.set(true);
    this.collectionError.set('');
    try {
      await action();
      if (!this.destroy.destroyed && this.auth.user()?.id === owner) {
        await this.loadCollections();
        if (this.collectionId()) await this.store.load();
      }
    } catch {
      if (!this.destroy.destroyed && this.auth.user()?.id === owner)
        this.collectionError.set(
          'The collection could not be updated. Check your selection and try again.',
        );
    } finally {
      if (!this.destroy.destroyed) this.collectionBusy.set(false);
    }
  }
  protected search(): void {
    this.review.set(undefined);
    void this.store.load({ ...this.form.getRawValue(), page: 1 });
  }
  protected tab(savedOnly: boolean): void {
    this.review.set(undefined);
    this.collectionId.set('');
    this.form.controls.status.setValue('all');
    void this.store.load({ ...this.form.getRawValue(), savedOnly, collectionId: '', page: 1 });
  }

  protected beginReview(word: WordView): void {
    const owner = this.auth.user()?.id;
    if (!owner || !word.savedWordId || this.reviewing()) return;
    this.review.set({ word, key: crypto.randomUUID(), owner });
    this.reviewAnswer.reset();
    this.reviewResult.set(undefined);
    this.reviewError.set('');
  }

  protected async gradeReview(): Promise<void> {
    const review = this.review();
    const answer = this.reviewAnswer.value.trim();
    if (!review?.word.savedWordId || !answer || this.reviewing() || this.reviewResult()) return;
    const current = () =>
      !this.destroy.destroyed && this.auth.user()?.id === review.owner && this.review() === review;
    this.reviewing.set(true);
    this.reviewError.set('');
    try {
      const result = await this.api.review(review.word.savedWordId, answer, review.key);
      if (current()) this.reviewResult.set(result);
    } catch (error) {
      if (current())
        this.reviewError.set(
          error instanceof HttpErrorResponse && error.status === 409
            ? 'This word is no longer due. Close the review to refresh your list.'
            : 'The answer could not be checked. Try submitting it again.',
        );
    } finally {
      if (!this.destroy.destroyed) this.reviewing.set(false);
    }
  }

  protected finishReview(): void {
    this.review.set(undefined);
    void this.store.load();
  }
}
