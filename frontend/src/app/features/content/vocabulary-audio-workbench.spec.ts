import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { ContentApi } from '../../core/content/content-api';
import { MediaApi } from '../../core/content/media-api';
import { VocabularyAudioWorkbench } from './vocabulary-audio-workbench';

describe('Vocabulary audio workbench', () => {
  it('saves the selected recording only after the admin confirms', async () => {
    const word = { id: 'word', word: 'hola', englishTranslation: 'hello', audioMediaId: null };
    const attach = vi.fn().mockResolvedValue(undefined);
    TestBed.configureTestingModule({
      imports: [VocabularyAudioWorkbench],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user: signal({ id: 'admin' }) } },
        {
          provide: ContentApi,
          useValue: {
            courses: async () => [{ id: 'spanish', title: 'Spanish', targetLanguageId: 2 }],
            words: async () => ({ items: [word], total: 1 }),
            attachWordAudio: attach,
          },
        },
        {
          provide: MediaApi,
          useValue: {
            list: async () => [
              {
                id: 'reviewed',
                languageId: 2,
                isVerified: true,
                storageState: 'Ready',
                source: 'Reviewed pronunciation',
                durationMilliseconds: 500,
              },
            ],
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(VocabularyAudioWorkbench);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    const course = host.querySelector('select')!;
    course.value = 'spanish';
    course.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    host.querySelector<HTMLButtonElement>('article button')!.click();
    await fixture.whenStable();
    const save = host.querySelector<HTMLButtonElement>('.actions .button-ink')!;
    expect(save.disabled).toBe(true);
    host
      .querySelector<HTMLButtonElement>('app-reviewed-audio-picker button[aria-pressed]')!
      .click();
    await fixture.whenStable();
    expect(attach).not.toHaveBeenCalled();
    save.click();
    await fixture.whenStable();
    expect(attach).toHaveBeenCalledExactlyOnceWith(word, 'reviewed');
    expect(host.textContent).toContain('Recording updated.');
  });
});
