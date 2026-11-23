import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { AuthStore } from '../../core/accounts/auth-store';
import { CompletionCardsApi, OwnedCompletionCard } from '../../core/learning/completion-cards-api';
import { CompletionCardsPage } from './completion-cards';

describe('Private completion cards and sharing', () => {
  const user = signal<{ id: string } | null>({ id: 'learner' });
  const card: OwnedCompletionCard = {
    id: 'card',
    courseId: 'course',
    unitId: 'unit',
    isShared: false,
    shareCode: null,
    revision: 1,
    details: {
      displayName: '<img src=x onerror=alert(1)>',
      courseTitle: 'Spanish',
      unitTitle: 'First conversations',
      lessonCount: 3,
      gradedLessonCount: 3,
      averageBestScore: 90,
      verificationCode: 'a'.repeat(32),
      completedAt: '2026-11-06T10:00:00Z',
      issuedAt: '2026-11-06T10:00:01Z',
    },
  };
  const api = { list: vi.fn(), get: vi.fn(), claim: vi.fn(), sharing: vi.fn() };
  let saved: OwnedCompletionCard;
  let params: BehaviorSubject<ReturnType<typeof convertToParamMap>>;
  beforeEach(() => {
    vi.resetAllMocks();
    user.set({ id: 'learner' });
    saved = structuredClone(card);
    params = new BehaviorSubject(convertToParamMap({}));
    api.list.mockImplementation(async () => ({
      items: [saved],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    }));
    api.get.mockImplementation(async () => saved);
    api.sharing.mockImplementation(async (_card, enabled) => {
      saved = {
        ...saved,
        isShared: enabled,
        shareCode: enabled ? 'b'.repeat(32) : null,
        revision: saved.revision + 1,
      };
      return { id: saved.id };
    });
    api.claim.mockResolvedValue({ id: 'card' });
    TestBed.configureTestingModule({
      imports: [CompletionCardsPage],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { queryParamMap: params } },
        { provide: AuthStore, useValue: { user } },
        { provide: CompletionCardsApi, useValue: api },
      ],
    });
  });
  function button(host: HTMLElement, text: string): HTMLButtonElement {
    return Array.from(host.querySelectorAll<HTMLButtonElement>('button')).find((item) =>
      item.textContent?.includes(text),
    )!;
  }
  async function open() {
    const fixture = TestBed.createComponent(CompletionCardsPage);
    await fixture.whenStable();
    return { fixture, host: fixture.nativeElement as HTMLElement };
  }
  async function consent(host: HTMLElement, fixture: { whenStable(): Promise<unknown> }) {
    host.querySelector<HTMLInputElement>('input[type=checkbox]')!.click();
    await fixture.whenStable();
  }

  it('renders the private card safely and requires consent before exposing a public link', async () => {
    const { fixture, host } = await open();
    expect(host.textContent).toContain(card.details.displayName);
    expect(host.querySelector('img')).toBeNull();
    expect(host.querySelector('#card-share-link')).toBeNull();
    expect(button(host, 'Enable public sharing').disabled).toBe(true);
    expect(api.sharing).not.toHaveBeenCalled();
    await consent(host, fixture);
    button(host, 'Enable public sharing').click();
    await fixture.whenStable();
    expect(api.sharing).toHaveBeenCalledExactlyOnceWith(card, true, true);
    expect(host.querySelector<HTMLInputElement>('#card-share-link')?.value).toContain(
      '/shared/completion/' + 'b'.repeat(32),
    );
    button(host, 'Turn sharing off').click();
    await fixture.whenStable();
    expect(host.querySelector('#card-share-link')).toBeNull();
    expect(host.textContent).toContain('old public link is unavailable');
  });

  it('requires reloading after an uncertain sharing result before changing or copying anything', async () => {
    const { fixture, host } = await open();
    await consent(host, fixture);
    api.get.mockRejectedValueOnce(new Error('Synthetic lost response'));
    button(host, 'Enable public sharing').click();
    await fixture.whenStable();
    expect(host.querySelector('#card-share-link')).toBeNull();
    expect(button(host, 'Enable public sharing')).toBeUndefined();
    expect(host.textContent).toContain('Reload your cards');
    button(host, 'Reload cards').click();
    await fixture.whenStable();
    expect(host.querySelector('#card-share-link')).not.toBeNull();
    expect(api.sharing).toHaveBeenCalledOnce();
  });

  it('does not claim a historic card merely by opening a unit link', async () => {
    params.next(convertToParamMap({ unitId: 'unit' }));
    api.list.mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const { fixture, host } = await open();
    expect(api.claim).not.toHaveBeenCalled();
    button(host, 'Create my completed-unit card').click();
    await fixture.whenStable();
    expect(api.claim).toHaveBeenCalledExactlyOnceWith('unit');
    expect(host.querySelector('app-completion-card-art')).not.toBeNull();
  });

  it('clears private details and ignores a late list response after switching accounts', async () => {
    let finish!: (value: unknown) => void;
    api.list.mockReturnValueOnce(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    const fixture = TestBed.createComponent(CompletionCardsPage);
    fixture.detectChanges();
    user.set({ id: 'other' });
    fixture.detectChanges();
    finish({ items: [card], page: 1, pageSize: 20, totalCount: 1 });
    await fixture.whenStable();
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('app-completion-card-art'),
    ).toBeNull();
  });

  it('discards an older unit response after navigating to another unit', async () => {
    let finish!: (value: unknown) => void;
    api.list.mockReturnValueOnce(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    const fixture = TestBed.createComponent(CompletionCardsPage);
    fixture.detectChanges();
    params.next(convertToParamMap({ unitId: 'latest' }));
    await vi.waitFor(() => expect(api.list).toHaveBeenCalledTimes(2));
    finish({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    await fixture.whenStable();
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('app-completion-card-art'),
    ).not.toBeNull();
  });
});
