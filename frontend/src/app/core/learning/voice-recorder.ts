import { DestroyRef, Injectable, inject, signal } from '@angular/core';

type RecordingState = 'idle' | 'requesting' | 'recording' | 'processing' | 'ready' | 'error';

@Injectable()
export class VoiceRecorder {
  readonly state = signal<RecordingState>('idle');
  readonly error = signal('');
  readonly seconds = signal(0);
  readonly clip = signal<string | null>(null);
  private recorder: MediaRecorder | null = null;
  private stream: MediaStream | null = null;
  private chunks: Blob[] = [];
  private captured: Blob | null = null;
  private size = 0;
  private generation = 0;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private ticker: ReturnType<typeof setInterval> | undefined;
  private readonly destroy = inject(DestroyRef);

  constructor() {
    const hidden = () => {
      if (document.hidden) this.clear();
    };
    const leaving = () => this.clear();
    document.addEventListener('visibilitychange', hidden);
    window.addEventListener('pagehide', leaving);
    this.destroy.onDestroy(() => {
      this.clear();
      document.removeEventListener('visibilitychange', hidden);
      window.removeEventListener('pagehide', leaving);
    });
  }

  async start(): Promise<void> {
    if (['requesting', 'recording', 'processing'].includes(this.state())) return;
    this.clear();
    if (
      !window.isSecureContext ||
      typeof MediaRecorder === 'undefined' ||
      !navigator.mediaDevices?.getUserMedia
    ) {
      this.fail('Recording needs a supported browser on HTTPS or localhost.');
      return;
    }
    const generation = this.generation;
    this.state.set('requesting');
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true, video: false });
      if (!this.current(generation)) {
        stream.getTracks().forEach((track) => track.stop());
        return;
      }
      this.stream = stream;
      const mimeType = ['audio/webm;codecs=opus', 'audio/ogg;codecs=opus', 'audio/mp4'].find(
        (type) => MediaRecorder.isTypeSupported(type),
      );
      const recorder = mimeType
        ? new MediaRecorder(stream, { mimeType })
        : new MediaRecorder(stream);
      this.recorder = recorder;
      recorder.ondataavailable = (event) => {
        if (!this.current(generation) || !event.data.size) return;
        this.size += event.data.size;
        if (this.size > 5 * 1024 * 1024) {
          this.fail('This clip exceeded the 5 MB limit. Try a shorter recording.');
          return;
        }
        this.chunks.push(event.data);
      };
      recorder.onerror = () => {
        if (this.current(generation))
          this.fail('The microphone stopped unexpectedly. Please try again.');
      };
      recorder.onstop = () => {
        if (!this.current(generation)) return;
        this.releaseMicrophone();
        this.clearTimers();
        const clip = new Blob(this.chunks, {
          type: recorder.mimeType || this.chunks[0]?.type || 'audio/webm',
        });
        this.chunks = [];
        this.recorder = null;
        if (!clip.size) {
          this.fail('No audio was captured. Check your microphone and try again.');
          return;
        }
        this.clip.set(URL.createObjectURL(clip));
        this.captured = clip;
        this.state.set('ready');
      };
      recorder.start(250);
      this.state.set('recording');
      const started = Date.now();
      this.ticker = setInterval(
        () =>
          this.seconds.set(Math.max(0, Math.min(30, Math.floor((Date.now() - started) / 1000)))),
        250,
      );
      this.timer = setTimeout(() => this.stop(), 30000);
    } catch (error) {
      if (!this.current(generation)) return;
      const name = error instanceof DOMException ? error.name : '';
      this.fail(
        name === 'NotAllowedError'
          ? 'Microphone access wasn’t allowed. Check your browser permissions before trying again.'
          : name === 'NotFoundError'
            ? 'No microphone was found. Connect one and try again.'
            : 'We couldn’t start your microphone. Check that another app is not using it.',
      );
    }
  }

  stop(): void {
    if (this.state() !== 'recording' || !this.recorder) return;
    this.state.set('processing');
    this.clearTimers();
    try {
      if (this.recorder.state !== 'inactive') this.recorder.stop();
    } catch {
      this.fail('The recording could not be finished. Please try again.');
    } finally {
      this.releaseMicrophone();
    }
  }

  clear(): void {
    ++this.generation;
    this.clearTimers();
    const recorder = this.recorder;
    this.recorder = null;
    if (recorder) {
      recorder.ondataavailable = null;
      recorder.onstop = null;
      recorder.onerror = null;
      try {
        if (recorder.state !== 'inactive') recorder.stop();
      } catch {
        /* Track cleanup still runs. */
      }
    }
    this.releaseMicrophone();
    const clip = this.clip();
    if (clip) URL.revokeObjectURL(clip);
    this.clip.set(null);
    this.captured = null;
    this.chunks = [];
    this.size = 0;
    this.seconds.set(0);
    this.error.set('');
    this.state.set('idle');
  }

  playbackError(): void {
    this.fail(
      'This browser couldn’t play the recording. Try another supported audio format or browser.',
    );
  }
  recording(): Blob | null {
    return this.captured;
  }
  private fail(message: string): void {
    this.clear();
    this.error.set(message);
    this.state.set('error');
  }
  private current(generation: number): boolean {
    return !this.destroy.destroyed && generation === this.generation;
  }
  private releaseMicrophone(): void {
    this.stream?.getTracks().forEach((track) => track.stop());
    this.stream = null;
  }
  private clearTimers(): void {
    clearTimeout(this.timer);
    clearInterval(this.ticker);
    this.timer = undefined;
    this.ticker = undefined;
  }
}
