import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Subject } from 'rxjs';
import { RegistrationApi } from '../../core/accounts/registration-api';
import { VerifyEmail } from './verify-email';

describe('Email confirmation', () => {
  let response: Subject<{ message: string }>;
  const api = { confirmEmail: vi.fn() };

  beforeEach(() => {
    response = new Subject();
    api.confirmEmail.mockReset().mockReturnValue(response);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'verify-email', component: VerifyEmail }]),
        { provide: RegistrationApi, useValue: api },
      ],
    });
  });

  async function open(link = '/verify-email?userId=test-user&token=private-test-token') {
    return RouterTestingHarness.create(link);
  }

  function confirm(harness: RouterTestingHarness): void {
    harness.routeNativeElement!.querySelector<HTMLButtonElement>('button')!.click();
    harness.detectChanges();
  }

  it('requires an explicit action, ignores repeated clicks and links successful confirmation to sign-in', async () => {
    const harness = await open();
    expect(api.confirmEmail).not.toHaveBeenCalled();
    confirm(harness);
    confirm(harness);
    expect(api.confirmEmail).toHaveBeenCalledOnce();
    response.next({ message: 'Confirmed' });
    response.complete();
    await harness.fixture.whenStable();
    harness.detectChanges();
    expect(harness.routeNativeElement!.textContent).toContain('Your email address is verified');
    expect(harness.routeNativeElement!.querySelector('a.button')?.getAttribute('href')).toBe(
      '/login',
    );
    expect(TestBed.inject(Router).url).not.toContain('private-test-token');
  });

  it.each([
    ['email_already_confirmed', 'Already confirmed.'],
    ['invalid_confirmation', 'This link is invalid or has expired.'],
  ])('handles the verified server outcome %s', async (code, expected) => {
    const harness = await open();
    confirm(harness);
    response.error(new HttpErrorResponse({ status: 400, error: { code } }));
    await harness.fixture.whenStable();
    harness.detectChanges();
    expect(harness.routeNativeElement!.textContent).toContain(expected);
    expect(TestBed.inject(Router).url).not.toContain('private-test-token');
    if (code === 'invalid_confirmation')
      expect(harness.routeNativeElement!.textContent).not.toContain('Already confirmed.');
  });

  it.each([0, 503, 429])('allows retry after a temporary failure (%s)', async (status) => {
    const harness = await open();
    confirm(harness);
    response.error(new HttpErrorResponse({ status }));
    await harness.fixture.whenStable();
    harness.detectChanges();
    expect(harness.routeNativeElement!.textContent).not.toContain('This link is invalid');
    expect(TestBed.inject(Router).url).toContain('private-test-token');
    const retry = new Subject<{ message: string }>();
    api.confirmEmail.mockReturnValue(retry);
    confirm(harness);
    expect(api.confirmEmail).toHaveBeenCalledTimes(2);
    retry.next({ message: 'Confirmed' });
    retry.complete();
    await harness.fixture.whenStable();
    harness.detectChanges();
    expect(harness.routeNativeElement!.textContent).toContain('Your email address is verified');
  });

  it('shows neutral guidance after a refresh without a token instead of claiming a failed verification', async () => {
    const harness = await open('/verify-email');
    expect(harness.routeNativeElement!.textContent).toContain(
      'If you’ve already confirmed your email',
    );
    expect(harness.routeNativeElement!.textContent).not.toContain('This link is invalid');
    expect(api.confirmEmail).not.toHaveBeenCalled();
  });
});
