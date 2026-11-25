import { signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../../core/accounts/auth-store';
import { ExtraStudyApi } from '../../core/classrooms/extra-study-api';
import { StudyMessages } from './study-messages';

describe('Private study messages', () => {
  const owner = signal<{ id: string } | null>({ id: 'learner' });
  const api = { messages: vi.fn(), sendMessage: vi.fn() };
  beforeEach(() => {
    vi.resetAllMocks();
    owner.set({ id: 'learner' });
    api.messages.mockResolvedValue({ items: [], nextBefore: null });
    TestBed.configureTestingModule({
      imports: [StudyMessages],
      providers: [
        { provide: AuthStore, useValue: { user: owner } },
        { provide: ExtraStudyApi, useValue: api },
      ],
    });
  });
  afterEach(() => vi.restoreAllMocks());

  it('receives new replies automatically and pauses polling while the page is hidden', async () => {
    let refresh: (() => void) | undefined;
    const interval = window.setInterval.bind(window);
    vi.spyOn(window, 'setInterval').mockImplementation((callback, delay, ...args) => {
      if (delay === 10_000) {
        refresh = callback as () => void;
        return 0;
      }
      return interval(callback, delay, ...args);
    });
    const visibility = vi.spyOn(document, 'hidden', 'get').mockReturnValue(false);
    const fixture = TestBed.createComponent(StudyMessages);
    fixture.componentRef.setInput('requestId', 'request');
    await fixture.whenStable();
    api.messages.mockResolvedValue({
      items: [
        {
          id: 'reply',
          senderName: 'Teacher',
          body: 'Your new reply',
          createdAt: '2026-11-25T00:00:00Z',
          isMine: false,
        },
      ],
      nextBefore: null,
    });
    refresh!();
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Your new reply');
    const reads = api.messages.mock.calls.length;
    visibility.mockReturnValue(true);
    refresh!();
    await fixture.whenStable();
    expect(api.messages.mock.calls.length).toBe(reads);
  });

  it('clears message text and unsent drafts when the server revokes access', async () => {
    api.messages
      .mockResolvedValueOnce({
        items: [
          {
            id: 'private',
            senderName: 'Teacher',
            body: 'Private reply',
            createdAt: '2026-11-25T00:00:00Z',
            isMine: false,
          },
        ],
        nextBefore: null,
      })
      .mockRejectedValueOnce(new HttpErrorResponse({ status: 404 }));
    const fixture = TestBed.createComponent(StudyMessages);
    fixture.componentRef.setInput('requestId', 'request');
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const textarea = host.querySelector('textarea')!;
    textarea.value = 'Unsent private draft';
    textarea.dispatchEvent(new Event('input'));
    host.querySelector<HTMLButtonElement>('section > button')!.click();
    await fixture.whenStable();
    expect(host.textContent).not.toContain('Private reply');
    expect(textarea.value).toBe('');
    expect(textarea.readOnly).toBe(true);
    expect(host.querySelector('[role=alert]')?.textContent).toContain('no longer available');
  });
  it('retries an unconfirmed send with the same reference and clears private text on account change', async () => {
    api.sendMessage.mockRejectedValueOnce(new Error('Connection lost')).mockResolvedValueOnce({});
    const fixture = TestBed.createComponent(StudyMessages);
    fixture.componentRef.setInput('requestId', 'request');
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const textarea = host.querySelector('textarea')!;
    textarea.value = 'Private lesson question';
    textarea.dispatchEvent(new Event('input'));
    host.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();
    expect(textarea.readOnly).toBe(true);
    expect(host.textContent).toContain('Retry sending the same message');
    const first = api.sendMessage.mock.calls[0][1];
    host.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await fixture.whenStable();
    expect(api.sendMessage.mock.calls[1][1]).toEqual(first);
    expect(textarea.value).toBe('');
    textarea.value = 'Unsent private draft';
    textarea.dispatchEvent(new Event('input'));
    owner.set(null);
    await fixture.whenStable();
    expect(textarea.value).toBe('');
    expect(host.textContent).not.toContain('Private lesson question');
  });
});
