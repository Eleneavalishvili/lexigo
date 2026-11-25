import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { ExtraStudyApi } from '../../core/classrooms/extra-study-api';
import { CatalogApi } from '../../core/content/catalog-api';
import { ExtraStudy } from './extra-study';

describe('Teacher request cancellation', () => {
  const api = { teachers: vi.fn(), requests: vi.fn(), close: vi.fn(), request: vi.fn() };
  beforeEach(() => {
    vi.resetAllMocks();
    api.teachers.mockResolvedValue([{ id: 'offer', displayName: 'Tutor', courseTitle: 'Spanish' }]);
    api.close.mockResolvedValue({});
    api.request.mockResolvedValue({});
    TestBed.configureTestingModule({
      imports: [ExtraStudy],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user: signal({ id: 'learner', roles: ['Learner'] }) } },
        { provide: CatalogApi, useValue: { courses: async () => [] } },
        { provide: ExtraStudyApi, useValue: api },
      ],
    });
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
});
