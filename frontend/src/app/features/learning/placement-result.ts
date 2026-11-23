import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthStore } from '../../core/accounts/auth-store';
import { LearningApi, PlacementResult } from '../../core/learning/learning-api';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

@Component({
  selector: 'app-placement-result',
  imports: [RouterLink, BrandLogo],
  template: `
    <main class="placement">
      <a routerLink="/" aria-label="LexiGo home"><app-brand-logo /></a>
      <p class="eyebrow">YOUR NEXT CHAPTER</p>
      <h1>A place to begin.</h1>
      @if (error()) {
        <p role="alert">{{ error() }}</p>
        <button class="button button-outline" (click)="load()" [disabled]="busy()">
          Try again
        </button>
      }
      @if (busy()) {
        <p role="status">Opening your starting point…</p>
      }
      @if (result(); as value) {
        @if (!value.completed) {
          <p>Finish every question to receive your recommendation.</p>
          <a class="button button-red" [routerLink]="['/learn/session', value.sessionId]"
            >Continue the check ↗</a
          >
        } @else {
          <h2>Recommended: {{ value.recommendedTitle }}</h2>
          <p>
            This short check measures the published material sampled today. It is not a CEFR
            certificate. We recommend the first unit where you scored below 80%; if you passed every
            unit, we recommend the last published unit.
          </p>
          <ul>
            @for (unit of value.units; track unit.id) {
              <li>{{ unit.title }} — {{ unit.correct }} / {{ unit.total }}</li>
            }
          </ul>
          <p>
            Applying this start opens the relevant earlier prerequisites. Skipped lessons stay
            uncompleted, earn no XP, and remain available for review. Your existing progress is
            kept.
          </p>
          @if (!value.applied) {
            <button class="button button-red" (click)="apply()" [disabled]="busy()">
              Use this starting point ↗
            </button>
          } @else {
            <p role="status">Your starting point is saved.</p>
          }
          <a
            class="button button-outline"
            [routerLink]="['/learn', value.courseId]"
            [fragment]="value.applied ? 'unit-' + value.recommendedUnitId : undefined"
            >{{ value.applied ? 'Open my course' : 'Keep my current start' }} ↗</a
          >
        }
      }
    </main>
  `,
  styles: `
    .placement {
      max-width: 780px;
      margin: auto;
      padding: 2rem 1.2rem 5rem;
    }
    h1 {
      font: 700 clamp(2rem, 6vw, 4rem)/1.2 var(--display-font);
    }
    h2 {
      font: 700 1.6rem/1.4 var(--display-font);
    }
    p,
    li {
      line-height: 1.8;
    }
    li {
      padding: 0.5rem 0;
      border-bottom: 1px solid var(--line);
    }
    .button {
      margin: 0.5rem 0.5rem 0.5rem 0;
    }
  `,
})
export class PlacementResultPage {
  private readonly api = inject(LearningApi);
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private id = '';
  private generation = 0;
  protected readonly result = signal<PlacementResult | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  constructor() {
    effect(() => {
      if (this.auth.user()?.id === this.owner) return;
      ++this.generation;
      this.result.set(null);
      void this.router.navigate(['/login']);
    });
    inject(ActivatedRoute)
      .paramMap.pipe(takeUntilDestroyed())
      .subscribe((params) => {
        this.id = params.get('sessionId') ?? '';
        void this.load();
      });
  }
  protected async load(): Promise<void> {
    const generation = ++this.generation;
    this.result.set(null);
    this.error.set('');
    this.busy.set(true);
    try {
      const value = await this.api.placementResult(this.id);
      if (this.current(generation)) this.result.set(value);
    } catch {
      if (this.current(generation))
        this.error.set('We couldn’t load this placement result. Try again.');
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }
  protected async apply(): Promise<void> {
    if (this.busy() || !this.result()?.completed || this.result()?.applied) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      const value = await this.api.applyPlacement(this.id);
      if (this.current(generation)) this.result.set(value);
    } catch {
      if (this.current(generation))
        this.error.set(
          'Your starting point could not be confirmed. Retry to check and save the same result.',
        );
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }
  private current(generation: number): boolean {
    return (
      generation === this.generation &&
      !this.destroy.destroyed &&
      this.auth.user()?.id === this.owner
    );
  }
}
