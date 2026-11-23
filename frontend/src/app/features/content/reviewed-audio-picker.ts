import { Component, DestroyRef, effect, inject, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { MediaApi, StaffMedia } from '../../core/content/media-api';

@Component({
  selector: 'app-reviewed-audio-picker',
  imports: [RouterLink],
  template: ` <section class="picker" aria-label="Reviewed audio selection">
    <p>
      <strong>Choose reviewed audio.</strong> <a routerLink="/manage/media">Open audio library ↗</a>
    </p>
    @if (selectedId()) {
      <p role="status">A recording is selected.</p>
      <button type="button" (click)="chosen.emit(null)" [disabled]="disabled()">
        Remove selection
      </button>
    }
    @if (loading()) {
      <p role="status">Looking through your audio…</p>
    }
    @if (error()) {
      <p role="alert">{{ error() }}</p>
    }
    @for (item of available(); track item.id) {
      <button
        type="button"
        [disabled]="disabled() || loading()"
        [attr.aria-pressed]="selectedId() === item.id"
        (click)="chosen.emit(item.id)"
      >
        {{ item.source.slice(0, 100) }} · {{ (item.durationMilliseconds ?? 0) / 1000 }}s
      </button>
    } @empty {
      @if (!loading()) {
        <p>No reviewed audio for this language on this page.</p>
      }
    }
    <nav aria-label="Audio selection pages">
      <button
        type="button"
        (click)="load(page() - 1)"
        [disabled]="disabled() || loading() || page() === 1"
      >
        ← Previous
      </button>
      <button type="button" (click)="load(page())" [disabled]="disabled() || loading()">
        Refresh
      </button>
      <button
        type="button"
        (click)="load(page() + 1)"
        [disabled]="disabled() || loading() || !hasMore()"
      >
        Next →
      </button>
    </nav>
  </section>`,
  styles: `
    .picker {
      border: 1px solid var(--line);
      padding: 1rem;
      margin-block: 1rem;
    }
    p {
      line-height: 1.7;
      font-size: 0.85rem;
    }
    button {
      background: var(--surface);
      border: 1px solid var(--line);
      padding: 0.7rem;
      min-height: 44px;
      text-align: left;
      cursor: pointer;
      margin: 0.4rem 0.4rem 0.4rem 0;
      max-width: 100%;
      overflow-wrap: anywhere;
    }
    button[aria-pressed='true'] {
      border-color: var(--ink);
      box-shadow: inset 0 0 0 1px var(--ink);
    }
    button:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
    nav {
      display: flex;
      flex-wrap: wrap;
    }
  `,
})
export class ReviewedAudioPicker {
  readonly languageId = input.required<number>();
  readonly maximumDurationMs = input<number | undefined>(undefined);
  readonly selectedId = input<string | null>(null);
  readonly disabled = input(false);
  readonly chosen = output<string | null>();
  private readonly api = inject(MediaApi);
  private readonly auth = inject(AuthStore);
  private readonly owner = this.auth.user()?.id;
  private generation = 0;
  protected readonly available = signal<readonly StaffMedia[]>([]);
  protected readonly page = signal(1);
  protected readonly hasMore = signal(false);
  protected readonly loading = signal(false);
  protected readonly error = signal('');

  constructor() {
    effect(() => {
      const language = this.languageId();
      this.maximumDurationMs();
      const user = this.auth.user();
      if (user?.id !== this.owner || !user) {
        this.generation++;
        this.available.set([]);
        this.hasMore.set(false);
        return;
      }
      if (language > 0) void this.load(1);
    });
    inject(DestroyRef).onDestroy(() => {
      this.generation++;
    });
  }

  protected async load(page: number): Promise<void> {
    const generation = ++this.generation;
    const language = this.languageId();
    const maximumDuration = this.maximumDurationMs();
    this.loading.set(true);
    this.error.set('');
    try {
      const rows = await this.api.list(page);
      if (generation !== this.generation) return;
      this.available.set(
        rows.filter(
          (item) =>
            item.languageId === language &&
            item.isVerified &&
            item.storageState === 'Ready' &&
            (maximumDuration === undefined ||
              (item.durationMilliseconds ?? Infinity) <= maximumDuration),
        ),
      );
      this.hasMore.set(rows.length === 25);
      this.page.set(page);
    } catch {
      if (generation === this.generation)
        this.error.set('Audio could not be loaded. Refresh to try again.');
    } finally {
      if (generation === this.generation) this.loading.set(false);
    }
  }
}
