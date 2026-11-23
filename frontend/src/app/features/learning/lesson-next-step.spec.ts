import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { CourseProgress, LearningApi, SessionView } from '../../core/learning/learning-api';
import { LessonNextStep } from './lesson-next-step';

describe('Next step after a lesson', () => {
  const user = signal<{ id: string } | null>({ id: 'learner' });
  const api = { progress: vi.fn(), start: vi.fn(), practice: vi.fn(), practiceMistakes: vi.fn() };
  const session: SessionView = {
    id: 'finished',
    courseId: 'course',
    lessonId: 'current',
    versionId: 'version',
    title: 'Greetings',
    explanation: '',
    mode: 'Lesson',
    hearts: 5,
    startedAt: '2026-11-06T10:00:00Z',
    completedAt: '2026-11-06T10:02:00Z',
    score: 100,
    passScore: 80,
    passed: true,
    awardedXp: 20,
    answeredCount: 1,
    correctCount: 1,
    totalCount: 1,
    accuracy: 100,
    currentExercise: null,
    lastFeedback: null,
    results: [],
  };
  const next = {
    id: 'next',
    unitId: 'first-unit',
    title: 'Your next words',
    objective: 'Learn more',
    orderNumber: 3,
    status: 'NotStarted' as const,
    bestScore: 0,
    isUnlocked: true,
    missingPrerequisites: [],
    activeSessionId: null,
  };
  const progress: CourseProgress = {
    courseId: 'course',
    title: 'Spanish',
    isEnrolled: true,
    openSessions: [],
    units: [
      { id: 'first-unit', title: 'First words', description: '', orderNumber: 1 },
      { id: 'later-unit', title: 'Later', description: '', orderNumber: 2 },
    ],
    lessons: [
      { ...next, id: 'locked', unitId: 'later-unit', orderNumber: 1, isUnlocked: false },
      next,
      { ...next, id: 'current', orderNumber: 2, status: 'Completed' },
      { ...next, id: 'earlier-gap', orderNumber: 1 },
    ],
  };

  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'learner' });
    api.progress.mockResolvedValue(progress);
    api.start.mockResolvedValue({ id: 'new-session' });
    api.practice.mockResolvedValue({ id: 'practice-session' });
    api.practiceMistakes.mockResolvedValue({ id: 'mistakes-session' });
    TestBed.configureTestingModule({
      imports: [LessonNextStep],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user } },
        { provide: LearningApi, useValue: api },
      ],
    });
  });

  function button(host: HTMLElement, text: string): HTMLButtonElement {
    return Array.from(host.querySelectorAll<HTMLButtonElement>('button')).find((item) =>
      item.textContent?.includes(text),
    )!;
  }

  async function open(value = session) {
    const fixture = TestBed.createComponent(LessonNextStep);
    fixture.componentRef.setInput('session', value);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    await fixture.whenStable();
    return { fixture, host: fixture.nativeElement as HTMLElement, navigate };
  }

  it('finds an unlocked unfinished step after the current lesson and starts it only on click', async () => {
    const { fixture, host, navigate } = await open();
    expect(host.textContent).toContain('Your next words');
    expect(api.start).not.toHaveBeenCalled();
    button(host, 'Start next lesson').click();
    await fixture.whenStable();
    expect(api.start).toHaveBeenCalledExactlyOnceWith('next');
    expect(navigate).toHaveBeenCalledWith(['/learn/session', 'new-session']);
  });

  it('resumes a saved session without creating another', async () => {
    api.progress.mockResolvedValue({
      ...progress,
      lessons: [{ ...next, activeSessionId: 'saved-session' }],
    });
    const { fixture, host, navigate } = await open();
    button(host, 'Continue saved lesson').click();
    await fixture.whenStable();
    expect(api.start).not.toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith(['/learn/session', 'saved-session']);
  });

  it('offers no next lesson when all other steps are completed or locked', async () => {
    api.progress.mockResolvedValue({
      ...progress,
      lessons: [
        { ...next, status: 'Completed' },
        { ...next, id: 'locked', isUnlocked: false },
      ],
    });
    const { host } = await open();
    expect(button(host, 'Start next lesson')).toBeUndefined();
    expect(host.textContent).toContain('finished the currently available steps');
  });

  it('offers retry and free practice after failure without recommending a later lesson', async () => {
    const { fixture, host, navigate } = await open({
      ...session,
      passed: false,
      hearts: 0,
      awardedXp: 0,
    });
    expect(api.progress).not.toHaveBeenCalled();
    expect(button(host, 'Start next lesson')).toBeUndefined();
    button(host, 'Free practice').click();
    await fixture.whenStable();
    expect(api.practice).toHaveBeenCalledExactlyOnceWith('current');
    expect(navigate).toHaveBeenCalledWith(['/learn/session', 'practice-session']);
    expect(host.textContent).toContain('does not award XP');
  });

  it('retries the original lesson and prevents duplicate submissions while waiting', async () => {
    let finish!: (value: { id: string }) => void;
    api.start.mockReturnValueOnce(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    const { fixture, host } = await open({ ...session, passed: false });
    const retry = button(host, 'Try this lesson again');
    retry.click();
    retry.click();
    expect(api.start).toHaveBeenCalledExactlyOnceWith('current');
    finish({ id: 'retry-session' });
    await fixture.whenStable();
  });

  it('can refresh a failed recommendation without changing the saved result', async () => {
    api.progress.mockRejectedValueOnce(new Error('Synthetic outage'));
    const { fixture, host } = await open();
    expect(host.querySelector('[role=alert]')?.textContent).toContain('could not load');
    button(host, 'Refresh next step').click();
    await fixture.whenStable();
    expect(host.querySelector('[role=alert]')).toBeNull();
    expect(button(host, 'Start next lesson')).toBeDefined();
    expect(api.start).not.toHaveBeenCalled();
  });

  it('clears recommendations and ignores a late response after the account changes', async () => {
    let finish!: (value: CourseProgress) => void;
    api.progress.mockReturnValueOnce(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    const fixture = TestBed.createComponent(LessonNextStep);
    fixture.componentRef.setInput('session', session);
    fixture.detectChanges();
    user.set({ id: 'different-account' });
    fixture.detectChanges();
    finish(progress);
    await fixture.whenStable();
    expect(button(fixture.nativeElement, 'Start next lesson')).toBeUndefined();
    expect(api.progress).toHaveBeenCalledOnce();
  });

  it('removes a previous recommendation when refreshing fails', async () => {
    const { fixture, host } = await open();
    expect(button(host, 'Start next lesson')).toBeDefined();
    api.progress.mockRejectedValueOnce(new Error('Synthetic outage'));
    button(host, 'Refresh next step').click();
    await fixture.whenStable();
    expect(button(host, 'Start next lesson')).toBeUndefined();
    expect(host.textContent).not.toContain('finished the currently available steps');
    expect(host.querySelector('[role=alert]')).not.toBeNull();
  });

  it('repeats course mistake practice without sending an empty lesson ID', async () => {
    const { fixture, host } = await open({
      ...session,
      mode: 'Practice',
      lessonId: '00000000-0000-0000-0000-000000000000',
    });
    button(host, 'Free practice').click();
    await fixture.whenStable();
    expect(api.practice).not.toHaveBeenCalled();
    expect(api.practiceMistakes).toHaveBeenCalledExactlyOnceWith('course');
  });
});
