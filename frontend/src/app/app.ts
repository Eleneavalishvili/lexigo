import { Component, inject } from '@angular/core';
import { PreferenceStore } from './core/accounts/preference-store';
import { RouterOutlet } from '@angular/router';
import { BackNavigation } from './shared/back-navigation/back-navigation';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, BackNavigation],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly preferences = inject(PreferenceStore);
}
