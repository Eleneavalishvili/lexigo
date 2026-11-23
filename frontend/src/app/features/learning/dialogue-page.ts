import {
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { Pronunciation } from '../../core/learning/pronunciation';
import { PronunciationButton } from './pronunciation-button';
import {
  DialogueApi,
  DialogueListing,
  DialogueStep,
  DialogueView,
} from '../../core/learning/dialogue-api';

@Component({
  selector: 'app-dialogue-page',
  imports: [RouterLink, PronunciationButton],
  templateUrl: './dialogue-page.html',
  styleUrl: './dialogue-page.scss',
})
export class DialoguePage {
  private readonly api = inject(DialogueApi);
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  protected readonly audio = inject(Pronunciation);
  private readonly injector = inject(Injector);
  private readonly turnHeading = viewChild<ElementRef<HTMLElement>>('turnHeading');
  private readonly params = toSignal(inject(ActivatedRoute).paramMap);
  protected readonly courseId = computed(() => this.params()?.get('courseId') ?? '');
  protected readonly sessionId = computed(() => this.params()?.get('sessionId') ?? null);
  protected readonly catalog = signal<DialogueListing[]>([]);
  protected readonly finishedCount = computed(
    () => this.catalog().filter((item) => item.hasCompleted).length,
  );
  protected readonly session = signal<DialogueView | null>(null);
  protected readonly feedback = signal<DialogueStep | null>(null);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly pending = signal<{ choiceId: string; key: string } | null>(null);
  private generation = 0;

  constructor() {
    this.destroy.onDestroy(() => this.audio.stop());
    effect(() => {
      const course = this.courseId();
      const session = this.sessionId();
      const owner = this.auth.user()?.id;
      if (owner && course) void this.load(course, session, owner);
      else {
        this.generation++;
        this.session.set(null);
        this.catalog.set([]);
        this.feedback.set(null);
        this.pending.set(null);
        this.error.set('');
        this.loading.set(false);
        this.busy.set(false);
      }
    });
  }

  protected reload(): void {
    const owner = this.auth.user()?.id;
    if (owner) void this.load(this.courseId(), this.sessionId(), owner);
  }

  private async load(course: string, session: string | null, owner: string): Promise<void> {
    const generation = ++this.generation;
    this.audio.stop();
    this.catalog.set([]);
    this.session.set(null);
    this.feedback.set(null);
    this.pending.set(null);
    this.busy.set(false);
    this.error.set('');
    this.loading.set(true);
    try {
      if (session) {
        const result = await this.api.read(session);
        if (this.current(generation, owner)) this.session.set(result);
      } else {
        const result = await this.api.list(course);
        if (this.current(generation, owner)) this.catalog.set(result);
      }
    } catch {
      if (this.current(generation, owner))
        this.error.set('We couldn’t open this conversation. Try again.');
    } finally {
      if (this.current(generation, owner)) this.loading.set(false);
    }
  }

  protected async start(dialogueId: string): Promise<void> {
    const owner = this.auth.user()?.id;
    if (!owner || this.busy()) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      const result = await this.api.start(dialogueId);
      if (this.current(generation, owner))
        await this.router.navigate(['/learn', this.courseId(), 'dialogues', result.id]);
    } catch {
      if (this.current(generation, owner))
        this.error.set('We couldn’t start that conversation. Try again.');
    } finally {
      if (this.current(generation, owner)) this.busy.set(false);
    }
  }

  protected async choose(choiceId: string): Promise<void> {
    const owner = this.auth.user()?.id;
    const session = this.session();
    if (!owner || !session || this.busy() || session.completedAt || this.feedback()) return;
    const existing = this.pending();
    if (existing && existing.choiceId !== choiceId) return;
    const request = existing ?? { choiceId, key: crypto.randomUUID() };
    this.audio.stop();
    const generation = this.generation;
    this.pending.set(request);
    this.busy.set(true);
    this.error.set('');
    try {
      const result = await this.api.choose(session.id, request.choiceId, request.key);
      if (!this.current(generation, owner)) return;
      this.session.set(result);
      this.feedback.set(result.path.at(-1) ?? null);
      this.pending.set(null);
      this.focusTurn();
    } catch (error) {
      if (!this.current(generation, owner)) return;
      const conflict = error instanceof HttpErrorResponse && error.status === 409;
      this.error.set(
        conflict
          ? 'This turn has changed. Reload the conversation.'
          : 'We couldn’t confirm that response. Retry the same choice or reload.',
      );
    } finally {
      if (this.current(generation, owner)) this.busy.set(false);
    }
  }

  protected continue(): void {
    this.audio.stop();
    this.feedback.set(null);
    this.focusTurn();
  }

  private focusTurn(): void {
    const generation = this.generation;
    afterNextRender(
      {
        write: () => {
          if (generation === this.generation) this.turnHeading()?.nativeElement.focus();
        },
      },
      { injector: this.injector },
    );
  }

  private current(generation: number, owner: string): boolean {
    return (
      !this.destroy.destroyed && this.generation === generation && this.auth.user()?.id === owner
    );
  }
}
