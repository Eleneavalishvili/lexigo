import { PronunciationButton } from './pronunciation-button';
import { Component, computed, inject, input } from '@angular/core';
import { Pronunciation, courseLanguage } from '../../core/learning/pronunciation';

@Component({
  selector: 'app-lesson-notes',
  imports: [PronunciationButton],
  template: `
    <div class="word-grid" aria-label="Learn these words first">
      @for (word of words(); track word.target) {
        <button
          type="button"
          (click)="listen(word.target)"
          [attr.aria-label]="'Listen to ' + word.target"
        >
          <strong>{{ word.target }} <span aria-hidden="true">♪</span></strong
          ><span>{{ word.meaning }}</span>
        </button>
      }
    </div>
    @for (sentence of sentences(); track sentence) {
      <div>
        <p>{{ sentence }}</p>
        <app-pronunciation-button [text]="sentence" [courseId]="courseId()" />
      </div>
    }
    <p>{{ explanation() }}</p>
  `,
  styles: `
    .word-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
      gap: 0.6rem;
    }
    button {
      text-align: left;
      border: 1px solid var(--line);
      border-radius: 0.8rem;
      padding: 0.9rem;
      background: var(--surface);
      color: var(--ink);
      cursor: pointer;
      font: inherit;
    }
    strong,
    button > span {
      display: block;
    }
    strong {
      margin-bottom: 0.3rem;
    }
    strong span {
      float: right;
    }
    p {
      white-space: pre-wrap;
      line-height: 1.8;
    }
  `,
})
export class LessonNotes {
  readonly text = input.required<string>();
  readonly courseId = input.required<string>();
  private readonly audio = inject(Pronunciation);
  protected readonly words = computed(() =>
    this.text()
      .split('\n')
      .filter((line) => line.includes(' — '))
      .map((line) => {
        const [target, ...meaning] = line.split(' — ');
        return { target, meaning: meaning.join(' — ') };
      }),
  );
  protected readonly sentences = computed(() => {
    const lines = this.text().split('\n');
    return lines.filter(
      (_, index) =>
        index > 0 && ['A useful phrase', 'Read it in context'].includes(lines[index - 1]),
    );
  });
  protected readonly explanation = computed(() =>
    this.text()
      .split('\n')
      .filter((line) => !line.includes(' — '))
      .join('\n'),
  );
  protected listen(text: string): void {
    void this.audio.play(text, courseLanguage(this.courseId()));
  }
}
