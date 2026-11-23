import { Component, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { AuthStore } from '../../core/accounts/auth-store';
import { LearningApi, MistakePage, PracticeFocus } from '../../core/learning/learning-api';

@Component({
  selector: 'app-mistake-practice',
  imports: [DatePipe, RouterLink],
  template: `
    <section aria-labelledby="mistake-title">
      <p class="eyebrow">MAKE IT STICK</p>
      <h3 id="mistake-title">Review the tricky bits.</h3>
      @if (error()) {
        <p role="alert">{{ error() }}</p>
      }
      @if (loading()) {
        <p role="status">Finding your saved mistakes…</p>
      }
      @if (summary(); as value) {
        @if (value.totalCount) {
          <p>
            {{ value.totalCount }} ready for review. Missed questions come first, followed by older
            material.
          </p>
          <ul>
            @for (
              item of expanded() ? value.items : value.items.slice(0, 5);
              track item.exerciseId
            ) {
              <li>
                {{ item.prompt }}
                @if (item.skills?.length) {
                  <span>{{ item.skills?.join(' · ') }}</span>
                }
                @if (item.lastAttemptAt; as attempt) {
                  <span
                    >Last attempted
                    <time [attr.datetime]="attempt">{{ attempt | date: 'medium' }}</time></span
                  >
                }
                @if (item.dueAt; as due) {
                  <span
                    >Due for review · <time [attr.datetime]="due">{{ due | date }}</time></span
                  >
                } @else {
                  <span>
                    {{ item.wrongCount > 1 ? 'Repeated mistake' : 'Needs another try' }} · Missed
                    {{ item.wrongCount }} {{ item.wrongCount === 1 ? 'time' : 'times' }} · Last
                    missed
                    <time [attr.datetime]="item.lastMissedAt">{{ item.lastMissedAt | date }}</time>
                  </span>
                }
              </li>
            }
          </ul>
          @if (!expanded() && value.totalCount > 5) {
            <button type="button" class="button button-outline" (click)="expanded.set(true)">
              Show more review questions
            </button>
          }
          @if (expanded() && value.totalCount > (value.pageSize ?? 20)) {
            <nav class="focus-actions" aria-label="Review question pages">
              <button
                type="button"
                class="button button-outline"
                (click)="load(value.page - 1)"
                [attr.aria-disabled]="busy() || loading() || value.page === 1"
              >
                Previous
              </button>
              <p role="status">Page {{ value.page }}</p>
              <button
                type="button"
                class="button button-outline"
                (click)="load(value.page + 1)"
                [attr.aria-disabled]="
                  busy() || loading() || value.page * (value.pageSize ?? 20) >= value.totalCount
                "
              >
                Next
              </button>
            </nav>
          }
          <p>
            Practise up to ten questions. No hearts lost or XP earned. Original results stay saved.
          </p>
          <button
            type="button"
            class="button button-outline"
            (click)="start()"
            [disabled]="busy() || loading()"
          >
            {{ busy() ? 'Opening practice…' : 'Practise my mistakes ↗' }}
          </button>
        } @else {
          <p>
            No saved mistakes are ready to practise. Finish a learning step to update this list;
            placement checks are separate.
          </p>
        }
        <h4>Focused practice</h4>
        <p>
          Due for review shows questions once their scheduled date arrives. Repeated mistakes shows
          questions you’ve missed at least twice and haven’t corrected yet.
        </p>
        @if (value.advancedPracticeAvailable) {
          <div class="focus-actions">
            <button
              class="button button-outline"
              type="button"
              (click)="start('repeated')"
              [disabled]="busy()"
            >
              Repeated mistakes
            </button>
            <button
              class="button button-outline"
              type="button"
              (click)="start('due')"
              [disabled]="busy()"
            >
              Due for review
            </button>
          </div>
        } @else {
          <a class="button button-outline" routerLink="/pricing">See Premium plans ↗</a>
        }
      }
      @if (error()) {
        <button
          type="button"
          class="button button-outline"
          (click)="load()"
          [disabled]="loading() || busy()"
        >
          Refresh mistakes
        </button>
      }
    </section>
  `,
  styles: `
    section {
      padding: 1.5rem;
      margin: 1.5rem 0;
      border: 2px solid var(--ink);
      border-radius: 1.2rem;
      background: var(--surface);
    }
    h3 {
      font: 700 1.6rem/1.3 var(--display-font);
      margin: 0.5rem 0;
    }
    p,
    li {
      line-height: 1.7;
    }
    .focus-actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.75rem;
    }
    li {
      margin: 0.7rem 0;
    }
    span {
      display: block;
      color: var(--muted);
      font-size: 0.9rem;
    }
  `,
})
export class MistakePractice {
  readonly courseId = input.required<string>();
  private readonly api = inject(LearningApi);
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private generation = 0;
  protected readonly summary = signal<MistakePage | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly expanded = signal(false);
  constructor() {
    effect(() => {
      this.courseId();
      void this.auth.user()?.id;
      this.expanded.set(false);
      this.summary.set(null);
      this.loading.set(false);
      this.busy.set(false);
      untracked(() => void this.load());
    });
  }
  protected async load(page = 1): Promise<void> {
    const currentPage = this.summary();
    if (
      this.loading() ||
      this.busy() ||
      page < 1 ||
      (currentPage &&
        page > 1 &&
        (page - 1) * (currentPage.pageSize ?? 20) >= currentPage.totalCount)
    )
      return;
    const generation = ++this.generation;
    const owner = this.auth.user()?.id;
    const course = this.courseId();
    this.error.set('');
    this.busy.set(false);
    if (!owner) {
      this.loading.set(false);
      return;
    }
    this.loading.set(true);
    try {
      const result = await (page === 1
        ? this.api.mistakes(course)
        : this.api.mistakes(course, page));
      if (this.current(generation, owner, course)) this.summary.set(result);
    } catch {
      if (this.current(generation, owner, course))
        this.error.set('We couldn’t load your saved mistakes. Try again.');
    } finally {
      if (this.current(generation, owner, course)) this.loading.set(false);
    }
  }
  protected async start(focus?: PracticeFocus): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || this.busy() || this.loading()) return;
    const generation = this.generation;
    const course = this.courseId();
    this.busy.set(true);
    this.error.set('');
    try {
      const session = focus
        ? await this.api.practiceMistakes(course, undefined, focus)
        : await this.api.practiceMistakes(course);
      if (this.current(generation, owner, course))
        await this.router.navigate(['/learn/session', session.id]);
    } catch (error) {
      if (this.current(generation, owner, course))
        this.error.set(
          error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
            ? error.error.title
            : 'Practice could not be opened. Try again.',
        );
    } finally {
      if (this.current(generation, owner, course)) this.busy.set(false);
    }
  }
  private current(generation: number, owner: string, course: string): boolean {
    return (
      generation === this.generation &&
      !this.destroy.destroyed &&
      this.auth.user()?.id === owner &&
      this.courseId() === course
    );
  }
}
