import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { PreferenceStore } from '../../core/accounts/preference-store';
import { LearningApi } from '../../core/learning/learning-api';
import { LeaderboardApi, LeaderboardView } from '../../core/learning/leaderboard-api';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

@Component({
  selector: 'app-leaderboard',
  imports: [RouterLink, BrandLogo],
  templateUrl: './leaderboard.html',
  styleUrl: './leaderboard.scss',
})
export class Leaderboard {
  private readonly auth = inject(AuthStore);
  private readonly http = inject(HttpClient);
  private readonly learning = inject(LearningApi);
  private readonly api = inject(LeaderboardApi);
  private readonly preferences = inject(PreferenceStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private readonly requested = signal<string | null>(null);
  private generation = 0;
  protected readonly courses = signal<readonly { courseId: string; title: string }[]>([]);
  protected readonly courseId = signal('');
  protected readonly board = signal<LeaderboardView | null>(null);
  protected readonly ownPhoto = signal<string | null>(null);
  protected readonly loading = signal(true);
  protected readonly working = signal(false);
  protected readonly error = signal('');
  private week: string | null = null;
  private page = 1;

  constructor() {
    this.route.queryParamMap
      .pipe(takeUntilDestroyed())
      .subscribe((params) => this.requested.set(params.get('courseId')));
    effect(() => {
      void this.auth.user()?.id;
      this.requested();
      void this.initialize();
    });
  }

  private async initialize(): Promise<void> {
    const generation = ++this.generation;
    const owner = this.auth.user()?.id;
    const requested = this.requested();
    this.board.set(null);
    this.ownPhoto.set(null);
    this.courses.set([]);
    this.courseId.set('');
    this.error.set('');
    this.working.set(false);
    this.loading.set(true);
    this.week = null;
    this.page = 1;
    if (!owner) {
      this.loading.set(false);
      return;
    }
    try {
      const result = await this.learning.enrollments(1);
      if (!this.current(generation, owner)) return;
      const courses = result.items;
      this.courses.set(courses);
      const selected = requested ? courses.find((item) => item.courseId === requested) : courses[0];
      if (requested && !selected) throw new Error('Course unavailable');
      if (selected) {
        this.courseId.set(selected.courseId);
        await this.load();
      } else this.loading.set(false);
    } catch {
      if (this.current(generation, owner)) {
        this.error.set(
          'Choose one of your joined courses. Refresh if the course list could not load.',
        );
        this.loading.set(false);
      }
    }
  }

  protected async load(): Promise<void> {
    const generation = ++this.generation;
    const owner = this.auth.user()?.id;
    const course = this.courseId();
    if (!owner || !course) {
      await this.initialize();
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this.ownPhoto.set(null);
    try {
      let value = await this.api.get(course, this.week, this.page);
      if (!this.current(generation, owner, course)) return;
      if (!value.items.length && value.totalCount > 0 && this.page > 1) {
        this.page = Math.ceil(value.totalCount / value.pageSize);
        value = await this.api.get(course, this.week, this.page);
      }
      if (this.current(generation, owner, course)) {
        this.board.set(value);
        if (value.optedIn) {
          void this.loadOwnPhoto(generation, owner, course);
        }
      }
    } catch {
      if (this.current(generation, owner, course)) {
        this.board.set(null);
        this.error.set('We couldn’t refresh the weekly ranking. Try again.');
      }
    } finally {
      if (this.current(generation, owner, course)) this.loading.set(false);
    }
  }

  private async loadOwnPhoto(generation: number, owner: string, course: string): Promise<void> {
    try {
      const photo = await firstValueFrom(
        this.http.get<{ url: string } | null>('/api/v1/me/avatar'),
      );
      if (this.current(generation, owner, course)) this.ownPhoto.set(photo?.url ?? null);
    } catch {
      // The ranking already shows initials while the private photo is unavailable.
    }
  }

  protected chooseCourse(event: Event): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { courseId: (event.target as HTMLSelectElement).value },
    });
  }
  protected changeWeek(direction: number): void {
    const value = this.board();
    if (!value || this.loading() || this.working()) return;
    const date = new Date(value.week + 'T00:00:00Z');
    date.setUTCDate(date.getUTCDate() + direction * 7);
    this.week = date.toISOString().slice(0, 10);
    this.page = 1;
    void this.load();
  }
  protected currentWeek(): void {
    this.week = null;
    this.page = 1;
    void this.load();
  }
  protected changePage(direction: number): void {
    this.page += direction;
    void this.load();
  }
  protected initials(name: string): string {
    return name
      .trim()
      .split(/\s+/u)
      .slice(0, 2)
      .map((word) => Array.from(word)[0] ?? '')
      .join('')
      .toUpperCase();
  }
  protected async participate(sharePhoto?: boolean): Promise<void> {
    const value = this.board();
    const owner = this.auth.user()?.id;
    const course = this.courseId();
    if (!value || !owner || this.working() || this.loading()) return;
    const generation = this.generation;
    this.working.set(true);
    this.error.set('');
    try {
      if (sharePhoto === undefined) {
        await this.preferences.setLeaderboardParticipation(!value.optedIn);
      } else {
        await this.preferences.setLeaderboardParticipation(true, sharePhoto);
      }
      if (this.current(generation, owner, course)) await this.load();
    } catch {
      if (this.current(generation, owner, course))
        this.error.set('Your choice could not be confirmed. Refresh to check its current status.');
    } finally {
      if (!this.destroy.destroyed && this.auth.user()?.id === owner && this.courseId() === course)
        this.working.set(false);
    }
  }
  private current(generation: number, owner: string, course?: string): boolean {
    return (
      !this.destroy.destroyed &&
      generation === this.generation &&
      this.auth.user()?.id === owner &&
      (!course || course === this.courseId())
    );
  }
}
