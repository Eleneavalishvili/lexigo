import { Component } from '@angular/core';

@Component({
  selector: 'app-lesson-heart-loss',
  host: { role: 'status' },
  template: '−1 heart',
  styles: `
    :host {
      position: absolute;
      inset-inline-end: 0;
      top: 100%;
      z-index: 1;
      padding: 0.25rem 0.65rem;
      border-radius: 0.5rem;
      border: 1px solid var(--red);
      color: var(--red);
      background: var(--surface);
      font-weight: 700;
      pointer-events: none;
      animation: heart-pop 240ms ease-out;
    }
    @keyframes heart-pop {
      from {
        opacity: 0;
        transform: translateY(0.35rem) scale(0.88);
      }
      to {
        opacity: 1;
        transform: none;
      }
    }
    @media (prefers-reduced-motion: reduce) {
      :host {
        animation: none;
      }
    }
  `,
})
export class LessonHeartLoss {}
