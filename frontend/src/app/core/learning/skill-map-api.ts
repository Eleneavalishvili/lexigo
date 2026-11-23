import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

export interface SkillMapEntry {
  id: number;
  name: string;
  category: string;
  correctCount: number;
  incorrectCount: number;
  lastPracticedAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class SkillMapApi {
  private readonly http = inject(HttpClient);

  read(courseId: string) {
    return this.http.get<SkillMapEntry[]>(
      `/api/v1/me/courses/${encodeURIComponent(courseId)}/skills`,
    );
  }
}
