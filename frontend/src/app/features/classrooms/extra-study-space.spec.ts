import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { ExtraStudyApi } from '../../core/classrooms/extra-study-api';
import { CatalogApi } from '../../core/content/catalog-api';
import { LearningApi } from '../../core/learning/learning-api';
import { ExtraStudySpace } from './extra-study-space';

describe('Private extra-study space', () => {
  const user = signal<{ id: string } | null>({ id: 'learner' });
  const api = { space: vi.fn(), share: vi.fn() };
  const catalog = { units: vi.fn(), lessons: vi.fn() };
  const learning = { start: vi.fn() };
  const lesson = { id: 'lesson', title: 'Greetings' };
  const space = { courseId: 'course', courseTitle: 'Spanish', canShare: false, lessons: [lesson] };
  const denied = new HttpErrorResponse({ status: 404 });

  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'learner' });
    api.space.mockResolvedValue(space);
    api.share.mockResolvedValue({});
    catalog.units.mockResolvedValue([{ id: 'unit', title: 'Unit one' }]);
    catalog.lessons.mockResolvedValue([lesson]);
    learning.start.mockResolvedValue({ id: 'session' });
    const params = convertToParamMap({ requestId: 'request' });
    TestBed.configureTestingModule({
      imports: [ExtraStudySpace],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: params }, paramMap: of(params) },
        },
        { provide: AuthStore, useValue: { user } },
        { provide: ExtraStudyApi, useValue: api },
        { provide: CatalogApi, useValue: catalog },
        { provide: LearningApi, useValue: learning },
      ],
    });
  });

  it('starts a shared lesson through the regular learner session flow', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(ExtraStudySpace);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('#share-title')).toBeNull();
    host.querySelector<HTMLButtonElement>('.class-list button')!.click();
    await fixture.whenStable();
    expect(learning.start).toHaveBeenCalledWith('lesson');
    expect(navigate).toHaveBeenCalledWith(['/learn/session', 'session']);
  });

  it('lets the teacher share a lesson, then hides it if access is revoked', async () => {
    const teacherSpace = { ...space, canShare: true };
    api.space
      .mockResolvedValueOnce({ ...teacherSpace, lessons: [] })
      .mockResolvedValueOnce(teacherSpace)
      .mockRejectedValueOnce(denied);
    const fixture = TestBed.createComponent(ExtraStudySpace);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const unit = host.querySelector<HTMLSelectElement>('#study-unit')!;
    unit.value = 'unit';
    unit.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    host.querySelector<HTMLSelectElement>('#study-lesson')!.value = 'lesson';
    host.querySelector<HTMLButtonElement>('section:last-child button')!.click();
    await fixture.whenStable();
    expect(api.share).toHaveBeenCalledWith('request', 'lesson');
    expect(host.querySelector('.class-list')?.textContent).toContain('Greetings');
    host.querySelector<HTMLButtonElement>('button.text-button')!.click();
    await fixture.whenStable();
    expect(host.querySelector('.class-list')).toBeNull();
    expect(host.querySelector('[role=alert]')?.textContent).toContain('unavailable');
  });
});
