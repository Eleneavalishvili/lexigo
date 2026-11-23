import { Component, computed, inject, input, signal } from '@angular/core';
import { Pronunciation, courseLanguage } from '../../core/learning/pronunciation';

export type CharacterMood =
  | 'idle'
  | 'greeting'
  | 'correct'
  | 'encouragement'
  | 'celebration'
  | 'listening'
  | 'checking'
  | 'speaking';
export type Companion = 'mira' | 'nico' | 'zara';

export function courseCompanion(courseId: string): Companion {
  const language = courseLanguage(courseId);
  return language === 'ru' ? 'zara' : language === 'it' ? 'nico' : 'mira';
}

const companions = {
  mira: { name: 'Mira', skin: '#bf7545', hair: '#302936', clothes: '#f6f0e1', accent: '#e5ad36' },
  nico: { name: 'Nico', skin: '#efad7b', hair: '#263447', clothes: '#467b7b', accent: '#e5ad36' },
  zara: { name: 'Zara', skin: '#945437', hair: '#302936', clothes: '#e5ad36', accent: '#467b7b' },
} as const;

@Component({
  selector: 'app-character-cue',
  templateUrl: './character-cue.html',
  styleUrl: './character-cue.scss',
  host: { '[attr.data-paused]': 'paused()' },
})
export class CharacterCue {
  readonly character = input<Companion>('mira');
  readonly mood = input<CharacterMood>('idle');
  // The cast page scopes playback to each demo. A lesson has a single active speaker.
  readonly audioLanguage = input<string>();
  protected readonly paused = signal(false);
  protected readonly profile = computed(() => companions[this.character()]);
  private readonly voice = inject(Pronunciation);
  protected readonly effectiveMood = computed(() =>
    this.voice.speaking() &&
    (!this.audioLanguage() || this.audioLanguage() === this.voice.language())
      ? 'speaking'
      : this.mood(),
  );
  readonly description = computed(() => {
    const moods: Record<CharacterMood, string> = {
      idle: 'ready for your next word',
      greeting: 'waving hello',
      correct: 'cheering your answer',
      encouragement: 'offering encouragement',
      celebration: 'celebrating your progress',
      listening: 'listening while you record',
      checking: 'waiting while your words are checked',
      speaking: 'presenting the reference recording',
    };
    return `${this.profile().name}, ${moods[this.effectiveMood()]}.`;
  });
  protected toggleMotion(): void {
    this.paused.update((value) => !value);
  }
}
