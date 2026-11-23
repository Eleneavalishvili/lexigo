import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ClassPage } from './classroom-api';

export interface AssignmentSummary {
  id: string;
  title: string;
  dueAt: string;
  minimumScore: number;
}

export interface AssignedLesson {
  id: string;
  title: string;
  isPublished: boolean;
}

export interface AssignmentDetail extends AssignmentSummary {
  classroomId: string;
  revision: number;
  lessons: readonly AssignedLesson[];
  learnerWork: {
    available: readonly AssignmentAttempt[];
    submitted: readonly SubmittedAssignmentAttempt[];
    feedback: readonly LearnerFeedback[];
  } | null;
}

export interface AssignmentAttempt {
  lessonId: string;
  sessionId: string;
  score: number;
  completedAt: string;
}

export interface SubmittedAssignmentAttempt {
  lessonId: string;
  score: number;
  submittedAt: string;
  isLate: boolean;
}

export interface CreateAssignment {
  title: string;
  dueAt: string;
  minimumScore: number;
  lessonIds: readonly string[];
  requestKey: string;
}

export type EditAssignment = Omit<CreateAssignment, 'requestKey'> & { revision: number };

export interface AssignmentLessonResult {
  lessonId: string;
  title: string;
  attemptCount: number;
  bestScore: number;
  latestSubmissionId: string;
  latestScore: number;
  latestAccuracy: number;
  submittedAt: string;
  isLate: boolean;
  feedback: AssignmentFeedback | null;
}

export interface AssignmentFeedback {
  message: string;
  createdAt: string;
}

export interface LearnerFeedback extends AssignmentFeedback {
  lessonId: string;
  score: number;
}

export interface AssignmentLearnerResult {
  learnerId: string;
  displayName: string;
  membershipStatus: 'Active' | 'Left' | 'Removed';
  status: 'NotSubmitted' | 'InProgress' | 'Completed';
  lessons: readonly AssignmentLessonResult[];
}

export interface AssignmentResultsPage {
  minimumScore: number;
  requiredLessons: number;
  learners: ClassPage<AssignmentLearnerResult>;
  isArchived: boolean;
}

@Injectable({ providedIn: 'root' })
export class AssignmentApi {
  private readonly http = inject(HttpClient);

  list(classId: string, page: number): Promise<ClassPage<AssignmentSummary>> {
    return firstValueFrom(
      this.http.get<ClassPage<AssignmentSummary>>(`/api/v1/classes/${classId}/assignments`, {
        params: { page },
      }),
    );
  }

  detail(id: string): Promise<AssignmentDetail> {
    return firstValueFrom(this.http.get<AssignmentDetail>(`/api/v1/assignments/${id}`));
  }

  create(classId: string, input: CreateAssignment): Promise<{ id: string }> {
    return firstValueFrom(
      this.http.post<{ id: string }>(`/api/v1/classes/${classId}/assignments`, input),
    );
  }

  edit(id: string, input: EditAssignment): Promise<{ id: string }> {
    return firstValueFrom(this.http.patch<{ id: string }>(`/api/v1/assignments/${id}`, input));
  }

  submit(id: string, sessionId: string): Promise<unknown> {
    return firstValueFrom(this.http.post(`/api/v1/assignments/${id}/submissions`, { sessionId }));
  }

  results(id: string, page: number): Promise<AssignmentResultsPage> {
    return firstValueFrom(
      this.http.get<AssignmentResultsPage>(`/api/v1/assignments/${id}/results`, {
        params: { page },
      }),
    );
  }

  postFeedback(id: string, submissionId: string, message: string): Promise<{ id: string }> {
    return firstValueFrom(
      this.http.post<{ id: string }>(`/api/v1/assignments/${id}/feedback`, {
        submissionId,
        message,
      }),
    );
  }
}
