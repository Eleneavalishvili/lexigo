import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { LearningApi } from '../../core/learning/learning-api';
import { WeeklySummary, WeeklySummaryApi } from '../../core/learning/weekly-summary-api';
import { WeeklySummaryPage } from './weekly-summary';

describe('Weekly learning summary', () => {
  const user = signal<{ id: string } | null>({ id: 'learner' });
  const api = { read: vi.fn(), compare: vi.fn() };
  const learning = { practiceMistakes: vi.fn() };
  const week: WeeklySummary = {
    weekStart: '2026-11-02',
    weekEnd: '2026-11-08',
    timeZone: 'Asia/Tbilisi',
    today: '2026-11-06',
    weeksAgo: 0,
    earnedXp: 40,
    newLessonsCompleted: 2,
    reviewedWords: 3,
    conversationsFinished: 2,
    studyDays: 1,
    gradedAnswers: 2,
    correctAnswers: 1,
    accuracy: 50,
    days: Array.from({ length: 7 }, (_, index) => ({
      date: '2026-11-' + (index + 2).toString().padStart(2, '0'),
      earnedXp: index === 0 ? 40 : 0,
      studied: index === 0,
    })),
    courses: [
      {
        courseId: 'spanish',
        title: 'Spanish',
        languageCode: 'es',
        isPublished: true,
        earnedXp: 40,
        newLessonsCompleted: 2,
        reviewedWords: 3,
        conversationsFinished: 2,
        gradedAnswers: 2,
        correctAnswers: 1,
        accuracy: 50,
        dueWords: 3,
      },
    ],
  };

  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'learner' });
    api.read.mockImplementation(async (weeksAgo: number) => ({ ...week, weeksAgo }));
    learning.practiceMistakes.mockResolvedValue({ id: 'practice' });
    TestBed.configureTestingModule({
      imports: [WeeklySummaryPage],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user } },
        { provide: WeeklySummaryApi, useValue: api },
        { provide: LearningApi, useValue: learning },
      ],
    });
  });

  function button(host: HTMLElement, text: string): HTMLButtonElement {
    return Array.from(host.querySelectorAll<HTMLButtonElement>('button')).find((item) =>
      item.textContent?.includes(text),
    )!;
  }

  async function open() {
    const fixture = TestBed.createComponent(WeeklySummaryPage);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    await fixture.whenStable();
    return { fixture, host: fixture.nativeElement as HTMLElement, navigate };
  }

  it('shows server totals, seven stable calendar days and truthful recommendations', async () => {
    const { host } = await open();
    expect(host.querySelectorAll('.days li')).toHaveLength(7);
    expect(host.querySelector('time')?.textContent).toBe('Mon');
    expect(host.textContent).toContain('Asia/Tbilisi');
    expect(host.textContent).toContain('50%');
    expect(host.textContent).toContain('2 conversations finished');
    expect(host.querySelector('a[href$="/dialogues"]')?.getAttribute('aria-label')).toBe(
      'Practise Spanish conversations',
    );
    expect(host.textContent).toContain('3 saved word(s) are due');
    expect(host.querySelector('a[href*="/vocabulary?"]')?.getAttribute('href')).toBe(
      '/vocabulary?courseId=spanish&status=due',
    );
    expect(host.textContent).toContain('You missed 1 graded answer');
    expect(host.textContent).toContain('Still ahead');
    expect(learning.practiceMistakes).not.toHaveBeenCalled();
  });

  it('shows unmeasured accuracy instead of a fabricated zero score', async () => {
    api.read.mockResolvedValue({ ...week, accuracy: null, courses: [] });
    const { host } = await open();
    expect(host.textContent).toContain('Not yet measured');
    expect(host.textContent).not.toContain('0%');
    expect(host.textContent).toContain('Choose your first language');
  });

  it('shows a plan link when the server denies Premium comparison', async () => {
    api.compare.mockRejectedValue(new HttpErrorResponse({ status: 403 }));
    const { fixture, host } = await open();
    button(host, 'Compare with the previous week').click();
    await fixture.whenStable();
    expect(api.compare).toHaveBeenCalledWith(0);
    expect(host.querySelector('a[href="/pricing"]')?.textContent).toContain('Compare plans');
    expect(host.querySelector('table')).toBeNull();
  });

  it('shows both weeks and labels the current week as incomplete', async () => {
    api.compare.mockResolvedValue({
      selected: week,
      previous: { ...week, weeksAgo: 1, weekStart: '2026-10-26', earnedXp: 20, accuracy: null },
    });
    const { fixture, host } = await open();
    button(host, 'Compare with the previous week').click();
    await fixture.whenStable();
    expect(host.querySelector('table')?.textContent).toContain('Oct 26');
    expect(host.querySelector('table')?.textContent).toContain('Nov 2');
    expect(host.querySelector('table')?.textContent).toContain('50%');
    expect(host.textContent).toContain('still in progress');
  });

  it('moves through week history without retaining stale totals during loading', async () => {
    const { fixture, host } = await open();
    expect(button(host, 'Next week').disabled).toBe(true);
    let finish!: (value: WeeklySummary) => void;
    api.read.mockReturnValueOnce(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    button(host, 'Previous week').click();
    fixture.detectChanges();
    expect(host.querySelector('.totals')).toBeNull();
    expect(button(host, 'Previous week').disabled).toBe(true);
    finish({ ...week, weeksAgo: 1 });
    await vi.waitFor(() => expect(button(host, 'Next week').disabled).toBe(false));
    await fixture.whenStable();
    expect(api.read).toHaveBeenLastCalledWith(1);
    expect(button(host, 'Next week').disabled).toBe(false);
    button(host, 'Next week').click();
    await fixture.whenStable();
    expect(api.read).toHaveBeenLastCalledWith(0);
  });

  it('offers read retry after failure without displaying invented progress', async () => {
    api.read.mockRejectedValueOnce(new Error('Synthetic outage'));
    const { fixture, host } = await open();
    expect(host.querySelector('[role=alert]')?.textContent).toContain('could not load');
    expect(host.querySelector('.totals')).toBeNull();
    button(host, 'Refresh summary').click();
    await fixture.whenStable();
    expect(host.querySelector('.totals')).not.toBeNull();
    expect(host.querySelector('[role=alert]')).toBeNull();
  });

  it('opens current mistake practice only after an explicit click', async () => {
    const { fixture, host, navigate } = await open();
    button(host, 'Practise current mistakes').click();
    await fixture.whenStable();
    expect(learning.practiceMistakes).toHaveBeenCalledExactlyOnceWith('spanish');
    expect(navigate).toHaveBeenCalledWith(['/learn/session', 'practice']);
  });

  it('handles already-resolved mistakes without changing the historical summary', async () => {
    learning.practiceMistakes.mockRejectedValueOnce(new HttpErrorResponse({ status: 409 }));
    const { fixture, host } = await open();
    button(host, 'Practise current mistakes').click();
    await fixture.whenStable();
    expect(host.querySelector('[role=alert]')?.textContent).toContain('no eligible mistakes');
    expect(host.textContent).toContain('40 XP');
    expect(host.textContent).toContain('You missed 1 graded answer');
  });

  it('ignores late private data after an account change', async () => {
    let finish!: (value: WeeklySummary) => void;
    api.read.mockReturnValueOnce(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    const fixture = TestBed.createComponent(WeeklySummaryPage);
    fixture.detectChanges();
    user.set({ id: 'another-account' });
    fixture.detectChanges();
    finish(week);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('.totals')).toBeNull();
    expect(host.textContent).not.toContain('Spanish');
    expect(api.read).toHaveBeenCalledOnce();
  });
});
