import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { ExtraStudyApi } from '../../core/classrooms/extra-study-api';
import { CatalogApi } from '../../core/content/catalog-api';
import { ExtraStudy } from './extra-study';

describe('Teacher request cancellation', () => {
  const user = signal({ id: 'learner', roles: ['Learner'] });
  const api = {
    teachers: vi.fn(),
    requests: vi.fn(),
    close: vi.fn(),
    request: vi.fn(),
    availability: vi.fn(),
  };
  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'learner', roles: ['Learner'] });
    api.availability.mockResolvedValue([]);
    api.teachers.mockResolvedValue([{ id: 'offer', displayName: 'Tutor', courseTitle: 'Spanish' }]);
    api.close.mockResolvedValue({});
    api.request.mockResolvedValue({});
    TestBed.configureTestingModule({
      imports: [ExtraStudy],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user } },
        { provide: CatalogApi, useValue: { courses: async () => [] } },
        { provide: ExtraStudyApi, useValue: api },
      ],
    });
  });
  it('opens teaching requests directly and keeps the learner directory optional for teachers', async () => {
    user.set({ id: 'teacher', roles: ['Teacher', 'Learner'] });
    api.requests.mockResolvedValue([]);
    const fixture = TestBed.createComponent(ExtraStudy);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.querySelector('h1')?.textContent).toBe('Your teaching requests');
    expect(host.querySelector<HTMLDetailsElement>('.directory-details')!.open).toBe(false);
    expect(host.querySelector('#requests-title')).not.toBeNull();
    expect(host.textContent).toContain('Your teaching availability');
    expect(api.availability).toHaveBeenCalledOnce();
  });
  it('unlocks the teacher card after withdrawal and keeps the closed history', async () => {
    const request = {
      id: 'old',
      offerId: 'offer',
      incoming: false,
      status: 'Pending',
      displayName: 'Tutor',
    };
    api.requests.mockResolvedValue([request]);
    const fixture = TestBed.createComponent(ExtraStudy);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const card = host.querySelector<HTMLButtonElement>('.tutor-card button')!;
    expect(card.disabled).toBe(true);
    const button = (label: string) =>
      [...host.querySelectorAll<HTMLButtonElement>('button')].find(
        (item) => item.textContent?.trim() === label,
      )!;
    button('Withdraw request').click();
    await fixture.whenStable();
    api.requests.mockResolvedValue([{ ...request, status: 'Closed' }]);
    button('Confirm close').click();
    await fixture.whenStable();
    expect(api.close).toHaveBeenCalledExactlyOnceWith('old');
    expect(host.textContent).toContain('Closed');
    expect(card.disabled).toBe(false);
    expect(card.textContent).toContain('Request extra study');
    api.requests.mockResolvedValue([
      { ...request, status: 'Closed' },
      { ...request, id: 'new' },
    ]);
    card.click();
    await fixture.whenStable();
    expect(api.request).toHaveBeenCalledExactlyOnceWith('offer');
    expect(card.disabled).toBe(true);
  });
  it('places an accepted learner conversation before the teacher directory', async () => {
    api.requests.mockResolvedValue([
      {
        id: 'accepted',
        status: 'Accepted',
        incoming: false,
        displayName: 'Sofia',
        courseTitle: 'Italian',
      },
    ]);
    const fixture = TestBed.createComponent(ExtraStudy);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const requests = host.querySelector('[aria-labelledby="requests-title"]')!;
    const directory = host.querySelector('.directory-details')!;
    expect(
      requests.compareDocumentPosition(directory) & Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy();
    expect(requests.querySelector('a')?.getAttribute('href')).toBe('/classes/extra-study/accepted');
    expect(requests.textContent).toContain('lessons and chat');
  });
});
