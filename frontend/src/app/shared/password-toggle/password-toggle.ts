import { Component, input, model } from '@angular/core';

@Component({
  selector: 'app-password-toggle',
  templateUrl: './password-toggle.html',
  styleUrl: './password-toggle.scss',
})
export class PasswordToggle {
  readonly visible = model(false);
  readonly inputId = input.required<string>();
}
