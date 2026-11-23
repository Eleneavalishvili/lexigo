import { Component, effect, input, model, signal } from '@angular/core';
import { ReviewedAudioPicker } from './reviewed-audio-picker';

@Component({
  selector: 'app-matching-audio-editor',
  imports: [ReviewedAudioPicker],
  templateUrl: './matching-audio-editor.html',
  styleUrl: './matching-audio-editor.scss',
})
export class MatchingAudioEditor {
  readonly labels = input.required<readonly string[]>();
  readonly languageId = input.required<number>();
  readonly disabled = input(false);
  readonly ids = model<readonly (string | null)[]>([]);
  protected readonly active = signal<number | null>(null);

  constructor() {
    effect(() => {
      const count = this.labels().length;
      if (this.ids().length > count) this.ids.set(this.ids().slice(0, count));
      const active = this.active();
      if (active !== null && active >= count) this.active.set(null);
    });
  }

  protected choose(index: number, id: string | null): void {
    if (this.disabled() || index < 0 || index >= this.labels().length) return;
    this.ids.set(
      this.labels().map((_, position) =>
        position === index ? id : (this.ids()[position] ?? null),
      ),
    );
    this.active.set(null);
  }
}
