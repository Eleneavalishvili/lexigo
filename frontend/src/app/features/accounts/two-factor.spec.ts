import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { TwoFactorApi } from '../../core/accounts/two-factor-api';
import { TwoFactor } from './two-factor';

describe('Two-factor settings', () => {
  const account = signal({ roles: ['Learner'] });
  const changed = vi.fn();
  const api = {
    status: vi.fn(),
    setup: vi.fn(),
    enable: vi.fn(),
    regenerate: vi.fn(),
    disable: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    account.set({ roles: ['Learner'] });
    api.status.mockResolvedValue({ enabled: false, recoveryCodesRemaining: 0 });
    TestBed.configureTestingModule({
      imports: [TwoFactor],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { user: account, credentialsChanged: changed } },
        { provide: TwoFactorApi, useValue: api },
      ],
    });
  });

  it('keeps setup separate from activation and shows recovery codes only after server confirmation', async () => {
    api.setup.mockResolvedValue({
      sharedKey: 'TEST-SETUP-KEY',
      authenticatorUri: 'otpauth://test',
    });
    api.enable.mockResolvedValue({ recoveryCodes: ['abcde-fghij', 'klmno-pqrst'] });
    const fixture = TestBed.createComponent(TwoFactor);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const password = host.querySelector<HTMLInputElement>('#security-password')!;
    password.value = 'Current password 42!';
    password.dispatchEvent(new Event('input'));
    host.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(api.setup).toHaveBeenCalledWith('Current password 42!');
    expect(api.enable).not.toHaveBeenCalled();
    expect(host.querySelector('.shared-key')?.textContent).toContain('TEST-SETUP-KEY');
    expect(host.querySelector('.recovery-codes')).toBeNull();
    const code = host.querySelector<HTMLInputElement>('#security-code')!;
    code.value = '123456';
    code.dispatchEvent(new Event('input'));
    host.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(api.enable).toHaveBeenCalledWith('Current password 42!', '123456');
    expect(changed).toHaveBeenCalledOnce();
    expect(host.querySelector('.shared-key')).toBeNull();
    expect(host.querySelector('.recovery-codes')?.textContent).toContain('abcde-fghij');
    expect(host.querySelector('input')).toBeNull();
  });

  it('keeps the authenticator mandatory for privileged accounts', async () => {
    account.set({ roles: ['Learner', 'Admin'] });
    api.status.mockResolvedValue({ enabled: true, recoveryCodesRemaining: 10 });
    const fixture = TestBed.createComponent(TwoFactor);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.textContent).toContain('Privileged accounts must keep');
    expect(host.textContent).not.toContain('Turn off two-factor authentication');
  });
});
