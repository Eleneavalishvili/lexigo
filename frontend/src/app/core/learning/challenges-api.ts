import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface ChallengeView {
  id: string;
  title: string;
  ruleType: string;
  target: number;
  rewardXp: number;
  startsAt: string;
  endsAt: string;
  progress: number;
  completedAt: string | null;
  rewardedAt: string | null;
  rewardCourseId: string | null;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class ChallengesApi {
  private readonly http = inject(HttpClient);
  get(): Promise<readonly ChallengeView[]> {
    return firstValueFrom(this.http.get<ChallengeView[]>('/api/v1/me/challenges'));
  }
}
