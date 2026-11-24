import { Component, computed, inject, signal } from '@angular/core';
import { Location } from '@angular/common';
import { NavigationEnd, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthStore } from '../../core/accounts/auth-store';

@Component({
  selector: 'app-back-navigation',
  template: `@if (visible()) {
    <nav aria-label="Previous page" class="back-navigation">
      <button
        type="button"
        class="button button-outline"
        (click)="back()"
        aria-label="Go back to the previous page"
      >
        ← Back
      </button>
    </nav>
  }`,
  styles: `
    .back-navigation {
      max-width: 1280px;
      margin-inline: auto;
      padding: 0.65rem clamp(1.25rem, 4vw, 3rem) 0;
    }
    button {
      font-size: 0.8rem;
      min-height: 40px;
      padding: 0.45rem 0.85rem;
    }
  `,
})
export class BackNavigation {
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly auth = inject(AuthStore);
  private readonly url = signal(this.router.url);
  protected readonly visible = computed(() => this.url().split(/[?#]/)[0] !== '/');

  constructor() {
    this.router.events.pipe(takeUntilDestroyed()).subscribe((event) => {
      if (event instanceof NavigationEnd) this.url.set(event.urlAfterRedirects);
    });
  }

  protected back(): void {
    const state = this.location.getState() as { navigationId?: number };
    if ((state?.navigationId ?? 0) > 1) this.location.back();
    else void this.router.navigateByUrl(this.auth.user() ? '/learn' : '/');
  }
}
