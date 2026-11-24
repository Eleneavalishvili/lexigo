import { DatePipe } from '@angular/common';
import { Component, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { AuthStore } from '../../core/accounts/auth-store';
import { ExtraStudyApi, StudyMessage } from '../../core/classrooms/extra-study-api';

@Component({
  selector: 'app-study-messages',
  imports: [DatePipe, ReactiveFormsModule],
  template: `<section class="panel" aria-labelledby="study-chat-title">
    <h2 id="study-chat-title">Private messages</h2>
    <p>Only you and your accepted teacher or learner can read this conversation.</p>
    <button type="button" [disabled]="loading() || sending()" (click)="load()">
      Refresh messages
    </button>
    @if (nextBefore(); as cursor) {
      <button type="button" [disabled]="loading() || sending()" (click)="load(cursor)">
        Load older messages
      </button>
    }
    @if (error()) {
      <p role="alert">{{ error() }}</p>
    }
    @if (loading()) {
      <p role="status">Opening your conversation…</p>
    }
    <ol class="messages" aria-label="Study conversation">
      @for (message of messages(); track message.id) {
        <li [class.mine]="message.isMine">
          <strong>{{ message.isMine ? 'You' : message.senderName }}</strong>
          <p>{{ message.body }}</p>
          <time [attr.datetime]="message.createdAt">{{ message.createdAt | date: 'short' }}</time>
        </li>
      } @empty {
        <li>No messages yet. Start with a hello.</li>
      }
    </ol>
    <form (submit)="$event.preventDefault(); send()">
      <label for="study-message">Your message</label>
      <textarea
        id="study-message"
        [formControl]="draft"
        maxlength="2000"
        rows="3"
        [readOnly]="!!pending()"
      ></textarea>
      <button class="button button-ink" type="submit" [disabled]="sending() || loading()">
        {{ sending() ? 'Sending…' : pending() ? 'Retry sending the same message' : 'Send message' }}
      </button>
    </form>
  </section>`,
  styleUrl: './classrooms.scss',
  styles: `
    .messages {
      list-style: none;
      padding: 0;
      display: grid;
      gap: 0.75rem;
      max-height: 32rem;
      overflow-y: auto;
    }
    .messages li {
      padding: 0.9rem 1rem;
      border-radius: 1rem;
      background: var(--surface);
      border: 1px solid var(--line);
      max-width: 90%;
    }
    .messages li.mine {
      margin-left: auto;
      background: #edf0e3;
    }
    .messages p {
      white-space: pre-wrap;
      overflow-wrap: anywhere;
      margin-block: 0.4rem;
    }
    time {
      font-size: 0.75rem;
    }
    textarea {
      width: 100%;
      box-sizing: border-box;
      resize: vertical;
      font: inherit;
      padding: 0.75rem;
    }
  `,
})
export class StudyMessages {
  readonly requestId = input.required<string>();
  private readonly api = inject(ExtraStudyApi);
  private readonly auth = inject(AuthStore);
  private readonly lifetime = inject(DestroyRef);
  private generation = 0;
  protected readonly draft = new FormControl('', { nonNullable: true });
  protected readonly messages = signal<StudyMessage[]>([]);
  protected readonly nextBefore = signal<string | null>(null);
  protected readonly loading = signal(false);
  protected readonly sending = signal(false);
  protected readonly error = signal('');
  protected readonly pending = signal<{ id: string; body: string } | null>(null);
  constructor() {
    effect(() => {
      const owner = this.auth.user()?.id;
      const request = this.requestId();
      untracked(() => {
        ++this.generation;
        this.messages.set([]);
        this.nextBefore.set(null);
        this.draft.reset();
        this.pending.set(null);
        this.sending.set(false);
        this.loading.set(false);
        this.error.set('');
        if (owner && request) void this.load();
      });
    });
  }
  protected async load(before?: string): Promise<void> {
    if (this.loading() || this.sending()) return;
    const generation = this.generation;
    this.loading.set(true);
    this.error.set('');
    try {
      const page = await this.api.messages(this.requestId(), before);
      if (!this.current(generation)) return;
      const combined = before ? [...page.items, ...this.messages()] : page.items;
      this.messages.set([...new Map(combined.map((message) => [message.id, message])).values()]);
      this.nextBefore.set(page.nextBefore);
    } catch {
      if (this.current(generation))
        this.error.set('Messages could not load. Refresh to try again.');
    } finally {
      if (this.current(generation)) this.loading.set(false);
    }
  }
  protected async send(): Promise<void> {
    if (this.sending() || this.loading()) return;
    const body = this.draft.value.trim();
    if (!this.pending() && (!body || body.length > 2000)) {
      this.error.set('Write a message of 1–2000 characters.');
      return;
    }
    const generation = this.generation;
    const message = this.pending() ?? { id: crypto.randomUUID(), body };
    this.pending.set(message);
    this.sending.set(true);
    this.error.set('');
    try {
      await this.api.sendMessage(this.requestId(), message);
      if (!this.current(generation)) return;
      this.pending.set(null);
      this.draft.reset();
      this.sending.set(false);
      await this.load();
    } catch {
      if (this.current(generation))
        this.error.set(
          'Sending was not confirmed. Retry to check the same message without duplicating it.',
        );
    } finally {
      if (this.current(generation)) this.sending.set(false);
    }
  }
  private current(generation: number): boolean {
    return !this.lifetime.destroyed && generation === this.generation;
  }
}
