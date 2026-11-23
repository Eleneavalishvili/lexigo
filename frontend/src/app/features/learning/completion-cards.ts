import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import {
  CompletionCardPage,
  CompletionCardsApi,
  OwnedCompletionCard,
} from '../../core/learning/completion-cards-api';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { CompletionCardArt } from '../../shared/completion-card-art/completion-card-art';

@Component({
  selector: 'app-completion-cards',
  imports: [RouterLink, ReactiveFormsModule, BrandLogo, CompletionCardArt],
  templateUrl: './completion-cards.html',
  styleUrl: './completion-cards.scss',
})
export class CompletionCardsPage {
  private readonly api = inject(CompletionCardsApi);
  private readonly auth = inject(AuthStore);
  private readonly owner = this.auth.user()?.id;
  private readonly lifetime = inject(DestroyRef);
  private generation = 0;
  protected readonly page = signal<CompletionCardPage | null>(null);
  protected readonly selected = signal<OwnedCompletionCard | null>(null);
  protected readonly unitId = signal('');
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly ready = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly confirmation = new FormControl(false, { nonNullable: true });
  protected readonly shareLink = computed(() => {
    const card = this.selected();
    return this.ready() && card?.isShared && card.shareCode
      ? window.location.origin + '/shared/completion/' + encodeURIComponent(card.shareCode)
      : '';
  });

  constructor() {
    effect(() => {
      if (this.auth.user()?.id === this.owner) return;
      ++this.generation;
      this.page.set(null);
      this.selected.set(null);
      this.ready.set(false);
      this.error.set('');
      this.notice.set('');
      this.loading.set(false);
      this.busy.set(false);
      this.confirmation.reset();
    });
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed())
      .subscribe((params) => {
        ++this.generation;
        this.busy.set(false);
        this.unitId.set(params.get('unitId') ?? '');
        void this.load(1);
      });
  }

  protected async load(page: number): Promise<void> {
    if (!this.current() || this.busy()) return;
    const generation = ++this.generation;
    this.loading.set(true);
    this.ready.set(false);
    this.error.set('');
    this.notice.set('');
    this.page.set(null);
    this.selected.set(null);
    this.confirmation.reset();
    try {
      const result = await this.api.list(page, this.unitId());
      if (!this.current(generation)) return;
      this.page.set(result);
      this.selected.set(result.items[0] ?? null);
      this.ready.set(true);
    } catch {
      if (this.current(generation)) this.error.set('Your cards could not load. Try again.');
    } finally {
      if (this.current(generation)) this.loading.set(false);
    }
  }

  protected select(card: OwnedCompletionCard): void {
    if (!this.ready() || this.busy()) return;
    this.selected.set(card);
    this.confirmation.reset();
    this.notice.set('');
    this.error.set('');
  }

  protected async claim(): Promise<void> {
    if (!this.current() || !this.unitId() || !this.ready() || this.busy()) return;
    const generation = this.generation;
    this.busy.set(true);
    this.error.set('');
    try {
      const result = await this.api.claim(this.unitId());
      const page = await this.api.list(1, this.unitId());
      if (!this.current(generation)) return;
      this.page.set(page);
      this.selected.set(page.items.find((item) => item.id === result.id) ?? null);
      this.notice.set('Your completed-unit card is ready. It is private.');
    } catch (error) {
      if (this.current(generation))
        this.error.set(
          this.message(error, 'The card could not be created. Reload to check its saved state.'),
        );
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }

  protected async sharing(enabled: boolean): Promise<void> {
    const card = this.selected();
    if (
      !card ||
      !this.current() ||
      !this.ready() ||
      this.busy() ||
      (enabled && !this.confirmation.value)
    )
      return;
    const generation = this.generation;
    this.busy.set(true);
    this.ready.set(false);
    this.error.set('');
    this.notice.set('');
    try {
      await this.api.sharing(card, enabled, this.confirmation.value);
      const saved = await this.api.get(card.id);
      if (!this.current(generation)) return;
      this.selected.set(saved);
      this.page.update((page) =>
        page
          ? { ...page, items: page.items.map((item) => (item.id === saved.id ? saved : item)) }
          : page,
      );
      this.ready.set(true);
      this.confirmation.reset();
      this.notice.set(
        saved.isShared
          ? 'Sharing is enabled. You can copy the public link.'
          : 'Sharing is off. The old public link is unavailable.',
      );
    } catch (error) {
      if (this.current(generation))
        this.error.set(
          this.message(
            error,
            'Sharing status could not be confirmed. Reload your cards before trying again.',
          ),
        );
    } finally {
      if (this.current(generation)) this.busy.set(false);
    }
  }

  protected async copy(): Promise<void> {
    if (!this.current() || !this.shareLink()) return;
    const generation = this.generation;
    try {
      await navigator.clipboard.writeText(this.shareLink());
      if (this.current(generation)) this.notice.set('Public link copied.');
    } catch {
      if (this.current(generation)) this.notice.set('Copy the public link from the box above.');
    }
  }
  protected print(): void {
    if (this.current() && this.selected()) window.print();
  }
  private current(generation = this.generation): boolean {
    return (
      !this.lifetime.destroyed &&
      generation === this.generation &&
      !!this.owner &&
      this.auth.user()?.id === this.owner
    );
  }
  private message(error: unknown, fallback: string): string {
    return error instanceof HttpErrorResponse && typeof error.error?.title === 'string'
      ? error.error.title
      : fallback;
  }
}
