import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom, fromEvent, takeUntil } from 'rxjs';

export interface LearningOption {
  audio?: AudioInformation | null;
  id: string;
  text: string;
  side?: 'left' | 'right' | null;
}
export interface AudioInformation {
  source: string;
  license: string;
  durationMilliseconds: number;
}
export interface ExerciseView {
  audio?: AudioInformation | null;
  id: string;
  type:
    | 'MultipleChoice'
    | 'Translation'
    | 'FillBlank'
    | 'SentenceBuilder'
    | 'Matching'
    | 'Reading'
    | 'DialogueChoice'
    | 'Alphabet'
    | 'Listening'
    | 'Speaking'
    | 'SpeakingCheck';
  prompt: string;
  contextText?: string | null;
  options: readonly LearningOption[];
}
export function isChoiceExercise(exercise: Pick<ExerciseView, 'type' | 'options'>): boolean {
  return (
    ['MultipleChoice', 'Reading', 'DialogueChoice', 'Alphabet'].includes(exercise.type) ||
    (exercise.type === 'Listening' && exercise.options.length > 0)
  );
}
export interface AnswerFeedback {
  correctAnswer?: string | null;
  prompt?: string | null;
  isGraded?: boolean;
  isCorrect: boolean;
  feedback: string;
  heartsRemaining: number;
  nextExerciseId: string | null;
}
export interface SpeechCheckFeedback {
  status: 'Matched' | 'Mismatch' | 'NoSpeech';
  message: string;
  heartsRemaining: number;
  freeRetryAvailable: boolean;
  answer: AnswerFeedback | null;
}
export interface MatchedPair {
  leftOptionId: string;
  rightOptionId: string;
}
export interface AnswerInput {
  practiceCompleted?: boolean;
  sessionExerciseId: string;
  answerText: string | null;
  selectedOptionIds: readonly string[];
  idempotencyKey: string;
  orderedTokenIds?: readonly string[];
  matchedPairs?: readonly MatchedPair[];
}
export interface SessionView {
  practiceSkillId?: number | null;
  answeredExercise?: ExerciseView | null;
  isPlacement?: boolean;
  unlimitedHearts?: boolean;
  speakingMissCount?: number;
  id: string;
  courseId: string;
  lessonId: string;
  versionId: string;
  title: string;
  explanation: string;
  mode: 'Lesson' | 'Practice';
  hearts: number;
  startedAt: string;
  completedAt: string | null;
  score: number;
  passScore: number;
  passed: boolean;
  awardedXp: number;
  answeredCount: number;
  correctCount: number;
  totalCount: number;
  accuracy: number;
  currentExercise: ExerciseView | null;
  lastFeedback: { exerciseId: string; feedback: AnswerFeedback } | null;
  gradedExerciseCount?: number;
  gradedAnsweredCount?: number;
  selfAssessmentCount?: number;
  results: readonly {
    correctAnswer?: string | null;
    options?: readonly LearningOption[] | null;
    isGraded?: boolean;
    prompt: string;
    isCorrect: boolean;
    feedback: string;
    contextText?: string | null;
    id?: string | null;
    audio?: AudioInformation | null;
  }[];
}
export interface LessonMapItem {
  hasGradedScore?: boolean;
  id: string;
  unitId: string;
  title: string;
  objective: string;
  orderNumber: number;
  status: 'NotStarted' | 'InProgress' | 'Completed';
  bestScore: number;
  isUnlocked: boolean;
  missingPrerequisites: readonly string[];
  activeSessionId: string | null;
}
export interface CourseProgress {
  recommendedUnitId?: string | null;
  startingLevel?: string | null;
  courseId: string;
  title: string;
  isEnrolled: boolean;
  units: readonly { id: string; title: string; description: string; orderNumber: number }[];
  lessons: readonly LessonMapItem[];
  openSessions: readonly { id: string; lessonId: string; title: string; mode: string }[];
}
export interface EnrollmentPage {
  items: readonly {
    courseId: string;
    title: string;
    isPublished: boolean;
    enrolledAt: string;
    placementLevel: string | null;
  }[];
  page: number;
  pageSize: number;
  totalCount: number;
  accuracy: number;
}
@Injectable({ providedIn: 'root' })
export class LearningApi {
  dashboard(courseId?: string | null): Promise<LearningDashboard> {
    return firstValueFrom(
      this.http.get<LearningDashboard>('/api/v1/me/dashboard', {
        params: courseId ? { courseId } : {},
      }),
    );
  }
  private readonly http = inject(HttpClient);
  mistakes(courseId: string, page = 1): Promise<MistakePage> {
    return firstValueFrom(
      this.http.get<MistakePage>('/api/v1/me/mistakes', {
        params: { courseId, includeDue: true, page },
      }),
    );
  }
  practiceMistakes(
    courseId: string,
    skillId?: number,
    focus?: PracticeFocus,
  ): Promise<SessionView> {
    return firstValueFrom(
      this.http.post<SessionView>('/api/v1/me/practice-sessions', {
        courseId,
        mistakesOnly: true,
        skillId,
        focus,
      }),
    );
  }
  placement(courseId: string): Promise<SessionView> {
    return firstValueFrom(
      this.http.post<SessionView>(
        '/api/v1/courses/' + encodeURIComponent(courseId) + '/placement-sessions',
        {},
      ),
    );
  }
  placementResult(id: string): Promise<PlacementResult> {
    return firstValueFrom(
      this.http.get<PlacementResult>('/api/v1/me/placement-sessions/' + encodeURIComponent(id)),
    );
  }
  applyPlacement(id: string): Promise<PlacementResult> {
    return firstValueFrom(
      this.http.post<PlacementResult>(
        '/api/v1/me/placement-sessions/' + encodeURIComponent(id) + '/apply',
        {},
      ),
    );
  }
  enrollments(page = 1): Promise<EnrollmentPage> {
    return firstValueFrom(
      this.http.get<EnrollmentPage>('/api/v1/me/enrollments', { params: { page } }),
    );
  }
  enroll(courseId: string, startingLevel?: string): Promise<unknown> {
    return firstValueFrom(this.http.post('/api/v1/me/enrollments', { courseId, startingLevel }));
  }
  progress(courseId: string): Promise<CourseProgress> {
    return firstValueFrom(
      this.http.get<CourseProgress>('/api/v1/me/progress', { params: { courseId } }),
    );
  }
  start(lessonId: string): Promise<SessionView> {
    return firstValueFrom(
      this.http.post<SessionView>(
        '/api/v1/lessons/' + encodeURIComponent(lessonId) + '/sessions',
        {},
      ),
    );
  }
  practice(lessonId: string): Promise<SessionView> {
    return firstValueFrom(
      this.http.post<SessionView>('/api/v1/me/practice-sessions', { lessonId }),
    );
  }
  session(id: string): Promise<SessionView> {
    return firstValueFrom(
      this.http.get<SessionView>('/api/v1/me/learning-sessions/' + encodeURIComponent(id)),
    );
  }
  answer(id: string, value: AnswerInput): Promise<AnswerFeedback> {
    return firstValueFrom(
      this.http.post<AnswerFeedback>(
        '/api/v1/me/learning-sessions/' + encodeURIComponent(id) + '/answers',
        value,
      ),
    );
  }
  checkSpeech(
    id: string,
    exerciseId: string,
    key: string,
    recording: Blob,
    signal: AbortSignal,
  ): Promise<SpeechCheckFeedback> {
    signal.throwIfAborted();
    const form = new FormData();
    form.append('ExerciseId', exerciseId);
    form.append('IdempotencyKey', key);
    form.append('Recording', recording, 'speech.wav');
    return firstValueFrom(
      this.http
        .post<SpeechCheckFeedback>(
          '/api/v1/me/learning-sessions/' + encodeURIComponent(id) + '/speech-checks',
          form,
        )
        .pipe(takeUntil(fromEvent(signal, 'abort'))),
    );
  }
  audio(
    sessionId: string,
    exerciseId: string,
    optionId?: string | null,
  ): Promise<{ url: string; expiresInSeconds: number }> {
    return firstValueFrom(
      this.http.get<{ url: string; expiresInSeconds: number }>(
        '/api/v1/me/learning-sessions/' +
          encodeURIComponent(sessionId) +
          '/exercises/' +
          encodeURIComponent(exerciseId) +
          (optionId ? '/options/' + encodeURIComponent(optionId) : '') +
          '/audio',
      ),
    );
  }
  complete(id: string): Promise<SessionView> {
    return firstValueFrom(
      this.http.post<SessionView>(
        '/api/v1/me/learning-sessions/' + encodeURIComponent(id) + '/complete',
        {},
      ),
    );
  }
}

export interface PlacementResult {
  sessionId: string;
  courseId: string;
  completed: boolean;
  applied: boolean;
  recommendedUnitId: string | null;
  recommendedTitle: string | null;
  units: readonly { id: string; title: string; correct: number; total: number }[];
}

export type PracticeFocus = 'repeated' | 'due';

export interface MistakePage {
  advancedPracticeAvailable?: boolean;
  pageSize?: number;
  items: readonly {
    exerciseId: string;
    prompt: string;
    type: string;
    contextText: string | null;
    wrongCount: number;
    lastMissedAt: string | null;
    lastAttemptAt?: string;
    skills?: readonly string[];
    dueAt?: string | null;
  }[];
  totalCount: number;
  page: number;
}

export interface LearningDashboard {
  totalXp: number;
  todayXp: number;
  dailyGoalXp: number;
  goalReached: boolean;
  currentStreak: number;
  completedLessons: number;
  localDate: string;
  timeZone: string;
  hearts: {
    count: number;
    maximum: number;
    refillAt: string | null;
    serverNow: string;
    unlimited: boolean;
  };
  course: {
    id: string;
    title: string;
    languageCode: string;
    earnedXp: number;
    completedLessons: number;
  } | null;
}
