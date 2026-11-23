import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CatalogApi } from '../../core/content/catalog-api';
import { Catalog } from './catalog';

describe('Published course catalog', () => {
  const api = { courses: vi.fn(), units: vi.fn(), lessons: vi.fn() };
  beforeEach(() => {
    vi.clearAllMocks();
    api.courses.mockResolvedValue([]);
    api.units.mockResolvedValue([
      {
        id: 'unit',
        title: 'Greetings',
        description: 'First words',
        difficulty: 'Beginner',
        orderNumber: 1,
      },
    ]);
    api.lessons.mockResolvedValue([
      {
        id: 'lesson',
        unitId: 'unit',
        versionId: 'published',
        title: 'Say hello',
        objective: 'Recognize a greeting',
        explanation: '<script>untrusted()</script>',
        orderNumber: 1,
        exerciseCount: 2,
      },
    ]);
    TestBed.configureTestingModule({
      imports: [Catalog],
      providers: [provideRouter([]), { provide: CatalogApi, useValue: api }],
    });
  });
  it('shows an honest empty state when no reviewed course is published', async () => {
    const fixture = TestBed.createComponent(Catalog);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('being prepared');
    expect(host.querySelector('.course-card')).toBeNull();
  });
  it('navigates published chapters and renders explanations as text', async () => {
    api.courses.mockResolvedValue([
      {
        id: 'course',
        title: 'Spanish',
        description: 'Through English',
        languageCode: 'es',
        lessonCount: 1,
      },
    ]);
    const fixture = TestBed.createComponent(Catalog);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    host.querySelector<HTMLButtonElement>('.course-card')!.click();
    await fixture.whenStable();
    host.querySelector<HTMLButtonElement>('.content-grid nav button')!.click();
    await fixture.whenStable();
    host.querySelector<HTMLButtonElement>('.lesson-list button')!.click();
    await fixture.whenStable();
    expect(host.querySelector('.explanation')?.textContent).toContain(
      '<script>untrusted()</script>',
    );
    expect(host.querySelector('.explanation script')).toBeNull();
    expect(api.units).toHaveBeenCalledWith('course');
    expect(api.lessons).toHaveBeenCalledWith('unit');
  });
  it('offers a real retry on catalog failure', async () => {
    api.courses.mockRejectedValueOnce(new Error('offline'));
    const fixture = TestBed.createComponent(Catalog);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('[role="alert"]')).not.toBeNull();
    Array.from(host.querySelectorAll<HTMLButtonElement>('button'))
      .find((button) => button.textContent?.includes('Try again'))!
      .click();
    await fixture.whenStable();
    expect(api.courses).toHaveBeenCalledTimes(2);
    expect(host.querySelector('[role="alert"]')).toBeNull();
  });
  it('explains an empty course without offering unavailable lesson links', async () => {
    api.courses.mockResolvedValue([
      { id: 'course', title: 'Spanish', description: '', languageCode: 'es', lessonCount: 0 },
    ]);
    api.units.mockResolvedValue([]);
    const fixture = TestBed.createComponent(Catalog);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    host.querySelector<HTMLButtonElement>('.course-card')!.click();
    await fixture.whenStable();
    expect(host.textContent).toContain('No chapters are published');
    expect(host.textContent).not.toContain('Pick a chapter');
    expect(host.querySelector('a[href="/try/course"]')).toBeNull();
    expect(host.querySelector('a[href="/learn/course"]')).toBeNull();
  });
  it('explains an empty chapter rather than leaving a blank lesson list', async () => {
    api.courses.mockResolvedValue([
      { id: 'course', title: 'Spanish', description: '', languageCode: 'es', lessonCount: 1 },
    ]);
    api.lessons.mockResolvedValue([]);
    const fixture = TestBed.createComponent(Catalog);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    host.querySelector<HTMLButtonElement>('.course-card')!.click();
    await fixture.whenStable();
    host.querySelector<HTMLButtonElement>('.content-grid nav button')!.click();
    await fixture.whenStable();
    expect(host.querySelector('.lesson-list [role=status]')?.textContent).toContain(
      'No lessons are published in this chapter yet',
    );
    expect(host.querySelector('.lesson-list button')).toBeNull();
  });
});
