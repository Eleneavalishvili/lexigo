import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { AssignmentApi } from '../../core/classrooms/assignment-api';
import { ClassroomView } from '../../core/classrooms/classroom-api';
import { CatalogApi } from '../../core/content/catalog-api';
import { LearningApi } from '../../core/learning/learning-api';
import { ClassAssignments } from './class-assignments';

const classroom: ClassroomView = {
  id: 'room',
  courseId: 'spanish',
  courseTitle: 'Spanish',
  name: 'Class',
  description: '',
  teacherName: 'Teacher',
  isArchived: false,
  revision: 1,
  isTeacher: false,
  canLeave: true,
  coursePublished: true,
};
const summary = {
  id: 'assignment',
  title: 'Greetings practice',
  dueAt: '2030-05-18T12:00:00Z',
  minimumScore: 80,
};

describe('Class assignments', () => {
  const assignments = {
    list: vi.fn(),
    detail: vi.fn(),
    create: vi.fn(),
    edit: vi.fn(),
    submit: vi.fn(),
    results: vi.fn(),
    postFeedback: vi.fn(),
  };
  const catalog = { units: vi.fn(), lessons: vi.fn() };
  const learning = { start: vi.fn() };

  beforeEach(() => {
    vi.resetAllMocks();
    assignments.list.mockResolvedValue({ items: [summary], page: 1, pageSize: 20, totalCount: 1 });
    assignments.detail.mockResolvedValue({
      ...summary,
      classroomId: 'room',
      revision: 1,
      lessons: [{ id: 'hello', title: 'Hello', isPublished: true }],
    });
    TestBed.configureTestingModule({
      imports: [ClassAssignments],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user: signal({ id: 'viewer' }) } },
        { provide: AssignmentApi, useValue: assignments },
        { provide: CatalogApi, useValue: catalog },
        { provide: LearningApi, useValue: learning },
      ],
    });
  });

  async function mount(overrides: Partial<ClassroomView> = {}) {
    const fixture = TestBed.createComponent(ClassAssignments);
    fixture.componentRef.setInput('classroom', { ...classroom, ...overrides });
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const click = (label: string) => {
      const button = [...host.querySelectorAll('button')].find(
        (item) => item.textContent?.trim() === label,
      );
      expect(button).toBeDefined();
      button!.click();
    };
    const change = (id: string, value: string, event = 'input') => {
      const control = host.querySelector<HTMLInputElement | HTMLSelectElement>('#' + id)!;
      control.value = value;
      control.dispatchEvent(new Event(event));
    };
    return { fixture, host, click, change };
  }

  it('opens a published assigned lesson through the normal learning session', async () => {
    learning.start.mockResolvedValue({ id: 'saved-session' });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const { fixture, host, click } = await mount();
    expect(assignments.list).toHaveBeenCalledWith('room', 1);
    expect(host.textContent).not.toContain('New assignment');
    click(summary.title);
    await fixture.whenStable();
    expect(host.textContent).not.toContain('View class results');
    click('Open lesson');
    await fixture.whenStable();
    expect(learning.start).toHaveBeenCalledWith('hello');
    expect(navigate).toHaveBeenCalledWith(['/learn/session', 'saved-session']);
  });

  it('loads results only when the teacher opens them and shows the recorded score and accuracy', async () => {
    assignments.results.mockResolvedValue({
      minimumScore: 80,
      requiredLessons: 1,
      learners: {
        page: 1,
        pageSize: 20,
        totalCount: 1,
        items: [
          {
            learnerId: 'student',
            displayName: 'Learner A',
            membershipStatus: 'Active',
            status: 'Completed',
            lessons: [
              {
                lessonId: 'hello',
                title: 'Hello',
                attemptCount: 2,
                bestScore: 100,
                latestScore: 85,
                latestAccuracy: 90,
                latestSubmissionId: 'submission',
                submittedAt: summary.dueAt,
                isLate: true,
              },
            ],
          },
        ],
      },
    });
    const { fixture, host, click } = await mount({ isTeacher: true });
    click(summary.title);
    await fixture.whenStable();
    expect(assignments.results).not.toHaveBeenCalled();
    click('View class results');
    await fixture.whenStable();
    expect(assignments.results).toHaveBeenCalledWith('assignment', 1);
    expect(host.textContent).toContain('Learner A');
    expect(host.textContent).toContain('Best score 100%');
    expect(host.textContent).toContain('85% score · 90% accuracy');
    expect(host.textContent).toContain('Late');
    assignments.postFeedback.mockResolvedValue({ id: 'feedback' });
    click('Leave feedback');
    await fixture.whenStable();
    const message = host.querySelector<HTMLTextAreaElement>('#feedback-message')!;
    message.value = 'Try reading the phrase aloud.';
    message.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(message.form?.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(
      false,
    );
    message.form!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(assignments.postFeedback).toHaveBeenCalledWith(
      'assignment',
      'submission',
      'Try reading the phrase aloud.',
    );
    expect(host.textContent).toContain('Feedback posted.');
  });

  it('submits the selected completed attempt and refreshes its late receipt', async () => {
    const detail = {
      ...summary,
      classroomId: 'room',
      revision: 1,
      lessons: [{ id: 'hello', title: 'Hello', isPublished: true }],
    };
    assignments.detail.mockResolvedValueOnce({
      ...detail,
      learnerWork: {
        available: [
          { lessonId: 'hello', sessionId: 'saved-session', score: 75, completedAt: summary.dueAt },
        ],
        submitted: [],
      },
    });
    assignments.detail.mockResolvedValueOnce({
      ...detail,
      learnerWork: {
        available: [],
        submitted: [{ lessonId: 'hello', score: 75, submittedAt: summary.dueAt, isLate: true }],
      },
    });
    assignments.submit.mockResolvedValue({});
    const { fixture, host, click } = await mount();
    click(summary.title);
    await fixture.whenStable();
    click('Submit attempt');
    await fixture.whenStable();
    expect(assignments.submit).toHaveBeenCalledWith('assignment', 'saved-session');
    expect(host.textContent).toContain('Latest submission: 75%');
    expect(host.textContent).toContain('Late');
    expect(host.textContent).not.toContain('Submit attempt');
  });

  it('keeps a stable request key on retry and sends catalog lesson IDs', async () => {
    catalog.units.mockResolvedValue([{ id: 'unit', title: 'First words' }]);
    catalog.lessons.mockResolvedValue([{ id: 'hello', title: 'Hello' }]);
    assignments.create.mockRejectedValueOnce(new Error('connection lost'));
    assignments.create.mockResolvedValueOnce({ id: 'assignment' });
    const { fixture, host, click, change } = await mount({ isTeacher: true, canLeave: false });
    click('New assignment');
    await fixture.whenStable();
    expect(catalog.units).toHaveBeenCalledWith('spanish');
    change('assignment-unit', 'unit', 'change');
    await fixture.whenStable();
    expect(catalog.lessons).toHaveBeenCalledWith('unit');
    host.querySelector<HTMLInputElement>('input[type=checkbox]')!.click();
    change('assignment-name', summary.title);
    change('assignment-deadline', '2030-05-18T12:00');
    click('Create assignment');
    await fixture.whenStable();
    expect(host.textContent).toContain('Retry with the same details');
    click('Create assignment');
    await fixture.whenStable();
    const first = assignments.create.mock.calls[0];
    expect(first).toEqual([
      'room',
      {
        title: summary.title,
        dueAt: new Date('2030-05-18T12:00').toISOString(),
        minimumScore: 80,
        lessonIds: ['hello'],
        requestKey: expect.any(String),
      },
    ]);
    expect(assignments.create.mock.calls[1]).toEqual(first);
    expect(host.textContent).toContain('Assignment created.');
  });

  it('shows archived assignments without creation controls', async () => {
    const { host, fixture, click } = await mount({ isTeacher: true, isArchived: true });
    expect(host.textContent).not.toContain('New assignment');
    click(summary.title);
    await fixture.whenStable();
    expect(host.textContent).toContain('Hello');
    expect(host.textContent).not.toContain('Edit assignment');
    expect(catalog.units).not.toHaveBeenCalled();
  });

  it('edits using the displayed revision and preserves an unchanged exact deadline', async () => {
    const dueAt = '2030-05-18T12:00:43.428123Z';
    assignments.detail.mockResolvedValue({
      ...summary,
      dueAt,
      classroomId: 'room',
      revision: 4,
      lessons: [{ id: 'hello', title: 'Hello', isPublished: true }],
    });
    assignments.edit.mockResolvedValue({ id: 'assignment' });
    catalog.units.mockResolvedValue([]);
    const { fixture, host, click, change } = await mount({ isTeacher: true });
    click(summary.title);
    await fixture.whenStable();
    click('Edit assignment');
    await fixture.whenStable();
    expect(host.querySelector<HTMLInputElement>('#assignment-name')?.value).toBe(summary.title);
    change('assignment-name', 'Updated greetings');
    click('Save changes');
    await fixture.whenStable();
    expect(assignments.edit).toHaveBeenCalledWith('assignment', {
      title: 'Updated greetings',
      dueAt,
      minimumScore: 80,
      lessonIds: ['hello'],
      revision: 4,
    });
    expect(assignments.create).not.toHaveBeenCalled();
    expect(host.textContent).toContain('Assignment updated.');
  });
});
