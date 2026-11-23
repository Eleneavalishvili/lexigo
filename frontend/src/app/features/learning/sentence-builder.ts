import { Component, computed, input, model, output } from '@angular/core';
import { LearningOption } from '../../core/learning/learning-api';

@Component({
  selector: 'app-sentence-builder',
  templateUrl: './sentence-builder.html',
  styleUrl: './sentence-builder.scss',
})
export class SentenceBuilder {
  readonly options = input.required<readonly LearningOption[]>();
  readonly disabled = input(false);
  readonly spoken = output<string>();
  readonly order = model<readonly string[]>([]);
  protected readonly selected = computed(() =>
    this.order()
      .map((id) => this.options().find((option) => option.id === id))
      .filter((option): option is LearningOption => !!option),
  );
  protected readonly sentence = computed(() =>
    this.selected()
      .map((option) => option.text)
      .join(' '),
  );

  protected add(id: string): void {
    if (
      this.disabled() ||
      this.order().includes(id) ||
      !this.options().some((option) => option.id === id)
    )
      return;
    this.order.update((order) => [...order, id]);
    this.spoken.emit(this.options().find((option) => option.id === id)!.text);
  }
  protected remove(id: string): void {
    if (!this.disabled()) this.order.update((order) => order.filter((item) => item !== id));
  }
  protected move(index: number, direction: number): void {
    const target = index + direction;
    if (this.disabled() || target < 0 || target >= this.order().length) return;
    const order = [...this.order()];
    [order[index], order[target]] = [order[target], order[index]];
    this.order.set(order);
  }
  protected clear(): void {
    if (!this.disabled()) this.order.set([]);
  }
}
