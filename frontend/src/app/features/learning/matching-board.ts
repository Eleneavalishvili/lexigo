import { Component, QueryList, ViewChildren, computed, input, model } from '@angular/core';
import { LessonAudio } from './lesson-audio';
import { LearningOption, MatchedPair } from '../../core/learning/learning-api';

@Component({
  selector: 'app-matching-board',
  imports: [LessonAudio],
  templateUrl: './matching-board.html',
  styleUrl: './matching-board.scss',
})
export class MatchingBoard {
  readonly sessionId = input('');
  readonly exerciseId = input('');
  @ViewChildren(LessonAudio) private players?: QueryList<LessonAudio>;
  readonly options = input.required<readonly LearningOption[]>();
  readonly disabled = input(false);
  readonly reviewOnly = input(false);
  readonly pairs = model<readonly MatchedPair[]>([]);
  protected readonly left = computed(() =>
    this.options().filter((option) => option.side === 'left'),
  );
  protected readonly right = computed(() =>
    this.options().filter((option) => option.side === 'right'),
  );
  protected focus(optionId: string): void {
    this.players?.forEach((player) => {
      if (player.optionId() !== optionId) player.stop();
    });
  }
  protected selected(id: string): string {
    return this.pairs().find((pair) => pair.leftOptionId === id)?.rightOptionId ?? '';
  }
  protected usedByAnother(leftId: string, rightId: string): boolean {
    return this.pairs().some(
      (pair) => pair.leftOptionId !== leftId && pair.rightOptionId === rightId,
    );
  }
  protected choose(leftId: string, event: Event): void {
    const rightId = (event.target as HTMLSelectElement).value;
    if (
      this.disabled() ||
      !this.left().some((option) => option.id === leftId) ||
      (rightId &&
        (!this.right().some((option) => option.id === rightId) ||
          this.usedByAnother(leftId, rightId)))
    )
      return;
    this.pairs.update((pairs) => [
      ...pairs.filter((pair) => pair.leftOptionId !== leftId),
      ...(rightId ? [{ leftOptionId: leftId, rightOptionId: rightId }] : []),
    ]);
  }
  protected clear(): void {
    if (!this.disabled()) this.pairs.set([]);
  }
}
