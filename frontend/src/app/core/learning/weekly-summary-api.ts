import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface WeeklyCourseSummary {
  courseId: string;
  title: string;
  languageCode: string;
  isPublished: boolean;
  earnedXp: number;
  newLessonsCompleted: number;
  reviewedWords: number;
  conversationsFinished?: number;
  gradedAnswers: number;
  correctAnswers: number;
  accuracy: number | null;
  dueWords: number;
}
export interface WeeklySummary {
  weekStart: string;
  weekEnd: string;
  timeZone: string;
  today: string;
  weeksAgo: number;
  earnedXp: number;
  newLessonsCompleted: number;
  reviewedWords: number;
  conversationsFinished?: number;
  studyDays: number;
  gradedAnswers: number;
  correctAnswers: number;
  accuracy: number | null;
  days: { date: string; earnedXp: number; studied: boolean }[];
  courses: WeeklyCourseSummary[];
}

type ComparisonWeek = Pick<
  WeeklySummary,
  'weekStart' | 'weeksAgo' | 'earnedXp' | 'newLessonsCompleted' | 'studyDays' | 'accuracy'
>;

export interface WeeklyComparison {
  selected: ComparisonWeek;
  previous: ComparisonWeek;
}

@Injectable({ providedIn: 'root' })
export class WeeklySummaryApi {
  private readonly http = inject(HttpClient);
  read(weeksAgo: number): Promise<WeeklySummary> {
    return firstValueFrom(
      this.http.get<WeeklySummary>('/api/v1/me/weekly-summary', { params: { weeksAgo } }),
    );
  }

  compare(weeksAgo: number): Promise<WeeklyComparison> {
    return firstValueFrom(
      this.http.get<WeeklyComparison>('/api/v1/me/weekly-summary/comparison', {
        params: { weeksAgo },
      }),
    );
  }
}
