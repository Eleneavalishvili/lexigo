import { DOCUMENT } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from './auth-store';
import { PreferenceStore, StudyPreferences } from './preference-store';

describe('Saved study preferences', () => {
  const user = signal<{ id: string } | null>(null);
  const value: StudyPreferences = {
    timeZone: 'Asia/Tbilisi',
    dailyGoalXp: 30,
    theme: 'dark',
    reduceMotion: true,
    soundEnabled: false,
    leaderboardOptIn: false,
    activeCourseId: null,
    studyDays: 31,
    reminderTime: '18:00:00',
    emailRemindersEnabled: false,
  };
  beforeEach(() => {
    user.set(null);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthStore, useValue: { user } },
      ],
    });
  });
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('loads account settings and applies appearance and reduced motion', async () => {
    const store = TestBed.inject(PreferenceStore);
    user.set({ id: 'first' });
    TestBed.tick();
    TestBed.inject(HttpTestingController).expectOne('/api/v1/me/preferences').flush(value);
    await Promise.resolve();
    TestBed.tick();
    expect(store.preferences()).toEqual(value);
    expect(TestBed.inject(DOCUMENT).documentElement.dataset['theme']).toBe('dark');
    expect(TestBed.inject(DOCUMENT).documentElement.dataset['reduceMotion']).toBe('true');
  });

  it('keeps legacy system preferences on daylight rather than enabling dark mode automatically', async () => {
    TestBed.inject(PreferenceStore);
    user.set({ id: 'first' });
    TestBed.tick();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/me/preferences')
      .flush({ ...value, theme: 'system' });
    await Promise.resolve();
    TestBed.tick();
    expect(TestBed.inject(DOCUMENT).documentElement.dataset['theme']).toBe('light');
  });

  it('does not apply a response from an account that has signed out', async () => {
    const store = TestBed.inject(PreferenceStore);
    user.set({ id: 'first' });
    TestBed.tick();
    const pending = TestBed.inject(HttpTestingController).expectOne('/api/v1/me/preferences');
    user.set(null);
    TestBed.tick();
    pending.flush(value);
    await Promise.resolve();
    TestBed.tick();
    expect(store.preferences()).toBeNull();
    expect(TestBed.inject(DOCUMENT).documentElement.dataset['theme']).toBe('light');
  });

  it('applies saves only after server acceptance and preserves the previous value on failure', async () => {
    const store = TestBed.inject(PreferenceStore);
    TestBed.tick();
    const save = store.save(value);
    expect(store.preferences()).toBeNull();
    TestBed.inject(HttpTestingController).expectOne('/api/v1/me/preferences').flush(null);
    await save;
    TestBed.tick();
    const failed = store.save({ ...value, theme: 'light' });
    const rejection = expect(failed).rejects.toBeTruthy();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/me/preferences')
      .flush({}, { status: 400, statusText: 'Bad Request' });
    await rejection;
    expect(store.preferences()).toEqual(value);
  });
});
