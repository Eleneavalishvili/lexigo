import {
  Component,
  DestroyRef,
  ElementRef,
  ViewChild,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { AuthStore } from '../../core/accounts/auth-store';
import { AudioInformation } from '../../core/learning/learning-api';
import { VoiceRecorder } from '../../core/learning/voice-recorder';
import { LessonAudio } from './lesson-audio';

@Component({
  selector: 'app-speaking-exercise',
  imports: [LessonAudio],
  providers: [VoiceRecorder],
  templateUrl: './speaking-exercise.html',
  styleUrl: './speaking-exercise.scss',
})
export class SpeakingExercise {
  readonly sessionId = input('');
  readonly exerciseId = input.required<string>();
  readonly audio = input<AudioInformation | null>(null);
  readonly preview = input(false);
  readonly disabled = input(false);
  readonly completed = output<void>();
  protected readonly recorder = inject(VoiceRecorder);
  private readonly auth = inject(AuthStore);
  private identity = '';
  @ViewChild('ownPlayer') private ownPlayer?: ElementRef<HTMLAudioElement>;
  @ViewChild(LessonAudio) private reference?: LessonAudio;
  protected readonly listened = signal(false);
  protected readonly manual = signal(false);
  protected readonly confirmed = signal(false);

  constructor() {
    effect(() => {
      const identity = JSON.stringify([this.sessionId(), this.exerciseId(), this.auth.user()?.id]);
      if (identity === this.identity) return;
      this.identity = identity;
      this.clear();
      this.listened.set(false);
      this.manual.set(false);
      this.confirmed.set(false);
    });
    effect(() => {
      if (!this.recorder.clip()) this.releaseOwnPlayer();
    });
    const leaving = () => {
      this.clear();
      this.reference?.stop();
    };
    const hidden = () => {
      if (document.hidden) leaving();
    };
    window.addEventListener('pagehide', leaving);
    document.addEventListener('visibilitychange', hidden);
    inject(DestroyRef).onDestroy(() => {
      leaving();
      window.removeEventListener('pagehide', leaving);
      document.removeEventListener('visibilitychange', hidden);
    });
  }

  protected async record(): Promise<void> {
    if (this.disabled()) return;
    this.reference?.stop();
    this.releaseOwnPlayer();
    this.listened.set(false);
    this.manual.set(false);
    this.confirmed.set(false);
    await this.recorder.start();
  }

  protected beforeReference(): void {
    this.ownPlayer?.nativeElement.pause();
    if (['recording', 'requesting', 'processing'].includes(this.recorder.state()))
      this.recorder.clear();
  }

  protected beforeOwnPlayback(): void {
    this.reference?.stop();
  }

  protected heardOwnClip(): void {
    this.listened.set(true);
    this.clear();
  }

  protected chooseManual(): void {
    this.clear();
    this.manual.set(!this.manual());
    this.confirmed.set(false);
  }

  protected discard(): void {
    this.clear();
    this.listened.set(false);
    this.confirmed.set(false);
  }

  protected confirm(event: Event): void {
    this.confirmed.set((event.target as HTMLInputElement).checked);
  }

  protected finish(): void {
    if (this.disabled() || !this.confirmed() || !(this.listened() || this.manual())) return;
    this.clear();
    this.reference?.stop();
    this.completed.emit();
  }

  private clear(): void {
    this.releaseOwnPlayer();
    this.recorder.clear();
  }
  private releaseOwnPlayer(): void {
    const player = this.ownPlayer?.nativeElement;
    if (player) {
      player.pause();
      player.removeAttribute('src');
      player.load();
    }
  }
}
