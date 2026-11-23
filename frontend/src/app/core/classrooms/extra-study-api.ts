import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface ExtraStudyTeacher {
  id: string;
  displayName: string;
}

export interface ExtraStudyRequest {
  id: string;
  offerId: string;
  courseTitle: string;
  displayName: string;
  incoming: boolean;
  status: 'Pending' | 'Accepted' | 'Declined' | 'Closed';
}

export interface SharedStudyLesson {
  id: string;
  title: string;
}

export interface ExtraStudySpaceView {
  courseId: string;
  courseTitle: string;
  canShare: boolean;
  lessons: SharedStudyLesson[];
}

interface ExtraStudyAvailability {
  courseId: string;
  enabled: boolean;
}

@Injectable({ providedIn: 'root' })
export class ExtraStudyApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/extra-study';

  close(id: string): Promise<unknown> {
    return firstValueFrom(this.http.delete(this.base + '/requests/' + encodeURIComponent(id)));
  }

  space(requestId: string): Promise<ExtraStudySpaceView> {
    return firstValueFrom(
      this.http.get<ExtraStudySpaceView>(
        this.base + '/requests/' + encodeURIComponent(requestId) + '/space',
      ),
    );
  }

  share(requestId: string, lessonId: string): Promise<unknown> {
    return firstValueFrom(
      this.http.put(
        this.base +
          '/requests/' +
          encodeURIComponent(requestId) +
          '/space/lessons/' +
          encodeURIComponent(lessonId),
        null,
      ),
    );
  }

  requests(after?: string): Promise<ExtraStudyRequest[]> {
    return firstValueFrom(
      this.http.get<ExtraStudyRequest[]>(this.base + '/requests', {
        params: after ? { after } : {},
      }),
    );
  }

  request(offerId: string): Promise<unknown> {
    return firstValueFrom(
      this.http.post(this.base + '/offers/' + encodeURIComponent(offerId) + '/requests', null),
    );
  }

  decide(id: string, accepted: boolean): Promise<unknown> {
    return firstValueFrom(
      this.http.put(this.base + '/requests/' + encodeURIComponent(id) + '/decision', { accepted }),
    );
  }

  teachers(courseId: string, after?: string): Promise<ExtraStudyTeacher[]> {
    return firstValueFrom(
      this.http.get<ExtraStudyTeacher[]>(
        this.base + '/courses/' + encodeURIComponent(courseId) + '/teachers',
        { params: after ? { after } : {} },
      ),
    );
  }

  availability(): Promise<ExtraStudyAvailability[]> {
    return firstValueFrom(this.http.get<ExtraStudyAvailability[]>(this.base + '/availability'));
  }

  setAvailability(courseId: string, enabled: boolean): Promise<void> {
    return firstValueFrom(
      this.http.put<void>(this.base + '/availability/' + encodeURIComponent(courseId), { enabled }),
    );
  }
}
