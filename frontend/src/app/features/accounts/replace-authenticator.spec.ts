import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { TwoFactorApi } from '../../core/accounts/two-factor-api';
import { AuthenticatorReplacementApi } from '../../core/accounts/authenticator-replacement-api';
import { ReplaceAuthenticator } from './replace-authenticator';

describe('Authenticator replacement', () => {
  const begin = vi.fn();
  const confirm = vi.fn();
  const changed = vi.fn();
  const status = vi.fn();
  beforeEach(() => {
    vi.clearAllMocks();
    status.mockResolvedValue({ enabled: true, recoveryCodesRemaining: 1 });
    begin.mockResolvedValue({
      requestId: 'pending-request',
      sharedKey: 'TEST-NEW-KEY',
      expiresAt: '2026-10-27T18:00:00Z',
    });
    confirm.mockResolvedValue({ recoveryCodes: ['one-time-test-code'] });
    TestBed.configureTestingModule({
      imports: [ReplaceAuthenticator],
      providers: [
        { provide: TwoFactorApi, useValue: { status } },
        provideRouter([]),
        { provide: AuthenticatorReplacementApi, useValue: { begin, confirm } },
        { provide: AuthStore, useValue: { credentialsChanged: changed } },
      ],
    });
  });

  it('directs accounts without an authenticator to enrollment instead of accepting a replacement', async () => {
    status.mockResolvedValue({ enabled: false, recoveryCodesRemaining: 0 });
    const fixture = TestBed.createComponent(ReplaceAuthenticator);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Set up your first one');
    expect(host.querySelector('form')).toBeNull();
    expect(begin).not.toHaveBeenCalled();
  });

  const fill = (host: HTMLElement, selector: string, value: string) => {
    const input = host.querySelector<HTMLInputElement>(selector)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  };
  const submit = (host: HTMLElement) =>
    host.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));

  it('stages a new key and shows fresh recovery codes only after verification', async () => {
    const fixture = TestBed.createComponent(ReplaceAuthenticator);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    fill(host, '#replacement-password', 'Current test password 42!');
    submit(host);
    await fixture.whenStable();
    expect(begin).toHaveBeenCalledWith('Current test password 42!');
    expect(confirm).not.toHaveBeenCalled();
    expect(host.querySelector('.shared-key')?.textContent).toContain('TEST-NEW-KEY');
    expect(host.querySelector('.recovery-codes')).toBeNull();
    fill(host, '#replacement-code', '123456');
    submit(host);
    await fixture.whenStable();
    expect(confirm).toHaveBeenCalledWith('pending-request', 'Current test password 42!', '123456');
    expect(changed).toHaveBeenCalledOnce();
    expect(host.querySelector('.recovery-codes')?.textContent).toContain('one-time-test-code');
    expect(host.querySelector('input')).toBeNull();
    expect(host.querySelector('.shared-key')).toBeNull();
    expect(host.textContent).toContain('Two-factor authentication is still on');
  });

  it('requires a current password and the new app code before sending requests', async () => {
    const fixture = TestBed.createComponent(ReplaceAuthenticator);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    submit(host);
    await fixture.whenStable();
    expect(begin).not.toHaveBeenCalled();
    fill(host, '#replacement-password', 'Current test password 42!');
    submit(host);
    await fixture.whenStable();
    submit(host);
    await fixture.whenStable();
    expect(confirm).not.toHaveBeenCalled();
    fill(host, '#replacement-code', 'abc123');
    submit(host);
    await fixture.whenStable();
    expect(confirm).not.toHaveBeenCalled();
    expect(changed).not.toHaveBeenCalled();
  });

  it('preserves setup for a retry when confirmation fails', async () => {
    confirm.mockRejectedValue(new Error('invalid'));
    const fixture = TestBed.createComponent(ReplaceAuthenticator);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    fill(host, '#replacement-password', 'Current test password 42!');
    submit(host);
    await fixture.whenStable();
    fill(host, '#replacement-code', '123456');
    submit(host);
    await fixture.whenStable();
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('couldn’t complete');
    expect(host.querySelector('.shared-key')).not.toBeNull();
    expect(host.querySelector('.recovery-codes')).toBeNull();
    expect(changed).not.toHaveBeenCalled();
  });

  it('does not expose a setup key after the server rejects an old or invalid sign-in', async () => {
    begin.mockRejectedValue(new Error('reauthentication required'));
    const fixture = TestBed.createComponent(ReplaceAuthenticator);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    fill(host, '#replacement-password', 'Current test password 42!');
    submit(host);
    await fixture.whenStable();
    expect(host.querySelector('[role="alert"]')?.textContent).toContain('five minutes');
    expect(host.querySelector('.shared-key')).toBeNull();
    expect(confirm).not.toHaveBeenCalled();
  });
});
