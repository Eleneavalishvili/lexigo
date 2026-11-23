import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export const vocabularyTopics = [
  { id: 'greetings', label: 'Alphabet and greetings' },
  { id: 'numbers', label: 'Numbers and time' },
  { id: 'family', label: 'Family and work' },
  { id: 'food', label: 'Food and shopping' },
  { id: 'travel', label: 'Travel and directions' },
  { id: 'routines', label: 'Routines and weather' },
] as const;

export interface WordView {
  id: string;
  courseId: string;
  courseTitle: string;
  languageCode: string;
  word: string;
  englishTranslation: string;
  example: string;
  partOfSpeech: string;
  topic?: string | null;
  hasAudio?: boolean;
  savedWordId: string | null;
  dueAt: string | null;
  intervalDays: number | null;
}
export interface WordPage {
  items: readonly WordView[];
  page: number;
  pageSize: number;
  totalCount: number;
}
export interface WordReviewResult {
  id: string;
  isCorrect: boolean;
  dueAt: string;
  intervalDays: number;
}
export interface WordAudioLink {
  url: string;
  expiresInSeconds: number;
  source: string;
  license: string;
}
export interface WordFilters {
  savedOnly: boolean;
  courseId: string;
  search: string;
  status: 'all' | 'due' | 'scheduled';
  page: number;
  collectionId?: string;
  topic?: string;
}
export interface WordCollectionView {
  id: string;
  courseId: string;
  name: string;
  wordCount: number;
}
@Injectable({ providedIn: 'root' })
export class VocabularyApi {
  private readonly http = inject(HttpClient);
  private readonly collectionsPath = '/api/v1/me/collections';

  audio(wordId: string): Promise<WordAudioLink> {
    return firstValueFrom(
      this.http.get<WordAudioLink>('/api/v1/vocabulary/' + encodeURIComponent(wordId) + '/audio'),
    );
  }

  collections(): Promise<WordCollectionView[]> {
    return firstValueFrom(this.http.get<WordCollectionView[]>(this.collectionsPath));
  }

  createCollection(courseId: string, name: string): Promise<{ id: string }> {
    return firstValueFrom(this.http.post<{ id: string }>(this.collectionsPath, { courseId, name }));
  }

  renameCollection(id: string, name: string): Promise<unknown> {
    return firstValueFrom(
      this.http.put(this.collectionsPath + '/' + encodeURIComponent(id), { name }),
    );
  }

  deleteCollection(id: string): Promise<unknown> {
    return firstValueFrom(this.http.delete(this.collectionsPath + '/' + encodeURIComponent(id)));
  }

  setCollectionWord(collectionId: string, wordId: string, add: boolean): Promise<unknown> {
    const url =
      this.collectionsPath +
      '/' +
      encodeURIComponent(collectionId) +
      '/words/' +
      encodeURIComponent(wordId);
    return firstValueFrom(add ? this.http.post(url, null) : this.http.delete(url));
  }
  review(savedWordId: string, answer: string, idempotencyKey: string): Promise<WordReviewResult> {
    return firstValueFrom(
      this.http.post<WordReviewResult>(
        '/api/v1/me/saved-words/' + encodeURIComponent(savedWordId) + '/reviews',
        { answer, idempotencyKey },
      ),
    );
  }
  list(filters: WordFilters): Promise<WordPage> {
    let params = new HttpParams()
      .set('courseId', filters.courseId)
      .set('search', filters.search)
      .set('status', filters.status)
      .set('page', filters.page);
    if (filters.collectionId) params = params.set('collectionId', filters.collectionId);
    if (filters.topic) params = params.set('topic', filters.topic);
    return firstValueFrom(
      this.http.get<WordPage>(filters.savedOnly ? '/api/v1/me/saved-words' : '/api/v1/vocabulary', {
        params,
      }),
    );
  }
  save(vocabularyItemId: string): Promise<unknown> {
    return firstValueFrom(this.http.post('/api/v1/me/saved-words', { vocabularyItemId }));
  }
  remove(id: string): Promise<void> {
    return firstValueFrom(
      this.http.delete<void>('/api/v1/me/saved-words/' + encodeURIComponent(id)),
    );
  }
}
