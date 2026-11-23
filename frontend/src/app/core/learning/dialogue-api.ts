import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface DialogueListing {
  id: string;
  title: string;
  scenario: string;
  versionNumber: number;
  resumeSessionId?: string | null;
  requiresPremium?: boolean;
  locked?: boolean;
  hasCompleted?: boolean;
}

export interface DialogueStep {
  prompt: string;
  response: string;
  feedback: string;
  isCorrect: boolean;
}

export interface DialogueView {
  awardedXp?: number;
  rewardXp?: number;
  languageCode?: string;
  id: string;
  dialogueId: string;
  title: string;
  current: {
    id: string;
    text: string;
    isTerminal: boolean;
    choices: readonly { id: string; text: string }[];
  };
  completedAt: string | null;
  path: readonly DialogueStep[];
  reviewPhrases?: readonly { text: string; tip: string }[];
}

@Injectable({ providedIn: 'root' })
export class DialogueApi {
  private readonly http = inject(HttpClient);

  list(courseId: string): Promise<DialogueListing[]> {
    return firstValueFrom(
      this.http.get<DialogueListing[]>('/api/v1/dialogues', { params: { courseId } }),
    );
  }

  start(id: string): Promise<DialogueView> {
    return firstValueFrom(
      this.http.post<DialogueView>(`/api/v1/dialogues/${encodeURIComponent(id)}/sessions`, {}),
    );
  }

  read(id: string): Promise<DialogueView> {
    return firstValueFrom(this.http.get<DialogueView>(this.sessionUrl(id)));
  }

  choose(id: string, choiceId: string, idempotencyKey: string): Promise<DialogueView> {
    return firstValueFrom(
      this.http.post<DialogueView>(this.sessionUrl(id) + '/choices', { choiceId, idempotencyKey }),
    );
  }

  private sessionUrl(id: string): string {
    return `/api/v1/me/dialogue-sessions/${encodeURIComponent(id)}`;
  }
}
