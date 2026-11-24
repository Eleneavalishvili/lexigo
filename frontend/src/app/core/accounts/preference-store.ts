import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { effect, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthStore } from './auth-store';

export interface StudyPreferences {
  timeZone: string;
  dailyGoalXp: number;
  theme: 'system' | 'light' | 'dark';
  reduceMotion: boolean;
  soundEnabled: boolean;
  leaderboardOptIn: boolean;
  activeCourseId: string | null;
  studyDays: number;
  reminderTime: string;
  emailRemindersEnabled: boolean;
}

@Injectable({ providedIn: 'root' })
export class PreferenceStore {
  private readonly http = inject(HttpClient);
  private readonly document = inject(DOCUMENT);
  private readonly auth = inject(AuthStore);
  private readonly value = signal<StudyPreferences | null>(null);
  readonly preferences = this.value.asReadonly();
  private generation = 0;

  constructor() {
    effect(() => {
      const userId = this.auth.user()?.id;
      const generation = ++this.generation;
      this.value.set(null);
      if (userId) void this.load(generation);
    });
    effect(() => {
      const preferences = this.value();
      const root = this.document.documentElement;
      root.dataset['theme'] = preferences?.theme === 'dark' ? 'dark' : 'light';
      root.dataset['soundEnabled'] = String(preferences?.soundEnabled ?? true);
      root.dataset['reduceMotion'] = String(preferences?.reduceMotion ?? false);
    });
  }

  private async load(generation: number): Promise<void> {
    try {
      const preferences = await this.get();
      if (generation === this.generation) this.value.set(preferences);
    } catch {
      // The settings page offers an explicit retry; offline data is never invented.
    }
  }

  get(): Promise<StudyPreferences> {
    return firstValueFrom(this.http.get<StudyPreferences>('/api/v1/me/preferences'));
  }

  async save(preferences: StudyPreferences): Promise<void> {
    const generation = ++this.generation;
    await firstValueFrom(this.http.put<void>('/api/v1/me/preferences', preferences));
    if (generation === this.generation) this.value.set(preferences);
  }

  courses(): Promise<readonly { id: string; title: string }[]> {
    return firstValueFrom(
      this.http.get<{ id: string; title: string }[]>('/api/v1/me/preferences/courses'),
    );
  }
  async setStudyGoal(dailyGoalXp: number): Promise<void> {
    if (this.value()?.dailyGoalXp === dailyGoalXp) return;
    const generation = ++this.generation;
    await firstValueFrom(this.http.put<void>('/api/v1/me/study-goal', { dailyGoalXp }));
    if (generation === this.generation) {
      this.value.update((value) => (value ? { ...value, dailyGoalXp } : null));
      await this.load(generation);
    }
  }
  async setLeaderboardParticipation(optedIn: boolean, sharePhoto?: boolean): Promise<void> {
    const generation = ++this.generation;
    await firstValueFrom(
      this.http.put<void>('/api/v1/me/leaderboard-participation', { optedIn, sharePhoto }),
    );
    if (generation === this.generation) {
      this.value.update((value) => (value ? { ...value, leaderboardOptIn: optedIn } : null));
      await this.load(generation);
    }
  }
}
