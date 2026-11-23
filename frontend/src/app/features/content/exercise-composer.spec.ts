import { TestBed } from '@angular/core/testing';
import { ExerciseComposer } from './exercise-composer';

describe('Text exercise authoring', () => {
  async function open() {
    TestBed.configureTestingModule({ imports: [ExerciseComposer] });
    const fixture = TestBed.createComponent(ExerciseComposer);
    fixture.componentRef.setInput('nextOrder', 1);
    await fixture.whenStable();
    return fixture;
  }
  function fill(host: HTMLElement, selector: string, text: string) {
    const input = host.querySelector<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>(
      selector,
    )!;
    input.value = text;
    input.dispatchEvent(new Event(input instanceof HTMLSelectElement ? 'change' : 'input'));
  }
  it('validates numbered permutations and sends token orders separately from text answer keys', async () => {
    const fixture = await open();
    fixture.componentRef.setInput('skills', [{ id: 7, name: 'Grammar' }]);
    const host: HTMLElement = fixture.nativeElement;
    const submitted = vi.fn();
    fixture.componentInstance.add.subscribe(submitted);
    fill(host, 'select', 'SentenceBuilder');
    await fixture.whenStable();
    fill(host, '#exercise-prompt', 'Build a polite sentence');
    fill(host, '#exercise-explanation', 'English feedback');
    fill(host, '#exercise-options', 'café\nQuiero\npor favor');
    fill(host, '#exercise-token-orders', '2 1 1');
    host.querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(submitted).not.toHaveBeenCalled();
    expect(host.textContent).toContain('each selected tile number once');
    fill(host, '#exercise-token-orders', '2 1 3');
    (host.querySelector('.exercise-skills input') as HTMLInputElement).click();
    host.querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(submitted).toHaveBeenCalledWith(
      expect.objectContaining({
        type: 'SentenceBuilder',
        options: ['café', 'Quiero', 'por favor'],
        answers: [],
        tokenOrders: [[2, 1, 3]],
        skillIds: [7],
      }),
    );
  });
  it('builds distinct matching request pairs and restores a private staff mapping on edit', async () => {
    const fixture = await open();
    const host: HTMLElement = fixture.nativeElement;
    const submitted = vi.fn();
    fixture.componentInstance.add.subscribe(submitted);
    fill(host, 'select', 'Matching');
    await fixture.whenStable();
    fill(host, '#exercise-prompt', 'Connect the words');
    fill(host, '#exercise-explanation', 'English feedback');
    fill(host, '#exercise-matching-left', 'Hola\nAdiós');
    fill(host, '#exercise-matching-right', 'Hello');
    host.querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(submitted).not.toHaveBeenCalled();
    fill(host, '#exercise-matching-right', 'Hello\nGoodbye');
    host.querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(submitted).toHaveBeenCalledWith(
      expect.objectContaining({
        type: 'Matching',
        options: [],
        answers: [],
        tokenOrders: [],
        matchPairs: [
          { left: 'Hola', right: 'Hello', leftAudioMediaId: null },
          { left: 'Adiós', right: 'Goodbye', leftAudioMediaId: null },
        ],
      }),
    );
    fixture.componentRef.setInput('editing', {
      id: 'matching',
      type: 'Matching',
      prompt: 'Connect',
      explanation: 'English',
      orderNumber: 1,
      options: [
        { id: 'a', text: 'Hola', orderNumber: 1, matchKey: 'private-a' },
        { id: 'b', text: 'Adiós', orderNumber: 2, matchKey: 'private-b' },
        { id: 'c', text: 'Hello', orderNumber: 3, matchKey: 'private-a' },
        { id: 'd', text: 'Goodbye', orderNumber: 4, matchKey: 'private-b' },
      ],
      answers: [],
    });
    await fixture.whenStable();
    expect(host.querySelector<HTMLTextAreaElement>('#exercise-matching-right')!.value).toBe(
      'Hello\nGoodbye',
    );
  });
  it.each(['Reading', 'DialogueChoice'])(
    'requires and restores a separate context for %s',
    async (type) => {
      const fixture = await open();
      const host: HTMLElement = fixture.nativeElement;
      const submitted = vi.fn();
      fixture.componentInstance.add.subscribe(submitted);
      fill(host, 'select', type);
      await fixture.whenStable();
      fill(host, '#exercise-prompt', 'Choose the answer');
      fill(host, '#exercise-explanation', 'English feedback');
      fill(host, '#exercise-options', 'Madrid\nBarcelona');
      fill(host, '#exercise-answers', 'Madrid');
      host.querySelector('form')!.dispatchEvent(new Event('submit'));
      await fixture.whenStable();
      expect(submitted).not.toHaveBeenCalled();
      expect(host.textContent).toContain('Add a reading passage or dialogue situation.');
      fill(host, '#exercise-context', 'Ana vive en Madrid.');
      host.querySelector('form')!.dispatchEvent(new Event('submit'));
      await fixture.whenStable();
      expect(submitted).toHaveBeenCalledWith(
        expect.objectContaining({
          type,
          contextText: 'Ana vive en Madrid.',
          options: ['Madrid', 'Barcelona'],
          answers: ['Madrid'],
        }),
      );
      fixture.componentRef.setInput('editing', {
        id: 'contextual',
        type,
        contextText: 'Saved context',
        prompt: 'Choose',
        explanation: 'English feedback',
        orderNumber: 1,
        options: [{ text: 'Madrid' }, { text: 'Barcelona' }],
        answers: [{ answerText: 'Madrid' }],
      });
      await fixture.whenStable();
      expect(host.querySelector<HTMLTextAreaElement>('#exercise-context')!.value).toBe(
        'Saved context',
      );
    },
  );
  it('offers visual Russian-letter options without fabricating an audio exercise', async () => {
    const fixture = await open();
    const host: HTMLElement = fixture.nativeElement;
    const submitted = vi.fn();
    fixture.componentInstance.add.subscribe(submitted);
    fill(host, 'select', 'Alphabet');
    await fixture.whenStable();
    expect(host.textContent).toContain('Russian course');
    fill(host, '#exercise-prompt', 'Find the letter yo.');
    fill(host, '#exercise-explanation', 'Ё is the requested letter.');
    fill(host, '#exercise-options', 'Е\nЁ');
    fill(host, '#exercise-answers', 'Ё');
    host.querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(submitted).toHaveBeenCalledWith(
      expect.objectContaining({
        type: 'Alphabet',
        options: ['Е', 'Ё'],
        answers: ['Ё'],
        contextText: null,
      }),
    );
  });
  it('requires a blank marker and restores an edited token order from its IDs', async () => {
    const fixture = await open();
    const host: HTMLElement = fixture.nativeElement;
    const submitted = vi.fn();
    fixture.componentInstance.add.subscribe(submitted);
    fill(host, 'select', 'FillBlank');
    await fixture.whenStable();
    fill(host, '#exercise-prompt', 'No placeholder');
    fill(host, '#exercise-explanation', 'English feedback');
    fill(host, '#exercise-answers', 'bebo');
    host.querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(submitted).not.toHaveBeenCalled();
    expect(host.textContent).toContain('one ____ placeholder');
    fixture.componentRef.setInput('editing', {
      id: 'exercise',
      type: 'SentenceBuilder',
      prompt: 'Arrange',
      explanation: 'English',
      orderNumber: 1,
      options: [
        { id: 'a', text: 'café' },
        { id: 'b', text: 'Quiero' },
      ],
      answers: [{ answerText: 'Quiero café', orderedTokenIds: '["b","a"]' }],
    });
    await fixture.whenStable();
    expect(host.querySelector<HTMLTextAreaElement>('#exercise-token-orders')!.value).toBe('2 1');
  });
  it('restores reviewed audio and emits a listening draft with optional choices', async () => {
    const fixture = await open();
    fixture.componentRef.setInput('editing', {
      id: 'exercise',
      type: 'Listening',
      prompt: 'Listen and choose',
      explanation: 'A greeting',
      orderNumber: 1,
      audioMediaId: 'reviewed-audio',
      options: [{ text: 'Hola' }, { text: 'Adiós' }],
      answers: [{ answerText: 'Hola' }],
    });
    await fixture.whenStable();
    const submitted = vi.fn();
    fixture.componentInstance.add.subscribe(submitted);
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    expect(submitted).toHaveBeenCalledWith(
      expect.objectContaining({
        type: 'Listening',
        audioMediaId: 'reviewed-audio',
        options: ['Hola', 'Adiós'],
      }),
    );
  });
  it('authors speaking with reviewed audio and no fabricated answer key', async () => {
    const fixture = await open();
    fixture.componentRef.setInput('editing', {
      id: 'exercise',
      type: 'Speaking',
      prompt: 'Say Hola',
      explanation: 'Compare your voice with the model.',
      orderNumber: 1,
      audioMediaId: 'reviewed-audio',
      options: [],
      answers: [],
    });
    await fixture.whenStable();
    const submitted = vi.fn();
    fixture.componentInstance.add.subscribe(submitted);
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    expect(submitted).toHaveBeenCalledWith(
      expect.objectContaining({
        type: 'Speaking',
        audioMediaId: 'reviewed-audio',
        options: [],
        answers: [],
      }),
    );
    expect(fixture.nativeElement.querySelector('#exercise-answers')).toBeNull();
  });
  it('restores matching clip IDs against their correct private pair rows', async () => {
    const fixture = await open();
    fixture.componentRef.setInput('editing', {
      id: 'matching',
      type: 'Matching',
      prompt: 'Listen and connect',
      explanation: 'Match meaning',
      orderNumber: 1,
      options: [
        {
          id: 'l1',
          text: 'Clip one',
          orderNumber: 1,
          matchKey: 'first',
          audioMediaId: 'audio-one',
        },
        {
          id: 'l2',
          text: 'Clip two',
          orderNumber: 2,
          matchKey: 'second',
          audioMediaId: 'audio-two',
        },
        { id: 'r2', text: 'Goodbye', orderNumber: 3, matchKey: 'second' },
        { id: 'r1', text: 'Hello', orderNumber: 4, matchKey: 'first' },
      ],
      answers: [],
    });
    await fixture.whenStable();
    const submitted = vi.fn();
    fixture.componentInstance.add.subscribe(submitted);
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    expect(submitted).toHaveBeenCalledWith(
      expect.objectContaining({
        matchPairs: [
          { left: 'Clip one', right: 'Hello', leftAudioMediaId: 'audio-one' },
          { left: 'Clip two', right: 'Goodbye', leftAudioMediaId: 'audio-two' },
        ],
        audioMediaId: null,
      }),
    );
  });
  it('restores Russian sound mode and explicitly detaches audio when switched to visual mode', async () => {
    const fixture = await open();
    fixture.componentRef.setInput('editing', {
      id: 'alphabet',
      type: 'Alphabet',
      prompt: 'Listen and choose',
      explanation: 'Ё and Е differ',
      orderNumber: 1,
      audioMediaId: 'reviewed-russian-sound',
      options: [{ text: 'Ё' }, { text: 'Е' }],
      answers: [{ answerText: 'Ё' }],
    });
    await fixture.whenStable();
    const submitted = vi.fn();
    fixture.componentInstance.add.subscribe(submitted);
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    expect(submitted).toHaveBeenLastCalledWith(
      expect.objectContaining({ audioMediaId: 'reviewed-russian-sound' }),
    );
    const sound = fixture.nativeElement.querySelector(
      'input[formControlName=alphabetSound]',
    ) as HTMLInputElement;
    sound.checked = false;
    sound.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    expect(submitted).toHaveBeenLastCalledWith(expect.objectContaining({ audioMediaId: null }));
  });
});
