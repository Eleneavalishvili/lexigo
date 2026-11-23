import { AuthStore } from '../../core/accounts/auth-store';
import { RouterLink } from '@angular/router';
import { Component, inject } from '@angular/core';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { HomeHero } from './home-hero';
import { HomeStory } from './home-story';
import { LanguageExplorer } from './language-explorer';
import { WorldGlobe } from './world-globe';

@Component({
  selector: 'app-home',
  imports: [RouterLink, BrandLogo, HomeHero, HomeStory, LanguageExplorer, WorldGlobe],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class Home {
  protected readonly auth = inject(AuthStore);
}
