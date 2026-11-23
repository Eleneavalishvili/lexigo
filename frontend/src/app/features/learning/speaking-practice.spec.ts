import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { VoiceRecorder } from '../../core/learning/voice-recorder';
import { SpeakingPractice } from './speaking-practice';

describe('Speaking self-assessment page', () => {
  it('opens Premium listening and reveals the meaning after the server checks a choice', async () => {
    const user = signal({ id: 'learner' });
    TestBed.configureTestingModule({
      imports: [SpeakingPractice],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthStore, useValue: { user } },
      ],
    });
    const fixture = TestBed.createComponent(SpeakingPractice);
    const http = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const button = (label: string) =>
      [...host.querySelectorAll('button')].find(
        (candidate) => candidate.textContent?.trim() === label,
      )!;
    const route = '/api/v1/me/voice-practice/es';
    button('Start guided practice').click();
    http.expectOne(route).flush([{ id: 0, phrase: 'café', choices: ['coffee', 'milk', 'water'] }]);
    await fixture.whenStable();
    expect(host.textContent).not.toContain('café');
    button('coffee').click();
    const answer = http.expectOne(route + '/0/answers');
    expect(answer.request.body).toEqual({ choice: 'coffee' });
    answer.flush({ correct: true, meaning: 'coffee' });
    await fixture.whenStable();
    expect(host.textContent).toContain('café');
    button('Continue').click();
    await fixture.whenStable();
    expect(host.textContent).toContain('Guided practice complete');
    http.verify();
  });

  it('requests recording only on user action and discards after playback/account changes', async () => {
    vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {});
    vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(() => {});
    const user = signal<{ id: string } | null>({ id: 'first' });
    const recorder = {
      state: signal('ready'),
      clip: signal<string | null>('blob:local-test'),
      seconds: signal(0),
      error: signal(''),
      start: vi.fn(),
      stop: vi.fn(),
      clear: vi.fn(),
      playbackError: vi.fn(),
    };
    TestBed.configureTestingModule({
      imports: [SpeakingPractice],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: AuthStore, useValue: { user } },
      ],
    });
    TestBed.overrideComponent(SpeakingPractice, {
      set: { providers: [{ provide: VoiceRecorder, useValue: recorder }] },
    });
    const fixture = TestBed.createComponent(SpeakingPractice);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(recorder.start).not.toHaveBeenCalled();
    expect(host.textContent).toContain('never uploaded');
    host.querySelector<HTMLAudioElement>('audio')!.dispatchEvent(new Event('ended'));
    await fixture.whenStable();
    expect(recorder.clear).toHaveBeenCalledOnce();
    user.set({ id: 'second' });
    await fixture.whenStable();
    expect(recorder.clear).toHaveBeenCalledTimes(2);
    host.querySelector<HTMLButtonElement>('.recording-actions button')!.click();
    await fixture.whenStable();
    expect(recorder.start).toHaveBeenCalledOnce();
    expect(host.textContent).toContain('does not produce a pronunciation score');
  });
});
