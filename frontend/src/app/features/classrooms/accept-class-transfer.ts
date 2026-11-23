import { DatePipe } from '@angular/common';
import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { ClassroomApi } from '../../core/classrooms/classroom-api';

@Component({
  selector: 'app-accept-class-transfer',
  imports: [RouterLink, DatePipe],
  template: `
    <main>
      <a routerLink="/classes">Back to classes</a>
      <h1>Accept a class handover</h1>
      @if (loading()) {
        <p role="status">Checking your handover…</p>
      }
      @if (error()) {
        <p role="alert">{{ error() }}</p>
      }
      @if (preview(); as value) {
        <h2>{{ value.className }}</h2>
        <p>
          You will become this class’s teacher and manage its existing members, assignments and
          feedback. The previous teacher loses class management access. Existing joining codes will
          be revoked.
        </p>
        <p>This handover expires {{ value.expiresAt | date: 'medium' }}.</p>
        <button class="button button-ink" [disabled]="working()" (click)="accept()">
          {{ working() ? 'Accepting…' : 'Accept responsibility for this class' }}
        </button>
      } @else if (!loading()) {
        <button class="button button-outline" (click)="load()">Check handover again</button>
      }
    </main>
  `,
  styles: `
    main {
      max-width: 44rem;
      margin: 2rem auto;
      padding: 1.5rem;
    }
    p {
      line-height: 1.7;
    }
  `,
})
export class AcceptClassTransfer {
  private readonly api = inject(ClassroomApi);
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private readonly token = inject(ActivatedRoute).snapshot.queryParamMap.get('token') ?? '';
  private generation = 0;
  protected readonly preview = signal<{ className: string; expiresAt: string } | null>(null);
  protected readonly loading = signal(false);
  protected readonly working = signal(false);
  protected readonly error = signal('');

  constructor() {
    effect(() => {
      void this.auth.user()?.id;
      void this.load();
    });
  }

  protected async load(): Promise<void> {
    const generation = ++this.generation;
    this.preview.set(null);
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.api.previewTransfer(this.token);
      if (!this.destroy.destroyed && generation === this.generation) this.preview.set(result);
    } catch {
      if (generation === this.generation)
        this.error.set(
          'This handover is unavailable. Sign in as the designated teacher or ask for a new link.',
        );
    } finally {
      if (generation === this.generation) this.loading.set(false);
    }
  }

  protected async accept(): Promise<void> {
    if (this.working() || !this.preview()) return;
    const generation = this.generation;
    this.working.set(true);
    this.error.set('');
    try {
      const result = await this.api.acceptTransfer(this.token);
      if (!this.destroy.destroyed && generation === this.generation)
        await this.router.navigate(['/classes', result.id]);
    } catch {
      if (generation === this.generation) {
        this.preview.set(null);
        this.error.set('The class could not be handed over. Check the link again before retrying.');
      }
    } finally {
      if (generation === this.generation) this.working.set(false);
    }
  }
}
