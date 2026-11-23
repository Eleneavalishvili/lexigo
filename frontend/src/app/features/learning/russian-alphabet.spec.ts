import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Pronunciation } from '../../core/learning/pronunciation';
import { RussianAlphabet, RussianLetter } from './russian-alphabet';

describe('Russian alphabet reference', () => {
  const alphabet = 'АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ';
  const letters: RussianLetter[] = Array.from(alphabet, (upper) => ({
    upper,
    lower: upper.toLowerCase(),
    name: upper.toLowerCase(),
    audioText: `Это буква ${upper.toLowerCase()}.`,
    example: 'пример',
    meaning: 'example',
    note: '',
  }));
  const voice = { stop: vi.fn(), play: vi.fn().mockResolvedValue(undefined), message: () => '' };
  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      imports: [RussianAlphabet],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Pronunciation, useValue: voice },
      ],
    });
  });
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('loads all letters and changes selection with keyboard focus without autoplay on arrows', async () => {
    const fixture = TestBed.createComponent(RussianAlphabet);
    TestBed.inject(HttpTestingController)
      .expectOne('/teaching/russian-alphabet.json')
      .flush(letters);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const buttons = host.querySelectorAll<HTMLButtonElement>('.letter-grid button');
    expect(buttons).toHaveLength(33);
    expect(voice.play).not.toHaveBeenCalled();
    buttons[0].focus();
    buttons[0].dispatchEvent(
      new KeyboardEvent('keydown', { key: 'End', bubbles: true, cancelable: true }),
    );
    await fixture.whenStable();
    expect(document.activeElement).toBe(buttons[32]);
    expect(buttons[32].getAttribute('aria-pressed')).toBe('true');
    expect(voice.play).not.toHaveBeenCalled();
    buttons[32].click();
    await fixture.whenStable();
    expect(voice.play).toHaveBeenCalledWith(letters[32].audioText, 'ru');
    fixture.destroy();
    expect(voice.stop).toHaveBeenCalled();
  });

  it('offers retry after a failed read without presenting an incomplete alphabet', async () => {
    const fixture = TestBed.createComponent(RussianAlphabet);
    TestBed.inject(HttpTestingController)
      .expectOne('/teaching/russian-alphabet.json')
      .flush([], { status: 503, statusText: 'Unavailable' });
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('[role=alert]')).not.toBeNull();
    expect(host.querySelector('.letter-grid')).toBeNull();
    host.querySelector<HTMLButtonElement>('button')!.click();
    TestBed.inject(HttpTestingController)
      .expectOne('/teaching/russian-alphabet.json')
      .flush(letters);
    await fixture.whenStable();
    expect(host.querySelector('[role=alert]')).toBeNull();
    expect(host.querySelectorAll('.letter-grid button')).toHaveLength(33);
  });

  it('rejects a reference that merges Ё into Е', async () => {
    const fixture = TestBed.createComponent(RussianAlphabet);
    const merged = letters.map((letter) => ({
      ...letter,
      upper: letter.upper === 'Ё' ? 'Е' : letter.upper,
    }));
    TestBed.inject(HttpTestingController)
      .expectOne('/teaching/russian-alphabet.json')
      .flush(merged);
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('[role=alert]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.letter-grid')).toBeNull();
  });
});
