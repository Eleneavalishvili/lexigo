import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface ManagedSkill {
  id: number;
  courseId: string;
  name: string;
  category: string;
  isArchived: boolean;
  revision: number;
}
export interface SkillPage {
  items: ManagedSkill[];
  page: number;
  pageSize: number;
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class SkillsApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/manage/skills';

  list(courseId: string, page: number, pageSize: number, search: string, includeArchived: boolean) {
    return firstValueFrom(
      this.http.get<SkillPage>(this.url, {
        params: { courseId, page, pageSize, search, includeArchived },
      }),
    );
  }

  create(courseId: string, name: string, category: string) {
    return firstValueFrom(this.http.post<ManagedSkill>(this.url, { courseId, name, category }));
  }

  edit(skill: ManagedSkill, name: string, category: string, reason: string) {
    return firstValueFrom(
      this.http.put<ManagedSkill>(`${this.url}/${skill.id}`, {
        name,
        category,
        reason,
        revision: skill.revision,
      }),
    );
  }

  visibility(skill: ManagedSkill, reason: string) {
    return firstValueFrom(
      this.http.put<ManagedSkill>(`${this.url}/${skill.id}/visibility`, {
        archived: !skill.isArchived,
        reason,
        revision: skill.revision,
      }),
    );
  }
}
