import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthStore } from '../../core/accounts/auth-store';
import { LearningApi, MistakePage } from '../../core/learning/learning-api';
import { MistakePractice } from './mistake-practice';

@Component({ template: '' })
class Destination {}

describe('Saved mistake practice', () => {
  const user = signal<{ id: string } | null>({ id: 'learner' });
  const api = { mistakes: vi.fn(), practiceMistakes: vi.fn() };
  const summary: MistakePage = {
    items: [
      {
        exerciseId: 'source',
        prompt: 'Translate hello',
        type: 'Translation',
        contextText: null,
        wrongCount: 2,
        lastMissedAt: '2026-11-02T10:00:00Z',
      },
    ],
    totalCount: 1,
    page: 1,
  };
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'learner' });
    api.mistakes.mockResolvedValue(summary);
    api.practiceMistakes.mockResolvedValue({ id: 'review' });
    TestBed.configureTestingModule({
      imports: [MistakePractice],
      providers: [
        provideRouter([{ path: 'learn/session/:sessionId', component: Destination }]),
        { provide: AuthStore, useValue: { user } },
        { provide: LearningApi, useValue: api },
      ],
    });
  });
  async function open() {
    const fixture = TestBed.createComponent(MistakePractice);
    fixture.componentRef.setInput('courseId', 'course');
    await fixture.whenStable();
    return fixture;
  }
  it('explains the recommendation and requests an owned course batch only after a click', async () => {
    const fixture = await open();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Repeated mistake');
    expect(host.textContent).toContain('Missed 2 times');
    expect(host.querySelector('time')?.dateTime).toBe(summary.items[0].lastMissedAt);
    expect(host.textContent).toContain('No hearts lost or XP earned');
    expect(api.practiceMistakes).not.toHaveBeenCalled();
    host.querySelector<HTMLButtonElement>('button')!.click();
    await fixture.whenStable();
    expect(api.practiceMistakes).toHaveBeenCalledWith('course');
  });
  it('shows an honest empty state without starting practice automatically', async () => {
    api.mistakes.mockResolvedValue({ items: [], totalCount: 0, page: 1 });
    const fixture = await open();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('No saved mistakes are ready to practise');
    expect(host.querySelector('button')).toBeNull();
    expect(host.querySelector('a[href="/pricing"]')?.textContent).toContain('Premium');
    expect(api.practiceMistakes).not.toHaveBeenCalled();
  });
  it('expands the preview and can navigate forward and back through the saved questions', async () => {
    const items = Array.from({ length: 20 }, (_, index) => ({
      ...summary.items[0],
      exerciseId: `first-${index}`,
    }));
    api.mistakes.mockResolvedValueOnce({ items, page: 1, pageSize: 20, totalCount: 21 });
    const fixture = await open();
    const host: HTMLElement = fixture.nativeElement;
    const click = (label: string) => {
      const button = [...host.querySelectorAll('button')].find(
        (item) => item.textContent?.trim() === label,
      );
      expect(button).toBeDefined();
      button!.click();
    };
    expect(host.querySelectorAll('li')).toHaveLength(5);
    click('Show more review questions');
    await fixture.whenStable();
    expect(host.querySelectorAll('li')).toHaveLength(20);

    api.mistakes.mockResolvedValueOnce({ ...summary, page: 2, pageSize: 20, totalCount: 21 });
    click('Next');
    await fixture.whenStable();
    expect(api.mistakes).toHaveBeenLastCalledWith('course', 2);
    expect(host.querySelectorAll('li')).toHaveLength(1);
    expect(host.textContent).toContain('Page 2');

    api.mistakes.mockResolvedValueOnce({ items, page: 1, pageSize: 20, totalCount: 21 });
    click('Previous');
    await fixture.whenStable();
    expect(host.querySelectorAll('li')).toHaveLength(20);
    expect(host.textContent).toContain('Page 1');
  });
  it.each([
    ['Repeated mistakes', 'repeated'],
    ['Due for review', 'due'],
  ])('opens the selected Premium practice mode: %s', async (label, focus) => {
    api.mistakes.mockResolvedValue({ ...summary, advancedPracticeAvailable: true });
    const fixture = await open();
    const host: HTMLElement = fixture.nativeElement;
    const action = [...host.querySelectorAll('button')].find(
      (button) => button.textContent?.trim() === label,
    );
    expect(action).toBeDefined();
    action!.click();
    await fixture.whenStable();
    expect(api.practiceMistakes).toHaveBeenCalledExactlyOnceWith('course', undefined, focus);
  });
  it('escapes saved prompts and shows a server rejection without claiming practice started', async () => {
    api.mistakes.mockResolvedValue({
      ...summary,
      items: [{ ...summary.items[0], prompt: '<script>bad()</script>' }],
    });
    api.practiceMistakes.mockRejectedValue(
      new HttpErrorResponse({
        status: 409,
        error: { title: 'No saved mistakes are ready to practise.' },
      }),
    );
    const fixture = await open();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('script')).toBeNull();
    host.querySelector<HTMLButtonElement>('button')!.click();
    await fixture.whenStable();
    expect(host.querySelector('[role=alert]')?.textContent).toContain('No saved mistakes');
    expect(host.textContent).not.toContain('Opening practice…');
  });
  it('discards a previous learner’s late summary', async () => {
    let resolve!: (value: MistakePage) => void;
    api.mistakes.mockReturnValue(
      new Promise<MistakePage>((done) => {
        resolve = done;
      }),
    );
    const fixture = TestBed.createComponent(MistakePractice);
    fixture.componentRef.setInput('courseId', 'course');
    fixture.detectChanges();
    user.set(null);
    fixture.detectChanges();
    resolve(summary);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Translate hello');
    expect(api.practiceMistakes).not.toHaveBeenCalled();
  });
});
