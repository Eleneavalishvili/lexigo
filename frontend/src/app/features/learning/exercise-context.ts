import { Component, input } from '@angular/core';

@Component({
  selector: 'app-exercise-context',
  templateUrl: './exercise-context.html',
  styleUrl: './exercise-context.scss',
})
export class ExerciseContext {
  readonly text = input.required<string>();
  readonly kind = input<string>('Reading');
}
