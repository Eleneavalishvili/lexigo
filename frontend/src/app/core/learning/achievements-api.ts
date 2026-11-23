import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface AchievementView {
  id: string;
  code: string;
  title: string;
  ruleType: string;
  threshold: number;
  progress: number;
  earnedAt: string | null;
  canCollect: boolean;
}

@Injectable({ providedIn: 'root' })
export class AchievementsApi {
  private readonly http = inject(HttpClient);
  get(): Promise<readonly AchievementView[]> {
    return firstValueFrom(this.http.get<AchievementView[]>('/api/v1/me/achievements'));
  }
  collect(): Promise<readonly AchievementView[]> {
    return firstValueFrom(this.http.post<AchievementView[]>('/api/v1/me/achievements/collect', {}));
  }
}
