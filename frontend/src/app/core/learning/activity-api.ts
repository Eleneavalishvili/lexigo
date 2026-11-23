import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';

export interface ActivityDay {
  date: string;
  earnedXp: number;
  completedLessons: number;
  goalReached: boolean;
}

export interface ActivityPage {
  items: ActivityDay[];
  page: number;
  pageSize: number;
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class ActivityApi {
  private readonly http = inject(HttpClient);

  list(from: string, to: string, page: number) {
    return this.http.get<ActivityPage>('/api/v1/me/activity', { params: { from, to, page } });
  }
}
