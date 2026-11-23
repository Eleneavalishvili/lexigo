import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { ChallengesApi, ChallengeView } from '../../core/learning/challenges-api';
import { Challenges } from './challenges';

const challenge = (changes: Partial<ChallengeView> = {}): ChallengeView => ({
  id: 'weekly',
  title: 'Three new chapters',
  ruleType: 'CompletedLessons',
  target: 3,
  rewardXp: 20,
  startsAt: '2026-11-02T00:00:00Z',
  endsAt: '2026-11-09T00:00:00Z',
  progress: 1,
  completedAt: null,
  rewardedAt: null,
  rewardCourseId: null,
  isActive: true,
  ...changes,
});

describe('Weekly challenges', () => {
  const user = signal<{ id: string } | null>({ id: 'owner' });
  const api = { get: vi.fn() };
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'owner' });
    api.get.mockResolvedValue([challenge()]);
    TestBed.configureTestingModule({
      imports: [Challenges],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user } },
        { provide: ChallengesApi, useValue: api },
      ],
    });
  });
  it('shows server progress, deadlines and automatic reward rules without a claim action', async () => {
    const fixture = TestBed.createComponent(Challenges);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('1 / 3');
    expect(host.textContent).toContain('+20 XP');
    expect(host.textContent).toContain('UTC');
    expect(host.textContent).toContain('Repeated lessons');
    expect(host.querySelectorAll('button').length).toBe(1);
    expect(host.querySelector('progress')!.getAttribute('value')).toBe('1');
  });
  it('keeps rewarded history and distinguishes ended challenges while escaping titles', async () => {
    api.get.mockResolvedValue([
      challenge({
        rewardedAt: '2026-11-05T10:00:00Z',
        completedAt: '2026-11-05T10:00:00Z',
        progress: 3,
        isActive: false,
      }),
      challenge({ id: 'expired', title: '<img src=x>', isActive: false }),
    ]);
    const fixture = TestBed.createComponent(Challenges);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('BONUS EARNED');
    expect(host.textContent).toContain('CHALLENGE ENDED');
    expect(host.querySelector('h2 img')).toBeNull();
  });
  it('clears the previous account while a response is pending', async () => {
    let resolve!: (value: ChallengeView[]) => void;
    api.get.mockReturnValueOnce(
      new Promise<ChallengeView[]>((done) => {
        resolve = done;
      }),
    );
    const fixture = TestBed.createComponent(Challenges);
    fixture.detectChanges();
    await Promise.resolve();
    user.set(null);
    fixture.detectChanges();
    resolve([challenge()]);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).not.toContain('Three new chapters');
  });
  it('offers retry on failed reads without inventing progress', async () => {
    api.get.mockRejectedValueOnce(new Error('offline'));
    const fixture = TestBed.createComponent(Challenges);
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[role=alert]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('progress')).toBeNull();
    fixture.nativeElement.querySelector('button').click();
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[role=alert]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Three new chapters');
  });
});
