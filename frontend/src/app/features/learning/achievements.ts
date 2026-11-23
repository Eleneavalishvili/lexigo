import { DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { AchievementsApi, AchievementView } from '../../core/learning/achievements-api';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

@Component({
  selector: 'app-achievements',
  imports: [RouterLink, BrandLogo, DatePipe],
  templateUrl: './achievements.html',
  styleUrl: './achievements.scss',
})
export class Achievements {
  private readonly api = inject(AchievementsApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private generation = 0;
  protected readonly badges = signal<readonly AchievementView[]>([]);
  protected readonly loading = signal(false);
  protected readonly working = signal(false);
  protected readonly error = signal('');
  protected readonly announcement = signal('');
  protected readonly ready = computed(() => this.badges().some((badge) => badge.canCollect));
  protected readonly earned = computed(
    () => this.badges().filter((badge) => badge.earnedAt).length,
  );

  constructor() {
    effect(() => {
      void this.auth.user()?.id;
      void this.load();
    });
  }

  private current(generation: number, owner: string): boolean {
    return (
      !this.destroy.destroyed && generation === this.generation && this.auth.user()?.id === owner
    );
  }

  protected async load(): Promise<void> {
    const generation = ++this.generation;
    const owner = this.auth.user()?.id;
    this.badges.set([]);
    this.error.set('');
    this.announcement.set('');
    this.working.set(false);
    this.loading.set(!!owner);
    if (!owner) return;
    try {
      const badges = await this.api.get();
      if (this.current(generation, owner)) this.badges.set(badges);
    } catch {
      if (this.current(generation, owner))
        this.error.set('Your badges could not load. Please try again.');
    } finally {
      if (this.current(generation, owner)) this.loading.set(false);
    }
  }

  protected async collect(): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || this.loading() || this.working() || !this.ready()) return;
    const generation = ++this.generation;
    this.working.set(true);
    this.error.set('');
    this.announcement.set('');
    try {
      const awards = await this.api.collect();
      if (!this.current(generation, owner)) return;
      this.announcement.set(
        awards.length
          ? 'Achievement unlocked: ' + awards.map((badge) => badge.title).join(', ') + '.'
          : 'Your badges are already up to date.',
      );
      const badges = await this.api.get();
      if (this.current(generation, owner)) this.badges.set(badges);
    } catch {
      if (this.current(generation, owner))
        this.error.set(
          'We could not refresh your awards. Retry safely; a badge can only be earned once.',
        );
    } finally {
      if (this.current(generation, owner)) this.working.set(false);
    }
  }

  protected description(badge: AchievementView): string {
    switch (badge.ruleType) {
      case 'CompletedLessons':
        return (
          'Successfully complete ' +
          badge.threshold +
          ' lesson' +
          (badge.threshold === 1 ? '.' : 's.')
        );
      case 'GoalStreak':
        return 'Reach your daily XP goal on ' + badge.threshold + ' consecutive days.';
      case 'SavedWords':
        return 'Keep ' + badge.threshold + ' different words in your vocabulary collection.';
      case 'PerfectLesson':
        return 'Finish a lesson with every graded answer correct.';
      default:
        return 'Keep learning to reach this milestone.';
    }
  }

  protected emblem(code: string): string {
    return (
      (
        {
          'first-lesson': '✦',
          'goal-streak': '☀',
          'word-collector': '❀',
          'perfect-lesson': '★',
        } as Record<string, string>
      )[code] ?? '✧'
    );
  }
}
