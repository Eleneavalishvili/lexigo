import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { ClassroomApi } from '../../core/classrooms/classroom-api';
import { CatalogApi } from '../../core/content/catalog-api';
import { Classrooms } from './classrooms';

function fill(host: HTMLElement, selector: string, value: string): void {
  const field = host.querySelector<HTMLInputElement>(selector)!;
  field.value = value;
  field.dispatchEvent(new Event('input'));
  field.dispatchEvent(new Event('change'));
}
async function ready(fixture: ComponentFixture<Classrooms>): Promise<void> {
  await vi.waitFor(async () => {
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('button.text-button').disabled).toBe(false);
  });
}
describe('Private classroom list', () => {
  const user = signal<{ id: string; roles: string[] } | null>({
    id: 'learner',
    roles: ['Learner'],
  });
  const api = { list: vi.fn(), join: vi.fn(), create: vi.fn() };
  const catalog = { courses: vi.fn() };
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'learner', roles: ['Learner'] });
    api.list.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    api.join.mockResolvedValue({ id: 'classroom' });
    api.create.mockResolvedValue({ id: 'classroom' });
    catalog.courses.mockResolvedValue([{ id: 'course', title: 'Spanish' }]);
    TestBed.configureTestingModule({
      imports: [Classrooms],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user } },
        { provide: ClassroomApi, useValue: api },
        { provide: CatalogApi, useValue: catalog },
      ],
    });
  });
  it('does not invent classes or expose teacher creation to a learner', async () => {
    const fixture = TestBed.createComponent(Classrooms);
    await ready(fixture);
    expect(fixture.nativeElement.textContent).toContain('No classes yet');
    expect(fixture.nativeElement.textContent).not.toContain('Create a class');
    expect(catalog.courses).not.toHaveBeenCalled();
    expect(api.join).not.toHaveBeenCalled();
  });
  it('posts invitation only after submission and navigates without putting the code in the URL', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(Classrooms);
    await ready(fixture);
    const host: HTMLElement = fixture.nativeElement;
    fill(host, '#join-code', '  invitation-for-unit-test  ');
    host
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await ready(fixture);
    expect(api.join).toHaveBeenCalledWith('invitation-for-unit-test');
    expect(navigate).toHaveBeenCalledWith(['/classes', 'classroom']);
    expect(host.querySelector<HTMLInputElement>('#join-code')!.value).toBe('');
  });
  it('drops a late join response and clears the invitation when the account changes', async () => {
    let resolve!: (value: { id: string }) => void;
    api.join.mockReturnValueOnce(
      new Promise((done) => {
        resolve = done;
      }),
    );
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(Classrooms);
    await ready(fixture);
    const host: HTMLElement = fixture.nativeElement;
    fill(host, '#join-code', 'invitation-for-unit-test');
    host
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await Promise.resolve();
    user.set({ id: 'other', roles: ['Learner'] });
    fixture.detectChanges();
    resolve({ id: 'old-class' });
    await ready(fixture);
    expect(navigate).not.toHaveBeenCalled();
    expect(host.querySelector<HTMLInputElement>('#join-code')!.value).toBe('');
  });
  it('requires a course selection and safely reuses the creation key after an unknown result', async () => {
    user.set({ id: 'teacher', roles: ['Teacher', 'Learner'] });
    api.create.mockRejectedValueOnce(new Error('connection lost'));
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(Classrooms);
    await ready(fixture);
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('h1')?.textContent).toContain('Your teacher desk.');
    expect(
      host.querySelector('nav[aria-label="Teacher tools"] a[href="/classes/extra-study"]'),
    ).not.toBeNull();
    expect(
      host.querySelector('nav[aria-label="Teacher tools"] a[href="/notifications"]'),
    ).not.toBeNull();
    expect(host.querySelector('details')?.open).toBe(false);
    expect(host.querySelector<HTMLSelectElement>('#class-course')!.value).toBe('');
    fill(host, '#class-course', 'course');
    fill(host, '#class-name', 'Our class');
    const form = host.querySelectorAll('form')[1];
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await ready(fixture);
    expect(host.querySelector('[role=alert]')).not.toBeNull();
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await ready(fixture);
    expect(api.create).toHaveBeenCalledTimes(2);
    expect(api.create.mock.calls[0][0].requestKey).toBe(api.create.mock.calls[1][0].requestKey);
    expect(api.create.mock.calls[0][0].courseId).toBe('course');
  });
});
