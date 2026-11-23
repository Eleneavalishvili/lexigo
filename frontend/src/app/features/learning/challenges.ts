import { DatePipe } from '@angular/common';
import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { ChallengesApi, ChallengeView } from '../../core/learning/challenges-api';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

@Component({
  selector: 'app-challenges',
  imports: [RouterLink, BrandLogo, DatePipe],
  templateUrl: './challenges.html',
  styleUrl: './challenges.scss',
})
export class Challenges {
  private readonly api = inject(ChallengesApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private generation = 0;
  protected readonly items = signal<readonly ChallengeView[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal('');

  constructor() {
    effect(() => {
      void this.auth.user()?.id;
      void this.load();
    });
  }

  protected async load(): Promise<void> {
    const generation = ++this.generation;
    const owner = this.auth.user()?.id;
    this.items.set([]);
    this.error.set('');
    this.loading.set(!!owner);
    if (!owner) return;
    const current = () =>
      !this.destroy.destroyed && this.generation === generation && this.auth.user()?.id === owner;
    try {
      const items = await this.api.get();
      if (current()) this.items.set(items);
    } catch {
      if (current()) this.error.set('Your challenges could not load. Please try again.');
    } finally {
      if (current()) this.loading.set(false);
    }
  }

  protected description(item: ChallengeView): string {
    return item.ruleType === 'CompletedLessons'
      ? 'Successfully complete ' + item.target + ' new lessons during this challenge.'
      : 'Complete a new lesson on ' +
          item.target +
          ' different UTC calendar days during this challenge.';
  }
}
