import { Component } from '@angular/core';

@Component({
  selector: 'app-lesson-celebration',
  host: { 'aria-hidden': 'true' },
  template: `
    @for (piece of pieces; track piece) {
      <i [style.--piece]="piece"></i>
    }
  `,
  styles: `
    :host {
      display: block;
      position: absolute;
      inset: 0;
      overflow: hidden;
      pointer-events: none;
    }
    i {
      position: absolute;
      top: -1rem;
      left: calc(var(--piece) * 4.2%);
      width: 8px;
      height: 14px;
      background: var(--gold);
      animation: confetti-fall 1.8s ease-out both;
      animation-delay: calc(var(--piece) * 25ms);
    }
    i:nth-child(3n) {
      background: var(--red);
      border-radius: 50%;
    }
    i:nth-child(3n + 1) {
      background: var(--sage);
    }
    @keyframes confetti-fall {
      from {
        transform: translateY(-2rem) rotate(0);
        opacity: 1;
      }
      to {
        transform: translateY(30rem) rotate(440deg);
        opacity: 0;
      }
    }
    @media (prefers-reduced-motion: reduce) {
      :host {
        display: none;
      }
    }
    :host-context([data-reduce-motion='true']) {
      display: none;
    }
  `,
})
export class LessonCelebration {
  protected readonly pieces = Array.from({ length: 24 }, (_, index) => index);
}
