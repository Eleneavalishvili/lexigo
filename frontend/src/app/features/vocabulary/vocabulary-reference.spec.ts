import { TestBed } from '@angular/core/testing';
import { Pronunciation } from '../../core/learning/pronunciation';
import { VocabularyReference } from './vocabulary-reference';

describe('Vocabulary reference recordings', () => {
  const word = {
    word: 'el café',
    example: 'El café está caliente.',
    languageCode: 'es',
    courseId: '10000000-0000-0000-0000-000000000001',
    hasAudio: false,
  };
  const available = vi.fn();
  beforeEach(() => {
    available.mockReset().mockResolvedValue(true);
    TestBed.configureTestingModule({
      imports: [VocabularyReference],
      providers: [
        {
          provide: Pronunciation,
          useValue: { available, play: vi.fn(), message: () => '', text: () => '' },
        },
      ],
    });
  });
  it('offers word and example playback only when reference files exist', async () => {
    const fixture = TestBed.createComponent(VocabularyReference);
    fixture.componentRef.setInput('word', word);
    await fixture.whenStable();
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelectorAll('app-pronunciation-button')).toHaveLength(2);
    });
    expect(fixture.nativeElement.textContent).toContain('Hear the example');
    available.mockResolvedValue(false);
    fixture.componentRef.setInput('word', { ...word, word: 'unrecorded', example: '' });
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('button')).toBeNull();
    expect(fixture.nativeElement.querySelector('details')).toBeNull();
  });
  it('keeps reviewed word playback separate and does not duplicate it', async () => {
    const fixture = TestBed.createComponent(VocabularyReference);
    fixture.componentRef.setInput('word', { ...word, hasAudio: true });
    await fixture.whenStable();
    expect(available).not.toHaveBeenCalledWith(word.word, 'es');
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelectorAll('app-pronunciation-button')).toHaveLength(1);
    });
  });
  it('ignores availability responses after the displayed word changes', async () => {
    let resolveOld!: (value: boolean) => void;
    const pending = new Promise<boolean>((resolve) => {
      resolveOld = resolve;
    });
    available.mockReturnValueOnce(pending).mockReturnValueOnce(pending).mockResolvedValue(false);
    const fixture = TestBed.createComponent(VocabularyReference);
    fixture.componentRef.setInput('word', word);
    fixture.detectChanges();
    fixture.componentRef.setInput('word', { ...word, word: 'new word', example: '' });
    fixture.detectChanges();
    resolveOld(true);
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('button')).toBeNull();
  });
});
