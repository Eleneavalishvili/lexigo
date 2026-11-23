import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface ManagedReward {
  id: string;
  title: string;
  ruleType: string;
  isArchived: boolean;
  revision: number;
  canEdit: boolean;
}
export interface ManagedAchievement extends ManagedReward {
  code: string;
  threshold: number;
}
export interface ManagedChallenge extends ManagedReward {
  target: number;
  rewardXp: number;
  startsAt: string;
  endsAt: string;
}
export interface CatalogPage<T> {
  items: readonly T[];
  page: number;
  pageSize: number;
  hasMore: boolean;
  totalCount: number;
}
export interface AchievementInput {
  code: string;
  title: string;
  ruleType: string;
  threshold: number;
  reason: string;
}
export interface ChallengeInput {
  title: string;
  ruleType: string;
  target: number;
  rewardXp: number;
  startsAt: string;
  endsAt: string;
  reason: string;
}
export type RewardResource = 'achievements' | 'challenges';

@Injectable({ providedIn: 'root' })
export class RewardCatalogApi {
  private readonly http = inject(HttpClient);
  achievements(page: number, pageSize = 20, search = ''): Promise<CatalogPage<ManagedAchievement>> {
    return firstValueFrom(
      this.http.get<CatalogPage<ManagedAchievement>>('/api/v1/manage/achievements', {
        params: { page, pageSize, search },
      }),
    );
  }
  challenges(page: number, pageSize = 20, search = ''): Promise<CatalogPage<ManagedChallenge>> {
    return firstValueFrom(
      this.http.get<CatalogPage<ManagedChallenge>>('/api/v1/manage/challenges', {
        params: { page, pageSize, search },
      }),
    );
  }
  saveAchievement(
    input: AchievementInput,
    selected: ManagedAchievement | null,
  ): Promise<{ id: string }> {
    const path = '/api/v1/manage/achievements';
    return firstValueFrom(
      selected
        ? this.http.put<{ id: string }>(path + '/' + selected.id, {
            revision: selected.revision,
            input,
          })
        : this.http.post<{ id: string }>(path, input),
    );
  }
  saveChallenge(input: ChallengeInput, selected: ManagedChallenge | null): Promise<{ id: string }> {
    const path = '/api/v1/manage/challenges';
    return firstValueFrom(
      selected
        ? this.http.put<{ id: string }>(path + '/' + selected.id, {
            revision: selected.revision,
            input,
          })
        : this.http.post<{ id: string }>(path, input),
    );
  }
  archive(
    resource: RewardResource,
    selected: ManagedReward,
    reason: string,
  ): Promise<{ id: string }> {
    return firstValueFrom(
      this.http.delete<{ id: string }>('/api/v1/manage/' + resource + '/' + selected.id, {
        body: { revision: selected.revision, reason },
      }),
    );
  }
}
