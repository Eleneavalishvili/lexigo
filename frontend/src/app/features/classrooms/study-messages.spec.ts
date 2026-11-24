import { signal } from '@angular/core';
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
