import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  effect,
  inject,
  Injector,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { AuthStore } from '../../core/accounts/auth-store';
import { ContentApi, PreviewSpeechFeedback } from '../../core/content/content-api';
import {
  AudioInformation,
  LearningApi,
  SpeechCheckFeedback,
} from '../../core/learning/learning-api';
import { VoiceRecorder } from '../../core/learning/voice-recorder';
import { prepareSpeechWave } from '../../core/learning/speech-wave';
import { CharacterMood } from '../../shared/character-cue/character-cue';
import { LessonAudio } from './lesson-audio';

@Component({
  selector: 'app-spoken-check',
  imports: [LessonAudio],
  providers: [VoiceRecorder],
  templateUrl: './spoken-check.html',
  styleUrl: './spoken-check.scss',
})
export class SpokenCheck {
  readonly sessionId = input('');
  readonly exerciseId = input.required<string>();
  readonly audio = input<AudioInformation | null>(null);
  readonly preview = input<{ lessonId: string; versionId: string } | null>(null);
  readonly disabled = input(false);
  readonly misses = input(0);
  readonly practice = input(false);
  readonly unlimited = input(false);
  readonly checked = output<SpeechCheckFeedback>();
  readonly reopen = output<void>();
  readonly activity = output<CharacterMood>();
  protected readonly recorder = inject(VoiceRecorder);
  protected readonly checking = signal(false);
  protected readonly error = signal('');
  protected readonly result = signal<PreviewSpeechFeedback | null>(null);
  private readonly auth = inject(AuthStore);
  private readonly api = inject(LearningApi);
  private readonly injector = inject(Injector);
  private readonly destroy = inject(DestroyRef);
  private readonly reference = viewChild(LessonAudio);
  private identity = '';
  private generation = 0;
  private request: AbortController | null = null;
  private prepared: Blob | null = null;
  private key: string | null = null;

  constructor() {
    effect(() => {
      const identity = this.currentIdentity();
      if (identity !== this.identity) {
        this.identity = identity;
        this.discard();
      }
    });
    effect(() => {
      this.activity.emit(
        this.checking()
          ? 'checking'
          : this.recorder.state() === 'recording'
            ? 'listening'
            : this.reference()?.playing()
              ? 'speaking'
              : this.result()?.status === 'Mismatch'
                ? 'encouragement'
                : 'idle',
      );
    });
    const leaving = () => {
      this.discard();
      this.reference()?.stop();
    };
    const hidden = () => {
      if (document.hidden) leaving();
    };
    window.addEventListener('pagehide', leaving);
    document.addEventListener('visibilitychange', hidden);
    this.destroy.onDestroy(() => {
      leaving();
      window.removeEventListener('pagehide', leaving);
      document.removeEventListener('visibilitychange', hidden);
    });
  }

  protected async record(): Promise<void> {
    if (this.disabled() || this.checking()) return;
    this.reference()?.stop();
    this.discard();
    await this.recorder.start();
  }

  protected beforeReference(): void {
    if (!this.checking()) this.discard();
  }

  protected async check(): Promise<void> {
    if (this.disabled() || this.checking() || !this.auth.user()) return;
    const recording = this.recorder.recording();
    if (!recording && !this.prepared) return;
    const generation = ++this.generation;
    const identity = this.identity;
    this.request = new AbortController();
    const signal = this.request.signal;
    this.checking.set(true);
    this.error.set('');
    this.reference()?.stop();
    try {
      const prepared = this.prepared ?? (await prepareSpeechWave(recording!, signal));
      if (!this.current(generation, identity)) return;
      this.prepared = prepared;
      const preview = this.preview();
      let learnerResult: SpeechCheckFeedback | undefined;
      if (preview) {
        const result = await this.injector
          .get(ContentApi)
          .checkPreviewSpeech(
            preview.lessonId,
            preview.versionId,
            this.exerciseId(),
            prepared,
            signal,
          );
        if (!this.current(generation, identity)) return;
        this.result.set(result);
      } else {
        this.key ??= crypto.randomUUID();
        const result = await this.api.checkSpeech(
          this.sessionId(),
          this.exerciseId(),
          this.key,
          prepared,
          signal,
        );
        if (!this.current(generation, identity)) return;
        this.result.set(result);
        learnerResult = result;
      }
      this.prepared = null;
      this.key = null;
      this.recorder.clear();
      if (learnerResult) this.checked.emit(learnerResult);
    } catch (error) {
      if (!this.current(generation, identity)) return;
      if (error instanceof HttpErrorResponse && error.status === 409) {
        this.prepared = null;
        this.key = null;
        this.recorder.clear();
        this.error.set(
          'This question may have changed. Reopen the saved lesson before recording again.',
        );
      } else {
        this.error.set(
          error instanceof HttpErrorResponse
            ? error.status === 429
              ? 'Take a short break before checking again.'
              : this.preview()
                ? 'Local voice checking could not finish. Try this recording again.'
                : 'The check could not finish. Retry this recording; it will not be charged twice.'
            : error instanceof Error
              ? error.message
              : 'This recording could not be prepared. Try again.',
        );
      }
    } finally {
      if (this.current(generation, identity)) this.checking.set(false);
    }
  }

  protected discard(): void {
    ++this.generation;
    this.request?.abort();
    this.request = null;
    this.prepared = null;
    this.key = null;
    this.recorder.clear();
    this.checking.set(false);
    this.error.set('');
    this.result.set(null);
  }

  private current(generation: number, identity: string): boolean {
    return (
      !this.destroy.destroyed &&
      generation === this.generation &&
      identity === this.identity &&
      identity === this.currentIdentity()
    );
  }

  private currentIdentity(): string {
    return JSON.stringify([
      this.sessionId(),
      this.exerciseId(),
      this.auth.user()?.id,
      this.preview(),
    ]);
  }
}
