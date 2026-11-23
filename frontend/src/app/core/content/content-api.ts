import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom, fromEvent, takeUntil } from 'rxjs';
import type { MatchedPair, SpeechCheckFeedback } from '../learning/learning-api';
export type PreviewSpeechFeedback = Pick<SpeechCheckFeedback, 'status' | 'message'>;
export interface StaffCourse {
  archivedAt?: string | null;
  orderNumber?: number;
  description?: string;
  id: string;
  title: string;
  targetLanguageId: number;
  canAuthor: boolean;
  canReview: boolean;
}
export interface StaffUnit {
  archivedAt?: string | null;
  id: string;
  title: string;
  description: string;
  difficulty: string;
  orderNumber: number;
  nextLessonOrder: number;
}
export interface StaffWord {
  id: string;
  word: string;
  englishTranslation: string;
  audioMediaId: string | null;
}
export interface LessonRow {
  id: string;
  title: string;
  versionId: string;
  versionStatus: string;
  versionNumber: number;
}
export interface StaffLesson extends LessonRow {
  orderNumber?: number;
  skills?: readonly StaffSkill[];
  versionPublishedAt?: string | null;
  privateTextErasedAt?: string | null;
  isLatestVersion?: boolean;
  reviews: readonly { reviewerId: string; decision: string; comment: string; reviewedAt: string }[];
  unitId: string;
  ownerId: string;
  currentVersionId: string | null;
  versionCreatedBy: string;
  objective: string;
  explanation: string;
  exercises: readonly StaffExercise[];
}
export interface StaffVersion {
  id: string;
  versionNumber: number;
  status: string;
  publishedAt: string | null;
  privateTextErasedAt: string | null;
}
export interface StaffSkill {
  id: number;
  name: string;
  isArchived?: boolean;
}
export interface StaffExercise {
  skillIds?: readonly number[];
  audioMediaId?: string | null;
  contextText?: string | null;
  evaluationRules?: string;
  id: string;
  type: string;
  prompt: string;
  explanation: string;
  orderNumber: number;
  options: readonly {
    id?: string;
    text: string;
    audioMediaId?: string | null;
    matchKey?: string | null;
    orderNumber?: number;
  }[];
  answers: readonly { answerText: string; orderedTokenIds?: string | null }[];
}
export interface ExerciseDraft {
  skillIds?: readonly number[];
  audioMediaId?: string | null;
  contextText?: string | null;
  matchPairs?: { left: string; right: string; leftAudioMediaId?: string | null }[];
  tokenOrders?: number[][];
  ignoreCase?: boolean;
  ignoreTerminalPunctuation?: boolean;
  type: string;
  prompt: string;
  explanation: string;
  orderNumber: number;
  options: string[];
  answers: string[];
}
export interface UnitDraft {
  courseId: string;
  title: string;
  description: string;
  difficulty: string;
  orderNumber: number;
}
export interface LessonDraft {
  unitId: string;
  title: string;
  objective: string;
  explanation: string;
  orderNumber: number;
}
export interface DraftMetadata {
  versionId: string;
  title: string;
  objective: string;
  explanation: string;
}
@Injectable({ providedIn: 'root' })
export class ContentApi {
  transferOwner(
    id: string,
    email: string,
    expectedOwnerId: string,
    reason: string,
  ): Promise<unknown> {
    return firstValueFrom(
      this.http.post(`/api/v1/manage/lessons/${id}/owner`, {
        email,
        expectedOwnerId,
        reason,
      }),
    );
  }
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/manage';
  courses(): Promise<readonly StaffCourse[]> {
    return firstValueFrom(this.http.get<StaffCourse[]>(this.url + '/courses'));
  }
  checkPreviewSpeech(
    lessonId: string,
    versionId: string,
    exerciseId: string,
    recording: Blob,
    signal: AbortSignal,
  ): Promise<PreviewSpeechFeedback> {
    signal.throwIfAborted();
    const form = new FormData();
    form.append('recording', recording, 'speech.wav');
    const route = `/lessons/${encodeURIComponent(lessonId)}/versions/${encodeURIComponent(versionId)}/exercises/${encodeURIComponent(exerciseId)}/preview-speech`;
    return firstValueFrom(
      this.http
        .post<PreviewSpeechFeedback>(this.url + route, form)
        .pipe(takeUntil(fromEvent(signal, 'abort'))),
    );
  }
  setCourseArchive(courseId: string, archived: boolean, reason: string): Promise<unknown> {
    return firstValueFrom(
      this.http.put(this.url + '/courses/' + encodeURIComponent(courseId) + '/archive', {
        archived,
        reason,
      }),
    );
  }
  editCourse(courseId: string, title: string, description: string): Promise<unknown> {
    return firstValueFrom(
      this.http.put(this.url + '/courses/' + encodeURIComponent(courseId), {
        title,
        description,
      }),
    );
  }
  moveCourse(courseId: string, fromPosition: number, toPosition: number): Promise<unknown> {
    return firstValueFrom(
      this.http.put(this.url + '/courses/' + encodeURIComponent(courseId) + '/position', {
        fromPosition,
        toPosition,
      }),
    );
  }
  setUnitArchive(unitId: string, archived: boolean, reason: string): Promise<unknown> {
    return firstValueFrom(
      this.http.put(this.url + '/units/' + encodeURIComponent(unitId) + '/archive', {
        archived,
        reason,
      }),
    );
  }
  words(courseId: string, page: number): Promise<{ items: StaffWord[]; total: number }> {
    return firstValueFrom(
      this.http.get<{ items: StaffWord[]; total: number }>(this.url + '/vocabulary', {
        params: { courseId, page },
      }),
    );
  }
  attachWordAudio(word: StaffWord, audioMediaId: string | null): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(this.url + '/vocabulary/' + encodeURIComponent(word.id) + '/audio', {
        audioMediaId,
        previousAudioMediaId: word.audioMediaId,
      }),
    );
  }
  units(courseId: string): Promise<readonly StaffUnit[]> {
    return firstValueFrom(
      this.http.get<StaffUnit[]>(this.url + '/units', { params: { courseId } }),
    );
  }
  lessons(courseId: string, page: number): Promise<{ total: number; items: readonly LessonRow[] }> {
    return firstValueFrom(
      this.http.get<{ total: number; items: LessonRow[] }>(this.url + '/lessons', {
        params: { courseId, page },
      }),
    );
  }
  lesson(id: string, versionId?: string): Promise<StaffLesson> {
    return firstValueFrom(
      this.http.get<StaffLesson>(this.url + '/lessons/' + encodeURIComponent(id), {
        params: versionId ? { versionId } : {},
      }),
    );
  }
  versions(id: string, page: number): Promise<StaffVersion[]> {
    return firstValueFrom(
      this.http.get<StaffVersion[]>(this.url + '/lessons/' + encodeURIComponent(id) + '/versions', {
        params: { page },
      }),
    );
  }
  erasePrivateDraft(id: string, versionId: string): Promise<unknown> {
    return firstValueFrom(
      this.http.delete(
        this.url +
          '/lessons/' +
          encodeURIComponent(id) +
          '/versions/' +
          encodeURIComponent(versionId) +
          '/private-text',
      ),
    );
  }
  createUnit(body: UnitDraft): Promise<{ id: string }> {
    return firstValueFrom(this.http.post<{ id: string }>(this.url + '/units', body));
  }
  createLesson(body: LessonDraft): Promise<{ id: string }> {
    return firstValueFrom(this.http.post<{ id: string }>(this.url + '/lessons', body));
  }
  edit(id: string, body: DraftMetadata): Promise<unknown> {
    return firstValueFrom(this.http.put(this.url + '/lessons/' + encodeURIComponent(id), body));
  }
  editUnit(id: string, body: Omit<UnitDraft, 'courseId'>): Promise<{ id: string }> {
    return firstValueFrom(
      this.http.put<{ id: string }>(this.url + '/units/' + encodeURIComponent(id), body),
    );
  }
  moveUnit(id: string, fromPosition: number, toPosition: number): Promise<unknown> {
    return firstValueFrom(
      this.http.put(this.url + '/units/' + encodeURIComponent(id) + '/position', {
        fromPosition,
        toPosition,
      }),
    );
  }
  addExercise(id: string, versionId: string, body: ExerciseDraft): Promise<unknown> {
    return firstValueFrom(
      this.http.post(this.url + '/lessons/' + encodeURIComponent(id) + '/exercises', {
        ...body,
        versionId,
      }),
    );
  }
  editExercise(
    id: string,
    exerciseId: string,
    versionId: string,
    body: ExerciseDraft,
  ): Promise<unknown> {
    return firstValueFrom(
      this.http.put(
        this.url +
          '/lessons/' +
          encodeURIComponent(id) +
          '/exercises/' +
          encodeURIComponent(exerciseId),
        { ...body, versionId },
      ),
    );
  }
  moveLesson(id: string, fromPosition: number, toPosition: number): Promise<unknown> {
    return firstValueFrom(
      this.http.put(this.url + '/lessons/' + encodeURIComponent(id) + '/position', {
        fromPosition,
        toPosition,
      }),
    );
  }
  moveExercise(
    id: string,
    exerciseId: string,
    versionId: string,
    neighborId: string,
    earlier: boolean,
  ): Promise<unknown> {
    return firstValueFrom(
      this.http.post(
        this.url +
          '/lessons/' +
          encodeURIComponent(id) +
          '/exercises/' +
          encodeURIComponent(exerciseId) +
          '/move',
        { versionId, neighborId, earlier },
      ),
    );
  }
  removeExercise(id: string, exerciseId: string, versionId: string): Promise<unknown> {
    return firstValueFrom(
      this.http.delete(
        this.url +
          '/lessons/' +
          encodeURIComponent(id) +
          '/exercises/' +
          encodeURIComponent(exerciseId),
        { body: { versionId } },
      ),
    );
  }
  submit(id: string, versionId: string): Promise<unknown> {
    return firstValueFrom(
      this.http.post(this.url + '/lessons/' + encodeURIComponent(id) + '/submit', { versionId }),
    );
  }
  publish(id: string, versionId: string): Promise<unknown> {
    return firstValueFrom(
      this.http.post(this.url + '/lessons/' + encodeURIComponent(id) + '/publish', { versionId }),
    );
  }
  archive(id: string, versionId: string, reason: string): Promise<unknown> {
    return firstValueFrom(
      this.http.delete(this.url + '/lessons/' + encodeURIComponent(id), {
        body: { versionId, reason },
      }),
    );
  }
  review(
    id: string,
    versionId: string,
    decision: 'Approved' | 'ChangesRequested',
    comment: string,
  ): Promise<unknown> {
    return firstValueFrom(
      this.http.post(this.url + '/lessons/' + encodeURIComponent(id) + '/reviews', {
        versionId,
        decision,
        comment,
      }),
    );
  }
  checkPreview(
    id: string,
    versionId: string,
    exerciseId: string,
    answer: string,
    orderedTokenIds: readonly string[] = [],
    matchedPairs: readonly MatchedPair[] = [],
  ): Promise<{ correct: boolean; explanation: string }> {
    return firstValueFrom(
      this.http.post<{ correct: boolean; explanation: string }>(
        this.url + '/lessons/' + encodeURIComponent(id) + '/preview/check',
        { versionId, exerciseId, answer, orderedTokenIds, matchedPairs },
      ),
    );
  }
  duplicate(id: string, versionId: string, title: string): Promise<{ id: string }> {
    return firstValueFrom(
      this.http.post<{ id: string }>(
        this.url + '/lessons/' + encodeURIComponent(id) + '/duplicate',
        {
          versionId,
          title,
        },
      ),
    );
  }
  revision(id: string): Promise<unknown> {
    return firstValueFrom(
      this.http.post(this.url + '/lessons/' + encodeURIComponent(id) + '/versions', {}),
    );
  }
}
