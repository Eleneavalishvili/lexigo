import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { ContentApi } from '../../core/content/content-api';
import { MediaApi, StaffMedia } from '../../core/content/media-api';
import { MediaLibrary } from './media-library';

@Component({ template: '' })
class AccountDestination {}

describe('Staff audio library', () => {
  const user = signal<{ id: string; roles: string[] } | null>(null);
  const row: StaffMedia = {
    id: 'recording',
    languageId: 2,
    mimeType: 'audio/wav',
    byteSize: 1644,
    durationMilliseconds: 100,
    source: '<img src=x onerror=alert(1)> original recording',
    license: 'Original author permission',
    uploadedBy: 'author',
    isVerified: false,
    storageState: 'Ready',
    createdAt: '2026-10-28T12:00:00Z',
    verifiedBy: null,
    reviewComment: 'Please check the final word.',
  };
  const courses = [
    { id: 'course', title: 'Spanish', targetLanguageId: 2, canAuthor: true, canReview: true },
  ];
  const api = {
    list: vi.fn(),
    upload: vi.fn(),
    preview: vi.fn(),
    review: vi.fn(),
    remove: vi.fn(),
    finishDeletion: vi.fn(),
  };
  const content = { courses: vi.fn() };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(() => {});
    vi.spyOn(HTMLMediaElement.prototype, 'load').mockImplementation(() => {});
    user.set({ id: 'author', roles: ['ContentAuthor', 'Reviewer'] });
    api.list.mockResolvedValue([row]);
    api.upload.mockResolvedValue({ id: row.id });
    api.review.mockResolvedValue({});
    api.remove.mockResolvedValue({});
    api.finishDeletion.mockResolvedValue({});
    api.preview.mockResolvedValue({
      url: 'http://localhost:3900/private/signed-test',
      expiresInSeconds: 120,
    });
    content.courses.mockResolvedValue(courses);
    TestBed.configureTestingModule({
      imports: [MediaLibrary],
      providers: [
        provideRouter([{ path: 'account', component: AccountDestination }]),
        { provide: AuthStore, useValue: { user } },
        { provide: MediaApi, useValue: api },
        { provide: ContentApi, useValue: content },
      ],
    });
  });

  async function open() {
    const fixture = TestBed.createComponent(MediaLibrary);
    await fixture.whenStable();
    return { fixture, host: fixture.nativeElement as HTMLElement };
  }

  function button(host: HTMLElement, text: string) {
    return Array.from(host.querySelectorAll<HTMLButtonElement>('button')).find((item) =>
      item.textContent?.includes(text),
    )!;
  }

  it('renders source and reviewer comments as text and hides self-approval', async () => {
    const { host } = await open();
    expect(host.textContent).toContain(row.source);
    expect(host.querySelector('.audio-card img')).toBeNull();
    expect(host.textContent).toContain(row.reviewComment);
    expect(button(host, 'Approve audio')).toBeUndefined();
    expect(button(host, 'Review recording')).toBeUndefined();
  });

  it('uploads only after explicit file selection and completed permission fields', async () => {
    const { fixture, host } = await open();
    const input = host.querySelector<HTMLInputElement>('input[type=file]')!;
    const file = new File(['synthetic test bytes; server validates real files'], 'test.wav', {
      type: 'audio/wav',
    });
    Object.defineProperty(input, 'files', { value: [file], configurable: true });
    input.dispatchEvent(new Event('change'));
    const source = host.querySelector<HTMLTextAreaElement>('textarea[formControlName=source]')!;
    source.value = 'Recorded by me';
    source.dispatchEvent(new Event('input'));
    const license = host.querySelector<HTMLTextAreaElement>('textarea[formControlName=license]')!;
    license.value = 'I own this recording';
    license.dispatchEvent(new Event('input'));
    expect(api.upload).not.toHaveBeenCalled();
    host
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(api.upload).toHaveBeenCalledWith(file, 2, 'Recorded by me', 'I own this recording');
    expect(host.textContent).toContain('Audio uploaded.');
    expect(input.value).toBe('');
  });

  it('requires a separate deletion confirmation and removes the active player', async () => {
    const { fixture, host } = await open();
    button(host, 'Listen to recording').click();
    await fixture.whenStable();
    expect(host.querySelector('audio')).not.toBeNull();
    button(host, 'Remove unused audio').click();
    await fixture.whenStable();
    expect(api.remove).not.toHaveBeenCalled();
    button(host, 'Yes, request deletion').click();
    await fixture.whenStable();
    expect(api.remove).toHaveBeenCalledWith(row.id);
    expect(host.querySelector('audio')).toBeNull();
    fixture.destroy();
  });

  it('finishes queued removal and refreshes the library', async () => {
    api.list.mockResolvedValueOnce([{ ...row, storageState: 'Deleting' }]);
    const { fixture, host } = await open();
    expect(button(host, 'Listen to recording')).toBeUndefined();
    api.list.mockResolvedValue([]);
    button(host, 'Finish deletion').click();
    await fixture.whenStable();
    expect(api.finishDeletion).toHaveBeenCalledWith(row.id);
    expect(host.querySelector('.audio-card')).toBeNull();
    expect(host.textContent).toContain('Recording removed from private storage.');
  });

  it('keeps queued removal available for retry after a storage outage', async () => {
    api.list.mockResolvedValue([{ ...row, storageState: 'Deleting' }]);
    api.finishDeletion.mockRejectedValueOnce(new Error('Storage unavailable'));
    const { fixture, host } = await open();
    button(host, 'Finish deletion').click();
    await fixture.whenStable();
    expect(host.querySelector('.audio-card')).not.toBeNull();
    expect(button(host, 'Finish deletion').disabled).toBe(false);
    api.list.mockResolvedValue([]);
    button(host, 'Finish deletion').click();
    await fixture.whenStable();
    expect(api.finishDeletion).toHaveBeenCalledTimes(2);
    expect(host.querySelector('.audio-card')).toBeNull();
  });

  it('hides queued removal from another author', async () => {
    api.list.mockResolvedValue([
      { ...row, uploadedBy: 'another-author', storageState: 'Deleting' },
    ]);
    const { host } = await open();
    expect(button(host, 'Finish deletion')).toBeUndefined();
  });

  it('allows a scoped independent reviewer to enter notes for one recording', async () => {
    user.set({ id: 'reviewer', roles: ['Reviewer'] });
    content.courses.mockResolvedValue([{ ...courses[0], canAuthor: false }]);
    const { fixture, host } = await open();
    expect(host.querySelector('input[type=file]')).toBeNull();
    button(host, 'Review recording').click();
    await fixture.whenStable();
    const notes = host.querySelector<HTMLTextAreaElement>('.review-controls textarea')!;
    notes.value = 'Pronunciation and license checked.';
    notes.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    button(host, 'Approve audio').click();
    await fixture.whenStable();
    expect(api.review).toHaveBeenCalledWith(
      row.id,
      'approve',
      'Pronunciation and license checked.',
    );
  });

  it('clears private rows and ignores a late response after the account changes', async () => {
    let finish!: (rows: StaffMedia[]) => void;
    api.list.mockReturnValueOnce(
      new Promise<StaffMedia[]>((resolve) => {
        finish = resolve;
      }),
    );
    const fixture = TestBed.createComponent(MediaLibrary);
    fixture.detectChanges();
    user.set({ id: 'other-account', roles: ['ContentAuthor'] });
    fixture.detectChanges();
    finish([row]);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).querySelector('.audio-card')).toBeNull();
  });
});
