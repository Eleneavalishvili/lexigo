import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Pronunciation, courseLanguage } from '../../core/learning/pronunciation';
import { teachingWords } from './word-teaching';

const articles: Readonly<Record<string, Readonly<Record<string, string>>>> = {
  es: {
    un: 'a / an (masculine)',
    una: 'a / an (feminine)',
    el: 'the (masculine)',
    la: 'the (feminine)',
  },
  it: {
    un: 'a / an (masculine)',
    una: 'a / an (feminine)',
    il: 'the (masculine)',
    lo: 'the (masculine)',
    la: 'the (feminine)',
  },
  de: {
    ein: 'a / an (masculine or neuter)',
    eine: 'a / an (feminine)',
    der: 'the (masculine)',
    die: 'the (feminine or plural)',
    das: 'the (neuter)',
  },
};
const normalized = (text: string): string =>
  text
    .normalize('NFC')
    .toLowerCase()
    .replace(/[.!?,]/gu, '')
    .trim();

@Component({
  selector: 'app-translation-prompt',
  template: `
    <h2 id="exercise-title">{{ parts().instruction }}</h2>
    @if (parts().text; as text) {
      <div class="speech" [attr.lang]="sourceLanguage()">
        <button type="button" class="speaker" (click)="listen()" aria-label="Hear this expression">
          ♪
        </button>
        @for (word of text.split(' '); track $index) {
          <button
            type="button"
            class="hint-word"
            [disabled]="!hintsEnabled()"
            [attr.aria-expanded]="selected() === $index"
            (click)="showHint(word, $index)"
          >
            {{ word + ' ' }}
          </button>
        }
      </div>
      @if (selected() !== null && hint()) {
        <p class="meaning" role="status" lang="en">{{ hint() }}</p>
      }
      @if (hintsEnabled()) {
        <p class="hint-help">Tap an underlined word for a meaning.</p>
      }
    }
  `,
  styles: `
    h2 {
      font: 700 clamp(1.4rem, 3vw, 2rem)/1.4 var(--body-font, inherit);
      margin: 0 0 1rem;
    }
    .speech {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 0.4rem;
    }
    .speaker,
    .hint-word {
      font: inherit;
      color: var(--ink);
      border: 0;
      background: transparent;
      padding: 0.25rem;
      cursor: pointer;
    }
    .speaker {
      font-size: 1.8rem;
    }
    .hint-word {
      font-size: 1.4rem;
      border-bottom: 2px dotted var(--ink);
    }
    .hint-word:disabled {
      border-bottom: 0;
      cursor: default;
    }
    .meaning {
      background: var(--paper);
      border: 1px solid var(--line);
      border-radius: 0.8rem;
      padding: 0.7rem 1rem;
      margin: 0.8rem 0 0;
    }
    .hint-help {
      font-size: 0.8rem;
      color: var(--muted);
      margin: 0.5rem 0 0;
    }
  `,
})
export class TranslationPrompt {
  readonly prompt = input.required<string>();
  readonly explanation = input.required<string>();
  readonly courseId = input.required<string>();
  readonly hintsEnabled = input(true);
  protected readonly selected = signal<number | null>(null);
  protected readonly hint = signal('');
  private readonly audio = inject(Pronunciation);
  protected readonly parts = computed(() => {
    const match = this.prompt().match(/^((?:Translate into|Write this in) [^:]+):\s*(.+)$/u);
    return match
      ? { instruction: match[1], text: match[2] }
      : { instruction: this.prompt(), text: '' };
  });
  protected readonly sourceLanguage = computed(() =>
    /(?:into|in) English$/u.test(this.parts().instruction) ? courseLanguage(this.courseId()) : 'en',
  );
  constructor() {
    effect(() => {
      this.prompt();
      this.selected.set(null);
      this.hint.set('');
    });
  }
  protected listen(): void {
    void this.audio.play(this.parts().text, this.sourceLanguage());
  }
  protected showHint(word: string, index: number): void {
    if (!this.hintsEnabled()) return;
    if (this.selected() === index) {
      this.selected.set(null);
      return;
    }
    this.selected.set(index);
    void this.audio.play(word, this.sourceLanguage(), true);
    const key = normalized(word);
    const article = articles[this.sourceLanguage()]?.[key];
    const dictionary = teachingWords(this.explanation());
    const entry =
      dictionary.find((item) => normalized(item.target) === key) ??
      dictionary.find((item) => normalized(item.target).split(' ').at(-1) === key);
    const reverse = dictionary.find(
      (item) => normalized(item.meaning) === normalized(this.parts().text),
    );
    this.hint.set(
      article ??
        (entry
          ? entry.meaning.replace(/^(a|an|the) /iu, '')
          : reverse
            ? 'Expression: ' + reverse.target
            : 'No word hint is available yet. Check the lesson notes.'),
    );
  }
}
