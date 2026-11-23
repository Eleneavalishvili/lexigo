import { Component, inject, input } from '@angular/core';
import { courseLanguage, Pronunciation } from '../../core/learning/pronunciation';

@Component({
  selector: 'app-pronunciation-button',
  template: `<button
      type="button"
      class="listen"
      (click)="listen()"
      [attr.aria-label]="'Listen: ' + text()"
      title="Locally generated pronunciation"
    >
      ♪ Listen
    </button>
    <button
      type="button"
      class="listen"
      (click)="listen(true)"
      [attr.aria-label]="'Listen slowly: ' + text()"
      title="Slower playback, same voice pitch"
    >
      ♪ Slowly
    </button>`,
  styles: `
    .listen {
      display: inline-flex;
      gap: 0.4rem;
      margin: 0.6rem 0;
      padding: 0.6rem 1rem;
      border-radius: 0.8rem;
      border: 1px solid var(--ink);
      background: var(--surface);
      color: var(--ink);
      cursor: pointer;
      font: inherit;
    }
  `,
})
export class PronunciationButton {
  readonly text = input.required<string>();
  readonly courseId = input.required<string>();
  readonly language = input<string>();
  protected readonly audio = inject(Pronunciation);
  protected listen(slow = false): void {
    void this.audio.play(
      this.text(),
      this.language() ?? courseLanguage(this.courseId()),
      false,
      slow,
    );
  }
}
