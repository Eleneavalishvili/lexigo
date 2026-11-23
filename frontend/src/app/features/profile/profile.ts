import { Component, DestroyRef, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { AccountApi } from '../../core/accounts/account-api';
import { PreferenceStore } from '../../core/accounts/preference-store';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { ProfileAvatar } from './profile-avatar';

@Component({
  selector: 'app-profile',
  imports: [ReactiveFormsModule, RouterLink, BrandLogo, ProfileAvatar],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class Profile {
  private readonly preferences = inject(PreferenceStore);
  private readonly auth = inject(AuthStore);
  private readonly account = inject(AccountApi);
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly builder = inject(FormBuilder).nonNullable;
  protected readonly loading = signal(true);
  protected readonly loaded = signal(false);
  protected readonly working = signal(false);
  protected readonly exporting = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly courses = signal<readonly { id: string; title: string }[]>([]);
  protected readonly days = [
    'Monday',
    'Tuesday',
    'Wednesday',
    'Thursday',
    'Friday',
    'Saturday',
    'Sunday',
  ];
  protected readonly zones = Intl.supportedValuesOf('timeZone');
  protected readonly name = this.builder.control(this.auth.user()?.displayName ?? '', [
    Validators.required,
    Validators.minLength(2),
    Validators.maxLength(80),
  ]);
  protected readonly nameForm = this.builder.group({ displayName: this.name });
  protected readonly form = this.builder.group({
    timeZone: ['UTC', Validators.required],
    dailyGoalXp: [20, [Validators.required, Validators.min(1), Validators.max(1000)]],
    activeCourseId: [''],
    studyDays: [31, [Validators.min(1), Validators.max(127)]],
    reminderTime: ['18:00', Validators.required],
    theme: this.builder.control<'system' | 'light' | 'dark'>('light'),
    reduceMotion: [false],
    soundEnabled: [true],
    leaderboardOptIn: [false],
    emailRemindersEnabled: [false],
  });

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const [preferences, courses] = await Promise.all([
        this.preferences.get(),
        this.preferences.courses(),
      ]);
      if (this.destroyRef.destroyed) return;
      this.courses.set(courses);
      this.loaded.set(true);
      this.form.reset({
        ...preferences,
        activeCourseId: preferences.activeCourseId ?? '',
        reminderTime: preferences.reminderTime.slice(0, 5),
      });
    } catch {
      if (!this.destroyRef.destroyed)
        this.error.set('We couldn’t load your settings. Please try again.');
    } finally {
      if (!this.destroyRef.destroyed) this.loading.set(false);
    }
  }

  protected selectedDay(index: number): boolean {
    return (this.form.controls.studyDays.value & (1 << index)) !== 0;
  }

  protected toggleDay(index: number): void {
    const control = this.form.controls.studyDays;
    control.setValue(control.value ^ (1 << index));
    control.markAsTouched();
  }

  protected async savePreferences(): Promise<void> {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    await this.perform(async () => {
      const value = this.form.getRawValue();
      await this.preferences.save({
        ...value,
        activeCourseId: value.activeCourseId || null,
        reminderTime: value.reminderTime + ':00',
      });
      this.notice.set('Your study settings are saved. Make a little space for curiosity.');
    });
  }

  protected async saveName(): Promise<void> {
    this.name.markAsTouched();
    if (this.name.invalid) return;
    await this.perform(async () => {
      await firstValueFrom(this.http.patch('/api/v1/me', { displayName: this.name.value.trim() }));
      await this.auth.refresh();
      this.notice.set('Your display name is saved.');
    });
  }

  protected async downloadLearningData(vocabularyOnly = false): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || this.exporting()) return;
    this.exporting.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      const data = await this.account.exportLearningData(vocabularyOnly);
      if (this.destroyRef.destroyed || this.auth.user()?.id !== owner) return;
      const url = URL.createObjectURL(data);
      const link = document.createElement('a');
      link.href = url;
      link.download = vocabularyOnly ? 'lexigo-vocabulary.json' : 'lexigo-learning-data.json';
      link.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
      this.notice.set('Your download is ready.');
    } catch {
      if (!this.destroyRef.destroyed && this.auth.user()?.id === owner)
        this.error.set('Your data could not be downloaded. Try again.');
    } finally {
      if (!this.destroyRef.destroyed) this.exporting.set(false);
    }
  }

  private async perform(operation: () => Promise<void>): Promise<void> {
    if (this.working()) return;
    this.working.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      await operation();
    } catch {
      this.error.set('We couldn’t save that change. Check your settings and try again.');
    } finally {
      this.working.set(false);
    }
  }
}
