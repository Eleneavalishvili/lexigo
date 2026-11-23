import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface CatalogCourse {
  id: string;
  title: string;
  description: string;
  languageCode: string;
  lessonCount: number;
}
export interface CatalogUnit {
  id: string;
  title: string;
  description: string;
  difficulty: string;
  orderNumber: number;
}
export interface CatalogLesson {
  id: string;
  unitId: string;
  versionId: string;
  title: string;
  objective: string;
  explanation: string;
  orderNumber: number;
  exerciseCount: number;
}

@Injectable({ providedIn: 'root' })
export class CatalogApi {
  private readonly http = inject(HttpClient);
  courses(): Promise<readonly CatalogCourse[]> {
    return firstValueFrom(this.http.get<CatalogCourse[]>('/api/v1/courses'));
  }
  units(id: string): Promise<readonly CatalogUnit[]> {
    return firstValueFrom(
      this.http.get<CatalogUnit[]>('/api/v1/courses/' + encodeURIComponent(id) + '/units'),
    );
  }
  lessons(id: string): Promise<readonly CatalogLesson[]> {
    return firstValueFrom(
      this.http.get<CatalogLesson[]>('/api/v1/units/' + encodeURIComponent(id) + '/lessons'),
    );
  }
}
