import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../../core/accounts/auth-store';
import { LearningApi } from '../../core/learning/learning-api';
import { MatchingBoard } from './matching-board';

describe('Audio matching playback', () => {
  const api = { audio: vi.fn() };
  beforeEach(() => {
    vi.clearAllMocks();
    vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {});
    vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(() => {});
    api.audio.mockImplementation(async (_session, _exercise, option) => ({
      url: 'http://localhost:3900/private/' + option,
      expiresInSeconds: 300,
    }));
    TestBed.configureTestingModule({
      imports: [MatchingBoard],
      providers: [
        { provide: AuthStore, useValue: { user: signal({ id: 'learner' }) } },
        { provide: LearningApi, useValue: api },
      ],
    });
  });
  async function open(reviewOnly = false) {
    const fixture = TestBed.createComponent(MatchingBoard);
    const audio = {
      source: 'Reviewed source',
      license: 'Original recording',
      durationMilliseconds: 1000,
    };
    fixture.componentRef.setInput('sessionId', 'session');
    fixture.componentRef.setInput('exerciseId', 'exercise');
    fixture.componentRef.setInput('reviewOnly', reviewOnly);
    fixture.componentRef.setInput('options', [
      { id: 'l1', text: 'Clip one', side: 'left', audio },
      { id: 'l2', text: 'Clip two', side: 'left', audio },
      { id: 'r1', text: 'Hello', side: 'right' },
      { id: 'r2', text: 'Goodbye', side: 'right' },
    ]);
    await fixture.whenStable();
    return { fixture, host: fixture.nativeElement as HTMLElement };
  }
  it('does not autoplay and keeps only the explicitly selected recording open', async () => {
    const { fixture, host } = await open();
    expect(api.audio).not.toHaveBeenCalled();
    const buttons = Array.from(host.querySelectorAll<HTMLButtonElement>('button')).filter(
      (button) => button.textContent?.includes('Listen to recording'),
    );
    buttons[0].click();
    await fixture.whenStable();
    const firstPlayer = host.querySelector('audio')!;
    buttons[1].click();
    await fixture.whenStable();
    expect(api.audio).toHaveBeenLastCalledWith('session', 'exercise', 'l2');
    expect(host.querySelectorAll('audio').length).toBe(1);
    expect(firstPlayer.getAttribute('src')).toBeNull();
    fixture.destroy();
  });
  it('reviews saved items without pretending there are zero completed connections', async () => {
    const { fixture, host } = await open(true);
    expect(host.querySelector('.pair-count')).toBeNull();
    expect(host.querySelector('.clear-matches')).toBeNull();
    expect(host.textContent).toContain('Answer labels:');
    expect(host.textContent).toContain('Hello');
    expect(host.querySelector('select')).toBeNull();
    fixture.destroy();
  });
});
