import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface VocabularyText {
  word: string;
  englishTranslation: string;
  example: string;
  partOfSpeech: string;
  topic: string | null;
}

export interface VocabularyRevision {
  id: string;
  revisionToken: string;
  wordId: string | null;
  authorId: string;
  status: 'Draft' | 'Submitted' | 'Approved' | 'Published' | 'Withdrawn';
  text: VocabularyText;
  reviewComment: string | null;
}

export type VocabularyAction =
  'edit' | 'submit' | 'approve' | 'request-changes' | 'publish' | 'withdraw';

@Injectable({ providedIn: 'root' })
export class VocabularyRevisionApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/manage/vocabulary/revisions';

  list(courseId: string, page: number): Promise<VocabularyRevision[]> {
    return firstValueFrom(
      this.http.get<VocabularyRevision[]>(this.url, { params: { courseId, page } }),
    );
  }

  create(courseId: string, wordId: string | null, text: VocabularyText): Promise<unknown> {
    return firstValueFrom(this.http.post(this.url, { courseId, wordId, text }));
  }

  change(
    revision: VocabularyRevision,
    action: VocabularyAction,
    text: VocabularyText | null = null,
    comment: string | null = null,
  ): Promise<unknown> {
    return firstValueFrom(
      this.http.put(this.url + '/' + encodeURIComponent(revision.id), {
        revisionToken: revision.revisionToken,
        action,
        text,
        comment,
      }),
    );
  }
}
