import { computed, DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { AuthStore } from '../accounts/auth-store';
import { VocabularyApi, WordFilters, WordPage, WordView } from './vocabulary-api';

@Injectable()
export class VocabularyStore {
  private readonly api = inject(VocabularyApi);
  private readonly auth = inject(AuthStore);
  private readonly destroy = inject(DestroyRef);
  private readonly owner = this.auth.user()?.id;
  private generation = 0;
  readonly filters = signal<WordFilters>({
    savedOnly: true,
    courseId: '',
    search: '',
    status: 'all',
    page: 1,
  });
  readonly result = signal<WordPage | null>(null);
  readonly loading = signal(false);
  readonly working = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly hasNext = computed(() => {
    const value = this.result();
    return !!value && value.page * value.pageSize < value.totalCount;
  });
  constructor() {
    effect(() => {
      if (this.auth.user()?.id === this.owner) return;
      ++this.generation;
      this.result.set(null);
      this.notice.set('');
      this.error.set('');
      this.loading.set(false);
    });
  }
  async load(changes: Partial<WordFilters> = {}): Promise<void> {
    if (!this.owner || this.auth.user()?.id !== this.owner) return;
    const generation = ++this.generation;
    this.filters.update((value) => ({ ...value, ...changes }));
    this.result.set(null);
    this.loading.set(true);
    this.error.set('');
    try {
      const value = await this.api.list(this.filters());
      if (this.current(generation)) this.result.set(value);
    } catch {
      if (this.current(generation))
        this.error.set('Your words couldn’t be loaded. Please try again.');
    } finally {
      if (this.current(generation)) this.loading.set(false);
    }
  }
  async toggle(word: WordView): Promise<void> {
    if (this.working() || this.loading() || !this.current(this.generation)) return;
    const generation = this.generation;
    this.working.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      if (word.savedWordId) await this.api.remove(word.savedWordId);
      else await this.api.save(word.id);
      if (!this.current(generation)) return;
      this.notice.set(
        word.savedWordId
          ? 'Word removed from your library.'
          : 'A new word to keep. Saved to your library.',
      );
      const page =
        word.savedWordId && this.filters().savedOnly && this.result()?.items.length === 1
          ? Math.max(1, this.filters().page - 1)
          : this.filters().page;
      await this.load({ page });
    } catch {
      if (this.current(generation))
        this.error.set('We couldn’t update this word. Refresh the library and try again.');
    } finally {
      if (!this.destroy.destroyed) this.working.set(false);
    }
  }
  private current(generation: number): boolean {
    return (
      !this.destroy.destroyed &&
      generation === this.generation &&
      !!this.owner &&
      this.auth.user()?.id === this.owner
    );
  }
}
