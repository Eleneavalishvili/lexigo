import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter, Router } from '@angular/router';
import { AuthStore } from '../../core/accounts/auth-store';
import { Login } from './login';

describe('Return to the requested page after sign-in', () => {
  for (const [destination, expected] of [
    ['/learn/course-id', '/learn/course-id'],
    ['/classes', '/classes'],
    ['/account/profile', '/account/profile'],
    ['/billing/receipt/payment-id', '/billing/receipt/payment-id'],
    ['/billing//untrusted.example', '/billing//untrusted.example'],
    ['/billing\\untrusted.example', '/learn'],
    ['https://untrusted.example/learn', '/learn'],
    ['//untrusted.example/learn', '/learn'],
  ]) {
    it(`uses a local destination safely: ${destination}`, async () => {
      TestBed.configureTestingModule({
        providers: [
          provideRouter([]),
          { provide: AuthStore, useValue: { login: vi.fn().mockResolvedValue({}) } },
          {
            provide: ActivatedRoute,
            useValue: {
              snapshot: { queryParamMap: convertToParamMap({ returnUrl: destination }) },
            },
          },
        ],
      });
      const navigation = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
      const fixture = TestBed.createComponent(Login);
      await fixture.whenStable();
      const host: HTMLElement = fixture.nativeElement;
      for (const [id, value] of [
        ['email', 'learner@example.test'],
        ['password', 'A long test passphrase 42!'],
      ]) {
        const input = host.querySelector<HTMLInputElement>('#login-' + id)!;
        input.value = value;
        input.dispatchEvent(new Event('input'));
      }
      host
        .querySelector('form')!
        .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
      await fixture.whenStable();
      expect(navigation).toHaveBeenCalledWith(expected);
    });
  }
});
