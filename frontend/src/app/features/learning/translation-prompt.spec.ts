import { TestBed } from '@angular/core/testing';
import { TranslationPrompt } from './translation-prompt';
import { Pronunciation } from '../../core/learning/pronunciation';

describe('Written speech and tap-to-reveal meanings', () => {
  const play = vi.fn();
  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      imports: [TranslationPrompt],
      providers: [{ provide: Pronunciation, useValue: { play } }],
    });
  });
  async function open(hints = true) {
    const fixture = TestBed.createComponent(TranslationPrompt);
    fixture.componentRef.setInput('courseId', '10000000-0000-0000-0000-000000000003');
    fixture.componentRef.setInput('prompt', 'Write this in English: ein Mädchen');
    fixture.componentRef.setInput('explanation', 'ein Mädchen — a girl');
    fixture.componentRef.setInput('hintsEnabled', hints);
    await fixture.whenStable();
    return fixture;
  }
  it('shows the target script, meaning and grammar from public teaching notes', async () => {
    const fixture = await open();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent?.replace(/\s+/g, ' ')).toContain('ein Mädchen');
    const words = host.querySelectorAll<HTMLButtonElement>('.hint-word');
    words[1].click();
    await fixture.whenStable();
    expect(host.querySelector('.meaning')?.textContent).toBe('girl');
    words[0].click();
    await fixture.whenStable();
    expect(host.querySelector('.meaning')?.textContent).toContain('masculine or neuter');
    expect(play).toHaveBeenCalledWith('ein', 'de', true);
  });
  it('keeps hints disabled for assessments', async () => {
    const fixture = await open(false);
    const host: HTMLElement = fixture.nativeElement;
    expect(
      Array.from(host.querySelectorAll<HTMLButtonElement>('.hint-word')).every(
        (button) => button.disabled,
      ),
    ).toBe(true);
    expect(host.querySelector('.meaning')).toBeNull();
  });
});
