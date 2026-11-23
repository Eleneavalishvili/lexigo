import { Component, computed, input, model, signal } from '@angular/core';

@Component({
  selector: 'app-answer-letters',
  template: `
    @if (letters().length) {
      <details>
        <summary>Need a letter? Open the language keyboard</summary>
        <div class="letters" role="group" aria-label="Letters for your answer">
          <button
            type="button"
            [disabled]="disabled()"
            [attr.aria-pressed]="uppercase()"
            (click)="uppercase.set(!uppercase())"
          >
            Shift
          </button>
          @for (letter of letters(); track letter) {
            <button
              type="button"
              [disabled]="disabled()"
              [attr.aria-label]="'Insert ' + display(letter)"
              (click)="insert(letter)"
            >
              {{ display(letter) }}
            </button>
          }
        </div>
      </details>
    }
  `,
  styles: `
    details {
      margin-block: 0.7rem;
    }
    summary {
      cursor: pointer;
      font-size: 0.9rem;
    }
    .letters {
      display: flex;
      flex-wrap: wrap;
      gap: 0.4rem;
      margin-block: 0.7rem;
    }
    button {
      min-width: 2.6rem;
      min-height: 2.6rem;
      border: 1px solid var(--ink);
      border-radius: 0.4rem;
      background: var(--surface);
      color: var(--ink);
      font: inherit;
      cursor: pointer;
    }
    button:focus-visible {
      outline: 3px solid var(--red);
      outline-offset: 2px;
    }
  `,
})
export class AnswerLetters {
  readonly courseId = input.required<string>();
  readonly field = input.required<HTMLTextAreaElement>();
  readonly disabled = input(false);
  readonly value = model('');
  protected readonly uppercase = signal(false);
  protected readonly letters = computed(() => {
    const course = this.courseId();
    if (course.endsWith('1')) return Array.from('áéíóúüñ¿¡');
    if (course.endsWith('2')) return Array.from('àèéìòù');
    if (course.endsWith('3')) return Array.from('äöüß');
    if (course.endsWith('4')) return Array.from('абвгдеёжзийклмнопрстуфхцчшщъыьэюя');
    return [];
  });
  protected display(letter: string): string {
    return this.uppercase() ? (letter === 'ß' ? 'ẞ' : letter.toUpperCase()) : letter;
  }
  protected insert(letter: string): void {
    if (this.disabled()) return;
    const field = this.field();
    const start = field.selectionStart;
    const end = field.selectionEnd;
    const inserted = this.display(letter);
    const next = this.value().slice(0, start) + inserted + this.value().slice(end);
    if (field.maxLength >= 0 && next.length > field.maxLength) return;
    this.value.set(next);
    field.focus();
    field.setSelectionRange(start + inserted.length, start + inserted.length);
  }
}
