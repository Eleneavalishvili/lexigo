import { Component, inject } from '@angular/core';
import { PreferenceStore } from './core/accounts/preference-store';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly preferences = inject(PreferenceStore);
}
