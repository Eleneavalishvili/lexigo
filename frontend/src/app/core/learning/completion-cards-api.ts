import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface CompletionCardDetails {
  displayName: string;
  courseTitle: string;
  unitTitle: string;
  issuedAt: string;
  completedAt: string;
  verificationCode: string;
  lessonCount: number;
  gradedLessonCount: number;
  averageBestScore: number | null;
}
export interface OwnedCompletionCard {
  id: string;
  courseId: string;
  unitId: string;
  details: CompletionCardDetails;
  isShared: boolean;
  shareCode: string | null;
  revision: number;
}
export interface CompletionCardPage {
  items: OwnedCompletionCard[];
  page: number;
  pageSize: number;
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class CompletionCardsApi {
  private readonly http = inject(HttpClient);
  private readonly path = '/api/v1/me/completion-cards';
  list(page: number, unitId: string): Promise<CompletionCardPage> {
    let params = new HttpParams().set('page', page);
    if (unitId) params = params.set('unitId', unitId);
    return firstValueFrom(this.http.get<CompletionCardPage>(this.path, { params }));
  }
  get(id: string): Promise<OwnedCompletionCard> {
    return firstValueFrom(
      this.http.get<OwnedCompletionCard>(this.path + '/' + encodeURIComponent(id)),
    );
  }
  claim(unitId: string): Promise<{ id: string }> {
    return firstValueFrom(
      this.http.post<{ id: string }>(
        '/api/v1/me/units/' + encodeURIComponent(unitId) + '/completion-card',
        {},
      ),
    );
  }
  sharing(
    card: OwnedCompletionCard,
    isShared: boolean,
    confirmPublicName: boolean,
  ): Promise<{ id: string }> {
    return firstValueFrom(
      this.http.put<{ id: string }>(this.path + '/' + encodeURIComponent(card.id) + '/sharing', {
        isShared,
        confirmPublicName,
        revision: card.revision,
      }),
    );
  }
  shared(code: string): Promise<CompletionCardDetails> {
    return firstValueFrom(
      this.http.get<CompletionCardDetails>(
        '/api/v1/completion-cards/shared/' + encodeURIComponent(code),
      ),
    );
  }
}
