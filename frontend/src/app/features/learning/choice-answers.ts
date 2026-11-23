import { Component, input, output } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { AnswerFeedback, LearningOption } from '../../core/learning/learning-api';
import { wordPicture } from './word-pictures';

@Component({
  selector: 'app-choice-answers',
  imports: [ReactiveFormsModule],
  styleUrl: './choice-answers.scss',
  template: `
    <fieldset [disabled]="disabled()">
      <legend class="sr-only">Choose one answer</legend>
      <div class="options" [class.picture-options]="pictures()">
        @for (option of options(); track option.id) {
          <label
            class="option"
            [class.choice-correct]="
              !!feedback() &&
              ((feedback()!.isCorrect && control().value === option.id) ||
                feedback()!.correctAnswer === option.text)
            "
            [class.choice-wrong]="
              !!feedback() && !feedback()!.isCorrect && control().value === option.id
            "
          >
            <input
              type="radio"
              [formControl]="control()"
              [value]="option.id"
              [name]="'choice-' + questionId()"
              (change)="spoken.emit(option.text)"
            />
            @if (pictures() && picture(option.text); as art) {
              <img [src]="art.src" [alt]="art.description" />
            }
            <span>{{ option.text }}</span>
          </label>
        }
      </div>
    </fieldset>
  `,
})
export class ChoiceAnswers {
  readonly options = input.required<readonly LearningOption[]>();
  readonly control = input.required<FormControl<string>>();
  readonly questionId = input.required<string>();
  readonly pictures = input(false);
  readonly disabled = input(false);
  readonly feedback = input<Pick<AnswerFeedback, 'isCorrect' | 'correctAnswer'> | null>(null);
  readonly spoken = output<string>();
  protected readonly picture = wordPicture;
}
