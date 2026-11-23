import { Component, input, output } from '@angular/core';
import { AnswerFeedback } from '../../core/learning/learning-api';
import { PronunciationButton } from './pronunciation-button';

@Component({
  selector: 'app-answer-feedback-panel',
  imports: [PronunciationButton],
  template: `
    <section
      class="feedback"
      [class.incorrect]="feedback().isGraded !== false && !feedback().isCorrect"
      [class.correct]="feedback().isCorrect"
      role="status"
      aria-live="polite"
    >
      <p class="eyebrow">
        {{
          feedback().isGraded === false
            ? 'PRACTICE SAVED'
            : feedback().isCorrect
              ? '✓ CORRECT'
              : '✕ INCORRECT'
        }}
      </p>
      <h2>
        {{
          feedback().isGraded === false
            ? 'Practice complete'
            : feedback().isCorrect
              ? 'Nice!'
              : 'Incorrect'
        }}
      </h2>
      @if (!feedback().isCorrect && feedback().correctAnswer) {
        <div class="answer-correction">
          <span>Correct answer</span><strong>{{ feedback().correctAnswer }}</strong>
          <app-pronunciation-button [text]="feedback().correctAnswer!" [courseId]="courseId()" />
        </div>
      }
      <p>{{ feedback().feedback }}</p>
      <button
        type="button"
        class="button button-ink"
        (click)="continued.emit()"
        [disabled]="busy()"
      >
        Continue ↗
      </button>
    </section>
  `,
  styles: `
    .feedback {
      padding: clamp(1rem, 3vw, 1.5rem);
      border: 2px solid var(--line);
      border-radius: 1.4rem;
      background: var(--surface);
      max-height: 46dvh;
      overflow-y: auto;
      box-shadow: 0 -4px 20px #00000014;
      animation: feedback-enter 180ms ease-out;
    }
    @media (min-width: 600px) {
      .feedback {
        display: grid;
        grid-template-columns: minmax(0, 1fr) 180px;
        column-gap: 1.5rem;
      }
      .feedback > * {
        grid-column: 1;
      }
      .feedback > .button {
        grid-column: 2;
        grid-row: 1 / 8;
        align-self: center;
        margin: 0;
      }
    }
    .correct {
      border-color: #416944;
      background: color-mix(in srgb, var(--sage) 22%, var(--surface));
    }
    .incorrect {
      border-color: var(--red);
      background: color-mix(in srgb, var(--red) 12%, var(--surface));
    }
    .correct h2,
    .correct .eyebrow {
      color: color-mix(in srgb, var(--ink) 70%, #416944);
    }
    .correct h2 {
      transform-origin: left center;
      animation: correct-pop 240ms ease-out;
    }
    @keyframes correct-pop {
      50% {
        transform: scale(1.12);
      }
    }
    @media (prefers-reduced-motion: reduce) {
      .feedback,
      .correct h2 {
        animation: none;
      }
    }
    .incorrect h2,
    .incorrect .eyebrow {
      color: var(--red);
    }
    h2 {
      margin: 0.3rem 0 1rem;
      font: 700 clamp(1.4rem, 3vw, 1.9rem)/1.3 var(--display-font);
    }
    p {
      line-height: 1.7;
    }
    .answer-correction {
      padding: 1rem 1.2rem;
      border-radius: 0.8rem;
      background: var(--surface);
      border: 1px solid var(--line);
    }
    .answer-correction span {
      display: block;
      font-size: 0.85rem;
      color: var(--muted);
      margin-bottom: 0.4rem;
    }
    strong {
      display: block;
      font-size: 1.35rem;
      white-space: pre-line;
      overflow-wrap: anywhere;
    }
    .button {
      width: 100%;
      min-height: 3.4rem;
      border-radius: 0.9rem;
      margin-top: 1rem;
      justify-content: center;
    }
    @keyframes feedback-enter {
      from {
        transform: translateY(1.5rem);
        opacity: 0;
      }
      to {
        transform: translateY(0);
        opacity: 1;
      }
    }
  `,
})
export class AnswerFeedbackPanel {
  readonly feedback = input.required<AnswerFeedback>();
  readonly courseId = input.required<string>();
  readonly busy = input(false);
  readonly continued = output<void>();
}
