import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { ContentApi } from '../../core/content/content-api';
import { ManagedSkill, SkillsApi } from '../../core/content/skills-api';
import { SkillManagementPage } from './skill-management';

describe('Course skill management', () => {
  const user = signal({ id: 'admin', roles: ['Admin'] });
  const skill: ManagedSkill = {
    id: 7,
    courseId: 'spanish',
    name: 'Greetings',
    category: 'Vocabulary',
    isArchived: false,
    revision: 2,
  };
  const page = { items: [skill], page: 1, pageSize: 20, totalCount: 1 };
  const courses = vi.fn();
  const api = { list: vi.fn(), create: vi.fn(), edit: vi.fn(), visibility: vi.fn() };

  beforeEach(() => {
    user.set({ id: 'admin', roles: ['Admin'] });
    courses
      .mockReset()
      .mockResolvedValue([
        { id: 'spanish', title: 'Spanish', targetLanguageId: 2, canAuthor: true, canReview: false },
      ]);
    for (const method of Object.values(api)) method.mockReset();
    api.list.mockResolvedValue(page);
    TestBed.configureTestingModule({
      imports: [SkillManagementPage],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user } },
        { provide: ContentApi, useValue: { courses } },
        { provide: SkillsApi, useValue: api },
      ],
    });
  });

  function fill(host: HTMLElement, name: string, value: string) {
    const input = host.querySelector<HTMLInputElement | HTMLTextAreaElement>(
      `[formControlName="${name}"]`,
    )!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  it('locks duplicate creation and reloads the server result', async () => {
    const fixture = TestBed.createComponent(SkillManagementPage);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    let finish!: (value: ManagedSkill) => void;
    api.create.mockImplementation(
      () =>
        new Promise<ManagedSkill>((resolve) => {
          finish = resolve;
        }),
    );
    fill(host, 'name', 'Travel phrases');
    const form = host.querySelector('form[ng-reflect-form]') ?? host.querySelectorAll('form')[1];
    form.dispatchEvent(new Event('submit'));
    form.dispatchEvent(new Event('submit'));
    expect(api.create).toHaveBeenCalledTimes(1);
    expect(api.create).toHaveBeenCalledWith('spanish', 'Travel phrases', 'Vocabulary');
    finish({ ...skill, name: 'Travel phrases' });
    await fixture.whenStable();
    expect(host.textContent).toContain('Skill saved.');
    expect(api.list).toHaveBeenCalledTimes(2);
  });

  it('submits the selected revision and requires a reason before archive or restore', async () => {
    const fixture = TestBed.createComponent(SkillManagementPage);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const edit = Array.from(host.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Edit Greetings'),
    )!;
    edit.click();
    await fixture.whenStable();
    const archive = Array.from(host.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Archive skill'),
    )!;
    expect(archive.disabled).toBe(true);
    fill(host, 'reason', 'Merge new assignments into another skill');
    api.visibility.mockResolvedValue({ ...skill, isArchived: true, revision: 3 });
    await fixture.whenStable();
    archive.click();
    await fixture.whenStable();
    expect(api.visibility).toHaveBeenCalledWith(skill, 'Merge new assignments into another skill');
    expect(host.textContent).toContain('Existing lessons and progress are preserved.');
    expect(host.textContent).toContain('Restore skill');
  });

  it('clears private results and ignores a late response after an account change', async () => {
    let finish!: (value: typeof page) => void;
    api.list.mockImplementation(
      () =>
        new Promise<typeof page>((resolve) => {
          finish = resolve;
        }),
    );
    const fixture = TestBed.createComponent(SkillManagementPage);
    await Promise.resolve();
    user.set({ id: 'other-admin', roles: ['Admin'] });
    finish(page);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).not.toContain('Greetings');
    expect(fixture.nativeElement.textContent).not.toContain('Course skills');
    expect(api.create).not.toHaveBeenCalled();
  });

  it('allows retry when the initial course load fails', async () => {
    courses.mockRejectedValueOnce(new HttpErrorResponse({ status: 503 }));
    const fixture = TestBed.createComponent(SkillManagementPage);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Please retry.');
    Array.from(host.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Reload skills'))!
      .click();
    await fixture.whenStable();
    expect(courses).toHaveBeenCalledTimes(2);
    expect(host.textContent).toContain('Greetings');
  });

  it('submits search without navigating away or resetting the filters', async () => {
    const fixture = TestBed.createComponent(SkillManagementPage);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const input = host.querySelector<HTMLInputElement>('input:not([formControlName])')!;
    input.value = 'greet';
    input.dispatchEvent(new Event('input'));
    const submit = new Event('submit', { cancelable: true });
    host.querySelector('form')!.dispatchEvent(submit);
    await fixture.whenStable();
    expect(submit.defaultPrevented).toBe(true);
    expect(api.list).toHaveBeenLastCalledWith('spanish', 1, 20, 'greet', false);
    expect(input.value).toBe('greet');
  });
});
