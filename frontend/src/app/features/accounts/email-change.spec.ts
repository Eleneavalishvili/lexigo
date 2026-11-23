import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { EmailChangeApi } from '../../core/accounts/email-change-api';
import { ChangeEmail } from './change-email';

describe('Email change request', () => {
  const request = vi.fn();
  beforeEach(() => {
    vi.clearAllMocks();
    request.mockResolvedValue({});
    TestBed.configureTestingModule({
      imports: [ChangeEmail],
      providers: [provideRouter([]), { provide: EmailChangeApi, useValue: { request } }],
    });
  });

  it('requires email and password, then clears credentials only after acceptance', async () => {
    const fixture = TestBed.createComponent(ChangeEmail);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const submit = () =>
      host.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    submit();
    await fixture.whenStable();
    expect(request).not.toHaveBeenCalled();
    for (const [id, value] of [
      ['new-email', 'new@example.test'],
      ['email-current-password', 'Current password 42!'],
    ]) {
      const input = host.querySelector<HTMLInputElement>('#' + id)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    }
    submit();
    await fixture.whenStable();
    expect(request).toHaveBeenCalledWith('new@example.test', 'Current password 42!', '', '');
    expect(host.querySelector('form')).toBeNull();
    expect(host.querySelector('[role="status"]')?.textContent).toContain(
      'current email stays the same',
    );
    expect(host.textContent).not.toContain('Current password 42!');
  });

  it('keeps failures visible and never claims the address has already changed', async () => {
    request.mockRejectedValue(new Error('offline'));
    const fixture = TestBed.createComponent(ChangeEmail);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    for (const [id, value] of [
      ['new-email', 'new@example.test'],
      ['email-current-password', 'Current password 42!'],
    ]) {
      const input = host.querySelector<HTMLInputElement>('#' + id)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    }
    host.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('couldn’t request');
    expect(host.querySelector('form')).not.toBeNull();
    expect(host.querySelector('[role="status"]')).toBeNull();
  });
});
