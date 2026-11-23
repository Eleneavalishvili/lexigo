import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';

interface StudySettingsView {
  defaults: {
    lessonXp: number;
    dialogueXp?: number;
    dailyGoalXp: number;
    audioUploadMiB: number;
    studyDays: number;
    reminderTime: string;
  };
  updatedAt: string | null;
}

@Component({
  selector: 'app-system-settings',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './system-settings.html',
  styleUrl: './system-settings.scss',
})
export class SystemSettingsPage {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private readonly endpoint = '/api/v1/admin/settings';
  private updatedAt: string | null = null;
  protected readonly canView = computed(() => {
    const user = this.auth.user();
    return !!user && user.id === this.owner && user.roles.includes('SuperAdmin');
  });
  protected readonly form = inject(FormBuilder).nonNullable.group({
    lessonXp: [20, [Validators.required, Validators.min(1), Validators.max(1000)]],
    dialogueXp: [10, [Validators.required, Validators.min(0), Validators.max(1000)]],
    dailyGoalXp: [20, [Validators.required, Validators.min(1), Validators.max(1000)]],
    audioUploadMiB: [10, [Validators.required, Validators.min(1), Validators.max(10)]],
    reminderTime: ['18:00', [Validators.required, Validators.pattern(/^\d{2}:\d{2}$/)]],
    reason: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(500)]],
  });
  protected readonly days = [
    'Monday',
    'Tuesday',
    'Wednesday',
    'Thursday',
    'Friday',
    'Saturday',
    'Sunday',
  ];
  protected readonly studyDays = signal(0);
  protected readonly ready = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');

  constructor() {
    void this.reload();
  }

  protected isStudyDay(index: number): boolean {
    return (this.studyDays() & (1 << index)) !== 0;
  }

  protected toggleDay(index: number): void {
    this.studyDays.update((days) => days ^ (1 << index));
  }

  protected async reload(): Promise<void> {
    await this.run(async () => {
      this.ready.set(false);
      this.notice.set('');
      await this.read();
    });
  }

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    if (!this.ready() || this.form.invalid || this.studyDays() === 0) return;
    const { reason, reminderTime, ...values } = this.form.getRawValue();
    await this.run(async () => {
      this.notice.set('');
      await firstValueFrom(
        this.http.put(this.endpoint, {
          defaults: { ...values, reminderTime: reminderTime + ':00', studyDays: this.studyDays() },
          expectedUpdatedAt: this.updatedAt,
          reason: reason.trim(),
        }),
      );
      if (!this.current()) return;
      this.ready.set(false);
      this.notice.set('Settings saved.');
      await this.read();
    });
  }

  private async read(): Promise<void> {
    const view = await firstValueFrom(this.http.get<StudySettingsView>(this.endpoint));
    if (!this.current()) return;
    this.updatedAt = view.updatedAt;
    this.studyDays.set(view.defaults.studyDays);
    this.form.reset({
      ...view.defaults,
      dialogueXp: view.defaults.dialogueXp ?? 10,
      reminderTime: view.defaults.reminderTime.slice(0, 5),
      reason: '',
    });
    this.ready.set(true);
  }

  private async run(action: () => Promise<void>): Promise<void> {
    if (!this.current() || this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await action();
    } catch (error) {
      if (this.current())
        this.error.set(
          error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
            ? error.error.title
            : 'Settings could not be loaded or saved. Try refreshing.',
        );
    } finally {
      if (this.current()) this.busy.set(false);
    }
  }

  private current(): boolean {
    return !this.destroy.destroyed && this.canView();
  }
}
