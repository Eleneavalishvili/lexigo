import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../accounts/auth-store';
import { VocabularyApi, WordPage, WordView } from './vocabulary-api';
import { VocabularyStore } from './vocabulary-store';

describe('Personal word library state', () => {
  const user = signal<{ id: string } | null>({ id: 'owner' });
  const api = { list: vi.fn(), save: vi.fn(), remove: vi.fn() };
  const word: WordView = {
    id: 'word',
    courseId: 'course',
    courseTitle: 'Spanish',
    languageCode: 'es',
    word: 'Hola',
    englishTranslation: 'Hello',
    example: '',
    partOfSpeech: '',
    savedWordId: null,
    dueAt: null,
    intervalDays: null,
  };
  const page: WordPage = { items: [word], page: 1, pageSize: 20, totalCount: 21 };
  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'owner' });
    api.list.mockResolvedValue(page);
    api.save.mockResolvedValue({});
    api.remove.mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      providers: [
        VocabularyStore,
        { provide: VocabularyApi, useValue: api },
        { provide: AuthStore, useValue: { user } },
      ],
    });
  });
  it('restores filtered pages and saves only the vocabulary identity', async () => {
    const store = TestBed.inject(VocabularyStore);
    await store.load({ search: 'hello', courseId: 'course', savedOnly: false });
    expect(store.hasNext()).toBe(true);
    await store.toggle(word);
    expect(api.save).toHaveBeenCalledExactlyOnceWith('word');
    expect(api.remove).not.toHaveBeenCalled();
    expect(api.list).toHaveBeenLastCalledWith({
      search: 'hello',
      courseId: 'course',
      savedOnly: false,
      status: 'all',
      page: 1,
    });
    expect(store.notice()).toContain('Saved');
  });
  it('moves back a page after removing its last saved word', async () => {
    const store = TestBed.inject(VocabularyStore);
    await store.load({ page: 2 });
    await store.toggle({ ...word, savedWordId: 'owned-save' });
    expect(api.remove).toHaveBeenCalledExactlyOnceWith('owned-save');
    expect(store.filters().page).toBe(1);
  });
  it('discards late search results after a newer search', async () => {
    let finish!: (value: WordPage) => void;
    api.list.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    );
    const store = TestBed.inject(VocabularyStore);
    const older = store.load({ search: 'old' });
    await store.load({ search: 'new' });
    finish({ ...page, items: [] });
    await older;
    expect(store.result()?.items).toEqual([word]);
    expect(store.filters().search).toBe('new');
  });
  it('clears private results and ignores in-flight work after account changes', async () => {
    const store = TestBed.inject(VocabularyStore);
    await store.load();
    TestBed.tick();
    let finish!: () => void;
    api.save.mockImplementationOnce(
      () =>
        new Promise<void>((resolve) => {
          finish = resolve;
        }),
    );
    const pending = store.toggle(word);
    user.set({ id: 'someone-else' });
    TestBed.tick();
    expect(store.result()).toBeNull();
    finish();
    await pending;
    expect(store.notice()).toBe('');
    expect(api.list).toHaveBeenCalledTimes(1);
    await store.toggle(word);
    expect(api.save).toHaveBeenCalledTimes(1);
  });
  it('reports errors without inventing an empty successful library', async () => {
    api.list.mockRejectedValueOnce(new Error('offline'));
    const store = TestBed.inject(VocabularyStore);
    await store.load();
    expect(store.result()).toBeNull();
    expect(store.error()).toContain('try again');
    expect(store.loading()).toBe(false);
    await store.load();
    expect(store.result()).toEqual(page);
    expect(store.error()).toBe('');
  });
});
