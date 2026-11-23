import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../../core/accounts/auth-store';
import { LearningApi } from '../../core/learning/learning-api';
import { VoiceRecorder } from '../../core/learning/voice-recorder';
import { SpeakingExercise } from './speaking-exercise';

describe('Guided speaking self-assessment', () => {
  const user = signal<{ id: string } | null>({ id: 'learner' });
  const recorder = {
    state: signal('idle'),
    seconds: signal(0),
    clip: signal<string | null>(null),
    error: signal(''),
    start: vi.fn(),
    stop: vi.fn(),
    clear: vi.fn(),
    playbackError: vi.fn(),
  };
  const api = { audio: vi.fn() };
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'learner' });
    recorder.state.set('idle');
    recorder.clip.set(null);
    recorder.error.set('');
    recorder.clear.mockImplementation(() => {
      recorder.state.set('idle');
      recorder.clip.set(null);
      recorder.error.set('');
    });
    recorder.start.mockImplementation(async () => {
      recorder.state.set('recording');
    });
    recorder.stop.mockImplementation(() => {
      recorder.state.set('ready');
      recorder.clip.set('blob:local-speaking-test');
    });
    api.audio.mockResolvedValue({
      url: 'http://localhost:3900/private/reference',
      expiresInSeconds: 300,
    });
    vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {});
    vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(() => {});
    TestBed.configureTestingModule({
      imports: [SpeakingExercise],
      providers: [
        { provide: AuthStore, useValue: { user } },
        { provide: LearningApi, useValue: api },
      ],
    });
    TestBed.overrideComponent(SpeakingExercise, {
      set: { providers: [{ provide: VoiceRecorder, useValue: recorder }] },
    });
  });
  async function open(preview = false) {
    const fixture = TestBed.createComponent(SpeakingExercise);
    fixture.componentRef.setInput('exerciseId', 'exercise');
    if (preview) fixture.componentRef.setInput('preview', true);
    else {
      fixture.componentRef.setInput('sessionId', 'session');
      fixture.componentRef.setInput('audio', {
        source: 'Reviewed reference',
        license: 'Original recording',
        durationMilliseconds: 1000,
      });
    }
    const completed = vi.fn();
    fixture.componentInstance.completed.subscribe(completed);
    await fixture.whenStable();
    return { fixture, host: fixture.nativeElement as HTMLElement, completed };
  }
  function button(host: HTMLElement, text: string) {
    return Array.from(host.querySelectorAll<HTMLButtonElement>('button')).find((item) =>
      item.textContent?.includes(text),
    )!;
  }
  function confirm(host: HTMLElement) {
    const checkbox = host.querySelector<HTMLInputElement>('input[type=checkbox]')!;
    checkbox.checked = true;
    checkbox.dispatchEvent(new Event('change'));
  }

  it('completes manual preview without fetching audio from a learner session', async () => {
    const { fixture, host, completed } = await open(true);
    expect(host.querySelector('app-lesson-audio')).toBeNull();
    button(host, 'I can’t record').click();
    await fixture.whenStable();
    expect(button(host, 'Finish preview practice').disabled).toBe(true);
    confirm(host);
    await fixture.whenStable();
    button(host, 'Finish preview practice').click();
    expect(completed).toHaveBeenCalledOnce();
    expect(api.audio).not.toHaveBeenCalled();
    expect(recorder.start).not.toHaveBeenCalled();
  });

  it('records only after a click and saves nothing automatically after recording or listening', async () => {
    const { fixture, host, completed } = await open();
    expect(recorder.start).not.toHaveBeenCalled();
    expect(api.audio).not.toHaveBeenCalled();
    button(host, 'Record my voice').click();
    await fixture.whenStable();
    expect(recorder.start).toHaveBeenCalledOnce();
    button(host, 'Stop recording').click();
    await fixture.whenStable();
    expect(completed).not.toHaveBeenCalled();
    host.querySelector('audio')!.dispatchEvent(new Event('ended'));
    await fixture.whenStable();
    expect(recorder.clip()).toBeNull();
    expect(completed).not.toHaveBeenCalled();
    confirm(host);
    await fixture.whenStable();
    button(host, 'Save practice completion').click();
    expect(completed).toHaveBeenCalledOnce();
    expect(host.textContent).toContain('pronunciation is not scored');
  });

  it('offers a manual self-assessment fallback after denied microphone access', async () => {
    recorder.start.mockImplementationOnce(async () => {
      recorder.state.set('error');
      recorder.error.set('Microphone access wasn’t allowed.');
    });
    const { fixture, host, completed } = await open();
    button(host, 'Record my voice').click();
    await fixture.whenStable();
    expect(host.querySelector('[role=alert]')!.textContent).toContain('wasn’t allowed');
    button(host, 'I can’t record').click();
    await fixture.whenStable();
    expect(host.textContent).toContain('without sharing a recording');
    confirm(host);
    await fixture.whenStable();
    button(host, 'Save practice completion').click();
    expect(completed).toHaveBeenCalledOnce();
  });

  it('clears private recording data when account or exercise changes', async () => {
    const { fixture, host } = await open();
    button(host, 'Record my voice').click();
    await fixture.whenStable();
    button(host, 'Stop recording').click();
    await fixture.whenStable();
    const player = host.querySelector('audio')!;
    user.set({ id: 'another' });
    await fixture.whenStable();
    expect(recorder.clip()).toBeNull();
    expect(player.getAttribute('src')).toBeNull();
    expect(host.querySelector('input[type=checkbox]')).toBeNull();
    fixture.componentRef.setInput('exerciseId', 'next');
    await fixture.whenStable();
    expect(recorder.state()).toBe('idle');
  });

  it('stops recording before opening the reference and stops native playback on destruction', async () => {
    const { fixture, host } = await open();
    button(host, 'Record my voice').click();
    await fixture.whenStable();
    button(host, 'Listen to recording').click();
    await fixture.whenStable();
    expect(recorder.state()).toBe('idle');
    expect(api.audio).toHaveBeenCalledWith('session', 'exercise');
    fixture.destroy();
    expect(HTMLMediaElement.prototype.pause).toHaveBeenCalled();
    expect(recorder.clear).toHaveBeenCalled();
  });
});
