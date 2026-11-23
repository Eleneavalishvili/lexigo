import { Component, effect, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatchingAudioEditor } from './matching-audio-editor';
import { ReviewedAudioPicker } from './reviewed-audio-picker';
import { ExerciseDraft, StaffExercise, StaffSkill } from '../../core/content/content-api';
@Component({
  selector: 'app-exercise-composer',
  imports: [ReactiveFormsModule, ReviewedAudioPicker, MatchingAudioEditor],
  templateUrl: './exercise-composer.html',
  styleUrl: './workbench-fields.scss',
})
export class ExerciseComposer {
  readonly languageId = input(0);
  readonly nextOrder = input.required<number>();
  readonly busy = input(false);
  readonly editing = input<StaffExercise | null>(null);
  readonly skills = input<readonly StaffSkill[]>([]);
  readonly add = output<ExerciseDraft>();
  protected readonly error = signal('');
  protected readonly skillIds = signal<readonly number[]>([]);
  protected readonly matchingAudioIds = signal<readonly (string | null)[]>([]);
  protected toggleSkill(id: number, selected: boolean): void {
    this.skillIds.update((ids) => (selected ? [...ids, id] : ids.filter((item) => item !== id)));
  }
  protected readonly form = inject(FormBuilder).nonNullable.group({
    type: ['MultipleChoice'],
    audioMediaId: [''],
    alphabetSound: [false],
    ignoreCase: [true],
    ignoreTerminalPunctuation: [false],
    prompt: ['', [Validators.required, Validators.maxLength(5000)]],
    contextText: ['', Validators.maxLength(4000)],
    explanation: ['', [Validators.required, Validators.maxLength(5000)]],
    orderNumber: [1, [Validators.required, Validators.min(1)]],
    options: [''],
    answers: [''],
    tokenOrders: [''],
    matchingLeft: ['', Validators.maxLength(4000)],
    matchingRight: ['', Validators.maxLength(4000)],
  });
  constructor() {
    effect(() => {
      const editing = this.editing();
      this.skillIds.set(editing?.skillIds ?? []);
      let rules: { ignoreCase?: boolean; ignoreTerminalPunctuation?: boolean } = {};
      try {
        rules = JSON.parse(editing?.evaluationRules ?? '{}');
      } catch {
        /* Invalid stored rules are rejected by the publication check. */
      }
      this.error.set('');
      const tokenOrders =
        editing?.type === 'SentenceBuilder'
          ? editing.answers
              .map((answer) => {
                try {
                  const ids: unknown = JSON.parse(answer.orderedTokenIds ?? '[]');
                  return Array.isArray(ids)
                    ? ids
                        .map((id) => editing.options.findIndex((option) => option.id === id) + 1)
                        .join(' ')
                    : '';
                } catch {
                  return '';
                }
              })
              .join('\n')
          : '';
      const matching =
        editing?.type === 'Matching'
          ? [...editing.options].sort((a, b) => (a.orderNumber ?? 0) - (b.orderNumber ?? 0))
          : [];
      const left = matching.slice(0, matching.length / 2);
      const right = matching.slice(matching.length / 2);
      this.matchingAudioIds.set(left.map((item) => item.audioMediaId ?? null));
      this.form.reset({
        matchingLeft: left.map((item) => item.text).join('\n'),
        matchingRight: left
          .map(
            (item) =>
              right.find((target) => target.matchKey && target.matchKey === item.matchKey)?.text ??
              '',
          )
          .join('\n'),
        alphabetSound: editing?.type === 'Alphabet' && !!editing.audioMediaId,
        audioMediaId: editing?.audioMediaId ?? '',
        tokenOrders,
        ignoreCase: rules.ignoreCase ?? true,
        ignoreTerminalPunctuation: rules.ignoreTerminalPunctuation ?? false,
        type: editing?.type ?? 'MultipleChoice',
        prompt: editing?.prompt ?? '',
        contextText: editing?.contextText ?? '',
        explanation: editing?.explanation ?? '',
        orderNumber: editing?.orderNumber ?? this.nextOrder(),
        options: editing?.options.map((item) => item.text).join('\n') ?? '',
        answers: editing?.answers.map((item) => item.answerText).join('\n') ?? '',
      });
    });
  }
  protected matchingLabels(): string[] {
    return this.form.controls.matchingLeft.value
      .split(/\r?\n/)
      .map((value) => value.trim())
      .filter(Boolean)
      .slice(0, 10);
  }
  protected submit(): void {
    this.error.set('');
    this.form.markAllAsTouched();
    if (this.form.invalid || this.busy()) return;
    const value = this.form.getRawValue();
    const lines = (text: string) =>
      text
        .split(/\r?\n/)
        .map((item) => item.trim())
        .filter(Boolean);
    if (
      (['Listening', 'Speaking', 'SpeakingCheck'].includes(value.type) ||
        (value.type === 'Alphabet' && value.alphabetSound)) &&
      !value.audioMediaId
    ) {
      this.error.set('Choose an independently reviewed recording.');
      return;
    }
    const contextual = ['Reading', 'DialogueChoice'].includes(value.type);
    if (contextual && !value.contextText.trim()) {
      this.error.set('Add a reading passage or dialogue situation.');
      return;
    }
    const options = lines(value.options);
    const answers = lines(value.answers);
    const orderLines = lines(value.tokenOrders);
    const tokenOrders = orderLines.map((line) => line.split(/\s+/).map(Number));
    const left = lines(value.matchingLeft);
    const right = lines(value.matchingRight);
    if (value.type === 'Matching') {
      if (
        left.length < 2 ||
        left.length > 10 ||
        left.length !== right.length ||
        [...left, ...right].some((text) => text.length > 300)
      ) {
        this.error.set('Add 2–10 pairs, with one item per line and the same number on both sides.');
        return;
      }
    } else if (value.type === 'SentenceBuilder') {
      if (
        options.length < 2 ||
        options.length > 20 ||
        !orderLines.length ||
        orderLines.length > 20 ||
        orderLines.some((line) => !/^\d+(?:\s+\d+)*$/.test(line)) ||
        tokenOrders.some(
          (order) =>
            order.length < 1 ||
            order.length > options.length ||
            new Set(order).size !== order.length ||
            order.some((position) => position < 1 || position > options.length),
        )
      ) {
        this.error.set(
          'Add 2–20 word tiles. Use each selected tile number once; unused tiles are distractors.',
        );
        return;
      }
    } else if (value.type !== 'Speaking' && !answers.length) {
      this.error.set('Add at least one accepted answer.');
      return;
    }
    if (value.type === 'FillBlank' && value.prompt.split('____').length !== 2) {
      this.error.set('Use exactly one ____ placeholder in the prompt.');
      return;
    }
    this.add.emit({
      type: value.type,
      audioMediaId:
        value.type === 'Matching' || (value.type === 'Alphabet' && !value.alphabetSound)
          ? null
          : value.audioMediaId || null,
      skillIds: this.skills().length ? this.skillIds() : undefined,
      prompt: value.prompt,
      contextText: contextual ? value.contextText.trim() : null,
      explanation: value.explanation,
      orderNumber: value.orderNumber,
      ignoreCase: value.ignoreCase,
      ignoreTerminalPunctuation: value.ignoreTerminalPunctuation,
      options: [
        'MultipleChoice',
        'SentenceBuilder',
        'Reading',
        'DialogueChoice',
        'Alphabet',
        'Listening',
      ].includes(value.type)
        ? options
        : [],
      answers: ['SentenceBuilder', 'Matching', 'Speaking'].includes(value.type) ? [] : answers,
      tokenOrders: value.type === 'SentenceBuilder' ? tokenOrders : [],
      matchPairs:
        value.type === 'Matching'
          ? left.map((text, index) => ({
              left: text,
              right: right[index],
              leftAudioMediaId: this.matchingAudioIds()[index] ?? null,
            }))
          : [],
    });
  }
}
