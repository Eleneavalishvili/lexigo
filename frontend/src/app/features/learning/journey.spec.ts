import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { CourseProgress, LearningApi } from '../../core/learning/learning-api';
import { SkillMapApi } from '../../core/learning/skill-map-api';
import { LearningJourney } from './journey';
@Component({ template: '' })
class Destination {}
describe('Language journey', () => {
  const user = signal<{ id: string } | null>({ id: 'learner' });
  const api = {
    progress: vi.fn(),
    enroll: vi.fn(),
    enrollments: vi.fn(),
    start: vi.fn(),
    practice: vi.fn(),
    placement: vi.fn(),
    mistakes: vi.fn(),
    practiceMistakes: vi.fn(),
    dashboard: vi.fn(),
  };
  const course: CourseProgress = {
    courseId: 'course',
    title: 'Spanish',
    isEnrolled: true,
    units: [{ id: 'unit', title: 'Greetings', description: 'First words', orderNumber: 1 }],
    openSessions: [],
    lessons: [
      {
        id: 'first',
        unitId: 'unit',
        title: 'First words',
        objective: 'Say hello',
        orderNumber: 1,
        status: 'Completed',
        bestScore: 100,
        isUnlocked: true,
        missingPrerequisites: [],
        activeSessionId: null,
      },
      {
        id: 'next',
        unitId: 'unit',
        title: 'Next words',
        objective: 'Say more',
        orderNumber: 2,
        status: 'NotStarted',
        bestScore: 0,
        isUnlocked: false,
        missingPrerequisites: ['first'],
        activeSessionId: null,
      },
    ],
  };
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'learner' });
    api.progress.mockResolvedValue(structuredClone(course));
    api.enroll.mockResolvedValue({});
    api.mistakes.mockResolvedValue({ items: [], totalCount: 0, page: 1 });
    api.dashboard.mockResolvedValue(null);
    TestBed.configureTestingModule({
      imports: [LearningJourney],
      providers: [
        provideRouter([
          { path: 'login', component: Destination },
          { path: 'learn/session/:sessionId', component: Destination },
        ]),
        { provide: AuthStore, useValue: { user } },
        { provide: LearningApi, useValue: api },
        {
          provide: SkillMapApi,
          useValue: {
            read: () =>
              of([
                {
                  id: 1,
                  name: 'Vocabulary',
                  correctCount: 1,
                  incorrectCount: 3,
                  lastPracticedAt: '2026-11-08',
                },
                {
                  id: 2,
                  name: 'Grammar',
                  correctCount: 0,
                  incorrectCount: 0,
                  lastPracticedAt: null,
                },
              ]),
          },
        },
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ courseId: 'course' })) },
        },
      ],
    });
  });
  it('shows server prerequisites and prevents locked-lesson starts', async () => {
    const fixture = TestBed.createComponent(LearningJourney);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const locked = host.querySelectorAll<HTMLLIElement>('.lesson-path li')[1];
    expect(locked.textContent).toContain('First complete:');
    expect(locked.querySelector('a[href="#lesson-first"]')?.textContent).toContain('First words');
    expect(locked.querySelector<HTMLButtonElement>('button')!.disabled).toBe(true);
    expect(host.textContent).toContain('Best graded score 100%');
    expect(host.querySelector('app-skill-map')?.textContent).toContain('25% correct');
    expect(host.querySelector('app-skill-map')?.textContent).toContain('Not yet practised');
    expect(api.start).not.toHaveBeenCalled();
  });
  it('starts a mistake batch for the skill selected on the map', async () => {
    api.practiceMistakes.mockResolvedValue({ id: 'skill-session' });
    const fixture = TestBed.createComponent(LearningJourney);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    Array.from(host.querySelectorAll<HTMLButtonElement>('app-skill-map button'))
      .find((button) => button.textContent?.includes('Review Vocabulary mistakes'))!
      .click();
    await fixture.whenStable();
    expect(api.practiceMistakes).toHaveBeenCalledExactlyOnceWith('course', 1);
  });

  it('joins an undecided learner before starting the placement check without claiming a self-reported level', async () => {
    api.progress.mockResolvedValue({ ...course, isEnrolled: false, startingLevel: null });
    api.placement.mockResolvedValue({ id: 'placement' });
    const fixture = TestBed.createComponent(LearningJourney);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((button) => button.textContent?.includes('Find my starting point'))!
      .click();
    await fixture.whenStable();
    expect(api.enroll).toHaveBeenCalledWith('course');
    expect(api.placement).toHaveBeenCalledWith('course');
    expect(api.start).not.toHaveBeenCalled();
  });
  it('offers the recommended unit rather than returning an assessed learner to the first unfinished basics', async () => {
    api.progress.mockResolvedValue({
      ...course,
      recommendedUnitId: 'later',
      units: [
        ...course.units,
        { id: 'later', title: 'Later unit', description: 'More', orderNumber: 2 },
      ],
      lessons: [
        { ...course.lessons[0], status: 'NotStarted' },
        { ...course.lessons[1], unitId: 'later', isUnlocked: true },
      ],
    });
    const fixture = TestBed.createComponent(LearningJourney);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('#next-lesson-title')?.textContent).toContain('Next words');
    expect(host.querySelector('.lesson-path')?.textContent).toContain('First words');
  });
  it('joins before offering lesson actions and loads the saved map again', async () => {
    api.progress.mockResolvedValueOnce({ ...course, isEnrolled: false });
    const fixture = TestBed.createComponent(LearningJourney);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Join this course');
    expect(host.querySelector('.lesson-actions')).toBeNull();
    const level = host.querySelector<HTMLSelectElement>('#starting-level')!;
    level.value = 'A1';
    level.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    host.querySelector<HTMLButtonElement>('.invitation button')!.click();
    await fixture.whenStable();
    expect(api.enroll).toHaveBeenCalledWith('course', 'A1');
    expect(api.progress).toHaveBeenCalledTimes(2);
    expect(host.querySelector('.lesson-actions')).not.toBeNull();
  });
  it('opens free practice without adding client-provided entitlement or XP values', async () => {
    api.practice.mockResolvedValue({ id: 'practice' });
    const fixture = TestBed.createComponent(LearningJourney);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    host.querySelector<HTMLButtonElement>('.lesson-actions .text-button')!.click();
    await fixture.whenStable();
    expect(api.practice).toHaveBeenCalledWith('first');
    expect(api.start).not.toHaveBeenCalled();
  });
  it('does not display a percentage for an ungraded speaking-only completion', async () => {
    api.progress.mockResolvedValue({
      ...course,
      lessons: [{ ...course.lessons[0], bestScore: 0, hasGradedScore: false }],
    });
    const fixture = TestBed.createComponent(LearningJourney);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Practice completed · ungraded');
    expect(host.querySelector('.lesson-copy .small')!.textContent).not.toContain('%');
  });
  it('shows a unit-card link only after every lesson in that unit is completed', async () => {
    const partial = TestBed.createComponent(LearningJourney);
    await partial.whenStable();
    expect(
      (partial.nativeElement as HTMLElement).querySelector(
        '.chapter-heading a[href^="/completion-cards"]',
      ),
    ).toBeNull();
    partial.destroy();
    api.progress.mockResolvedValue({
      ...course,
      lessons: course.lessons.map((lesson) => ({
        ...lesson,
        status: 'Completed',
        isUnlocked: true,
      })),
    });
    const completed = TestBed.createComponent(LearningJourney);
    await completed.whenStable();
    const link = (completed.nativeElement as HTMLElement).querySelector(
      '.chapter-heading a[href^="/completion-cards"]',
    );
    expect(link?.getAttribute('href')).toBe('/completion-cards?unitId=unit');
  });
});
