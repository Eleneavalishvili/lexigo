import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { signal } from '@angular/core';
import { AuthStore } from '../../core/accounts/auth-store';
import { EmailChangeApi } from '../../core/accounts/email-change-api';
import { ConfirmEmailChange } from './confirm-email-change';

describe('New email confirmation', () => {
  const confirm = vi.fn();
  const changed = vi.fn();
  const user = signal<{ id: string } | null>(null);
  beforeEach(() => {
    vi.clearAllMocks();
    user.set({ id: 'owner' });
    confirm.mockResolvedValue({});
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'confirm-email-change', component: ConfirmEmailChange }]),
        { provide: EmailChangeApi, useValue: { confirm } },
        { provide: AuthStore, useValue: { user, credentialsChanged: changed } },
      ],
    });
  });
  const url = '/confirm-email-change?userId=owner&requestId=request&token=private-test-token';

  it('waits for explicit action and clears tokens and the affected local account after success', async () => {
    const harness = await RouterTestingHarness.create(url);
    expect(confirm).not.toHaveBeenCalled();
    harness.routeNativeElement!.querySelector<HTMLButtonElement>('button')!.click();
    await harness.fixture.whenStable();
    harness.detectChanges();
    expect(confirm).toHaveBeenCalledWith('owner', 'request', 'private-test-token');
    expect(changed).toHaveBeenCalledOnce();
    expect(TestBed.inject(Router).url).toBe('/confirm-email-change');
    expect(harness.routeNativeElement!.textContent).toContain('email address is updated');
  });

  it('does not sign out an unrelated account viewing the link', async () => {
    user.set({ id: 'another' });
    const harness = await RouterTestingHarness.create(url);
    harness.routeNativeElement!.querySelector<HTMLButtonElement>('button')!.click();
    await harness.fixture.whenStable();
    expect(changed).not.toHaveBeenCalled();
  });

  it('keeps the link on transient errors for retry but removes invalid tokens', async () => {
    confirm.mockRejectedValueOnce(new HttpErrorResponse({ status: 503 }));
    const harness = await RouterTestingHarness.create(url);
    harness.routeNativeElement!.querySelector<HTMLButtonElement>('button')!.click();
    await harness.fixture.whenStable();
    harness.detectChanges();
    expect(TestBed.inject(Router).url).toContain('token=private-test-token');
    expect(harness.routeNativeElement!.textContent).toContain('try again shortly');
    confirm.mockRejectedValueOnce(new HttpErrorResponse({ status: 400 }));
    harness.routeNativeElement!.querySelector<HTMLButtonElement>('button')!.click();
    await harness.fixture.whenStable();
    harness.detectChanges();
    expect(TestBed.inject(Router).url).toBe('/confirm-email-change');
    expect(harness.routeNativeElement!.textContent).toContain('invalid, already used or expired');
    expect(changed).not.toHaveBeenCalled();
  });

  it('gives neutral guidance when the URL has no token', async () => {
    const harness = await RouterTestingHarness.create('/confirm-email-change');
    expect(harness.routeNativeElement!.textContent).toContain('Where to next?');
    expect(harness.routeNativeElement!.querySelector('button')).toBeNull();
    expect(confirm).not.toHaveBeenCalled();
  });
});
