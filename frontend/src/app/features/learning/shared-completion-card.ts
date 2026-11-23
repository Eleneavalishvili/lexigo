import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Meta } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  CompletionCardDetails,
  CompletionCardsApi,
} from '../../core/learning/completion-cards-api';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { CompletionCardArt } from '../../shared/completion-card-art/completion-card-art';

@Component({
  selector: 'app-shared-completion-card',
  imports: [RouterLink, BrandLogo, CompletionCardArt],
  template: `<div class="wrap">
    <header>
      <a routerLink="/" aria-label="LexiGo home"><app-brand-logo /></a>
    </header>
    <main>
      @if (loading()) {
        <p role="status">Opening a completed chapter…</p>
      }
      @if (error()) {
        <h1>This card is unavailable.</h1>
        <p role="status">{{ error() }}</p>
        @if (canRetry()) {
          <button class="button button-outline" (click)="load()">Try again</button>
        }
      }
      @if (details(); as card) {
        <app-completion-card-art [details]="card" />
        <p><a routerLink="/reports" [queryParams]="{ card: code }">Report the public name</a></p>
      }
      <p><a routerLink="/courses">Discover your own first words ↗</a></p>
    </main>
  </div>`,
  styles: `
    .wrap {
      max-width: 900px;
      margin: auto;
      padding: clamp(1rem, 4vw, 3rem);
    }
    main {
      margin-top: 3rem;
    }
    p {
      line-height: 1.7;
    }
  `,
})
export class SharedCompletionCardPage {
  private readonly api = inject(CompletionCardsApi);
  private readonly lifetime = inject(DestroyRef);
  protected code = '';
  private generation = 0;
  protected readonly details = signal<CompletionCardDetails | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal('');
  protected readonly canRetry = signal(false);

  constructor() {
    const meta = inject(Meta);
    meta.updateTag({ name: 'robots', content: 'noindex, nofollow, noarchive' });
    this.lifetime.onDestroy(() => meta.removeTag('name="robots"'));
    inject(ActivatedRoute)
      .paramMap.pipe(takeUntilDestroyed())
      .subscribe((params) => {
        this.code = params.get('code') ?? '';
        void this.load();
      });
  }

  protected async load(): Promise<void> {
    const generation = ++this.generation;
    this.details.set(null);
    this.loading.set(true);
    this.error.set('');
    this.canRetry.set(false);
    try {
      const card = await this.api.shared(this.code);
      if (this.current(generation)) this.details.set(card);
    } catch (error) {
      if (!this.current(generation)) return;
      const unavailable = error instanceof HttpErrorResponse && [400, 404].includes(error.status);
      this.canRetry.set(!unavailable);
      this.error.set(
        unavailable
          ? 'It is private, no longer shared, or the link is unavailable.'
          : 'The card could not load. Try again.',
      );
    } finally {
      if (this.current(generation)) this.loading.set(false);
    }
  }
  private current(generation: number): boolean {
    return !this.lifetime.destroyed && generation === this.generation;
  }
}
