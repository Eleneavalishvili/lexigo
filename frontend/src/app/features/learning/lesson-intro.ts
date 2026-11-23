import { courseLanguage } from '../../core/learning/pronunciation';
import { Component, computed, effect, input, output, signal } from '@angular/core';
import { CharacterCue, courseCompanion } from '../../shared/character-cue/character-cue';
import { PronunciationButton } from './pronunciation-button';

@Component({
  selector: 'app-lesson-intro',
  imports: [CharacterCue, PronunciationButton],
  template: `
    <section class="intro" aria-labelledby="word-learning-title">
      <p class="eyebrow">LEARN FIRST · THEN PRACTISE</p>
      <h2 id="word-learning-title">
        {{ words().length === 1 ? 'One new expression.' : 'Meet your new words.' }}
      </h2>
      <p>Read the meaning and listen. Next, you’ll practise using it.</p>
      @if (words()[position()]; as word) {
        <p class="word-count">New word {{ position() + 1 }} of {{ words().length }}</p>
        <div class="word-card">
          <app-character-cue [character]="companion()" />
          <div>
            <h3 [attr.lang]="language()">{{ word.target }}</h3>
            <p class="meaning">{{ word.meaning }}</p>
            <app-pronunciation-button [text]="word.target" [courseId]="courseId()" />
          </div>
        </div>
        <div class="actions">
          <button
            type="button"
            class="button button-outline"
            [disabled]="position() === 0"
            (click)="position.update(previous)"
          >
            ← Previous word
          </button>
          @if (position() < words().length - 1) {
            <button type="button" class="button button-red" (click)="position.update(next)">
              Next word →
            </button>
          } @else {
            <button type="button" class="button button-red" (click)="ready.emit()">
              {{ words().length === 1 ? 'Now try it →' : 'Start the questions →' }}
            </button>
          }
        </div>
      }
    </section>
  `,
  styles: `
    .intro {
      padding: clamp(1.2rem, 4vw, 2rem);
      border: 2px solid var(--line);
      border-radius: 1.4rem;
      background: var(--surface);
    }
    h2 {
      margin: 0.5rem 0;
      font: 700 clamp(1.6rem, 4vw, 2.2rem)/1.3 var(--display-font);
    }
    p {
      line-height: 1.7;
    }
    .word-count {
      color: var(--muted);
      font-size: 0.9rem;
    }
    .word-card {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 1.5rem;
      padding: 1.5rem 0;
    }
    .word-card app-character-cue {
      width: 140px;
      flex-shrink: 0;
    }
    h3 {
      margin: 0;
      font-size: clamp(1.7rem, 5vw, 2.8rem);
      overflow-wrap: anywhere;
    }
    .meaning {
      font-size: 1.25rem;
      margin: 0.4rem 0;
    }
    .actions {
      display: flex;
      justify-content: space-between;
      gap: 0.8rem;
      flex-wrap: wrap;
    }
    .actions .button {
      border-radius: 0.8rem;
    }
    @media (max-width: 500px) {
      .word-card {
        gap: 0.6rem;
      }
      .word-card app-character-cue {
        width: 90px;
      }
      .actions .button {
        width: 100%;
        justify-content: center;
      }
    }
  `,
})
export class LessonIntro {
  protected readonly language = computed(() => courseLanguage(this.courseId()));
  readonly explanation = input.required<string>();
  readonly courseId = input.required<string>();
  readonly ready = output<void>();
  protected readonly companion = computed(() => courseCompanion(this.courseId()));
  protected readonly position = signal(0);
  protected readonly previous = (value: number): number => Math.max(0, value - 1);
  protected readonly next = (value: number): number => Math.min(this.words().length - 1, value + 1);
  protected readonly words = computed(() =>
    this.explanation()
      .split('\n')
      .filter((line) => line.includes(' — '))
      .map((line) => {
        const [target, ...meaning] = line.split(' — ');
        return { target, meaning: meaning.join(' — ') };
      }),
  );
  constructor() {
    effect(() => {
      this.explanation();
      this.position.set(0);
    });
  }
}
