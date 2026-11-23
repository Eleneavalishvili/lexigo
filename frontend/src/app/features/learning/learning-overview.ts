import { Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { LearningApi, LearningDashboard } from '../../core/learning/learning-api';
import { CourseFlag } from '../../shared/course-flag';

@Component({
  selector: 'app-learning-overview',
  imports: [RouterLink, RouterLinkActive, CourseFlag],
  templateUrl: './learning-overview.html',
  styleUrl: './learning-overview.scss',
})
export class LearningOverview {
  readonly courseId = input<string | null>(null);
  private readonly api = inject(LearningApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private generation = 0;
  private receivedAt = 0;
  private lastAttemptAt = 0;
  private expiredRefill = '';
  protected readonly value = signal<LearningDashboard | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal('');
  private readonly now = signal(Date.now());
  protected readonly countdown = computed(() => {
    const hearts = this.value()?.hearts;
    if (!hearts?.refillAt) return '';
    const serverNow = Date.parse(hearts.serverNow) + this.now() - this.receivedAt;
    const seconds = Math.max(0, Math.ceil((Date.parse(hearts.refillAt) - serverNow) / 1000));
    return `${Math.floor(seconds / 60)}:${(seconds % 60).toString().padStart(2, '0')}`;
  });

  constructor() {
    effect(() => {
      void this.auth.user()?.id;
      this.courseId();
      this.value.set(null);
      this.expiredRefill = '';
      void this.load();
    });
    const timer = setInterval(() => {
      this.now.set(Date.now());
      if (document.visibilityState === 'hidden' || this.loading()) return;
      const refill = this.value()?.hearts.refillAt;
      const expired = !!refill && this.countdown() === '0:00' && refill !== this.expiredRefill;
      if (expired) this.expiredRefill = refill;
      if (expired || Date.now() - this.lastAttemptAt > 60_000) void this.load();
    }, 1000);
    this.destroy.onDestroy(() => clearInterval(timer));
  }

  protected async load(): Promise<void> {
    const generation = ++this.generation;
    const owner = this.auth.user()?.id;
    const course = this.courseId();
    this.error.set('');
    this.lastAttemptAt = Date.now();
    if (!owner) {
      this.value.set(null);
      this.loading.set(false);
      return;
    }
    this.loading.set(true);
    try {
      const result = await this.api.dashboard(course);
      if (this.current(generation, owner, course)) {
        this.receivedAt = Date.now();
        this.now.set(this.receivedAt);
        this.value.set(result);
      }
    } catch {
      if (this.current(generation, owner, course))
        this.error.set('Your learning stats couldn’t refresh.');
    } finally {
      if (this.current(generation, owner, course)) this.loading.set(false);
    }
  }

  private current(generation: number, owner: string, course: string | null): boolean {
    return (
      !this.destroy.destroyed &&
      generation === this.generation &&
      this.auth.user()?.id === owner &&
      this.courseId() === course
    );
  }
}
