import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { DialogueApi, DialogueView } from '../../core/learning/dialogue-api';
import { Pronunciation } from '../../core/learning/pronunciation';
import { DialoguePage } from './dialogue-page';

describe('Conversation practice', () => {
  it('links to the saved session when the catalog has a newer conversation version', async () => {
    TestBed.configureTestingModule({
      imports: [DialoguePage],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user: signal({ id: 'learner' }) } },
        {
          provide: DialogueApi,
          useValue: {
            list: vi.fn().mockResolvedValue([
              {
                id: 'new',
                title: 'At the café',
                scenario: 'cafe',
                versionNumber: 2,
                resumeSessionId: 'saved',
                hasCompleted: true,
              },
            ]),
          },
        },
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ courseId: 'spanish' })) },
        },
      ],
    });
    const fixture = TestBed.createComponent(DialoguePage);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(
      host.querySelector('[aria-label="Resume conversation: At the café"]')?.getAttribute('href'),
    ).toBe('/learn/spanish/dialogues/saved');
    expect(host.textContent).toContain('1 of 1 conversations finished');
  });

  it('retries an uncertain choice with the same key and shows feedback before the summary', async () => {
    const opening: DialogueView = {
      languageCode: 'es',
      id: 'session',
      dialogueId: 'cafe',
      title: 'At the café',
      completedAt: null,
      current: {
        id: 'opening',
        text: '¿Qué desea?',
        isTerminal: false,
        choices: [
          { id: 'tea', text: 'Un té, por favor.' },
          { id: 'coffee', text: 'Un café, por favor.' },
        ],
      },
      path: [],
    };
    const start = vi.fn().mockResolvedValue({ id: 'replay' });
    const choose = vi
      .fn()
      .mockRejectedValueOnce(new Error('Connection dropped'))
      .mockResolvedValueOnce({
        ...opening,
        awardedXp: 10,
        completedAt: '2026-11-09T09:00:00Z',
        current: { id: 'ending', text: 'Aquí tiene su té.', isTerminal: true, choices: [] },
        reviewPhrases: [{ text: 'Un té, por favor.', tip: 'A polite request.' }],
        path: [
          {
            prompt: '¿Qué desea?',
            response: 'Un té, por favor.',
            feedback: 'A polite request.',
            isCorrect: true,
          },
        ],
      });
    TestBed.configureTestingModule({
      imports: [DialoguePage],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user: signal({ id: 'learner' }) } },
        {
          provide: DialogueApi,
          useValue: { read: vi.fn().mockResolvedValue(opening), choose, start },
        },
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({ courseId: 'spanish', sessionId: 'session' })),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(DialoguePage);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const button = (text: string) =>
      Array.from(host.querySelectorAll('button')).find((item) => item.textContent?.includes(text))!;

    button('Un té').click();
    await fixture.whenStable();
    expect(button('Un café').disabled).toBe(true);
    button('Retry').click();
    await fixture.whenStable();
    expect(choose.mock.calls[0]).toEqual(choose.mock.calls[1]);
    expect(host.textContent).toContain('A polite request.');
    expect(document.activeElement?.textContent).toContain('Good response');
    expect(host.textContent).not.toContain('The path you took');

    button('See conversation summary').click();
    await fixture.whenStable();
    expect(host.textContent).toContain('Aquí tiene su té.');
    expect(document.activeElement?.getAttribute('lang')).toBe('es');
    const pronunciation = TestBed.inject(Pronunciation);
    const play = vi.spyOn(pronunciation, 'play').mockImplementation(async () => {
      pronunciation.message.set('Audio could not play. Tap Listen to try again.');
    });
    host.querySelector<HTMLButtonElement>('[aria-label="Listen: Aquí tiene su té."]')!.click();
    expect(play).toHaveBeenCalledWith('Aquí tiene su té.', 'es', false, false);
    await fixture.whenStable();
    expect(host.querySelector('[role="status"]')?.textContent).toContain('Audio could not play');
    expect(document.activeElement?.textContent).toContain('Aquí tiene su té.');
    expect(host.textContent).toContain('The path you took');
    expect(host.querySelector('[aria-labelledby="dialogue-reward"]')?.textContent).toContain(
      '+10 XP',
    );
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    button('Try another path').click();
    await fixture.whenStable();
    expect(start).toHaveBeenCalledWith('cafe');
    expect(navigate).toHaveBeenCalledWith(['/learn', 'spanish', 'dialogues', 'replay']);
    expect(host.querySelector('[aria-labelledby="dialogue-review"]')?.textContent).toContain(
      'Un té, por favor.',
    );
  });
});
