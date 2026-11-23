import { Component, effect, inject, input, signal } from '@angular/core';
import { Pronunciation } from '../../core/learning/pronunciation';
import { WordView } from '../../core/vocabulary/vocabulary-api';
import { PronunciationButton } from '../learning/pronunciation-button';

@Component({
  selector: 'app-vocabulary-reference',
  imports: [PronunciationButton],
  template: `
    @if (wordAudio()) {
      <app-pronunciation-button [text]="word().word" [courseId]="word().courseId" />
    }
    @if (exampleAudio()) {
      <details>
        <summary>Hear the example</summary>
        <app-pronunciation-button [text]="word().example" [courseId]="word().courseId" />
      </details>
    }
    @if (
      pronunciation.message() &&
      (pronunciation.text() === word().word || pronunciation.text() === word().example)
    ) {
      <p role="status">{{ pronunciation.message() }}</p>
    }
  `,
  styles: `
    details {
      margin-block: 0.5rem;
    }
    summary {
      cursor: pointer;
      color: var(--ink);
    }
    summary:focus-visible {
      outline: 2px solid var(--ink);
      outline-offset: 4px;
    }
  `,
})
export class VocabularyReference {
  readonly word =
    input.required<Pick<WordView, 'word' | 'example' | 'courseId' | 'languageCode' | 'hasAudio'>>();
  protected readonly pronunciation = inject(Pronunciation);
  protected readonly wordAudio = signal(false);
  protected readonly exampleAudio = signal(false);

  constructor() {
    effect((onCleanup) => {
      const word = this.word();
      let current = true;
      onCleanup(() => {
        current = false;
      });
      this.wordAudio.set(false);
      this.exampleAudio.set(false);
      void Promise.all([
        word.hasAudio
          ? Promise.resolve(false)
          : this.pronunciation.available(word.word, word.languageCode),
        this.pronunciation.available(word.example, word.languageCode),
      ]).then(([wordAvailable, exampleAvailable]) => {
        if (!current) return;
        this.wordAudio.set(wordAvailable);
        this.exampleAudio.set(exampleAvailable);
      });
    });
  }
}
