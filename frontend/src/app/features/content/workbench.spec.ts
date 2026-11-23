import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { ContentApi } from '../../core/content/content-api';
import { ContentWorkbench } from './workbench';
@Component({ template: '' })
class AccountDestination {}

describe('Lesson workshop', () => {
  const user = signal<{ id: string; roles: string[] } | null>(null);
  const course = {
    id: 'course',
    title: 'Spanish',
    targetLanguageId: 2,
    canAuthor: true,
    canReview: false,
  };
  const lesson = {
    id: 'lesson',
    unitId: 'unit',
    ownerId: 'author',
    currentVersionId: null,
    versionCreatedBy: 'author',
    versionId: 'version',
    versionNumber: 1,
    versionStatus: 'Draft',
    title: 'First hello',
    objective: 'Recognize greetings',
    explanation: 'English grammar',
    reviews: [],
    exercises: [
      {
        id: 'exercise',
        type: 'MultipleChoice',
        prompt: 'A private draft prompt',
        explanation: 'English feedback',
        orderNumber: 1,
        options: [{ text: 'Hola' }, { text: 'Adiós' }],
        answers: [{ answerText: 'Hola' }],
      },
    ],
  };
  const api = {
    courses: vi.fn(),
    units: vi.fn(),
    lessons: vi.fn(),
    lesson: vi.fn(),
    submit: vi.fn(),
    publish: vi.fn(),
    review: vi.fn(),
    revision: vi.fn(),
    versions: vi.fn(),
    erasePrivateDraft: vi.fn(),
  };
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'author', roles: ['Learner', 'ContentAuthor'] });
    api.courses.mockResolvedValue([course]);
    api.units.mockResolvedValue([
      { id: 'unit', title: 'Greetings', orderNumber: 1, nextLessonOrder: 2 },
    ]);
    api.lessons.mockResolvedValue({ total: 1, items: [lesson] });
    api.lesson.mockResolvedValue(lesson);
    api.submit.mockResolvedValue({});
    api.review.mockResolvedValue({});
    api.versions.mockResolvedValue([
      {
        id: 'version',
        versionNumber: 1,
        status: 'Draft',
        publishedAt: null,
        privateTextErasedAt: null,
      },
    ]);
    api.erasePrivateDraft.mockResolvedValue({});
    TestBed.configureTestingModule({
      imports: [ContentWorkbench],
      providers: [
        provideRouter([{ path: 'account', component: AccountDestination }]),
        { provide: AuthStore, useValue: { user } },
        { provide: ContentApi, useValue: api },
      ],
    });
  });
  async function open() {
    const fixture = TestBed.createComponent(ContentWorkbench);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    host.querySelector<HTMLButtonElement>('.course-tabs button')!.click();
    await fixture.whenStable();
    host.querySelector<HTMLButtonElement>('.lesson-list button')!.click();
    await fixture.whenStable();
    return { fixture, host };
  }
  it('submits an explicit current version and cannot approve or publish its own draft', async () => {
    const { fixture, host } = await open();
    const button = Array.from(host.querySelectorAll<HTMLButtonElement>('button')).find((item) =>
      item.textContent?.includes('Submit for independent review'),
    )!;
    expect(host.textContent).not.toContain('Approve version');
    expect(host.textContent).not.toContain('Publish reviewed version');
    button.click();
    await fixture.whenStable();
    expect(api.submit).toHaveBeenCalledWith('lesson', 'version');
  });
  it('opens version history and requires confirmation before erasing private text', async () => {
    const { fixture, host } = await open();
    const button = (label: string) =>
      [...host.querySelectorAll<HTMLButtonElement>('button')].find((item) =>
        item.textContent?.includes(label),
      )!;
    button('View version history').click();
    await fixture.whenStable();
    expect(api.versions).toHaveBeenCalledWith('lesson', 1);
    host
      .querySelector<HTMLButtonElement>('nav[aria-label="Lesson version history"] button')!
      .click();
    await fixture.whenStable();
    expect(api.lesson).toHaveBeenLastCalledWith('lesson', 'version');
    button('Erase private draft text').click();
    await fixture.whenStable();
    expect(api.erasePrivateDraft).not.toHaveBeenCalled();
    api.lesson.mockResolvedValue({
      ...lesson,
      versionStatus: 'Archived',
      privateTextErasedAt: '2026-11-06T00:00:00Z',
      exercises: [],
    });
    button('Confirm private text erasure').click();
    await fixture.whenStable();
    expect(api.erasePrivateDraft).toHaveBeenCalledExactlyOnceWith('lesson', 'version');
    expect(host.textContent).toContain("This private draft's text has been erased.");
    expect(host.textContent).not.toContain('Start a separate revision');
  });
  it('requires reviewer comments and separates approval from publication', async () => {
    user.set({ id: 'reviewer', roles: ['Reviewer'] });
    api.courses.mockResolvedValue([{ ...course, canAuthor: false, canReview: true }]);
    api.lesson.mockResolvedValue({ ...lesson, versionStatus: 'Submitted' });
    const { fixture, host } = await open();
    const approve = Array.from(host.querySelectorAll<HTMLButtonElement>('button')).find((item) =>
      item.textContent?.includes('Approve version'),
    )!;
    approve.click();
    await fixture.whenStable();
    expect(api.review).not.toHaveBeenCalled();
    const input = host.querySelector<HTMLTextAreaElement>('#review-comment')!;
    input.value = 'Checked the grammar and answers';
    input.dispatchEvent(new Event('input'));
    approve.click();
    await fixture.whenStable();
    expect(api.review).toHaveBeenCalledWith(
      'lesson',
      'version',
      'Approved',
      'Checked the grammar and answers',
    );
    expect(api.publish).not.toHaveBeenCalled();
  });
  it('clears private drafts and answer keys when the account changes', async () => {
    const { fixture, host } = await open();
    expect(host.textContent).toContain('Staff answer key');
    user.set(null);
    await fixture.whenStable();
    expect(host.textContent).not.toContain('Staff answer key');
    expect(host.querySelector('#edit-explanation')).toBeNull();
  });
});
