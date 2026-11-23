import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { AuthStore } from '../../core/accounts/auth-store';
import { PreferenceStore } from '../../core/accounts/preference-store';
import { Profile } from './profile';

describe('Profile and study settings', () => {
  const initial = {
    timeZone: 'UTC',
    dailyGoalXp: 20,
    theme: 'system',
    reduceMotion: false,
    soundEnabled: true,
    leaderboardOptIn: false,
    activeCourseId: null,
    studyDays: 31,
    reminderTime: '18:00:00',
    emailRemindersEnabled: false,
  };
  const preferences = { get: vi.fn(), courses: vi.fn(), save: vi.fn() };
  const refresh = vi.fn();
  beforeEach(() => {
    vi.clearAllMocks();
    preferences.get.mockResolvedValue(initial);
    preferences.courses.mockResolvedValue([]);
    preferences.save.mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      imports: [Profile],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PreferenceStore, useValue: preferences },
        {
          provide: AuthStore,
          useValue: { user: signal({ id: 'one', displayName: 'Curious Learner' }), refresh },
        },
      ],
    });
  });

  it('loads saved values and preserves settings after a failed save even when no courses are published', async () => {
    preferences.save.mockRejectedValue(new Error('offline'));
    const fixture = TestBed.createComponent(Profile);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector<HTMLInputElement>('#daily-goal')?.value).toBe('20');
    host.querySelector<HTMLFormElement>('.settings-form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('couldn’t save');
    expect(host.querySelector('.settings-form')).not.toBeNull();
    expect(host.querySelector('[role="status"]')).toBeNull();
  });

  it('requests only the vocabulary file from the Privacy download button', async () => {
    const fixture = TestBed.createComponent(Profile);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const button = [...host.querySelectorAll('button')].find((item) =>
      item.textContent?.includes('Download my vocabulary'),
    )!;
    expect(button.type).toBe('button');
    button.click();
    const request = TestBed.inject(HttpTestingController).expectOne('/api/v1/me/export/vocabulary');
    expect(request.request.method).toBe('GET');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(), { status: 503, statusText: 'Unavailable' });
    await fixture.whenStable();
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('could not be downloaded');
    expect(preferences.save).not.toHaveBeenCalled();
  });

  it('uses the learning export endpoint from the main Privacy download button', async () => {
    const fixture = TestBed.createComponent(Profile);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    [...host.querySelectorAll('button')]
      .find((item) => item.textContent?.includes('Download my learning data'))!
      .click();
    const request = TestBed.inject(HttpTestingController).expectOne('/api/v1/me/export');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(), { status: 503, statusText: 'Unavailable' });
    await fixture.whenStable();
    expect(preferences.save).not.toHaveBeenCalled();
  });

  it('requires at least one study day and sends local reminder time with no course when unselected', async () => {
    const fixture = TestBed.createComponent(Profile);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const days = host.querySelectorAll<HTMLButtonElement>('.days button');
    for (let i = 0; i < 5; i++) days[i].click();
    host.querySelector<HTMLFormElement>('.settings-form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(preferences.save).not.toHaveBeenCalled();
    days[6].click();
    host.querySelector<HTMLFormElement>('.settings-form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(preferences.save).toHaveBeenCalledWith({ ...initial, studyDays: 64 });
    expect(host.querySelector('[role="status"]')?.textContent).toContain('saved');
  });

  it('shows a retry when loading fails', async () => {
    preferences.get.mockRejectedValueOnce(new Error('offline'));
    const fixture = TestBed.createComponent(Profile);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('.settings-form')).toBeNull();
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((button) => button.textContent?.includes('Try again'))!
      .click();
    await fixture.whenStable();
    expect(host.querySelector('.settings-form')).not.toBeNull();
  });

  it('renames only through the account API and refreshes the displayed account after success', async () => {
    const fixture = TestBed.createComponent(Profile);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const input = host.querySelector<HTMLInputElement>('#display-name')!;
    input.value = 'New Name';
    input.dispatchEvent(new Event('input'));
    host.querySelector<HTMLFormElement>('.name-card form')!.dispatchEvent(new Event('submit'));
    const request = TestBed.inject(HttpTestingController).expectOne('/api/v1/me');
    expect(request.request.method).toBe('PATCH');
    expect(request.request.body).toEqual({ displayName: 'New Name' });
    request.flush(null);
    await fixture.whenStable();
    expect(refresh).toHaveBeenCalledOnce();
  });
});
