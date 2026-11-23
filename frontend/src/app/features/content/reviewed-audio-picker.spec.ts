import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { MediaApi, StaffMedia } from '../../core/content/media-api';
import { ReviewedAudioPicker } from './reviewed-audio-picker';

describe('Reviewed audio selection', () => {
  const user = signal<{ id: string } | null>({ id: 'author' });
  const api = { list: vi.fn() };
  const good = {
    id: 'good',
    languageId: 2,
    isVerified: true,
    storageState: 'Ready',
    source: 'My reviewed voice',
    durationMilliseconds: 1000,
  } as StaffMedia;
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'author' });
    api.list.mockResolvedValue([
      good,
      { ...good, id: 'foreign', languageId: 3 },
      { ...good, id: 'unreviewed', isVerified: false },
    ]);
    TestBed.configureTestingModule({
      imports: [ReviewedAudioPicker],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user } },
        { provide: MediaApi, useValue: api },
      ],
    });
  });
  it('offers only ready reviewed recordings for the selected language and requires explicit selection', async () => {
    const fixture = TestBed.createComponent(ReviewedAudioPicker);
    fixture.componentRef.setInput('languageId', 2);
    const chosen = vi.fn();
    fixture.componentInstance.chosen.subscribe(chosen);
    await fixture.whenStable();
    const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>(
      'button[aria-pressed]',
    );
    expect(buttons.length).toBe(1);
    expect(chosen).not.toHaveBeenCalled();
    buttons[0].click();
    expect(chosen).toHaveBeenCalledWith('good');
  });
  it('does not reveal a late library response after the account changes', async () => {
    let resolve!: (rows: StaffMedia[]) => void;
    api.list.mockReturnValueOnce(
      new Promise((finish) => {
        resolve = finish;
      }),
    );
    const fixture = TestBed.createComponent(ReviewedAudioPicker);
    fixture.componentRef.setInput('languageId', 2);
    fixture.detectChanges();
    user.set({ id: 'another' });
    fixture.detectChanges();
    resolve([good]);
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).not.toContain('My reviewed voice');
  });
});
