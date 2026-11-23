import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthStore } from '../../core/accounts/auth-store';
import { RewardCatalogApi } from '../../core/content/reward-catalog-api';
import { RewardCatalog } from './reward-catalog';

const row = {
  id: 'badge',
  code: 'first-lesson',
  title: 'Your first little step',
  ruleType: 'CompletedLessons',
  threshold: 1,
  revision: 1,
  isArchived: false,
  canEdit: true,
};

describe('Staff reward catalog', () => {
  const user = signal<{ id: string; roles: string[] } | null>({ id: 'admin', roles: ['Admin'] });
  const api = {
    achievements: vi.fn(),
    challenges: vi.fn(),
    saveAchievement: vi.fn(),
    saveChallenge: vi.fn(),
    archive: vi.fn(),
  };
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'admin', roles: ['Admin'] });
    api.achievements.mockResolvedValue({
      items: [row],
      page: 1,
      pageSize: 20,
      hasMore: false,
      totalCount: 1,
    });
    api.challenges.mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 20,
      hasMore: false,
      totalCount: 0,
    });
    api.saveAchievement.mockResolvedValue({ id: 'badge' });
    api.archive.mockResolvedValue({ id: 'badge' });
    TestBed.configureTestingModule({
      imports: [RewardCatalog],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user } },
        { provide: RewardCatalogApi, useValue: api },
      ],
    });
  });
  it('reads without mutating and switches catalogs explicitly', async () => {
    const fixture = TestBed.createComponent(RewardCatalog);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain(row.title);
    expect(api.saveAchievement).not.toHaveBeenCalled();
    expect(api.archive).not.toHaveBeenCalled();
    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.trim() === 'Challenges')!
      .click();
    await fixture.whenStable();
    expect(api.challenges).toHaveBeenCalledWith(1, 20, '');
    expect(host.textContent).toContain('No records on this page');
  });
  it('uses the displayed revision and requires a reason before retiring a protected badge', async () => {
    api.achievements.mockResolvedValue({
      items: [{ ...row, canEdit: false }],
      page: 1,
      pageSize: 20,
      hasMore: false,
      totalCount: 1,
    });
    const fixture = TestBed.createComponent(RewardCatalog);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    host.querySelector<HTMLButtonElement>('.record')!.click();
    await fixture.whenStable();
    const reason = host.querySelector<HTMLTextAreaElement>('#badge-reason')!;
    reason.value = 'Retire this outdated milestone.';
    reason.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.trim() === 'Retire badge')!
      .click();
    await fixture.whenStable();
    expect(api.archive).toHaveBeenCalledWith(
      'achievements',
      expect.objectContaining({ id: 'badge', revision: 1 }),
      reason.value,
    );
    expect(host.textContent).toContain('Existing learner history is preserved');
  });
  it('does not expose controls or stale results after account/role changes', async () => {
    let resolve!: (value: unknown) => void;
    api.achievements.mockReturnValueOnce(
      new Promise((done) => {
        resolve = done;
      }),
    );
    const fixture = TestBed.createComponent(RewardCatalog);
    fixture.detectChanges();
    await Promise.resolve();
    user.set({ id: 'learner', roles: ['Learner'] });
    fixture.detectChanges();
    resolve({ items: [row], page: 1, pageSize: 20, hasMore: false, totalCount: 1 });
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).not.toContain(row.title);
    expect(fixture.nativeElement.querySelector('.workspace')).toBeNull();
    expect(api.archive).not.toHaveBeenCalled();
  });
  it('sends bounded search filters without reloading the browser and displays the filtered count', async () => {
    const fixture = TestBed.createComponent(RewardCatalog);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const input = host.querySelector<HTMLInputElement>('.catalog-filters input')!;
    input.value = 'milestone';
    input.dispatchEvent(new Event('input'));
    const submit = new Event('submit', { cancelable: true });
    host.querySelector('.catalog-filters')!.dispatchEvent(submit);
    await fixture.whenStable();
    expect(submit.defaultPrevented).toBe(true);
    expect(api.achievements).toHaveBeenLastCalledWith(1, 20, 'milestone');
    expect(host.textContent).toContain('1 record · Page 1');
    const size = host.querySelector<HTMLSelectElement>('.catalog-filters select')!;
    size.value = '10';
    size.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(api.achievements).toHaveBeenLastCalledWith(1, 10, 'milestone');
  });

  it('retains the editor and explains a stale-revision conflict without claiming success', async () => {
    api.archive.mockRejectedValueOnce(
      new HttpErrorResponse({
        status: 409,
        error: { title: 'This badge changed. Refresh the catalog.' },
      }),
    );
    const fixture = TestBed.createComponent(RewardCatalog);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    host.querySelector<HTMLButtonElement>('.record')!.click();
    await fixture.whenStable();
    const reason = host.querySelector<HTMLTextAreaElement>('#badge-reason')!;
    reason.value = 'Retire this outdated milestone.';
    reason.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    [...host.querySelectorAll('button')]
      .find((button) => button.textContent?.trim() === 'Retire badge')!
      .click();
    await fixture.whenStable();
    expect(host.querySelector('[role=alert]')?.textContent).toContain('Refresh the catalog');
    expect(host.querySelector('[role=status]')).toBeNull();
    expect(host.querySelector('app-achievement-editor')).not.toBeNull();
  });
});
