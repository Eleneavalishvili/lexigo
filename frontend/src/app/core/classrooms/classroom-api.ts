import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface ClassPage<T> {
  items: readonly T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
export interface ClassroomView {
  id: string;
  courseId: string;
  courseTitle: string;
  name: string;
  description: string;
  teacherName: string;
  isArchived: boolean;
  revision: number;
  isTeacher: boolean;
  canLeave: boolean;
  coursePublished: boolean;
}
export interface ClassMember {
  userId: string;
  displayName: string;
  joinedAt: string;
  status: 'Active' | 'Left' | 'Removed';
}
export interface InvitationStatus {
  expiresAt: string;
  maxUses: number;
  usedCount: number;
}
export interface ClassroomDetail {
  classroom: ClassroomView;
  members: ClassPage<ClassMember> | null;
  invitation: InvitationStatus | null;
}
export interface InvitationCreated {
  code: string;
  expiresAt: string;
  maxUses: number;
}
export interface CreateClassroom {
  courseId: string;
  name: string;
  description: string;
  requestKey: string;
}
export interface EditClassroom {
  name: string;
  description: string;
  isArchived: boolean;
  revision: number;
}

@Injectable({ providedIn: 'root' })
export class ClassroomApi {
  offerTransfer(id: string, email: string): Promise<{ token: string; expiresAt: string }> {
    return firstValueFrom(
      this.http.post<{ token: string; expiresAt: string }>(`/api/v1/classes/${id}/transfer`, {
        email,
      }),
    );
  }
  previewTransfer(token: string): Promise<{ className: string; expiresAt: string }> {
    return firstValueFrom(
      this.http.post<{ className: string; expiresAt: string }>(
        '/api/v1/classes/transfers/preview',
        { token },
      ),
    );
  }
  acceptTransfer(token: string): Promise<{ id: string }> {
    return firstValueFrom(
      this.http.post<{ id: string }>('/api/v1/classes/transfers/accept', { token }),
    );
  }
  private readonly http = inject(HttpClient);
  list(page: number): Promise<ClassPage<ClassroomView>> {
    return firstValueFrom(
      this.http.get<ClassPage<ClassroomView>>('/api/v1/classes', { params: { page } }),
    );
  }
  detail(id: string, memberPage: number): Promise<ClassroomDetail> {
    return firstValueFrom(
      this.http.get<ClassroomDetail>('/api/v1/classes/' + id, { params: { memberPage } }),
    );
  }
  create(input: CreateClassroom): Promise<{ id: string }> {
    return firstValueFrom(this.http.post<{ id: string }>('/api/v1/classes', input));
  }
  edit(id: string, input: EditClassroom): Promise<{ id: string }> {
    return firstValueFrom(this.http.patch<{ id: string }>('/api/v1/classes/' + id, input));
  }
  join(code: string): Promise<{ id: string }> {
    return firstValueFrom(this.http.post<{ id: string }>('/api/v1/classes/join', { code }));
  }
  invite(id: string, days: number, maxUses: number): Promise<InvitationCreated> {
    return firstValueFrom(
      this.http.post<InvitationCreated>('/api/v1/classes/' + id + '/invitations', {
        days,
        maxUses,
      }),
    );
  }
  remove(id: string, userId: string): Promise<{ id: string }> {
    return firstValueFrom(
      this.http.delete<{ id: string }>('/api/v1/classes/' + id + '/members/' + userId),
    );
  }
  restore(id: string, userId: string): Promise<{ id: string }> {
    return firstValueFrom(
      this.http.patch<{ id: string }>('/api/v1/classes/' + id + '/members/' + userId, {
        status: 'Active',
      }),
    );
  }
}
