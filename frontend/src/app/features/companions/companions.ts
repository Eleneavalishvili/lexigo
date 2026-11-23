import { PronunciationButton } from '../learning/pronunciation-button';
import { Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { CharacterCue, CharacterMood } from '../../shared/character-cue/character-cue';

@Component({
  selector: 'app-companions',
  imports: [RouterLink, BrandLogo, CharacterCue, PronunciationButton],
  templateUrl: './companions.html',
  styleUrl: './companions.scss',
})
export class Companions {
  protected readonly mood = signal<CharacterMood>('greeting');
  protected readonly reactions: readonly { mood: CharacterMood; label: string }[] = [
    { mood: 'greeting', label: 'Say hello' },
    { mood: 'correct', label: 'A little win' },
    { mood: 'encouragement', label: 'Try again' },
    { mood: 'celebration', label: 'Celebrate' },
    { mood: 'idle', label: 'Take a breath' },
  ];
}
