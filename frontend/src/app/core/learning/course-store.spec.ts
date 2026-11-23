import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthStore } from '../accounts/auth-store';
import { CourseStore } from './course-store';
import { CourseProgress, LearningApi } from './learning-api';

describe('Course map state', () => {
  const user = signal<{ id: string } | null>({ id: 'learner' });
  const api = { progress: vi.fn(), enrollments: vi.fn() };
  const router = { navigate: vi.fn() };
  const progress = (courseId: string): CourseProgress => ({
    courseId,
    title: courseId,
    isEnrolled: true,
    units: [],
    lessons: [],
    openSessions: [],
  });

  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'learner' });
    api.progress.mockImplementation((id: string) => Promise.resolve(progress(id)));
    TestBed.configureTestingModule({
      providers: [
        CourseStore,
        { provide: AuthStore, useValue: { user } },
        { provide: LearningApi, useValue: api },
        { provide: Router, useValue: router },
      ],
    });
  });

  it('keeps a late language response from replacing the current course', async () => {
    let finishSpanish!: (value: CourseProgress) => void;
    api.progress.mockImplementation((id: string) =>
      id === 'Spanish'
        ? new Promise<CourseProgress>((resolve) => {
            finishSpanish = resolve;
          })
        : Promise.resolve(progress(id)),
    );
    const store = TestBed.inject(CourseStore);
    const first = store.selectCourse('Spanish');
    store.selectedLevel.set('A2');
    await store.selectCourse('Italian');
    finishSpanish(progress('Spanish'));
    await first;
    expect(store.progress()?.courseId).toBe('Italian');
    expect(store.selectedLevel()).toBe('');
    expect(store.loading()).toBe(false);
  });

  it('clears the previous account map and ignores an in-flight response after sign-out', async () => {
    const store = TestBed.inject(CourseStore);
    await store.selectCourse('Spanish');
    await TestBed.tick();
    let finish!: (value: CourseProgress) => void;
    api.progress.mockReturnValue(
      new Promise<CourseProgress>((resolve) => {
        finish = resolve;
      }),
    );
    const refresh = store.load();
    user.set(null);
    await TestBed.tick();
    expect(store.progress()).toBeNull();
    finish(progress('Spanish'));
    await refresh;
    expect(store.progress()).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });
});
