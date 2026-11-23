import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { PreferenceStore } from '../../core/accounts/preference-store';
import { LearningApi } from '../../core/learning/learning-api';
import { CourseStart } from './course-start';

@Component({ template: '' })
class Destination {}

describe('Choose a goal and a beginner or assessed start', () => {
  const user = signal<{ id: string } | null>({ id: 'learner' });
  const api = { progress: vi.fn(), enroll: vi.fn(), start: vi.fn(), placement: vi.fn() };
  const preferences = { setStudyGoal: vi.fn() };
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'learner' });
    api.progress.mockResolvedValue({
      title: 'Spanish',
      lessons: [{ id: 'pictures', isUnlocked: true }],
    });
    api.enroll.mockResolvedValue({});
    api.start.mockResolvedValue({ id: 'lesson-session' });
    api.placement.mockResolvedValue({ id: 'placement-session' });
    preferences.setStudyGoal.mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      imports: [CourseStart],
      providers: [
        provideRouter([
          { path: 'login', component: Destination },
          { path: 'learn/session/:sessionId', component: Destination },
        ]),
        { provide: AuthStore, useValue: { user } },
        { provide: LearningApi, useValue: api },
        { provide: PreferenceStore, useValue: preferences },
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ courseId: 'course' })) },
        },
      ],
    });
  });
  async function chooseGoal() {
    const fixture = TestBed.createComponent(CourseStart);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector<HTMLButtonElement>('footer button')!.disabled).toBe(true);
    host.querySelectorAll<HTMLButtonElement>('.choices button')[1].click();
    await fixture.whenStable();
    host.querySelector<HTMLButtonElement>('footer button')!.click();
    await fixture.whenStable();
    return fixture;
  }
  it('keeps setup unavailable after a course load fails and restores it through retry', async () => {
    api.progress.mockRejectedValueOnce(new Error('offline'));
    const fixture = TestBed.createComponent(CourseStart);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('[role=alert]')?.textContent).toContain('could not be opened');
    expect(host.querySelector<HTMLButtonElement>('.choices button')!.disabled).toBe(true);
    expect(host.querySelector<HTMLButtonElement>('footer .button-red')!.disabled).toBe(true);
    expect(preferences.setStudyGoal).not.toHaveBeenCalled();
    Array.from(host.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Try loading course again'))!
      .click();
    await fixture.whenStable();
    expect(api.progress).toHaveBeenCalledTimes(2);
    expect(host.querySelector('[role=alert]')).toBeNull();
    expect(host.querySelector('.course-title')?.textContent).toContain('Spanish');
    expect(host.querySelector<HTMLButtonElement>('.choices button')!.disabled).toBe(false);
    expect(api.enroll).not.toHaveBeenCalled();
  });
  it('does not duplicate goal saving or enrollment while starting a lesson', async () => {
    let finishSave!: () => void;
    preferences.setStudyGoal.mockReturnValue(
      new Promise<void>((resolve) => (finishSave = resolve)),
    );
    const fixture = await chooseGoal();
    const host: HTMLElement = fixture.nativeElement;
    const begin = host.querySelector<HTMLButtonElement>('footer .button-red')!;
    begin.click();
    begin.click();
    fixture.detectChanges();
    expect(begin.disabled).toBe(true);
    expect(preferences.setStudyGoal).toHaveBeenCalledTimes(1);
    finishSave();
    await vi.waitFor(() => expect(api.start).toHaveBeenCalledTimes(1));
    await fixture.whenStable();
    expect(api.enroll).toHaveBeenCalledTimes(1);
    expect(api.start).toHaveBeenCalledTimes(1);
  });
  it('saves the selected XP goal and starts the first unlocked picture lesson without a diagnostic', async () => {
    const fixture = await chooseGoal();
    const host: HTMLElement = fixture.nativeElement;
    expect(preferences.setStudyGoal).not.toHaveBeenCalled();
    expect(host.textContent).toContain('Start from scratch');
    host.querySelector<HTMLButtonElement>('footer .button-red')!.click();
    await fixture.whenStable();
    expect(preferences.setStudyGoal).toHaveBeenCalledWith(40);
    expect(api.enroll).toHaveBeenCalledWith('course', 'A1');
    expect(api.start).toHaveBeenCalledWith('pictures');
    expect(api.placement).not.toHaveBeenCalled();
  });
  it('offers a real placement route without inventing an A1 selection', async () => {
    const fixture = await chooseGoal();
    const host: HTMLElement = fixture.nativeElement;
    host.querySelectorAll<HTMLButtonElement>('.choices button')[1].click();
    await fixture.whenStable();
    host.querySelector<HTMLButtonElement>('footer .button-red')!.click();
    await fixture.whenStable();
    expect(api.enroll).toHaveBeenCalledWith('course', undefined);
    expect(api.placement).toHaveBeenCalledWith('course');
    expect(api.start).not.toHaveBeenCalled();
  });
  it('does not continue enrollment under a different account after a late goal save', async () => {
    let resolve!: () => void;
    preferences.setStudyGoal.mockReturnValue(
      new Promise<void>((done) => {
        resolve = done;
      }),
    );
    const fixture = await chooseGoal();
    const host: HTMLElement = fixture.nativeElement;
    host.querySelector<HTMLButtonElement>('footer .button-red')!.click();
    user.set({ id: 'other' });
    fixture.detectChanges();
    resolve();
    await fixture.whenStable();
    expect(api.enroll).not.toHaveBeenCalled();
    expect(api.start).not.toHaveBeenCalled();
  });
});
