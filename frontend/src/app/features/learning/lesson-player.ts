import { LessonIntro } from './lesson-intro';
import { TranslationPrompt } from './translation-prompt';
import { hasWordPictures } from './word-pictures';
import { ChoiceAnswers } from './choice-answers';
import { teachingWord } from './word-teaching';
import { AnswerFeedbackPanel } from './answer-feedback-panel';
import { LessonNotes } from './lesson-notes';
import { PronunciationButton } from './pronunciation-button';
import { Pronunciation, courseLanguage } from '../../core/learning/pronunciation';
import { AnswerLetters } from './answer-letters';
import {
  MatchedPair,
  ExerciseView,
  SpeechCheckFeedback,
  isChoiceExercise,
} from '../../core/learning/learning-api';
import { SpeakingExercise } from './speaking-exercise';
import { SpokenCheck } from './spoken-check';
import { HeartRecovery } from './heart-recovery';
import { LessonAudio } from './lesson-audio';
import { ExerciseContext } from './exercise-context';
import { MatchingBoard } from './matching-board';
import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { LearningStore } from '../../core/learning/learning-store';
import { SentenceBuilder } from './sentence-builder';
import {
  CharacterCue,
  CharacterMood,
  courseCompanion,
} from '../../shared/character-cue/character-cue';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { LessonCelebration } from '../../shared/lesson-celebration';
import { LessonResultStats } from './lesson-result-stats';
import { LessonHeartLoss } from './lesson-heart-loss';
import { LessonNextStep } from './lesson-next-step';

@Component({
  selector: 'app-lesson-player',
  imports: [
    RouterLink,
    LessonIntro,
    TranslationPrompt,
    ChoiceAnswers,
    AnswerFeedbackPanel,
    LessonNotes,
    PronunciationButton,
    AnswerLetters,
    ReactiveFormsModule,
    BrandLogo,
    LessonCelebration,
    LessonResultStats,
    LessonHeartLoss,
    LessonNextStep,
    SentenceBuilder,
    CharacterCue,
    MatchingBoard,
    ExerciseContext,
    LessonAudio,
    SpeakingExercise,
    SpokenCheck,
    HeartRecovery,
  ],
  providers: [LearningStore],
  templateUrl: './lesson-player.html',
  styleUrl: './lesson-player.scss',
})
export class LessonPlayer {
  protected readonly companionForCourse = courseCompanion;
  protected readonly hasPictures = hasWordPictures;
  private readonly submittedExercise = signal<ExerciseView | null>(null);
  protected displayExercise(): ExerciseView | null {
    const session = this.store.session();
    return this.store.feedback()
      ? (this.submittedExercise() ?? session?.answeredExercise ?? session?.currentExercise ?? null)
      : (session?.currentExercise ?? null);
  }
  protected answerLanguage(exercise: ExerciseView): string {
    return /(?:into|in) English:/u.test(exercise.prompt)
      ? 'en'
      : courseLanguage(this.store.session()?.courseId ?? '');
  }
  private readonly taughtWords = signal<readonly string[]>([]);
  private teachingSession = '';
  protected readonly pendingWord = computed(() => {
    const session = this.store.session();
    if (
      !session?.currentExercise ||
      session.completedAt ||
      session.isPlacement ||
      this.store.feedback()
    )
      return null;
    if (hasWordPictures(session.currentExercise) || this.isLetterListening(session.currentExercise))
      return null;
    const word = teachingWord(
      session.explanation,
      session.currentExercise.prompt,
      session.currentExercise.contextText,
    );
    return word && !this.taughtWords().includes(word.target) ? word : null;
  });
  protected acknowledgeWord(): void {
    const word = this.pendingWord();
    if (word) this.taughtWords.update((words) => [...words, word.target]);
  }
  protected hasTeachingWords(text: string): boolean {
    return text.includes(' — ');
  }
  protected readonly pronunciation = inject(Pronunciation);
  protected isLetterListening(exercise: ExerciseView): boolean {
    return exercise.type === 'Alphabet' && !!exercise.audio;
  }
  protected playChoicePronunciation(exercise: ExerciseView, text: string, courseId: string): void {
    if (!this.isLetterListening(exercise))
      this.pronunciation.play(text, courseLanguage(courseId), true);
  }
  protected readonly courseLanguage = courseLanguage;
  protected readonly isChoice = isChoiceExercise;
  protected readonly exerciseLabels: Readonly<Record<ExerciseView['type'], string>> = {
    MultipleChoice: 'A LITTLE CHOICE',
    Translation: 'PUT IT INTO WORDS',
    FillBlank: 'THE MISSING PIECE',
    SentenceBuilder: 'WORD BY WORD',
    Matching: 'FIND THE CONNECTION',
    Reading: 'READ BETWEEN THE LINES',
    DialogueChoice: 'YOUR NEXT REPLY',
    Alphabet: 'MEET THE CYRILLIC',
    Listening: 'A SECOND LISTEN',
    Speaking: 'FIND YOUR VOICE',
    SpeakingCheck: 'SAY IT OUT LOUD',
  };
  protected readonly expandedMatchingResult = signal<string | null>(null);
  protected readonly speechMood = signal<CharacterMood>('idle');
  protected readonly store = inject(LearningStore);
  protected readonly matchedPairs = signal<readonly MatchedPair[]>([]);
  protected readonly tokenOrder = signal<readonly string[]>([]);
  private readonly builder = inject(FormBuilder).nonNullable;
  protected readonly form = this.builder.group({
    text: ['', Validators.maxLength(1000)],
    option: [''],
  });
  private exerciseId: string | undefined;
  constructor() {
    effect(() => {
      const id = this.store.session()?.id ?? '';
      if (id === this.teachingSession) return;
      this.teachingSession = id;
      this.taughtWords.set([]);
      this.submittedExercise.set(null);
    });
    inject(DestroyRef).onDestroy(() => this.pronunciation.stop());
    effect(() => {
      const id = this.displayExercise()?.id;
      if (id === this.exerciseId) return;
      this.exerciseId = id;
      this.form.reset();
      this.tokenOrder.set([]);
      this.matchedPairs.set([]);
      this.speechMood.set('idle');
    });
    inject(ActivatedRoute)
      .paramMap.pipe(takeUntilDestroyed())
      .subscribe((params) => {
        void this.store.load(params.get('sessionId') ?? '');
      });
  }
  protected async submit(): Promise<void> {
    const exercise = this.store.session()?.currentExercise;
    const value = this.form.getRawValue();
    if (!exercise || this.form.invalid || !this.hasAnswer()) {
      this.form.markAllAsTouched();
      return;
    }
    this.submittedExercise.set(exercise);
    await this.store.answer(value.text, value.option, this.tokenOrder(), this.matchedPairs());
    if (this.store.feedback() && hasWordPictures(exercise)) {
      const word = teachingWord(this.store.session()?.explanation ?? '', exercise.prompt);
      if (word) this.taughtWords.update((words) => [...words, word.target]);
    }
  }
  protected async finishSpeaking(): Promise<void> {
    this.submittedExercise.set(this.store.session()?.currentExercise ?? null);
    await this.store.answer('', '', [], [], true);
  }
  protected async acceptSpeechResult(result: SpeechCheckFeedback): Promise<void> {
    this.submittedExercise.set(this.store.session()?.currentExercise ?? null);
    await this.store.acceptSpeechResult(result);
  }
  protected gradedTotal(): number {
    return this.store.session()?.gradedExerciseCount ?? this.store.session()?.totalCount ?? 0;
  }
  protected async next(): Promise<void> {
    await this.store.next();
  }
  protected hasAnswer(): boolean {
    const exercise = this.store.session()?.currentExercise;
    if (!exercise) return false;
    if (exercise.type === 'Matching')
      return (
        this.matchedPairs().length ===
        exercise.options.filter((option) => option.side === 'left').length
      );
    if (exercise.type === 'SentenceBuilder') return this.tokenOrder().length > 0;
    return isChoiceExercise(exercise)
      ? !!this.form.controls.option.value
      : !!this.form.controls.text.value.trim();
  }
  protected characterMood(): CharacterMood {
    const session = this.store.session();
    if (session?.completedAt) return session.passed ? 'celebration' : 'encouragement';
    const feedback = this.store.feedback();
    if (feedback)
      return feedback.isGraded === false
        ? 'idle'
        : feedback.isCorrect
          ? 'correct'
          : 'encouragement';
    return this.speechMood() !== 'idle'
      ? this.speechMood()
      : session?.answeredCount === 0
        ? 'greeting'
        : 'idle';
  }
  protected elapsed(): string {
    const value = this.store.session();
    if (!value?.completedAt) return '';
    const seconds = Math.max(
      0,
      Math.floor((Date.parse(value.completedAt) - Date.parse(value.startedAt)) / 1000),
    );
    return `${Math.floor(seconds / 60)}m ${seconds % 60}s`;
  }
}
