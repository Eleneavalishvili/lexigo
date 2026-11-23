import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { LearningApi, LearningDashboard } from '../../core/learning/learning-api';
import { LearningOverview } from './learning-overview';

const summary = (course = 'spanish'): LearningDashboard => ({
  totalXp: 120,
  todayXp: 20,
  dailyGoalXp: 40,
  goalReached: false,
  currentStreak: 3,
  completedLessons: 6,
  localDate: '2026-11-03',
  timeZone: 'Asia/Tbilisi',
  hearts: {
    count: 2,
    maximum: 5,
    serverNow: '2020-01-01T00:00:00Z',
    refillAt: '2020-01-01T00:01:00Z',
    unlimited: false,
  },
  course: {
    id: course,
    title: course === 'spanish' ? 'Spanish' : 'Italian',
    languageCode: course === 'spanish' ? 'es' : 'it',
    earnedXp: 60,
    completedLessons: 3,
  },
});

describe('Owned learning overview', () => {
  const user = signal<{ id: string } | null>({ id: 'learner' });
  const api = { dashboard: vi.fn() };
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'learner' });
    api.dashboard.mockResolvedValue(summary());
    TestBed.configureTestingModule({
      imports: [LearningOverview],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user } },
        { provide: LearningApi, useValue: api },
      ],
    });
  });
  it('shows server XP/streak and an offset-corrected heart timer without inventing rewards', async () => {
    const fixture = TestBed.createComponent(LearningOverview);
    fixture.componentRef.setInput('courseId', 'spanish');
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('120');
    expect(host.textContent).toContain('3');
    expect(host.textContent).toContain('2/5');
    expect(host.textContent).toContain('Full hearts in 1:00');
    expect(host.textContent).toContain('20 / 40 XP');
    const progress = host.querySelector('progress')!;
    expect(progress.value).toBe(20);
    expect(progress.max).toBe(40);
    expect(host.querySelector('nav')?.getAttribute('aria-label')).toBe('Learning navigation');
    expect(host.querySelector('a[href="/learn/spanish/dialogues"]')).not.toBeNull();
    expect(host.textContent).not.toContain('gems');
  });
  it('clears the old owner immediately and ignores their late response', async () => {
    let resolve!: (value: LearningDashboard) => void;
    api.dashboard.mockReturnValue(new Promise<LearningDashboard>((done) => (resolve = done)));
    const fixture = TestBed.createComponent(LearningOverview);
    await fixture.whenStable();
    user.set(null);
    await fixture.whenStable();
    resolve(summary());
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).not.toContain('120');
    expect(fixture.nativeElement.querySelector('progress')).toBeNull();
    expect(fixture.nativeElement.querySelector('a[href$="/dialogues"]')).toBeNull();
  });
  it('ignores a late course response and provides a retry after a failed read', async () => {
    let resolve!: (value: LearningDashboard) => void;
    api.dashboard.mockImplementation((course: string) =>
      course === 'spanish'
        ? new Promise<LearningDashboard>((done) => (resolve = done))
        : Promise.resolve(summary('italian')),
    );
    const fixture = TestBed.createComponent(LearningOverview);
    fixture.componentRef.setInput('courseId', 'spanish');
    await fixture.whenStable();
    fixture.componentRef.setInput('courseId', 'italian');
    await fixture.whenStable();
    resolve(summary());
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Italian');
    expect(fixture.nativeElement.textContent).not.toContain('Spanish');
    expect(
      fixture.nativeElement.querySelector('a[href="/learn/italian/dialogues"]'),
    ).not.toBeNull();
    api.dashboard.mockRejectedValue(new Error('offline'));
    fixture.componentRef.setInput('courseId', 'german');
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('couldn’t refresh');
    expect(fixture.nativeElement.querySelector('progress')).toBeNull();
  });
});
