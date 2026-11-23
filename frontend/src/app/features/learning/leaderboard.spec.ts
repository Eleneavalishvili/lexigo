import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { PreferenceStore } from '../../core/accounts/preference-store';
import { LearningApi } from '../../core/learning/learning-api';
import { LeaderboardApi, LeaderboardView } from '../../core/learning/leaderboard-api';
import { Leaderboard } from './leaderboard';

const result = (courseId = 'course'): LeaderboardView => ({
  courseId,
  courseTitle: courseId === 'course' ? 'Spanish' : 'Italian',
  week: '2026-11-02',
  currentWeek: '2026-11-02',
  optedIn: false,
  myXp: 20,
  me: null,
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 20,
});

describe('Opt-in weekly ranking', () => {
  const user = signal<{ id: string } | null>({ id: 'owner' });
  const query = new BehaviorSubject(convertToParamMap({ courseId: 'course' }));
  const learning = { enrollments: vi.fn() };
  const api = { get: vi.fn() };
  const preferences = { setLeaderboardParticipation: vi.fn() };
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'owner' });
    query.next(convertToParamMap({ courseId: 'course' }));
    learning.enrollments.mockResolvedValue({
      items: [
        { courseId: 'italian', title: 'Italian', isPublished: false },
        { courseId: 'course', title: 'Spanish', isPublished: true },
      ],
      page: 1,
      pageSize: 20,
      totalCount: 2,
    });
    api.get.mockResolvedValue(result());
    preferences.setLeaderboardParticipation.mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      imports: [Leaderboard],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { queryParamMap: query } },
        { provide: AuthStore, useValue: { user } },
        { provide: LearningApi, useValue: learning },
        { provide: LeaderboardApi, useValue: api },
        { provide: PreferenceStore, useValue: preferences },
      ],
    });
  });
  it('shares nothing automatically and changes participation only after a deliberate click', async () => {
    const fixture = TestBed.createComponent(Leaderboard);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector<HTMLSelectElement>('#ranking-course')!.value).toBe('course');
    expect(preferences.setLeaderboardParticipation).not.toHaveBeenCalled();
    expect(host.textContent).toContain('You have saved XP');
    expect(host.textContent).toContain('including previous weeks');
    api.get.mockResolvedValue({
      ...result(),
      optedIn: true,
      totalCount: 1,
      items: [{ rank: 1, displayName: '<img src=x onerror=alert(1)>', xp: 20, isMe: true }],
      me: { rank: 1, displayName: 'Learner', xp: 20, isMe: true },
    });
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((button) => button.textContent?.includes('Join weekly ranking'))!
      .click();
    await fixture.whenStable();
    expect(preferences.setLeaderboardParticipation).toHaveBeenCalledWith(true);
    expect(host.textContent).toContain('Leave ranking');
    TestBed.inject(HttpTestingController).expectOne('/api/v1/me/avatar').flush(null);
    expect(host.querySelectorAll('.ranking li').length).toBe(1);
    expect(host.querySelector('img,script')).toBeNull();
    expect(host.textContent).toContain('<img src=x onerror=alert(1)>');
  });
  it('renders the private profile photo only on the owner row and falls back to initials', async () => {
    api.get.mockResolvedValue({
      ...result(),
      optedIn: true,
      totalCount: 2,
      items: [
        { rank: 1, displayName: 'Other Learner', xp: 25, isMe: false },
        { rank: 2, displayName: 'Signed In', xp: 20, isMe: true },
      ],
    });
    const fixture = TestBed.createComponent(Leaderboard);
    await fixture.whenStable();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/v1/me/avatar').flush({ url: 'https://storage.example.test/own-photo' });
    await Promise.resolve();
    await fixture.whenStable();
    fixture.detectChanges();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('.ranking li:not(.me) img')).toBeNull();
    expect(host.querySelector('.ranking li:not(.me) .avatar')?.textContent?.trim()).toBe('OL');
    const photo = host.querySelector('.ranking .me img')!;
    expect(photo.getAttribute('src')).toBe('https://storage.example.test/own-photo');
    photo.dispatchEvent(new Event('error'));
    await fixture.whenStable();
    expect(host.querySelector('.ranking .me .avatar')?.textContent?.trim()).toBe('SI');
    http.verify();
  });

  it('sends separate photo consent and renders an explicitly shared participant photo', async () => {
    const board = {
      ...result(),
      optedIn: true,
      items: [
        {
          rank: 1,
          displayName: 'Other Learner',
          xp: 25,
          isMe: false,
          photoUrl: 'https://storage.example.test/shared',
        },
      ],
    };
    api.get.mockResolvedValue(board);
    const fixture = TestBed.createComponent(Leaderboard);
    await fixture.whenStable();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/v1/me/avatar').flush({ url: 'https://storage.example.test/own-photo' });
    await Promise.resolve();
    await fixture.whenStable();
    fixture.detectChanges();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('.ranking img')?.getAttribute('src')).toBe(
      'https://storage.example.test/shared',
    );
    api.get.mockResolvedValue({ ...board, photoShared: true });
    Array.from(host.querySelectorAll('button'))
      .find((button) => button.textContent?.trim() === 'Share profile photo')!
      .click();
    await fixture.whenStable();
    expect(preferences.setLeaderboardParticipation).toHaveBeenCalledExactlyOnceWith(true, true);
    expect(host.textContent).toContain('Stop sharing profile photo');
    http.expectOne('/api/v1/me/avatar').flush(null);
    http.verify();
  });
  it('clears old account data and ignores a late response', async () => {
    let resolve!: (value: LeaderboardView) => void;
    api.get.mockReturnValue(new Promise<LeaderboardView>((done) => (resolve = done)));
    const fixture = TestBed.createComponent(Leaderboard);
    await fixture.whenStable();
    user.set(null);
    await fixture.whenStable();
    resolve({ ...result(), myXp: 999 });
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).not.toContain('999');
    expect(fixture.nativeElement.querySelector('.participation')).toBeNull();
  });
  it('removes stale participation controls if rankings cannot reload after a saved choice', async () => {
    const fixture = TestBed.createComponent(Leaderboard);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    api.get.mockRejectedValueOnce(new Error('Temporarily unavailable'));
    Array.from(host.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Join weekly ranking'))!
      .click();
    await fixture.whenStable();
    expect(preferences.setLeaderboardParticipation).toHaveBeenCalledExactlyOnceWith(true);
    expect(host.querySelector('.participation')).toBeNull();
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('Refresh ranking');

    api.get.mockResolvedValue({ ...result(), optedIn: true });
    host.querySelector<HTMLButtonElement>('[role="alert"] button')!.click();
    await fixture.whenStable();
    expect(host.querySelector('.participation')?.textContent).toContain('Leave ranking');
  });
  it('ignores late course results and requests past weeks without using a local score', async () => {
    let resolve!: (value: LeaderboardView) => void;
    api.get.mockImplementation((course: string, week: string | null) =>
      course === 'course'
        ? new Promise<LeaderboardView>((done) => (resolve = done))
        : Promise.resolve({ ...result('italian'), week: week ?? '2026-11-02' }),
    );
    const fixture = TestBed.createComponent(Leaderboard);
    await fixture.whenStable();
    query.next(convertToParamMap({ courseId: 'italian' }));
    await fixture.whenStable();
    resolve({ ...result(), myXp: 999 });
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).not.toContain('999');
    const previous = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Previous week'))!;
    previous.click();
    await fixture.whenStable();
    expect(api.get).toHaveBeenLastCalledWith('italian', '2026-10-26', 1);
  });
  it('returns to the final available page when participation changes shrink a page', async () => {
    const entries = Array.from({ length: 20 }, (_, index) => ({
      rank: index + 1,
      displayName: 'Learner ' + index,
      xp: 20,
      isMe: false,
    }));
    let initial = true;
    api.get.mockImplementation((_course: string, _week: string | null, page: number) => {
      if (page === 2) return Promise.resolve({ ...result(), totalCount: 20, page: 2 });
      const totalCount = initial ? 25 : 20;
      initial = false;
      return Promise.resolve({ ...result(), items: entries, totalCount });
    });
    const fixture = TestBed.createComponent(Leaderboard);
    await fixture.whenStable();
    const next = Array.from(
      fixture.nativeElement.querySelectorAll('.pagination button') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.trim() === 'Next')!;
    next.click();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelectorAll('.ranking li').length).toBe(20);
    expect(fixture.nativeElement.textContent).toContain('Page 1');
    expect(api.get).toHaveBeenLastCalledWith('course', null, 1);
  });
});
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
