import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthStore } from '../../core/accounts/auth-store';
import { LearningApi, SessionView } from '../../core/learning/learning-api';
import { LessonPlayer } from './lesson-player';
import { HeartsApi } from '../../core/learning/hearts-api';
import { LearningStore } from '../../core/learning/learning-store';
import { Pronunciation } from '../../core/learning/pronunciation';

@Component({ template: '' })
class LoginDestination {}
export const savedLesson: SessionView = {
  id: 'session',
  courseId: 'course',
  lessonId: 'lesson',
  versionId: 'version',
  title: 'Say hello',
  explanation: '<script>notHtml()</script>',
  mode: 'Lesson',
  hearts: 5,
  startedAt: '2026-10-28T10:00:00Z',
  completedAt: null,
  score: 0,
  passScore: 80,
  passed: false,
  awardedXp: 0,
  answeredCount: 0,
  correctCount: 0,
  totalCount: 1,
  accuracy: 0,
  currentExercise: { id: 'exercise', type: 'Translation', prompt: 'Translate hello', options: [] },
  lastFeedback: null,
  results: [],
};
describe('Interactive lesson', () => {
  const user = signal<{ id: string } | null>({ id: 'learner' });
  const api = {
    session: vi.fn(),
    answer: vi.fn(),
    complete: vi.fn(),
    progress: vi.fn(),
    audio: vi.fn(),
  };
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'learner' });
    api.progress.mockResolvedValue({ isEnrolled: true, units: [], lessons: [] });
    api.session.mockResolvedValue(structuredClone(savedLesson));
    api.answer.mockResolvedValue({
      isCorrect: true,
      feedback: 'Hola is a greeting.',
      heartsRemaining: 5,
      nextExerciseId: null,
    });
    TestBed.configureTestingModule({
      imports: [LessonPlayer],
      providers: [
        provideRouter([{ path: 'login', component: LoginDestination }]),
        {
          provide: ActivatedRoute,
          useValue: { paramMap: of(convertToParamMap({ sessionId: 'session' })) },
        },
        { provide: AuthStore, useValue: { user } },
        { provide: LearningApi, useValue: api },
        {
          provide: HeartsApi,
          useValue: {
            status: async () => {
              const session = await api.session.mock.results.at(-1)?.value;
              return {
                count: session?.hearts ?? 5,
                maximum: 5,
                refillAt: session?.hearts === 0 ? '2026-10-28T20:00:00Z' : null,
                serverNow: '2026-10-28T19:00:00Z',
              };
            },
          },
        },
      ],
    });
  });
  function fill(host: HTMLElement) {
    const field = host.querySelector<HTMLTextAreaElement>('textarea')!;
    field.value = 'Hola';
    field.dispatchEvent(new Event('input'));
  }
  function submit(host: HTMLElement) {
    host.querySelector('form')!.dispatchEvent(new Event('submit'));
  }
  it('teaches one expression, opens its writing question, then teaches the next expression', async () => {
    const initial: SessionView = {
      ...savedLesson,
      explanation: 'Hola — Hello\nGracias — Thank you',
      totalCount: 2,
      currentExercise: {
        id: 'hello',
        type: 'Translation',
        prompt: 'Translate into Spanish: Hello',
        options: [],
      },
    };
    api.session.mockResolvedValue(initial);
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('app-lesson-intro')?.textContent).toContain('Hola');
    expect(host.querySelector('app-lesson-intro')?.textContent).not.toContain('Gracias');
    expect(host.querySelector('form')).toBeNull();
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((button) => button.textContent?.includes('Now try it'))!
      .click();
    await fixture.whenStable();
    expect(host.querySelector('form textarea')).not.toBeNull();
    expect(api.answer).not.toHaveBeenCalled();
    const store = fixture.debugElement.injector.get(LearningStore);
    store.session.set({
      ...initial,
      answeredCount: 1,
      currentExercise: {
        id: 'thanks',
        type: 'Translation',
        prompt: 'Translate into Spanish: Thank you',
        options: [],
      },
    });
    await fixture.whenStable();
    expect(host.querySelector('app-lesson-intro')?.textContent).toContain('Gracias');
    expect(host.querySelector('form')).toBeNull();
  });
  it('starts with illustrated choices and keeps the answered card visible with bottom feedback', async () => {
    const picture: SessionView = {
      ...savedLesson,
      explanation: 'Kaffee — coffee\nMilch — milk',
      currentExercise: {
        id: 'picture',
        type: 'MultipleChoice',
        prompt: 'Which one of these is “coffee”?',
        options: [
          { id: 'coffee', text: 'Kaffee' },
          { id: 'milk', text: 'Milch' },
        ],
      },
    };
    api.session.mockResolvedValue(picture);
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('app-lesson-intro')).toBeNull();
    expect(host.querySelectorAll('.picture-options img')).toHaveLength(2);
    api.session.mockResolvedValue({
      ...picture,
      answeredCount: 1,
      answeredExercise: picture.currentExercise,
      currentExercise: {
        id: 'word-bank',
        type: 'SentenceBuilder',
        prompt: 'Write this in English: Kaffee',
        options: [
          { id: 'tea', text: 'coffee' },
          { id: 'and', text: 'and' },
        ],
      },
    });
    const radio = host.querySelector<HTMLInputElement>('.picture-options input')!;
    radio.checked = true;
    radio.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    submit(host);
    await fixture.whenStable();
    expect(host.querySelector('.picture-options')).not.toBeNull();
    expect(host.querySelector('app-answer-feedback-panel')?.textContent).toContain('Nice!');
    expect(host.querySelector('.picture-options input:checked')).not.toBeNull();
    expect(host.querySelector('button[type=submit]')).toBeNull();
  });
  it('teaches the visible passage and English meaning before a free-practice reading question', async () => {
    api.session.mockResolvedValue({
      ...savedLesson,
      mode: 'Practice',
      explanation:
        'Read it in context\nHoy llueve. Leo un libro en casa.\nIt is raining today. I am reading at home.',
      currentExercise: {
        id: 'reading',
        type: 'Reading',
        prompt: 'What is the weather like?',
        contextText: 'Hoy llueve. Leo un libro en casa.',
        options: [{ id: 'rain', text: 'Rainy' }],
      },
    });
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const intro = host.querySelector('app-lesson-intro');
    expect(intro?.textContent).toContain('Hoy llueve. Leo un libro en casa.');
    expect(intro?.textContent).toContain('It is raining today.');
    expect(host.querySelector('form')).toBeNull();
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((button) => button.textContent?.includes('Now try it'))!
      .click();
    await fixture.whenStable();
    expect(host.querySelector('form')).not.toBeNull();
    expect(api.answer).not.toHaveBeenCalled();
  });
  it('does not reveal teaching cards inside placement checks', async () => {
    api.session.mockResolvedValue({
      ...savedLesson,
      mode: 'Practice',
      isPlacement: true,
      explanation: 'Hola — Hello',
      currentExercise: { id: 'check', type: 'Translation', prompt: 'Translate hello', options: [] },
    });
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).querySelector('app-lesson-intro')).toBeNull();
  });
  it('sends text without scoring fields, displays feedback and completes from saved state', async () => {
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('.grammar script')).toBeNull();
    expect(host.querySelector('details.grammar')?.hasAttribute('open')).toBe(true);
    expect(
      host.querySelector('details.grammar')!.compareDocumentPosition(host.querySelector('form')!) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(host.querySelector('.character-prompt app-character-cue')).not.toBeNull();
    expect(host.querySelector('.prompt-bubble h2')?.textContent).toContain('Translate hello');
    const answered = { ...savedLesson, answeredCount: 1, correctCount: 1, currentExercise: null };
    api.session.mockResolvedValue(answered);
    fill(host);
    submit(host);
    await fixture.whenStable();
    expect(api.answer).toHaveBeenCalledWith(
      'session',
      expect.objectContaining({
        answerText: 'Hola',
        selectedOptionIds: [],
        sessionExerciseId: 'exercise',
        idempotencyKey: expect.any(String),
      }),
    );
    expect(api.answer.mock.calls[0][1]).not.toHaveProperty('score');
    expect(host.textContent).toContain('Hola is a greeting.');
    host.querySelector<HTMLButtonElement>('.feedback button')!.click();
    await fixture.whenStable();
    api.complete.mockResolvedValue({
      ...answered,
      completedAt: '2026-10-28T10:01:20Z',
      score: 100,
      passed: true,
      awardedXp: 20,
      results: [{ prompt: 'Translate hello', isCorrect: true, feedback: 'Greeting' }],
    });
    host.querySelector<HTMLButtonElement>('.feedback button')!.click();
    await fixture.whenStable();
    expect(host.textContent).toContain('+20');
    expect(host.querySelector('.xp-earned')?.textContent).toContain('+20');
    expect(host.textContent).toContain('1m 20s');
    expect(api.complete).toHaveBeenCalledOnce();
  });
  it('retries an uncertain answer with the same idempotency key', async () => {
    api.answer.mockRejectedValueOnce(new HttpErrorResponse({ status: 0 }));
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    fill(host);
    submit(host);
    await fixture.whenStable();
    expect(host.textContent).toContain('Connection interrupted');
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((button) => button.textContent?.includes('Retry this answer'))!
      .click();
    await fixture.whenStable();
    expect(api.answer.mock.calls[0][1].idempotencyKey).toBe(
      api.answer.mock.calls[1][1].idempotencyKey,
    );
    expect(api.answer).toHaveBeenCalledTimes(2);
  });
  it('submits a sentence tile order and labels a fill-blank answer correctly', async () => {
    api.session.mockResolvedValue({
      ...savedLesson,
      currentExercise: {
        id: 'sentence',
        type: 'SentenceBuilder',
        prompt: 'Build a sentence',
        options: [
          { id: 'a', text: 'café' },
          { id: 'b', text: 'Quiero' },
        ],
      },
    });
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    host.querySelectorAll<HTMLButtonElement>('.word-bank button')[1].click();
    await fixture.whenStable();
    host.querySelectorAll<HTMLButtonElement>('.word-bank button')[0].click();
    await fixture.whenStable();
    submit(host);
    await fixture.whenStable();
    expect(api.answer).toHaveBeenCalledWith(
      'session',
      expect.objectContaining({
        answerText: null,
        selectedOptionIds: [],
        orderedTokenIds: ['b', 'a'],
      }),
    );
  });
  it('accepts a typed missing word without sending an option or token selection', async () => {
    api.session.mockResolvedValue({
      ...savedLesson,
      currentExercise: { id: 'blank', type: 'FillBlank', prompt: 'Yo ____ agua.', options: [] },
    });
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Missing word or phrase');
    fill(host);
    submit(host);
    await fixture.whenStable();
    expect(api.answer).toHaveBeenCalledWith(
      'session',
      expect.objectContaining({ answerText: 'Hola', selectedOptionIds: [], orderedTokenIds: [] }),
    );
  });
  it('submits complete matching pairs without deriving correctness in the browser', async () => {
    api.session.mockResolvedValue({
      ...savedLesson,
      currentExercise: {
        id: 'matching',
        type: 'Matching',
        prompt: 'Connect the greetings',
        options: [
          { id: 'l1', text: 'Hola', side: 'left' },
          { id: 'l2', text: 'Adiós', side: 'left' },
          { id: 'r1', text: 'Hello', side: 'right' },
          { id: 'r2', text: 'Goodbye', side: 'right' },
        ],
      },
    });
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const fields = host.querySelectorAll<HTMLSelectElement>('app-matching-board select');
    fields[0].value = 'r1';
    fields[0].dispatchEvent(new Event('change'));
    await fixture.whenStable();
    submit(host);
    await fixture.whenStable();
    expect(api.answer).not.toHaveBeenCalled();
    fields[1].value = 'r2';
    fields[1].dispatchEvent(new Event('change'));
    await fixture.whenStable();
    submit(host);
    await fixture.whenStable();
    expect(api.answer).toHaveBeenCalledWith(
      'session',
      expect.objectContaining({
        answerText: null,
        selectedOptionIds: [],
        orderedTokenIds: [],
        matchedPairs: [
          { leftOptionId: 'l1', rightOptionId: 'r1' },
          { leftOptionId: 'l2', rightOptionId: 'r2' },
        ],
      }),
    );
  });
  it.each(['Reading', 'DialogueChoice'] as const)(
    'shows %s context and sends only a selected option to the server',
    async (type) => {
      api.session.mockResolvedValue({
        ...savedLesson,
        currentExercise: {
          id: 'contextual',
          type,
          prompt: 'Choose a reply or answer',
          contextText: 'Ana vive en Madrid.\n<script>bad()</script>',
          options: [
            { id: 'a', text: 'Madrid' },
            { id: 'b', text: 'Barcelona' },
          ],
        },
      });
      const fixture = TestBed.createComponent(LessonPlayer);
      await fixture.whenStable();
      const host: HTMLElement = fixture.nativeElement;
      expect(host.querySelector('.context-text')!.textContent).toContain('Ana vive en Madrid.');
      expect(host.querySelector('.context-text script')).toBeNull();
      expect(host.querySelector('textarea')).toBeNull();
      const radio = host.querySelector<HTMLInputElement>('input[type="radio"]')!;
      radio.click();
      radio.dispatchEvent(new Event('change'));
      await fixture.whenStable();
      submit(host);
      await fixture.whenStable();
      expect(api.answer).toHaveBeenCalledWith(
        'session',
        expect.objectContaining({
          answerText: null,
          selectedOptionIds: ['a'],
          orderedTokenIds: [],
          matchedPairs: [],
        }),
      );
      expect(host.textContent).toContain('Hola is a greeting.');
    },
  );
  it('uses a choice for Cyrillic recognition and keeps Ё distinct from Е', async () => {
    api.session.mockResolvedValue({
      ...savedLesson,
      currentExercise: {
        id: 'alphabet',
        type: 'Alphabet',
        prompt: 'Find the letter yo.',
        options: [
          { id: 'e', text: 'Е' },
          { id: 'yo', text: 'Ё' },
        ],
      },
    });
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const radio = host.querySelectorAll<HTMLInputElement>('input[type="radio"]')[1];
    radio.click();
    radio.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    submit(host);
    await fixture.whenStable();
    expect(api.answer).toHaveBeenCalledWith(
      'session',
      expect.objectContaining({ answerText: null, selectedOptionIds: ['yo'] }),
    );
    expect(host.querySelector('textarea, audio')).toBeNull();
  });
  it('uses only the reviewed recording before grading a Cyrillic listening question', async () => {
    api.session.mockResolvedValue({
      ...savedLesson,
      explanation: 'Ё — the letter name',
      currentExercise: {
        id: 'alphabet-listening',
        type: 'Alphabet',
        prompt: 'Choose the letter name you hear.',
        options: [
          { id: 'e', text: 'Е' },
          { id: 'yo', text: 'Ё' },
        ],
        audio: {
          source: 'Russian alphabet recording set',
          license: 'Test recording',
          durationMilliseconds: 850,
        },
      },
    });
    api.audio.mockResolvedValue({
      url: 'https://example.test/reviewed.wav',
      expiresInSeconds: 300,
    });
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const pronunciation = vi.spyOn(fixture.debugElement.injector.get(Pronunciation), 'play');
    expect(host.textContent).toContain('LISTEN FOR THE LETTER');
    expect(host.querySelector('app-lesson-intro, audio')).toBeNull();

    host.querySelector<HTMLButtonElement>('app-lesson-audio button')!.click();
    await fixture.whenStable();
    expect(api.audio).toHaveBeenCalledWith('session', 'alphabet-listening');
    expect(host.querySelector('audio')?.getAttribute('src')).toBe(
      'https://example.test/reviewed.wav',
    );

    host.querySelectorAll<HTMLInputElement>('input[type="radio"]')[1].click();
    await fixture.whenStable();
    expect(pronunciation).not.toHaveBeenCalled();
    submit(host);
    await fixture.whenStable();
    expect(api.answer).toHaveBeenCalledWith(
      'session',
      expect.objectContaining({ answerText: null, selectedOptionIds: ['yo'] }),
    );
  });
  it('restores feedback after refresh without re-submitting an answer', async () => {
    api.session.mockResolvedValue({
      ...savedLesson,
      answeredCount: 1,
      currentExercise: null,
      lastFeedback: {
        exerciseId: 'exercise',
        feedback: {
          isCorrect: false,
          feedback: 'Try another greeting.',
          correctAnswer: 'Hola',
          prompt: 'Translate hello',
          heartsRemaining: 4,
          nextExerciseId: null,
        },
      },
    });
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Try another greeting.');
    expect(fixture.nativeElement.querySelector('.feedback.incorrect h2').textContent).toContain(
      'Incorrect',
    );
    expect(fixture.nativeElement.querySelector('.answer-correction').textContent).toContain('Hola');
    expect(api.answer).not.toHaveBeenCalled();
  });
  it('offers a resumable failure state without showing another account’s late response', async () => {
    let resolve!: (value: SessionView) => void;
    api.session.mockReturnValue(
      new Promise<SessionView>((done) => {
        resolve = done;
      }),
    );
    const fixture = TestBed.createComponent(LessonPlayer);
    fixture.detectChanges();
    user.set({ id: 'someone-else' });
    fixture.detectChanges();
    resolve(savedLesson);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).not.toContain('Say hello');
  });
  it('shows exhausted hearts and a finish action without another answer form', async () => {
    api.session.mockResolvedValue({ ...savedLesson, hearts: 0, currentExercise: null });
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Time for a little practice');
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
  });
  for (const choice of [false, true]) {
    it(`sends the correct ${choice ? 'choice' : 'typed'} Listening payload without client scoring`, async () => {
      api.session.mockResolvedValue({
        ...savedLesson,
        currentExercise: {
          id: 'exercise',
          type: 'Listening',
          prompt: 'Listen and answer',
          options: choice
            ? [
                { id: 'greeting', text: 'Hola' },
                { id: 'farewell', text: 'Adiós' },
              ]
            : [],
        },
      });
      const fixture = TestBed.createComponent(LessonPlayer);
      await fixture.whenStable();
      const host: HTMLElement = fixture.nativeElement;
      if (choice) {
        const option = host.querySelector<HTMLInputElement>('input[type=radio]')!;
        option.checked = true;
        option.dispatchEvent(new Event('change'));
      } else fill(host);
      submit(host);
      await fixture.whenStable();
      expect(api.answer).toHaveBeenCalledWith(
        'session',
        expect.objectContaining({
          answerText: choice ? null : 'Hola',
          selectedOptionIds: choice ? ['greeting'] : [],
        }),
      );
    });
  }
  it('submits an explicit speaking confirmation without score, text or recording data', async () => {
    api.session.mockResolvedValue({
      ...savedLesson,
      currentExercise: {
        id: 'exercise',
        type: 'Speaking',
        prompt: 'Practise the phrase',
        options: [],
        audio: {
          source: 'Reviewed model',
          license: 'Original recording',
          durationMilliseconds: 1000,
        },
      },
    });
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const fallback = Array.from(host.querySelectorAll<HTMLButtonElement>('button')).find((button) =>
      button.textContent?.includes('I can’t record'),
    )!;
    fallback.click();
    await fixture.whenStable();
    const checkbox = host.querySelector<HTMLInputElement>('input[type=checkbox]')!;
    checkbox.checked = true;
    checkbox.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    const save = Array.from(host.querySelectorAll<HTMLButtonElement>('button')).find((button) =>
      button.textContent?.includes('Save practice completion'),
    )!;
    save.click();
    await fixture.whenStable();
    expect(api.answer).toHaveBeenCalledWith(
      'session',
      expect.objectContaining({ practiceCompleted: true, answerText: null, selectedOptionIds: [] }),
    );
    expect(api.answer.mock.calls[0][1]).not.toHaveProperty('isCorrect');
    expect(api.answer.mock.calls[0][1]).not.toHaveProperty('recording');
  });
  it('reloads server hearts after a voice miss without advancing or creating answer feedback', async () => {
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    const store = fixture.debugElement.injector.get(LearningStore);
    api.session.mockResolvedValue({ ...savedLesson, hearts: 4, speakingMissCount: 2 });
    await store.acceptSpeechResult({
      status: 'Mismatch',
      message: 'One heart was used.',
      heartsRemaining: 4,
      freeRetryAvailable: false,
      answer: null,
    });
    expect(store.session()?.hearts).toBe(4);
    expect(store.session()?.speakingMissCount).toBe(2);
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('.heart-loss')?.textContent).toContain('−1 heart');
    expect(store.session()?.answeredCount).toBe(0);
    expect(store.feedback()).toBeNull();
    expect(api.answer).not.toHaveBeenCalled();
  });
  it('retains a successful voice result if reloading the saved lesson fails', async () => {
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    const store = fixture.debugElement.injector.get(LearningStore);
    api.session.mockRejectedValueOnce(new Error('offline'));
    const answer = {
      sessionExerciseId: 'exercise',
      isCorrect: true,
      feedback: 'Matched.',
      heartsRemaining: 5,
      nextExerciseId: null,
    };
    await store.acceptSpeechResult({
      status: 'Matched',
      message: 'Those words matched.',
      heartsRemaining: 5,
      freeRetryAvailable: false,
      answer,
    });
    expect(store.feedback()).toEqual(answer);
    expect(store.error()).toContain('voice check is saved');
    expect(store.busy()).toBe(false);
  });
  it('shows ungraded results without a pronunciation percentage or incorrect label', async () => {
    api.session.mockResolvedValue({
      ...savedLesson,
      completedAt: '2026-10-28T10:01:20Z',
      passed: true,
      currentExercise: null,
      gradedExerciseCount: 0,
      gradedAnsweredCount: 0,
      selfAssessmentCount: 1,
      answeredCount: 1,
      results: [
        {
          prompt: 'Say the phrase',
          isCorrect: false,
          isGraded: false,
          feedback: 'Reflect on the reference.',
        },
      ],
    });
    const fixture = TestBed.createComponent(LessonPlayer);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Ungraded practice');
    expect(host.textContent).toContain('no pronunciation accuracy score');
    expect(host.textContent).not.toContain('0%');
    expect(host.textContent).not.toContain('Keep practising');
    expect(host.textContent).not.toContain('completed this lesson before');
  });
});
