import { PronunciationButton } from '../learning/pronunciation-button';
import { Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

interface LanguagePreview {
  code: string;
  name: string;
  courseId: string;
  greeting: string;
  pronunciation: string;
  translation: string;
  invitation: string;
  topics: readonly string[];
}

@Component({
  selector: 'app-language-explorer',
  imports: [RouterLink, PronunciationButton],
  templateUrl: './language-explorer.html',
  styleUrl: './language-explorer.scss',
})
export class LanguageExplorer {
  protected readonly languages: readonly LanguagePreview[] = [
    {
      code: 'es',
      courseId: '10000000-0000-0000-0000-000000000001',
      name: 'Spanish',
      greeting: 'Hola, mundo.',
      pronunciation: 'OH-lah MOON-doh',
      translation: 'Hello, world.',
      invitation: 'For your next sobremesa.',
      topics: ['Your first hello', 'Coffee & conversation', 'Finding your way'],
    },
    {
      code: 'it',
      courseId: '10000000-0000-0000-0000-000000000002',
      name: 'Italian',
      greeting: 'Ciao, mondo.',
      pronunciation: 'CHOW MOHN-doh',
      translation: 'Hello, world.',
      invitation: 'A little more dolce vita.',
      topics: ['Your first ciao', 'At the neighborhood café', 'People & places'],
    },
    {
      code: 'de',
      courseId: '10000000-0000-0000-0000-000000000003',
      name: 'German',
      greeting: 'Hallo, Welt.',
      pronunciation: 'HAH-loh velt',
      translation: 'Hello, world.',
      invitation: 'Big plans. Small beginnings.',
      topics: ['Your first hallo', 'Everyday little things', 'Out & about'],
    },
    {
      code: 'ru',
      courseId: '10000000-0000-0000-0000-000000000004',
      name: 'Russian',
      greeting: 'Привет, мир.',
      pronunciation: 'pree-VYET meer',
      translation: 'Hello, world.',
      invitation: 'A whole new alphabet awaits.',
      topics: ['Meet the alphabet', 'Your first привет', 'Everyday conversations'],
    },
  ];

  protected readonly selectedLanguage = signal<LanguagePreview>(this.languages[0]);

  protected selectLanguage(language: LanguagePreview): void {
    this.selectedLanguage.set(language);
  }
}
