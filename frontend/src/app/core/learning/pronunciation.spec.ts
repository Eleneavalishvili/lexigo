import { TestBed } from '@angular/core/testing';
import { Pronunciation } from './pronunciation';

describe('Locally generated pronunciation playback', () => {
  const created: FakeAudio[] = [];
  class FakeAudio {
    playbackRate = 1;
    preservesPitch = false;
    onplaying: (() => void) | null = null;
    onended: (() => void) | null = null;
    onerror: (() => void) | null = null;
    constructor(readonly src: string) {
      created.push(this);
    }
    play = vi.fn(async () => {
      this.onplaying?.();
    });
    pause = vi.fn();
  }
  beforeEach(() => {
    created.length = 0;
    vi.stubGlobal('Audio', FakeAudio);
  });
  afterEach(() => vi.unstubAllGlobals());

  it('checks availability without autoplay, English fallback or unsafe paths', async () => {
    const fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        clips: {
          'es|café': { file: 'a'.repeat(24) + '.wav' },
          'en|student': { file: 'b'.repeat(24) + '.wav' },
          'es|bad': { file: '../private.wav' },
        },
      }),
    });
    vi.stubGlobal('fetch', fetch);
    const voice = TestBed.inject(Pronunciation);
    expect(await voice.available(' CAFÉ! ', 'es')).toBe(true);
    expect(await voice.available('student', 'es')).toBe(false);
    expect(await voice.available('bad', 'es')).toBe(false);
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(created).toHaveLength(0);
  });

  it('plays the selected target-language word and cancels overlap without accepting old end events', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          clips: {
            'es|hola': { file: 'a'.repeat(24) + '.wav' },
            'es|gracias': { file: 'b'.repeat(24) + '.wav' },
          },
        }),
      }),
    );
    const voice = TestBed.inject(Pronunciation);
    await voice.play('Hola!', 'es');
    expect(created[0].src).toBe('/pronunciation/' + 'a'.repeat(24) + '.wav');
    expect(created[0].playbackRate).toBe(1);
    expect(created[0].preservesPitch).toBe(true);
    expect(voice.speaking()).toBe(true);
    await voice.play('Gracias', 'es', false, true);
    expect(created[1].playbackRate).toBe(0.85);
    expect(created[0].pause).toHaveBeenCalled();
    created[0].onended?.();
    expect(voice.speaking()).toBe(true);
    created[1].onended?.();
    expect(voice.speaking()).toBe(false);
  });

  it('uses an English recording for an English reading answer and rejects unsafe file paths', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({
          clips: {
            'en|two': { file: 'c'.repeat(24) + '.wav' },
            'es|bad': { file: '../private.wav' },
          },
        }),
      }),
    );
    const voice = TestBed.inject(Pronunciation);
    await voice.play('Two', 'es');
    expect(created).toHaveLength(1);
    await voice.play('bad', 'es');
    expect(created).toHaveLength(1);
    expect(voice.message()).toContain('not available');
  });
});
