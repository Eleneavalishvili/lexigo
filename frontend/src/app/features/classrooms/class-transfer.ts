import { DatePipe } from '@angular/common';
import { Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthStore } from '../../core/accounts/auth-store';
import { ClassroomApi } from '../../core/classrooms/classroom-api';

@Component({
  selector: 'app-class-transfer',
  imports: [ReactiveFormsModule, DatePipe],
  template: `
    <section class="panel" aria-labelledby="handover-title">
      <h2 id="handover-title">Hand over this class</h2>
      <p>
        The receiving teacher must have two-factor authentication and accept your link. You remain
        responsible until they accept. Saving class changes invalidates outstanding links.
      </p>
      <form (submit)="$event.preventDefault(); offer()">
        <label for="handover-email">Receiving teacher’s email</label>
        <input id="handover-email" type="email" autocomplete="off" [formControl]="email" />
        <button class="button button-outline" [disabled]="busy() || email.invalid">
          Create handover link
        </button>
      </form>
      @if (error()) {
        <p role="alert">{{ error() }}</p>
      }
      @if (link(); as value) {
        <p>
          Send this link to the receiving teacher. It expires {{ expiresAt() | date: 'medium' }}.
        </p>
        <label for="handover-link">Class handover link</label>
        <input id="handover-link" [value]="value" readonly (focus)="$any($event.target).select()" />
      }
    </section>
  `,
  styles: `
    .panel {
      margin-block: 1.5rem;
      border: 1px solid var(--line);
      border-radius: 1rem;
      padding: 1rem;
    }
    form {
      display: grid;
      gap: 0.7rem;
    }
    input {
      display: block;
      width: 100%;
      padding: 0.7rem;
      background: var(--paper);
      color: var(--ink);
      border: 1px solid var(--line);
      border-radius: 0.4rem;
      font: inherit;
    }
  `,
})
export class ClassTransfer {
  readonly classId = input.required<string>();
  private readonly api = inject(ClassroomApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private generation = 0;
  protected readonly email = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.email, Validators.maxLength(256)],
  });
  protected readonly link = signal('');
  protected readonly expiresAt = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  constructor() {
    effect(() => {
      this.classId();
      void this.auth.user()?.id;
      ++this.generation;
      this.link.set('');
      this.expiresAt.set('');
      this.error.set('');
      this.busy.set(false);
    });
  }

  protected async offer(): Promise<void> {
    if (this.email.invalid || this.busy()) return;
    const generation = this.generation;
    const owner = this.auth.user()?.id;
    this.busy.set(true);
    this.error.set('');
    this.link.set('');
    try {
      const result = await this.api.offerTransfer(this.classId(), this.email.value.trim());
      if (
        this.destroy.destroyed ||
        this.generation !== generation ||
        this.auth.user()?.id !== owner
      )
        return;
      const url = new URL('/classes/handover', location.origin);
      url.searchParams.set('token', result.token);
      this.link.set(url.href);
      this.expiresAt.set(result.expiresAt);
    } catch {
      if (generation === this.generation)
        this.error.set(
          'Choose an active teacher with a confirmed email and two-factor authentication, then try again.',
        );
    } finally {
      if (generation === this.generation) this.busy.set(false);
    }
  }
}
