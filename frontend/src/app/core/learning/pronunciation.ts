import { Injectable, signal } from '@angular/core';

interface PronunciationClip {
  file: string;
  text: string;
  language: string;
}
interface PronunciationIndex {
  clips: Record<string, PronunciationClip>;
}

export function courseLanguage(id: string): string {
  const courses: Record<string, string> = {
    '10000000-0000-0000-0000-000000000001': 'es',
    '10000000-0000-0000-0000-000000000002': 'it',
    '10000000-0000-0000-0000-000000000003': 'de',
    '10000000-0000-0000-0000-000000000004': 'ru',
  };
  return courses[id] ?? 'en';
}

@Injectable({ providedIn: 'root' })
export class Pronunciation {
  private index: Promise<PronunciationIndex> | null = null;
  private audio: HTMLAudioElement | null = null;
  private generation = 0;
  readonly speaking = signal(false);
  readonly text = signal('');
  readonly language = signal('en');
  readonly message = signal('');

  async available(text: string | null | undefined, language: string): Promise<boolean> {
    if (!text?.trim()) return false;
    try {
      const index = await this.loadIndex();
      const clip = index.clips[language + '|' + this.normalize(text)];
      return !!clip && /^[a-f0-9]{24}\.wav$/.test(clip.file);
    } catch {
      this.index = null;
      return false;
    }
  }

  private loadIndex(): Promise<PronunciationIndex> {
    this.index ??= fetch('/pronunciation/manifest.json', { credentials: 'same-origin' }).then(
      async (response) => {
        if (!response.ok) throw new Error('Pronunciation unavailable');
        return (await response.json()) as PronunciationIndex;
      },
    );
    return this.index;
  }

  private normalize(text: string): string {
    return text
      .normalize('NFC')
      .trim()
      .toLowerCase()
      .replace(/\s+/g, ' ')
      .replace(/^[.!?¡¿]+|[.!?¡¿]+$/gu, '');
  }

  stop(): void {
    ++this.generation;
    this.audio?.pause();
    this.audio = null;
    this.speaking.set(false);
    this.message.set('');
  }

  async play(text: string, language: string, automatic = false, slow = false): Promise<void> {
    if (automatic && document.documentElement.dataset['soundEnabled'] === 'false') return;
    this.stop();
    const generation = this.generation;
    this.text.set(text);
    this.language.set(language);
    try {
      const index = await this.loadIndex();
      if (generation !== this.generation) return;
      const normalized = this.normalize(text);
      const targetClip = index.clips[language + '|' + normalized];
      const clip = targetClip ?? index.clips['en|' + normalized];
      if (!clip || !/^[a-f0-9]{24}\.wav$/.test(clip.file)) {
        this.message.set('A pronunciation recording is not available for this text yet.');
        return;
      }
      const audio = new Audio('/pronunciation/' + clip.file);
      this.language.set(targetClip ? language : 'en');
      audio.playbackRate = slow ? 0.85 : 1;
      audio.preservesPitch = true;
      this.audio = audio;
      audio.onplaying = () => {
        if (generation === this.generation) this.speaking.set(true);
      };
      audio.onended = () => {
        if (generation === this.generation) this.speaking.set(false);
      };
      audio.onerror = () => {
        if (generation === this.generation) {
          this.speaking.set(false);
          this.message.set('Audio could not play. Tap Listen to try again.');
        }
      };
      await audio.play();
    } catch {
      if (generation === this.generation) {
        this.index = null;
        this.speaking.set(false);
        this.message.set('Audio could not play. Tap Listen to try again.');
      }
    }
  }
}
