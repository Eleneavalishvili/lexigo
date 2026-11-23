import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { GuestTrial, GuestTrialApi, TrialFeedback } from '../../core/learning/guest-trial-api';
import { Pronunciation, courseLanguage } from '../../core/learning/pronunciation';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { CharacterCue, courseCompanion } from '../../shared/character-cue/character-cue';
import { ChoiceAnswers } from './choice-answers';
import { LessonIntro } from './lesson-intro';
import { PronunciationButton } from './pronunciation-button';

@Component({
  selector: 'app-guest-trial',
  imports: [RouterLink, BrandLogo, CharacterCue, ChoiceAnswers, LessonIntro, PronunciationButton],
  templateUrl: './guest-trial.html',
  styleUrl: './guest-trial.scss',
})
export class GuestTrialPage {
  private readonly api = inject(GuestTrialApi);
  protected readonly audio = inject(Pronunciation);
  private readonly lifetime = inject(DestroyRef);
  private generation = 0;
  protected readonly courseId = signal('');
  protected readonly trial = signal<GuestTrial | null>(null);
  protected readonly loading = signal(false);
  protected readonly checking = signal(false);
  protected readonly error = signal('');
  protected readonly started = signal(false);
  protected readonly position = signal(0);
  protected readonly correct = signal(0);
  protected readonly feedback = signal<TrialFeedback | null>(null);
  protected readonly answer = new FormControl('', { nonNullable: true });
  protected readonly question = computed(() => this.trial()?.questions[this.position()] ?? null);
  protected readonly companion = computed(() => courseCompanion(this.courseId()));
  protected readonly complete = computed(
    () => !!this.trial() && this.position() >= this.trial()!.questions.length,
  );

  constructor() {
    inject(ActivatedRoute)
      .paramMap.pipe(takeUntilDestroyed())
      .subscribe((params) => {
        this.courseId.set(params.get('courseId') ?? '');
        void this.load();
      });
    this.lifetime.onDestroy(() => {
      ++this.generation;
      this.audio.stop();
    });
  }

  protected async load(): Promise<void> {
    const generation = ++this.generation;
    this.audio.stop();
    this.audio.message.set('');
    this.trial.set(null);
    this.error.set('');
    this.loading.set(true);
    this.checking.set(false);
    this.started.set(false);
    this.position.set(0);
    this.correct.set(0);
    this.feedback.set(null);
    this.answer.reset();
    try {
      const trial = await this.api.read(this.courseId());
      if (generation === this.generation) this.trial.set(trial);
    } catch {
      if (generation === this.generation)
        this.error.set('This trial could not open. Try again or choose another language.');
    } finally {
      if (generation === this.generation) this.loading.set(false);
    }
  }

  protected async check(): Promise<void> {
    const question = this.question();
    const trial = this.trial();
    if (!question || !trial || !this.answer.value || this.checking() || this.feedback()) return;
    const generation = this.generation;
    this.error.set('');
    this.checking.set(true);
    try {
      const feedback = await this.api.check(
        trial.courseId,
        trial.versionId,
        question.id,
        this.answer.value,
      );
      if (generation !== this.generation) return;
      this.feedback.set(feedback);
      if (feedback.isCorrect) this.correct.update((value) => value + 1);
    } catch {
      if (generation === this.generation)
        this.error.set(
          'Your answer could not be checked. Try again. If the lesson changed, restart the trial.',
        );
    } finally {
      if (generation === this.generation) this.checking.set(false);
    }
  }

  protected next(): void {
    if (!this.feedback()) return;
    this.audio.stop();
    this.position.update((value) => value + 1);
    this.feedback.set(null);
    this.answer.reset();
  }

  protected speak(text: string): void {
    void this.audio.play(text, courseLanguage(this.courseId()));
  }
}
