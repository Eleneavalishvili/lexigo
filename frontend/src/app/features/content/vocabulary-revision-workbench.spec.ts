import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { ContentApi } from '../../core/content/content-api';
import { VocabularyRevisionApi } from '../../core/content/vocabulary-revision-api';
import { VocabularyApi } from '../../core/vocabulary/vocabulary-api';
import { VocabularyRevisionWorkbench } from './vocabulary-revision-workbench';

describe('Vocabulary revision workbench', () => {
  it('saves a new word as a draft and exposes a separate submit action', async () => {
    const create = vi.fn().mockResolvedValue({});
    const change = vi.fn().mockResolvedValue({});
    const revision = {
      id: 'revision',
      revisionToken: 'current-token',
      authorId: 'author',
      status: 'Draft',
      text: { word: 'hola', englishTranslation: 'hello', topic: null },
    };
    const list = vi.fn().mockResolvedValueOnce([]).mockResolvedValue([revision]);
    TestBed.configureTestingModule({
      imports: [VocabularyRevisionWorkbench],
      providers: [
        provideRouter([]),
        {
          provide: AuthStore,
          useValue: { user: signal({ id: 'author', roles: ['ContentAuthor'] }) },
        },
        {
          provide: ContentApi,
          useValue: { courses: async () => [{ id: 'spanish', title: 'Spanish', canAuthor: true }] },
        },
        { provide: VocabularyApi, useValue: {} },
        { provide: VocabularyRevisionApi, useValue: { create, change, list } },
      ],
    });
    const fixture = TestBed.createComponent(VocabularyRevisionWorkbench);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const course = host.querySelector<HTMLSelectElement>('#revision-course')!;
    course.value = 'spanish';
    course.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    for (const [id, value] of [
      ['revision-word', 'hola'],
      ['revision-english', 'hello'],
    ]) {
      const field = host.querySelector<HTMLInputElement>('#' + id)!;
      field.value = value;
      field.dispatchEvent(new Event('input'));
    }
    await fixture.whenStable();
    host.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await fixture.whenStable();
    expect(create).toHaveBeenCalledExactlyOnceWith(
      'spanish',
      null,
      expect.objectContaining({ word: 'hola', englishTranslation: 'hello' }),
    );
    expect(change).not.toHaveBeenCalled();

    [...host.querySelectorAll<HTMLButtonElement>('article button')]
      .find((button) => button.textContent?.includes('Submit for review'))!
      .click();
    await fixture.whenStable();
    expect(change).toHaveBeenCalledExactlyOnceWith(revision, 'submit', null, null);
    expect(host.textContent).not.toContain('Publish word');
    change.mockClear();
    [...host.querySelectorAll<HTMLButtonElement>('article button')]
      .find((button) => button.textContent?.includes('Withdraw revision'))!
      .click();
    await fixture.whenStable();
    expect(change).not.toHaveBeenCalled();
    [...host.querySelectorAll<HTMLButtonElement>('article button')]
      .find((button) => button.textContent?.includes('Confirm withdrawal'))!
      .click();
    await fixture.whenStable();
    expect(change).toHaveBeenCalledExactlyOnceWith(revision, 'withdraw', null, null);
  });
});
