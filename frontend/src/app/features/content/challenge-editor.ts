import { Component, effect, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ChallengeInput, ManagedChallenge } from '../../core/content/reward-catalog-api';

@Component({
  selector: 'app-challenge-editor',
  imports: [ReactiveFormsModule],
  templateUrl: './challenge-editor.html',
  styleUrl: '../../shared/styles/reward-editor.scss',
})
export class ChallengeEditor {
  readonly value = input<ManagedChallenge | null>(null);
  readonly busy = input(false);
  readonly save = output<ChallengeInput>();
  readonly archive = output<string>();
  protected readonly error = signal('');
  private readonly builder = inject(FormBuilder).nonNullable;
  protected readonly form = this.builder.group({
    title: ['', [Validators.required, Validators.maxLength(120)]],
    ruleType: ['CompletedLessons', Validators.required],
    target: [3, [Validators.required, Validators.min(1), Validators.max(10000)]],
    rewardXp: [20, [Validators.required, Validators.min(1), Validators.max(1000)]],
    startsAt: ['', Validators.required],
    endsAt: ['', Validators.required],
    reason: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(500)]],
  });
  constructor() {
    effect(() => {
      const value = this.value();
      const start = new Date(Date.now() + 86400000);
      const end = new Date(start.getTime() + 7 * 86400000);
      this.form.reset({
        title: value?.title ?? '',
        ruleType: value?.ruleType ?? 'CompletedLessons',
        target: value?.target ?? 3,
        rewardXp: value?.rewardXp ?? 20,
        startsAt: (value?.startsAt ?? start.toISOString()).slice(0, 16),
        endsAt: (value?.endsAt ?? end.toISOString()).slice(0, 16),
        reason: '',
      });
      this.error.set('');
    });
  }
  protected submit(): void {
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.form.invalid || this.busy()) return;
    const raw = this.form.getRawValue();
    const start = new Date(raw.startsAt + 'Z');
    const end = new Date(raw.endsAt + 'Z');
    if (!Number.isFinite(start.getTime()) || !Number.isFinite(end.getTime()) || end <= start) {
      this.error.set('Choose an end time after the start time.');
      return;
    }
    const original = this.value();
    this.save.emit({
      ...raw,
      startsAt:
        original && raw.startsAt === original.startsAt.slice(0, 16)
          ? original.startsAt
          : start.toISOString(),
      endsAt:
        original && raw.endsAt === original.endsAt.slice(0, 16)
          ? original.endsAt
          : end.toISOString(),
    });
  }
  protected retire(): void {
    const reason = this.form.controls.reason;
    reason.markAsTouched();
    if (reason.valid && !this.busy()) this.archive.emit(reason.value);
  }
}
