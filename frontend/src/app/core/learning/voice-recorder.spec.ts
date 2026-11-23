import { TestBed } from '@angular/core/testing';
import { VoiceRecorder } from './voice-recorder';

class FakeRecorder {
  static latest: FakeRecorder;
  static isTypeSupported = () => true;
  state = 'inactive';
  mimeType: string;
  ondataavailable: ((event: { data: Blob }) => void) | null = null;
  onstop: (() => void) | null = null;
  onerror: (() => void) | null = null;
  constructor(_stream: MediaStream, options?: MediaRecorderOptions) {
    this.mimeType = options?.mimeType ?? 'audio/webm';
    FakeRecorder.latest = this;
  }
  start = vi.fn(() => {
    this.state = 'recording';
  });
  stop = vi.fn(() => {
    this.state = 'inactive';
    this.onstop?.();
  });
  data(blob = new Blob(['voice'], { type: 'audio/webm' })) {
    this.ondataavailable?.({ data: blob });
  }
}

describe('Local voice recording lifecycle', () => {
  const track = { stop: vi.fn() };
  const stream = { getTracks: () => [track] } as unknown as MediaStream;
  const getUserMedia = vi.fn();
  const create = vi.fn(() => 'blob:lexigo-test');
  const revoke = vi.fn();
  let originals: [object, string, PropertyDescriptor | undefined][];
  beforeEach(() => {
    vi.clearAllMocks();
    originals = [
      [navigator, 'mediaDevices', Object.getOwnPropertyDescriptor(navigator, 'mediaDevices')],
      [window, 'isSecureContext', Object.getOwnPropertyDescriptor(window, 'isSecureContext')],
      [URL, 'createObjectURL', Object.getOwnPropertyDescriptor(URL, 'createObjectURL')],
      [URL, 'revokeObjectURL', Object.getOwnPropertyDescriptor(URL, 'revokeObjectURL')],
    ];
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia },
    });
    Object.defineProperty(window, 'isSecureContext', { configurable: true, value: true });
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: create });
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: revoke });
    vi.stubGlobal('MediaRecorder', FakeRecorder);
    getUserMedia.mockResolvedValue(stream);
    TestBed.configureTestingModule({ providers: [VoiceRecorder] });
  });
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.useRealTimers();
    vi.unstubAllGlobals();
    for (const [target, key, descriptor] of originals) {
      if (descriptor) Object.defineProperty(target, key, descriptor);
      else Reflect.deleteProperty(target, key);
    }
  });
  it('requests only audio on explicit start, then releases tracks and revokes the clip', async () => {
    const recorder = TestBed.inject(VoiceRecorder);
    expect(getUserMedia).not.toHaveBeenCalled();
    await recorder.start();
    expect(getUserMedia).toHaveBeenCalledWith({ audio: true, video: false });
    expect(recorder.state()).toBe('recording');
    FakeRecorder.latest.data();
    recorder.stop();
    expect(track.stop).toHaveBeenCalled();
    expect(recorder.clip()).toBe('blob:lexigo-test');
    recorder.clear();
    expect(revoke).toHaveBeenCalledWith('blob:lexigo-test');
    expect(recorder.clip()).toBeNull();
  });
  it('handles denied permission without creating a recording', async () => {
    getUserMedia.mockRejectedValue(new DOMException('Denied', 'NotAllowedError'));
    const recorder = TestBed.inject(VoiceRecorder);
    await recorder.start();
    expect(recorder.state()).toBe('error');
    expect(recorder.error()).toContain('wasn’t allowed');
    expect(create).not.toHaveBeenCalled();
  });
  it('releases a late microphone stream after permission was cancelled', async () => {
    let resolve!: (value: MediaStream) => void;
    getUserMedia.mockReturnValue(
      new Promise<MediaStream>((done) => {
        resolve = done;
      }),
    );
    const recorder = TestBed.inject(VoiceRecorder);
    const pending = recorder.start();
    recorder.clear();
    resolve(stream);
    await pending;
    expect(track.stop).toHaveBeenCalledOnce();
    expect(recorder.state()).toBe('idle');
    expect(create).not.toHaveBeenCalled();
  });
  it('stops automatically at thirty seconds', async () => {
    vi.useFakeTimers();
    const recorder = TestBed.inject(VoiceRecorder);
    await recorder.start();
    FakeRecorder.latest.data();
    vi.advanceTimersByTime(30000);
    expect(FakeRecorder.latest.stop).toHaveBeenCalledOnce();
    expect(recorder.state()).toBe('ready');
    expect(track.stop).toHaveBeenCalled();
  });
  it('rejects an oversized clip and releases the microphone without keeping audio', async () => {
    const recorder = TestBed.inject(VoiceRecorder);
    await recorder.start();
    FakeRecorder.latest.data(new Blob([new Uint8Array(5 * 1024 * 1024 + 1)]));
    expect(recorder.state()).toBe('error');
    expect(recorder.error()).toContain('5 MB');
    expect(track.stop).toHaveBeenCalled();
    expect(create).not.toHaveBeenCalled();
  });
  it('discards clips on page exit and stops capture on destruction', async () => {
    const recorder = TestBed.inject(VoiceRecorder);
    await recorder.start();
    FakeRecorder.latest.data();
    recorder.stop();
    window.dispatchEvent(new Event('pagehide'));
    expect(revoke).toHaveBeenCalledOnce();
    expect(recorder.clip()).toBeNull();
    await recorder.start();
    const last = FakeRecorder.latest;
    TestBed.resetTestingModule();
    expect(last.stop).toHaveBeenCalled();
    expect(last.ondataavailable).toBeNull();
  });
  it('stops and discards an active recording when the page becomes hidden', async () => {
    const recorder = TestBed.inject(VoiceRecorder);
    await recorder.start();
    FakeRecorder.latest.data();
    const descriptor = Object.getOwnPropertyDescriptor(document, 'hidden');
    try {
      Object.defineProperty(document, 'hidden', { configurable: true, value: true });
      document.dispatchEvent(new Event('visibilitychange'));
      expect(recorder.state()).toBe('idle');
      expect(track.stop).toHaveBeenCalled();
      expect(create).not.toHaveBeenCalled();
    } finally {
      if (descriptor) Object.defineProperty(document, 'hidden', descriptor);
      else Reflect.deleteProperty(document, 'hidden');
    }
  });
  it('releases permission granted after the component has already been destroyed', async () => {
    let resolve!: (value: MediaStream) => void;
    getUserMedia.mockReturnValue(
      new Promise<MediaStream>((done) => {
        resolve = done;
      }),
    );
    const recorder = TestBed.inject(VoiceRecorder);
    const pending = recorder.start();
    TestBed.resetTestingModule();
    resolve(stream);
    await pending;
    expect(track.stop).toHaveBeenCalledOnce();
    expect(create).not.toHaveBeenCalled();
  });
  it('fails safely when the browser does not provide recording support', async () => {
    vi.stubGlobal('MediaRecorder', undefined);
    const recorder = TestBed.inject(VoiceRecorder);
    await recorder.start();
    expect(recorder.error()).toContain('supported browser');
    expect(getUserMedia).not.toHaveBeenCalled();
  });
});
