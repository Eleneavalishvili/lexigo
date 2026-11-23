import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Pronunciation } from '../../core/learning/pronunciation';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { PronunciationButton } from './pronunciation-button';

export interface RussianLetter {
  upper: string;
  lower: string;
  name: string;
  audioText: string;
  example: string;
  meaning: string;
  note: string;
}

@Component({
  selector: 'app-russian-alphabet',
  imports: [RouterLink, BrandLogo, PronunciationButton],
  templateUrl: './russian-alphabet.html',
  styleUrl: './russian-alphabet.scss',
})
export class RussianAlphabet {
  private readonly http = inject(HttpClient);
  private readonly destroy = inject(DestroyRef);
  protected readonly voice = inject(Pronunciation);
  protected readonly courseId = '10000000-0000-0000-0000-000000000004';
  protected readonly letters = signal<readonly RussianLetter[]>([]);
  protected readonly index = signal(0);
  protected readonly selected = computed(() => this.letters()[this.index()] ?? null);
  protected readonly loading = signal(false);
  protected readonly error = signal('');

  constructor() {
    this.voice.stop();
    void this.load();
    this.destroy.onDestroy(() => this.voice.stop());
  }

  protected async load(): Promise<void> {
    if (this.loading()) return;
    this.loading.set(true);
    this.error.set('');
    try {
      const letters = await firstValueFrom(
        this.http.get<RussianLetter[]>('/teaching/russian-alphabet.json'),
      );
      if (this.destroy.destroyed) return;
      const alphabet = 'АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ';
      if (
        !Array.isArray(letters) ||
        letters.length !== 33 ||
        letters.map((letter) => letter.upper).join('') !== alphabet ||
        letters.some(
          (letter) =>
            letter.lower !== letter.upper.toLowerCase() ||
            !letter.name ||
            !letter.audioText ||
            !letter.example ||
            !letter.meaning,
        )
      )
        throw new Error('Incomplete alphabet reference');
      this.letters.set(letters);
      this.index.set(0);
    } catch {
      if (!this.destroy.destroyed)
        this.error.set('The alphabet could not be opened. Please try again.');
    } finally {
      if (!this.destroy.destroyed) this.loading.set(false);
    }
  }

  protected choose(index: number, speak = true): void {
    if (!this.letters().length) return;
    this.voice.stop();
    this.index.set((index + this.letters().length) % this.letters().length);
    const letter = this.selected();
    if (speak && letter) void this.voice.play(letter.audioText, 'ru');
  }

  protected move(event: KeyboardEvent, index: number): void {
    let next: number;
    switch (event.key) {
      case 'ArrowRight':
        next = index + 1;
        break;
      case 'ArrowLeft':
        next = index - 1;
        break;
      case 'Home':
        next = 0;
        break;
      case 'End':
        next = this.letters().length - 1;
        break;
      default:
        return;
    }
    event.preventDefault();
    this.choose(next, false);
    const button = event.currentTarget as HTMLElement;
    button.parentElement?.querySelectorAll<HTMLButtonElement>('button')[this.index()]?.focus();
  }
}
