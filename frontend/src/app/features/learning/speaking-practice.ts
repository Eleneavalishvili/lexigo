import {
  Component,
  DestroyRef,
  ElementRef,
  ViewChild,
  effect,
  inject,
  signal,
} from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { VoiceRecorder } from '../../core/learning/voice-recorder';
import { Pronunciation } from '../../core/learning/pronunciation';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

interface PracticeItem {
  id: number;
  phrase: string;
  choices: string[];
}

interface PracticeFeedback {
  correct: boolean;
  meaning: string;
}

@Component({
  selector: 'app-speaking-practice',
  imports: [RouterLink, BrandLogo],
  providers: [VoiceRecorder],
  templateUrl: './speaking-practice.html',
  styleUrl: './speaking-practice.scss',
})
export class SpeakingPractice {
  protected readonly recorder = inject(VoiceRecorder);
  private readonly auth = inject(AuthStore);
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  protected readonly voice = inject(Pronunciation);
  protected readonly items = signal<PracticeItem[]>([]);
  protected readonly index = signal(0);
  protected readonly feedback = signal<PracticeFeedback | null>(null);
  protected readonly error = signal('');
  protected readonly locked = signal(false);
  protected readonly busy = signal(false);
  protected readonly signedIn = this.auth.user;
  protected readonly language = signal('es');
  private generation = 0;
  @ViewChild('clipPlayer') private clipPlayer?: ElementRef<HTMLAudioElement>;
  private owner = this.auth.user()?.id;
  constructor() {
    effect(() => {
      if (!this.recorder.clip()) this.releasePlayer();
    });
    this.destroy.onDestroy(() => {
      this.releasePlayer();
      this.voice.stop();
    });
    effect(() => {
      const id = this.auth.user()?.id;
      if (id !== this.owner) {
        this.owner = id;
        this.releasePlayer();
        this.recorder.clear();
        this.reset();
      }
    });
  }

  protected async open(language: string): Promise<void> {
    if (!this.auth.user()) return;
    this.reset();
    this.recorder.clear();
    this.language.set(language);
    const generation = this.generation;
    this.busy.set(true);
    try {
      const items = await firstValueFrom(this.http.get<PracticeItem[]>(this.route(language)));
      if (this.current(generation)) this.items.set(items);
    } catch (error) {
      this.showError(error, generation);
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }

  protected async answer(item: PracticeItem, choice: string): Promise<void> {
    if (this.busy() || this.feedback()) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      const feedback = await firstValueFrom(
        this.http.post<PracticeFeedback>(`${this.route(this.language())}/${item.id}/answers`, {
          choice,
        }),
      );
      if (this.current(generation)) this.feedback.set(feedback);
    } catch (error) {
      this.showError(error, generation);
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }

  protected next(): void {
    this.voice.stop();
    this.recorder.clear();
    this.feedback.set(null);
    this.index.update((index) => index + 1);
  }

  private reset(): void {
    ++this.generation;
    this.voice.stop();
    this.items.set([]);
    this.feedback.set(null);
    this.index.set(0);
    this.error.set('');
    this.locked.set(false);
    this.busy.set(false);
  }

  private route(language: string): string {
    return '/api/v1/me/voice-practice/' + encodeURIComponent(language);
  }

  private current(generation: number): boolean {
    return (
      !this.destroy.destroyed &&
      generation === this.generation &&
      this.auth.user()?.id === this.owner
    );
  }

  private showError(error: unknown, generation: number): void {
    if (!this.current(generation)) return;
    if (error instanceof HttpErrorResponse && error.status === 403) {
      this.reset();
      this.locked.set(true);
    } else {
      this.error.set('We couldn’t load voice practice. Try again.');
    }
  }
  private releasePlayer(): void {
    const player = this.clipPlayer?.nativeElement;
    if (player) {
      player.pause();
      player.removeAttribute('src');
      player.load();
    }
  }
}
