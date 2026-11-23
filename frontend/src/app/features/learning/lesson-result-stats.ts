import { Component, input } from '@angular/core';
import { SessionView } from '../../core/learning/learning-api';

@Component({
  selector: 'app-lesson-result-stats',
  template: `
    <dl class="result-grid">
      <div>
        <dt>Accuracy</dt>
        <dd>
          {{
            (session().gradedAnsweredCount ?? session().answeredCount) > 0
              ? session().accuracy + '%'
              : '—'
          }}
        </dd>
      </div>
      <div>
        <dt>{{ gradedTotal() > 0 ? 'Correct' : 'Practice' }}</dt>
        <dd>
          {{
            gradedTotal() > 0 ? session().correctCount + ' / ' + gradedTotal() : 'Ungraded practice'
          }}
        </dd>
      </div>
      <div [class.xp-earned]="session().awardedXp > 0">
        <dt>Lesson XP</dt>
        <dd>+{{ session().awardedXp }}</dd>
      </div>
      <div>
        <dt>Time</dt>
        <dd>{{ elapsed() }}</dd>
      </div>
    </dl>
  `,
  styles: `
    .result-grid {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: 1rem;
    }
    .result-grid > div {
      padding: 1rem;
      border: 1px solid var(--line);
      border-radius: 0.8rem;
      background: var(--paper);
    }
    dt {
      font-size: 0.8rem;
    }
    dd {
      font-family: var(--display-font);
      font-size: 1.8rem;
      margin: 0.5rem 0;
      overflow-wrap: anywhere;
    }
    .xp-earned dd {
      transform-origin: left center;
      animation: xp-pop 420ms ease-out;
    }
    @keyframes xp-pop {
      0% {
        opacity: 0;
        transform: translateY(0.4rem) scale(0.8);
      }
      60% {
        transform: scale(1.18);
      }
      100% {
        opacity: 1;
        transform: none;
      }
    }
    @media (prefers-reduced-motion: reduce) {
      .xp-earned dd {
        animation: none;
      }
    }
    @media (max-width: 600px) {
      .result-grid {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
    }
  `,
})
export class LessonResultStats {
  readonly session = input.required<SessionView>();
  readonly gradedTotal = input.required<number>();
  readonly elapsed = input.required<string>();
}
