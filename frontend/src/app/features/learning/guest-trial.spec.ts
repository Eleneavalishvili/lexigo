import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { GuestTrial, GuestTrialApi } from '../../core/learning/guest-trial-api';
import { Pronunciation } from '../../core/learning/pronunciation';
import { GuestTrialPage } from './guest-trial';

describe('Guest lesson trial', () => {
  const courseId = '10000000-0000-0000-0000-000000000001';
  const lesson: GuestTrial = {
    courseId,
    versionId: 'published',
    title: 'First drinks',
    explanation: 'café — coffee\nagua — water',
    questions: [
      {
        id: 'coffee',
        prompt: 'Which word means coffee?',
        options: [
          { id: 'a', text: 'café' },
          { id: 'b', text: 'agua' },
        ],
      },
      {
        id: 'water',
        prompt: 'Which word means water?',
        options: [
          { id: 'c', text: 'agua' },
          { id: 'd', text: 'café' },
        ],
      },
    ],
  };
  const api = { read: vi.fn(), check: vi.fn() };
  const audio = {
    play: vi.fn(),
    stop: vi.fn(),
    speaking: signal(false),
    text: signal(''),
    language: signal('es'),
    message: signal(''),
  };
  let params: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

  beforeEach(() => {
    vi.clearAllMocks();
    api.read.mockResolvedValue(lesson);
    api.check.mockResolvedValue({
      isCorrect: true,
      correctAnswer: 'agua',
      explanation: 'agua means water.',
    });
    audio.play.mockResolvedValue(undefined);
    params = new BehaviorSubject(convertToParamMap({ courseId }));
    TestBed.configureTestingModule({
      imports: [GuestTrialPage],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: params } },
        { provide: GuestTrialApi, useValue: api },
        { provide: Pronunciation, useValue: audio },
      ],
    });
  });

  function button(host: HTMLElement, text: string): HTMLButtonElement {
    return Array.from(host.querySelectorAll<HTMLButtonElement>('button')).find((item) =>
      item.textContent?.includes(text),
    )!;
  }

  async function begin() {
    const fixture = TestBed.createComponent(GuestTrialPage);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('coffee');
    expect(host.querySelector('input[type=radio]')).toBeNull();
    button(host, 'Next word').click();
    await fixture.whenStable();
    button(host, 'Start the questions').click();
    await fixture.whenStable();
    return { fixture, host };
  }

  it('teaches first, waits for server feedback and completes without promising XP', async () => {
    const { fixture, host } = await begin();
    expect(button(host, 'Check answer').disabled).toBe(true);
    expect(api.check).not.toHaveBeenCalled();
    api.check.mockResolvedValueOnce({
      isCorrect: false,
      correctAnswer: 'café',
      explanation: 'café means coffee.',
    });
    host.querySelectorAll<HTMLInputElement>('input[type=radio]')[1].click();
    await fixture.whenStable();
    expect(audio.play).toHaveBeenCalledWith('agua', 'es');
    button(host, 'Check answer').click();
    await fixture.whenStable();
    expect(api.check).toHaveBeenCalledWith(courseId, 'published', 'coffee', 'b');
    expect(host.querySelector('.feedback.incorrect')?.textContent).toContain('café');
    expect(host.textContent).toContain('Question 1 of 2');
    button(host, 'Continue →').click();
    await fixture.whenStable();
    host.querySelector<HTMLInputElement>('input[type=radio]')!.click();
    await fixture.whenStable();
    button(host, 'Check answer').click();
    await fixture.whenStable();
    button(host, 'Continue →').click();
    await fixture.whenStable();
    expect(host.textContent).toContain('1 of 2 correct');
    expect(host.textContent).toContain('This trial awards no XP');
    expect(host.querySelector('a.button-red')?.getAttribute('href')).toBe('/learn/' + courseId);
    fixture.destroy();
    expect(audio.stop).toHaveBeenCalled();
  });

  it('keeps an unanswered question available after a network failure', async () => {
    const { fixture, host } = await begin();
    api.check.mockRejectedValueOnce(new Error('Synthetic outage'));
    host.querySelector<HTMLInputElement>('input[type=radio]')!.click();
    await fixture.whenStable();
    button(host, 'Check answer').click();
    await fixture.whenStable();
    expect(host.querySelector('[role=alert]')?.textContent).toContain('could not be checked');
    expect(host.querySelector('.feedback')).toBeNull();
    expect(host.textContent).toContain('Question 1 of 2');
    button(host, 'Check answer').click();
    await fixture.whenStable();
    expect(host.querySelector('.feedback')).not.toBeNull();
    expect(api.check).toHaveBeenCalledTimes(2);
  });

  it('discards a previous language response after the route changes', async () => {
    let finish!: (lesson: GuestTrial) => void;
    api.read.mockReturnValueOnce(
      new Promise<GuestTrial>((resolve) => {
        finish = resolve;
      }),
    );
    const fixture = TestBed.createComponent(GuestTrialPage);
    fixture.detectChanges();
    api.read.mockResolvedValue({ ...lesson, title: 'Latest language' });
    params.next(convertToParamMap({ courseId: 'different-course' }));
    await fixture.whenStable();
    finish(lesson);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('h1')?.textContent).toBe('Latest language');
  });
});
