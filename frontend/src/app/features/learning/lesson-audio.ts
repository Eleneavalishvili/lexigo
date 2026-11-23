import { HttpErrorResponse } from '@angular/common/http';
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
import { AudioInformation, LearningApi } from '../../core/learning/learning-api';

@Component({
  selector: 'app-lesson-audio',
  templateUrl: './lesson-audio.html',
  styleUrl: './lesson-audio.scss',
})
export class LessonAudio {
  readonly playing = signal(false);
  readonly starting = output<void>();
  readonly sessionId = input.required<string>();
  readonly optionId = input<string | null>(null);
  readonly exerciseId = input.required<string>();
  readonly audio = input.required<AudioInformation>();
  private readonly api = inject(LearningApi);
  private readonly auth = inject(AuthStore);
  private generation = 0;
  private identity = '';
  private expiry?: ReturnType<typeof setTimeout>;
  @ViewChild('player') private player?: ElementRef<HTMLAudioElement>;
  protected readonly url = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly speed = signal(1);

  constructor() {
    effect(() => {
      const identity = JSON.stringify([
        this.sessionId(),
        this.exerciseId(),
        this.optionId(),
        this.auth.user()?.id,
      ]);
      if (identity === this.identity) return;
      this.identity = identity;
      this.close();
      this.error.set('');
      this.speed.set(1);
    });
    inject(DestroyRef).onDestroy(() => this.close());
  }

  protected async open(): Promise<void> {
    if (this.busy() || !this.auth.user()) return;
    this.starting.emit();
    this.close();
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      const link = this.optionId()
        ? await this.api.audio(this.sessionId(), this.exerciseId(), this.optionId())
        : await this.api.audio(this.sessionId(), this.exerciseId());
      if (generation !== this.generation) return;
      this.url.set(link.url);
      this.expiry = setTimeout(
        () => {
          this.close();
          this.error.set('This listening link has expired. Open the recording again to continue.');
        },
        Math.min(300, Math.max(1, link.expiresInSeconds)) * 1000,
      );
    } catch (error) {
      if (generation === this.generation)
        this.error.set(
          error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
            ? error.error.title
            : 'This recording could not be opened. Please try again.',
        );
    } finally {
      if (generation === this.generation) this.busy.set(false);
    }
  }

  stop(): void {
    this.close();
  }

  protected close(): void {
    this.playing.set(false);
    this.generation++;
    if (this.expiry) clearTimeout(this.expiry);
    this.expiry = undefined;
    const player = this.player?.nativeElement;
    if (player) {
      player.pause();
      player.removeAttribute('src');
      player.load();
    }
    this.url.set(null);
    this.busy.set(false);
  }

  protected changeSpeed(event: Event): void {
    const rate = Number((event.target as HTMLSelectElement).value);
    if (![0.65, 0.75, 0.8, 1, 1.25].includes(rate)) return;
    this.speed.set(rate);
    this.applySpeed();
  }

  protected applySpeed(): void {
    if (this.player) {
      this.player.nativeElement.playbackRate = this.speed();
      this.player.nativeElement.preservesPitch = true;
    }
  }
  protected playbackError(): void {
    this.close();
    this.error.set('Your browser could not play this recording. Try opening it again.');
  }
}
