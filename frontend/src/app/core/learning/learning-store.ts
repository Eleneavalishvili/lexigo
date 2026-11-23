import { DestroyRef, Injectable, effect, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { AuthStore } from '../accounts/auth-store';
import {
  AnswerFeedback,
  AnswerInput,
  LearningApi,
  SessionView,
  MatchedPair,
  isChoiceExercise,
  SpeechCheckFeedback,
} from './learning-api';

@Injectable()
export class LearningStore {
  private readonly api = inject(LearningApi);
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private generation = 0;
  private id = '';
  private pending: AnswerInput | null = null;
  private heartLossSequence = 0;
  readonly session = signal<SessionView | null>(null);
  readonly feedback = signal<AnswerFeedback | null>(null);
  readonly heartLoss = signal<number | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly canRetryAnswer = signal(false);

  constructor() {
    effect(() => {
      if (this.auth.user()?.id === this.owner) return;
      ++this.generation;
      this.pending = null;
      this.session.set(null);
      this.feedback.set(null);
      this.heartLoss.set(null);
      this.canRetryAnswer.set(false);
      this.error.set('');
      this.busy.set(false);
      void this.router.navigate(['/login']);
    });
  }

  async reload(): Promise<void> {
    await this.load(this.id);
  }

  async load(id: string): Promise<void> {
    this.id = id;
    const generation = ++this.generation;
    this.session.set(null);
    this.feedback.set(null);
    this.heartLoss.set(null);
    this.pending = null;
    this.canRetryAnswer.set(false);
    this.error.set('');
    this.busy.set(true);
    try {
      const value = await this.api.session(id);
      if (!this.current(generation)) return;
      this.session.set(value);
      this.feedback.set(value.completedAt ? null : (value.lastFeedback?.feedback ?? null));
    } catch {
      if (this.current(generation))
        this.error.set('We couldn’t open your saved lesson. Please try again.');
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }

  async answer(
    text: string,
    selectedOption: string,
    tokenOrder: readonly string[] = [],
    matches: readonly MatchedPair[] = [],
    practiceCompleted = false,
  ): Promise<void> {
    const value = this.session();
    if (this.busy() || this.feedback() || !value?.currentExercise) return;
    this.pending ??= {
      sessionExerciseId: value.currentExercise.id,
      practiceCompleted: value.currentExercise.type === 'Speaking' && practiceCompleted,
      answerText:
        ['Translation', 'FillBlank', 'Listening'].includes(value.currentExercise.type) &&
        !isChoiceExercise(value.currentExercise)
          ? text
          : null,
      selectedOptionIds: isChoiceExercise(value.currentExercise) ? [selectedOption] : [],
      idempotencyKey: crypto.randomUUID(),
      orderedTokenIds: value.currentExercise.type === 'SentenceBuilder' ? [...tokenOrder] : [],
      matchedPairs:
        value.currentExercise.type === 'Matching' ? matches.map((pair) => ({ ...pair })) : [],
    };
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      const feedback = await this.api.answer(value.id, this.pending);
      if (!this.current(generation)) return;
      this.pending = null;
      this.canRetryAnswer.set(false);
      this.feedback.set(feedback);
      if (
        value.mode === 'Lesson' &&
        !value.unlimitedHearts &&
        !feedback.isCorrect &&
        feedback.isGraded !== false &&
        feedback.heartsRemaining < value.hearts
      )
        this.heartLoss.set(++this.heartLossSequence);
      const updated = await this.api.session(value.id);
      if (this.current(generation)) this.session.set(updated);
    } catch (error) {
      if (!this.current(generation)) return;
      if (
        error instanceof HttpErrorResponse &&
        error.status >= 400 &&
        error.status < 500 &&
        error.status !== 429
      ) {
        this.pending = null;
        this.canRetryAnswer.set(false);
        this.error.set('That answer could not be saved. Reopen the saved lesson to continue.');
      } else {
        this.canRetryAnswer.set(this.pending !== null);
        this.error.set(
          this.pending
            ? 'Connection interrupted. Retry the same answer, or reopen the saved lesson.'
            : 'Your answer is saved. Reopen the lesson to load the next question.',
        );
      }
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }

  async next(): Promise<void> {
    if (this.busy()) return;
    // Reload before removing feedback, including after an interrupted post-answer fetch.
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      const updated = await this.api.session(this.id);
      if (!this.current(generation)) return;
      this.session.set(updated);
      this.feedback.set(null);
      this.heartLoss.set(null);
    } catch {
      if (this.current(generation))
        this.error.set('We couldn’t load the next step. Please try again.');
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }
  async acceptSpeechResult(result: SpeechCheckFeedback): Promise<void> {
    if (this.busy() || !this.current(this.generation)) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    const value = this.session();
    const lostHeart =
      value?.mode === 'Lesson' &&
      !value.unlimitedHearts &&
      result.status !== 'Matched' &&
      result.heartsRemaining < value.hearts;
    this.heartLoss.set(lostHeart ? ++this.heartLossSequence : null);
    if (result.answer) this.feedback.set(result.answer);
    try {
      const updated = await this.api.session(this.id);
      if (this.current(generation)) this.session.set(updated);
    } catch {
      if (this.current(generation))
        this.error.set(
          'Your voice check is saved. Reopen the lesson to restore its current state.',
        );
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }

  async complete(): Promise<void> {
    const value = this.session();
    if (!value || this.busy()) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      const result = await this.api.complete(value.id);
      if (!this.current(generation)) return;
      this.session.set(result);
      this.feedback.set(null);
      this.heartLoss.set(null);
    } catch {
      if (this.current(generation))
        this.error.set(
          'Your answers are saved, but we couldn’t finish the lesson. Please try again.',
        );
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }

  private current(generation: number): boolean {
    return (
      !this.destroy.destroyed &&
      generation === this.generation &&
      this.auth.user()?.id === this.owner
    );
  }
}
