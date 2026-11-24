import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AccountApi } from '../../core/accounts/account-api';
import { ForgotPassword } from './forgot-password';

describe('Password recovery request', () => {
  const forgot = vi.fn();
  beforeEach(() => {
    forgot.mockReset();
    TestBed.configureTestingModule({
      imports: [ForgotPassword],
      providers: [provideRouter([]), { provide: AccountApi, useValue: { forgot } }],
    });
  });
  async function form() {
    const fixture = TestBed.createComponent(ForgotPassword);
    await fixture.whenStable();
    const host: HTMLElement = fixture.nativeElement;
    const email = host.querySelector<HTMLInputElement>('input[type=email]')!;
    email.value = 'learner@example.test';
    email.dispatchEvent(new Event('input'));
    return {
      fixture,
      host,
      submit: () =>
        host.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true })),
    };
  }
  it('keeps duplicate submissions disabled until the request finishes', async () => {
    let finish!: () => void;
    forgot.mockImplementation(
      () =>
        new Promise<void>((resolve) => {
          finish = resolve;
        }),
    );
    const { fixture, host, submit } = await form();
    submit();
    fixture.detectChanges();
    expect(host.querySelector<HTMLButtonElement>('button[type=submit]')!.disabled).toBe(true);
    submit();
    expect(forgot).toHaveBeenCalledTimes(1);
    finish();
    await fixture.whenStable();
    expect(host.querySelector('[role=status]')?.textContent).toContain(
      'Your password hasn’t changed',
    );
    expect(host.querySelector('form')).toBeNull();
  });
  it('uses the same confirmation for a non-existing account', async () => {
    forgot.mockResolvedValue({});
    const { fixture, host, submit } = await form();
    submit();
    await fixture.whenStable();
    expect(host.querySelector('[role=status]')?.textContent).toContain(
      'If this account can be recovered',
    );
  });
  it('shows a retryable failure without claiming email was sent', async () => {
    forgot.mockRejectedValue(new Error('Unavailable'));
    const { fixture, host, submit } = await form();
    submit();
    await fixture.whenStable();
    expect(host.querySelector('[role=alert]')?.textContent).toContain(
      'We couldn’t send that request',
    );
    expect(host.querySelector('[role=status]')).toBeNull();
    expect(host.querySelector<HTMLButtonElement>('button[type=submit]')!.disabled).toBe(false);
  });
});
