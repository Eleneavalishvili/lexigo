import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { ContentApi } from '../../core/content/content-api';
import { LessonImport } from './lesson-import';

describe('Saved CSV import reports', () => {
  const user = signal({ id: 'author', roles: ['ContentAuthor'] });
  const saved = {
    id: 'owned-job',
    status: 'Valid',
    exerciseCount: 1,
    errors: [],
    title: 'A saved greeting lesson',
    lessonId: null as string | null,
  };
  const get = vi.fn();
  const post = vi.fn();

  beforeEach(() => {
    user.set({ id: 'author', roles: ['ContentAuthor'] });
    get.mockReset().mockReturnValue(of(saved));
    post.mockReset().mockReturnValue(of({ id: 'created-lesson' }));
    TestBed.configureTestingModule({
      imports: [LessonImport],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user } },
        { provide: HttpClient, useValue: { get, post } },
        { provide: ContentApi, useValue: { courses: vi.fn().mockResolvedValue([]) } },
      ],
    });
    const route = TestBed.inject(ActivatedRoute);
    vi.spyOn(route.snapshot, 'queryParamMap', 'get').mockReturnValue(
      convertToParamMap({ job: 'owned-job' }),
    );
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  it('restores the saved valid report and commits its existing job without uploading again', async () => {
    const fixture = TestBed.createComponent(LessonImport);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(get).toHaveBeenCalledWith('/api/v1/manage/imports/owned-job');
    expect(host.textContent).toContain(saved.title);
    expect(host.textContent).toContain('Saved validation report restored.');
    expect(host.querySelector('input[type=file]')).toBeNull();
    Array.from(host.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Create draft'))!
      .click();
    await fixture.whenStable();
    expect(post).toHaveBeenCalledWith('/api/v1/manage/imports/owned-job/commit', null);
    expect(host.textContent).toContain('Draft created.');
  });

  it('restores a committed job without offering to create a second draft', async () => {
    get.mockReturnValue(of({ ...saved, status: 'Committed', lessonId: 'created-lesson' }));
    const fixture = TestBed.createComponent(LessonImport);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Draft created.');
    expect(fixture.nativeElement.textContent).not.toContain('Create draft');
    expect(post).not.toHaveBeenCalled();
  });

  it('retries a failed report read without exposing an unverified saved report', async () => {
    get.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 503 })));
    const fixture = TestBed.createComponent(LessonImport);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).not.toContain(saved.title);
    expect(host.querySelector('[role=alert]')).not.toBeNull();
    Array.from(host.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Retry loading import'))!
      .click();
    await fixture.whenStable();
    expect(get).toHaveBeenCalledTimes(2);
    expect(host.textContent).toContain(saved.title);
  });

  it('ignores a report response after the account changes', async () => {
    const response = new Subject<typeof saved>();
    get.mockReturnValue(response);
    const fixture = TestBed.createComponent(LessonImport);
    await Promise.resolve();
    user.set({ id: 'another-author', roles: ['ContentAuthor'] });
    response.next(saved);
    response.complete();
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).not.toContain(saved.title);
    expect(fixture.nativeElement.textContent).not.toContain('Import a lesson draft');
    expect(post).not.toHaveBeenCalled();
  });
});
