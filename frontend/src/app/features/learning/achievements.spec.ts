import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { AchievementsApi, AchievementView } from '../../core/learning/achievements-api';
import { Achievements } from './achievements';

const badge = (changes: Partial<AchievementView> = {}): AchievementView => ({
  id: 'first',
  code: 'first-lesson',
  title: 'Your first little step',
  ruleType: 'CompletedLessons',
  threshold: 1,
  progress: 1,
  earnedAt: null,
  canCollect: true,
  ...changes,
});

describe('Achievement collection', () => {
  const user = signal<{ id: string } | null>({ id: 'owner' });
  const api = { get: vi.fn(), collect: vi.fn() };
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'owner' });
    api.get.mockResolvedValue([badge()]);
    api.collect.mockResolvedValue([badge({ earnedAt: '2026-11-03T10:00:00Z', canCollect: false })]);
    TestBed.configureTestingModule({
      imports: [Achievements],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user } },
        { provide: AchievementsApi, useValue: api },
      ],
    });
  });
  it('reads without awarding and collects only on a deliberate click', async () => {
    const fixture = TestBed.createComponent(Achievements);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(api.collect).not.toHaveBeenCalled();
    expect(host.textContent).toContain('READY TO COLLECT');
    api.get.mockResolvedValue([badge({ earnedAt: '2026-11-03T10:00:00Z', canCollect: false })]);
    host.querySelector<HTMLButtonElement>('.collection-summary button')!.click();
    await fixture.whenStable();
    expect(api.collect).toHaveBeenCalledOnce();
    expect(host.querySelector('[role=status]')?.textContent).toContain('Achievement unlocked:');
    expect(host.textContent).toContain('Recognized');
    expect(host.querySelector('.collection-summary button')).toBeNull();
  });
  it('retains earned status after word removal and escapes titles', async () => {
    api.get.mockResolvedValue([
      badge({
        ruleType: 'SavedWords',
        title: '<img src=x onerror=alert(1)>',
        progress: 9,
        threshold: 10,
        earnedAt: '2026-11-03T10:00:00Z',
        canCollect: false,
      }),
    ]);
    const fixture = TestBed.createComponent(Achievements);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('.badge-card.earned')).not.toBeNull();
    expect(host.querySelector('h2')!.textContent).toContain('<img');
    expect(host.querySelector('h2 img')).toBeNull();
    expect(host.querySelector('progress')).toBeNull();
  });
  it('discards old account responses', async () => {
    let resolve!: (value: AchievementView[]) => void;
    api.get.mockReturnValueOnce(
      new Promise<AchievementView[]>((done) => {
        resolve = done;
      }),
    );
    const fixture = TestBed.createComponent(Achievements);
    fixture.detectChanges();
    await Promise.resolve();
    user.set(null);
    fixture.detectChanges();
    resolve([badge()]);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).not.toContain('Your first little step');
    expect(api.collect).not.toHaveBeenCalled();
  });
});
