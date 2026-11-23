import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../../core/accounts/auth-store';
import { LearningApi } from '../../core/learning/learning-api';
import { LessonAudio } from './lesson-audio';

describe('Owned lesson audio', () => {
  const user = signal<{ id: string } | null>({ id: 'learner' });
  const api = { audio: vi.fn() };
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'learner' });
    vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {});
    vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(() => {});
    api.audio.mockResolvedValue({
      url: 'http://localhost:3900/private/audio',
      expiresInSeconds: 300,
    });
    TestBed.configureTestingModule({
      imports: [LessonAudio],
      providers: [
        { provide: AuthStore, useValue: { user } },
        { provide: LearningApi, useValue: api },
      ],
    });
  });
  async function open() {
    const fixture = TestBed.createComponent(LessonAudio);
    fixture.componentRef.setInput('sessionId', 'session');
    fixture.componentRef.setInput('exerciseId', 'exercise');
    fixture.componentRef.setInput('audio', {
      source: '<script>source</script>',
      license: 'Original recording permission',
      durationMilliseconds: 1000,
    });
    await fixture.whenStable();
    return { fixture, host: fixture.nativeElement as HTMLElement };
  }

  it('requests a link only after a click and keeps attribution escaped without autoplay', async () => {
    const { fixture, host } = await open();
    expect(api.audio).not.toHaveBeenCalled();
    expect(host.querySelector('script')).toBeNull();
    host.querySelector<HTMLButtonElement>('button')!.click();
    await fixture.whenStable();
    expect(api.audio).toHaveBeenCalledWith('session', 'exercise');
    expect(host.querySelector('audio')!.autoplay).toBe(false);
    const speed = host.querySelector('select')!;
    speed.value = '0.75';
    speed.dispatchEvent(new Event('change'));
    expect(host.querySelector('audio')!.playbackRate).toBe(0.75);
    fixture.destroy();
    expect(HTMLMediaElement.prototype.pause).toHaveBeenCalled();
  });

  it('stops the element and clears its source when the account changes', async () => {
    const { fixture, host } = await open();
    host.querySelector<HTMLButtonElement>('button')!.click();
    await fixture.whenStable();
    const player = host.querySelector('audio')!;
    user.set({ id: 'another' });
    await fixture.whenStable();
    expect(player.getAttribute('src')).toBeNull();
    expect(host.querySelector('audio')).toBeNull();
    expect(HTMLMediaElement.prototype.pause).toHaveBeenCalled();
  });

  it('keeps playback through a token refresh for the same account', async () => {
    const { fixture, host } = await open();
    host.querySelector<HTMLButtonElement>('button')!.click();
    await fixture.whenStable();
    user.set({ id: 'learner' });
    await fixture.whenStable();
    expect(host.querySelector('audio')).not.toBeNull();
    fixture.destroy();
  });

  it('ignores a late link after moving to another exercise', async () => {
    let resolve!: (link: { url: string; expiresInSeconds: number }) => void;
    api.audio.mockReturnValueOnce(
      new Promise((finish) => {
        resolve = finish;
      }),
    );
    const { fixture, host } = await open();
    host.querySelector<HTMLButtonElement>('button')!.click();
    fixture.componentRef.setInput('exerciseId', 'next');
    fixture.detectChanges();
    resolve({ url: 'http://localhost:3900/private/old', expiresInSeconds: 300 });
    await fixture.whenStable();
    expect(host.querySelector('audio')).toBeNull();
  });

  it('shows retry guidance when the server or browser cannot supply audio', async () => {
    api.audio.mockRejectedValueOnce(new Error('Synthetic outage'));
    const { fixture, host } = await open();
    host.querySelector<HTMLButtonElement>('button')!.click();
    await fixture.whenStable();
    expect(host.querySelector('[role=alert]')!.textContent).toContain('could not be opened');
    host.querySelector<HTMLButtonElement>('button')!.click();
    await fixture.whenStable();
    host.querySelector('audio')!.dispatchEvent(new Event('error'));
    await fixture.whenStable();
    expect(host.querySelector('audio')).toBeNull();
    expect(host.querySelector('[role=alert]')!.textContent).toContain('could not play');
  });
  it('requests the selected matching option and ignores a late URL after switching options', async () => {
    let resolve!: (link: { url: string; expiresInSeconds: number }) => void;
    api.audio.mockReturnValueOnce(
      new Promise((finish) => {
        resolve = finish;
      }),
    );
    const { fixture, host } = await open();
    fixture.componentRef.setInput('optionId', 'option-one');
    await fixture.whenStable();
    host.querySelector<HTMLButtonElement>('button')!.click();
    expect(api.audio).toHaveBeenCalledWith('session', 'exercise', 'option-one');
    fixture.componentRef.setInput('optionId', 'option-two');
    fixture.detectChanges();
    resolve({ url: 'http://localhost:3900/private/old-option', expiresInSeconds: 300 });
    await fixture.whenStable();
    expect(host.querySelector('audio')).toBeNull();
  });
});
