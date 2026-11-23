import { Component, computed, DestroyRef, inject, input, signal } from '@angular/core';
import { MediaApi } from '../../core/content/media-api';
import { ContentApi, StaffLesson } from '../../core/content/content-api';
import type { LearningOption, MatchedPair } from '../../core/learning/learning-api';
import { MatchingBoard } from '../learning/matching-board';
import { SentenceBuilder } from '../learning/sentence-builder';
import { SpeakingExercise } from '../learning/speaking-exercise';
import { SpokenCheck } from '../learning/spoken-check';

@Component({
  selector: 'app-lesson-preview',
  imports: [MatchingBoard, SentenceBuilder, SpeakingExercise, SpokenCheck],
  templateUrl: './lesson-preview.html',
  styleUrl: './lesson-preview.scss',
})
export class LessonPreview {
  private readonly media = inject(MediaApi);
  private readonly content = inject(ContentApi);
  private readonly destroy = inject(DestroyRef);
  private request = 0;
  private expiry?: ReturnType<typeof setTimeout>;
  protected readonly playback = signal<{ url: string; label: string } | null>(null);
  protected readonly loadingAudio = signal(false);
  protected readonly audioError = signal('');
  readonly lesson = input.required<StaffLesson>();
  protected readonly speechPreview = computed(() => ({
    lessonId: this.lesson().id,
    versionId: this.lesson().versionId,
  }));
  protected readonly index = signal(0);
  protected readonly exercise = computed(() => this.lesson().exercises[this.index()]);
  protected readonly answer = signal('');
  protected readonly tokenOrder = signal<readonly string[]>([]);
  protected readonly pairs = signal<readonly MatchedPair[]>([]);
  protected readonly checking = signal(false);
  protected readonly feedback = signal<{ correct: boolean; explanation: string } | null>(null);
  protected readonly checkError = signal('');
  protected readonly speakingFinished = signal(false);
  protected readonly speakingOpened = signal(false);
  protected readonly choice = computed(() => {
    const exercise = this.exercise();
    return (
      !!exercise?.options.length &&
      ['MultipleChoice', 'Reading', 'DialogueChoice', 'Alphabet', 'Listening'].includes(
        exercise.type,
      )
    );
  });
  protected readonly typed = computed(
    () =>
      ['Translation', 'FillBlank', 'Listening'].includes(this.exercise()?.type ?? '') &&
      !this.choice(),
  );
  protected readonly sentence = computed(() => this.exercise()?.type === 'SentenceBuilder');
  protected readonly matching = computed(() => this.exercise()?.type === 'Matching');
  protected readonly structured = computed(() => this.sentence() || this.matching());
  protected readonly options = computed(() => this.exercise()?.options ?? []);
  protected readonly boardOptions = computed(() => {
    const ordered = [...this.options()].sort((a, b) => (a.orderNumber ?? 0) - (b.orderNumber ?? 0));
    return ordered
      .flatMap((option, index): LearningOption[] =>
        option.id
          ? [
              {
                id: option.id,
                text: option.text,
                side: this.matching() ? (index < ordered.length / 2 ? 'left' : 'right') : null,
              },
            ]
          : [],
      )
      .sort((a, b) => a.text.localeCompare(b.text));
  });
  protected readonly canCheck = computed(() => {
    if (this.sentence()) return this.tokenOrder().length > 0;
    if (this.matching()) {
      return this.pairs().length > 0 && this.pairs().length * 2 === this.boardOptions().length;
    }
    return !!this.answer().trim();
  });

  protected playTile(text: string): void {
    const recording = this.options().find((option) => option.text === text)?.audioMediaId;
    if (recording) void this.listen(recording, text);
  }

  protected move(offset: number): void {
    const next = this.index() + offset;
    if (this.checking() || next < 0 || next >= this.lesson().exercises.length) return;
    this.clearPlayback();
    this.audioError.set('');
    this.answer.set('');
    this.tokenOrder.set([]);
    this.pairs.set([]);
    this.speakingFinished.set(false);
    this.speakingOpened.set(false);
    this.feedback.set(null);
    this.checkError.set('');
    this.checking.set(false);
    this.index.set(next);
  }

  protected async check(): Promise<void> {
    const exercise = this.exercise();
    const answer = this.answer();
    if (!exercise || !this.canCheck() || this.checking()) return;
    const lesson = this.lesson();
    this.checking.set(true);
    this.feedback.set(null);
    this.checkError.set('');
    try {
      const feedback = await this.content.checkPreview(
        lesson.id,
        lesson.versionId,
        exercise.id,
        answer,
        this.tokenOrder(),
        this.pairs(),
      );
      if (!this.destroy.destroyed && this.exercise() === exercise) this.feedback.set(feedback);
    } catch {
      if (!this.destroy.destroyed && this.exercise() === exercise)
        this.checkError.set('Could not check this preview answer.');
    } finally {
      if (!this.destroy.destroyed && this.exercise() === exercise) this.checking.set(false);
    }
  }

  constructor() {
    this.destroy.onDestroy(() => clearTimeout(this.expiry));
  }

  protected async listen(id: string, label: string): Promise<void> {
    this.clearPlayback();
    this.audioError.set('');
    this.loadingAudio.set(true);
    const request = this.request;
    try {
      const link = await this.media.preview(id);
      if (this.destroy.destroyed || this.request !== request) return;
      this.playback.set({ url: link.url, label });
      this.expiry = setTimeout(
        () => this.clearPlayback(),
        Math.min(120, Math.max(1, link.expiresInSeconds)) * 1000,
      );
    } catch {
      if (!this.destroy.destroyed && this.request === request) this.playbackFailed();
    } finally {
      if (!this.destroy.destroyed && this.request === request) this.loadingAudio.set(false);
    }
  }

  protected playbackFailed(): void {
    this.clearPlayback();
    this.audioError.set('The recording could not be opened. Choose Listen to try again.');
  }

  protected openSpeaking(): void {
    this.clearPlayback();
    this.speakingFinished.set(false);
    this.speakingOpened.set(true);
  }

  protected finishSpeaking(): void {
    this.speakingOpened.set(false);
    this.speakingFinished.set(true);
  }

  private clearPlayback(): void {
    this.request++;
    clearTimeout(this.expiry);
    this.playback.set(null);
    this.loadingAudio.set(false);
  }
}
