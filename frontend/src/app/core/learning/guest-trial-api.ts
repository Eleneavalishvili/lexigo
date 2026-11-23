import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { LearningOption } from './learning-api';

export interface TrialQuestion {
  id: string;
  prompt: string;
  options: LearningOption[];
}
export interface GuestTrial {
  courseId: string;
  versionId: string;
  title: string;
  explanation: string;
  questions: TrialQuestion[];
}
export interface TrialFeedback {
  isCorrect: boolean;
  correctAnswer: string;
  explanation: string;
}

@Injectable({ providedIn: 'root' })
export class GuestTrialApi {
  private readonly http = inject(HttpClient);
  read(courseId: string): Promise<GuestTrial> {
    return firstValueFrom(this.http.get<GuestTrial>(this.path(courseId)));
  }
  check(
    courseId: string,
    versionId: string,
    questionId: string,
    optionId: string,
  ): Promise<TrialFeedback> {
    return firstValueFrom(
      this.http.post<TrialFeedback>(this.path(courseId) + '/answers', {
        versionId,
        questionId,
        optionId,
      }),
    );
  }
  private path(courseId: string): string {
    return '/api/v1/courses/' + encodeURIComponent(courseId) + '/trial';
  }
}
