import { Component, DestroyRef, effect, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ContentApi } from '../../core/content/content-api';
import { AuthStore } from '../../core/accounts/auth-store';

@Component({
  selector: 'app-draft-owner-transfer',
  imports: [ReactiveFormsModule],
  template: `
    <details>
      <summary>Hand over this unpublished lesson</summary>
      <p>
        Assign ongoing work to an active author with MFA and access to this language. Existing
        version authorship stays in the history.
      </p>
      <form [formGroup]="form" (ngSubmit)="transfer()">
        <label for="draft-owner-email">Receiving author’s email</label>
        <input
          id="draft-owner-email"
          type="email"
          formControlName="email"
          autocomplete="off"
          maxlength="256"
        />
        <label for="draft-owner-reason">Handover reason</label>
        <textarea id="draft-owner-reason" formControlName="reason" maxlength="500"></textarea>
        <label
          ><input type="checkbox" formControlName="confirmed" /> I reviewed this shared draft and
          confirm its new owner.</label
        >
        <button class="button button-outline" [disabled]="busy() || form.invalid">
          {{ busy() ? 'Handing over…' : 'Assign lesson owner' }}
        </button>
      </form>
      @if (error()) {
        <p role="alert">{{ error() }}</p>
      }
    </details>
  `,
  styles: `
    details {
      border: 1px solid var(--line);
      border-radius: 0.7rem;
      padding: 1rem;
      margin-block: 1rem;
    }
    form {
      display: grid;
      gap: 0.65rem;
      margin-block: 1rem;
    }
    input[type='email'],
    textarea {
      width: 100%;
      border: 1px solid var(--line);
      padding: 0.7rem;
      font: inherit;
      background: var(--paper);
      color: var(--ink);
      border-radius: 0.4rem;
    }
  `,
})
export class DraftOwnerTransfer {
  readonly lessonId = input.required<string>();
  readonly ownerId = input.required<string>();
  readonly changed = output<void>();
  private readonly api = inject(ContentApi);
  private readonly auth = inject(AuthStore);
  private readonly lifetime = inject(DestroyRef);
  private generation = 0;
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    reason: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(500)]],
    confirmed: [false, Validators.requiredTrue],
  });

  constructor() {
    effect(() => {
      this.lessonId();
      this.ownerId();
      void this.auth.user()?.id;
      ++this.generation;
      this.form.reset();
      this.error.set('');
      this.busy.set(false);
    });
  }

  protected async transfer(): Promise<void> {
    if (this.busy() || this.form.invalid) return;
    const generation = this.generation;
    const account = this.auth.user()?.id;
    const value = this.form.getRawValue();
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.transferOwner(
        this.lessonId(),
        value.email.trim(),
        this.ownerId(),
        value.reason.trim(),
      );
      if (
        this.lifetime.destroyed ||
        generation !== this.generation ||
        this.auth.user()?.id !== account
      )
        return;
      this.changed.emit();
    } catch (error) {
      if (generation === this.generation)
        this.error.set(
          error instanceof HttpErrorResponse && error.status === 409
            ? (error.error?.title ?? 'Refresh the lesson before trying again.')
            : 'The lesson owner could not be changed.',
        );
    } finally {
      if (generation === this.generation) this.busy.set(false);
    }
  }
}
