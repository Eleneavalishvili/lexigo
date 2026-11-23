import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AccountApi } from '../../core/accounts/account-api';
import { AuthStore } from '../../core/accounts/auth-store';
import { DeleteAccount } from './delete-account';

describe('Account deletion confirmation', () => {
  it('requires explicit confirmation and clears credentials after successful deletion', async () => {
    const deletion = vi.fn().mockResolvedValue(undefined);
    const clear = vi.fn();
    TestBed.configureTestingModule({
      imports: [DeleteAccount],
      providers: [
        provideRouter([]),
        {
          provide: AccountApi,
          useValue: {
            deleteAccount: deletion,
            deletionResponsibilities: vi.fn().mockResolvedValue([]),
          },
        },
        { provide: AuthStore, useValue: { credentialsChanged: clear } },
      ],
    });
    const fixture = TestBed.createComponent(DeleteAccount);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const submit = host.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    expect(submit.disabled).toBe(true);
    const type = async (id: string, value: string) => {
      const input = host.querySelector<HTMLInputElement>('#' + id)!;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      await fixture.whenStable();
    };
    await type('delete-password', 'Synthetic test password');
    expect(submit.disabled).toBe(true);
    await type('delete-confirmation', 'DELETE');
    await type('delete-recovery', 'synthetic-code');
    submit.click();
    await fixture.whenStable();
    expect(deletion).toHaveBeenCalledExactlyOnceWith({
      currentPassword: 'Synthetic test password',
      confirmation: 'DELETE',
      authenticatorCode: '',
      recoveryCode: 'synthetic-code',
    });
    expect(clear).toHaveBeenCalledOnce();
    expect(host.textContent).toContain('Your account has been deleted.');
    expect(host.querySelector('form')).toBeNull();
  });
});
