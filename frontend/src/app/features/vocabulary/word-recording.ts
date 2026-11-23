import { Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { AuthStore } from '../../core/accounts/auth-store';
import { VocabularyApi, WordAudioLink } from '../../core/vocabulary/vocabulary-api';

@Component({
  selector: 'app-word-recording',
  template: `
    @if (clip(); as recording) {
      <audio
        #player
        [src]="recording.url"
        controls
        preload="none"
        (error)="close(); error.set('This recording could not play. Open it again to retry.')"
      >
        Your browser does not support audio playback.
      </audio>
      <button
        type="button"
        class="button button-outline"
        (click)="slow.set(!slow()); player.playbackRate = slow() ? 0.8 : 1"
        [attr.aria-pressed]="slow()"
      >
        Slow playback
      </button>
      <button type="button" class="button button-outline" (click)="close()">Close recording</button>
      <details>
        <summary>Audio source &amp; permission</summary>
        <p>{{ recording.source }}</p>
        <p>{{ recording.license }}</p>
      </details>
    } @else {
      <button type="button" class="button button-outline" [disabled]="busy()" (click)="open()">
        {{ busy() ? 'Opening recording…' : 'Listen to word ▷' }}
      </button>
    }
    @if (error()) {
      <p role="alert">{{ error() }}</p>
    }
  `,
  styles: 'audio { display: block; max-width: 100%; margin-block: .5rem; }',
})
export class WordRecording {
  readonly wordId = input.required<string>();
  private readonly api = inject(VocabularyApi);
  private readonly auth = inject(AuthStore);
  private generation = 0;
  private expiry?: ReturnType<typeof setTimeout>;
  protected readonly clip = signal<WordAudioLink | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly slow = signal(false);

  constructor() {
    effect(() => {
      this.wordId();
      void this.auth.user()?.id;
      this.close();
    });
    inject(DestroyRef).onDestroy(() => this.close());
  }

  protected async open(): Promise<void> {
    if (this.busy() || !this.auth.user()) return;
    const generation = ++this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      const recording = await this.api.audio(this.wordId());
      if (this.generation !== generation) return;
      this.clip.set(recording);
      this.expiry = setTimeout(() => this.close(), recording.expiresInSeconds * 1000);
    } catch {
      if (this.generation === generation) this.error.set('This recording is unavailable.');
    } finally {
      if (this.generation === generation) this.busy.set(false);
    }
  }

  protected close(): void {
    this.generation++;
    clearTimeout(this.expiry);
    this.clip.set(null);
    this.busy.set(false);
    this.error.set('');
    this.slow.set(false);
  }
}
