import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AccountApi, DeletionResponsibility } from '../../core/accounts/account-api';
import { DeletionResponsibilities } from './deletion-responsibilities';

describe('Deletion responsibilities checklist', () => {
  const load = vi.fn();

  beforeEach(() => {
    load.mockReset().mockResolvedValue([]);
    TestBed.configureTestingModule({
      imports: [DeletionResponsibilities],
      providers: [
        provideRouter([]),
        { provide: AccountApi, useValue: { deletionResponsibilities: load } },
      ],
    });
  });

  it('links each unfinished responsibility to its resolution screen', async () => {
    load.mockResolvedValue([
      { code: 'classes', message: 'Archive active classes.' },
      { code: 'extra-study', message: 'Close outstanding requests.' },
      { code: 'lesson-drafts', message: 'Resolve lesson drafts.' },
      { code: 'vocabulary-drafts', message: 'Withdraw vocabulary drafts.' },
      { code: 'recordings', message: 'Remove unused recordings.' },
    ]);
    const fixture = TestBed.createComponent(DeletionResponsibilities);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    expect(
      Array.from(host.querySelectorAll('li a')).map((link) => link.getAttribute('href')),
    ).toEqual([
      '/classes',
      '/classes/extra-study',
      '/manage/lessons',
      '/manage/vocabulary/revisions',
      '/manage/media',
    ]);
    expect(host.textContent).not.toContain('You have no unfinished');
    load.mockResolvedValue([]);
    host.querySelector<HTMLButtonElement>('button')!.click();
    await fixture.whenStable();
    expect(host.querySelector('li')).toBeNull();
    expect(host.textContent).toContain('You have no unfinished teaching');
    expect(host.textContent).toContain('Security and retained billing records are checked');
  });

  it('shows a retry after failure without claiming the account has no outstanding work', async () => {
    load.mockRejectedValueOnce(new Error('Synthetic network failure'));
    const fixture = TestBed.createComponent(DeletionResponsibilities);
    await fixture.whenStable();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('[role=alert]')?.textContent).toContain('could not be checked');
    expect(host.textContent).not.toContain('You have no unfinished');
    host.querySelector<HTMLButtonElement>('button')!.click();
    await fixture.whenStable();
    expect(host.querySelector('[role=alert]')).toBeNull();
    expect(host.textContent).toContain('You have no unfinished');
  });

  it('ignores a response after leaving the page', async () => {
    let finish!: (value: DeletionResponsibility[]) => void;
    load.mockReturnValue(
      new Promise<DeletionResponsibility[]>((resolve) => {
        finish = resolve;
      }),
    );
    const fixture = TestBed.createComponent(DeletionResponsibilities);
    fixture.detectChanges();
    fixture.destroy();
    finish([{ code: 'classes', message: 'Late response' }]);
    await Promise.resolve();
    expect(fixture.componentInstance['pending']()).toEqual([]);
  });
});
