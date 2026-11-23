import { Component, input } from '@angular/core';
import { LearningOverview } from './learning-overview';

@Component({
  selector: 'app-learning-shell',
  imports: [LearningOverview],
  template: `<aside><app-learning-overview [courseId]="courseId()" /></aside>
    <div class="content"><ng-content /></div>`,
  styles: `
    :host {
      display: grid;
      grid-template-columns: 220px minmax(0, 1fr);
      align-items: start;
      gap: 2rem;
    }
    aside {
      position: sticky;
      top: 1.5rem;
      margin-top: 4rem;
    }
    .content {
      min-width: 0;
    }
    @media (max-width: 900px) {
      :host {
        display: block;
      }
      aside {
        position: static;
        margin-top: 1.5rem;
      }
    }
  `,
})
export class LearningShell {
  readonly courseId = input<string | null>(null);
}
