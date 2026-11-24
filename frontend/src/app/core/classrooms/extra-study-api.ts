import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface ExtraStudyTeacher {
  id: string;
  displayName: string;
  pricePerLesson?: number;
  levelFrom?: string;
  levelTo?: string;
  biography?: string;
  portraitKey?: string;
  courseTitle?: string;
  courseId?: string;
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
  scheduledAt?: string | null;
  durationMinutes?: number | null;
  status?: string;
  bestScore?: number | null;
  completedAt?: string | null;
}

export interface ExtraStudySpaceView {
  courseId: string;
  courseTitle: string;
  canShare: boolean;
  lessons: SharedStudyLesson[];
  partnerName?: string;
}

export interface StudyMessage {
  id: string;
  senderName: string;
  body: string;
  createdAt: string;
  isMine: boolean;
}
export interface StudyMessagePage {
  items: StudyMessage[];
  nextBefore: string | null;
}

interface ExtraStudyAvailability {
  courseId: string;
  enabled: boolean;
}

@Injectable({ providedIn: 'root' })
export class ExtraStudyApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/v1/extra-study';

  messages(requestId: string, before?: string): Promise<StudyMessagePage> {
    return firstValueFrom(
      this.http.get<StudyMessagePage>(
        `${this.base}/requests/${encodeURIComponent(requestId)}/messages`,
        { params: before ? { before } : {} },
      ),
    );
  }
  sendMessage(requestId: string, message: { id: string; body: string }): Promise<unknown> {
    return firstValueFrom(
      this.http.post(`${this.base}/requests/${encodeURIComponent(requestId)}/messages`, message),
    );
  }

  schedule(
    requestId: string,
    lessonId: string,
    scheduledAt: string | null,
    durationMinutes: number | null,
  ): Promise<unknown> {
    return firstValueFrom(
      this.http.put(
        `${this.base}/requests/${encodeURIComponent(requestId)}/space/lessons/${encodeURIComponent(lessonId)}/schedule`,
        { scheduledAt, durationMinutes },
      ),
    );
  }

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
        courseId
          ? this.base + '/courses/' + encodeURIComponent(courseId) + '/teachers'
          : this.base + '/teachers',
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
