import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface LeaderboardEntry {
  rank: number;
  displayName: string;
  xp: number;
  isMe: boolean;
  photoUrl?: string | null;
  reportToken?: string | null;
}
export interface LeaderboardView {
  courseId: string;
  courseTitle: string;
  week: string;
  currentWeek: string;
  optedIn: boolean;
  photoShared?: boolean;
  myXp: number;
  me: LeaderboardEntry | null;
  items: readonly LeaderboardEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class LeaderboardApi {
  private readonly http = inject(HttpClient);
  get(courseId: string, week: string | null, page: number): Promise<LeaderboardView> {
    return firstValueFrom(
      this.http.get<LeaderboardView>('/api/v1/leaderboards', {
        params: { courseId, page, ...(week ? { week } : {}) },
      }),
    );
  }
}
