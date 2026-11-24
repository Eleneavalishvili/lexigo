import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { LANGUAGE_BASICS } from './language-basics-data';

@Component({
  selector: 'app-language-basics',
  imports: [RouterLink, BrandLogo],
  templateUrl: './language-basics.html',
  styleUrl: './language-basics.scss',
})
export class LanguageBasics {
  private readonly route = inject(ActivatedRoute);
  private readonly params = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly code = computed(() => this.params().get('code') ?? '');
  protected readonly guide = computed(() =>
    Object.hasOwn(LANGUAGE_BASICS, this.code()) ? LANGUAGE_BASICS[this.code()] : null,
  );
}
