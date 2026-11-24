import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface DialogueChoiceDraft {
  text: string;
  feedback: string;
  isCorrect: boolean;
  nextNodeKey: string | null;
}
export interface DialogueNodeDraft {
  audioMediaId?: string | null;
  key: string;
  text: string;
  isStart: boolean;
  isTerminal: boolean;
  choices: DialogueChoiceDraft[];
}
export interface DialogueDraftRow {
  id: string;
  courseId: string;
  title: string;
  scenario: string;
  versionNumber: number;
  status: string;
  requiresPremium: boolean;
  ownerId: string | null;
  editToken: string;
}
export interface DialogueDraftDetail {
  dialogue: DialogueDraftRow;
  nodes: DialogueNodeDraft[];
  reviewerId: string | null;
  reviewedAt: string | null;
  reviewComment: string | null;
}
export interface DialogueDraftPage {
  items: DialogueDraftRow[];
  page: number;
  pageSize: number;
  totalCount: number;
}
@Injectable({ providedIn: 'root' })
export class DialogueAuthoringApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/manage/dialogues';
  list(courseId: string, page: number): Promise<DialogueDraftPage> {
    return firstValueFrom(
      this.http.get<DialogueDraftPage>(this.url, { params: { courseId, page } }),
    );
  }
  detail(id: string): Promise<DialogueDraftDetail> {
    return firstValueFrom(
      this.http.get<DialogueDraftDetail>(this.url + '/' + encodeURIComponent(id)),
    );
  }
  create(
    courseId: string,
    title: string,
    scenario: string,
    requiresPremium: boolean,
  ): Promise<{ id: string }> {
    return firstValueFrom(
      this.http.post<{ id: string }>(this.url, { courseId, title, scenario, requiresPremium }),
    );
  }
  save(
    id: string,
    editToken: string,
    title: string,
    requiresPremium: boolean,
    nodes: DialogueNodeDraft[],
  ): Promise<unknown> {
    return firstValueFrom(
      this.http.put(this.url + '/' + encodeURIComponent(id), {
        editToken,
        title,
        requiresPremium,
        nodes,
      }),
    );
  }
  transition(id: string, operation: string, editToken: string, comment: string): Promise<unknown> {
    const url = this.url + '/' + encodeURIComponent(id);
    const body = { editToken, comment };
    return firstValueFrom(
      operation === 'archive'
        ? this.http.delete(url, { body })
        : this.http.post(url + '/' + operation, body),
    );
  }
  revision(id: string): Promise<{ id: string }> {
    return firstValueFrom(
      this.http.post<{ id: string }>(this.url + '/' + encodeURIComponent(id) + '/versions', {}),
    );
  }
}
