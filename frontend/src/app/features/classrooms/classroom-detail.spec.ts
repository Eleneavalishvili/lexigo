import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { ClassroomApi, ClassroomDetail } from '../../core/classrooms/classroom-api';
import { ClassroomDetailPage } from './classroom-detail';
import { AssignmentApi } from '../../core/classrooms/assignment-api';
import { LearningApi } from '../../core/learning/learning-api';

function value(teacher = true): ClassroomDetail {
  return {
    classroom: {
      id: 'room',
      courseId: 'course',
      courseTitle: 'Spanish',
      name: 'Our class',
      description: 'Learn together',
      teacherName: 'Teacher',
      isArchived: false,
      revision: 3,
      isTeacher: teacher,
      canLeave: !teacher,
      coursePublished: true,
    },
    members: {
      items: [
        {
          userId: 'student',
          displayName: '<img src=x>',
          joinedAt: '2026-11-03T10:00:00Z',
          status: 'Active',
        },
      ],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    },
    invitation: null,
  };
}
describe('Classroom detail privacy and actions', () => {
  const user = signal<{ id: string; roles: string[] }>({ id: 'teacher', roles: ['Teacher'] });
  const params = new BehaviorSubject(convertToParamMap({ classId: 'room' }));
  const api = {
    detail: vi.fn(),
    invite: vi.fn(),
    edit: vi.fn(),
    remove: vi.fn(),
    restore: vi.fn(),
  };
  const assignments = { list: vi.fn() };
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'teacher', roles: ['Teacher'] });
    params.next(convertToParamMap({ classId: 'room' }));
    api.detail.mockResolvedValue(value());
    assignments.list.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    api.invite.mockResolvedValue({
      code: 'invitation-for-unit-test',
      expiresAt: '2026-11-10T10:00:00Z',
      maxUses: 30,
    });
    api.edit.mockResolvedValue({ id: 'room' });
    api.remove.mockResolvedValue({ id: 'room' });
    TestBed.configureTestingModule({
      imports: [ClassroomDetailPage],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: params } },
        { provide: AuthStore, useValue: { user } },
        { provide: ClassroomApi, useValue: api },
        { provide: AssignmentApi, useValue: assignments },
        { provide: LearningApi, useValue: {} },
      ],
    });
  });
  it('does not render a roster or invitation controls for a learner even if extra data arrives', async () => {
    user.set({ id: 'learner', roles: ['Learner'] });
    api.detail.mockResolvedValue(value(false));
    const fixture = TestBed.createComponent(ClassroomDetailPage);
    await fixture.whenStable();
    const text = fixture.nativeElement.textContent;
    expect(text).not.toContain('Members');
    expect(text).not.toContain('<img src=x>');
    expect(text).toContain('Leave class');
    expect(text).toContain('Hide assignments');
    expect(text).toContain('Assignments');
    expect(assignments.list).toHaveBeenCalledExactlyOnceWith('room', 1);
    expect(api.invite).not.toHaveBeenCalled();
  });
  it('generates a code only on click and removes it on refresh', async () => {
    const fixture = TestBed.createComponent(ClassroomDetailPage);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(api.invite).not.toHaveBeenCalled();
    host.querySelector<HTMLButtonElement>('form button[type=submit]')!.click();
    await fixture.whenStable();
    expect(api.invite).toHaveBeenCalledWith('room', 7, 30);
    expect(host.querySelector<HTMLInputElement>('#fresh-code')!.value).toBe(
      'invitation-for-unit-test',
    );
    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Refresh class'))!
      .click();
    await fixture.whenStable();
    expect(host.querySelector('#fresh-code')).toBeNull();
  });
  it('requires archive acknowledgement and sends the displayed revision', async () => {
    const fixture = TestBed.createComponent(ClassroomDetailPage);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const button = [...host.querySelectorAll('button')].find(
      (item) => item.textContent?.trim() === 'Archive class',
    )!;
    expect(button.disabled).toBe(true);
    const checkbox = host.querySelector<HTMLInputElement>('input[type=checkbox]')!;
    checkbox.checked = true;
    checkbox.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    button.click();
    await fixture.whenStable();
    expect(api.edit).toHaveBeenCalledWith(
      'room',
      expect.objectContaining({ revision: 3, isArchived: true }),
    );
  });
  it('uses the signed-in learner identity for leaving and keeps names escaped', async () => {
    user.set({ id: 'learner', roles: ['Learner'] });
    api.detail.mockResolvedValue(value(false));
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(ClassroomDetailPage);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.trim() === 'Leave class')!
      .click();
    await fixture.whenStable();
    expect(api.remove).toHaveBeenCalledWith('room', 'learner');
    expect(navigate).toHaveBeenCalledWith(['/classes']);
    expect(host.querySelector('img')).toBeNull();
  });
});
