import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { CatalogApi } from '../../core/content/catalog-api';
import { VocabularyApi } from '../../core/vocabulary/vocabulary-api';
import { Vocabulary } from './vocabulary';

describe('Word library screen', () => {
  it('opens a weekly summary link on the first page of saved due words for the selected course', async () => {
    const courseId = '10000000-0000-0000-0000-000000000001';
    const list = vi.fn().mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    TestBed.configureTestingModule({
      imports: [Vocabulary],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user: signal({ id: 'owner' }) } },
        { provide: CatalogApi, useValue: { courses: async () => [] } },
        { provide: VocabularyApi, useValue: { list } },
        {
          provide: ActivatedRoute,
          useValue: { queryParamMap: of(convertToParamMap({ courseId, status: 'due' })) },
        },
      ],
    });
    const fixture = TestBed.createComponent(Vocabulary);
    await fixture.whenStable();
    expect(list).toHaveBeenCalledExactlyOnceWith(
      expect.objectContaining({ courseId, status: 'due', savedOnly: true, page: 1 }),
    );
  });

  it('hides the translation during review and retries with the same submission key', async () => {
    const review = vi.fn().mockRejectedValueOnce(new Error('connection lost')).mockResolvedValue({
      id: 'review',
      isCorrect: true,
      dueAt: '2026-11-06T10:00:00Z',
      intervalDays: 1,
    });
    const list = vi.fn().mockResolvedValue({
      items: [
        {
          id: 'word',
          word: 'Hola',
          englishTranslation: 'Hello',
          languageCode: 'es',
          courseTitle: 'Spanish',
          savedWordId: 'saved',
          dueAt: '2026-11-04T10:00:00Z',
        },
      ],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });
    TestBed.configureTestingModule({
      imports: [Vocabulary],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user: signal({ id: 'owner' }) } },
        { provide: CatalogApi, useValue: { courses: async () => [] } },
        {
          provide: VocabularyApi,
          useValue: { review, list },
        },
      ],
    });
    const fixture = TestBed.createComponent(Vocabulary);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const schedule = host.querySelector<HTMLSelectElement>('#word-status')!;
    const topic = host.querySelector<HTMLSelectElement>('#word-topic')!;
    schedule.value = 'due';
    schedule.dispatchEvent(new Event('change'));
    topic.value = 'greetings';
    topic.dispatchEvent(new Event('change'));
    host.querySelector<HTMLButtonElement>('form button[type="submit"]')!.click();
    await fixture.whenStable();
    expect(list).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: 'due', topic: 'greetings' }),
    );
    const article = host.querySelector('article')!;
    [...article.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Review word'))!
      .click();
    await fixture.whenStable();
    expect(article.querySelector('.translation')).toBeNull();
    const field = article.querySelector('input')!;
    field.value = 'Hello';
    field.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    const submit = article.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    submit.click();
    await fixture.whenStable();
    submit.click();
    await fixture.whenStable();
    expect(review).toHaveBeenCalledTimes(2);
    expect(review.mock.calls[1]).toEqual(review.mock.calls[0]);
    expect(article.querySelector('[role=status]')?.textContent).toContain('Correct!');
  });

  it('escapes learning text and offers an accessible save action', async () => {
    const save = vi.fn().mockResolvedValue({});
    const audio = vi.fn().mockResolvedValue({
      url: 'https://storage.example.test/clip.wav',
      expiresInSeconds: 300,
      source: 'Test speaker',
      license: 'Test permission',
    });
    TestBed.configureTestingModule({
      imports: [Vocabulary],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user: signal({ id: 'owner' }) } },
        { provide: CatalogApi, useValue: { courses: async () => [] } },
        {
          provide: VocabularyApi,
          useValue: {
            save,
            audio,
            list: async () => ({
              items: [
                {
                  id: 'word',
                  word: '<script>bad()</script>',
                  englishTranslation: 'Hello',
                  example: '<img src=x>',
                  languageCode: 'es',
                  courseTitle: 'Spanish',
                  partOfSpeech: 'greeting',
                  topic: 'greetings',
                  hasAudio: true,
                  savedWordId: null,
                  dueAt: null,
                },
              ],
              page: 1,
              pageSize: 20,
              totalCount: 1,
            }),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(Vocabulary);
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('article script')).toBeNull();
    expect(element.querySelector('blockquote img')).toBeNull();
    expect(element.querySelector('h2')?.textContent).toContain('<script>');
    expect(element.querySelector('.word-type')?.textContent).toContain('Alphabet and greetings');
    element.querySelector<HTMLButtonElement>('app-word-recording button')!.click();
    await fixture.whenStable();
    expect(audio).toHaveBeenCalledExactlyOnceWith('word');
    expect(element.querySelector('audio')?.getAttribute('src')).toContain('clip.wav');
    expect(element.querySelector('details')?.textContent).toContain('Test permission');
    element.querySelector<HTMLButtonElement>('article button[aria-label^="Save "]')!.click();
    await fixture.whenStable();
    expect(save).toHaveBeenCalledExactlyOnceWith('word');
    expect(element.querySelector('label[for="word-search"]')).not.toBeNull();
  });
});
