import { Component, effect, inject, input, output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AchievementInput, ManagedAchievement } from '../../core/content/reward-catalog-api';

@Component({
  selector: 'app-achievement-editor',
  imports: [ReactiveFormsModule],
  templateUrl: './achievement-editor.html',
  styleUrl: '../../shared/styles/reward-editor.scss',
})
export class AchievementEditor {
  readonly value = input<ManagedAchievement | null>(null);
  readonly busy = input(false);
  readonly save = output<AchievementInput>();
  readonly archive = output<string>();
  private readonly builder = inject(FormBuilder).nonNullable;
  protected readonly form = this.builder.group({
    code: [
      '',
      [Validators.required, Validators.maxLength(60), Validators.pattern(/^[a-z][a-z0-9-]*$/)],
    ],
    title: ['', [Validators.required, Validators.maxLength(120)]],
    ruleType: ['CompletedLessons', Validators.required],
    threshold: [1, [Validators.required, Validators.min(1), Validators.max(10000)]],
    reason: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(500)]],
  });
  constructor() {
    effect(() => {
      const value = this.value();
      this.form.reset({
        code: value?.code ?? '',
        title: value?.title ?? '',
        ruleType: value?.ruleType ?? 'CompletedLessons',
        threshold: value?.threshold ?? 1,
        reason: '',
      });
    });
  }
  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.valid && !this.busy()) this.save.emit(this.form.getRawValue());
  }
  protected retire(): void {
    const reason = this.form.controls.reason;
    reason.markAsTouched();
    if (reason.valid && !this.busy()) this.archive.emit(reason.value);
  }
}
