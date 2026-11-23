import { DatePipe, PercentPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { SkillMapApi, SkillMapEntry } from '../../core/learning/skill-map-api';
import { LearningApi } from '../../core/learning/learning-api';

@Component({
  selector: 'app-skill-map',
  imports: [DatePipe, PercentPipe],
  template: `
    <section aria-labelledby="skills-heading">
      <h3 id="skills-heading">Your skills map</h3>
      <p>See how your graded answers add up in this course.</p>
      @if (practiceError()) {
        <p role="alert">{{ practiceError() }}</p>
      }
      @if (failed()) {
        <p role="alert">We couldn’t load your skills.</p>
        <button type="button" class="button button-outline" (click)="refresh()">Try again</button>
      } @else if (entries(); as skills) {
        @if (!skills.length) {
          <p>This course doesn’t have skill categories yet.</p>
        }
        <ul>
          @for (skill of skills; track skill.id) {
            <li>
              <strong>{{ skill.name }}</strong>
              @if (skill.lastPracticedAt) {
                <span>
                  {{ skill.correctCount / (skill.correctCount + skill.incorrectCount) | percent }}
                  correct · {{ skill.correctCount + skill.incorrectCount }} answers
                </span>
                <span>Last practised {{ skill.lastPracticedAt | date }}</span>
                @if (skill.incorrectCount > skill.correctCount) {
                  <span class="review">Could use more practice</span>
                }
              } @else {
                <span>Not yet practised</span>
              }
              <button
                type="button"
                class="button button-outline"
                [disabled]="opening() !== null"
                (click)="practice(skill.id)"
              >
                {{ opening() === skill.id ? 'Opening…' : 'Review ' + skill.name + ' mistakes' }}
              </button>
            </li>
          }
        </ul>
      } @else {
        <p role="status">Loading your skills…</p>
      }
    </section>
  `,
  styles: `
    section {
      margin: 1.5rem 0;
    }
    ul {
      padding: 0;
      list-style: none;
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(min(100%, 13rem), 1fr));
      gap: 0.75rem;
    }
    li {
      display: grid;
      gap: 0.35rem;
      padding: 1rem;
      border: 1px solid var(--ink);
      border-radius: 0.9rem;
      background: var(--surface);
    }
    .review {
      font-weight: 700;
    }
  `,
})
export class SkillMap {
  readonly courseId = input.required<string>();
  private readonly api = inject(SkillMapApi);
  private readonly auth = inject(AuthStore);
  private readonly learning = inject(LearningApi);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private readonly reload = signal(0);
  protected readonly entries = signal<SkillMapEntry[] | null>(null);
  protected readonly failed = signal(false);
  protected readonly opening = signal<number | null>(null);
  protected readonly practiceError = signal('');
  private generation = 0;

  constructor() {
    effect((onCleanup) => {
      const course = this.courseId();
      const owner = this.auth.user()?.id;
      this.reload();
      this.generation++;
      this.opening.set(null);
      this.practiceError.set('');
      this.entries.set(null);
      this.failed.set(false);
      if (!owner) return;

      const request = this.api.read(course).subscribe({
        next: (entries) => {
          if (this.auth.user()?.id === owner && this.courseId() === course)
            this.entries.set(entries);
        },
        error: () => {
          if (this.auth.user()?.id === owner && this.courseId() === course) this.failed.set(true);
        },
      });
      onCleanup(() => request.unsubscribe());
    });
  }

  protected refresh(): void {
    this.reload.update((value) => value + 1);
  }

  protected async practice(skillId: number): Promise<void> {
    const owner = this.auth.user()?.id;
    const course = this.courseId();
    const generation = this.generation;
    if (!owner || this.opening() !== null) return;
    const current = () =>
      !this.destroy.destroyed &&
      this.generation === generation &&
      this.auth.user()?.id === owner &&
      this.courseId() === course;
    this.opening.set(skillId);
    this.practiceError.set('');
    try {
      const session = await this.learning.practiceMistakes(course, skillId);
      if (current()) await this.router.navigate(['/learn/session', session.id]);
    } catch (error) {
      if (current())
        this.practiceError.set(
          error instanceof HttpErrorResponse &&
            error.status === 409 &&
            typeof error.error?.title === 'string'
            ? error.error.title
            : 'Practice could not be opened. Try again.',
        );
    } finally {
      if (current()) this.opening.set(null);
    }
  }
}
