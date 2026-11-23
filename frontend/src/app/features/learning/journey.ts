import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CourseStore } from '../../core/learning/course-store';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { MistakePractice } from './mistake-practice';
import { SkillMap } from './skill-map';
import { LearningShell } from './learning-shell';

@Component({
  selector: 'app-learning-journey',
  providers: [CourseStore],
  imports: [RouterLink, BrandLogo, MistakePractice, SkillMap, LearningShell],
  templateUrl: './journey.html',
  styleUrl: './journey.scss',
})
export class LearningJourney {
  protected readonly store = inject(CourseStore);

  constructor() {
    inject(ActivatedRoute)
      .paramMap.pipe(takeUntilDestroyed())
      .subscribe((params) => {
        void this.store.selectCourse(params.get('courseId') ?? '');
      });
  }

  protected showPrerequisite(event: MouseEvent, id: string): void {
    const details = document.getElementById(`lesson-${id}`);
    if (!(details instanceof HTMLDetailsElement)) return;
    event.preventDefault();
    details.open = true;
    details.querySelector('summary')?.focus();
  }
}
