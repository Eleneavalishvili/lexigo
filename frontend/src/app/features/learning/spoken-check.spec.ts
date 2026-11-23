import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../../core/accounts/auth-store';
import { ContentApi } from '../../core/content/content-api';
import { LearningApi } from '../../core/learning/learning-api';
import { VoiceRecorder } from '../../core/learning/voice-recorder';
import { SpokenCheck } from './spoken-check';

describe('In-lesson spoken word checks', () => {
  const user = signal<{ id: string } | null>({ id: 'owner' });
  const recorder = {
    state: signal('idle'),
    clip: signal<string | null>(null),
    seconds: signal(0),
    error: signal(''),
    start: vi.fn(),
    stop: vi.fn(),
    clear: vi.fn(),
    recording: vi.fn(),
  };
  const api = { audio: vi.fn(), checkSpeech: vi.fn() };
  const content = { checkPreviewSpeech: vi.fn() };
  const mismatch = {
    status: 'Mismatch',
    message: 'No heart was used this time.',
    heartsRemaining: 5,
    freeRetryAvailable: false,
    answer: null,
  };
  const decoded = {
    numberOfChannels: 1,
    sampleRate: 16000,
    getChannelData: () => new Float32Array(1600),
  };
  const decode = vi.fn();
  afterEach(() => vi.unstubAllGlobals());
  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'owner' });
    recorder.state.set('idle');
    recorder.clip.set(null);
    recorder.error.set('');
    recorder.clear.mockImplementation(() => {
      recorder.state.set('idle');
      recorder.clip.set(null);
    });
    recorder.start.mockImplementation(async () => {
      recorder.state.set('recording');
    });
    recorder.stop.mockImplementation(() => {
      recorder.state.set('ready');
      recorder.clip.set('blob:fixture');
    });
    recorder.recording.mockImplementation(() =>
      recorder.clip() ? new Blob(['encoded voice']) : null,
    );
    decode.mockResolvedValue(decoded);
    vi.stubGlobal(
      'OfflineAudioContext',
      class {
        decodeAudioData = decode;
      },
    );
    api.checkSpeech.mockResolvedValue(mismatch);
    api.audio.mockResolvedValue({ url: 'http://localhost:3900/reference', expiresInSeconds: 300 });
    vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {});
    vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(() => {});
    TestBed.configureTestingModule({
      imports: [SpokenCheck],
      providers: [
        { provide: AuthStore, useValue: { user } },
        { provide: LearningApi, useValue: api },
        { provide: ContentApi, useValue: content },
      ],
    });
    TestBed.overrideComponent(SpokenCheck, {
      set: { providers: [{ provide: VoiceRecorder, useValue: recorder }] },
    });
  });
  function button(host: HTMLElement, text: string) {
    return Array.from(host.querySelectorAll<HTMLButtonElement>('button')).find((item) =>
      item.textContent?.includes(text),
    )!;
  }
  async function open() {
    const fixture = TestBed.createComponent(SpokenCheck);
    fixture.componentRef.setInput('sessionId', 'session');
    fixture.componentRef.setInput('exerciseId', 'exercise');
    fixture.componentRef.setInput('audio', {
      source: 'Reviewed fixture',
      license: 'Original fixture',
      durationMilliseconds: 1000,
    });
    const checked = vi.fn();
    fixture.componentInstance.checked.subscribe(checked);
    const activity = vi.fn();
    fixture.componentInstance.activity.subscribe(activity);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    return { fixture, host, checked, activity };
  }
  async function record(host: HTMLElement, fixture: ReturnType<typeof TestBed.createComponent>) {
    button(host, 'Record my answer').click();
    await fixture.whenStable();
    button(host, 'Stop recording').click();
    await fixture.whenStable();
  }
  it('requires an explicit check and emits server feedback without client scoring', async () => {
    const { fixture, host, checked, activity } = await open();
    expect(api.checkSpeech).not.toHaveBeenCalled();
    await record(host, fixture);
    expect(api.checkSpeech).not.toHaveBeenCalled();
    expect(activity).toHaveBeenCalledWith('listening');
    button(host, 'Check my words').click();
    await fixture.whenStable();
    expect(api.checkSpeech).toHaveBeenCalledExactlyOnceWith(
      'session',
      'exercise',
      expect.any(String),
      expect.any(Blob),
      expect.any(AbortSignal),
    );
    expect(checked).toHaveBeenCalledExactlyOnceWith(mismatch);
    expect(host.textContent).toContain('No heart was used');
    expect(recorder.clip()).toBeNull();
    expect(activity).toHaveBeenCalledWith('encouragement');
  });
  it('checks a preview recording without submitting a learner answer', async () => {
    const { fixture, host, checked } = await open();
    fixture.componentRef.setInput('preview', { lessonId: 'draft', versionId: 'revision' });
    fixture.componentRef.setInput('audio', null);
    await fixture.whenStable();
    content.checkPreviewSpeech.mockResolvedValue({
      status: 'Matched',
      message: 'The words match.',
    });
    await record(host, fixture);
    expect(content.checkPreviewSpeech).not.toHaveBeenCalled();
    button(host, 'Check my words').click();
    await fixture.whenStable();
    expect(content.checkPreviewSpeech).toHaveBeenCalledExactlyOnceWith(
      'draft',
      'revision',
      'exercise',
      expect.any(Blob),
      expect.any(AbortSignal),
    );
    expect(api.checkSpeech).not.toHaveBeenCalled();
    expect(checked).not.toHaveBeenCalled();
    expect(host.textContent).toContain('The words match.');
    expect(recorder.clip()).toBeNull();
  });
  it('reuses the same recording and request key after a connection failure', async () => {
    api.checkSpeech.mockRejectedValueOnce(new HttpErrorResponse({ status: 503 }));
    const { fixture, host } = await open();
    await record(host, fixture);
    button(host, 'Check my words').click();
    await fixture.whenStable();
    const first = api.checkSpeech.mock.calls[0];
    button(host, 'Check my words').click();
    await fixture.whenStable();
    const second = api.checkSpeech.mock.calls[1];
    expect(second[2]).toBe(first[2]);
    expect(second[3]).toBe(first[3]);
    expect(decode).toHaveBeenCalledOnce();
  });
  it('cancels and ignores an uploaded check when the account changes', async () => {
    let finish!: (value: unknown) => void;
    api.checkSpeech.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    );
    const { fixture, host, checked } = await open();
    await record(host, fixture);
    button(host, 'Check my words').click();
    await vi.waitFor(() => expect(api.checkSpeech).toHaveBeenCalledOnce());
    const signal = api.checkSpeech.mock.calls[0][4] as AbortSignal;
    user.set({ id: 'someone-else' });
    fixture.detectChanges();
    finish(mismatch);
    await fixture.whenStable();
    expect(signal.aborted).toBe(true);
    expect(checked).not.toHaveBeenCalled();
    expect(recorder.clip()).toBeNull();
  });
  it('does not upload a recording whose conversion finishes after cancellation', async () => {
    let finish!: (value: unknown) => void;
    decode.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          finish = resolve;
        }),
    );
    const { fixture, host } = await open();
    await record(host, fixture);
    button(host, 'Check my words').click();
    await vi.waitFor(() => expect(finish).toBeDefined());
    button(host, 'Discard recording').click();
    finish(decoded);
    await fixture.whenStable();
    expect(api.checkSpeech).not.toHaveBeenCalled();
    expect(recorder.clip()).toBeNull();
  });
});
