import { encodeSpeechWave, prepareSpeechWave } from './speech-wave';

describe('Speech WAV conversion', () => {
  afterEach(() => vi.unstubAllGlobals());
  it('writes bounded mono 16-bit little-endian PCM without metadata', async () => {
    const channel = new Float32Array(1600);
    channel.set([-1, 0, 1, 2, -2]);
    const blob = encodeSpeechWave([channel], 16000);
    const bytes = await blob.arrayBuffer();
    const view = new DataView(bytes);
    expect(blob.type).toBe('audio/wav');
    expect(bytes.byteLength).toBe(3244);
    expect(new TextDecoder().decode(new Uint8Array(bytes).slice(0, 4))).toBe('RIFF');
    expect(view.getUint32(24, true)).toBe(16000);
    expect(view.getUint16(22, true)).toBe(1);
    expect(view.getUint16(34, true)).toBe(16);
    expect([0, 1, 2, 3, 4].map((index) => view.getInt16(44 + index * 2, true))).toEqual([
      -32768, 0, 32767, 32767, -32768,
    ]);
  });
  it('mixes stereo channels without doubling volume', async () => {
    const blob = encodeSpeechWave(
      [new Float32Array(1600).fill(1), new Float32Array(1600).fill(-1)],
      16000,
    );
    const view = new DataView(await blob.arrayBuffer());
    expect(view.getInt16(44, true)).toBe(0);
  });
  it('refuses invalid formats, mismatched lengths and non-finite samples', () => {
    for (const frames of [1599, 480001])
      expect(() => encodeSpeechWave([new Float32Array(frames)], 16000)).toThrow();
    expect(() => encodeSpeechWave([new Float32Array(1600)], 48000)).toThrow();
    expect(() =>
      encodeSpeechWave([new Float32Array(1600), new Float32Array(1601)], 16000),
    ).toThrow();
    expect(() => encodeSpeechWave([new Float32Array(1600).fill(Number.NaN)], 16000)).toThrow();
    expect(() => encodeSpeechWave([], 16000)).toThrow();
  });
  it('does not decode oversized input or cancelled requests', async () => {
    const context = vi.fn();
    vi.stubGlobal('OfflineAudioContext', context);
    const controller = new AbortController();
    await expect(
      prepareSpeechWave(new Blob([new Uint8Array(5 * 1024 * 1024 + 1)]), controller.signal),
    ).rejects.toThrow();
    controller.abort();
    await expect(prepareSpeechWave(new Blob(['audio']), controller.signal)).rejects.toThrow();
    expect(context).not.toHaveBeenCalled();
  });
  it('discards a decode that finishes after cancellation', async () => {
    let finish!: (value: unknown) => void;
    vi.stubGlobal(
      'OfflineAudioContext',
      class {
        decodeAudioData() {
          return new Promise((resolve) => {
            finish = resolve;
          });
        }
      },
    );
    const controller = new AbortController();
    const pending = prepareSpeechWave(new Blob(['audio']), controller.signal);
    await vi.waitFor(() => expect(finish).toBeDefined());
    controller.abort();
    finish({
      numberOfChannels: 1,
      sampleRate: 16000,
      getChannelData: () => new Float32Array(1600),
    });
    await expect(pending).rejects.toThrow();
  });
  it('decodes at 16 kHz before encoding the checked waveform', async () => {
    const constructor = vi.fn();
    vi.stubGlobal(
      'OfflineAudioContext',
      class {
        constructor(channels: number, frames: number, sampleRate: number) {
          constructor(channels, frames, sampleRate);
        }
        async decodeAudioData() {
          return {
            numberOfChannels: 1,
            sampleRate: 16000,
            getChannelData: () => new Float32Array(1600),
          };
        }
      },
    );
    const result = await prepareSpeechWave(
      new Blob(['recorded audio']),
      new AbortController().signal,
    );
    expect(constructor).toHaveBeenCalledExactlyOnceWith(1, 1, 16000);
    expect(result.type).toBe('audio/wav');
    expect(result.size).toBe(3244);
  });
});
